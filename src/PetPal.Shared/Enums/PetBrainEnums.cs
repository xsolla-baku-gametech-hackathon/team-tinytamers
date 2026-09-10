using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>
/// Oyunçu xassəsinin ailəsi. Üç ailə qəsdəndir: MARAQ "nə haqqındadır",
/// ÜSLUB "necə oynayır", MEXANİKA isə "hansı qarşılıqlı təsiri sevir"
/// sualına cavab verir — direktor üçünü ayrı çəkilərlə işlədir.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainTraitCategory
{
    Interest = 0,
    PlayStyle = 1,

    /// <summary>
    /// Oyun MEXANİKASI: marşrut qurmaq, sıralamaq, naxış tapmaq, bəzəmək…
    ///
    /// <para><b>Mövzudan QƏSDƏN ayrıdır.</b> Uşaq kosmosu sevə, kosmos
    /// tapmacasını isə sevməyə bilər — bir bal bu iki fərqli faktı saxlaya
    /// bilmirdi və nəticədə sistem «kosmos təklif et» deyib eyni sevilməyən
    /// tapmacanı təkrarlayırdı.</para>
    /// </summary>
    Mechanic = 2
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
    MissionCompleted = 14,

    /// <summary>
    /// Uşaq ALTERNATİV kartı seçdi — əsas təklifdən imtina edərək.
    ///
    /// <para>Ayrı hadisə olması vacibdir: «uşaq macərəni başlatdı» ilə «uşaq
    /// mənim təklifimi bəyənmədi, amma yanındakını götürdü» eyni şey deyil və
    /// birincisi kimi sayılsa, siyasətin səhvi öz uğuru kimi görünərdi.</para>
    /// </summary>
    AlternativeSelected = 15,

    /// <summary>«Bunu bəyənirəm» — uşağın AÇIQ müsbət siqnalı.</summary>
    ExplicitLiked = 16,

    /// <summary>«Bunu daha az göstər» — uşağın AÇIQ mənfi siqnalı.</summary>
    ExplicitDisliked = 17,

    /// <summary>Eyni macəra yenidən oynanıldı — güclü müsbət siqnal.</summary>
    ContentReplayed = 18,

    /// <summary>Mükafat seçildi (bir neçə variant təklif olunanda).</summary>
    RewardSelected = 19,

    /// <summary>Ayar dəyişdi — uşaq və ya valideyn tərəfindən.</summary>
    SettingChanged = 20,

    /// <summary>Valideyn açıq bir qərar tətbiq etdi (blok, sıfırlama, söndürmə).</summary>
    ParentOverrideApplied = 21,

    /// <summary>Pet bir xatirəni geri çağırdı.</summary>
    MemoryReferenced = 22,

    /// <summary>İpucu FAYDALI oldu — ondan sonra tapmaca həll olundu.</summary>
    HintHelpful = 23,

    /// <summary>İlk tanışlıq tamamlandı və ya keçildi.</summary>
    OnboardingAnswered = 24
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
    Puzzle = 2,

    /// <summary>
    /// Seçimin NƏTİCƏSİ — uşaq nə etdiyini dərhal görür.
    ///
    /// <para>Seçim mərhələsindən ayrıdır: orada uşaq qərar verir, burada isə
    /// hekayə qərara cavab verir. İkisini birləşdirmək seçimi bəzək edərdi.</para>
    /// </summary>
    Consequence = 3,

    /// <summary>Macəranın sonluğu — uşaq yolunu bütöv görür.</summary>
    Ending = 4
}

/// <summary>Bir addımın nəticəsi — xülasə real qeydlərdən qurulsun deyə.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainStageResult
{
    /// <summary>Doğru/səhv anlayışı olmayan addım (giriş, seçim, nəticə).</summary>
    None = 0,

    Solved = 1,

    /// <summary>Təzyiqsiz yolda hər etibarlı cavab qəbul edilir.</summary>
    Accepted = 2
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
    FirstAdventure = 4,

    /// <summary>
    /// SEMANTİK nəticə: bir neçə epizoddan çıxarılmış yüksək səviyyəli fakt —
    /// "kəşf seçimlərinə üstünlük verir", "tapmacada dəstək faydalı olur".
    ///
    /// <para>Episodik faktlardan (yuxarıdakılardan) FƏRQLİDİR: onlar bir
    /// hadisəni saxlayır, bu isə bir NAXIŞI. Ona görə də bir epizoddan
    /// çıxarılmır — ən azı iki təsdiqlənmiş müşahidə tələb edir.</para>
    /// </summary>
    PatternLearned = 5
}

/// <summary>
/// Yaddaşın SƏVİYYƏSİ — nəyi saxladığı, nə qədər yaşadığı.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainMemoryTier
{
    /// <summary>Bir hadisə: "Ayda şimal kraterini seçdin".</summary>
    Episodic = 0,

    /// <summary>
    /// Bir neçə epizoddan çıxarılan nəticə: "kəşf seçimlərinə üstünlük verir".
    /// Daha az, daha davamlı və təmizlənərkən daha güclü qorunur.
    /// </summary>
    Semantic = 1
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
