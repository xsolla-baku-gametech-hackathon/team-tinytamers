using Microsoft.Extensions.Options;
using PetPal.Api.PetBrain.Media;

namespace PetPal.Api.Wardrobe;

/// <summary>
/// Dizayn studiyasının Runway yolu — mövcud Runway açarı ilə OpenAI-ın
/// <c>gpt_image_2_5_flare</c> modeli.
///
/// <para>Runway bu modeli üçüncü tərəf modeli kimi satır (rəsmi qiymət,
/// 2026-09-11): 1K/2K şəkil <c>low</c> 1, <c>medium</c> 5, <c>high</c> 16
/// kredit, hər istinad şəkli +1. Sorğu mövcud <see cref="IRunwayTaskClient"/>-dən
/// keçir: açar yalnız API sorğusuna gedir, hazır fayl bizim saxlanca köçürülür.</para>
///
/// <para><b>Xərc ƏVVƏLCƏ yoxlanılır</b>: ən pis hal krediti tavanı
/// (<see cref="WardrobeOptions.MaxCreditsPerImage"/>) keçirsə və ya xərc dövrə
/// kəsicisi açıqdırsa pullu iş BAŞLAMIR. Provayder razılaşdırılandan baha
/// hesablasa kəsici açılır — tapmaca və recap ilə EYNİ kəsici, çünki hesab
/// birdir.</para>
///
/// <para>Runway-in təhlükəsizlik rəddi (<c>SAFETY.*</c>,
/// <c>INPUT_PREPROCESSING.SAFETY.*</c>) uşağa «başqa paltar» kimi çatır və
/// təkrarlanmır. Rəsmi sənədə görə <c>SAFETY.INPUT.*</c> rəddində kredit
/// QAYTARILMIR — determinist filtrin pullu çağırışdan əvvəl işləməsinin bir
/// səbəbi də budur.</para>
/// </summary>
public sealed class RunwayWardrobeImageProvider : IWardrobeImageProvider
{
    /// <summary>GPT Image modellərinin prompt həddi Runway-də 1000-dən xeyli uzundur.</summary>
    private const int MaxPromptLength = 4000;

    /// <summary>
    /// Runway <c>data:</c> URI-ni ən çox 5 MB qəbul edir; base64 baytı təxminən
    /// 4/3 böyüdür. Bu həddən böyük istinad göndərilmir.
    /// </summary>
    private const int ReferenceBytesLimit = 3_700_000;

    private readonly IRunwayTaskClient _client;
    private readonly WardrobeOptions _options;
    private readonly MediaCircuitBreaker _breaker;
    private readonly TimeProvider _clock;
    private readonly ILogger<RunwayWardrobeImageProvider> _logger;

    public RunwayWardrobeImageProvider(
        IRunwayTaskClient client,
        IOptions<WardrobeOptions> options,
        MediaCircuitBreaker breaker,
        TimeProvider clock,
        ILogger<RunwayWardrobeImageProvider> logger)
    {
        _client = client;
        _options = options.Value;
        _breaker = breaker;
        _clock = clock;
        _logger = logger;
    }

    public bool IsEnabled => _options.UsesRunway && _client.IsConfigured;

    public string Name => "runway";

    public string Model => _options.EffectiveRunwayModel;

    public int MaxReferenceBytes => ReferenceBytesLimit;

    public Task<WardrobeImageResult> GenerateAsync(string prompt, CancellationToken ct = default) =>
        RunAsync(prompt, reference: null, ct);

    public Task<WardrobeImageResult> EditAsync(
        string prompt, byte[] reference, string referenceContentType, CancellationToken ct = default) =>
        RunAsync(prompt, $"data:{referenceContentType};base64,{Convert.ToBase64String(reference)}", ct);

    /// <summary>
    /// Runway tapşırığını tanıyır: <c>SAFETY.*</c> kodları və mətn
    /// moderasiyasının ön emal kodu təhlükəsizlik rəddidir, qalanı texniki xəta.
    /// </summary>
    public static bool IsSafetyRejection(string? reason) =>
        reason is not null &&
        reason.StartsWith("failed:", StringComparison.Ordinal) &&
        reason.Contains("SAFETY", StringComparison.OrdinalIgnoreCase);

    private async Task<WardrobeImageResult> RunAsync(string prompt, string? reference, CancellationToken ct)
    {
        if (!IsEnabled)
            return WardrobeImageResult.Failed("disabled");

        if (_breaker.IsOpen)
            return WardrobeImageResult.Failed("circuit-open");

        var estimate = _options.RunwayCreditsFor(withReference: reference is not null);

        if (estimate > _options.MaxCreditsPerImage)
        {
            _logger.LogInformation(
                "Wardrobe: şəkil işi başlamadı — təxmin {Estimate} kredit, tavan {Cap}.",
                estimate, _options.MaxCreditsPerImage);

            return WardrobeImageResult.Failed("over-credit-cap");
        }

        var created = await _client.CreateTextToImageAsync(new RunwayTextToImageRequest(
            Model,
            prompt,
            _options.EffectiveRunwayRatio,
            _options.EffectiveQuality,
            reference is null ? null : [reference],
            MaxPromptLength), ct);

        var finished = created.State == RunwayTaskState.Failed ? created : await WaitAsync(created, ct);

        if (finished.Cost is { } cost && cost > _options.MaxCreditsPerImage)
        {
            _breaker.Open("wardrobe-over-cost");

            _logger.LogWarning(
                "Wardrobe: Runway {Cost} kredit hesabladı, tavan {Cap} — yeni pullu işlər dayandırıldı.",
                cost, _options.MaxCreditsPerImage);
        }

        if (finished.State != RunwayTaskState.Succeeded || finished.OutputUrl is null)
        {
            return IsSafetyRejection(finished.FailureReason)
                ? WardrobeImageResult.Refusal()
                : WardrobeImageResult.Failed(finished.FailureReason);
        }

        var bytes = await _client.DownloadAsync(finished.OutputUrl, _options.MaxImageBytes, ct);

        return bytes is null
            ? WardrobeImageResult.Failed("download-failed")
            : WardrobeImageResult.Ok(bytes);
    }

    /// <summary>
    /// Tapşırığı vaxt həddi daxilində izləyir. Oxuma xətası tapşırığı
    /// öldürmür — tapşırıq artıq pulludur, soruşmaq isə təhlükəsiz təkrarlanır.
    /// </summary>
    private async Task<RunwayTask> WaitAsync(RunwayTask task, CancellationToken ct)
    {
        var deadline = _clock.GetUtcNow().AddSeconds(Math.Max(10, _options.TimeoutSeconds));
        var current = task;

        while (current.State is RunwayTaskState.Pending or RunwayTaskState.Running)
        {
            if (_clock.GetUtcNow() >= deadline)
                return RunwayTask.Failed("deadline");

            await Task.Delay(TimeSpan.FromMilliseconds(_options.EffectivePollMilliseconds), ct);

            var polled = await _client.PollAsync(task.Id, ct);

            if (polled.State == RunwayTaskState.Failed && MediaFailure.IsRetryableRead(polled.FailureReason))
                continue;

            current = polled;
        }

        return current;
    }
}
