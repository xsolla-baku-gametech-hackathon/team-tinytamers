namespace PetPal.Api.Learning.Arena;

/// <summary>
/// Bilik Arenasının məhsul qərarları — bax docs/LEARN_ARENA.md.
/// Burada yalnız hədlər və mükafatlar var; uyğunlaşdırmanın riyaziyyatı
/// <see cref="ArenaMatchmaker"/> və <see cref="ArenaRatingEngine"/>-dədir.
/// </summary>
public class ArenaOptions
{
    public const string SectionName = "Arena";

    /// <summary>
    /// Gündəlik duel həddi. <b>0 və ya mənfi — hədd YOXDUR</b>, uşaq istədiyi
    /// qədər yarışa bilər (2026-08-23-də belə seçildi).
    ///
    /// <para>Ekran vaxtı mühafizəsi bundan ASILI DEYİL: yuxu rejimi və gündəlik
    /// vaxt həddi yenə də duel açmağı bloklayır — bax <see cref="ScreenTimeGuard"/>.
    /// Yəni "hədd yoxdur" gecə yarısı sonsuz oynamaq demək deyil.</para>
    /// </summary>
    public int DuelsPerDay { get; set; }

    /// <summary>Bir duelin sual sayı. Qısa olmalıdır: uşaq nəticəni tez görməlidir.</summary>
    public int QuestionCount { get; set; } = 5;

    /// <summary>Uyğunlaşdırmanın başlanğıc reytinq zolağı (±).</summary>
    public int RatingBand { get; set; } = 150;

    /// <summary>
    /// Zolaq bu qədər SANİYƏDƏN sonra genişlənir, iki misli keçəndə isə tamam açılır.
    /// Sinxron yarışda uşaq canlı gözləyir — dəqiqələr çox uzundur.
    /// </summary>
    public int WidenAfterSeconds { get; set; } = 20;

    /// <summary>Genişlənmiş zolaq (±). Bundan sonrakı mərhələdə zolaq yoxdur.</summary>
    public int WidenedRatingBand { get; set; } = 300;

    /// <summary>
    /// Rəqib gözləyən duel bu qədər DƏQİQƏDƏN sonra ləğv olunur.
    ///
    /// <para>Qısa olmalıdır: duel sinxrondur, yəni açıq duel "hazırda ekran
    /// qarşısında gözləyən uşaq" deməkdir. App çökübsə və ya uşaq telefonu
    /// cibinə qoyubsa, hovuzda qalan belə duelə qoşulan ikinci uşaq heç vaxt
    /// oynamayacaq rəqiblə üz-üzə qalardı.</para>
    /// </summary>
    public int WaitingExpireMinutes { get; set; } = 3;

    /// <summary>
    /// Rəqib tapılandan sonrakı geri sayım (saniyə). Hər iki uşaq bu saniyələri
    /// birlikdə görür və dəst ikisi üçün eyni anda başlayır.
    /// </summary>
    public int CountdownSeconds { get; set; } = 3;

    /// <summary>
    /// Dosta göndərilən birbaşa çağırış bu qədər SANİYƏDƏN sonra sönür.
    ///
    /// <para>Açıq dueldən qısadır: çağırış konkret adama ünvanlanıb və çağıran
    /// ekran qarşısında gözləyir. Dost cavab vermirsə uşağı bir neçə dəqiqə
    /// gözlətməkdənsə çağırışı söndürüb adi rəqib axtarmaq yaxşıdır.</para>
    /// </summary>
    public int ChallengeExpireSeconds { get; set; } = 60;

    public int WinStars { get; set; } = 20;

    /// <summary>
    /// Sürət bonusunun yuxarı həddi (ulduz). Yalnız QALİBƏ verilir: rəqibi nə
    /// qədər tez qabaqlayıbsa, bonusun bir o qədər çox hissəsi düşür — bax
    /// <see cref="ArenaRatingEngine.SpeedBonusFor"/>.
    /// </summary>
    public int SpeedBonusStars { get; set; } = 10;

    /// <summary>Uduzmaq da ulduz gətirir — 6–10 yaş app-ində mənfi mükafat yoxdur.</summary>
    public int LossStars { get; set; } = 8;

    public int DrawStars { get; set; } = 12;
}
