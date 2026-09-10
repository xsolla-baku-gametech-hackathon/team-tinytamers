using System.ComponentModel.DataAnnotations;
using PetPal.Shared.Enums;

namespace PetPal.Shared.Dtos.PetBrain;

/// <summary>
/// Pet Brain giriş ekranının tam vəziyyəti — bir sorğuda yığılır.
/// </summary>
public class PetBrainStateDto
{
    /// <summary>Özəllik konfiqurasiya ilə bağlıdırsa ekran nəzakətli boş hal göstərir.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Yumurta macəraya çıxmır. Bağlıdırsa uşağa "əvvəlcə yumurtanı aç" deyilir —
    /// mövcud yumurta axını ilə eyni qayda.
    /// </summary>
    public bool PetIsHatched { get; set; }

    public string PetName { get; set; } = string.Empty;

    /// <summary>Pet-in salamlaması — yaddaşdan gələn bir detalı daşıyır.</summary>
    public string Greeting { get; set; } = string.Empty;

    /// <summary>Bağ (0–100) — xoşbəxtlikdən fərqli, YAVAŞ artan uzunmüddətli dəyər.</summary>
    public int Bond { get; set; }

    /// <summary>Bağın pilləsi — bar əvəzinə görünən bir mərtəbə.</summary>
    public PetBrainBondTier BondTier { get; set; }

    public string BondTierLabel { get; set; } = string.Empty;

    /// <summary>Növbəti pilləyə çatmaq üçün lazım olan bal; sonuncuda <c>0</c>.</summary>
    public int BondNextThreshold { get; set; }

    /// <summary>Bu pillənin açdığı emote — UI onu pet-in yanında göstərir.</summary>
    public string BondEmote { get; set; } = string.Empty;

    /// <summary>Pilləni izah edən bir cümlə (vəd, tələb deyil).</summary>
    public string BondUnlockLine { get; set; } = string.Empty;

    public PetBrainPersonality Personality { get; set; }

    /// <summary>Xarakterin uşağın dilində adı.</summary>
    public string PersonalityLabel { get; set; } = string.Empty;

    public List<PetBrainTraitDto> Interests { get; set; } = new();
    public List<PetBrainTraitDto> PlayStyles { get; set; } = new();

    /// <summary>Son mənalı xatirələr — uşağa təbii cümlə kimi göstərilir.</summary>
    public List<PetBrainMemoryDto> Memories { get; set; } = new();

    /// <summary>Növbəti təcrübə üçün direktorun qərarı. Bloklu halda boş qala bilər.</summary>
    public PetBrainRecommendationDto? Recommendation { get; set; }

    /// <summary>Yarımçıq qalmış təcrübə — uşaq davam edə bilsin deyə.</summary>
    public PetBrainRunDto? ActiveRun { get; set; }

    /// <summary>Ekran vaxtı bloku — yeni təcrübə başlamaq olmur.</summary>
    public bool ScreenTimeBlocked { get; set; }

    public string ScreenTimeMessage { get; set; } = string.Empty;

    /// <summary>Nümayiş/izah paneli açıqdırmı (yalnız <c>PetBrain:DemoMode</c> ilə).</summary>
    public bool DemoMode { get; set; }

    /// <summary>Yalnız <see cref="DemoMode"/> açıq olanda dolur.</summary>
    public PetBrainDebugDto? Debug { get; set; }
}

public class PetBrainTraitDto
{
    public string Key { get; set; } = string.Empty;

    /// <summary>Uşağın dilində ad — "Kosmos" / "Space".</summary>
    public string Label { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    /// <summary>0–100.</summary>
    public int Score { get; set; }
}

public class PetBrainMemoryDto
{
    public PetBrainMemoryKind Kind { get; set; }

    /// <summary>Hazır, uşağa uyğun cümlə: "Ay macərasını birlikdə bitirdik!"</summary>
    public string Text { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    /// <summary>0–100 — yaddaşın seçilmə prioriteti.</summary>
    public int Importance { get; set; }
}

/// <summary>
/// Direktorun qərarı. Struktur qapalıdır: klient nə şablon, nə mükafat, nə də
/// çətinlik təklif edə bilir — hamısı serverdən gəlir.
/// </summary>
public class PetBrainRecommendationDto
{
    /// <summary>
    /// Bu qərarın id-si. Uşağın cavabı (<c>başla</c>, <c>başqa fikir</c>,
    /// <c>sonra</c>) məhz buna bağlanır.
    ///
    /// <para>Klient nə şablon, nə bal dəyişikliyi göndərə bilir — yalnız
    /// serverin verdiyi bu id-ni geri qaytarır. Yad və ya köhnəlmiş id rədd
    /// olunur.</para>
    /// </summary>
    public Guid DecisionId { get; set; }

    public string TemplateKey { get; set; } = string.Empty;
    public PetBrainExperienceType ExperienceType { get; set; }

    /// <summary>Təsdiqlənmiş mövzu taksonomiyasından: space, animals, fantasy…</summary>
    public string Theme { get; set; } = string.Empty;

    public string ActivityType { get; set; } = string.Empty;
    public PetBrainDifficulty Difficulty { get; set; }
    public int TargetMinutes { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Intro { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    /// <summary>İlk tamamlamada açılan kosmetik əşyanın kodu; yoxdursa boş.</summary>
    public string RewardCode { get; set; } = string.Empty;

    /// <summary>Bu təcrübə artıq bir dəfə tamamlanıb — kosmetik təkrar açılmır.</summary>
    public bool AlreadyCompleted { get; set; }

    /// <summary>
    /// İki–dörd qısa, hazır izah. Bunlar modelin "düşüncəsi" DEYİL: strukturlu
    /// girişlərdən hesablanmış cümlələrdir.
    /// </summary>
    public List<string> Reasons { get; set; } = new();

    /// <summary>Mətn şablondan, yoxsa modeldən gəldi — nümayiş panelində dürüstlük üçün.</summary>
    public string NarrativeSource { get; set; } = "template";

    /// <summary>Pet-in xarakterinə uyğun bir cümlə — nişan deyil, səs.</summary>
    public string PetLine { get; set; } = string.Empty;

    /// <summary>
    /// "Başqa fikir" hələ mümkündürmü. Sonsuz yeniləmə YOXDUR: kart dəyişimi
    /// sessiya başına məhduddur və limit dolanda düymə gizlənir.
    /// </summary>
    public bool CanShowAnother { get; set; } = true;
}

/// <summary>Başlanmış təcrübənin cari vəziyyəti — yenilənmədən sonra bərpa üçün kifayətdir.</summary>
public class PetBrainRunDto
{
    public Guid RunId { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public PetBrainExperienceType ExperienceType { get; set; }
    public string Theme { get; set; } = string.Empty;

    /// <summary>Səhnənin vizual açarı — UI hansı fon və rəngləri çəkəcəyini bilir.</summary>
    public string SceneKey { get; set; } = string.Empty;

    public PetBrainDifficulty Difficulty { get; set; }
    public PetBrainRunStatus Status { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>0-dan başlayır; <see cref="StageCount"/>-a çatanda təcrübə bitirilə bilər.</summary>
    public int CurrentStage { get; set; }
    public int StageCount { get; set; }

    /// <summary>
    /// Budaqlanan macərada uşağın atdığı addım sayı.
    ///
    /// <para>Budaqlarda "5 mərhələdən 3-cü" ifadəsi yanlışdır — yollar müxtəlif
    /// uzunluqdadır. Ona görə ekran keçilmiş addımları və TƏXMİNİ uzunluğu
    /// göstərir, faiz vəd etmir.</para>
    /// </summary>
    public int StepsTaken { get; set; }

    /// <summary>Ən uzun yolun addım sayı — irəliləmə göstəricisinin miqyası.</summary>
    public int EstimatedSteps { get; set; }

    /// <summary>Macəra bitibsə hansı sonluqla; əks halda boş.</summary>
    public string EndingKey { get; set; } = string.Empty;

    /// <summary>
    /// Uşağın seçdiyi yol — ekrandakı sadə cığır göstəricisi üçün.
    /// Hər element bir addımın işarəsi və etiketidir.
    /// </summary>
    public List<PetBrainPathStepDto> Path { get; set; } = new();

    /// <summary>Cari mərhələ; run bitibsə boş qalır.</summary>
    public PetBrainStageDto? Stage { get; set; }

    /// <summary>
    /// Qarşıdakı tapmacanın səhnəsi. Cari mərhələ tapmacadırsa və ya qarşıda
    /// tapmaca yoxdursa boş qalır.
    /// </summary>
    public PetBrainUpcomingSceneDto? UpcomingScene { get; set; }

    /// <summary>İndiyə qədər seçilmiş variantların açarları — səhnə onlara görə dəyişir.</summary>
    public List<string> Choices { get; set; } = new();

    public int HintsUsed { get; set; }
    public int Mistakes { get; set; }

    /// <summary>Run tamamlanıbsa yekun ekranın məlumatı.</summary>
    public PetBrainSummaryDto? Summary { get; set; }
}

/// <summary>
/// Yolun bir addımı — uşağa öz cığırını göstərmək üçün.
///
/// <para>Rəng TƏK daşıyıcı deyil: hər addımın işarəsi və mətn etiketi var,
/// ona görə göstərici rəng görməyən uşaq üçün də oxunur.</para>
/// </summary>
public class PetBrainPathStepDto
{
    /// <summary>Neçənci addım (1-dən).</summary>
    public int Ordinal { get; set; }

    public string Icon { get; set; } = string.Empty;

    /// <summary>Uşağın dilində qısa etiket — "Şimal krateri".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Bu addım uşağın SEÇİMİ idimi (yoxsa hekayənin öz addımı).</summary>
    public bool WasChoice { get; set; }
}

public class PetBrainStageDto
{
    public int Index { get; set; }

    /// <summary>
    /// Budaqlanan macərada cari düyünün açarı; xətti macərada boşdur.
    ///
    /// <para>Klient onu geri qaytarır və server uyğunluğu yoxlayır — yəni iki
    /// dəfə basmaq və ya köhnə ekrandan cavab göndərmək mümkün deyil. Açar
    /// SEÇMƏK üçün deyil, TƏSDİQLƏMƏK üçündür: klient başqa düyünə keçə bilmir.</para>
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>Səhnənin variantı — uşağın seçimləri onu dəyişir.</summary>
    public string SceneVariant { get; set; } = string.Empty;

    /// <summary>
    /// Pet-in əvvəlki macəradan xatırladığı bir detal; yoxdursa boş.
    /// UI onu "Mən bunu xatırlayıram" nişanı ilə göstərir.
    /// </summary>
    public string MemoryCallback { get; set; } = string.Empty;

    public PetBrainStageKind Kind { get; set; }

    /// <summary>Ekranın ən böyük yazısı — mərhələ boyu qalır.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Pet-in bu mərhələdə dediyi replika.</summary>
    public string PetLine { get; set; } = string.Empty;

    public List<PetBrainOptionDto> Options { get; set; } = new();

    /// <summary>
    /// Tapmaca mərhələsində uşağa VERİLMİŞ tapmaca. Seçim mərhələsində boşdur.
    ///
    /// <para>Cavabsızdır: doğru həll yalnız serverdə saxlanılır və heç vaxt
    /// göndərilmir — bax <see cref="PetBrainPuzzleDto"/>.</para>
    /// </summary>
    public PetBrainPuzzleDto? Puzzle { get; set; }

    /// <summary>Yalnız tapmaca mərhələsində: ipucu istənə bilər.</summary>
    public bool SupportsHint { get; set; }

    /// <summary>İpucu istənibsə mətni burada gəlir (server verir, klient uydurmur).</summary>
    public string Hint { get; set; } = string.Empty;
}

public class PetBrainOptionDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    /// <summary>Seçimin qısa təsviri — uşaq nəticəni əvvəlcədən görsün.</summary>
    public string Detail { get; set; } = string.Empty;
}

public class PetBrainSummaryDto
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Pet-in yekun replikası.</summary>
    public string PetLine { get; set; } = string.Empty;

    public int XpEarned { get; set; }
    public int BondEarned { get; set; }
    public int BondTotal { get; set; }

    /// <summary>Bu tamamlamada açılan kosmetik əşya (ilk dəfə); yoxdursa boş.</summary>
    public string UnlockedAccessoryCode { get; set; } = string.Empty;
    public string UnlockedAccessoryName { get; set; } = string.Empty;
    public string UnlockedAccessoryIcon { get; set; } = string.Empty;

    /// <summary>Bu təcrübədən yaranan yeni xatirələr.</summary>
    public List<PetBrainMemoryDto> NewMemories { get; set; } = new();

    /// <summary>Pet-in yaşı artdımı.</summary>
    public bool PetLeveledUp { get; set; }
    public int PetLevel { get; set; }

    /// <summary>
    /// Macəranın 10 saniyəlik xülasəsi. HƏMİŞƏ doludur — video hazır olmasa da
    /// deterministik storyboard oynayır.
    /// </summary>
    public PetBrainRecapDto Recap { get; set; } = new();
}

/// <summary>
/// Uşağın seçimlərindən qurulan 10 saniyəlik xülasə.
///
/// <para><b>Altyazılar videonun içində DEYİL</b>, burada, serverin qurduğu
/// deterministik mətndədir. Nəticə: model kiçik vizual uyğunsuzluq versə belə,
/// ekranda yazılan şey uşağın həqiqətən seçdiyidir.</para>
/// </summary>
public class PetBrainRecapDto
{
    public PetBrainRecapStatus Status { get; set; } = PetBrainRecapStatus.Fallback;

    /// <summary>
    /// Videonun ünvanı — YALNIZ app-in öz, sahiblik yoxlanan endpoint-i.
    /// Provayderin URL-i heç vaxt klientə verilmir. Hazır deyilsə boşdur.
    /// </summary>
    public string VideoUrl { get; set; } = string.Empty;

    /// <summary>Tam 10.0 saniyə — həm video, həm də deterministik storyboard üçün.</summary>
    public double DurationSeconds { get; set; } = 10;

    /// <summary>Üç kadr: vaxt, altyazı və işarə.</summary>
    public List<PetBrainRecapShotDto> Shots { get; set; } = new();
}

/// <summary>Bir kadr — uşağa göstərilən hissə.</summary>
public class PetBrainRecapShotDto
{
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }

    /// <summary>Uşağın dilində, onun HƏQİQİ seçimini deyən cümlə.</summary>
    public string Caption { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// Təcrübəni başlatmaq. Şablon açarı MƏCBURİ DEYİL: verilməsə server öz
/// tövsiyəsini başladır. Verilibsə, o, hazırkı tövsiyə ilə ÜST-ÜSTƏ DÜŞMƏLİDİR —
/// yəni klient kataloqdan istədiyi macərəni seçə bilmir.
/// </summary>
public class StartPetBrainRunRequest
{
    [StringLength(60)]
    public string? TemplateKey { get; set; }

    /// <summary>
    /// Başladılan tövsiyənin qərar id-si.
    ///
    /// <para>Verilibsə, o, uşağın ÖZ və HƏLƏ AÇIQ qərarı olmalıdır: başqa
    /// uşağın və ya köhnəlmiş qərarın id-si rədd olunur. Verilməsə server öz
    /// cari tövsiyəsini başladır.</para>
    /// </summary>
    public Guid? DecisionId { get; set; }
}

/// <summary>
/// Tövsiyəyə cavab: "başqa fikir" və ya "sonra".
///
/// <para>Klient burada NƏ şablon, NƏ də bal dəyişikliyi göndərmir — yalnız
/// serverin verdiyi qərar id-sini və cavabın növünü. Beləliklə uşaq klienti
/// profili birbaşa idarə edə bilmir.</para>
/// </summary>
public class PetBrainFeedbackRequest
{
    public Guid DecisionId { get; set; }

    /// <summary>Yalnız <c>ShowAnother</c> və <c>NotNow</c> qəbul edilir.</summary>
    public PetBrainRecommendationFeedback Feedback { get; set; }
}

/// <summary>Mərhələ cavabı. Klient nə mərhələ nömrəsini, nə də doğruluğu təyin edə bilmir.</summary>
public class PetBrainChoiceRequest
{
    /// <summary>
    /// Serverin gözlədiyi mərhələnin indeksi. Uyğun gəlməsə <c>409</c> qayıdır —
    /// iki dəfə basmaq və ya mərhələ atlamaq mümkün olmur.
    /// </summary>
    [Range(0, 31)]
    public int StageIndex { get; set; }

    /// <summary>
    /// Budaqlanan macərada uşağın baxdığı düyünün açarı.
    ///
    /// <para>Mərhələ indeksi ilə eyni işi görür: klientin gördüyü ekran
    /// serverin gözlədiyi ekranla üst-üstə düşməlidir. Klient bununla başqa
    /// düyünə KEÇƏ bilmir — açar yalnız təsdiq üçündür.</para>
    /// </summary>
    [StringLength(40)]
    public string? NodeId { get; set; }

    /// <summary>Seçim mərhələsində seçilən variantın açarı.</summary>
    [StringLength(40)]
    public string? OptionKey { get; set; }

    /// <summary>
    /// Tapmaca cavabı — seçilmiş elementlərin id-ləri.
    ///
    /// <para>Sıra <see cref="PetBrainAnswerKind.OrderIds"/> sxemində ƏHƏMİYYƏTLİDİR.
    /// Klient burada YALNIZ id göndərir: doğruluq, bal və mükafat sahələri
    /// ümumiyyətlə yoxdur, çünki onları yalnız server bilir.</para>
    /// </summary>
    [MaxLength(12)]
    public List<string>? SelectedIds { get; set; }

    /// <summary>İpucu istəyi — seçim göndərilmir, mərhələ irəliləmir.</summary>
    public bool RequestHint { get; set; }
}

/// <summary>Nümayiş rejimində qərarın izahı. Uşaq üçün deyil, münsif/valideyn üçündür.</summary>
public class PetBrainDebugDto
{
    /// <summary>Cari təcrübə çətinliyi və onu doğuran son nəticə.</summary>
    public PetBrainDifficulty Difficulty { get; set; }
    public string DifficultySignal { get; set; } = string.Empty;

    /// <summary>Son mənalı hadisələr (tip + mənbə + vaxt).</summary>
    public List<PetBrainEventDto> RecentEvents { get; set; } = new();

    /// <summary>Namizədlərin balları — direktorun necə seçdiyi görünsün.</summary>
    public List<PetBrainCandidateDto> Candidates { get; set; } = new();

    /// <summary>Mətn deterministik şablondan, yoxsa modeldən gəldi.</summary>
    public string NarrativeSource { get; set; } = "template";

    /// <summary>Nümayişin şüarı.</summary>
    public string Headline { get; set; } = "SAME GAME · SAME PET · DIFFERENT CHILD · DIFFERENT EXPERIENCE";
}

public class PetBrainEventDto
{
    public PetBrainEventType Type { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

public class PetBrainCandidateDto
{
    public string TemplateKey { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;

    /// <summary>Uyğunluq balı (0–100).</summary>
    public int FitScore { get; set; }

    /// <summary>Yenilik balı (0–100) — son vaxt görülən mövzular cəzalanır.</summary>
    public int NoveltyScore { get; set; }

    /// <summary>Məhdud sürpriz payı (0–100).</summary>
    public int SurpriseScore { get; set; }

    /// <summary>Yekun bal — 70/20/10 çəkiləri ilə.</summary>
    public int TotalScore { get; set; }

    public bool Selected { get; set; }
}

/// <summary>
/// Valideyn müqayisə görünüşü: iki uşaq, eyni build, fərqli təklif.
/// Yalnız valideyn sessiyası və yalnız ÖZ uşaqları üçün.
/// </summary>
public class PetBrainComparisonDto
{
    public List<PetBrainChildSnapshotDto> Children { get; set; } = new();
}

public class PetBrainChildSnapshotDto
{
    public Guid ChildId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string PetName { get; set; } = string.Empty;
    public int Bond { get; set; }
    public string PersonalityLabel { get; set; } = string.Empty;
    public List<PetBrainTraitDto> TopInterests { get; set; } = new();
    public string RecommendedTemplateKey { get; set; } = string.Empty;
    public string RecommendedTitle { get; set; } = string.Empty;
    public List<string> Reasons { get; set; } = new();
}

/// <summary>
/// Ana ekranda görünən kompakt təklif. <see cref="PetBrainStateDto"/>-nun
/// yerinə keçmir — məqsəd ana ekranın açılışına ƏLAVƏ sorğu qatmamaqdır,
/// ona görə <c>HomeStateDto</c> aqreqatının içində gəlir.
/// </summary>
public class PetBrainHomeChipDto
{
    /// <summary>Davam etdiriləcək yarımçıq təcrübə varmı.</summary>
    public bool HasActiveRun { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    /// <summary>Bir sətirlik səbəb — çipdə göstərilir.</summary>
    public string Reason { get; set; } = string.Empty;
}
