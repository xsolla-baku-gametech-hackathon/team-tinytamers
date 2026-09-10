using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Mind;

/// <summary>Bir keçmiş macəranın NƏTİCƏSİ — direktorun yeniliyi hesabladığı giriş.</summary>
public sealed record MindRunOutcome(
    string TemplateKey,
    string Theme,
    PetBrainExperienceType Type,
    PetBrainRunStatus Status,
    int ScorePercent,
    int HintsUsed,
    int Mistakes,
    string EndingKey)
{
    /// <summary>
    /// Bu nəticə ÇƏTİNLİK siqnalı verirmi.
    ///
    /// <para>Yaradıcı macərada doğru/səhv yoxdur və nəticə həmişə 100-dür —
    /// onu məharət kimi oxumaq çətinliyi haqsız yerə qaldırardı.</para>
    /// </summary>
    public bool CountsForDifficulty =>
        Status == PetBrainRunStatus.Completed && Type != PetBrainExperienceType.Creative;
}

/// <summary>
/// Yaddaşın QƏRAR üçün lazım olan hissəsi — cümlə deyil, açarlar.
/// </summary>
public sealed record MindMemory(
    Guid Id,
    PetBrainMemoryKind Kind,
    string FactKey,
    string ValueKey,
    int Importance,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    IReadOnlyList<string> Tags);

/// <summary>
/// Pet-in dünya haqqında BİLDİYİ hər şey — bir yerdə, bir dəfə yığılmış.
///
/// <para><b>Nə üçün ortaq model?</b> Ana ekran, söhbət, qulluq və Pet Brain
/// ayrı-ayrılıqda öz kontekstini qurduqda pet bir ekranda "yorğunam", digərində
/// "gəl macərəya çıxaq" deyirdi — çünki iki ekran eyni anda fərqli həqiqətə
/// baxırdı. İndi hamısı bu modeldən oxuyur.</para>
///
/// <para><b>Buraya heç vaxt düşməyənlər</b> — dəqiq yaş, uşağın söhbət mətni,
/// şəkillər, ünvan, məktəb, cihazın yeri, sağlamlıq və sensor məlumatı. Model
/// yalnız ZOLAQ və AÇAR daşıyır, ona görə "səhvən nəyisə göndərmək" mümkün
/// deyil: göndəriləsi sahə ümumiyyətlə mövcud deyil.</para>
/// </summary>
public sealed record PetMindContext(
    Guid ChildId,

    /// <summary>Dəqiq yaş YOX, zolaq — <c>5-6</c>, <c>7-8</c>, <c>9-10</c>.</summary>
    string AgeBand,

    string Language,

    /// <summary>Yaş həddi yoxlamaları üçün — qərar qatına düşmür, süzgəcə düşür.</summary>
    int AgeForSafetyLimits,

    bool PetIsHatched,
    string PetSpecies,
    PetStage PetStage,
    PetMood Mood,

    PetBrainCareBand Happiness,
    PetBrainCareBand Energy,
    PetBrainCareBand Fullness,
    PetBrainCareBand Cleanliness,

    int Bond,
    PetBrainBondTier BondTier,

    /// <summary>SAXLANAN xarakter — hər sorğuda yenidən çıxarılan deyil.</summary>
    PetBrainPersonality Personality,

    /// <summary>Köhnəlmə tətbiq olunmuş EFFEKTİV maraq balları.</summary>
    IReadOnlyDictionary<string, int> Interests,

    IReadOnlyDictionary<string, int> PlayStyles,

    /// <summary>
    /// Açar üzrə İNAM (0–100): nə qədər müşahidə, nə qədər müxtəlif mənbə,
    /// nə qədər təzə, nə qədər ziddiyyətsiz.
    ///
    /// <para>Baldan AYRIDIR və bu, qəsdəndir: «70 bal, amma cəmi bir
    /// müşahidə» ilə «70 bal, üç mənbədən on müşahidə» eyni şey deyil.</para>
    /// </summary>
    IReadOnlyDictionary<string, int> InterestConfidence,

    IReadOnlyDictionary<string, int> PlayStyleConfidence,

    /// <summary>
    /// MEXANİKA üstünlükləri — mövzudan qəsdən ayrı.
    ///
    /// <para>Uşaq kosmosu sevib marşrut tapmacasını sevməyə bilər. İkisi bir
    /// balda birləşəndə sistem «kosmos təklif et» deyib eyni sevilməyən
    /// mexanikanı təkrarlayırdı.</para>
    /// </summary>
    IReadOnlyDictionary<string, int> Mechanics,

    IReadOnlyDictionary<string, int> MechanicConfidence,

    /// <summary>
    /// Mexanika üzrə USTALIQ — üstünlükdən AYRI.
    ///
    /// <para>Sevmək və bacarmaq eyni şey deyil: uşaq marşrutu sevə, amma hələ
    /// bacarmaya bilər. Birincisi «nə təklif edim», ikincisi «hansı çətinlikdə»
    /// sualına cavab verir.</para>
    /// </summary>
    IReadOnlyDictionary<string, int> MechanicMastery,

    IReadOnlyDictionary<string, PetBrainDifficulty> MechanicChallengeBand,

    /// <summary>Ən yenidən köhnəyə doğru.</summary>
    IReadOnlyList<MindRunOutcome> RecentOutcomes,

    IReadOnlySet<string> CompletedTemplates,

    /// <summary>Seçim üçün NAMİZƏD xatirələr — hamısı deyil, uyğun olanlar.</summary>
    IReadOnlyList<MindMemory> Memories,

    IReadOnlyList<string> ActiveMissionKeys,
    PetBrainDailyGoalBand DailyGoal,
    WorldWeather Weather,
    PetBrainScreenTimeBand ScreenTime,
    PetBrainSessionBucket SessionBucket,

    /// <summary>Son MƏNALI qarşılıqlı təsirdən keçən vaxt; heç vaxt olmayıbsa <c>null</c>.</summary>
    TimeSpan? SinceLastInteraction,

    /// <summary>Yarımçıq macəra — varsa təklif əvəzinə davam təklif olunur.</summary>
    string? UnfinishedTemplateKey,

    /// <summary>Bu sessiyada uşağın "sonra" və ya "başqa fikir" dediyi şablonlar.</summary>
    IReadOnlySet<string> DeclinedTemplates,

    PetBrainDifficulty Difficulty,

    /// <summary>
    /// AÇIQ fərdiləşdirmə qatı: ayarlar, dəstək planı, əlçatanlıq.
    ///
    /// <para>Xassələrdən qəsdən ayrıdır: xassə öyrənilən ehtimaldır və
    /// köhnəlir, ayar isə qərardır və heç bir davranış siqnalı onu
    /// üstələmir.</para>
    /// </summary>
    PersonalizationProfile Personalization,

    /// <summary>Valideynin bloklandığı mövzular — namizəd hovuzundan çıxır.</summary>
    IReadOnlySet<string> BlockedThemes,

    /// <summary>Valideynin bloklandığı macəralar.</summary>
    IReadOnlySet<string> BlockedTemplates,

    /// <summary>Uşağın «daha az göstər» dediyi mövzular — geri çəkilir, yox olmur.</summary>
    IReadOnlySet<string> ShowLessThemes,

    /// <summary>Uşağın «daha az göstər» dediyi macəralar.</summary>
    IReadOnlySet<string> ShowLessTemplates,

    /// <summary>Uşağın açıq bəyəndiyi mövzular.</summary>
    IReadOnlySet<string> LikedThemes,

    /// <summary>Uşağın açıq bəyəndiyi macəralar.</summary>
    IReadOnlySet<string> LikedTemplates,

    /// <summary>
    /// Profilin ÜMUMİ inamı (0–100) — «bu uşaq haqqında nə qədər şey bilirik».
    ///
    /// <para>Aşağı olanda sistem daha çox kəşf edir və izahı dürüst saxlayır
    /// («hələ tanış oluruq»), yüksək olanda isə üstünlüyə güvənir.</para>
    /// </summary>
    int ProfileConfidence)
{
    /// <summary>Qulluq ehtiyacı TƏCİLİDİR — pet macərədən əvvəl köməyə ehtiyac duyur.</summary>
    public bool NeedsCare =>
        Happiness == PetBrainCareBand.Urgent
        || Energy == PetBrainCareBand.Urgent
        || Fullness == PetBrainCareBand.Urgent
        || Cleanliness == PetBrainCareBand.Urgent;

    /// <summary>Yeni fəaliyyət başlamaq olmur.</summary>
    public bool ActivityBlocked => ScreenTime == PetBrainScreenTimeBand.Blocked;

    /// <summary>
    /// Mövcud direktor kontekstinə çevirir.
    ///
    /// <para>Direktorun öz müqaviləsi qəsdən dar saxlanılır: o, SAF qərar
    /// qatıdır və bu modelin bütün sahələrinə ehtiyacı yoxdur. Çevirmə burada
    /// olduğuna görə hər çağıran eyni cür kontekst qurur.</para>
    /// </summary>
    public PetBrainDirectorContext ToDirectorContext() => new(
        ChildId: ChildId,
        Age: AgeForSafetyLimits,
        Language: Language,
        Interests: Interests,
        PlayStyles: PlayStyles,
        RecentRuns: [.. RecentOutcomes.Select(o => new RunHistoryEntry(o.TemplateKey, o.Theme, o.Status))],
        CompletedTemplates: CompletedTemplates,
        Difficulty: Difficulty,
        PetIsHatched: PetIsHatched);
}
