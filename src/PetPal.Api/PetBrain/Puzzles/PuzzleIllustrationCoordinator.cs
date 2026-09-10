using Microsoft.EntityFrameworkCore;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// Rəsmin vəziyyətini idarə edən qat.
///
/// <para>Üç sərt qayda burada saxlanılır:</para>
/// <list type="number">
///   <item><b>Bir səhnə → bir pullu sorğu.</b> Təminat bazadadır:
///   <c>SceneSpecHash</c> üzərində UNİKAL indeks. İki eyni vaxtlı sorğudan
///   ikincisi <c>DbUpdateException</c> alır və mövcud sətri oxuyur — yaddaşdakı
///   "artıq işləyir" yoxlaması proseslər arasında işləməzdi.</item>
///
///   <item><b>Model çağırışı tranzaksiyadan KƏNARDADIR.</b> Sətir əvvəlcə
///   <c>Pending</c> yazılır, bağlantı buraxılır, yalnız sonra model çağırılır.</item>
///
///   <item><b>Uğursuzluq görünmür.</b> Nə timeout, nə pozuq bayt, nə də
///   moderasiya rəddi uşağa çatır — ekranda onsuz da tam oynanan deterministik
///   səhnə var.</item>
/// </list>
/// </summary>
public sealed class PuzzleIllustrationCoordinator
{
    private readonly AppDbContext _db;
    private readonly IPuzzleIllustrationProvider _provider;
    private readonly IPuzzleIllustrationStore _store;
    private readonly TimeProvider _clock;
    private readonly ILogger<PuzzleIllustrationCoordinator> _logger;

    public PuzzleIllustrationCoordinator(
        AppDbContext db,
        IPuzzleIllustrationProvider provider,
        IPuzzleIllustrationStore store,
        TimeProvider clock,
        ILogger<PuzzleIllustrationCoordinator> logger)
    {
        _db = db;
        _provider = provider;
        _store = store;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Səhnə sətrini AÇIR (yoxdursa yaradır) və hazır olub-olmadığını qaytarır.
    ///
    /// <para>Model burada çağırılmır — bu metod sürətli olmalıdır, çünki
    /// tapmacanı göstərən sorğunun içindədir.</para>
    /// </summary>
    public async Task<PuzzleIllustration> EnsureRowAsync(PuzzleSceneSpec spec, CancellationToken ct)
    {
        var hash = spec.Hash();

        var existing = await _db.PuzzleIllustrations
            .FirstOrDefaultAsync(i => i.SceneSpecHash == hash, ct);

        if (existing is not null)
            return existing;

        // AI bağlıdırsa sətir dərhal "Fallback" kimi yazılır: uşaq gözləmir,
        // biz isə hər sorğuda yenidən yoxlamırıq.
        var row = new PuzzleIllustration
        {
            Id = Guid.NewGuid(),
            SceneSpecHash = hash,
            BlueprintKey = spec.BlueprintKey,
            Status = _provider.IsEnabled
                ? PetBrainIllustrationStatus.Pending
                : PetBrainIllustrationStatus.Fallback,
            PromptTemplateVersion = SafePuzzleIllustrationPromptBuilder.TemplateVersion,
            FailureReason = _provider.IsEnabled ? string.Empty : "disabled",
            RequestedAt = _clock.GetUtcNow().UtcDateTime
        };

        _db.PuzzleIllustrations.Add(row);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Başqa sorğu qabaqladı — öz namizədimizi atıb onunkunu oxuyuruq.
            // Beləliklə eyni səhnə üçün ikinci pullu iş BAŞLAMIR.
            _db.Entry(row).State = EntityState.Detached;

            return await _db.PuzzleIllustrations.FirstAsync(i => i.SceneSpecHash == hash, ct);
        }

        return row;
    }

    /// <summary>
    /// Gözləyən səhnəni ÇƏKDİRİR. Bu metod arxa fon işçisindən çağırılır —
    /// uşağın sorğusu onu gözləmir.
    /// </summary>
    public async Task RenderAsync(string sceneSpecHash, PuzzleSceneSpec spec, CancellationToken ct)
    {
        if (!_provider.IsEnabled)
            return;

        var row = await _db.PuzzleIllustrations.FirstOrDefaultAsync(i => i.SceneSpecHash == sceneSpecHash, ct);

        if (row is null || row.Status != PetBrainIllustrationStatus.Pending)
            return;

        var prompt = SafePuzzleIllustrationPromptBuilder.Build(spec);

        row.PromptHash = SafePuzzleIllustrationPromptBuilder.HashOf(prompt);
        row.PromptTemplateVersion = SafePuzzleIllustrationPromptBuilder.TemplateVersion;

        PuzzleIllustrationResult result;

        try
        {
            // DİQQƏT: burada açıq tranzaksiya YOXDUR. Model saniyələrlə
            // gecikə bilər, baza bağlantısı isə o müddətdə tutulmamalıdır.
            result = await _provider.RenderAsync(spec, prompt, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Provayderin diaqnostikası uşağa çatmır və ekranı dəyişmir.
            _logger.LogWarning(ex, "PetBrain: səhnə rəsmi alınmadı ({Hash}).", sceneSpecHash);
            result = PuzzleIllustrationResult.Failed("provider-exception");
        }

        if (!result.Succeeded)
        {
            Reject(row, result.Reason, result.Provider, result.Model);
            await _db.SaveChangesAsync(ct);
            return;
        }

        // Provayder "image/png" desə də baytların ÖZÜ yoxlanılır.
        var check = PuzzleIllustrationValidator.Validate(result.Bytes);

        if (!check.IsValid)
        {
            Reject(row, check.Reason, result.Provider, result.Model);
            await _db.SaveChangesAsync(ct);
            return;
        }

        row.AssetKey = await _store.SaveAsync(sceneSpecHash, result.Bytes!, check.ContentType, ct);
        row.ContentType = check.ContentType;
        row.Width = check.Width;
        row.Height = check.Height;
        row.Provider = result.Provider;
        row.Model = result.Model;
        row.Status = PetBrainIllustrationStatus.Ready;
        row.FailureReason = string.Empty;
        row.CompletedAt = _clock.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
    }

    private void Reject(PuzzleIllustration row, string reason, string provider, string model)
    {
        row.Status = PetBrainIllustrationStatus.Fallback;
        row.FailureReason = reason;
        row.Provider = provider;
        row.Model = model;
        row.CompletedAt = _clock.GetUtcNow().UtcDateTime;

        _logger.LogInformation(
            "PetBrain: səhnə rəsmi rədd edildi ({Hash}, səbəb {Reason}) — deterministik səhnə qalır.",
            row.SceneSpecHash, reason);
    }
}
