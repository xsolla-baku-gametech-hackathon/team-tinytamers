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

    /// <summary>
    /// Tapşırığı YARATMA cəhdlərinin sayı. İkinci cəhd yalnız provayder sorğunu
    /// İŞLƏMƏDİYİNİ deyəndə olur (<see cref="MediaFailure.IsRetryableCreate"/>),
    /// yəni təkrar ikinci pullu tapşırıq yarada bilmir.
    /// </summary>
    private const int MaxCreateAttempts = 2;

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

    /// <summary>
    /// Səhnəni çəkdirir.
    ///
    /// <para>Xərc ƏVVƏLCƏ hesablanır: naməlum model, profil kənarı, dəstəklənməyən
    /// nisbət (şəkil videonun ilk kadrıdır, nisbəti eyni olmalıdır) və ya tavanı
    /// aşan təxmin pullu işi başlamağa qoymur.</para>
    ///
    /// <para>Format və ölçü AYRICA yoxlanılır (<see cref="PuzzleIllustrationValidator"/>):
    /// provayderin başlığına inanılmır, baytlar özü oxunur.</para>
    /// </summary>
    public async Task<PuzzleIllustrationResult> RenderAsync(
        PuzzleSceneSpec spec, string prompt, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return PuzzleIllustrationResult.Failed("disabled");

        var decision = _cost.ForImage();

        if (!decision.Allowed)
        {
            _logger.LogInformation("PetBrain media: şəkil işi başlamadı ({Reason}).", decision.Reason);
            return PuzzleIllustrationResult.Failed(decision.Reason, ProviderName);
        }

        var created = await CreateAsync(decision.Model, prompt, ct);

        if (created.State == RunwayTaskState.Failed)
            return PuzzleIllustrationResult.Failed(created.FailureReason, ProviderName, decision.Model);

        var finished = await WaitAsync(created, ct);

        if (finished.State != RunwayTaskState.Succeeded || finished.OutputUrl is null)
            return PuzzleIllustrationResult.Failed(finished.FailureReason, ProviderName, decision.Model);

        var bytes = await _client.DownloadAsync(finished.OutputUrl, PuzzleIllustrationValidator.MaxBytes, ct);

        if (bytes is null)
            return PuzzleIllustrationResult.Failed("download-failed", ProviderName, decision.Model);

        return PuzzleIllustrationResult.Ok(bytes, PuzzleIllustrationValidator.Png, ProviderName, decision.Model);
    }

    private async Task<RunwayTask> CreateAsync(string model, string prompt, CancellationToken ct)
    {
        var created = await _client.CreateImageAsync(model, prompt, _cost.Options.VideoRatio, ct);

        for (var attempt = 1;
             attempt < MaxCreateAttempts &&
             created.State == RunwayTaskState.Failed &&
             MediaFailure.IsRetryableCreate(created.FailureReason);
             attempt++)
        {
            await Task.Delay(PollDelay, ct);
            created = await _client.CreateImageAsync(model, prompt, _cost.Options.VideoRatio, ct);
        }

        return created;
    }

    /// <summary>
    /// Tapşırığı vaxt həddi daxilində izləyir.
    ///
    /// <para>Oxuma xətası (şəbəkə, <c>429</c>, <c>5xx</c>) tapşırığı ÖLDÜRMÜR:
    /// tapşırıq artıq pulludur, soruşmaq isə təhlükəsiz təkrarlanır.</para>
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

            var polled = await _client.PollAsync(task.Id, ct);

            if (polled.State == RunwayTaskState.Failed && MediaFailure.IsRetryableRead(polled.FailureReason))
                continue;

            current = polled;
        }

        return current;
    }

    private TimeSpan PollDelay => TimeSpan.FromMilliseconds(Math.Max(50, _cost.Options.ImagePollMilliseconds));
}
