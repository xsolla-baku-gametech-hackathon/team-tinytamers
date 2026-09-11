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

    /// <summary>
    /// Pillənin açdığı POZA açarı — pet-in duruşu bir neçə piksel dəyişir.
    /// Açar SERVERİN qapalı siyahısındandır; klient yenisini uydura bilmir.
    /// </summary>
    public string BondPose { get; set; } = string.Empty;

    /// <summary>Pillənin açdığı otaq bəzəyinin açarı; yoxdursa boş.</summary>
    public string BondRoomDecor { get; set; } = string.Empty;

    /// <summary>
    /// Bu pilləyə qədər açılmış HƏR ŞEY — pillə geri düşmədiyi üçün siyahı
    /// yalnız uzanır. Ekran «nə qazandım» görünüşünü bundan qurur.
    /// </summary>
    public List<PetBrainBondUnlockDto> BondUnlocks { get; set; } = new();

    public PetBrainPersonality Personality { get; set; }

    /// <summary>Xarakterin uşağın dilində adı.</summary>
    public string PersonalityLabel { get; set; } = string.Empty;

    public List<PetBrainTraitDto> Interests { get; set; } = new();
    public List<PetBrainTraitDto> PlayStyles { get; set; } = new();

    /// <summary>Son mənalı xatirələr — uşağa təbii cümlə kimi göstərilir.</summary>
    public List<PetBrainMemoryDto> Memories { get; set; } = new();

    /// <summary>Növbəti təcrübə üçün direktorun qərarı. Bloklu halda boş qala bilər.</summary>
    public PetBrainRecommendationDto? Recommendation { get; set; }

    /// <summary>
    /// ALTERNATİV təkliflər — uşaq bunlardan istənilənini başlada bilər.
    ///
    /// <para>Siyahı qəsdən fərqli ROLLAR daşıyır: davam, yaxınlıqda, yeni. Üç
    /// «ən uyğun» kart seçim deyil, təkrardır — və uşağı öz keçmişinə
    /// kilidləyir.</para>
    ///
    /// <para>Hər alternativin ÖZ <c>DecisionId</c>-si var: server yalnız əsas
    /// təklifi qəbul edib uşağı məcbur etmir.</para>
    /// </summary>
    public List<PetBrainRecommendationDto> Alternatives { get; set; } = new();

    /// <summary>
    /// Uşağın açıq mexanika üstünlükləri — mövzudan AYRI göstərilir.
    /// </summary>
    public List<PetBrainTraitDto> Mechanics { get; set; } = new();

    /// <summary>
    /// Fərdiləşdirmənin AÇIQ qatı: sessiya, temp, kömək, əlçatanlıq.
    ///
    /// <para>Ekran bunu həm tətbiq edir (hərəkət, şrift, kontrast), həm də
    /// uşağa «istəsən dəyişə bilərsən» kimi göstərir.</para>
    /// </summary>
    public PetBrainSettingsDto Settings { get; set; } = new();

    /// <summary>
    /// İlk tanışlıq hələ göstərilməyibsə <c>true</c> — ekran onu bir dəfə
    /// təklif edir və uşaq onu tamamilə keçə bilir.
    /// </summary>
    public bool OnboardingPending { get; set; }

    /// <summary>
    /// Profilin ÜMUMİ inamı (0–100) — «səni nə qədər tanıyıram».
    ///
    /// <para>Uşağa rəqəm kimi göstərilmir; ekran onu dürüst bir cümləyə
    /// çevirir.</para>
    /// </summary>
    public int ProfileConfidence { get; set; }

    /// <summary>Yarımçıq qalmış təcrübə — uşaq davam edə bilsin deyə.</summary>
    public PetBrainRunDto? ActiveRun { get; set; }

    /// <summary>Ekran vaxtı bloku — yeni təcrübə başlamaq olmur.</summary>
    public bool ScreenTimeBlocked { get; set; }

    public string ScreenTimeMessage { get; set; } = string.Empty;

    /// <summary>Nümayiş/izah paneli açıqdırmı (yalnız <c>PetBrain:DemoMode</c> ilə).</summary>
    public bool DemoMode { get; set; }

    /// <summary>Yalnız <see cref="DemoMode"/> açıq olanda dolur.</summary>
    public PetBrainDebugDto? Debug { get; set; }

    /// <summary>
    /// Pet-in öz niyyəti — <c>PetBrainV2:IntentEnabled</c> açıq olanda dolur.
    /// Bağlıdırsa <c>null</c> qalır və ekran onu ümumiyyətlə çəkmir.
    /// </summary>
    public PetBrainIntentDto? Intent { get; set; }
}

/// <summary>
/// Pet-in ÖZ niyyəti — uşaq baxmayanda nə etdiyi.
///
/// <para>Cümlə serverin təsdiqlədiyi açardan qurulur; pet burada sərbəst mətn
/// yaratmır. Heç bir variant uşağı günahlandırmır və ya tələsdirmir.</para>
/// </summary>
public class PetBrainIntentDto
{
    public PetBrainIntentType Type { get; set; }

    public string Icon { get; set; } = string.Empty;

    /// <summary>Niyyət BİTİBMİ — bitibsə cümlə nəticəni danışır.</summary>
    public bool IsComplete { get; set; }

    /// <summary>Uşağın dilində hazır cümlə.</summary>
    public string Line { get; set; } = string.Empty;

    /// <summary>Təsdiqlənmiş artefakt açarı; niyyət bitməyibsə boş.</summary>
    public string OutcomeKey { get; set; } = string.Empty;
}

/// <summary>
/// Bir bağ pilləsinin açdığı görünən şey — uşağa «nə qazandım» kimi göstərilir.
/// </summary>
public class PetBrainBondUnlockDto
{
    public PetBrainBondTier Tier { get; set; }

    /// <summary>Pillənin uşağın dilində adı.</summary>
    public string Label { get; set; } = string.Empty;

    public string Emote { get; set; } = string.Empty;

    /// <summary>Nə açıldığını bir cümlə ilə deyir — bal rəqəmi ilə yox.</summary>
    public string Line { get; set; } = string.Empty;

    /// <summary>Uşaq bu pilləyə ÇATIBMI. Çatmayanlar da göstərilir — vəd görünür.</summary>
    public bool Reached { get; set; }
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

public record PetBrainMemoryDto
{
    /// <summary>
    /// Sətrin id-si — YALNIZ valideyn görünüşündə doldurulur.
    ///
    /// <para>Uşaq ekranında boş qalır: uşağa xatirəni silmək düyməsi
    /// verilmir, bu, valideyn qərarıdır.</para>
    /// </summary>
    public Guid Id { get; set; }

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


    /// <summary>
    /// Kartın ROLU — əsas, davam, yaxınlıqda, yeni.
    ///
    /// <para>Ekran bunu kiçik bir nişanla göstərir: uşaq üç eyni kart deyil,
    /// üç FƏRQLİ təklif gördüyünü bilməlidir.</para>
    /// </summary>
    public PetBrainRecommendationSlot Slot { get; set; }

    /// <summary>Rolun uşağın dilində adı.</summary>
    public string SlotLabel { get; set; } = string.Empty;

    /// <summary>
    /// İzahın SƏBƏB KODLARI. <see cref="Reasons"/> bunlardan qurulur —
    /// klient öz mətnini yaza bilsin və jurnal dil dəyişəndə köhnəlməsin.
    /// </summary>
    public List<PetBrainWhyReason> ReasonCodes { get; set; } = new();

    /// <summary>
    /// Qərara təsir edən ölçülərin parçalanması — valideyn/münsif üçün.
    ///
    /// <para>Uşaq ekranında göstərilmir: uşağa cümlə lazımdır, rəqəm yox.</para>
    /// </summary>
    public List<PetBrainFactorDto> PersonalizationFactors { get; set; } = new();

    /// <summary>Çətinliyin uşağın dilində adı — «Rahat gediş», «Tam sənə görə».</summary>
    public string ChallengeLabel { get; set; } = string.Empty;

    /// <summary>Bu macərada ipucu mövcuddurmu.</summary>
    public bool SupportAvailable { get; set; }

    /// <summary>Köməyin uşağın seçdiyi FORMASI — «Addım-addım kömək».</summary>
    public string SupportLabel { get; set; } = string.Empty;

    /// <summary>Mükafatın forması.</summary>
    public PetBrainRewardPreference RewardFlavor { get; set; }

    /// <summary>Mükafatın uşağın dilində adı.</summary>
    public string RewardLabel { get; set; } = string.Empty;

    /// <summary>Bu kart KƏŞF payından gəldimi — nümayiş və ölçmə üçün.</summary>
    public bool WasExploration { get; set; }

    /// <summary>Qərarı verən siyasətin versiyası.</summary>
    public int PolicyVersion { get; set; }

    /// <summary>
    /// Uşaq bu kart üçün açıq rəy verə bilirmi.
    ///
    /// <para>Fərdiləşdirmə söndürüləndə <c>false</c> olur: profil yazılmayan
    /// halda «bəyənirəm» düyməsi uşağa yalan vəd verərdi.</para>
    /// </summary>
    public bool CanGiveFeedback { get; set; } = true;
}

/// <summary>
/// Qərara təsir edən BİR ölçü. Uşağa deyil, valideyn və münsifə göstərilir.
/// </summary>
public class PetBrainFactorDto
{
    /// <summary>Sabit açar: <c>topic</c>, <c>mechanic</c>, <c>mastery</c>…</summary>
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>0–100 (açıq seçim düzəlişi mənfi ola bilər).</summary>
    public int Value { get; set; }

    /// <summary>Bu ölçünün siyasətdəki çəkisi (0–1).</summary>
    public double Weight { get; set; }
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

    /// <summary>
    /// Chapter-li macəranın vəziyyəti — məqsədlər, inventar, jurnal, irəliləmə.
    ///
    /// <para>Chapter-siz (qısa) macərada <c>null</c> qalır və ekran köhnə,
    /// sadə görünüşünü saxlayır: HUD yalnız onu daşıya bilən macərada çıxır.</para>
    /// </summary>
    public PetBrainAdventureStateDto? Adventure { get; set; }

    /// <summary>Chapter indicə bitibsə onun yekun ekranı; əks halda <c>null</c>.</summary>
    public PetBrainChapterCompleteDto? ChapterComplete { get; set; }
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

    /// <summary>
    /// İpucu TƏKLİFİNİN üslubu — xarakterin səsi ilə.
    ///
    /// <para>İpucunun ÖZÜ deyil: bu, uşağı köməyə dəvət edən cümlədir və
    /// kömək istəməyi zəiflik kimi göstərmir.</para>
    /// </summary>
    public string HintOffer { get; set; } = string.Empty;

    /// <summary>
    /// Seçimdən sonra pet-in reaksiyası — nəticə ekranında hekayənin öz
    /// replikasının yanında gəlir. Xarakterə görə dəyişir.
    /// </summary>
    public string PetReaction { get; set; } = string.Empty;

    /// <summary>
    /// İpucunun hazırkı PİLLƏSİ — hər istəkdə bir pillə güclənir.
    ///
    /// <para>Ekran bunu uşağa «neçənci kömək» kimi göstərmir: məqsəd sayğac
    /// deyil, növbəti köməyin nə olacağını bilməkdir.</para>
    /// </summary>
    public PetBrainHintLevel HintLevel { get; set; }

    /// <summary>
    /// Cavabın ARTIQ açılmış addımları — ekran onları işarələyir.
    ///
    /// <para>Tam həll heç vaxt burada olmur; ən çox yarısı.</para>
    /// </summary>
    public List<string> HintRevealIds { get; set; } = new();

    /// <summary>Pet «gəl birlikdə bitirək» təklif edir — ipucunun son pilləsi.</summary>
    public bool AssistAvailable { get; set; }

    /// <summary>
    /// Jurnaldan bu tapmacaya aid QEYD — uşaq əvvəllər tapdığı ipucunu
    /// tapmacanın yanında görür. Uyğun ipucu yoxdursa boş.
    /// </summary>
    public string JournalNote { get; set; } = string.Empty;

    public string JournalNoteTitle { get; set; } = string.Empty;
}

public class PetBrainOptionDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    /// <summary>Seçimin qısa təsviri — uşaq nəticəni əvvəlcədən görsün.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>
    /// Pet bu variantı TƏKLİF edir — uşağın öz üslubuna ən yaxın olanı.
    ///
    /// <para>Sıra dəyişmir və digər variantlar gizlənmir: təklif bir işarədir,
    /// qərar isə uşağındır. «Pet-in təklifini qəbul etmək və ya başqasını
    /// seçmək» özü bir seçimdir.</para>
    /// </summary>
    public bool Suggested { get; set; }
}

public class PetBrainSummaryDto
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Pet-in yekun replikası.</summary>
    public string PetLine { get; set; } = string.Empty;

    /// <summary>
    /// Bağ pilləsinin açdığı macəra reaksiyası — pillə qalxdıqca isinir.
    /// «Bacardıq!» ilə «Komandamız yenilməzdir!» arasındakı fərq uşağın
    /// QAZANDIĞI şeydir.
    /// </summary>
    public string BondReaction { get; set; } = string.Empty;

    public int XpEarned { get; set; }
    public int BondEarned { get; set; }
    public int BondTotal { get; set; }

    /// <summary>
    /// Fəsilli macəranın epiloqu — sonluq, ünvan, dünyadakı izlər, növbəti
    /// qarmaq. Qısa macərada <c>null</c>.
    /// </summary>
    public PetBrainEpilogueDto? Epilogue { get; set; }

    /// <summary>Bu tamamlamada açılan kosmetik əşya (ilk dəfə); yoxdursa boş.</summary>
    public string UnlockedAccessoryCode { get; set; } = string.Empty;
    public string UnlockedAccessoryName { get; set; } = string.Empty;
    public string UnlockedAccessoryIcon { get; set; } = string.Empty;

    /// <summary>Bu təcrübədən yaranan yeni xatirələr.</summary>
    public List<PetBrainMemoryDto> NewMemories { get; set; } = new();

    /// <summary>
    /// Mükafatın FORMASI — uşağın seçdiyi növ.
    ///
    /// <para>Mükafat iqtisadiyyatı bununla dəyişmir (xp, bağ və kosmetik olduğu
    /// kimi qalır): dəyişən onun necə təqdim olunmasıdır. Kolleksiya sevən uşaq
    /// «tapıntı», hekayə sevən «albom səhifəsi» görür.</para>
    /// </summary>
    public PetBrainRewardPreference RewardFlavor { get; set; }

    /// <summary>Mükafatın uşağın dilində adı.</summary>
    public string RewardLabel { get; set; } = string.Empty;

    /// <summary>
    /// Uşaq bu macəra haqqında açıq rəy verə bilirmi.
    ///
    /// <para>Yekun ekranı rəy üçün ən dürüst andır: uşaq macəranı ARTIQ
    /// oynayıb, ona görə «bəyəndim» sözü təxmin deyil, təcrübədir.</para>
    /// </summary>
    public bool CanGiveFeedback { get; set; } = true;

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
/// «Macəra videoları» rəfindəki bir macəra — uşaq videoya sonradan da baxır.
/// </summary>
public class PetBrainRecapEntryDto
{
    public Guid RunId { get; set; }

    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>Macəranın adı — uşağın dilində.</summary>
    public string Title { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    /// <summary>Səhnə açarı — storyboard fonunun rəngi bundan gəlir.</summary>
    public string SceneKey { get; set; } = string.Empty;

    public DateTime? CompletedAt { get; set; }

    /// <summary>Storyboard həmişə doludur; video yalnız hazır olanda ünvan alır.</summary>
    public PetBrainRecapDto Recap { get; set; } = new();
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
    ///
    /// <para>Yuxarı hədd chapter-li macəranın ən uzun yolundan (bax
    /// <c>ExperienceGraphValidator.MaxPathLength</c>) BÖYÜK olmalıdır. Əvvəlki
    /// 31 həddi dörd mərhələli xətti macəraya görə seçilmişdi və altı fəsilli
    /// macərada beşinci fəsildə sorğunu rədd edirdi — yəni uşaq macərənin
    /// ortasında bağlı qapı görürdü. Hədd yenə var: o, sonsuz indeks deyil,
    /// REAL yolun uzunluğunu qoruyur.</para>
    /// </summary>
    [Range(0, 127)]
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

    /// <summary>
    /// Klientin gördüyü vəziyyət nömrəsi.
    ///
    /// <para>Serverinkindən KÖHNƏdirsə <c>409</c> qayıdır: uşaq iki cihazda
    /// eyni macərəni açıbsa, biri digərinin addımını səssizcə üstələməməlidir.
    /// Göndərilməyəndə (<c>null</c>) yoxlama aparılmır — köhnə klientlər
    /// işləməyə davam edir.</para>
    /// </summary>
    public int? ClientRevision { get; set; }

    /// <summary>
    /// Bu addımın TƏKRARSIZLIQ açarı.
    ///
    /// <para>Zəif şəbəkədə klient eyni sorğunu iki dəfə göndərir. Açar
    /// tətbiq olunmuş sayılırsa, server heç nə etmir və cari vəziyyəti
    /// qaytarır — yəni bir seçim üçün iki dəfə əşya düşmür.</para>
    /// </summary>
    [StringLength(64)]
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Uşaq pet-in «birlikdə bitirək» təklifini qəbul etdi.
    ///
    /// <para>Yalnız ipucunun son pilləsində etibarlıdır — server pilləni öz
    /// sayğacı ilə yoxlayır. Tapmaca hekayə üçün tamamlanır, mənimsəməyə isə
    /// köməksiz həll kimi YAZILMIR.</para>
    /// </summary>
    public bool AcceptAssist { get; set; }
}

/// <summary>Macərəni dayandırmaq və ya davam etdirmək istəyi.</summary>
public class PetBrainPauseRequest
{
    /// <summary>Bu sessiyada faktiki oynanılan saniyə — ölçmə üçün.</summary>
    [Range(0, 7200)]
    public int PlayedSeconds { get; set; }
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

    /// <summary>
    /// Sərt şərtdən keçməyən namizədlər və səbəbləri.
    ///
    /// <para>«Niyə bu macəra göstərilmədi?» sualı yalnız bununla cavablanır —
    /// bal cədvəli onu göstərə bilmir, çünki süzülən namizəd sıralamaya heç
    /// girmir.</para>
    /// </summary>
    public List<PetBrainFilteredCandidateDto> FilteredCandidates { get; set; } = new();

    /// <summary>Nümayişin şüarı.</summary>
    public string Headline { get; set; } = "SAME GAME · SAME PET · DIFFERENT CHILD · DIFFERENT EXPERIENCE";
}

/// <summary>Sərt şərtdən keçməyən bir namizəd.</summary>
public class PetBrainFilteredCandidateDto
{
    public string TemplateKey { get; set; } = string.Empty;
    public PetBrainFilterReason Reason { get; set; }
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

    /// <summary>
    /// Chapter-li yarımçıq macərada «Fəsil 3/6»; qısa macərada boş.
    ///
    /// <para>Ana ekran uzun macəranı «yarımçıq qalıb» deyə yox, HARADA
    /// qaldığını deyərək xatırladır — uşaq bir həftə sonra qayıtsa da
    /// macəranın ortasında olduğunu bilir.</para>
    /// </summary>
    public string ChapterLabel { get; set; } = string.Empty;

    /// <summary>0–100, tamamlanmış fəsillərdən; qısa macərada 0.</summary>
    public int ProgressPercent { get; set; }

    /// <summary>Macəra uşaq tərəfindən DAYANDIRILIB (itirilməyib).</summary>
    public bool IsPaused { get; set; }
}
