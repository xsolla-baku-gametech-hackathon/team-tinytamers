using Microsoft.Extensions.Options;
using PetPal.Api.PetBrain.Media;

namespace PetPal.Api.PetBrain.Recap;

/// <summary>Başladılmış işin nəticəsi — tapşırıq id-si SAXLANILIR.</summary>
public sealed record RecapJobStart(bool Started, string JobId, string Provider, string Model, string Reason)
{
    public static RecapJobStart Failed(string reason) => new(false, string.Empty, string.Empty, string.Empty, reason);
}

/// <summary>İşin izlənməsi. Bayt YALNIZ hazır olanda gəlir.</summary>
public sealed record RecapJobProgress(
    PetBrainRecapProgress State, byte[]? Bytes, int RealizedCredits, string Reason)
{
    public static RecapJobProgress Working() => new(PetBrainRecapProgress.Working, null, 0, string.Empty);

    public static RecapJobProgress Failed(string reason) =>
        new(PetBrainRecapProgress.Failed, null, 0, reason);

    public static RecapJobProgress Ready(byte[] bytes, int credits) =>
        new(PetBrainRecapProgress.Ready, bytes, credits, string.Empty);
}

/// <summary>İşin sadə vəziyyəti — provayderdən asılı deyil.</summary>
public enum PetBrainRecapProgress
{
    Working = 0,
    Ready = 1,
    Failed = 2
}

/// <summary>
/// Recap videosunu hazırlayan qat.
///
/// <para>İki mərhələlidir və bu, QƏSDƏNDİR: iş <b>başladılır</b>, tapşırıq
/// id-si bazaya yazılır, sonra izlənir. Beləliklə proses yenidən başlasa da
/// iş itmir və <b>ikinci dəfə pul xərclənmir</b>.</para>
/// </summary>
public interface IRecapVideoProvider
{
    bool IsEnabled { get; }

    Task<RecapJobStart> StartAsync(
        AdventureRecapSpec spec, string prompt, byte[]? referenceImage, CancellationToken ct = default);

    Task<RecapJobProgress> PollAsync(string jobId, CancellationToken ct = default);
}

/// <summary>
/// Standart implementasiya: video YOXDUR.
///
/// <para>Bu, "ehtiyat variant" deyil — ƏSAS variantdır. Uşaq tam 10 saniyəlik
/// deterministik storyboard görür, mükafatını isə onsuz da almışdır.</para>
/// </summary>
public sealed class DisabledRecapVideoProvider : IRecapVideoProvider
{
    public bool IsEnabled => false;

    public Task<RecapJobStart> StartAsync(
        AdventureRecapSpec spec, string prompt, byte[]? referenceImage, CancellationToken ct = default) =>
        Task.FromResult(RecapJobStart.Failed("disabled"));

    public Task<RecapJobProgress> PollAsync(string jobId, CancellationToken ct = default) =>
        Task.FromResult(RecapJobProgress.Failed("disabled"));
}

/// <summary>
/// Runway ilə image-to-video.
///
/// <para>Tapmacanın hazır rəsmi <b>ilk kadr</b> kimi verilir — fon, pet, Robo,
/// palitra və rekvizitlər beləcə eyni qalır. İkinci referans kadr
/// GENERASİYA OLUNMUR: mövcud rəsm etibarlıdırsa, artıq şəkil xərci yoxdur.</para>
/// </summary>
public sealed class RunwayRecapVideoProvider : IRecapVideoProvider
{
    private const string ProviderName = "runway";

    private readonly IRunwayTaskClient _client;
    private readonly PetBrainMediaCostPolicy _cost;
    private readonly ILogger<RunwayRecapVideoProvider> _logger;

    public RunwayRecapVideoProvider(
        IRunwayTaskClient client,
        PetBrainMediaCostPolicy cost,
        IOptions<PetBrainMediaOptions> options,
        ILogger<RunwayRecapVideoProvider> logger)
    {
        _client = client;
        _cost = cost;
        _logger = logger;
    }

    public bool IsEnabled =>
        _cost.Options.Provider == PetBrainMediaProvider.Runway &&
        _cost.Options.PaidMediaEnabled &&
        _client.IsConfigured;

    public async Task<RecapJobStart> StartAsync(
        AdventureRecapSpec spec, string prompt, byte[]? referenceImage, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return RecapJobStart.Failed("disabled");

        // Xərc ƏVVƏLCƏ hesablanır — tapşırıq yaradılmazdan qabaq.
        var decision = _cost.ForVideo();

        if (!decision.Allowed)
        {
            _logger.LogInformation("PetBrain media: video işi başlamadı ({Reason}).", decision.Reason);
            return RecapJobStart.Failed(decision.Reason);
        }

        // Referans kadr olmadan image-to-video mümkün deyil; mətn-video
        // provayderi üçün bu qat ayrıca yazılmalıdır.
        if (referenceImage is null || referenceImage.Length == 0)
            return RecapJobStart.Failed("no-reference-image");

        var dataUri = $"data:image/png;base64,{Convert.ToBase64String(referenceImage)}";

        var task = await _client.CreateVideoAsync(
            decision.Model, prompt, dataUri, _cost.Options.VideoRatio, _cost.Options.VideoDurationSeconds, ct);

        if (task.State == RunwayTaskState.Failed || string.IsNullOrEmpty(task.Id))
            return RecapJobStart.Failed(task.FailureReason);

        return new RecapJobStart(true, task.Id, ProviderName, decision.Model, string.Empty);
    }

    public async Task<RecapJobProgress> PollAsync(string jobId, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return RecapJobProgress.Failed("disabled");

        var task = await _client.PollAsync(jobId, ct);

        return task.State switch
        {
            RunwayTaskState.Pending or RunwayTaskState.Running => RecapJobProgress.Working(),

            RunwayTaskState.Succeeded when task.OutputUrl is { } url =>
                await DownloadAsync(url, ct),

            _ => RecapJobProgress.Failed(task.FailureReason)
        };
    }

    private async Task<RecapJobProgress> DownloadAsync(string url, CancellationToken ct)
    {
        var bytes = await _client.DownloadAsync(url, RecapVideoValidator.MaxBytes, ct);

        if (bytes is null)
            return RecapJobProgress.Failed("download-failed");

        // Həqiqi kredit provayder bildirməyəndə razılaşdırılmış təxminlə eyni
        // sayılır — «bilmirik» demək «pulsuz» demək deyil.
        return RecapJobProgress.Ready(bytes, _cost.ForVideo().Credits);
    }
}
