using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// Bir tapmaca mexanikasının müqaviləsi.
///
/// <para>Kataloq QAPALIDIR və versiyalıdır: naməlum açar heç yerdə qəbul
/// edilmir (fail closed), qaydalar dəyişəndə isə versiya artır və köhnə,
/// verilmiş tapmacalar öz versiyası ilə qiymətləndirilməyə davam edir.</para>
/// </summary>
public sealed record PuzzleBlueprint(
    string Key,
    int Version,
    PetBrainPuzzleMechanic Mechanic,

    /// <summary>Hansı təcrübə janrına yaraşır — yaradıcı yol məntiq tapmacası almır.</summary>
    PetBrainExperienceType ExperienceType,

    /// <summary>Cavabın forması: dəst, yoxsa ardıcıllıq.</summary>
    PetBrainAnswerKind AnswerKind,

    /// <summary>Mexanikanı SEÇƏN ox — oyun üslubu (bax ProfileLearningRules).</summary>
    IReadOnlyList<string> PlayStyles,

    /// <summary>Mövzu/lüğəti çəkən ox — maraqlar.</summary>
    IReadOnlyList<string> Interests,

    /// <summary>Yaş həddi — TƏHLÜKƏSİZLİK sərhədi, bacarıq iddiası deyil.</summary>
    int MinAge,

    /// <summary>Doğru/səhv təzyiqi yoxdur: hər etibarlı seçim qəbul edilir.</summary>
    bool LowPressure,

    /// <summary>
    /// Şablon HEKAYƏSİNƏ bağlı mexanikalar üçün icazə verilən təcrübə açarları.
    ///
    /// <para>Boş siyahı = mövzudan asılı olmayan mexanika: onun mətni
    /// <see cref="PuzzleBlueprintCatalog.VocabularyFor"/> lüğətindən qurulur və
    /// hər macərəyə uyğun gəlir.</para>
    ///
    /// <para>Dolu siyahı = mexanikanın mətni HEKAYƏNİN İÇİNDƏNDİR (Marsdakı
    /// Robo, Aydakı kristal). Belə tapmaca yad macərəyə düşsəydi, uşaq Ay
    /// macərasında Robonu xilas etməyə çağırılardı — süzgəc məhz bunu bağlayır.</para>
    /// </summary>
    IReadOnlyList<string> SupportedExperienceKeys)
{
    /// <summary>Bu mexanika verilən macərada işlədilə bilərmi.</summary>
    public bool SupportsExperience(string experienceKey) =>
        SupportedExperienceKeys.Count == 0
        || SupportedExperienceKeys.Contains(experienceKey, StringComparer.Ordinal);

    /// <summary>Bu mexanika üçün icazə verilən ən böyük element sayı.</summary>
    public int MaxItems => Mechanic switch
    {
        PetBrainPuzzleMechanic.SequenceOrder => 5,
        PetBrainPuzzleMechanic.RouteLogic => 4,
        _ => 8
    };

    /// <summary>
    /// Lövhə qraf üzərindədir — element siyahısı yox, düyün və keçidlər.
    /// Yoxlayıcı buna görə başqa qaydalar tətbiq edir.
    /// </summary>
    public bool IsGraphBoard => Mechanic == PetBrainPuzzleMechanic.OrderedRoute;
}

/// <summary>
/// Mövzuya görə TƏSDİQLƏNMİŞ lüğət. Uşağa görünən hər söz buradan gəlir —
/// nə modeldən, nə də istifadəçi mətnindən.
/// </summary>
public sealed record PuzzleVocabulary(
    string NounAz,
    string NounEn,
    IReadOnlyList<string> Icons);

public static class PuzzleBlueprintCatalog
{
    /// <summary>
    /// <b>Referans şablon.</b> Dron marşrutu: enerji, məcburi antena, tələ yolu.
    /// </summary>
    public const string MarsSignalRouteKey = "mars-signal-route";

    /// <summary>
    /// Ay macərasının öz marşrut tapmacası — eyni qaydalar, AYIN hekayəsi.
    ///
    /// <para>Marşrut mexanikası bir dənə olanda Ay macərası Mars mətnini alırdı:
    /// uşağa «Robo ilə əlaqəni bərpa et» deyilirdi, halbuki hekayədə Robo
    /// ümumiyyətlə yox idi. İndi mexanika eynidir, hekayə paketi isə ayrıdır.</para>
    /// </summary>
    public const string MoonCrystalRouteKey = "moon-crystal-route";

    public const string SequenceOrderKey = "sequence-order";
    public const string RouteLogicKey = "route-logic";
    public const string LightFragmentsKey = "light-fragments";

    /// <summary>Ay siqnalının naxışı — «Göydən gələn siqnal» chapter-i.</summary>
    public const string MoonSignalPatternKey = "moon-signal-pattern";

    /// <summary>Roverin izləri — «İtib-batmış rover» chapter-i.</summary>
    public const string MoonTrackRecallKey = "moon-track-recall";

    /// <summary>Kristal parçaları və yuvaları — rəsədxana və final.</summary>
    public const string MoonShardMatchKey = "moon-shard-match";

    /// <summary>
    /// Ay bazasında enerjini xəttə çəkmək — «Səssiz Ay bazası» fəsli.
    ///
    /// <para>Mexanika Marsdakı dron marşrutu ilə eynidir, hekayə isə ayrıdır:
    /// burada dron yox, enerji axır və məcburi düyün paylayıcı qutudur.</para>
    /// </summary>
    public const string MoonBasePowerKey = "moon-base-power";

    /// <summary>
    /// Beş şablon, dörd mexanika — hər biri AYRI qarşılıqlı təsirdir.
    ///
    /// <para><b>Keyfiyyət həddi:</b> tapmaca hekayənin problemini BİRBAŞA həll
    /// etməlidir. «İki rəqəm seç, cəmi 5 olsun» tipli ayrıq arifmetik kart
    /// QƏSDƏN yoxdur — o, macərəyə yapışdırılmış viktorina olardı, nə Robonu
    /// xilas edərdi, nə də uşağın seçimini mənalandırardı.</para>
    ///
    /// <para><b>Uyğunluq:</b> hekayə mətni daşıyan şablonlar
    /// <c>SupportedExperienceKeys</c> ilə öz macərasına bağlanır; mövzudan asılı
    /// olmayanlar isə boş siyahı ilə hər macərəyə açıq qalır.</para>
    /// </summary>
    public static IReadOnlyList<PuzzleBlueprint> Blueprints { get; } =
    [
        // REFERANS: dronu planlaşdır — enerji çatsın, antena bərpa olunsun,
        // sonra Roboya çat. Düşünmə hərəkəti xilasetmənin ÖZÜDÜR.
        new(MarsSignalRouteKey, Version: 1, PetBrainPuzzleMechanic.OrderedRoute,
            PetBrainExperienceType.Adventure, PetBrainAnswerKind.OrderedNodeIds,
            PlayStyles: [TraitKeys.ProblemSolver, TraitKeys.Explorer],
            Interests: [TraitKeys.Space, TraitKeys.Science, TraitKeys.Puzzles],
            MinAge: 6, LowPressure: false,
            SupportedExperienceKeys: [ExperienceCatalog.MarsRoverRescue]),

        // Ayda kristalı gücləndir: eyni marşrut qaydaları, Ayın öz hekayəsi.
        new(MoonCrystalRouteKey, Version: 1, PetBrainPuzzleMechanic.OrderedRoute,
            PetBrainExperienceType.Adventure, PetBrainAnswerKind.OrderedNodeIds,
            PlayStyles: [TraitKeys.ProblemSolver, TraitKeys.Explorer],
            Interests: [TraitKeys.Space, TraitKeys.Science, TraitKeys.Puzzles],
            MinAge: 6, LowPressure: false,
            SupportedExperienceKeys: [ExperienceCatalog.MoonCrystalRescue]),

        // Kəşfiyyatçı üçün sadə variant: şərtə uyğun YEGANƏ yolu seç.
        new(RouteLogicKey, Version: 1, PetBrainPuzzleMechanic.RouteLogic,
            PetBrainExperienceType.Adventure, PetBrainAnswerKind.SelectIds,
            PlayStyles: [TraitKeys.Explorer],
            Interests: [TraitKeys.Ocean, TraitKeys.Nature],
            MinAge: 5, LowPressure: false,
            SupportedExperienceKeys: []),

        // Şən/qayğıkeş üçün: addımları hekayə sırasına düz. Cavab SIRALIDIR.
        new(SequenceOrderKey, Version: 1, PetBrainPuzzleMechanic.SequenceOrder,
            PetBrainExperienceType.Adventure, PetBrainAnswerKind.OrderIds,
            PlayStyles: [TraitKeys.Playful, TraitKeys.Caring],
            Interests: [TraitKeys.Stories, TraitKeys.Animals],
            MinAge: 5, LowPressure: false,
            SupportedExperienceKeys: []),

        // Yaradıcı yol: qanadın naxışını işıq parçalarından bərpa et.
        // Bir neçə palitra eyni dərəcədə doğrudur — cəza dili yoxdur.
        new(LightFragmentsKey, Version: 1, PetBrainPuzzleMechanic.LightFragments,
            PetBrainExperienceType.Creative, PetBrainAnswerKind.SelectIds,
            PlayStyles: [TraitKeys.Creative],
            Interests: [TraitKeys.Fantasy, TraitKeys.Stories, TraitKeys.Animals],
            MinAge: 5, LowPressure: true,
            SupportedExperienceKeys: []),

        new(MoonBasePowerKey, Version: 1, PetBrainPuzzleMechanic.OrderedRoute,
            PetBrainExperienceType.Adventure, PetBrainAnswerKind.OrderedNodeIds,
            PlayStyles: [TraitKeys.ProblemSolver, TraitKeys.Explorer],
            Interests: [TraitKeys.Space, TraitKeys.Science, TraitKeys.Puzzles],
            MinAge: 6, LowPressure: false,
            SupportedExperienceKeys: [ExperienceCatalog.MoonCrystalSecret]),

        new(MoonSignalPatternKey, Version: 1, PetBrainPuzzleMechanic.SignalPattern,
            PetBrainExperienceType.Adventure, PetBrainAnswerKind.OrderIds,
            PlayStyles: [TraitKeys.ProblemSolver, TraitKeys.Creative],
            Interests: [TraitKeys.Space, TraitKeys.Science, TraitKeys.Puzzles],
            MinAge: 6, LowPressure: false,
            SupportedExperienceKeys: [ExperienceCatalog.MoonCrystalSecret]),

        new(MoonTrackRecallKey, Version: 1, PetBrainPuzzleMechanic.ObservationRecall,
            PetBrainExperienceType.Adventure, PetBrainAnswerKind.SelectIds,
            PlayStyles: [TraitKeys.Explorer, TraitKeys.ProblemSolver],
            Interests: [TraitKeys.Space, TraitKeys.Science],
            MinAge: 6, LowPressure: false,
            SupportedExperienceKeys: [ExperienceCatalog.MoonCrystalSecret]),

        new(MoonShardMatchKey, Version: 1, PetBrainPuzzleMechanic.MatchingPairs,
            PetBrainExperienceType.Adventure, PetBrainAnswerKind.OrderIds,
            PlayStyles: [TraitKeys.ProblemSolver, TraitKeys.Caring],
            Interests: [TraitKeys.Space, TraitKeys.Science, TraitKeys.Puzzles],
            MinAge: 5, LowPressure: false,
            SupportedExperienceKeys: [ExperienceCatalog.MoonCrystalSecret])
    ];

    /// <summary>Hekayəsi olan marşrut şablonları — mətn paketi ayrıca seçilir.</summary>
    public static bool IsRouteStory(string? key) =>
        key is MarsSignalRouteKey or MoonCrystalRouteKey or MoonBasePowerKey;

    public static PuzzleBlueprint? Find(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : Blueprints.FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.Ordinal));

    /// <summary>Naməlum mexanika heç yerdə qəbul edilmir.</summary>
    public static bool IsKnown(string? key) => Find(key) is not null;

    /// <summary>
    /// Mövzunun lüğəti. Naməlum mövzu ÜMUMİ lüğətə düşür — uydurma söz yaranmır.
    /// </summary>
    public static PuzzleVocabulary VocabularyFor(string theme) => theme switch
    {
        TraitKeys.Space => new("enerji xanası", "energy cell", ["🔆", "🔋", "💎", "⚡", "🛰️"]),
        TraitKeys.Science => new("nümunə", "sample", ["🧪", "🔬", "🧫", "⚗️", "🧲"]),
        TraitKeys.Ocean => new("mərcan", "coral", ["🐚", "🪸", "💧", "🐟", "⭐"]),
        TraitKeys.Nature => new("yarpaq", "leaf", ["🍀", "🌿", "🍂", "🌰", "🌼"]),
        TraitKeys.Animals => new("dost", "friend", ["🦊", "🐰", "🦔", "🐿️", "🦉"]),
        TraitKeys.Fantasy => new("kristal", "crystal", ["✨", "🔮", "🌟", "💜", "🌸"]),
        TraitKeys.Stories => new("səhifə", "page", ["📖", "🕯️", "🪶", "📜", "🔖"]),
        "robots" => new("modul", "module", ["⚙️", "🔩", "🔌", "📡", "🧰"]),
        _ => new("parça", "piece", ["🔷", "🔶", "🟣", "🟢", "🟡"])
    };

    /// <summary>
    /// Marşrut tapmacasında BAĞLI yolun işarəsi.
    ///
    /// <para>Qəsdən GÖRÜNƏN məlumatdır: bağlılıq gizli sahədə saxlansaydı,
    /// klient onu oxuyub cavabı çıxara bilərdi. İndi isə uşaq da, yoxlayıcı da
    /// eyni şeyə baxır.</para>
    /// </summary>
    public const string HazardIcon = "⛔";

    /// <summary>Rəngdən ƏLAVƏ forma işarəsi — rəng tək daşıyıcı olmamalıdır.</summary>
    public static readonly IReadOnlyList<string> Shapes = ["circle", "square", "triangle", "diamond", "hexagon"];

    /// <summary>
    /// Yaradıcı yığımın parçaları. Açarlar QƏSDƏN əjdahanın naxış açarlarıdır:
    /// uşağın seçdiyi parça birbaşa qanadın üstünə düşür.
    /// </summary>
    public static IReadOnlyList<(string Id, string LabelAz, string LabelEn, string Icon, string Shape)> AssemblyPieces { get; } =
    [
        ("stars", "Ulduzlar", "Stars", "⭐", "diamond"),
        ("spots", "Xallar", "Spots", "🔵", "circle"),
        ("stripes", "Zolaqlar", "Stripes", "🌈", "square"),
        ("hearts", "Ürəklər", "Hearts", "💜", "triangle")
    ];

    public static string Noun(string theme, string language)
    {
        var vocabulary = VocabularyFor(theme);
        return Localized.T(language, vocabulary.NounAz, vocabulary.NounEn);
    }
}
