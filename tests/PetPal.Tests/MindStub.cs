using PetPal.Api.PetBrain.Mind;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Testlər üçün <see cref="PetMindContext"/> quran ORTAQ yer.
///
/// <para>Kontekst qəsdən genişdir — pet-in bildiyi hər şey oradadır — və hər
/// test faylının onu əl ilə qurması iki problem yaradırdı: yeni sahə əlavə
/// edəndə beş fayl sınırdı, və iki testin «eyni» konteksti əslində fərqli
/// olurdu. İndi standart bir yerdədir, test isə yalnız ÖZ mövzusu olan sahəni
/// dəyişir.</para>
/// </summary>
public static class MindStub
{
    public static readonly Guid DefaultChildId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static PetMindContext Build(
        Guid? childId = null,
        PetBrainScreenTimeBand screenTime = PetBrainScreenTimeBand.Plenty,
        PetBrainSessionBucket bucket = PetBrainSessionBucket.Afternoon,
        PetBrainPersonality personality = PetBrainPersonality.Balanced,
        IReadOnlyList<string>? missions = null,
        IReadOnlyList<MindMemory>? memories = null,
        bool hatched = true,
        int age = 9,
        IReadOnlyDictionary<string, int>? interests = null,
        IReadOnlyDictionary<string, int>? playStyles = null,
        IReadOnlyDictionary<string, int>? mechanics = null,
        IReadOnlyDictionary<string, int>? mechanicMastery = null,
        IReadOnlyDictionary<string, PetBrainDifficulty>? mechanicBands = null,
        IReadOnlyList<MindRunOutcome>? recentOutcomes = null,
        IReadOnlySet<string>? completedTemplates = null,
        IReadOnlySet<string>? declinedTemplates = null,
        IReadOnlySet<string>? blockedThemes = null,
        IReadOnlySet<string>? blockedTemplates = null,
        IReadOnlySet<string>? showLessThemes = null,
        IReadOnlySet<string>? showLessTemplates = null,
        IReadOnlySet<string>? likedThemes = null,
        IReadOnlySet<string>? likedTemplates = null,
        string? unfinishedTemplateKey = null,
        PetBrainDifficulty difficulty = PetBrainDifficulty.Medium,
        PersonalizationProfile? personalization = null,
        int profileConfidence = 50,
        TimeSpan? sinceLastInteraction = null) => new(
        ChildId: childId ?? DefaultChildId,
        AgeBand: AgeBands.Of(age),
        Language: "az",
        AgeForSafetyLimits: age,
        PetIsHatched: hatched,
        PetSpecies: "fox",
        PetStage: PetStage.Child,
        Mood: PetMood.Happy,
        Happiness: PetBrainCareBand.Great,
        Energy: PetBrainCareBand.Great,
        Fullness: PetBrainCareBand.Great,
        Cleanliness: PetBrainCareBand.Great,
        Bond: 40,
        BondTier: PetBrainBondTier.TrustedFriend,
        Personality: personality,
        Interests: interests ?? new Dictionary<string, int>(),
        PlayStyles: playStyles ?? new Dictionary<string, int>(),
        InterestConfidence: new Dictionary<string, int>(),
        PlayStyleConfidence: new Dictionary<string, int>(),
        Mechanics: mechanics ?? new Dictionary<string, int>(),
        MechanicConfidence: new Dictionary<string, int>(),
        MechanicMastery: mechanicMastery ?? new Dictionary<string, int>(),
        MechanicChallengeBand: mechanicBands ?? new Dictionary<string, PetBrainDifficulty>(),
        RecentOutcomes: recentOutcomes ?? [],
        CompletedTemplates: completedTemplates ?? new HashSet<string>(),
        Memories: memories ?? [],
        ActiveMissionKeys: missions ?? [],
        DailyGoal: PetBrainDailyGoalBand.InProgress,
        Weather: WorldWeather.Clear,
        ScreenTime: screenTime,
        SessionBucket: bucket,
        SinceLastInteraction: sinceLastInteraction ?? TimeSpan.FromHours(3),
        UnfinishedTemplateKey: unfinishedTemplateKey,
        DeclinedTemplates: declinedTemplates ?? new HashSet<string>(),
        Difficulty: difficulty,
        Personalization: personalization ?? DefaultPersonalization(),
        BlockedThemes: blockedThemes ?? new HashSet<string>(),
        BlockedTemplates: blockedTemplates ?? new HashSet<string>(),
        ShowLessThemes: showLessThemes ?? new HashSet<string>(),
        ShowLessTemplates: showLessTemplates ?? new HashSet<string>(),
        LikedThemes: likedThemes ?? new HashSet<string>(),
        LikedTemplates: likedTemplates ?? new HashSet<string>(),
        ProfileConfidence: profileConfidence);

    /// <summary>Heç kim ayarlara toxunmayıb — məhsulun standartları.</summary>
    public static PersonalizationProfile DefaultPersonalization() =>
        PersonalizationProfileFactory.Build(settings: null, recentOutcomes: []);
}
