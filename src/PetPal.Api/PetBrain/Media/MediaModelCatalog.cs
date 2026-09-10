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
/// <para><b>Qiymətlər 2026-09-10-da rəsmi sənəddən yoxlanılıb</b>
/// (<c>docs.dev.runwayml.com/guides/pricing/</c>): 1 kredit = $0.01.
/// Qiymət xarici faktdır — dəyişəndə burada YENİLƏNMƏLİDİR, kod isə
/// təxmin etməməlidir.</para>
/// </summary>
public static class MediaModelCatalog
{
    public const string Gen4ImageTurbo = "gen4_image_turbo";
    public const string Gen4Turbo = "gen4_turbo";
    public const string Gen45 = "gen4.5";
    public const string MuseImage = "muse_image";

    /// <summary>Kreditin dollar dəyəri — yalnız BÖYÜKLƏR üçün hesabatda.</summary>
    public const decimal UsdPerCredit = 0.01m;

    /// <summary>Portret nisbətlər — kətan 390×690-dır, yataylıq kəsilərdi.</summary>
    private static readonly string[] Portrait = ["720:1280", "768:1280", "832:1104"];

    private static readonly Dictionary<string, MediaModel> Models = new(StringComparer.Ordinal)
    {
        // 2 kredit / şəkil, hər ölçüdə.
        [Gen4ImageTurbo] = new(Gen4ImageTurbo, CreditsPerUnit: 2, IsVideo: false, Portrait),

        // 1 kredit / şəkil — YALNIZ sınaq profilində.
        [MuseImage] = new(MuseImage, CreditsPerUnit: 1, IsVideo: false, Portrait),

        // 5 kredit / saniyə → 10 saniyə = 50 kredit = $0.50.
        [Gen4Turbo] = new(Gen4Turbo, CreditsPerUnit: 5, IsVideo: true, Portrait, MaxDurationSeconds: 10),

        // 12 kredit / saniyə → 10 saniyə = 120 kredit = $1.20.
        [Gen45] = new(Gen45, CreditsPerUnit: 12, IsVideo: true, Portrait, MaxDurationSeconds: 10)
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
        PetBrainMediaProfile.Budget => [Gen4ImageTurbo, Gen4Turbo],
        PetBrainMediaProfile.QualityDemo => [Gen4ImageTurbo, Gen45],
        PetBrainMediaProfile.ExperimentalBudget => [Gen4ImageTurbo, MuseImage, Gen4Turbo],
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
