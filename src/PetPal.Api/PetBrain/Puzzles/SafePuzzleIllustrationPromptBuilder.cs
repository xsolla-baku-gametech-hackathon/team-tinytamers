using System.Security.Cryptography;
using System.Text;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// Rəsm promptunu TƏSDİQLƏNMİŞ ifadələrdən yığır.
///
/// <para>Burada string birləşdirmə var, amma <b>heç bir sərbəst mətn yoxdur</b>:
/// hər açar əvvəlcədən yazılmış, nəzərdən keçirilmiş ifadəyə çevrilir. Naməlum
/// açar ÜMUMİ ifadəyə düşür — yəni uydurma söz modelə çatmır.</para>
///
/// <para>Sonda DƏYİŞMƏZ təhlükəsizlik bəndləri əlavə olunur: nə mətn, nə rəqəm,
/// nə ox, nə şəbəkə, nə də həll. Rəsm yalnız ATMOSFERDİR; qaydaları daşıyan
/// hər şey deterministik overlay-dədir.</para>
/// </summary>
public static class SafePuzzleIllustrationPromptBuilder
{
    /// <summary>
    /// Şablonun versiyası. Bəndlər dəyişəndə artır — köhnə keşlənmiş rəsm
    /// yeni qaydalarla qarışmasın deyə hash-a da daxildir.
    /// </summary>
    public const int TemplateVersion = 1;

    /// <summary>
    /// DƏYİŞMƏZ bəndlər. Hər promptun sonuna EYNİ formada əlavə olunur —
    /// açar dəyişsə də bunlar düşmür.
    /// </summary>
    private const string SafetyClause =
        "No children, no photorealistic people, no text, letters, numbers, arrows, route lines, " +
        "grids, UI, logos, weapons, danger, injury, or frightening imagery. Do not reveal a solution.";

    public static string Build(PuzzleSceneSpec spec)
    {
        var builder = new StringBuilder();

        builder.Append("Create a polished child-friendly 2D storybook game illustration in a vertical mobile composition. ");
        builder.Append("Scene: ").Append(Environment(spec.Environment)).Append(", ");
        builder.Append(StoryBeat(spec.StoryBeat)).Append(", ");
        builder.Append(Palette(spec.Palette)).Append(", ");
        builder.Append(Mood(spec.Mood)).Append(", soft cinematic light, clear depth, playful shapes. ");

        if (spec.Props.Count > 0)
            builder.Append("Include: ").Append(string.Join(", ", spec.Props.Select(Prop))).Append(". ");

        builder.Append("A friendly ").Append(Descriptor(spec.PetColor)).Append(' ')
            .Append(Descriptor(spec.PetSpecies)).Append(" companion is present. ");

        if (spec.QuietZones.Count > 0)
        {
            builder.Append("Keep ").Append(string.Join(" and ", spec.QuietZones.Select(QuietZone)))
                .Append(" visually uncluttered so game controls can be overlaid. ");
        }

        builder.Append("Aspect ratio ").Append(PuzzleSceneSpec.AspectRatio).Append(". ");
        builder.Append(SafetyClause);

        return builder.ToString();
    }

    /// <summary>Promptun barmaq izi — nə saxlandığını sonradan yoxlamaq üçün.</summary>
    public static string HashOf(string prompt) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(prompt)));

    // ==================== Təsdiqlənmiş ifadələr ====================

    private static string Environment(string key) => key switch
    {
        "martian-canyon" => "a wide Martian canyon under a dusty orange sky",
        "lunar-crater" => "a wide shallow Moon crater under a black star-filled sky",
        "crystal-garden-at-dusk" => "a quiet crystal garden at dusk with glowing blooms",
        "sunlit-coral-reef" => "a sunlit coral reef under clear turquoise water",
        "sunny-forest-clearing" => "a sunny forest clearing with a winding path",
        "friendly-robot-workshop" => "a bright tidy workshop full of rounded friendly machines",
        _ => "a calm imaginary landscape"
    };

    private static string StoryBeat(string key) => key switch
    {
        "dust-storm-aftermath" => "just after a gentle dust storm has settled",
        "dimmed-crystal-on-the-moon" => "the moment the crystal's glow has faded to a faint spark",
        "dimmed-crystal-garden" => "the moment the garden's light has faded",
        "lost-glowing-fish" => "as little glowing fish drift gently, looking for their way home",
        "parade-preparations" => "as forest friends get ready for a cheerful parade",
        "robot-waking-up" => "the moment a friendly robot blinks awake",
        _ => "a quiet moment in the story"
    };

    private static string Palette(string key) => key switch
    {
        "warm-orange" => "warm orange and sand tones",
        "silver-and-deep-blue" => "silver, pale grey and deep blue tones",
        "violet-and-moonlight" => "violet, teal and moonlight tones",
        "turquoise-and-coral" => "turquoise, deep blue and soft coral tones",
        "leaf-green-and-sunshine" => "fresh leaf green and warm sunshine tones",
        "mint-and-silver" => "mint green, silver and warm lamp-light tones",
        _ => "soft daylight tones"
    };

    private static string Mood(string key) => key switch
    {
        "hopeful-adventurous" => "hopeful and adventurous mood with no fear or damage",
        "calm-wonder" => "calm wonder, quiet and safe",
        "gentle-wonder" => "gentle wonder, warm and safe",
        "playful-cheer" => "playful and cheerful mood, calm and safe",
        "curious-bright" => "bright and curious mood, calm and safe",
        _ => "calm and curious mood"
    };

    private static string Prop(string key) => key switch
    {
        "lander" => "a compact rounded lander in the lower left",
        "solar-array" => "a small solar charging array",
        "relay-antenna" => "a communication antenna on a central ridge",
        "rounded-rocks" => "smooth rounded rocks",
        "friendly-robot" => "a small friendly stranded robot in the upper-right distance",
        "moon-lander" => "a compact rounded moon lander in the lower left",
        "glow-pool" => "a small pool of soft glowing light",
        "mirror-field" => "a cluster of tilted mirror panels on a central rise",
        "rounded-craters" => "smooth shallow craters",
        "large-crystal" => "a tall calm crystal in the upper-right distance",
        "friendly-dragon" => "a small friendly dragon with soft rounded features",
        "crystal-blooms" => "tall crystal blooms",
        "floating-light-motes" => "slow floating motes of light",
        "coral-arches" => "soft rounded coral arches",
        "glowing-fish" => "small friendly fish with a gentle glow",
        "sea-grass" => "swaying sea grass",
        "pearl-shells" => "pearl shells on the sand",
        "friendly-forest-animals" => "a friendly bunny, fox and hedgehog",
        "flower-garlands" => "flower garlands between the trees",
        "mushroom-houses" => "tiny mushroom houses",
        "old-oak-tree" => "a wide old oak tree",
        "workshop-robot" => "a small friendly robot at a workbench",
        "rounded-machines" => "rounded colorful machines",
        "gear-shelves" => "shelves with big friendly gears",
        "desk-lamp" => "a warm desk lamp",
        _ => "simple storybook scenery"
    };

    private static string QuietZone(string key) => key switch
    {
        "central-travel-corridor" => "the central travel corridor",
        "six-node-anchors" => "six evenly spread composition anchors",
        "dragon-wing-slots" => "the area across the dragon's near wing",
        "lower-selection-strip" => "the lower third of the frame",
        _ => "the centre of the frame"
    };

    /// <summary>
    /// Nəzarətli sahədən gələn təsviri təhlükəsiz formaya salır.
    ///
    /// <para>Bu sahələr onsuz da bizim kataloqumuzdandır, amma müdafiə
    /// QATLIDIR: gözlənilməz dəyər buraya düşsə, o, prompta yox, ümumi sözə
    /// çevrilir.</para>
    /// </summary>
    private static string Descriptor(string value) =>
        value.Length is > 0 and <= 24 && value.All(c => char.IsAsciiLetter(c) || c == '-')
            ? value.Replace('-', ' ')
            : "storybook";
}
