using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain.Media;
using PetPal.Api.PetBrain.Scenery;
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
///
///   <item><b>Gündəlik kredit tavanı sətir açılmazdan ƏVVƏL yoxlanılır.</b>
///   Tapmaca səhnəsi, macəra arxa fonu və obraz eyni büdcədən xərcləyir, ona
///   görə hədd də birdir: dolubsa sətir birbaşa «Fallback» açılır, heç bir iş
///   növbəyə düşmür və uşaq deterministik səhnə ilə oynamağa davam edir.</item>
/// </list>
///
/// <para>Qat səhnənin NÖVÜNÜ tanımır: tapmaca rəsmi də, arxa fon da, obraz da
/// eyni <see cref="IStoryScene"/> müqaviləsindədir.</para>
/// </summary>
public sealed class PuzzleIllustrationCoordinator
{
    private readonly AppDbContext _db;
    private readonly IPuzzleIllustrationProvider _provider;
    private readonly IPuzzleIllustrationStore _store;
    private readonly PetBrainMediaOptions _media;
    private readonly TimeProvider _clock;
    private readonly ILogger<PuzzleIllustrationCoordinator> _logger;

    public PuzzleIllustrationCoordinator(
        AppDbContext db,
        IPuzzleIllustrationProvider provider,
        IPuzzleIllustrationStore store,
        IOptions<PetBrainMediaOptions> media,
        TimeProvider clock,
        ILogger<PuzzleIllustrationCoordinator> logger)
    {
        _db = db;
        _provider = provider;
        _store = store;
        _media = media.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Səhnə sətrini AÇIR (yoxdursa yaradır) və hazır olub-olmadığını qaytarır.
    ///
    /// <para>Model burada çağırılmır — bu metod sürətli olmalıdır, çünki
    /// tapmacanı göstərən sorğunun içindədir.</para>
    /// </summary>
    public async Task<PuzzleIllustration> EnsureRowAsync(IStoryScene scene, CancellationToken ct)
    {
        var hash = scene.Hash();

        var existing = await _db.PuzzleIllustrations
            .FirstOrDefaultAsync(i => i.SceneSpecHash == hash, ct);

        if (existing is not null)
            return await ReopenIfNowEnabledAsync(existing, ct);

        // AI bağlıdırsa sətir dərhal "Fallback" kimi yazılır: uşaq gözləmir,
        // biz isə hər sorğuda yenidən yoxlamırıq.
        var denial = await DenialAsync(ct);

        var row = new PuzzleIllustration
        {
            Id = Guid.NewGuid(),
            SceneSpecHash = hash,
            BlueprintKey = scene.SceneKey,
            Status = denial.Length == 0
                ? PetBrainIllustrationStatus.Pending
                : PetBrainIllustrationStatus.Fallback,
            PromptTemplateVersion = scene.PromptVersion,
            FailureReason = denial,
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
    public async Task RenderAsync(string sceneSpecHash, IStoryScene scene, CancellationToken ct)
    {
        if (!_provider.IsEnabled)
            return;

        var row = await _db.PuzzleIllustrations.FirstOrDefaultAsync(i => i.SceneSpecHash == sceneSpecHash, ct);

        if (row is null || row.Status != PetBrainIllustrationStatus.Pending)
            return;

        var prompt = scene.BuildPrompt();

        row.PromptHash = StoryScenePrompt.Fingerprint(prompt);
        row.PromptTemplateVersion = scene.PromptVersion;

        PuzzleIllustrationResult result;

        try
        {
            // DİQQƏT: burada açıq tranzaksiya YOXDUR. Model saniyələrlə
            // gecikə bilər, baza bağlantısı isə o müddətdə tutulmamalıdır.
            result = await _provider.RenderAsync(scene, prompt, ct);
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

    /// <summary>
    /// Provayderə HEÇ ÇATMAMIŞ ehtiyat səhnəsini yenidən açır.
    ///
    /// <para>Səhnə hash-ı uşaqdan asılı deyil və əbədi keşlənir. AI bağlı ikən
    /// yazılan «Fallback» sətri olduğu kimi qalsaydı, açar sonradan qoşulanda
    /// həmin səhnələr heç vaxt çəkilməzdi. Provayderə çatmış və ya rədd olunmuş
    /// səhnəyə toxunulmur — o, ikinci pullu sorğu olardı.</para>
    ///
    /// <para><b>Faylı itmiş hazır səhnə</b> də yenidən açılır. Sətir «hazır»
    /// desə də şəkil saxlancda yoxdursa (disk təmizlənib, paket yenidən
    /// qurulub), keş onu əbədi «hazır» saxlayardı: tapmaca həmişə sadə kadrda
    /// qalar, recap isə ilk kadrını tapmazdı. Belə səhnə növbəti dəfə lazım
    /// olanda bir dəfə yenidən çəkilir — gündəlik hədd doludursa sabah.</para>
    /// </summary>
    private async Task<PuzzleIllustration> ReopenIfNowEnabledAsync(PuzzleIllustration row, CancellationToken ct)
    {
        var lost = await FileLostAsync(row, ct);

        if (!lost &&
            (row.Status != PetBrainIllustrationStatus.Fallback ||
             !MediaFailure.NeverReachedProvider(row.FailureReason)))
            return row;

        var now = _clock.GetUtcNow().UtcDateTime;
        var denial = await DenialAsync(ct);

        if (denial.Length > 0)
        {
            if (!lost)
                return row;

            row.Status = PetBrainIllustrationStatus.Fallback;
            row.FailureReason = denial;
            row.AssetKey = string.Empty;
            row.CompletedAt = now;

            await _db.SaveChangesAsync(ct);

            return row;
        }

        if (lost)
            _logger.LogWarning(
                "PetBrain: hazır səhnənin faylı yoxdur ({Hash}) — səhnə yenidən çəkiləcək.", row.SceneSpecHash);

        row.Status = PetBrainIllustrationStatus.Pending;
        row.FailureReason = string.Empty;
        row.Provider = string.Empty;
        row.Model = string.Empty;
        row.AssetKey = string.Empty;
        row.RequestedAt = now;
        row.CompletedAt = null;

        await _db.SaveChangesAsync(ct);

        return row;
    }

    /// <summary>Sətir hazırdır, amma faylı saxlancda yoxdur.</summary>
    private async Task<bool> FileLostAsync(PuzzleIllustration row, CancellationToken ct) =>
        row.Status == PetBrainIllustrationStatus.Ready &&
        (row.AssetKey.Length == 0 || await _store.OpenAsync(row.AssetKey, ct) is null);

    /// <summary>
    /// Pullu iş bu an ümumiyyətlə başlaya bilərmi — boş sətir «başlaya bilər»
    /// deməkdir.
    ///
    /// <para>Səbəb sətirdə saxlanılır və hamısı <see cref="MediaFailure"/>-in
    /// «provayderə heç çatmadı» siyahısındadır: hədd sabahkı gün sıfırlananda
    /// və ya açar sonradan qoşulanda həmin səhnə yenidən açılır.</para>
    /// </summary>
    private async Task<string> DenialAsync(CancellationToken ct)
    {
        if (!_provider.IsEnabled)
            return "disabled";

        return await PaidTodayAsync(ct) >= Math.Max(0, _media.MaxPaidScenesPerDay)
            ? "global-daily-quota"
            : string.Empty;
    }

    /// <summary>
    /// Bu gün PROVAYDERƏ çatmış səhnələrin sayı.
    ///
    /// <para>Ehtiyata düşən, amma heç vaxt göndərilməmiş sətirlər sayılmır —
    /// onlar pul xərcləməyib. Sayğac səhnənin növünə baxmır: büdcə birdir.</para>
    /// </summary>
    private Task<int> PaidTodayAsync(CancellationToken ct)
    {
        var since = _clock.GetUtcNow().UtcDateTime.Date;

        return _db.PuzzleIllustrations.CountAsync(
            i => i.RequestedAt >= since &&
                 (i.Status != PetBrainIllustrationStatus.Fallback ||
                  !MediaFailure.NotAttemptedReasons.Contains(i.FailureReason)),
            ct);
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
