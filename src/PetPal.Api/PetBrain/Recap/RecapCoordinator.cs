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

    /// <summary>
    /// İlk kadrın rəsmi hələ çəkilir — sətir <c>Pending</c> qalır və gözləyir.
    /// Video itmir, sadəcə başlamaq üçün öz ilk kadrını gözləyir.
    /// </summary>
    public const string AwaitingScene = "awaiting-scene";

    /// <summary>İlk kadr (tapmacanın hazır rəsmi) yoxdur — image-to-video başlaya bilmir.</summary>
    public const string NoReferenceImage = "no-reference-image";

    /// <summary>
    /// Rəsm ən çox bu qədər gözlənilir. Hədd keçsə sətir ehtiyata düşür, rəsm
    /// sonra hazır olanda isə yenidən açılır — gözləmə sonsuz deyil.
    /// </summary>
    private static readonly TimeSpan SceneWaitLimit = TimeSpan.FromMinutes(10);

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
    ///
    /// <para>Kvota sətir yaradılmazdan ƏVVƏL yoxlanılır: hədd dolubsa sətir
    /// birbaşa "Fallback" kimi açılır və heç bir iş növbəyə düşmür.</para>
    ///
    /// <para>Bu forma BAXIŞ üçündür (yekunu yenidən açmaq, recap ünvanı):
    /// faylı itmiş videonu yenidən sifariş etmir.</para>
    /// </summary>
    public Task<AdventureRecap> EnsureAsync(AdventureRecapSpec spec, CancellationToken ct) =>
        EnsureAsync(spec, reorderLostVideo: false, ct);

    /// <summary>
    /// Recap sətrini AÇIR; <paramref name="reorderLostVideo"/> doğrudursa
    /// faylı itmiş videonu da yenidən sifariş edir.
    ///
    /// <para>Bayrağı yalnız macəranın YENİ tamamlanması qaldırır. Hash seçimlərdən
    /// qurulur və əbədi keşlənir: bayraq olmasa eyni seçimlərlə bitən hər yeni
    /// macəra itmiş videonun storyboard-ına düşər və bu seçimlər bir daha heç
    /// vaxt video almazdı. Tamamlanma isə uşağın açıq sifarişidir — video bir
    /// dəfə yenidən çəkilir, rəfə və yekuna baxmaq isə yenə pul xərcləmir.</para>
    /// </summary>
    public async Task<AdventureRecap> EnsureAsync(
        AdventureRecapSpec spec, bool reorderLostVideo, CancellationToken ct)
    {
        var hash = spec.Hash();
        var now = _clock.GetUtcNow().UtcDateTime;

        var existing = await _db.AdventureRecaps.FirstOrDefaultAsync(r => r.RecapSpecHash == hash, ct);

        if (existing is not null)
            return await ReopenIfNowAllowedAsync(
                await DropLostVideoAsync(existing, ct), spec, now, reorderLostVideo, ct);

        var denial = await DenialAsync(spec.ChildProfileId, now, ct);
        var allowed = denial.Length == 0;

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
            FailureReason = denial,
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

        // Tapmacanın hazır rəsmi videonun ilk kadrıdır — ikinci referans kadr
        // GENERASİYA OLUNMUR, yəni artıq şəkil xərci yoxdur.
        var reference = await ReferenceImageAsync(spec, ct);

        if (reference is null && await SceneStillDrawingAsync(row, spec, ct))
            return;

        var shots = RecapStoryboard.Build(spec);
        var prompt = SafeRecapPromptBuilder.Build(spec, shots);

        row.PromptHash = SafeRecapPromptBuilder.HashOf(prompt);
        row.PromptTemplateVersion = SafeRecapPromptBuilder.TemplateVersion;
        row.Attempts++;

        var estimate = _cost.ForVideo();
        row.EstimatedCredits = estimate.Allowed ? estimate.Credits : 0;

        var started = await _provider.StartAsync(spec, prompt, reference, ct);

        if (!started.Started && ShouldRetryStart(row, started.Reason))
        {
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (!started.Started)
        {
            Settle(row, PetBrainRecapStatus.Fallback, started.Reason);
            await _db.SaveChangesAsync(ct);
            return;
        }

        row.Status = PetBrainRecapStatus.Generating;
        row.FailureReason = string.Empty;
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
    private async Task<byte[]?> ReferenceImageAsync(AdventureRecapSpec spec, CancellationToken ct) =>
        await ReferenceFileAsync(spec, ct) is { } file
            ? await File.ReadAllBytesAsync(file.AbsolutePath, ct)
            : null;

    /// <summary>Hazır rəsmin faylı — baytları oxunmadan, yalnız varlığı.</summary>
    private async Task<PuzzleIllustrationFile?> ReferenceFileAsync(AdventureRecapSpec spec, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(spec.SceneSpecHash))
            return null;

        var illustration = await _db.PuzzleIllustrations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.SceneSpecHash == spec.SceneSpecHash, ct);

        if (illustration is not { Status: PetBrainIllustrationStatus.Ready } ||
            string.IsNullOrEmpty(illustration.AssetKey))
            return null;

        return await _illustrations.OpenAsync(illustration.AssetKey, ct);
    }

    /// <summary>
    /// İlk kadrın rəsmi hələ ÇƏKİLİRMİ. Çəkilirsə recap gözləyir: sətir
    /// <c>Pending</c> qalır, cəhd sayılmır (provayderə heç nə getməyib), işçi
    /// isə onu bir azdan yenidən yoxlayır.
    ///
    /// <para>Əvvəl belə recap dərhal ehtiyata düşürdü. Hash seçimlərdən
    /// qurulduğu üçün həmin seçimlər bundan sonra HEÇ VAXT video almırdı — uşaq
    /// macərəni rəsmdən tez bitirəndə video həmişəlik itirdi.</para>
    ///
    /// <para>Gözləmə sərhədlidir (<see cref="SceneWaitLimit"/>): rəsm ilişib
    /// qalsa sətir ehtiyata düşür və rəsm sonra hazır olanda yenidən açılır
    /// (<see cref="LateSceneArrivedAsync"/>).</para>
    /// </summary>
    private async Task<bool> SceneStillDrawingAsync(AdventureRecap row, AdventureRecapSpec spec, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(spec.SceneSpecHash))
            return false;

        var scene = await _db.PuzzleIllustrations
            .AsNoTracking()
            .Where(i => i.SceneSpecHash == spec.SceneSpecHash)
            .Select(i => (PetBrainIllustrationStatus?)i.Status)
            .FirstOrDefaultAsync(ct);

        if (scene != PetBrainIllustrationStatus.Pending)
            return false;

        if (_clock.GetUtcNow().UtcDateTime - row.RequestedAt > SceneWaitLimit)
        {
            Settle(row, PetBrainRecapStatus.Fallback, NoReferenceImage);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        if (row.FailureReason != AwaitingScene)
        {
            row.FailureReason = AwaitingScene;
            await _db.SaveChangesAsync(ct);
        }

        return true;
    }

    /// <summary>
    /// Ehtiyata düşmənin səbəbi ilk kadrın yoxluğu idisə və rəsm İNDİ
    /// hazırdırsa, sətir yenidən açıla bilər. Provayderə heç nə getməmişdi —
    /// bu, ikinci pullu iş deyil; seçimlər sadəcə gec gələn rəsmlə öz videosunu
    /// alır.
    /// </summary>
    private async Task<bool> LateSceneArrivedAsync(AdventureRecap row, AdventureRecapSpec spec, CancellationToken ct) =>
        row.FailureReason == NoReferenceImage && await ReferenceFileAsync(spec, ct) is not null;

    /// <summary>
    /// Videolar rəfi üçün sətri OXUYUR — yeni pullu iş yaratmır.
    ///
    /// <para>Yeganə istisna ilk kadrı gec gələn recap-dır: o, macəra AI açıq
    /// ikən bitəndə sifariş olunmuşdu, sadəcə rəsm gecikmişdi. Belə sətir rəsm
    /// hazır olanda burada yenidən açılır. AI bağlı ikən bitmiş köhnə macəralar
    /// isə rəfə baxmaqla video ALMIR — rəfi açmaq kredit xərcləməməlidir.</para>
    /// </summary>
    public async Task<AdventureRecap?> FindAsync(AdventureRecapSpec spec, CancellationToken ct)
    {
        var hash = spec.Hash();
        var row = await _db.AdventureRecaps.FirstOrDefaultAsync(r => r.RecapSpecHash == hash, ct);

        if (row is null)
            return null;

        row = await DropLostVideoAsync(row, ct);

        if (row is not { Status: PetBrainRecapStatus.Fallback } || row.FailureReason != NoReferenceImage)
            return row;

        return await ReopenIfNowAllowedAsync(row, spec, _clock.GetUtcNow().UtcDateTime, reorderLostVideo: false, ct);
    }

    /// <summary>Hazır videonun faylı saxlancda yoxdur — sətir artıq «hazır» deyil.</summary>
    public const string AssetMissing = "asset-missing";

    /// <summary>
    /// «Hazır» deyən, amma faylı saxlancda olmayan videonu storyboard-a salır.
    ///
    /// <para>Belə olmasa ekran «videoya bax» düyməsi göstərir, video isə
    /// açılmır. Səbəb «provayderə heç çatmadı» siyahısında yoxdur, yəni rəfə və
    /// ya yekuna baxmaq video ÇƏKDİRMİR. Yenidən çəkməni yalnız eyni seçimlərlə
    /// bitən YENİ macəra sifariş edir
    /// (<see cref="EnsureAsync(AdventureRecapSpec, bool, CancellationToken)"/>).</para>
    /// </summary>
    private async Task<AdventureRecap> DropLostVideoAsync(AdventureRecap row, CancellationToken ct)
    {
        if (row.Status != PetBrainRecapStatus.Ready ||
            (!string.IsNullOrEmpty(row.AssetKey) && await _store.OpenAsync(row.AssetKey, ct) is not null))
            return row;

        Settle(row, PetBrainRecapStatus.Fallback, AssetMissing);
        await _db.SaveChangesAsync(ct);

        return row;
    }

    /// <summary>
    /// Başlatma yenidən cəhd edilsinmi. Yalnız provayder sorğunu İŞLƏMƏDİYİNİ
    /// deyəndə (<see cref="MediaFailure.IsRetryableCreate"/>) və cəhd həddi
    /// dolmayıbsa: sətir <c>Pending</c> qalır, işçi növbəti addımda yenidən
    /// başladır. Nəqliyyat xətası təkrarlanmır — o, ikinci pullu tapşırıq ola
    /// bilərdi.
    /// </summary>
    private static bool ShouldRetryStart(AdventureRecap row, string reason) =>
        MediaFailure.IsRetryableCreate(reason) && row.Attempts < MaxAttempts;

    /// <summary>
    /// Yeni PULLU işə nə mane olur; boş sətir — heç nə.
    ///
    /// <para>Keşlənmiş təkrar sayılmır, çünki o, xərc yaratmır — sayılan yalnız
    /// həqiqətən provayderə gedən işlərdir. Uşaq başına hədd bir uşağın bütün
    /// büdcəni tutmasının, ümumi hədd isə gündəlik xərcin sərhədsiz böyüməsinin
    /// qarşısını alır.</para>
    ///
    /// <para>Hər iki hədd standart olaraq YOXDUR (0): yalnız müsbət dəyər
    /// yazılanda sayılır. Video başına xərci onsuz da xərc siyasəti və keş
    /// saxlayır — eyni seçimlər ikinci dəfə pul xərcləmir.</para>
    /// </summary>
    private async Task<string> DenialAsync(Guid childId, DateTime now, CancellationToken ct)
    {
        if (!_provider.IsEnabled)
            return "disabled";

        if (_breaker.IsOpen)
            return "circuit-open";

        var perChild = _cost.Options.MaxPaidRecapsPerChildPerDay;
        var overall = _cost.Options.MaxPaidRecapsPerDay;

        if (perChild <= 0 && overall <= 0)
            return string.Empty;

        var since = now.Date;

        var paidToday = _db.AdventureRecaps
            .Where(r => r.RequestedAt >= since && r.Status != PetBrainRecapStatus.Fallback);

        if (perChild > 0 && await paidToday.CountAsync(r => r.ChildProfileId == childId, ct) >= perChild)
            return "daily-quota";

        if (overall > 0 && await paidToday.CountAsync(ct) >= overall)
            return "global-daily-quota";

        return string.Empty;
    }

    /// <summary>
    /// Provayderə HEÇ ÇATMAMIŞ ehtiyat sətrini yenidən açır.
    ///
    /// <para>Belə sətir «video alınmadı» demək deyil: o an AI bağlı idi, kvota
    /// dolu idi və ya dövrə açıq idi — pul xərclənməyib. Hash seçimlərdən
    /// qurulur və əbədi keşlənir, ona görə sətir olduğu kimi qalsaydı, açar
    /// sonradan qoşulanda eyni seçimlər heç vaxt video almazdı. Provayderə
    /// çatmış, rədd olunmuş və ya hazır sətrə TOXUNULMUR.</para>
    ///
    /// <para>Sətir yalnız bu an HƏR ŞEY icazə verəndə açılır — provayder, xərc
    /// siyasəti və kvota. Əks halda o, hər sorğuda açılıb-bağlanardı. Açılan
    /// sətir sorğunu verən uşağın adına keçir: xərc onun kvotasına yazılır.</para>
    ///
    /// <para>İlk kadrı gec gələn sətir də buraya aiddir: provayderə heç nə
    /// getməmişdi, rəsm isə indi hazırdır (<see cref="LateSceneArrivedAsync"/>).</para>
    ///
    /// <para>Faylı itmiş video isə yalnız <paramref name="reorderLostVideo"/>
    /// ilə açılır — yəni eyni seçimlərlə YENİ macəra bitəndə. Köhnə tapşırığın
    /// id-si və fayl açarı silinir: işçi yeni tapşırıq başladır, köhnəni sorğulamır.</para>
    /// </summary>
    private async Task<AdventureRecap> ReopenIfNowAllowedAsync(
        AdventureRecap row, AdventureRecapSpec spec, DateTime now, bool reorderLostVideo, CancellationToken ct)
    {
        if (row.Status != PetBrainRecapStatus.Fallback ||
            !(MediaFailure.NeverReachedProvider(row.FailureReason) ||
              (reorderLostVideo && row.FailureReason == AssetMissing) ||
              await LateSceneArrivedAsync(row, spec, ct)))
            return row;

        if (!_cost.ForVideo().Allowed || (await DenialAsync(spec.ChildProfileId, now, ct)).Length > 0)
            return row;

        row.Status = PetBrainRecapStatus.Pending;
        row.FailureReason = string.Empty;
        row.AssetKey = string.Empty;
        row.ProviderJobId = string.Empty;
        row.ChildProfileId = spec.ChildProfileId;
        row.ExperienceRunId = spec.RunId;
        row.Attempts = 0;
        row.RequestedAt = now;
        row.StartedAt = null;
        row.CompletedAt = null;

        await _db.SaveChangesAsync(ct);

        return row;
    }

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
