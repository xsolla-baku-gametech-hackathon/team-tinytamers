using PetPal.Api.PetBrain.Puzzles;

namespace PetPal.Api.PetBrain.Media;

/// <summary>
/// Tapmacanın hekayə rəsmini Runway ilə çəkir.
///
/// <para>Mövcud <see cref="IPuzzleIllustrationProvider"/> müqaviləsindədir, yəni
/// tapmaca qatı provayderi TANIMIR: rəsm gəlsə də, gəlməsə də düyünlər,
/// enerji, qaydalar və cavab dəyişmir.</para>
///
/// <para>Şəkil həm də videonun İLK KADRIDIR (bax <c>RunwayRecapVideoProvider</c>):
/// eyni səhnə iki dəfə çəkilmir, ona görə xərc bir şəkil + bir videodur.</para>
/// </summary>
public sealed class RunwayPuzzleIllustrationProvider : IPuzzleIllustrationProvider
{
    private const string ProviderName = "runway";

    /// <summary>Tapşırığın hazır olmasını gözləmə addımı.</summary>
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(2);

    private readonly IRunwayTaskClient _client;
    private readonly PetBrainMediaCostPolicy _cost;
    private readonly MediaCircuitBreaker _breaker;
    private readonly TimeProvider _clock;
    private readonly ILogger<RunwayPuzzleIllustrationProvider> _logger;

    public RunwayPuzzleIllustrationProvider(
        IRunwayTaskClient client,
        PetBrainMediaCostPolicy cost,
        MediaCircuitBreaker breaker,
        TimeProvider clock,
        ILogger<RunwayPuzzleIllustrationProvider> logger)
    {
        _client = client;
        _cost = cost;
        _breaker = breaker;
        _clock = clock;
        _logger = logger;
    }

    public bool IsEnabled =>
        _cost.Options.Provider == PetBrainMediaProvider.Runway &&
        _cost.Options.PaidMediaEnabled &&
        _client.IsConfigured;

    public async Task<PuzzleIllustrationResult> RenderAsync(
        PuzzleSceneSpec spec, string prompt, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return PuzzleIllustrationResult.Failed("disabled");

        // Xərc ƏVVƏLCƏ hesablanır: naməlum model, profil kənarı və ya tavanı
        // aşan təxmin pullu işi başlamağa qoymur.
        var decision = _cost.ForImage();

        if (!decision.Allowed)
        {
            _logger.LogInformation("PetBrain media: şəkil işi başlamadı ({Reason}).", decision.Reason);
            return PuzzleIllustrationResult.Failed(decision.Reason, ProviderName);
        }

        var ratio = _cost.Options.VideoRatio;

        // Şəkil videonun ilk kadrı olacaq, ona görə nisbət EYNİ olmalıdır.
        if (!MediaModelCatalog.SupportsRatio(decision.Model, ratio))
            return PuzzleIllustrationResult.Failed("ratio-not-supported", ProviderName, decision.Model);

        var created = await _client.CreateImageAsync(decision.Model, prompt, ratio, ct);

        if (created.State == RunwayTaskState.Failed)
            return PuzzleIllustrationResult.Failed(created.FailureReason, ProviderName, decision.Model);

        var finished = await WaitAsync(created, ct);

        if (finished.State != RunwayTaskState.Succeeded || finished.OutputUrl is null)
            return PuzzleIllustrationResult.Failed(finished.FailureReason, ProviderName, decision.Model);

        var bytes = await _client.DownloadAsync(finished.OutputUrl, PuzzleIllustrationValidator.MaxBytes, ct);

        if (bytes is null)
            return PuzzleIllustrationResult.Failed("download-failed", ProviderName, decision.Model);

        // Format və ölçü AYRICA yoxlanılır (PuzzleIllustrationValidator):
        // provayderin başlığına inanılmır, baytlar özü oxunur.
        return PuzzleIllustrationResult.Ok(bytes, PuzzleIllustrationValidator.Png, ProviderName, decision.Model);
    }

    /// <summary>
    /// Tapşırığı vaxt həddi daxilində izləyir.
    ///
    /// <para>Həddi keçəndə iş TƏRK EDİLİR, amma dövrə kəsicisi AÇILMIR: gecikmə
    /// artıq xərc demək deyil, sadəcə uşaq gözləməyəcək.</para>
    /// </summary>
    private async Task<RunwayTask> WaitAsync(RunwayTask task, CancellationToken ct)
    {
        var deadline = _clock.GetUtcNow().AddSeconds(_cost.Options.GenerationDeadlineSeconds);
        var current = task;

        while (current.State is RunwayTaskState.Pending or RunwayTaskState.Running)
        {
            if (_clock.GetUtcNow() >= deadline)
                return RunwayTask.Failed("deadline");

            await Task.Delay(PollDelay, ct);
            current = await _client.PollAsync(current.Id, ct);
        }

        return current;
    }
}
