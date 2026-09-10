namespace PetPal.Api.PetBrain.Media;

/// <summary>Media provayderi. Standart <see cref="None"/>-dur.</summary>
public enum PetBrainMediaProvider
{
    /// <summary>Xarici çağırış YOXDUR — deterministik rəsm və recap qalır. Standart.</summary>
    None = 0,

    /// <summary>Runway Dev API — şəkil və video eyni hesabdan.</summary>
    Runway = 1,

    /// <summary>
    /// Köhnə OpenAI-uyğun şəkil adapteri. Yalnız AÇIQ seçiləndə işləyir və
    /// video qatı YOXDUR — bu, sənədləşdirilmiş məhdudiyyətdir.
    /// </summary>
    OpenAiCompatibleImage = 2
}

/// <summary>
/// Xərc profili — UŞAĞA GÖRÜNMÜR, yalnız yerləşdirmə qərarıdır.
/// </summary>
public enum PetBrainMediaProfile
{
    /// <summary>Tələb olunan standart: <c>gen4_image_turbo</c> + <c>gen4_turbo</c>.</summary>
    Budget = 0,

    /// <summary>Nümayiş üçün daha keyfiyyətli video (<c>gen4.5</c>) — bahalıdır.</summary>
    QualityDemo = 1,

    /// <summary>Heç bir xarici çağırış yoxdur; provayder xərci sıfırdır.</summary>
    FallbackOnly = 2,

    /// <summary>
    /// Sınaq modelləri. Yalnız icazə siyahısındakı modellər və yalnız açıq
    /// seçimlə — vizual davamlılıq, portret, müddət və uşaq təhlükəsizliyi
    /// testlərindən sonra.
    /// </summary>
    ExperimentalBudget = 3
}

/// <summary>
/// Media qatının konfiqurasiyası.
///
/// <para>Standartlar QORUYUCUDUR: provayder yoxdur, profil <c>Budget</c>,
/// kredit tavanları isə profilin öz dəyərləri ilə eynidir. Yəni açar olmadan
/// heç nə xərclənmir və heç nə sınmır.</para>
/// </summary>
public class PetBrainMediaOptions
{
    public const string SectionName = "PetBrainMedia";

    public PetBrainMediaProvider Provider { get; set; } = PetBrainMediaProvider.None;

    public PetBrainMediaProfile Profile { get; set; } = PetBrainMediaProfile.Budget;

    public string ImageModel { get; set; } = MediaModelCatalog.Gen4ImageTurbo;

    public string VideoModel { get; set; } = MediaModelCatalog.Gen4Turbo;

    /// <summary>Portret — app kətanı 390×690-dır.</summary>
    public string VideoRatio { get; set; } = "720:1280";

    public int VideoDurationSeconds { get; set; } = 10;

    /// <summary>Bir run üçün şəkil kreditinin TAVANI.</summary>
    public int MaxImageCreditsPerRun { get; set; } = 2;

    /// <summary>Bir run üçün video kreditinin TAVANI.</summary>
    public int MaxVideoCreditsPerRun { get; set; } = 50;

    /// <summary>Bir uşağa gündə neçə PULLU recap. Keşlənmiş təkrar sayılmır.</summary>
    public int MaxPaidRecapsPerChildPerDay { get; set; } = 3;

    /// <summary>Video işinin ÜMUMİ vaxt həddi (saniyə).</summary>
    public int GenerationDeadlineSeconds { get; set; } = 180;

    /// <summary>
    /// İşin izlənmə addımı (millisaniyə).
    ///
    /// <para>Konfiqurasiya oluna bilir ki, testlər saatlarla oynamadan sürətli
    /// qalsın; produksiyada standart dəyər provayderi lazımsız yerə yormur.
    /// Aşağı hədd 50 ms-dir — sıfır dəyər sıx dövrə yaradardı.</para>
    /// </summary>
    public int RecapPollMilliseconds { get; set; } = 3000;

    /// <summary>Pullu iş ümumiyyətlə mümkündürmü.</summary>
    public bool PaidMediaEnabled =>
        Provider is not PetBrainMediaProvider.None && Profile is not PetBrainMediaProfile.FallbackOnly;
}

/// <summary>Runway Dev API-nin bağlantı məlumatı.</summary>
public class RunwayOptions
{
    public const string SectionName = "Runway";

    public string BaseUrl { get; set; } = "https://api.dev.runwayml.com/v1";

    /// <summary>
    /// Nəzərdən keçirilmiş API versiyası. <b>Bir yerdə saxlanılır</b> — adapter
    /// dəyişəndə də sənəd və testlər eyni dəyərə baxır.
    /// </summary>
    public string ApiVersion { get; set; } = "2024-11-06";

    /// <summary>
    /// YALNIZ secret store və ya <c>Runway__ApiKey</c> ilə verilir.
    /// Heç vaxt commit edilmir, brauzerə göndərilmir, loga və xəta mətninə düşmür.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
