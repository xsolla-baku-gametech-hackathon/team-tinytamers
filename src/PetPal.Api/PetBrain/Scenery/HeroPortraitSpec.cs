using System.Security.Cryptography;
using System.Text;
using PetPal.Api.Common;

namespace PetPal.Api.PetBrain.Scenery;

/// <summary>
/// Macəranın OBRAZI — uşağın seçimlərinin geyindirdiyi yoldaş.
///
/// <para><b>Obraz uşağın özü DEYİL, pet-idir.</b> Bu, bəzək qərarı deyil,
/// təhlükəsizlik qərarıdır: rəsm modelindən heç vaxt uşaq portreti istənmir və
/// promptun dəyişməz bəndi bunu açıq qadağan edir. Uşağın seçimləri (rol,
/// çanta, yol) yoldaşın görünüşünə keçir — ekranda "bunu mən seçdim" hissini
/// daşıyan məhz odur.</para>
///
/// <para>Açar yalnız <b>obrazı dəyişən</b> seçimlərdən qurulur: rol, geyim və
/// daşınan alətlər. Ona görə macəra boyu obraz bir neçə dəfə yenilənir, hər
/// düyündə yox — həm hekayə baxımından doğrudur, həm də xərc sərhədlidir.</para>
/// </summary>
public sealed record HeroPortraitSpec(
    /// <summary>Təcrübə açarı — eyni rol fərqli macərada fərqli geyimdir.</summary>
    string ExperienceKey,

    /// <summary>Rol açarı — <see cref="SceneryKeys"/>-dən.</summary>
    string Role,

    /// <summary>Daşınan təsdiqlənmiş əşyalar — SIRALANMIŞ, yəni seçim sırası hash-ı dəyişmir.</summary>
    IReadOnlyList<string> Gear,

    string Palette,

    string Mood,

    string CompanionSpecies,
    string CompanionColor,

    /// <summary>Uşağın dili — yalnız alt mətn üçün, prompta DÜŞMÜR.</summary>
    string Language) : IStoryScene
{
    /// <summary>Obrazda ən çox neçə əşya görünür — portret dağılmasın.</summary>
    public const int MaxGear = 3;

    private const char Separator = (char)0x1F;

    public string SceneKey => SceneryKeys.PortraitSceneKey;

    public int PromptVersion => SafeSceneryPromptBuilder.TemplateVersion;

    public string BuildPrompt() => SafeSceneryPromptBuilder.Portrait(this);

    public string Hash()
    {
        var canonical = string.Join(Separator,
        [
            "v" + PromptVersion,
            SceneKey,
            ExperienceKey,
            Role,
            string.Join(',', Gear),
            Palette,
            Mood,
            CompanionSpecies,
            CompanionColor,
            SafeSceneryPromptBuilder.AspectRatio
        ]);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// Ekran oxuyucusu üçün mətn: rol + daşınan əşyalar, uşağın dilində.
    /// Modelin çıxışından DEYİL — nəzarətli şablondan qurulur.
    /// </summary>
    public string AltText()
    {
        var role = RoleText();

        if (Gear.Count == 0)
            return role;

        var carried = string.Join(", ", Gear.Select(GearText));

        return Localized.T(Language, $"{role} Yanında: {carried}.", $"{role} Carrying: {carried}.");
    }

    private string RoleText() => Role switch
    {
        SceneryKeys.RoleScientist => Localized.T(Language,
            "Yoldaşın alim geyimindədir.", "Your companion is dressed as a scientist."),
        SceneryKeys.RoleEngineer => Localized.T(Language,
            "Yoldaşın mühəndis geyimindədir.", "Your companion is dressed as an engineer."),
        SceneryKeys.RoleTracker => Localized.T(Language,
            "Yoldaşın izçi geyimindədir.", "Your companion is dressed as a tracker."),
        SceneryKeys.RoleDiver => Localized.T(Language,
            "Yoldaşın dalğıc geyimindədir.", "Your companion is dressed for a dive."),
        SceneryKeys.RoleParade => Localized.T(Language,
            "Yoldaşın karnaval geyimindədir.", "Your companion is dressed for the parade."),
        SceneryKeys.RoleMechanic => Localized.T(Language,
            "Yoldaşın emalatxana önlüyündədir.", "Your companion is wearing a workshop apron."),
        SceneryKeys.RoleArtist => Localized.T(Language,
            "Yoldaşın rəssam geyimindədir.", "Your companion is dressed as an artist."),
        _ => Localized.T(Language,
            "Yoldaşın səfər çantası ilə hazırdır.", "Your companion is ready with an explorer backpack.")
    };

    private string GearText(string key) => key switch
    {
        SceneryKeys.GearSpaceSuit => Localized.T(Language, "kosmik skafandr", "a space suit"),
        SceneryKeys.GearScanner => Localized.T(Language, "skaner", "a scanner"),
        SceneryKeys.GearRepairBot => Localized.T(Language, "kiçik təmir robotu", "a little repair bot"),
        SceneryKeys.GearLightCrystal => Localized.T(Language, "işıq kristalı", "a light crystal"),
        SceneryKeys.GearMoonMap => Localized.T(Language, "Ay xəritəsi", "a moon map"),
        SceneryKeys.GearShard => Localized.T(Language, "kristal parçası", "a crystal shard"),
        SceneryKeys.GearSeedPot => Localized.T(Language, "cücərti dibçəyi", "a pot with a shoot"),
        SceneryKeys.GearLantern => Localized.T(Language, "fənər", "a lantern"),
        SceneryKeys.GearLeafCape => Localized.T(Language, "yarpaq plaşı", "a leaf cape"),
        SceneryKeys.GearFlowerCrown => Localized.T(Language, "çiçək tacı", "a flower crown"),
        SceneryKeys.GearStarCloak => Localized.T(Language, "ulduzlu plaş", "a star cloak"),
        SceneryKeys.GearDrum => Localized.T(Language, "təbil", "a drum"),
        SceneryKeys.GearFlute => Localized.T(Language, "tütək", "a flute"),
        SceneryKeys.GearPaintBrush => Localized.T(Language, "fırça", "a paint brush"),
        SceneryKeys.GearNewLamp => Localized.T(Language, "yeni lampa", "a new lamp"),
        SceneryKeys.GearStoryBook => Localized.T(Language, "nağıl kitabı", "a storybook"),
        SceneryKeys.GearBadge => Localized.T(Language, "nişan", "a badge"),
        _ => Localized.T(Language, "kiçik yadigar", "a small keepsake")
    };
}
