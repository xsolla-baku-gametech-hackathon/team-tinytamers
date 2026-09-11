using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Scenes;

namespace PetPal.Api.PetBrain.Scenery;

/// <summary>
/// Macəranın VƏZİYYƏTİ — rəsm qatının gördüyü yeganə giriş.
///
/// <para>Burada nə uşaq, nə pet adı, nə də sərbəst mətn var: yalnız hekayənin
/// öz açarları. Direktor onları qapalı rəsm lüğətinə (<see cref="SceneryKeys"/>)
/// çevirir, yəni hekayə açarı heç vaxt birbaşa prompta düşmür.</para>
/// </summary>
public sealed record SceneryContext(
    string ExperienceKey,
    string Theme,

    /// <summary>Cari düyünün səhnə variantı; xətti macərada boş ola bilər.</summary>
    string SceneVariant,

    /// <summary>İndiyə qədər seçilmiş variant açarları.</summary>
    IReadOnlySet<string> Choices,

    /// <summary>Hekayə bayraqları — enerji, yol, kömək.</summary>
    IReadOnlySet<string> Flags,

    /// <summary>Macəradan sonra da yaşayan bayraqlar.</summary>
    IReadOnlySet<string> WorldFlags,

    /// <summary>İnventardakı əşya açarları.</summary>
    IReadOnlySet<string> Items,

    string Species,
    string Language)
{
    private static readonly HashSet<string> Empty = new(StringComparer.Ordinal);

    /// <summary>Xətti (chapter-siz) macəra üçün: yalnız seçimlər var.</summary>
    public static SceneryContext Linear(
        string experienceKey, string theme, string sceneVariant,
        IEnumerable<string> choices, string species, string language) =>
        new(experienceKey, theme, sceneVariant,
            new HashSet<string>(choices, StringComparer.Ordinal),
            Empty, Empty, Empty, species, language);

    public bool Has(string key) =>
        Choices.Contains(key) || Flags.Contains(key) || WorldFlags.Contains(key) || Items.Contains(key);
}

/// <summary>
/// Vəziyyəti ARXA FONA və OBRAZA çevirən qat.
///
/// <para>Saf funksiyadır: bazasız, saatsız, təsadüfsüz. Ona görə eyni vəziyyət
/// həmişə eyni təsviri, eyni təsvir isə eyni hash-ı verir — keş və idempotentlik
/// məhz bunun üzərində dayanır.</para>
///
/// <para><b>Dənəvərlik qəsdən kobuddur.</b> Rəsm hər düyün üçün deyil, MƏKAN
/// üçün çəkilir: eyni bazada beş düyün gəzmək bir fon deməkdir, işıq isə
/// seçimlə dəyişir. Beləliklə "seçdiyim şey dünyanı dəyişdi" hissi qalır, xərc
/// isə macəra boyu onlarla yox, bir neçə rəsmdir. Düyün səviyyəsindəki incə
/// fərqi onsuz da deterministik səhnə (<c>ExperienceScene</c>) daşıyır.</para>
/// </summary>
public static class AdventureSceneDirector
{
    public static AdventureBackdropSpec Backdrop(SceneryContext context)
    {
        var place = PlaceOf(context);
        var species = SceneryKeys.ApprovedSpecies(context.Species);

        return new AdventureBackdropSpec(
            ExperienceKey: context.ExperienceKey,
            Place: place,
            Light: LightOf(context, place),
            Palette: PaletteOf(context, place),
            Mood: MoodOf(context, place),
            SetPieces: SetPiecesOf(context, place),
            CompanionSpecies: species,
            CompanionColor: SceneryKeys.ColorOf(species),
            Language: context.Language);
    }

    public static HeroPortraitSpec Portrait(SceneryContext context)
    {
        var species = SceneryKeys.ApprovedSpecies(context.Species);

        return new HeroPortraitSpec(
            ExperienceKey: context.ExperienceKey,
            Role: RoleOf(context),
            Gear: GearOf(context),
            Palette: ThemePalette(context),
            Mood: ThemeMood(context),
            CompanionSpecies: species,
            CompanionColor: SceneryKeys.ColorOf(species),
            Language: context.Language);
    }

    private static string PlaceOf(SceneryContext context) =>
        MoonPlace(context.SceneVariant)
        ?? context.ExperienceKey switch
        {
            ExperienceCatalog.MarsRoverRescue =>
                context.Has("crater") ? SceneryKeys.MartianCrater
                : context.Has("mountain") ? SceneryKeys.MartianMountain
                : SceneryKeys.MartianCanyon,

            ExperienceCatalog.DragonLostColors =>
                context.Has("cloud-castle") ? SceneryKeys.CloudCastle
                : context.Has("flower-valley") ? SceneryKeys.FlowerValley
                : context.Has("crystal-cave") ? SceneryKeys.CrystalCave
                : SceneryKeys.CrystalGarden,

            ExperienceCatalog.OceanGlowQuest =>
                context.Has("kelp-forest") ? SceneryKeys.KelpForest
                : context.Has("open-water") ? SceneryKeys.OpenWater
                : SceneryKeys.CoralReef,

            ExperienceCatalog.ForestFriendsParade =>
                context.Has("river-bank") ? SceneryKeys.RiverBank
                : context.Has("old-oak") ? SceneryKeys.OldOak
                : context.Has("hilltop") ? SceneryKeys.Hilltop
                : SceneryKeys.ForestClearing,

            ExperienceCatalog.RobotLabPuzzle => SceneryKeys.RobotWorkshop,

            ExperienceCatalog.MoonCrystalRescue or ExperienceCatalog.MoonCrystalSecret =>
                SceneryKeys.MoonPlain,

            _ => ThemePlace(context.Theme)
        };

    /// <summary>
    /// Ay macəralarının səhnə variantını MƏKANA yığır.
    ///
    /// <para>Variant → ailə uyğunluğu PAYLAŞILAN müqavilədədir
    /// (<see cref="MoonSceneVariants"/>), çünki eyni cədvələ ekranın
    /// deterministik səhnəsi də baxır. İki nüsxə saxlamaq o deməkdir ki, yeni
    /// hekayə açarı bir tərəfdə tanınıb digərində səssizcə itə bilər.</para>
    ///
    /// <para>Naməlum variant <c>null</c> qaytarır və qərar təcrübə açarına keçir.</para>
    /// </summary>
    private static string? MoonPlace(string sceneVariant) =>
        MoonSceneVariants.Knows(sceneVariant)
            ? PlaceOfFamily(MoonSceneVariants.FamilyOf(sceneVariant))
            : null;

    private static string PlaceOfFamily(MoonSceneFamily family) => family switch
    {
        MoonSceneFamily.EarthNight => SceneryKeys.NightWindow,
        MoonSceneFamily.EarthDesk => SceneryKeys.SignalDesk,
        MoonSceneFamily.KitTable => SceneryKeys.KitTable,
        MoonSceneFamily.Launch => SceneryKeys.LaunchField,
        MoonSceneFamily.Arrival => SceneryKeys.MoonBaseOutside,
        MoonSceneFamily.BaseInside => SceneryKeys.MoonBaseInside,
        MoonSceneFamily.Fork => SceneryKeys.MoonFork,
        MoonSceneFamily.Cave => SceneryKeys.CrystalCave,
        MoonSceneFamily.Crater => SceneryKeys.ShadowedCrater,
        MoonSceneFamily.Passage => SceneryKeys.HiddenPassage,
        MoonSceneFamily.RoverSite => SceneryKeys.RoverSite,
        MoonSceneFamily.Garden => SceneryKeys.MoonGarden,
        MoonSceneFamily.Observatory => SceneryKeys.ObservatoryHall,
        MoonSceneFamily.Core => SceneryKeys.ObservatoryCore,
        MoonSceneFamily.Finale => SceneryKeys.MoonlightReturn,
        _ => SceneryKeys.MoonPlain
    };

    private static string ThemePlace(string theme) => theme switch
    {
        TraitKeys.Space => SceneryKeys.MoonPlain,
        TraitKeys.Fantasy => SceneryKeys.CrystalGarden,
        TraitKeys.Animals or TraitKeys.Nature => SceneryKeys.ForestClearing,
        TraitKeys.Science => SceneryKeys.RobotWorkshop,
        _ => SceneryKeys.ForestClearing
    };

    /// <summary>
    /// İşıq SEÇİMİN nəticəsidir: bazaya enerji verildisə panellər yanır, çantada
    /// işıq kristalı varsa qaranlıq yol işıqlanır, kristal bərpa olunubsa dünya
    /// parlayır.
    /// </summary>
    private static string LightOf(SceneryContext context, string place) => place switch
    {
        SceneryKeys.MoonBaseInside or SceneryKeys.MoonBaseOutside =>
            BaseAwake(context) ? SceneryKeys.LightAwake : SceneryKeys.LightStandby,

        SceneryKeys.CrystalCave or SceneryKeys.ShadowedCrater or SceneryKeys.HiddenPassage
            or SceneryKeys.MoonFork or SceneryKeys.RoverSite or SceneryKeys.MoonPlain =>
            CarriesLight(context) ? SceneryKeys.LightLantern : SceneryKeys.LightStar,

        SceneryKeys.ObservatoryCore or SceneryKeys.MoonlightReturn => SceneryKeys.LightCrystal,
        SceneryKeys.ObservatoryHall or SceneryKeys.MoonGarden => SceneryKeys.LightStar,
        SceneryKeys.NightWindow => SceneryKeys.LightStar,
        SceneryKeys.SignalDesk or SceneryKeys.KitTable => SceneryKeys.LightAwake,
        SceneryKeys.LaunchField or SceneryKeys.CloudCastle or SceneryKeys.Hilltop => SceneryKeys.LightDawn,
        SceneryKeys.CrystalGarden => SceneryKeys.LightCrystal,
        _ => SceneryKeys.LightDaylight
    };

    private static bool BaseAwake(SceneryContext context) =>
        context.Has(MoonKeys.WorldBaseAwake) ||
        context.Has(MoonKeys.FlagMapPowered) ||
        context.Has(MoonKeys.FlagCommsPowered) ||
        context.Has(MoonKeys.FlagRepairPowered);

    private static bool CarriesLight(SceneryContext context) =>
        context.Has(MoonKeys.ToolLightCrystal) || context.Has("lantern-trail");

    private static string PaletteOf(SceneryContext context, string place) =>
        place is SceneryKeys.ObservatoryCore or SceneryKeys.MoonlightReturn
            ? SceneryKeys.PaletteViolet
            : ThemePalette(context);

    private static string ThemePalette(SceneryContext context) => context.ExperienceKey switch
    {
        ExperienceCatalog.MarsRoverRescue => SceneryKeys.PaletteWarmOrange,
        ExperienceCatalog.DragonLostColors => DragonPalette(context),
        ExperienceCatalog.OceanGlowQuest => SceneryKeys.PaletteTurquoise,
        ExperienceCatalog.ForestFriendsParade => SceneryKeys.PaletteLeafGreen,
        ExperienceCatalog.RobotLabPuzzle => SceneryKeys.PaletteMintSilver,
        _ => SceneryKeys.PaletteSilverBlue
    };

    /// <summary>Əjdaha macərasında palitranı uşaq ÖZÜ seçir — fon da onu geyinir.</summary>
    private static string DragonPalette(SceneryContext context) =>
        context.Has(SceneryKeys.PaletteSunset) ? SceneryKeys.PaletteSunset
        : context.Has(SceneryKeys.PaletteOcean) ? SceneryKeys.PaletteOcean
        : context.Has(SceneryKeys.PaletteForest) ? SceneryKeys.PaletteForest
        : context.Has(SceneryKeys.PaletteBerry) ? SceneryKeys.PaletteBerry
        : SceneryKeys.PaletteViolet;

    private static string MoodOf(SceneryContext context, string place) => place switch
    {
        SceneryKeys.MoonlightReturn => SceneryKeys.MoodProud,
        SceneryKeys.MoonBaseInside or SceneryKeys.ShadowedCrater or SceneryKeys.HiddenPassage
            => SceneryKeys.MoodCalmWonder,
        _ => ThemeMood(context)
    };

    private static string ThemeMood(SceneryContext context) => context.ExperienceKey switch
    {
        ExperienceCatalog.MarsRoverRescue => SceneryKeys.MoodHopeful,
        ExperienceCatalog.DragonLostColors => SceneryKeys.MoodGentle,
        ExperienceCatalog.OceanGlowQuest => SceneryKeys.MoodGentle,
        ExperienceCatalog.ForestFriendsParade => SceneryKeys.MoodPlayful,
        ExperienceCatalog.RobotLabPuzzle => SceneryKeys.MoodBright,
        _ => SceneryKeys.MoodCalmWonder
    };

    /// <summary>
    /// Seçimlərin GÖRÜNƏN izi. Sıra sabitdir və say məhduddur — eyni vəziyyət
    /// həmişə eyni siyahını verməlidir, yoxsa hash dəyişər və rəsm təkrar
    /// çəkilərdi.
    /// </summary>
    private static IReadOnlyList<string> SetPiecesOf(SceneryContext context, string place)
    {
        var pieces = new List<string>();

        switch (place)
        {
            case SceneryKeys.MoonBaseInside:
                if (context.Has(MoonKeys.FlagMapPowered)) pieces.Add(SceneryKeys.PieceGlowingMap);
                if (context.Has(MoonKeys.FlagCommsPowered)) pieces.Add(SceneryKeys.PieceLiveAntenna);
                if (context.Has(MoonKeys.FlagRepairPowered)) pieces.Add(SceneryKeys.PieceRepairBay);
                if (context.Has("bot-fix")) pieces.Add(SceneryKeys.PieceServiceBot);
                break;

            case SceneryKeys.MoonBaseOutside or SceneryKeys.MoonPlain or SceneryKeys.MoonFork:
                pieces.Add(SceneryKeys.PieceLander);
                pieces.Add(SceneryKeys.PieceRoundedRocks);
                break;

            case SceneryKeys.CrystalCave:
                pieces.Add(SceneryKeys.PieceCrystalBlooms);
                if (context.Has("cave-help")) pieces.Add(SceneryKeys.PieceRopeBridge);
                break;

            case SceneryKeys.ShadowedCrater:
                pieces.Add(SceneryKeys.PieceLongShadows);
                pieces.Add(SceneryKeys.PieceMirrorField);
                break;

            case SceneryKeys.RoverSite:
                pieces.Add(context.Has(MoonKeys.WorldRoverAwake) || context.Has(MoonKeys.FlagRoverHelps)
                    ? SceneryKeys.PieceFriendlyRover
                    : SceneryKeys.PieceSleepingRover);
                break;

            case SceneryKeys.MoonGarden:
                pieces.Add(SceneryKeys.PieceGardenShoots);
                break;

            case SceneryKeys.ObservatoryHall or SceneryKeys.ObservatoryCore or SceneryKeys.MoonlightReturn:
                pieces.Add(SceneryKeys.PieceObservatoryRing);
                if (HoldsShards(context)) pieces.Add(SceneryKeys.PieceCollectedShards);
                if (context.Has(MoonKeys.WorldGardenAlive)) pieces.Add(SceneryKeys.PieceGardenShoots);
                break;

            case SceneryKeys.MartianCanyon or SceneryKeys.MartianCrater or SceneryKeys.MartianMountain:
                pieces.Add(SceneryKeys.PieceLander);
                if (context.Has("solar-panel")) pieces.Add(SceneryKeys.PieceSolarArray);
                if (context.Has("battery")) pieces.Add(SceneryKeys.PieceSpareBattery);
                if (context.Has("carry-to-ship")) pieces.Add(SceneryKeys.PieceReturnShip);
                break;

            case SceneryKeys.CoralReef or SceneryKeys.KelpForest or SceneryKeys.OpenWater:
                pieces.Add(SceneryKeys.PieceGlowingFish);
                if (context.Has("lantern-trail")) pieces.Add(SceneryKeys.PieceLanternTrail);
                pieces.Add(SceneryKeys.PiecePearlShells);
                break;

            case SceneryKeys.ForestClearing or SceneryKeys.RiverBank or SceneryKeys.OldOak or SceneryKeys.Hilltop:
                pieces.Add(SceneryKeys.PieceForestFriends);
                pieces.Add(SceneryKeys.PieceFlowerGarlands);
                if (context.Has("drum-beat")) pieces.Add(SceneryKeys.PieceDrumCircle);
                pieces.Add(SceneryKeys.PieceMushroomHouses);
                break;

            case SceneryKeys.RobotWorkshop:
                pieces.Add(SceneryKeys.PieceGearShelves);
                pieces.Add(SceneryKeys.PieceWorkshopLamp);
                break;

            case SceneryKeys.CrystalGarden or SceneryKeys.CloudCastle or SceneryKeys.FlowerValley:
                pieces.Add(SceneryKeys.PieceFloatingMotes);
                break;
        }

        return pieces.Count > AdventureBackdropSpec.MaxSetPieces
            ? pieces.GetRange(0, AdventureBackdropSpec.MaxSetPieces)
            : pieces;
    }

    private static bool HoldsShards(SceneryContext context) =>
        context.Has(MoonKeys.ItemShardOne) ||
        context.Has(MoonKeys.ItemShardTwo) ||
        context.Has(MoonKeys.ItemShardThree);

    private static string RoleOf(SceneryContext context)
    {
        if (context.Has(MoonKeys.RoleScientist)) return SceneryKeys.RoleScientist;
        if (context.Has(MoonKeys.RoleEngineer)) return SceneryKeys.RoleEngineer;
        if (context.Has(MoonKeys.RoleTracker)) return SceneryKeys.RoleTracker;

        return context.ExperienceKey switch
        {
            ExperienceCatalog.OceanGlowQuest => SceneryKeys.RoleDiver,
            ExperienceCatalog.ForestFriendsParade => SceneryKeys.RoleParade,
            ExperienceCatalog.DragonLostColors => SceneryKeys.RoleArtist,
            ExperienceCatalog.RobotLabPuzzle =>
                context.Has("paint-nozzle") ? SceneryKeys.RoleArtist : SceneryKeys.RoleMechanic,
            _ => SceneryKeys.RoleExplorer
        };
    }

    /// <summary>
    /// Obrazın daşıdıqları — YALNIZ geyinilən dəst.
    ///
    /// <para>Hər əşya bura düşmür: kristal parçası kimi tez-tez dəyişən inventar
    /// obrazı hər addımda yenidən çəkdirərdi. Burada uşağın <b>qərar verdiyi</b>
    /// şeylər var: çantaya seçdiyi alətlər, enerji qərarının verdiyi xəritə,
    /// karnaval geyimi, alət seçimi. Sıra sabitdir — hash seçim sırasından
    /// asılı olmamalıdır.</para>
    /// </summary>
    private static IReadOnlyList<string> GearOf(SceneryContext context)
    {
        var gear = new List<string>();

        if (IsSpace(context))
            gear.Add(SceneryKeys.GearSpaceSuit);

        if (context.Has(MoonKeys.ToolScanner)) gear.Add(SceneryKeys.GearScanner);
        if (context.Has(MoonKeys.ToolRepairBot)) gear.Add(SceneryKeys.GearRepairBot);
        if (context.Has(MoonKeys.ToolLightCrystal)) gear.Add(SceneryKeys.GearLightCrystal);
        if (context.Has(MoonKeys.ItemMoonMap)) gear.Add(SceneryKeys.GearMoonMap);

        if (context.Has("leaf-capes")) gear.Add(SceneryKeys.GearLeafCape);
        if (context.Has("flower-crowns")) gear.Add(SceneryKeys.GearFlowerCrown);
        if (context.Has("star-cloaks")) gear.Add(SceneryKeys.GearStarCloak);
        if (context.Has("drum-beat")) gear.Add(SceneryKeys.GearDrum);
        if (context.Has("flute-song")) gear.Add(SceneryKeys.GearFlute);

        if (context.Has("lantern-trail")) gear.Add(SceneryKeys.GearLantern);
        if (context.Has("paint-nozzle")) gear.Add(SceneryKeys.GearPaintBrush);
        if (context.Has("new-lamp")) gear.Add(SceneryKeys.GearNewLamp);
        if (context.Has("story-book")) gear.Add(SceneryKeys.GearStoryBook);
        if (context.Has("painted-badge")) gear.Add(SceneryKeys.GearBadge);

        return gear.Count > HeroPortraitSpec.MaxGear
            ? gear.GetRange(0, HeroPortraitSpec.MaxGear)
            : gear;
    }

    private static bool IsSpace(SceneryContext context) =>
        context.ExperienceKey is ExperienceCatalog.MarsRoverRescue
            or ExperienceCatalog.MoonCrystalRescue
            or ExperienceCatalog.MoonCrystalSecret ||
        string.Equals(context.Theme, TraitKeys.Space, StringComparison.Ordinal);
}
