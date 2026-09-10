namespace PetPal.Api.PetBrain.Media;

/// <summary>
/// Bir modelin TƏSDİQLƏNMİŞ qiyməti və məhdudiyyətləri.
/// </summary>
/// <param name="Key">Provayderdəki model açarı.</param>
/// <param name="CreditsPerUnit">Şəkil üçün kadr başına, video üçün SANİYƏ başına.</param>
/// <param name="IsVideo">Video modelidirmi.</param>
/// <param name="Ratios">İcazə verilən nisbətlər — portret tələbi buradan yoxlanılır.</param>
/// <param name="MaxDurationSeconds">Video üçün icazə verilən ən uzun müddət.</param>
public sealed record MediaModel(
    string Key,
    int CreditsPerUnit,
    bool IsVideo,
    IReadOnlyList<string> Ratios,
    int MaxDurationSeconds = 0);

/// <summary>
/// Modellərin <b>QAPALI icazə siyahısı</b>.
///
/// <para>Bu, sadəcə qiymət cədvəli deyil — təhlükəsizlik sərhədidir. Naməlum
/// model heç yerdə qəbul edilmir (fail closed), yəni provayderin kataloqu
/// dəyişəndə yeni, bahalı model avtomatik uyğun gəlmir.</para>
///
/// <para><b>Qiymətlər və müqavilə 2026-09-11-də yoxlanılıb</b> — qiymət
/// <c>docs.dev.runwayml.com/guides/pricing/</c>, parametrlər rəsmi SDK-dan
/// (<c>runwayml/sdk-node</c>, <c>runwayml/sdk-python</c>): 1 kredit = $0.01.
/// Qiymət xarici faktdır — dəyişəndə burada YENİLƏNMƏLİDİR, kod isə təxmin
/// etməməlidir.</para>
///
/// <para><c>gen4_image_turbo</c> QƏSDƏN yoxdur. O, 1–3 referans şəkil TƏLƏB
/// EDİR, hekayə səhnəsi isə yalnız mətndən çəkilir — kataloqda qalsaydı hər
/// sorğu <c>400</c> alardı və heç bir tapmaca rəsm görməzdi.</para>
/// </summary>
public static class MediaModelCatalog
{
    /// <summary>Mətndən şəkil: 720p şəkil 5 kredit, 1080p 8 kredit. Referans şəkil məcburi deyil.</summary>
    public const string Gen4Image = "gen4_image";

    /// <summary>Image-to-video: 5 kredit / saniyə → 10 saniyə = 50 kredit = $0.50.</summary>
    public const string Gen4Turbo = "gen4_turbo";

    /// <summary>12 kredit / saniyə → 10 saniyə = 120 kredit = $1.20.</summary>
    public const string Gen45 = "gen4.5";

    /// <summary>1 kredit / şəkil — YALNIZ sınaq profilində.</summary>
    public const string MuseImage = "muse_image";

    /// <summary>Kreditin dollar dəyəri — yalnız BÖYÜKLƏR üçün hesabatda.</summary>
    public const decimal UsdPerCredit = 0.01m;

    /// <summary>
    /// <c>gen4_image</c>-in icazəli nisbətləri — YALNIZ 720p sinfi portret.
    ///
    /// <para>1080p nisbətlər (<c>1080:1920</c>, <c>1080:1440</c>) 8 kredit tutur.
    /// Onların siyahıdan kənarda qalması «ən pis hal» qiymətini 5 kreditdə
    /// saxlayır: bahalı ölçü konfiqurasiya ilə təsadüfən seçilə bilmir.</para>
    /// </summary>
    private static readonly string[] Gen4ImagePortrait = ["720:1280", "720:960"];

    /// <summary><c>gen4_turbo</c> üçün SDK-da sadalanan portret nisbətlər.</summary>
    private static readonly string[] Gen4TurboPortrait = ["720:1280", "832:1104"];

    /// <summary>Yalnız app-in işlətdiyi nisbət — bu modellərin tam siyahısı yoxlanmayıb.</summary>
    private static readonly string[] AppRatioOnly = ["720:1280"];

    private static readonly Dictionary<string, MediaModel> Models = new(StringComparer.Ordinal)
    {
        [Gen4Image] = new(Gen4Image, CreditsPerUnit: 5, IsVideo: false, Gen4ImagePortrait),
        [MuseImage] = new(MuseImage, CreditsPerUnit: 1, IsVideo: false, AppRatioOnly),
        [Gen4Turbo] = new(Gen4Turbo, CreditsPerUnit: 5, IsVideo: true, Gen4TurboPortrait, MaxDurationSeconds: 10),
        [Gen45] = new(Gen45, CreditsPerUnit: 12, IsVideo: true, AppRatioOnly, MaxDurationSeconds: 10)
    };

    /// <summary>
    /// Profilin İCAZƏ VERDİYİ modellər.
    ///
    /// <para>Model açarı konfiqurasiyadan gəlsə də profil onu məhdudlaşdırır:
    /// <c>Budget</c> profilində <c>gen4.5</c> yazmaq işləməyəcək. Beləliklə
    /// "konfiqurasiyanı dəyişib bahalı modelə keçmək" təsadüfən baş vermir.</para>
    /// </summary>
    public static IReadOnlyList<string> Allowed(PetBrainMediaProfile profile) => profile switch
    {
        PetBrainMediaProfile.Budget => [Gen4Image, Gen4Turbo],
        PetBrainMediaProfile.QualityDemo => [Gen4Image, Gen45],
        PetBrainMediaProfile.ExperimentalBudget => [Gen4Image, MuseImage, Gen4Turbo],
        _ => []
    };

    public static MediaModel? Find(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : Models.GetValueOrDefault(key);

    public static bool IsAllowed(PetBrainMediaProfile profile, string? key) =>
        key is not null && Allowed(profile).Contains(key, StringComparer.Ordinal);

    /// <summary>Nisbət bu model üçün icazəlidirmi (və portretdirmi).</summary>
    public static bool SupportsRatio(string modelKey, string? ratio) =>
        Find(modelKey) is { } model &&
        ratio is not null &&
        model.Ratios.Contains(ratio, StringComparer.Ordinal);

    /// <summary>Ən pis halda neçə kredit — TƏXMİN deyil, YUXARI HƏDD.</summary>
    public static int WorstCaseCredits(string modelKey, int units) =>
        Find(modelKey) is { } model ? model.CreditsPerUnit * Math.Max(1, units) : int.MaxValue;

    public static decimal Usd(int credits) => credits * UsdPerCredit;
}
