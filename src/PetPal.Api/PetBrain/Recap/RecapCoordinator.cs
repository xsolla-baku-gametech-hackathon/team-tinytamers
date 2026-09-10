using Microsoft.EntityFrameworkCore;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain.Media;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Recap;

/// <summary>
/// Recap işinin ömür dövrünü idarə edir.
///
/// <para>Dörd sərt qayda burada saxlanılır:</para>
/// <list type="number">
///   <item><b>Mükafat heç vaxt gözləmir.</b> Sətir tamamlama tranzaksiyasından
///   SONRA yaradılır; XP, bağ, xatirə və kosmetik onsuz da verilib.</item>
///
///   <item><b>Bir seçim dəsti → bir pullu iş.</b> Təminat bazadadır:
///   <c>RecapSpecHash</c> üzərində UNİKAL indeks.</item>
///
///   <item><b>Yenidən başlatma pul xərcləmir.</b> Tapşırıq id-si saxlanılır;
///   proses qayıdanda həmin işi İZLƏYİR, yenisini yaratmır.</item>
///
///   <item><b>Kvota və dövrə kəsicisi çağırışdan ƏVVƏL.</b> Gündəlik hədd
///   dolubsa iş başlamır və uşaq deterministik recap görür.</item>
/// </list>
/// </summary>
public sealed class RecapCoordinator
{
    /// <summary>Bir səhnə üçün ən çox neçə cəhd — sonsuz təkrar olmasın.</summary>
    private const int MaxAttempts = 3;

    private readonly AppDbContext _db;
    private readonly IRecapVideoProvider _provider;
    private readonly IRecapVideoStore _store;
    private readonly IPuzzleIllustrationStore _illustrations;
    private readonly PetBrainMediaCostPolicy _cost;
    private readonly MediaCircuitBreaker _breaker;
    private readonly TimeProvider _clock;
    private readonly ILogger<RecapCoordinator> _logger;

    public RecapCoordinator(
        AppDbContext db,
        IRecapVideoProvider provider,
        IRecapVideoStore store,
        IPuzzleIllustrationStore illustrations,
        PetBrainMediaCostPolicy cost,
        MediaCircuitBreaker breaker,
        TimeProvider clock,
        ILogger<RecapCoordinator> logger)
    {
        _db = db;
        _provider = provider;
        _store = store;
        _illustrations = illustrations;
        _cost = cost;
        _breaker = breaker;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Recap sətrini AÇIR (yoxdursa yaradır).
    ///
    /// <para>Provayder çağırılmır — bu metod tamamlama cavabının içindədir və
    /// sürətli olmalıdır.</para>
    /// </summary>
    public async Task<AdventureRecap> EnsureAsync(AdventureRecapSpec spec, CancellationToken ct)
    {
        var hash = spec.Hash();

        var existing = await _db.AdventureRecaps.FirstOrDefaultAsync(r => r.RecapSpecHash == hash, ct);

        if (existing is not null)
            return existing;

        var now = _clock.GetUtcNow().UtcDateTime;

        // Kvota sətir yaradılmazdan ƏVVƏL yoxlanılır: hədd dolubsa sətir
        // birbaşa "Fallback" kimi açılır və heç bir iş növbəyə düşmür.
        var allowed = _provider.IsEnabled && await WithinQuotaAsync(spec.ChildProfileId, now, ct);

        var row = new AdventureRecap
        {
            Id = Guid.NewGuid(),
            RecapSpecHash = hash,
            ChildProfileId = spec.ChildProfileId,
            ExperienceRunId = spec.RunId,
            ExperienceKey = spec.ExperienceKey,
            SpecVersion = spec.SpecVersion,
            Status = allowed ? PetBrainRecapStatus.Pending : PetBrainRecapStatus.Fallback,
            PromptTemplateVersion = SafeRecapPromptBuilder.TemplateVersion,
            FailureReason = allowed ? string.Empty : QuotaReason(),
            RequestedAt = now
        };

        _db.AdventureRecaps.Add(row);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Başqa sorğu qabaqladı — öz namizədimizi atıb onunkunu oxuyuruq.
            // Beləliklə eyni seçim dəsti üçün ikinci iş BAŞLAMIR.
            _db.Entry(row).State = EntityState.Detached;

            return await _db.AdventureRecaps.FirstAsync(r => r.RecapSpecHash == hash, ct);
        }

        return row;
    }

    /// <summary>
    /// İşi bir addım irəli aparır: başlamayıbsa başladır, gedirsə izləyir.
    ///
    /// <para>Arxa fon işçisindən çağırılır — uşağın sorğusu bunu gözləmir.
    /// Baza tranzaksiyası model gözləyərkən AÇIQ QALMIR.</para>
    /// </summary>
    public async Task AdvanceAsync(AdventureRecapSpec spec, CancellationToken ct)
    {
        var hash = spec.Hash();
        var row = await _db.AdventureRecaps.FirstOrDefaultAsync(r => r.RecapSpecHash == hash, ct);

        if (row is null)
            return;

        switch (row.Status)
        {
            case PetBrainRecapStatus.Pending:
                await StartAsync(row, spec, ct);
                return;

            case PetBrainRecapStatus.Generating:
                await PollAsync(row, ct);
                return;

            default:
                return;
        }
    }

    private async Task StartAsync(AdventureRecap row, AdventureRecapSpec spec, CancellationToken ct)
    {
        if (!_provider.IsEnabled)
        {
            Settle(row, PetBrainRecapStatus.Fallback, "disabled");
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (row.Attempts >= MaxAttempts)
        {
            Settle(row, PetBrainRecapStatus.Fallback, "attempts-exhausted");
            await _db.SaveChangesAsync(ct);
            return;
        }

        var shots = RecapStoryboard.Build(spec);
        var prompt = SafeRecapPromptBuilder.Build(spec, shots);

        row.PromptHash = SafeRecapPromptBuilder.HashOf(prompt);
        row.PromptTemplateVersion = SafeRecapPromptBuilder.TemplateVersion;
        row.Attempts++;

        // Tapmacanın hazır rəsmi videonun ilk kadrıdır — ikinci referans kadr
        // GENERASİYA OLUNMUR, yəni artıq şəkil xərci yoxdur.
        var reference = await ReferenceImageAsync(spec, ct);

        var estimate = _cost.ForVideo();
        row.EstimatedCredits = estimate.Allowed ? estimate.Credits : 0;

        var started = await _provider.StartAsync(spec, prompt, reference, ct);

        if (!started.Started)
        {
            Settle(row, PetBrainRecapStatus.Fallback, started.Reason);
            await _db.SaveChangesAsync(ct);
            return;
        }

        row.Status = PetBrainRecapStatus.Generating;
        row.ProviderJobId = started.JobId;
        row.Provider = started.Provider;
        row.Model = started.Model;
        row.StartedAt = _clock.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
    }

    private async Task PollAsync(AdventureRecap row, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        // Ümumi vaxt həddi: iş sonsuza qədər izlənmir.
        if (row.StartedAt is { } started &&
            (now - started).TotalSeconds > _cost.Options.GenerationDeadlineSeconds)
        {
            Settle(row, PetBrainRecapStatus.Fallback, "deadline");
            await _db.SaveChangesAsync(ct);
            return;
        }

        var progress = await _provider.PollAsync(row.ProviderJobId, ct);

        if (progress.State == PetBrainRecapProgress.Working)
            return;

        if (progress.State == PetBrainRecapProgress.Failed || progress.Bytes is null)
        {
            Settle(row, PetBrainRecapStatus.Fallback, progress.Reason);
            await _db.SaveChangesAsync(ct);
            return;
        }

        // Baytlar MÜSTƏQİL yoxlanılır: konteyner, ölçü, portret və müddət.
        var check = RecapVideoValidator.Validate(progress.Bytes);

        if (!check.IsValid)
        {
            Settle(row, PetBrainRecapStatus.Rejected, check.Reason);
            await _db.SaveChangesAsync(ct);
            return;
        }

        // Həqiqi xərc razılaşdırılandan çoxdursa, DÖVRƏ AÇILIR: səssizcə artıq
        // xərcləməkdənsə yeni pullu işləri dayandırmaq düzgündür.
        row.RealizedCredits = progress.RealizedCredits;

        if (row.EstimatedCredits > 0 && progress.RealizedCredits > row.EstimatedCredits)
        {
            _breaker.Open("realized-over-estimate");

            _logger.LogWarning(
                "PetBrain media: həqiqi kredit ({Realized}) razılaşdırılandan ({Estimated}) çoxdur — dövrə açıldı.",
                progress.RealizedCredits, row.EstimatedCredits);
        }

        row.AssetKey = await _store.SaveAsync(row.RecapSpecHash, progress.Bytes, ct);
        row.ContentType = check.ContentType;
        row.Width = check.Width;
        row.Height = check.Height;
        row.DurationMs = (int)Math.Round(check.Seconds * 1000);
        row.Status = PetBrainRecapStatus.Ready;
        row.FailureReason = string.Empty;
        row.CompletedAt = now;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Tapmacanın hazır rəsmi — videonun ilk kadrı.
    ///
    /// <para>Yoxdursa <c>null</c> qayıdır və image-to-video provayderi işi
    /// başlatmır: uydurma kadr çəkmək həm pul, həm də vizual davamlılıq
    /// itkisi olardı.</para>
    /// </summary>
    private async Task<byte[]?> ReferenceImageAsync(AdventureRecapSpec spec, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(spec.SceneSpecHash))
            return null;

        var illustration = await _db.PuzzleIllustrations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.SceneSpecHash == spec.SceneSpecHash, ct);

        if (illustration is not { Status: PetBrainIllustrationStatus.Ready } ||
            string.IsNullOrEmpty(illustration.AssetKey))
            return null;

        if (await _illustrations.OpenAsync(illustration.AssetKey, ct) is not { } file)
            return null;

        return await File.ReadAllBytesAsync(file.AbsolutePath, ct);
    }

    /// <summary>
    /// Gündəlik PULLU recap kvotası.
    ///
    /// <para>Keşlənmiş təkrar sayılmır, çünki o, xərc yaratmır — sayılan yalnız
    /// həqiqətən provayderə gedən işlərdir.</para>
    /// </summary>
    private async Task<bool> WithinQuotaAsync(Guid childId, DateTime now, CancellationToken ct)
    {
        if (_breaker.IsOpen)
            return false;

        var since = now.Date;

        var paidToday = await _db.AdventureRecaps
            .CountAsync(r => r.ChildProfileId == childId
                             && r.RequestedAt >= since
                             && r.Status != PetBrainRecapStatus.Fallback, ct);

        return paidToday < _cost.Options.MaxPaidRecapsPerChildPerDay;
    }

    private string QuotaReason() =>
        _breaker.IsOpen ? "circuit-open"
        : _provider.IsEnabled ? "daily-quota"
        : "disabled";

    private void Settle(AdventureRecap row, PetBrainRecapStatus status, string reason)
    {
        row.Status = status;
        row.FailureReason = reason;
        row.CompletedAt = _clock.GetUtcNow().UtcDateTime;

        _logger.LogInformation(
            "PetBrain recap: {Hash} → {Status} ({Reason}). Deterministik recap qalır.",
            row.RecapSpecHash, status, reason);
    }
}
