namespace PetPal.Api.Wardrobe;

/// <summary>Dizayn studiyasının şəkil provayderi. Standart <see cref="None"/>-dur.</summary>
public enum WardrobeProvider
{
    /// <summary>Xarici çağırış YOXDUR — paltar otağı adi əşyalarla tam işləyir.</summary>
    None = 0,

    /// <summary>OpenAI Image API (<c>/images/generations</c>, <c>/images/edits</c>) — öz OpenAI açarı ilə.</summary>
    OpenAi = 1,

    /// <summary>
    /// Runway Dev API üzərindən EYNİ GPT Image modeli — mövcud Runway açarı ilə
    /// (<c>Runway:ApiKey</c>). Kredit tapmaca və recap ilə eyni hesabdan gedir.
    /// </summary>
    Runway = 2
}

/// <summary>
/// Dizayn studiyasının konfiqurasiyası.
///
/// <para>Standartlar QORUYUCUDUR: provayder yoxdur, yəni heç nə xərclənmir.
/// Açar yalnız secret store və ya mühit dəyişəni ilə verilir — commit
/// edilmir, brauzerə göndərilmir, loga düşmür.</para>
///
/// <para>Keyfiyyət, format, ölçü və nisbət AÇIQ siyahıdan seçilir: bahalı
/// <c>xhigh</c>/<c>max</c>, <c>auto</c> (Runway-də standartı <c>high</c>-dır)
/// və ya naməlum dəyər konfiqurasiya ilə təsadüfən seçilə bilmir, standarta
/// düşür. Hədlər kiçik kredit büdcəsinə görə ehtiyatlıdır.</para>
/// </summary>
public class WardrobeOptions
{
    public const string SectionName = "Wardrobe";

    /// <summary>OpenAI-ın sürətli GPT Image 2.5 modeli — OpenAI API-dəki adı.</summary>
    public const string DefaultImageModel = "gpt-image-2.5-flare";

    /// <summary>Eyni model Runway API-də (rəsmi qiymət səhifəsi, 2026-09-11).</summary>
    public const string DefaultRunwayModel = "gpt_image_2_5_flare";

    private static readonly string[] Qualities = ["low", "medium", "high"];
    private static readonly string[] Formats = ["webp", "jpeg", "png"];

    /// <summary>Portret ölçülər — 16-ya bölünür və app kətanına uyğundur.</summary>
    private static readonly string[] Sizes = ["1024x1536", "832x1248"];

    /// <summary>Runway-in GPT Image modelləri üçün sənədləşdirdiyi portret nisbətlər (1K/2K sinfi).</summary>
    private static readonly string[] RunwayRatios = ["1088:1920", "1280:1920", "1440:1920"];

    /// <summary>
    /// Runway-in rəsmi qiyməti (2026-09-11): 1K/2K şəkil <c>low</c> 1,
    /// <c>medium</c> 5, <c>high</c> 16 kredit; hər istinad şəkli +1.
    /// </summary>
    private static readonly Dictionary<string, int> RunwayCredits = new(StringComparer.Ordinal)
    {
        ["low"] = 1,
        ["medium"] = 5,
        ["high"] = 16
    };

    public WardrobeProvider Provider { get; set; } = WardrobeProvider.None;

    /// <summary>OpenAI API-nin ünvanı — şəkil və moderasiya sorğuları üçün.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>OpenAI açarı — yalnız <see cref="WardrobeProvider.OpenAi"/> yolunda şəkil üçün.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string ImageModel { get; set; } = DefaultImageModel;

    public string RunwayImageModel { get; set; } = DefaultRunwayModel;

    public string RunwayRatio { get; set; } = "1088:1920";

    /// <summary>Uşağın mətnini şəkildən ƏVVƏL yoxlayan pulsuz OpenAI modeli.</summary>
    public string ModerationModel { get; set; } = "omni-moderation-latest";

    /// <summary>
    /// Yalnız moderasiya üçün OpenAI açarı. Şəkillər Runway-dən gələndə də
    /// pulsuz mənaca yoxlama işləyə bilsin deyə şəkil açarından ayrıdır.
    /// </summary>
    public string ModerationApiKey { get; set; } = string.Empty;

    public string Size { get; set; } = "1024x1536";

    public string Quality { get; set; } = "medium";

    /// <summary>
    /// WebP standartdır (OpenAI yolu): PNG portret şəkli mobil şəbəkə üçün
    /// ağırdır. Runway yolunda format provayderin seçimidir.
    /// </summary>
    public string OutputFormat { get; set; } = "webp";

    public int OutputCompression { get; set; } = 85;

    /// <summary>
    /// Pet-in ARDICIL görünüşü üçün növ+mərhələ başına bir dəfə baza portreti
    /// çəkilir və hər paltar onun üzərində redaktə edilir. Söndürüləndə hər
    /// dizayn sıfırdan çəkilir — ucuzdur, amma pet hər dəfə bir az fərqli olur.
    /// </summary>
    public bool UseBasePortrait { get; set; } = true;

    /// <summary>Bir uşağa gündə neçə dizayn. Alınmayan cəhd sayılmır.</summary>
    public int DesignsPerChildPerDay { get; set; } = 3;

    /// <summary>Bütün uşaqlar üzrə gündə neçə pullu şəkil — xərcin sərhədi.</summary>
    public int MaxPaidImagesPerDay { get; set; } = 20;

    /// <summary>
    /// Runway-də bir şəklin kredit tavanı. Standart 6 = <c>medium</c> (5) +
    /// istinad (1). Bahalı keyfiyyət tavanı keçir və pullu iş BAŞLAMIR;
    /// provayder daha çox hesablasa isə xərc kəsicisi açılır.
    /// </summary>
    public int MaxCreditsPerImage { get; set; } = 6;

    /// <summary>Dizayn şəklinin bayt həddi — Flare portreti tapmaca rəsmindən böyükdür.</summary>
    public int MaxImageBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>Studiyada göstərilən son dizaynların sayı.</summary>
    public int GalleryLimit { get; set; } = 12;

    /// <summary>Şəkil sorğusunun vaxt həddi — uşaq gözləmir, işçi gözləyir.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Moderasiya sorğusunun vaxt həddi — qısadır, çünki cavab kiçikdir.</summary>
    public int ModerationTimeoutSeconds { get; set; } = 10;

    /// <summary>Runway tapşırığının izlənmə addımı (millisaniyə). Aşağı hədd 50 ms-dir.</summary>
    public int PollMilliseconds { get; set; } = 2000;

    public bool UsesOpenAi =>
        Provider == WardrobeProvider.OpenAi &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(BaseUrl);

    /// <summary>Runway seçilib; açarın varlığını Runway klienti özü yoxlayır.</summary>
    public bool UsesRunway => Provider == WardrobeProvider.Runway;

    public bool IsEnabled => UsesOpenAi || UsesRunway;

    /// <summary>Moderasiya üçün OpenAI açarı: ayrıca verilibsə o, yoxdursa OpenAI şəkil açarı.</summary>
    public string EffectiveModerationKey =>
        !string.IsNullOrWhiteSpace(ModerationApiKey) ? ModerationApiKey
        : UsesOpenAi ? ApiKey
        : string.Empty;

    public string EffectiveImageModel =>
        string.IsNullOrWhiteSpace(ImageModel) ? DefaultImageModel : ImageModel;

    public string EffectiveRunwayModel =>
        string.IsNullOrWhiteSpace(RunwayImageModel) ? DefaultRunwayModel : RunwayImageModel;

    public string EffectiveQuality => Qualities.Contains(Quality) ? Quality : "medium";

    public string EffectiveOutputFormat => Formats.Contains(OutputFormat) ? OutputFormat : "webp";

    public string EffectiveSize => Sizes.Contains(Size) ? Size : Sizes[0];

    public string EffectiveRunwayRatio => RunwayRatios.Contains(RunwayRatio) ? RunwayRatio : RunwayRatios[0];

    public int EffectiveCompression => Math.Clamp(OutputCompression, 40, 100);

    public int EffectivePollMilliseconds => Math.Max(50, PollMilliseconds);

    /// <summary>Runway-də bir şəklin ən pis halda neçə kredit tutacağı — TƏXMİN deyil, YUXARI HƏDD.</summary>
    public int RunwayCreditsFor(bool withReference) =>
        RunwayCredits[EffectiveQuality] + (withReference ? 1 : 0);
}
