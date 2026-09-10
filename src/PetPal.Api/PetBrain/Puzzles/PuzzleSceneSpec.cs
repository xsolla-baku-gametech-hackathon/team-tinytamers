using System.Security.Cryptography;
using System.Text;
using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// Rəsm modelinə verilə bilən <b>TAM SİYAHI</b>.
///
/// <para>Burada uşağa aid heç nə yoxdur: nə ad, nə id, nə şəkil, nə söhbət, nə
/// xatirə mətni, nə məktəb, nə yer. Yalnız kataloqdan gələn təsdiqlənmiş
/// açarlar var — ona görə "səhvən nəsə göndərmək" mümkün deyil: göndəriləsi
/// sahə ümumiyyətlə mövcud deyil.</para>
///
/// <para>Həll də burada YOXDUR. Model marşrutu bilmir, bilə də bilməz —
/// yəni rəsm cavabı sızdıra bilmir.</para>
/// </summary>
public sealed record PuzzleSceneSpec(
    /// <summary>Kataloq açarı — <c>mars-signal-route</c>, <c>light-fragments</c>.</summary>
    string BlueprintKey,

    int BlueprintVersion,

    /// <summary>Təcrübə açarı — <c>mars-rover-rescue</c>.</summary>
    string ExperienceKey,

    /// <summary>Hekayənin hansı anı: <c>dust-storm-aftermath</c>.</summary>
    string StoryBeat,

    /// <summary>Mühit açarı: <c>martian-canyon</c>.</summary>
    string Environment,

    /// <summary>Əhval açarı: <c>hopeful</c>.</summary>
    string Mood,

    /// <summary>Palitra açarı: <c>warm-orange</c>.</summary>
    string Palette,

    /// <summary>Pet-in növü və rəngi — NƏZARƏTLİ sahələrdən.</summary>
    string PetSpecies,
    string PetColor,

    /// <summary>Səhnədə görünən təsdiqlənmiş rekvizit açarları.</summary>
    IReadOnlyList<string> Props,

    /// <summary>Overlay-in tutduğu, VİZUAL OLARAQ SAKİT qalmalı zonalar.</summary>
    IReadOnlyList<string> QuietZones,

    /// <summary>Uşağın dili — yalnız alt mətn üçün, prompta düşmür.</summary>
    string Language)
{
    /// <summary>Portret kompozisiya — app kətanı 390×690-dır.</summary>
    public const string AspectRatio = "9:16";

    /// <summary>Sahə ayırıcısı — PuzzleSeed ilə eyni (0x1F, unit separator).</summary>
    private const char Separator = (char)0x1F;

    /// <summary>
    /// Kanonik hash — eyni run üçün SABİTDİR.
    ///
    /// <para>İdempotentlik açarıdır: eyni hash üçün ikinci PULLU sorğu
    /// getmir, yenilənmədən sonra isə uşaq eyni səhnəni görür.</para>
    /// </summary>
    public string Hash()
    {
        // Ayırıcı PuzzleSeed ilə eyni prinsipdədir (0x1F, unit separator):
        // qonşu sahələr bir-birinə "yapışa" bilməz, yəni ("ab","c") ilə
        // ("a","bc") fərqli hash verir.
        var canonical = string.Join(Separator,
        [
            "v" + SafePuzzleIllustrationPromptBuilder.TemplateVersion,
            BlueprintKey,
            BlueprintVersion.ToString(),
            ExperienceKey,
            StoryBeat,
            Environment,
            Mood,
            Palette,
            PetSpecies,
            PetColor,
            string.Join(',', Props),
            string.Join(',', QuietZones),
            AspectRatio
        ]);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// Ekran oxuyucusu üçün mətn — NƏZARƏTLİ şablondan, modelin çıxışından yox.
    ///
    /// <para>Model mətni buraya düşsəydi, ekran oxuyucusu ilə işləyən uşaq
    /// yoxlanmamış cümlə eşidərdi.</para>
    /// </summary>
    public string AltText() => BlueprintKey switch
    {
        PuzzleBlueprintCatalog.MarsSignalRouteKey => Localized.T(Language,
            "Mars dərəsində eniş modulu, günəş stansiyası, rabitə antenası və dost robot.",
            "A Martian canyon with a lander, a solar station, a relay antenna and a friendly robot."),

        PuzzleBlueprintCatalog.MoonCrystalRouteKey => Localized.T(Language,
            "Ay krateri: ay modulu, işıq gölməçəsi, güzgü sahəsi və sönmüş kristal.",
            "A Moon crater: the lander, a glow pool, a mirror field and a dimmed crystal."),

        PuzzleBlueprintCatalog.LightFragmentsKey => Localized.T(Language,
            "Kristal bağçada mehriban əjdaha — qanadının naxışı hələ solğundur.",
            "A friendly dragon in a crystal garden — the pattern on its wing is still faded."),

        _ => Localized.T(Language, "Hekayə səhnəsi.", "A story scene.")
    };

    /// <summary>
    /// Şablondan səhnə təsviri qurur.
    ///
    /// <para>Bütün dəyərlər KATALOQDANDIR. Uşağın profili yalnız artıq
    /// təsdiqlənmiş açarlar arasında seçim edə bilir — yeni söz yarada bilmir.</para>
    /// </summary>
    public static PuzzleSceneSpec For(
        PuzzleBlueprint blueprint,
        string language,
        ExperienceTemplate template,
        string species)
    {
        var petSpecies = Approved(species);
        var petColor = ColorFor(petSpecies);

        return blueprint.Key switch
        {
            PuzzleBlueprintCatalog.MarsSignalRouteKey => new(
            blueprint.Key, blueprint.Version, template.Key,
            StoryBeat: "dust-storm-aftermath",
            Environment: "martian-canyon",
            Mood: "hopeful-adventurous",
            Palette: "warm-orange",
            PetSpecies: petSpecies,
            PetColor: petColor,
            Props: ["lander", "solar-array", "relay-antenna", "rounded-rocks", "friendly-robot"],
            QuietZones: ["central-travel-corridor", "six-node-anchors"],
            Language: language),

            PuzzleBlueprintCatalog.MoonCrystalRouteKey => new(
            blueprint.Key, blueprint.Version, template.Key,
            StoryBeat: "dimmed-crystal-on-the-moon",
            Environment: "lunar-crater",
            Mood: "calm-wonder",
            Palette: "silver-and-deep-blue",
            PetSpecies: petSpecies,
            PetColor: petColor,
            Props: ["moon-lander", "glow-pool", "mirror-field", "rounded-craters", "large-crystal"],
            QuietZones: ["central-travel-corridor", "six-node-anchors"],
            Language: language),

            PuzzleBlueprintCatalog.LightFragmentsKey => new(
            blueprint.Key, blueprint.Version, template.Key,
            StoryBeat: "dimmed-crystal-garden",
            Environment: "crystal-garden-at-dusk",
            Mood: "gentle-wonder",
            Palette: "violet-and-moonlight",
            PetSpecies: petSpecies,
            PetColor: petColor,
            Props: ["friendly-dragon", "crystal-blooms", "floating-light-motes"],
            QuietZones: ["dragon-wing-slots", "lower-selection-strip"],
            Language: language),

            _ => new(
                blueprint.Key, blueprint.Version, template.Key,
                StoryBeat: "quiet-moment",
            Environment: template.Theme,
            Mood: "calm-curious",
            Palette: "soft-daylight",
            PetSpecies: petSpecies,
            PetColor: petColor,
            Props: [],
            QuietZones: ["lower-selection-strip"],
            Language: language)
        };
    }

    /// <summary>
    /// Növ adı TƏSDİQLƏNMİŞ siyahıdadırmı. Bu sahə onsuz da bizim
    /// kataloqumuzdandır, amma müdafiə QATLIDIR: gözlənilməz dəyər prompta
    /// düşməməlidir.
    /// </summary>
    private static string Approved(string species) => species switch
    {
        "fox" or "cat" or "dragon" or "owl" or "bunny" => species,
        _ => "fox"
    };

    /// <summary>Növün NƏZARƏTLİ rəngi — sərbəst mətn deyil.</summary>
    private static string ColorFor(string species) => species switch
    {
        "fox" => "warm-orange",
        "cat" => "soft-grey",
        "dragon" => "mint-green",
        "owl" => "dusk-blue",
        "bunny" => "cream-white",
        _ => "warm-orange"
    };
}
