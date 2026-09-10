using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>
/// Oyunçu xassəsinin ailəsi. İki ailə qəsdəndir: MARAQ "nə haqqındadır"
/// sualına, ÜSLUB isə "necə oynayır" sualına cavab verir — direktor ikisini
/// ayrı çəkilərlə işlədir.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainTraitCategory
{
    Interest = 0,
    PlayStyle = 1
}

/// <summary>
/// İzlənən davranış hadisələri. Siyahı QAPALIDIR: klient ixtiyari hadisə adı
/// göndərə bilmir, domen endpoint-ləri yalnız buradakı dəyərləri işlədir.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainEventType
{
    RecommendationViewed = 0,
    RecommendationSelected = 1,
    ExperienceStarted = 2,
    StageChoiceMade = 3,
    HintRequested = 4,
    PuzzleSolved = 5,
    PuzzleFailed = 6,
    ExperienceCompleted = 7,
    ExperienceAbandoned = 8,
    LearningAnswerValidated = 9,
    LearningSessionCompleted = 10,
    MiniGameCompleted = 11,
    PetCared = 12,
    AccessoryEquipped = 13,
    MissionCompleted = 14
}

/// <summary>Təcrübənin janrı — macəra doğru/səhv daşıyır, yaradıcı isə daşımır.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainExperienceType
{
    Adventure = 0,
    Creative = 1
}

/// <summary>
/// Təcrübənin çətinlik pilləsi. Bu, məktəb reytinqi (<see cref="SkillArea"/> üzrə
/// Elo) DEYİL — təcrübənin ritmini ölçür və ondan qəsdən ayrıdır.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainDifficulty
{
    Easy = 0,
    Medium = 1,
    Hard = 2
}

/// <summary>Mərhələnin növü — UI hər növü ayrı çəkir, server hər növü ayrı yoxlayır.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainStageKind
{
    /// <summary>Pet-in giriş replikası; uşaq yalnız "davam" deyir.</summary>
    Intro = 0,

    /// <summary>Təsdiqlənmiş variantlardan biri seçilir.</summary>
    Choice = 1,

    /// <summary>Serverin verdiyi tapmaca; cavab serverdə yoxlanılır.</summary>
    Puzzle = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainRunStatus
{
    Active = 0,
    Completed = 1,
    Abandoned = 2
}

/// <summary>
/// Uzunmüddətli yaddaşın növü. Açar siyahısı qapalıdır — söhbətdən və ya
/// sərbəst mətndən yeni növ yaranmır.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainMemoryKind
{
    /// <summary>"Ay macərasını birlikdə bitirdik."</summary>
    ExperienceCompleted = 0,

    /// <summary>"Robonu günəş paneli ilə xilas etdin."</summary>
    ChoiceMade = 1,

    /// <summary>"Kosmos tapmacalarını təkrar-təkrar seçirsən."</summary>
    PreferenceObserved = 2,

    /// <summary>"Mars dəbilqəsini açdın."</summary>
    CosmeticUnlocked = 3,

    /// <summary>"Bu, bizim ilk böyük macəramız idi."</summary>
    FirstAdventure = 4
}

/// <summary>
/// Pet-in yoldaşlıq xarakteri — uşağın SABİT xassələrindən çıxarılır, ayrıca
/// saxlanılmır. Növ, yaş və mərhələ ilə heç bir əlaqəsi yoxdur.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainPersonality
{
    Balanced = 0,
    CuriousScientist = 1,
    CreativeCompanion = 2,
    ExplorerCompanion = 3,
    CaringCompanion = 4
}
