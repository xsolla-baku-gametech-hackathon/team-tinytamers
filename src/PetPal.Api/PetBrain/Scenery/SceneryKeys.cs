namespace PetPal.Api.PetBrain.Scenery;

/// <summary>
/// Arxa fon və obraz təsvirinin BÜTÜN söz ehtiyatı.
///
/// <para>Bu, sadəcə sabitlər siyahısı deyil — <b>qapalı lüğətdir</b>. Direktor
/// yalnız buradakı açarları qaytara bilir, prompt qurucusu isə yalnız buradakı
/// açarları tanıyır. Nəticədə "uşağın seçimi mətnə çevrilib modelə getdi"
/// vəziyyəti mümkün deyil: seçim yalnız hansı hazır ifadənin götürüləcəyini
/// müəyyən edir.</para>
///
/// <para>Açarlar hekayə açarlarından (<c>MoonKeys</c>, kataloqun variant
/// açarları) QƏSDƏN ayrıdır: hekayə açarı dəyişəndə rəsm lüğəti sınmır və
/// keşlənmiş səhnələr etibarsız olmur.</para>
/// </summary>
public static class SceneryKeys
{
    public const string NightWindow = "night-window";
    public const string SignalDesk = "signal-desk";
    public const string KitTable = "kit-table";
    public const string LaunchField = "launch-field";
    public const string MoonPlain = "moon-plain";
    public const string MoonBaseOutside = "moon-base-outside";
    public const string MoonBaseInside = "moon-base-inside";
    public const string MoonFork = "moon-fork";
    public const string CrystalCave = "crystal-cave";
    public const string ShadowedCrater = "shadowed-crater";
    public const string HiddenPassage = "hidden-passage";
    public const string RoverSite = "rover-site";
    public const string MoonGarden = "moon-garden";
    public const string ObservatoryHall = "observatory-hall";
    public const string ObservatoryCore = "observatory-core";
    public const string MoonlightReturn = "moonlight-return";
    public const string MartianCanyon = "martian-canyon";
    public const string MartianCrater = "martian-crater";
    public const string MartianMountain = "martian-mountain";
    public const string CloudCastle = "cloud-castle";
    public const string FlowerValley = "flower-valley";
    public const string CrystalGarden = "crystal-garden";
    public const string CoralReef = "coral-reef";
    public const string KelpForest = "kelp-forest";
    public const string OpenWater = "open-water";
    public const string ForestClearing = "forest-clearing";
    public const string RiverBank = "river-bank";
    public const string OldOak = "old-oak";
    public const string Hilltop = "hilltop";
    public const string RobotWorkshop = "robot-workshop";

    public const string LightDaylight = "light-daylight";
    public const string LightStandby = "light-standby";
    public const string LightAwake = "light-awake";
    public const string LightLantern = "light-lantern";
    public const string LightStar = "light-star";
    public const string LightCrystal = "light-crystal";
    public const string LightDawn = "light-dawn";

    public const string PaletteWarmOrange = "warm-orange";
    public const string PaletteSilverBlue = "silver-and-deep-blue";
    public const string PaletteViolet = "violet-and-moonlight";
    public const string PaletteTurquoise = "turquoise-and-coral";
    public const string PaletteLeafGreen = "leaf-green-and-sunshine";
    public const string PaletteMintSilver = "mint-and-silver";
    public const string PaletteSunset = "sunset";
    public const string PaletteOcean = "ocean";
    public const string PaletteForest = "forest";
    public const string PaletteBerry = "berry";

    public const string MoodHopeful = "hopeful-adventurous";
    public const string MoodCalmWonder = "calm-wonder";
    public const string MoodGentle = "gentle-wonder";
    public const string MoodPlayful = "playful-cheer";
    public const string MoodBright = "curious-bright";
    public const string MoodProud = "proud-warm";

    public const string PieceGlowingMap = "glowing-map";
    public const string PieceLiveAntenna = "live-antenna";
    public const string PieceRepairBay = "repair-bay";
    public const string PieceFriendlyRover = "friendly-rover";
    public const string PieceSleepingRover = "sleeping-rover";
    public const string PieceServiceBot = "service-bot";
    public const string PieceGardenShoots = "garden-shoots";
    public const string PieceObservatoryRing = "observatory-ring";
    public const string PieceCrystalBlooms = "crystal-blooms";
    public const string PieceLongShadows = "long-shadows";
    public const string PieceCollectedShards = "collected-shards";
    public const string PieceRopeBridge = "rope-bridge";
    public const string PieceMirrorField = "mirror-field";
    public const string PieceLander = "lander";
    public const string PieceSolarArray = "solar-array";
    public const string PieceSpareBattery = "spare-battery";
    public const string PieceReturnShip = "return-ship";
    public const string PieceRoundedRocks = "rounded-rocks";
    public const string PieceGlowingFish = "glowing-fish";
    public const string PieceLanternTrail = "lantern-trail";
    public const string PiecePearlShells = "pearl-shells";
    public const string PieceFlowerGarlands = "flower-garlands";
    public const string PieceMushroomHouses = "mushroom-houses";
    public const string PieceForestFriends = "forest-friends";
    public const string PieceDrumCircle = "drum-circle";
    public const string PieceGearShelves = "gear-shelves";
    public const string PieceWorkshopLamp = "workshop-lamp";
    public const string PieceFloatingMotes = "floating-motes";

    public const string RoleExplorer = "role-explorer";
    public const string RoleScientist = "role-scientist";
    public const string RoleEngineer = "role-engineer";
    public const string RoleTracker = "role-tracker";
    public const string RoleDiver = "role-diver";
    public const string RoleParade = "role-parade";
    public const string RoleMechanic = "role-mechanic";
    public const string RoleArtist = "role-artist";

    public const string GearSpaceSuit = "gear-space-suit";
    public const string GearScanner = "gear-scanner";
    public const string GearRepairBot = "gear-repair-bot";
    public const string GearLightCrystal = "gear-light-crystal";
    public const string GearMoonMap = "gear-moon-map";
    public const string GearShard = "gear-shard";
    public const string GearSeedPot = "gear-seed-pot";
    public const string GearLantern = "gear-lantern";
    public const string GearLeafCape = "gear-leaf-cape";
    public const string GearFlowerCrown = "gear-flower-crown";
    public const string GearStarCloak = "gear-star-cloak";
    public const string GearDrum = "gear-drum";
    public const string GearFlute = "gear-flute";
    public const string GearPaintBrush = "gear-paint-brush";
    public const string GearNewLamp = "gear-new-lamp";
    public const string GearStoryBook = "gear-story-book";
    public const string GearBadge = "gear-badge";

    /// <summary>
    /// Səhnə sətrində saxlanan NÖV açarları — baza sütunu ən çox 40 simvoldur.
    /// </summary>
    public const string BackdropSceneKey = "adventure-backdrop";
    public const string PortraitSceneKey = "hero-portrait";

    /// <summary>Sətir mərhələnin arxa fonu və ya obrazıdırmı — tapmaca rəsmi deyil.</summary>
    public static bool IsStageArt(string? sceneKey) =>
        sceneKey is BackdropSceneKey or PortraitSceneKey;

    /// <summary>
    /// Pet-in TƏSDİQLƏNMİŞ növləri. Sahə onsuz da bizim kataloqumuzdandır,
    /// amma müdafiə QATLIDIR: gözlənilməz dəyər prompta düşməməlidir.
    /// </summary>
    public static string ApprovedSpecies(string? species) => species switch
    {
        "fox" or "cat" or "dragon" or "owl" or "bunny" => species,
        _ => "fox"
    };

    /// <summary>Növün NƏZARƏTLİ rəngi — sərbəst mətn deyil.</summary>
    public static string ColorOf(string species) => species switch
    {
        "fox" => "warm-orange",
        "cat" => "soft-grey",
        "dragon" => "mint-green",
        "owl" => "dusk-blue",
        "bunny" => "cream-white",
        _ => "warm-orange"
    };
}
