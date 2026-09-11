using System.Security.Cryptography;
using System.Text;

namespace PetPal.Api.PetBrain.Scenery;

/// <summary>
/// Arxa fon və obraz promptlarını TƏSDİQLƏNMİŞ ifadələrdən yığır.
///
/// <para>Prinsip tapmaca rəsmi ilə eynidir (<c>SafePuzzleIllustrationPromptBuilder</c>):
/// burada string birləşdirmə var, amma <b>sərbəst mətn yoxdur</b>. Uşağın
/// seçimi yalnız hansı ifadənin götürüləcəyini müəyyən edir — yeni söz yarada
/// bilmir. Naməlum açar ÜMUMİ ifadəyə düşür.</para>
///
/// <para>Sonda DƏYİŞMƏZ təhlükəsizlik bəndləri gəlir. Obrazda bir bənd daha
/// güclüdür: obraz <b>heyvan yoldaşdır</b>, uşağın özü deyil — modeldən heç
/// vaxt insan portreti istənmir.</para>
/// </summary>
public static class SafeSceneryPromptBuilder
{
    /// <summary>Şablonun versiyası — bəndlər dəyişəndə artır və hash-a düşür.</summary>
    public const int TemplateVersion = 1;

    /// <summary>Portret kompozisiya — app kətanı 390×690-dır.</summary>
    public const string AspectRatio = "9:16";

    private const string SafetyClause =
        "No children, no humans, no photorealistic people, no text, letters, numbers, arrows, route lines, " +
        "grids, UI, logos, weapons, danger, injury, or frightening imagery. Do not reveal a solution.";

    /// <summary>Arxa fon: seçimlərin qurduğu MƏKAN — üstünə oyun idarəsi düşür.</summary>
    public static string Backdrop(AdventureBackdropSpec spec)
    {
        var builder = new StringBuilder();

        builder.Append("Create a polished child-friendly 2D storybook game background in a vertical mobile composition. ");
        builder.Append("Scene: ").Append(Place(spec.Place)).Append(", ");
        builder.Append(Light(spec.Light)).Append(", ");
        builder.Append(Palette(spec.Palette)).Append(" tones, ");
        builder.Append(Mood(spec.Mood)).Append(", soft cinematic light, clear depth, playful rounded shapes. ");

        if (spec.SetPieces.Count > 0)
            builder.Append("Include: ").Append(string.Join(", ", spec.SetPieces.Select(SetPiece))).Append(". ");

        builder.Append("A friendly ").Append(Descriptor(spec.CompanionColor)).Append(' ')
            .Append(Descriptor(spec.CompanionSpecies))
            .Append(" companion is present, small and far from the camera. ");

        builder.Append("Keep the upper third and the lower third visually uncluttered so game controls can be overlaid. ");
        builder.Append("Aspect ratio ").Append(AspectRatio).Append(". ");
        builder.Append(SafetyClause);

        return builder.ToString();
    }

    /// <summary>Obraz: seçimlərin geyindirdiyi YOLDAŞ — sadə fonda, yaxın plan.</summary>
    public static string Portrait(HeroPortraitSpec spec)
    {
        var builder = new StringBuilder();

        builder.Append("Create a polished child-friendly 2D storybook character portrait in a vertical mobile composition. ");
        builder.Append("A friendly ").Append(Descriptor(spec.CompanionColor)).Append(' ')
            .Append(Descriptor(spec.CompanionSpecies))
            .Append(" animal companion, shown from the chest up, facing the viewer, with big kind eyes and rounded shapes. ");

        builder.Append("It is ").Append(Role(spec.Role)).Append(". ");

        if (spec.Gear.Count > 0)
            builder.Append("It carries: ").Append(string.Join(", ", spec.Gear.Select(Gear))).Append(". ");

        builder.Append("Background: a simple soft ").Append(Palette(spec.Palette))
            .Append(" gradient with gentle bokeh and no scenery detail. ");

        builder.Append(Mood(spec.Mood)).Append(", soft rim light, centred composition. ");
        builder.Append("Aspect ratio ").Append(AspectRatio).Append(". ");
        builder.Append(SafetyClause);

        return builder.ToString();
    }

    /// <summary>Promptun barmaq izi — nə saxlandığını sonradan yoxlamaq üçün.</summary>
    public static string HashOf(string prompt) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(prompt)));

    private static string Place(string key) => key switch
    {
        SceneryKeys.NightWindow => "a quiet room window at night with a wide starry sky and the Moon outside",
        SceneryKeys.SignalDesk => "a small home science desk with a handmade antenna and warm lamplight",
        SceneryKeys.KitTable => "a table with a neat row of rounded expedition tools laid out",
        SceneryKeys.LaunchField => "a small rounded rocket standing on a grassy launch field",
        SceneryKeys.MoonPlain => "a wide shallow Moon crater under a black star-filled sky",
        SceneryKeys.MoonBaseOutside => "a small domed Moon base on a grey lunar plain",
        SceneryKeys.MoonBaseInside => "a rounded corridor inside a small Moon base with smooth wall panels",
        SceneryKeys.MoonFork => "a lunar ridge where two soft paths split, one toward a cave, one toward a deep crater",
        SceneryKeys.CrystalCave => "a tall crystal cave with rounded glowing formations",
        SceneryKeys.ShadowedCrater => "a deep Moon crater with smooth rounded walls and long soft shadows",
        SceneryKeys.HiddenPassage => "a narrow hidden passage of pale stone with a soft glow ahead",
        SceneryKeys.RoverSite => "a quiet lunar slope where wheel tracks lead to a small stranded rover",
        SceneryKeys.MoonGarden => "a tiny sheltered garden dome on the Moon with small green shoots",
        SceneryKeys.ObservatoryHall => "a round observatory hall with a wide window onto the stars",
        SceneryKeys.ObservatoryCore => "the heart of an observatory, a circular socket ring waiting for crystal shards",
        SceneryKeys.MoonlightReturn => "an observatory balcony as warm light spreads across the lunar plain below",
        SceneryKeys.MartianCanyon => "a wide Martian canyon under a dusty orange sky",
        SceneryKeys.MartianCrater => "a deep rounded Martian crater under a dusty orange sky",
        SceneryKeys.MartianMountain => "a tall windy Martian mountain ridge under a dusty orange sky",
        SceneryKeys.CloudCastle => "a castle of soft clouds high above a pastel sky",
        SceneryKeys.FlowerValley => "a wide valley of oversized friendly flowers",
        SceneryKeys.CrystalGarden => "a quiet crystal garden at dusk with glowing blooms",
        SceneryKeys.CoralReef => "a sunlit coral reef under clear turquoise water",
        SceneryKeys.KelpForest => "a tall swaying kelp forest with soft beams of light",
        SceneryKeys.OpenWater => "wide open water with soft light beams reaching down into the blue",
        SceneryKeys.ForestClearing => "a sunny forest clearing with a winding path",
        SceneryKeys.RiverBank => "a calm river bank with smooth stones and tall grass",
        SceneryKeys.OldOak => "a wide old oak tree with a friendly hollow at its base",
        SceneryKeys.Hilltop => "a grassy hilltop looking over a forest at golden hour",
        SceneryKeys.RobotWorkshop => "a bright tidy workshop full of rounded friendly machines",
        _ => "a calm imaginary landscape"
    };

    private static string Light(string key) => key switch
    {
        SceneryKeys.LightStandby => "lit only by faint standby lights",
        SceneryKeys.LightAwake => "warm panel lights just switched on",
        SceneryKeys.LightLantern => "a carried light crystal casting a warm pool of light",
        SceneryKeys.LightStar => "cool starlight and pale earthlight",
        SceneryKeys.LightCrystal => "bright crystal light filling the scene",
        SceneryKeys.LightDawn => "soft dawn light",
        _ => "clear open daylight"
    };

    private static string Palette(string key) => key switch
    {
        SceneryKeys.PaletteWarmOrange => "warm orange and sand",
        SceneryKeys.PaletteSilverBlue => "silver, pale grey and deep blue",
        SceneryKeys.PaletteViolet => "violet, teal and moonlight",
        SceneryKeys.PaletteTurquoise => "turquoise, deep blue and soft coral",
        SceneryKeys.PaletteLeafGreen => "fresh leaf green and warm sunshine",
        SceneryKeys.PaletteMintSilver => "mint green, silver and warm lamp light",
        SceneryKeys.PaletteSunset => "sunset rose, amber and soft gold",
        SceneryKeys.PaletteOcean => "ocean teal, aqua and pale sky",
        SceneryKeys.PaletteForest => "forest green, moss and warm bark",
        SceneryKeys.PaletteBerry => "berry pink, plum and soft lilac",
        _ => "soft daylight"
    };

    private static string Mood(string key) => key switch
    {
        SceneryKeys.MoodHopeful => "hopeful and adventurous mood with no fear or damage",
        SceneryKeys.MoodCalmWonder => "calm wonder, quiet and safe",
        SceneryKeys.MoodGentle => "gentle wonder, warm and safe",
        SceneryKeys.MoodPlayful => "playful and cheerful mood, calm and safe",
        SceneryKeys.MoodBright => "bright and curious mood, calm and safe",
        SceneryKeys.MoodProud => "proud and warm mood, calm and safe",
        _ => "calm and curious mood"
    };

    private static string SetPiece(string key) => key switch
    {
        SceneryKeys.PieceGlowingMap => "a wall map glowing softly",
        SceneryKeys.PieceLiveAntenna => "an antenna with a gentle signal light",
        SceneryKeys.PieceRepairBay => "a small working repair bay",
        SceneryKeys.PieceFriendlyRover => "a small friendly rover with its lamp on",
        SceneryKeys.PieceSleepingRover => "a small quiet rover resting on its wheels",
        SceneryKeys.PieceServiceBot => "a tiny round service robot",
        SceneryKeys.PieceGardenShoots => "small green shoots in a planter",
        SceneryKeys.PieceObservatoryRing => "a ring of sockets around a calm crystal",
        SceneryKeys.PieceCrystalBlooms => "tall crystal blooms along the path",
        SceneryKeys.PieceLongShadows => "long soft shadows across the ground",
        SceneryKeys.PieceCollectedShards => "a few calm crystal shards resting together",
        SceneryKeys.PieceRopeBridge => "a simple rope bridge over a gap",
        SceneryKeys.PieceMirrorField => "a cluster of tilted mirror panels",
        SceneryKeys.PieceLander => "a compact rounded lander",
        SceneryKeys.PieceSolarArray => "a small solar charging array",
        SceneryKeys.PieceSpareBattery => "a rounded spare battery pack",
        SceneryKeys.PieceReturnShip => "a friendly rounded ship waiting on the ground",
        SceneryKeys.PieceRoundedRocks => "smooth rounded rocks",
        SceneryKeys.PieceGlowingFish => "small friendly fish with a gentle glow",
        SceneryKeys.PieceLanternTrail => "a trail of small floating lanterns",
        SceneryKeys.PiecePearlShells => "pearl shells on the sand",
        SceneryKeys.PieceFlowerGarlands => "flower garlands between the trees",
        SceneryKeys.PieceMushroomHouses => "tiny mushroom houses",
        SceneryKeys.PieceForestFriends => "a friendly bunny, fox and hedgehog",
        SceneryKeys.PieceDrumCircle => "a circle of small hand drums",
        SceneryKeys.PieceGearShelves => "shelves with big friendly gears",
        SceneryKeys.PieceWorkshopLamp => "a warm desk lamp",
        SceneryKeys.PieceFloatingMotes => "slow floating motes of light",
        _ => "simple storybook scenery"
    };

    private static string Role(string key) => key switch
    {
        SceneryKeys.RoleScientist => "dressed as a young scientist, with round explorer goggles and a small sample pouch",
        SceneryKeys.RoleEngineer => "dressed as an engineer, with a tool belt of rounded friendly tools",
        SceneryKeys.RoleTracker => "dressed as a tracker, with a soft scarf and a little lens on a strap",
        SceneryKeys.RoleDiver => "dressed for a gentle dive, with soft fins and a round bubble helmet",
        SceneryKeys.RoleParade => "dressed for a forest parade, in a light handmade costume",
        SceneryKeys.RoleMechanic => "dressed for the workshop, in a soft apron",
        SceneryKeys.RoleArtist => "dressed as an artist, in a small paint smock",
        _ => "dressed for a friendly expedition, with a small explorer backpack"
    };

    private static string Gear(string key) => key switch
    {
        SceneryKeys.GearSpaceSuit => "a soft rounded space suit with a clear round helmet",
        SceneryKeys.GearScanner => "a small handheld scanner",
        SceneryKeys.GearRepairBot => "a tiny helper robot on its shoulder",
        SceneryKeys.GearLightCrystal => "a glowing light crystal on a strap",
        SceneryKeys.GearMoonMap => "a folded map tucked into a pocket",
        SceneryKeys.GearShard => "one calm crystal shard held carefully",
        SceneryKeys.GearSeedPot => "a tiny pot with a green shoot",
        SceneryKeys.GearLantern => "a small warm lantern",
        SceneryKeys.GearLeafCape => "a cape of stitched leaves",
        SceneryKeys.GearFlowerCrown => "a crown of small flowers",
        SceneryKeys.GearStarCloak => "a cloak with tiny embroidered stars",
        SceneryKeys.GearDrum => "a small hand drum",
        SceneryKeys.GearFlute => "a simple wooden flute",
        SceneryKeys.GearPaintBrush => "a wide soft paint brush",
        SceneryKeys.GearNewLamp => "a small round lamp",
        SceneryKeys.GearStoryBook => "a little storybook",
        SceneryKeys.GearBadge => "a hand-painted badge",
        _ => "a simple friendly keepsake"
    };

    /// <summary>
    /// Nəzarətli sahədən gələn təsviri təhlükəsiz formaya salır — müdafiə
    /// QATLIDIR: gözlənilməz dəyər prompta yox, ümumi sözə çevrilir.
    /// </summary>
    private static string Descriptor(string value) =>
        value.Length is > 0 and <= 24 && value.All(c => char.IsAsciiLetter(c) || c == '-')
            ? value.Replace('-', ' ')
            : "storybook";
}
