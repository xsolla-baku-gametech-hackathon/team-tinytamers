using Microsoft.Extensions.Options;
using PetPal.Api.PetBrain.Mind;
using PetPal.Api.PetBrain.Story;

namespace PetPal.Api.PetBrain.BoundedAi;

/// <summary>
/// Modelin planı ilə deterministik tərif arasında SEÇİM edən qat.
///
/// <para>Qayda bir cümlədədir: <b>deterministik tərif standartdır; modelin
/// planı yalnız BÜTÜN yoxlamalardan keçəndə onu əvəz edir.</b></para>
///
/// <para>Bu, «AI ilə yaradılmış macəra» iddiası deyil. Model hazır parçaları
/// yenidən düzür; mətn, mükafat, tapmacanın cavabı, çətinlik, ekran vaxtı və
/// yaş həddi həmişə serverin öz qərarı olaraq qalır.</para>
///
/// <para>Hər uğursuzluq — açar bağlı, model əlçatmaz, pozuq cavab, naməlum ID,
/// dalan, çatılmayan düyün — SƏSSİZCƏ deterministik yola qayıdır. Uşaq heç bir
/// xəta görmür, çünki tərif onsuz da hazırdır.</para>
/// </summary>
public sealed class StoryPlanCoordinator
{
    private readonly IStoryPlanProvider _provider;
    private readonly PetBrainTelemetry _telemetry;
    private readonly PetBrainV2Options _options;
    private readonly ILogger<StoryPlanCoordinator> _logger;

    public StoryPlanCoordinator(
        IStoryPlanProvider provider,
        PetBrainTelemetry telemetry,
        IOptions<PetBrainV2Options> options,
        ILogger<StoryPlanCoordinator> logger)
    {
        _provider = provider;
        _telemetry = telemetry;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Bu macəra üçün işlədiləcək tərif.
    ///
    /// <para>Həmişə <b>etibarlı</b> tərif qaytarır: ən pis halda istinad
    /// tərifinin özü.</para>
    /// </summary>
    public async Task<ExperienceDefinition> ResolveAsync(
        ExperienceDefinition reference, PetMindContext mind, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.BoundedAiEnabled)
            return reference;

        StoryPlan? plan;

        try
        {
            var theme = ExperienceCatalog.Find(reference.Key)?.Theme ?? string.Empty;

            plan = await _provider.ProposeAsync(
                StoryPlanAllowlist.RequestFor(mind.Language, mind.AgeBand, theme, reference.Key), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Model qatının HEÇ BİR nasazlığı uşağa çatmır.
            _logger.LogDebug(ex, "Pet Brain: plan provayderi uğursuz oldu — deterministik tərif işlənir.");
            _telemetry.AiPlan("rejected", "provider-error");

            return reference;
        }

        if (plan is null)
        {
            _telemetry.AiPlan("skipped", "no-plan");
            return reference;
        }

        var review = StoryPlanValidator.Review(plan, reference);

        if (!review.Accepted)
        {
            _logger.LogInformation(
                "Pet Brain: model planı rədd edildi ({Reason}): {Problems}",
                review.RejectionReason, string.Join(" | ", review.Problems));

            _telemetry.AiPlan("rejected", review.RejectionReason ?? "unknown");

            return reference;
        }

        _telemetry.AiPlan("accepted", "validated");

        return review.Definition!;
    }
}
