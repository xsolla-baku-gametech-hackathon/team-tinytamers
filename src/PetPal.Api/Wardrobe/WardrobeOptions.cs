namespace PetPal.Api.Wardrobe;

/// <summary>Dizayn studiyasının şəkil provayderi. Standart <see cref="None"/>-dur.</summary>
public enum WardrobeProvider
{
    /// <summary>Xarici çağırış YOXDUR — paltar otağı adi əşyalarla tam işləyir.</summary>
    None = 0,

    /// <summary>OpenAI Image API (<c>/images/generations</c>, <c>/images/edits</c>).</summary>
    OpenAi = 1
}

/// <summary>
/// Dizayn studiyasının konfiqurasiyası.
///
/// <para>Standartlar QORUYUCUDUR: provayder yoxdur, açar yoxdur, yəni heç nə
/// xərclənmir. Açar yalnız secret store və ya <c>Wardrobe__ApiKey</c> ilə
/// verilir — commit edilmir, brauzerə göndərilmir, loga düşmür.</para>
///
/// <para>Keyfiyyət, format və ölçü AÇIQ siyahıdan seçilir: bahalı
/// <c>xhigh</c>/<c>max</c> və ya naməlum dəyər konfiqurasiya ilə təsadüfən
/// seçilə bilmir, standarta düşür.</para>
/// </summary>
public class WardrobeOptions
{
    public const string SectionName = "Wardrobe";

    /// <summary>İstifadəçinin seçdiyi model — OpenAI-ın sürətli GPT Image 2.5 modeli.</summary>
    public const string DefaultImageModel = "gpt-image-2.5-flare";

    private static readonly string[] Qualities = ["low", "medium", "high", "auto"];
    private static readonly string[] Formats = ["webp", "jpeg", "png"];

    /// <summary>Portret ölçülər — 16-ya bölünür və app kətanına uyğundur.</summary>
    private static readonly string[] Sizes = ["1024x1536", "832x1248"];

    public WardrobeProvider Provider { get; set; } = WardrobeProvider.None;

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string ImageModel { get; set; } = DefaultImageModel;

    /// <summary>Uşağın mətnini şəkildən ƏVVƏL yoxlayan pulsuz model.</summary>
    public string ModerationModel { get; set; } = "omni-moderation-latest";

    public string Size { get; set; } = "1024x1536";

    public string Quality { get; set; } = "medium";

    /// <summary>
    /// WebP standartdır: PNG portret şəkli mobil şəbəkə üçün ağırdır və saxlanc
    /// həddini (3 MB) keçə bilər.
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
    public int DesignsPerChildPerDay { get; set; } = 5;

    /// <summary>Bütün uşaqlar üzrə gündə neçə pullu şəkil — xərcin sərhədi.</summary>
    public int MaxPaidImagesPerDay { get; set; } = 60;

    /// <summary>Studiyada göstərilən son dizaynların sayı.</summary>
    public int GalleryLimit { get; set; } = 12;

    /// <summary>Şəkil sorğusunun vaxt həddi — uşaq gözləmir, işçi gözləyir.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Moderasiya sorğusunun vaxt həddi — qısadır, çünki cavab kiçikdir.</summary>
    public int ModerationTimeoutSeconds { get; set; } = 10;

    public bool IsEnabled =>
        Provider == WardrobeProvider.OpenAi &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(BaseUrl);

    public string EffectiveImageModel =>
        string.IsNullOrWhiteSpace(ImageModel) ? DefaultImageModel : ImageModel;

    public string EffectiveQuality => Qualities.Contains(Quality) ? Quality : "medium";

    public string EffectiveOutputFormat => Formats.Contains(OutputFormat) ? OutputFormat : "webp";

    public string EffectiveSize => Sizes.Contains(Size) ? Size : Sizes[0];

    public int EffectiveCompression => Math.Clamp(OutputCompression, 40, 100);
}
