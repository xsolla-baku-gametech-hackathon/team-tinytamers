using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>
/// Uşağın üstün tutduğu sessiya uzunluğu.
///
/// <para>Bu ölçü ekran vaxtını ARTIRMAQ üçün deyil: o, macəranın neçə addım
/// sürəcəyini müəyyən edir və «uzun» seçimi belə valideynin gündəlik limitini
/// keçə bilmir.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainSessionLength
{
    /// <summary>Ən qısa yol — tamamlanan bir macəra üçün ən az addım.</summary>
    Short = 0,

    Medium = 1,

    /// <summary>Daha çox səhnə; ekran vaxtı zolağı yenə üstündür.</summary>
    Long = 2
}

/// <summary>Keçidlərin tempi — səhnə dəyişimi nə qədər tez baş verir.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainPace
{
    /// <summary>Sakit: daha çox nəfəs, daha az eyni anda görünən element.</summary>
    Calm = 0,

    Balanced = 1,

    /// <summary>Sürətli: qısa giriş mətni, dərhal fəaliyyət.</summary>
    Fast = 2
}

/// <summary>
/// İpucunun FORMASI — nə deyildiyi deyil, NECƏ deyildiyi.
///
/// <para>Bu, uşağın bacarığı, əlilliyi və ya diaqnozu haqqında iddia DEYİL:
/// yalnız «hansı izah bu uşaq üçün işlədi» müşahidəsidir.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainHintStyle
{
    /// <summary>Vizual: ekranda vacib obyekt işıqlandırılır.</summary>
    Visual = 0,

    /// <summary>Addım-addım: növbəti BİR addım deyilir.</summary>
    StepByStep = 1,

    /// <summary>Nümunə: oxşar, artıq həll olunmuş hal göstərilir.</summary>
    Example = 2,

    /// <summary>Qayda: tapmacanın şərti öz sözləri ilə təkrarlanır.</summary>
    Rule = 3
}

/// <summary>İpucunun nə vaxt təklif olunduğu.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainHintTiming
{
    /// <summary>Yalnız uşaq istəyəndə — standart.</summary>
    OnRequest = 0,

    /// <summary>Bir neçə cəhddən sonra pet özü təklif edir.</summary>
    Delayed = 1,

    /// <summary>Tapmaca açılan kimi kömək görünür (açıq seçimlə).</summary>
    Immediate = 2
}

/// <summary>
/// Uşağın yeniliyə açıqlığı — tanış və yeni məzmunun nisbətini idarə edir.
///
/// <para>Ən aşağı pillədə də yenilik SIFIR olmur: filter bubble qəsdən mümkün
/// deyil.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainNoveltyTolerance
{
    Low = 0,
    Balanced = 1,
    High = 2
}

/// <summary>
/// Uşağın üstün tutduğu mükafat növü.
///
/// <para>Siyahıda nə loot box, nə təsadüfi qutu, nə də pulla alınan element
/// var: hər mükafat tamamlamaya bağlı, görünən və birdəfəlikdir.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainRewardPreference
{
    /// <summary>Pet-in geyimi və aksesuarı.</summary>
    PetCosmetic = 0,

    /// <summary>Otaq bəzəyi.</summary>
    RoomDecor = 1,

    /// <summary>Kolleksiya elementi — kəşf lövhəsinə düşür.</summary>
    Collection = 2,

    /// <summary>Xatirə albomuna düşən hekayə səhifəsi.</summary>
    StoryPage = 3,

    /// <summary>Yeni yaradıcı alət (palitra, naxış).</summary>
    CreativeTool = 4
}

/// <summary>
/// Bir ayarın HARADAN gəldiyi. Sıra həm də ÜSTÜNLÜK sırasıdır: valideyn seçimi
/// uşağın seçimini, uşağın seçimi isə sistemin təxminini üstələyir.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainSettingSource
{
    /// <summary>Heç kim toxunmayıb — məhsulun təhlükəsiz standartı.</summary>
    Default = 0,

    /// <summary>Sistemin davranışdan çıxardığı təxmin — ƏN ZƏİF mənbə.</summary>
    Inferred = 1,

    /// <summary>İlk tanışlıqda uşağın seçimi — prior, daimi həqiqət deyil.</summary>
    Onboarding = 2,

    /// <summary>Uşağın oyun içindəki açıq seçimi.</summary>
    Child = 3,

    /// <summary>Valideynin açıq seçimi — heç bir təxmin bunu dəyişə bilmir.</summary>
    Parent = 4
}

/// <summary>
/// Mətnin oxu mürəkkəbliyi.
///
/// <para>Yalnız açıq ayarla dəyişir. Bir zəif cavaba görə avtomatik aşağı
/// salınmır — bu, uşaq haqqında iddia olardı.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainReadingLevel
{
    /// <summary>Qısa cümlə, çox işarə.</summary>
    Simple = 0,

    Standard = 1,

    /// <summary>Daha geniş izah və daha zəngin lüğət.</summary>
    Rich = 2
}

/// <summary>
/// Açıq məzmun seçimi. Dolayı davranış siqnallarından GÜCLÜDÜR: uşaq və ya
/// valideyn birbaşa deyəndə sistem təxmin etmir.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainContentPreferenceKind
{
    /// <summary>«Bunu bəyənirəm» — güclü müsbət.</summary>
    Liked = 0,

    /// <summary>«Bunu daha az göstər» — güclü mənfi, amma BLOK deyil.</summary>
    ShowLess = 1,

    /// <summary>Valideyn blokladı — namizəd hovuzundan tam çıxır.</summary>
    Blocked = 2
}

/// <summary>Açıq seçimin NƏYƏ aid olduğu.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainContentScope
{
    /// <summary>Mövzu: kosmos, heyvanlar…</summary>
    Theme = 0,

    /// <summary>Konkret macəra şablonu.</summary>
    Template = 1,

    /// <summary>Oyun mexanikası: marşrut, sıralama…</summary>
    Mechanic = 2
}

/// <summary>
/// Tövsiyə siyahısındakı bir kartın ROLU.
///
/// <para>Siyahı qəsdən fərqli rollar daşıyır: ən uyğun, davam, yaxın kəşf və
/// təhlükəsiz sürpriz. Üç «ən uyğun» kart filter bubble-dır.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainRecommendationSlot
{
    /// <summary>Ən yüksək bal — əsas təklif.</summary>
    Primary = 0,

    /// <summary>Yarımçıq və ya davamı olan macəra.</summary>
    Continuity = 1,

    /// <summary>Yaxın qonşu mövzu/mexanika — tanışdan bir addım kənar.</summary>
    NearbyDiscovery = 2,

    /// <summary>Təhlükəsiz sürpriz — yaşa uyğun, amma tamamilə yeni.</summary>
    SafeExploration = 3
}

/// <summary>
/// «Niyə bunu göstərirəm?» üçün SƏBƏB KODU.
///
/// <para>Kod saxlanılır, cümlə isə uşağın dilində ondan qurulur: beləliklə izah
/// tərcümə oluna bilir və audit jurnalında sərbəst mətn qalmır.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainWhyReason
{
    /// <summary>Bu mövzunu tez-tez seçir.</summary>
    StrongTopic = 0,

    /// <summary>Bu mexanikanı sevir.</summary>
    LovedMechanic = 1,

    /// <summary>Sevdiyi mexanika YENİ mövzuda.</summary>
    LovedMechanicNewTopic = 2,

    /// <summary>Yarımçıq macəranın davamı.</summary>
    ContinueStory = 3,

    /// <summary>Seçdiyi sessiya uzunluğuna uyğun.</summary>
    MatchesSessionLength = 4,

    /// <summary>Bacarığına uyğun çətinlik.</summary>
    MatchesChallenge = 5,

    /// <summary>Bu dəfə yeni bir şey.</summary>
    SomethingNew = 6,

    /// <summary>Sürpriz istədi.</summary>
    AskedForSurprise = 7,

    /// <summary>Sevdiyi mükafat növü var.</summary>
    RewardMatch = 8,

    /// <summary>Bu yaxınlarda oxşar mövzu oldu — bu dəfə başqa yer.</summary>
    RecentlyPlayedSimilar = 9,

    /// <summary>Kömək hazırdır — istədiyi formada.</summary>
    SupportReady = 10,

    /// <summary>Hələ tanışlıq mərhələsidir; sistem az şey bilir.</summary>
    StillLearning = 11
}

/// <summary>
/// Bir namizədin hansı SƏRT şərtdən keçmədiyi — qərar jurnalı üçün.
///
/// <para>Sərbəst mətn deyil: kod saxlanılır ki, «niyə bu macəra
/// göstərilmədi?» sualı sonradan cavablana bilsin.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainFilterReason
{
    None = 0,

    /// <summary>Yaş həddi — təhlükəsizlik sərhədi.</summary>
    AgeGate = 1,

    /// <summary>Valideyn bu mövzunu və ya macəranı bloklayıb.</summary>
    ParentBlocked = 2,

    /// <summary>Uşaq «daha az göstər» dedi və soyuma müddəti bitməyib.</summary>
    ShowLessCooldown = 3,

    /// <summary>Bu sessiyada kənara qoyulub.</summary>
    DeclinedThisSession = 4,

    // 5 QƏSDƏN boşdur. Orada «bu yaxınlarda oynanıb» səbəbi vardı və
    // silinib: sərt süzgəc uşağa sevdiyi macəraya qayıtmağı qadağan edirdi,
    // halbuki təkrar oynama ən güclü müsbət siqnaldır. Təkrar indi yalnız
    // yenilik balında cəza alır. Nömrə təkrar işlədilmir ki, köhnə jurnal
    // sətri yeni səbəb kimi oxunmasın.

    /// <summary>Pet hələ yumurtadadır.</summary>
    PetNotHatched = 6,

    /// <summary>Ekran vaxtı azdır, macəra isə uzundur.</summary>
    TooLongForRemainingTime = 7
}
