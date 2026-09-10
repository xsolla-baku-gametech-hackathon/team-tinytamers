using Microsoft.Extensions.Options;

namespace PetPal.Api.PetBrain.Media;

/// <summary>Xərc qərarı. <see cref="Allowed"/> yalnız HƏR ŞEY sağlam olanda doğrudur.</summary>
public sealed record MediaCostDecision(bool Allowed, int Credits, string Model, string Reason)
{
    public decimal Usd => MediaModelCatalog.Usd(Credits);

    public static MediaCostDecision Deny(string reason) => new(false, 0, string.Empty, reason);
}

/// <summary>
/// Pullu iş başlamazdan ƏVVƏL xərci hesablayan qat.
///
/// <para>Qayda sadədir və <b>bağlı sınır</b>: model naməlumdursa, profil ona
/// icazə vermirsə, nisbət portret deyilsə, müddət modelin həddindən uzundursa
/// və ya təxmin tavanı aşırsa — <b>pullu iş başlamır</b> və deterministik
/// ehtiyat qalır. Uşaq fərqi görmür.</para>
///
/// <para>Bu, idempotentliyi ƏVƏZ ETMİR: təkrarın qarşısını baza indeksi alır,
/// bu qat isə "nə qədər" sualına cavab verir.</para>
/// </summary>
public sealed class PetBrainMediaCostPolicy
{
    private readonly PetBrainMediaOptions _options;
    private readonly MediaCircuitBreaker _breaker;

    public PetBrainMediaCostPolicy(IOptions<PetBrainMediaOptions> options, MediaCircuitBreaker breaker)
    {
        _options = options.Value;
        _breaker = breaker;
    }

    public PetBrainMediaOptions Options => _options;

    /// <summary>
    /// Şəkil üçün icazə və ən pis hal krediti. Şəkil videonun ilk kadrıdır, ona
    /// görə nisbəti videonunku ilə EYNİDİR və şəkil modeli üçün də yoxlanılır.
    /// </summary>
    public MediaCostDecision ForImage() =>
        Evaluate(
            _options.ImageModel,
            units: 1,
            ratio: _options.VideoRatio,
            cap: _options.MaxImageCreditsPerRun,
            video: false);

    /// <summary>Video üçün icazə və ən pis hal krediti.</summary>
    public MediaCostDecision ForVideo() =>
        Evaluate(
            _options.VideoModel,
            units: _options.VideoDurationSeconds,
            ratio: _options.VideoRatio,
            cap: _options.MaxVideoCreditsPerRun,
            video: true);

    private MediaCostDecision Evaluate(string modelKey, int units, string? ratio, int cap, bool video)
    {
        if (!_options.PaidMediaEnabled)
            return MediaCostDecision.Deny("paid-media-disabled");

        // Dövrə açıqdırsa yeni pullu iş BAŞLAMIR — "səssizcə artıq xərcləmək"
        // ən pis nəticə olardı.
        if (_breaker.IsOpen)
            return MediaCostDecision.Deny("circuit-open");

        var model = MediaModelCatalog.Find(modelKey);

        if (model is null)
            return MediaCostDecision.Deny("unknown-model");

        // Profil icazə siyahısı model açarından GÜCLÜDÜR.
        if (!MediaModelCatalog.IsAllowed(_options.Profile, modelKey))
            return MediaCostDecision.Deny("model-not-in-profile");

        if (model.IsVideo != video)
            return MediaCostDecision.Deny("model-modality-mismatch");

        if (video && (units < 1 || units > model.MaxDurationSeconds))
            return MediaCostDecision.Deny("duration-out-of-contract");

        if (!MediaModelCatalog.SupportsRatio(modelKey, ratio))
            return MediaCostDecision.Deny("ratio-not-supported");

        var credits = MediaModelCatalog.WorstCaseCredits(modelKey, units);

        if (credits > cap)
            return MediaCostDecision.Deny("over-credit-cap");

        return new MediaCostDecision(true, credits, modelKey, string.Empty);
    }
}

/// <summary>
/// Xərc dövrə kəsicisi.
///
/// <para>Provayder razılaşdırılandan BAHA çıxsa (məsələn model gözlənilməz
/// müddət qaytarsa), yeni pullu işlər dayanır. Bu, prosesdaxilidir — prototip
/// üçün qəsdən sadədir; hadisə isə böyüklərin panelinə düşür.</para>
/// </summary>
public sealed class MediaCircuitBreaker
{
    private int _open;

    public bool IsOpen => Volatile.Read(ref _open) == 1;

    public string Reason { get; private set; } = string.Empty;

    public void Open(string reason)
    {
        Reason = reason;
        Interlocked.Exchange(ref _open, 1);
    }

    /// <summary>Yalnız böyüklərin açıq qərarı ilə bağlanır.</summary>
    public void Reset()
    {
        Reason = string.Empty;
        Interlocked.Exchange(ref _open, 0);
    }
}
