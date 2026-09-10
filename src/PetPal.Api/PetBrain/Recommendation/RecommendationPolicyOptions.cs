namespace PetPal.Api.PetBrain.Recommendation;

/// <summary>
/// Tövsiyə siyasətinin ÇƏKİLƏRİ və kəşf payı.
///
/// <para><b>Nə üçün konfiqurasiya?</b> Bu rəqəmlər məhsul qərarıdır, kod
/// detalı deyil. Kodun içinə səpələnəndə balansı dəyişmək üçün beş fayla
/// toxunmaq lazım gəlirdi və heç kim cəmin hələ də 1.0 olub-olmadığını
/// bilmirdi. İndi hamısı bir yerdədir, versiyalanır və
/// <see cref="Validate"/> ilə yoxlanılır.</para>
///
/// <para><b>Versiya niyə vacibdir?</b> Qərar jurnalındakı hər sətir hansı
/// çəkilərlə verildiyini daşıyır. Balans dəyişəndə köhnə qərarlar yeni
/// qaydalarla «izah edilmiş» görünməməlidir.</para>
/// </summary>
public class RecommendationPolicyOptions
{
    public const string SectionName = "PetBrainRecommendation";

    /// <summary>Bu çəkilər dəyişəndə ARTIRILIR — jurnal köhnə qərarı doğru izah etsin.</summary>
    public int PolicyVersion { get; set; } = 2;

    // ---------- Bal komponentlərinin çəkiləri ----------
    //
    // Cəm 1.0 olmalıdır. Validate() bunu yoxlayır: səhvən 1.3 yazılsa,
    // bütün ballar şişər və "80 bal" ifadəsi mənasını itirərdi.

    /// <summary>Mövzu uyğunluğu — «kosmos macəralarını sevir».</summary>
    public double TopicFit { get; set; } = 0.20;

    /// <summary>
    /// Mexanika uyğunluğu — «marşrut qurmağı sevir».
    ///
    /// <para>Mövzudan AYRI çəkilir: uşaq kosmosu sevib marşrutu sevməyə
    /// bilər.</para>
    /// </summary>
    public double MechanicFit { get; set; } = 0.15;

    /// <summary>Çətinliyin uşağın ustalığına uyğunluğu.</summary>
    public double MasteryChallengeFit { get; set; } = 0.15;

    /// <summary>Oyun üslubu — «kəşfiyyatçıdır», «həlledicidir».</summary>
    public double StyleFit { get; set; } = 0.10;

    /// <summary>Lazım olan dəstəyin bu macərada mövcudluğu.</summary>
    public double SupportFit { get; set; } = 0.10;

    /// <summary>Sessiya uzunluğu və tempə uyğunluq.</summary>
    public double PaceFit { get; set; } = 0.08;

    /// <summary>Yarımçıq və ya davamı olan hekayə.</summary>
    public double ContinuityFit { get; set; } = 0.07;

    /// <summary>Mükafat növünün uşağın seçiminə uyğunluğu.</summary>
    public double RewardFit { get; set; } = 0.07;

    /// <summary>Yenilik — son vaxt görülənlər geri çəkilir.</summary>
    public double NoveltyValue { get; set; } = 0.08;

    // ---------- Kəşf payı ----------

    /// <summary>
    /// Tövsiyə siyahısındakı KART sayı (əsas + alternativlər).
    ///
    /// <para>Üçdən çox kart uşaq üçün seçim deyil, siyahıdır: qərar vermək
    /// çətinləşir və macəra oynamaq əvəzinə kart çevirmək başlayır.</para>
    /// </summary>
    public int CardCount { get; set; } = 3;

    /// <summary>
    /// Ekran vaxtı azalanda təklif olunan macəranın ƏN ÇOX dəqiqəsi.
    ///
    /// <para>Uşağa bitirə bilməyəcəyi macəranı təklif etmək ən pis
    /// nəticələrdən biridir: ya yarımçıq qalır, ya da limit «haqsız»
    /// görünür.</para>
    /// </summary>
    public int ShortSessionMaxMinutes { get; set; } = 4;

    /// <summary>Çəkilərin cəmi 1.0-dan bu qədər fərqlənə bilər.</summary>
    public const double WeightTolerance = 0.0001;

    /// <summary>Bütün çəkilərin cəmi.</summary>
    public double WeightSum =>
        TopicFit + MechanicFit + MasteryChallengeFit + StyleFit + SupportFit
        + PaceFit + ContinuityFit + RewardFit + NoveltyValue;

    /// <summary>
    /// Konfiqurasiyanın etibarlılığı. Səhv çəki sistemi səssizcə pozardı:
    /// ballar şişər, «80 bal» ifadəsi mənasını itirər, jurnal isə yanlış
    /// izah saxlayardı.
    /// </summary>
    public bool Validate(out string error)
    {
        if (Math.Abs(WeightSum - 1.0) > WeightTolerance)
        {
            error = $"Tövsiyə çəkilərinin cəmi 1.0 olmalıdır; hazırda {WeightSum:F4}.";
            return false;
        }

        if (CardCount is < 1 or > 5)
        {
            error = "CardCount 1 ilə 5 arasında olmalıdır.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
