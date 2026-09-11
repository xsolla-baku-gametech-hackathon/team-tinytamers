using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>
/// Tapmaca mexanikalarının QAPALI siyahısı.
///
/// <para>Bu, sadəcə enum deyil — təhlükəsizlik sərhədidir. Server yalnız
/// buradakı dəyərləri verə bilir, klient isə yalnız bunları tanıyır. Naməlum
/// açar həm generatorda, həm də UI-da <b>bağlı</b> sınır (fail closed): render
/// olunmur, qiymətləndirilmir.</para>
///
/// <para>Dördü QƏSDƏN fərqli qarşılıqlı təsirdir, eyni sualın dörd donu deyil:
/// biri marşrut planlaşdırır, biri sıralayır, biri şərtə görə süzür, biri isə
/// sərbəst qurur.</para>
///
/// <para><b>3 nömrə boşdur.</b> Orada «iki elementin cəmi hədəfə bərabər»
/// mexanikası vardı və QƏSDƏN silinib: o, macərəyə yapışdırılmış arifmetik
/// kart idi — Robonu xilas etmirdi, uşağın seçimini mənalandırmırdı. Nömrə
/// təkrar istifadə edilmir ki, köhnə sətir yeni mexanika kimi oxunmasın.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainPuzzleMechanic
{
    /// <summary>Addımları düzgün SIRAYA düz (cavab sıralıdır).</summary>
    SequenceOrder = 1,

    /// <summary>Şərtə uyğun YEGANƏ marşrutu seç.</summary>
    RouteLogic = 2,

    /// <summary>
    /// <b>Referans mexanika.</b> Qraf üzərində enerjili marşrut planlaşdırma:
    /// dron başlanğıcdan hədəfə gedir, yolda enerji doldurur və MƏCBURİ
    /// düyünə (antena) uğramalıdır.
    ///
    /// <para>Fərqi budur ki, düşünmə hərəkəti hekayənin problemini BİRBAŞA
    /// həll edir — ayrıca viktorina sualı deyil. Tələ yolu Roboya çatır, amma
    /// rabitəni bərpa etmədiyi üçün işə yaramır.</para>
    /// </summary>
    OrderedRoute = 4,

    /// <summary>
    /// Əjdahanın qanad naxışını işıq parçalarından bərpa etmək.
    ///
    /// <para>Təzyiqsizdir: bir neçə palitra eyni dərəcədə doğrudur. Yalnız
    /// yüksək pillədə simmetriya şərti əlavə olunur — cəza dili yenə yoxdur.</para>
    /// </summary>
    LightFragments = 5,

    /// <summary>
    /// Siqnalın NAXIŞINI davam etdirmək: işıq-səs zolağı təkrarlanan qayda ilə
    /// gedir, uşaq növbəti addımları düzür.
    ///
    /// <para>Sıralamadan (<see cref="SequenceOrder"/>) fərqi budur ki, orada
    /// qayda «kiçikdən böyüyə» kimi hazır verilir, burada isə uşaq qaydanı ÖZÜ
    /// tapmalıdır — göstərilən hissədən çıxarır və davamını qurur.</para>
    /// </summary>
    SignalPattern = 6,

    /// <summary>
    /// Səhnədə NƏYİN DƏYİŞDİYİNİ tapmaq: jurnal əvvəlki vəziyyəti saxlayır,
    /// uşaq indiki səhnə ilə tutuşdurur.
    ///
    /// <para>Bu mexanika ipucu jurnalı olmadan işləmir — məhz buna görə
    /// seçilib: jurnal bəzək deyil, tapmacanın GİRİŞİdir.</para>
    /// </summary>
    ObservationRecall = 7,

    /// <summary>
    /// Hər elementi öz cütü ilə UYĞUNLAŞDIRMAQ: sol sütun sabit, uşaq sağ
    /// sütunu onların qarşısına düzür.
    ///
    /// <para>Cavab SIRALIDIR, çünki sıra «kim kiminlədir» məlumatını daşıyır.</para>
    /// </summary>
    MatchingPairs = 8,

    /// <summary>
    /// Macəranın ŞƏKLİNİ yığmaq: hekayə rəsmi parçalara bölünür, uşaq onları
    /// çərçivədəki yerlərinə qaytarır.
    ///
    /// <para>Hər macərada var və adi tapmacalarla yarışmır — mərhələ onu AÇIQ
    /// istəyir. Rəsm yenə yalnız görüntüdür: parçanın yeri serverin rəqəmindən
    /// gəlir, rəsm gəlməsə macəranın deterministik şəkli kəsilir.</para>
    /// </summary>
    PictureAssembly = 9
}

/// <summary>
/// İpucunun GÜCÜ — hər istəkdə bir pillə artır.
///
/// <para>Pillələr cavaba addım-addım yaxınlaşır: əvvəl ruhlandırma, sonra
/// istiqamət, sonra nümunə, sonra cavabın bir hissəsi, sonda isə pet-in birgə
/// tamamlaması. Uşaq heç vaxt macərədən çıxarılmır və heç vaxt «səhv etdin,
/// yenidən başla» eşitmir — bu, yumşaq uğursuzluğun özüdür.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainHintLevel
{
    /// <summary>İpucu istənməyib.</summary>
    None = 0,

    /// <summary>«Tələsmə, bir daha bax» — cavab haqqında heç nə demir.</summary>
    GentlePrompt = 1,

    /// <summary>Qaydanın istiqaməti — generatorun öz ipucu.</summary>
    DirectionalHint = 2,

    /// <summary>Cavabın BİRİNCİ addımı göstərilir.</summary>
    WorkedExample = 3,

    /// <summary>Cavabın yarısı göstərilir — qalanını uşaq tamamlayır.</summary>
    StepByStepHelp = 4,

    /// <summary>Pet təklif edir: «gəl birlikdə edək». Mənimsəməyə ayrıca yazılır.</summary>
    AssistedCompletion = 5
}

/// <summary>Cavabın forması. Klient bunu OXUYUR, təyin etmir.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainAnswerKind
{
    /// <summary>Sıra əhəmiyyətsizdir — dəst kimi müqayisə olunur.</summary>
    SelectIds = 0,

    /// <summary>Sıra ƏHƏMİYYƏTLİDİR — ardıcıllıq kimi müqayisə olunur.</summary>
    OrderIds = 1,

    /// <summary>
    /// Sıralı DÜYÜN yolu: qonşuluq, enerji və məcburi uğrama qaydaları ilə
    /// birlikdə yoxlanılır — sadəcə siyahı müqayisəsi deyil.
    /// </summary>
    OrderedNodeIds = 2
}

/// <summary>
/// Marşrut düyününün rolu. Rol GÖRÜNƏNDİR: qayda gizli sahədə saxlansaydı,
/// klient onu oxuyub cavabı çıxara bilərdi.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainNodeKind
{
    /// <summary>Başlanğıc — dron buradan çıxır.</summary>
    Start = 0,

    /// <summary>Adi keçid.</summary>
    Path = 1,

    /// <summary>Enerji doldurur (<c>EnergyDelta</c>).</summary>
    Recharge = 2,

    /// <summary>Hədəfdən ƏVVƏL mütləq uğranmalıdır.</summary>
    Required = 3,

    /// <summary>Tələ: hədəfə çatır, amma şərti ödəmir.</summary>
    Decoy = 4,

    /// <summary>Hədəf.</summary>
    Goal = 5,

    /// <summary>Keçilməz — daş, uçurum.</summary>
    Blocked = 6
}

/// <summary>Hekayə illüstrasiyasının vəziyyəti.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainIllustrationStatus
{
    /// <summary>Model hələ cavab verməyib — ekranda deterministik ehtiyat rəsm var.</summary>
    Pending = 0,

    /// <summary>AI rəsmi hazırdır və keşlənib.</summary>
    Ready = 1,

    /// <summary>
    /// AI bağlıdır, uğursuz oldu və ya çıxışı rədd edildi — hekayəyə uyğun
    /// deterministik rəsm QALIR. Uşaq heç bir xəta görmür.
    /// </summary>
    Fallback = 2
}

/// <summary>Verilmiş tapmacanın vəziyyəti.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainPuzzleStatus
{
    Issued = 0,
    Solved = 1,

    /// <summary>Run yarımçıq qaldı — tapmaca da bağlandı.</summary>
    Abandoned = 2
}

/// <summary>
/// Recap videosunun vəziyyəti.
///
/// <para>Uşaq üçün yalnız ikisi mənalıdır: hazır video, yoxsa deterministik
/// storyboard. Qalanları böyüklərin/nümayiş qatı üçündür.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainRecapStatus
{
    /// <summary>Növbədədir — ekranda deterministik storyboard oynayır.</summary>
    Pending = 0,

    /// <summary>Provayderdə iş gedir; tapşırıq id-si saxlanılıb.</summary>
    Generating = 1,

    /// <summary>Video hazırdır və keşlənib.</summary>
    Ready = 2,

    /// <summary>
    /// AI bağlıdır, kvota bitib, gecikib və ya çıxış rədd edilib — <b>tam 10
    /// saniyəlik deterministik recap</b> qalır. Mükafat toxunulmazdır.
    /// </summary>
    Fallback = 3,

    /// <summary>Çıxış müqaviləyə uymadı (müddət, nisbət, kodek, ölçü).</summary>
    Rejected = 4
}
