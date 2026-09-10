using PetPal.Api.Entities;
using PetPal.Api.PetBrain.Mind;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Recommendation;

/// <summary>
/// Fərdiləşdirmə vəziyyətinin DTO-ya çevrilməsi — <b>saf funksiyalar</b>.
///
/// <para>Servis qatından ayrıdır ki, eyni çevirmə həm uşaq ekranında, həm
/// valideyn panelində, həm də ixracda EYNİ qaydalarla getsin. Üç yerdə üç
/// fərqli çevirmə «valideyn gördüyü ilə uşağın aldığı fərqlidir» səhvinin
/// ən qısa yoludur.</para>
/// </summary>
public static class PersonalizationMapper
{
    /// <summary>
    /// Uşağın ayarları + həll edilmiş plan.
    ///
    /// <para><paramref name="settings"/> <c>null</c> ola bilər: heç kim
    /// toxunmayıbsa standartlar göstərilir və hər sahənin mənbəyi
    /// <see cref="PetBrainSettingSource.Default"/> qalır — yəni ekran «bunu
    /// mən seçmişəm» kimi göstərmir.</para>
    /// </summary>
    public static PetBrainSettingsDto ToDto(
        ChildPersonalizationSettings? settings, PetMindContext mind, string language)
    {
        var profile = mind.Personalization;

        return new PetBrainSettingsDto
        {
            PersonalizationEnabled = settings?.PersonalizationEnabled ?? true,
            AiNarrativeEnabled = profile.AiNarrativeEnabled,
            SurpriseEnabled = profile.SurpriseEnabled,

            SessionLength = profile.SessionLength,
            SessionLengthSource = settings?.SessionLengthSource ?? PetBrainSettingSource.Default,

            Pace = profile.Pace,
            PaceSource = settings?.PaceSource ?? PetBrainSettingSource.Default,

            NoveltyTolerance = profile.NoveltyTolerance,
            NoveltyToleranceSource = settings?.NoveltyToleranceSource ?? PetBrainSettingSource.Default,

            HintStyle = profile.Support.Style,
            HintStyleSource = settings?.HintStyleSource ?? PetBrainSettingSource.Default,

            HintTiming = profile.Support.Timing,
            HintTimingSource = HintTimingSourceOf(settings, profile.Support),

            ReducedOptions = profile.Support.ReducedOptions,
            DemonstrationFirst = profile.Support.DemonstrationFirst,
            ExtraResponseTime = profile.Support.ExtraResponseTime,

            ReducedMotion = profile.Accessibility.ReducedMotion,
            LargeText = profile.Accessibility.LargeText,
            HighContrast = profile.Accessibility.HighContrast,
            IconWithText = profile.Accessibility.IconWithText,
            Narration = profile.Accessibility.Narration,
            Subtitles = profile.Accessibility.Subtitles,

            ReadingLevel = profile.Accessibility.ReadingLevel,
            ReadingLevelSource = settings?.ReadingLevelSource ?? PetBrainSettingSource.Default,

            RewardPreference = profile.RewardPreference,
            RewardPreferenceSource = settings?.RewardPreferenceSource ?? PetBrainSettingSource.Default,

            SupportLabel = RecommendationVoice.SupportLabel(profile.Support.Style, language),
            RewardLabel = RecommendationVoice.RewardLabel(profile.RewardPreference, language)
        };
    }

    private static PetBrainSettingSource HintTimingSourceOf(
        ChildPersonalizationSettings? settings, SupportPlan support)
    {
        var stored = settings?.HintTimingSource ?? PetBrainSettingSource.Default;

        if (stored != PetBrainSettingSource.Default)
            return stored;

        var storedTiming = settings?.HintTiming ?? PetBrainHintTiming.OnRequest;

        return support.Timing == storedTiming
            ? PetBrainSettingSource.Default
            : PetBrainSettingSource.Inferred;
    }

    /// <summary>
    /// Bal parçalanması — valideyn və münsif üçün.
    ///
    /// <para>Uşaq ekranında göstərilmir: uşağa cümlə lazımdır, rəqəm yox. Amma
    /// «niyə bu?» sualının hesablanmış cavabı bir yerdə qalmalıdır, yoxsa
    /// izah sonradan uydurulan bir hekayəyə çevrilir.</para>
    /// </summary>
    public static List<PetBrainFactorDto> FactorsOf(
        CandidateScore candidate, RecommendationPolicyOptions options, string language)
    {
        List<PetBrainFactorDto> factors =
        [
            Factor("topic", candidate.TopicFit, options.TopicFit, language),
            Factor("mechanic", candidate.MechanicFit, options.MechanicFit, language),
            Factor("mastery", candidate.MasteryChallengeFit, options.MasteryChallengeFit, language),
            Factor("style", candidate.StyleFit, options.StyleFit, language),
            Factor("support", candidate.SupportFit, options.SupportFit, language),
            Factor("pace", candidate.PaceFit, options.PaceFit, language),
            Factor("continuity", candidate.ContinuityFit, options.ContinuityFit, language),
            Factor("reward", candidate.RewardFit, options.RewardFit, language),
            Factor("novelty", candidate.NoveltyValue, options.NoveltyValue, language)
        ];

        if (candidate.ExplicitAdjustment != 0)
            factors.Add(Factor("explicit", candidate.ExplicitAdjustment, 1.0, language));

        return factors;
    }

    private static PetBrainFactorDto Factor(string key, int value, double weight, string language) => new()
    {
        Key = key,
        Label = RecommendationVoice.FactorLabel(key, language),
        Value = value,
        Weight = weight
    };

    /// <summary>Bir xassə sətrinin valideyn görünüşü — bal və onun ARXASINDAKI sübut.</summary>
    public static ParentProfileEntryDto ToEntry(PlayerTrait trait, DateTime now, string language) => new()
    {
        Key = trait.Key,
        Label = TraitKeys.Label(trait.Key, language),
        Icon = TraitKeys.Icon(trait.Key),
        Score = TraitEvidence.EffectiveScore(trait, now),
        Confidence = TraitEvidence.Confidence(trait, now),
        ObservationCount = trait.ObservationCount,
        PositiveEvidence = trait.PositiveEvidence,
        NegativeEvidence = trait.NegativeEvidence,
        SkipEvidence = trait.SkipEvidence,
        ExposureCount = trait.ExposureCount,
        SourceCount = TraitEvidence.SourceCount(trait),
        LastObservedAt = trait.LastObservedAt,
        ModelVersion = trait.ModelVersion
    };

    /// <summary>Bir mexanika ustalığının valideyn görünüşü.</summary>
    public static ParentMasteryEntryDto ToEntry(MechanicMastery mastery, DateTime now, string language) => new()
    {
        Key = mastery.Mechanic,
        Label = MechanicKeys.Label(mastery.Mechanic, language),
        Icon = MechanicKeys.Icon(mastery.Mechanic),
        EstimatedLevel = mastery.EstimatedLevel,
        Confidence = MechanicMasteryRules.Confidence(mastery, now),
        Attempts = mastery.Attempts,
        Successes = mastery.Successes,
        AssistedSuccesses = mastery.AssistedSuccesses,
        RecentTrend = mastery.RecentTrend,
        LastPracticedAt = mastery.LastPracticedAt,
        RecommendedBand = MechanicMasteryRules.BandFor(mastery)
    };

    /// <summary>Bir açıq məzmun seçiminin valideyn görünüşü.</summary>
    public static ParentContentPreferenceDto ToEntry(ContentPreference preference, string language) => new()
    {
        Id = preference.Id,
        Scope = preference.Scope,
        Key = preference.Key,
        Label = LabelFor(preference.Scope, preference.Key, language),
        Kind = preference.Kind,
        Source = preference.Source,
        CreatedAt = preference.CreatedAt,
        ExpiresAt = preference.ExpiresAt
    };

    /// <summary>Açarın uşağın dilində adı — sahəsinə görə doğru kataloqdan.</summary>
    public static string LabelFor(PetBrainContentScope scope, string key, string language) => scope switch
    {
        PetBrainContentScope.Template => ExperienceCatalog.Find(key)?.Title(language) ?? key,
        PetBrainContentScope.Mechanic => MechanicKeys.Label(key, language),
        _ => TraitKeys.Label(key, language)
    };
}
