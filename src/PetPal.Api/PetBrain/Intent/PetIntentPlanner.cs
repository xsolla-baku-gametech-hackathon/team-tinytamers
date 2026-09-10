using PetPal.Api.PetBrain.Mind;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Intent;

/// <summary>Planlayıcının qərarı — niyyət, səbəb və nə qədər davam edəcəyi.</summary>
public sealed record IntentPlan(
    PetBrainIntentType Type,
    string ReasonKey,
    TimeSpan Duration,
    string MissionKey = "");

/// <summary>
/// Pet-in niyyətini seçən DETERMİNİST planlayıcı — saf sinif.
///
/// <para>Baza yoxdur, saat yoxdur, təsadüf yoxdur: eyni kontekst həmişə eyni
/// niyyəti verir. Bu, təkcə test rahatlığı deyil — uşaq app-i bağlayıb açanda
/// pet-in "fikrini dəyişməsi" onu qeyri-real edərdi.</para>
///
/// <para><b>Prioritet sırası pozulmamalıdır:</b></para>
/// <list type="number">
///   <item><b>Ekran vaxtı.</b> Bloklu olanda YENİ fəaliyyət niyyəti başlamır —
///   pet uşağı geri çağırmaq üçün bəhanə yaratmamalıdır. Yalnız dincəlmək
///   qalır.</item>
///   <item><b>Təcili qulluq.</b> Pet acdırsa, macəra planlamaq yalandır.</item>
///   <item><b>Aktiv missiya.</b> Uşağın başladığı işi pet də davam etdirir.</item>
///   <item><b>Xarakter və yaddaş.</b> Qalan hallarda pet ÖZÜ kimi davranır.</item>
/// </list>
/// </summary>
public static class PetIntentPlanner
{
    /// <summary>Qaydalar dəyişəndə artır — saxlanan niyyət hansı versiya ilə qurulduğunu bilir.</summary>
    public const int Version = 1;

    // ---- Səbəb açarları (QAPALI siyahı) ----
    public const string ReasonLowEnergy = "low-energy";
    public const string ReasonHungry = "hungry";
    public const string ReasonUnclean = "unclean";
    public const string ReasonQuietHours = "quiet-hours";
    public const string ReasonMission = "mission";
    public const string ReasonCurious = "curious";
    public const string ReasonInspired = "inspired";
    public const string ReasonRemembered = "remembered";
    public const string ReasonJustBecause = "just-because";

    /// <summary>Niyyət bu qədər çəkir — uşaq növbəti açılışda nəticəni görsün deyə qısadır.</summary>
    private static readonly TimeSpan Short = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan Medium = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan Long = TimeSpan.FromHours(2);

    /// <summary>
    /// Növbəti niyyət. Yumurta üçün <c>null</c> — açılmamış pet nə edir?
    /// </summary>
    public static IntentPlan? Plan(PetMindContext mind)
    {
        if (!mind.PetIsHatched)
            return null;

        // 1) EKRAN VAXTI hər şeydən üstündür.
        //
        // Bloklu olanda pet yeni fəaliyyət planlamır: "səni gözləyirəm",
        // "gəl oynayaq" tipli niyyət uşağı qaydanı pozmağa çağırardı.
        // Dincəlmək isə uşağı çağırmır — sadəcə pet də istirahət edir.
        if (mind.ActivityBlocked)
            return new IntentPlan(PetBrainIntentType.Rest, ReasonQuietHours, Long);

        // 2) TƏCİLİ qulluq — pet acdırsa macəra planlamaq yalan olardı.
        if (mind.Fullness == PetBrainCareBand.Urgent)
            return new IntentPlan(PetBrainIntentType.Care, ReasonHungry, Short);

        if (mind.Cleanliness == PetBrainCareBand.Urgent)
            return new IntentPlan(PetBrainIntentType.Care, ReasonUnclean, Short);

        if (mind.Energy == PetBrainCareBand.Urgent)
            return new IntentPlan(PetBrainIntentType.Rest, ReasonLowEnergy, Medium);

        // 3) Gecədir — pet də yatır. Uşağı oyatmaq üçün heç bir bəhanə yoxdur.
        if (mind.SessionBucket == PetBrainSessionBucket.Night)
            return new IntentPlan(PetBrainIntentType.Rest, ReasonQuietHours, Long);

        // 4) Uşağın başladığı missiya — pet onu tək qoymur.
        if (mind.ActiveMissionKeys.Count > 0)
            return new IntentPlan(
                PetBrainIntentType.Help, ReasonMission, Medium, mind.ActiveMissionKeys[0]);

        // 5) Yaddaşdan gələn niyyət: pet birlikdə etdikləri bir şeyə qayıdır.
        if (mind.Memories.FirstOrDefault(m => m.Kind == PetBrainMemoryKind.PatternLearned) is { } pattern)
            return new IntentPlan(PatternIntent(pattern.FactKey), ReasonRemembered, Medium);

        // 6) Xarakter — pet ÖZÜ kimi davranır.
        return mind.Personality switch
        {
            PetBrainPersonality.CuriousScientist =>
                new IntentPlan(PetBrainIntentType.Learn, ReasonCurious, Medium),

            PetBrainPersonality.CreativeCompanion =>
                new IntentPlan(PetBrainIntentType.Create, ReasonInspired, Medium),

            PetBrainPersonality.ExplorerCompanion =>
                new IntentPlan(PetBrainIntentType.Explore, ReasonCurious, Medium),

            PetBrainPersonality.CaringCompanion =>
                new IntentPlan(PetBrainIntentType.Help, ReasonInspired, Medium),

            _ => new IntentPlan(PetBrainIntentType.Play, ReasonJustBecause, Short)
        };
    }

    /// <summary>Öyrənilmiş naxışdan çıxan niyyət.</summary>
    private static PetBrainIntentType PatternIntent(string factKey) => factKey switch
    {
        SemanticMemory.PrefersExploring => PetBrainIntentType.Explore,
        SemanticMemory.PrefersCreating => PetBrainIntentType.Create,
        SemanticMemory.PrefersSolving => PetBrainIntentType.Learn,
        SemanticMemory.PrefersHelping => PetBrainIntentType.Help,
        _ => PetBrainIntentType.Play
    };

    /// <summary>
    /// Niyyətin NƏTİCƏSİ — təsdiqlənmiş artefakt açarı.
    ///
    /// <para>Determinist seçim: niyyətin id-sindən çıxarılır, yəni eyni niyyət
    /// həmişə eyni nəticəni verir və "sən yoxkən..." cümləsi yenilənmədən
    /// sonra dəyişmir.</para>
    /// </summary>
    public static string OutcomeFor(PetBrainIntentType type, Guid intentId)
    {
        var options = Outcomes(type);

        return options[(int)(StableHash.Unit($"petbrain-intent:{intentId:N}") * options.Count) % options.Count];
    }

    /// <summary>Hər niyyətin təsdiqlənmiş nəticə açarları — QAPALI siyahı.</summary>
    public static IReadOnlyList<string> Outcomes(PetBrainIntentType type) => type switch
    {
        PetBrainIntentType.Rest => ["nap", "stargazing", "daydream"],
        PetBrainIntentType.Explore => ["pebble", "feather", "map-corner"],
        PetBrainIntentType.Create => ["sketch", "pattern", "tiny-tower"],
        PetBrainIntentType.Help => ["tidy-room", "watered-plant", "mission-note"],
        PetBrainIntentType.Learn => ["new-word", "star-fact", "leaf-shape"],
        PetBrainIntentType.Play => ["bubble-game", "hide-and-seek", "ball-toss"],
        _ => ["snack", "warm-bath", "brushed-fur"]
    };
}
