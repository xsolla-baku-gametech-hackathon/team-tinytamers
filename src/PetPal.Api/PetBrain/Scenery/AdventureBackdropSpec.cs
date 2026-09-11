using System.Security.Cryptography;
using System.Text;
using PetPal.Api.Common;

namespace PetPal.Api.PetBrain.Scenery;

/// <summary>
/// Macəranın ARXA FONU — uşağın seçimlərinin qurduğu məkan.
///
/// <para>Tapmaca səhnəsindən fərqi məqsədindədir: tapmaca səhnəsi bir lövhənin
/// altındakı kadrdır, bu isə <b>bütün mərhələnin dünyasıdır</b>. Ona görə
/// kompozisiya boşdur (yuxarı və aşağı üçdəbir sakit qalır), yoldaş isə uzaqda
/// və kiçikdir — mətn, seçim düymələri və HUD onun üstündə oxunmalıdır.</para>
///
/// <para><b>Burada uşağa aid heç nə yoxdur:</b> nə ad, nə id, nə şəkil, nə
/// söhbət, nə xatirə mətni. Yalnız qapalı lüğətdən (<see cref="SceneryKeys"/>)
/// gələn açarlar var — göndəriləsi sahə ümumiyyətlə mövcud deyil.</para>
///
/// <para><b>Dənəvərlik xərc qərarıdır.</b> Açar hər düyün üçün deyil, MƏKAN və
/// seçimlərin izi üçündür: eyni fəsildə on düyün gəzmək bir arxa fon deməkdir.
/// Hash uşaqdan asılı olmadığına görə eyni yolu gedən bütün uşaqlar eyni rəsmi
/// paylaşır — ikinci pullu sorğu getmir.</para>
/// </summary>
public sealed record AdventureBackdropSpec(
    /// <summary>Təcrübə açarı — <c>moon-crystal-secret</c>.</summary>
    string ExperienceKey,

    /// <summary>Məkan açarı — <see cref="SceneryKeys"/>-dən.</summary>
    string Place,

    /// <summary>İşıq vəziyyəti: seçim bazanı oyatdısa fon da oyanır.</summary>
    string Light,

    string Palette,

    string Mood,

    /// <summary>Səhnədə görünən, SEÇİMLƏRDƏN gələn təsdiqlənmiş obyektlər.</summary>
    IReadOnlyList<string> SetPieces,

    string CompanionSpecies,
    string CompanionColor,

    /// <summary>Uşağın dili — yalnız alt mətn üçün, prompta DÜŞMÜR.</summary>
    string Language) : IStoryScene
{
    /// <summary>Bir arxa fonda ən çox neçə obyekt — prompt uzanıb dağılmasın.</summary>
    public const int MaxSetPieces = 4;

    /// <summary>Sahə ayırıcısı — PuzzleSeed ilə eyni prinsip (0x1F).</summary>
    private const char Separator = (char)0x1F;

    public string SceneKey => SceneryKeys.BackdropSceneKey;

    public int PromptVersion => SafeSceneryPromptBuilder.TemplateVersion;

    public string BuildPrompt() => SafeSceneryPromptBuilder.Backdrop(this);

    /// <summary>
    /// Kanonik hash.
    ///
    /// <para>Dil QƏSDƏN kənardadır: rəsmin içində mətn yoxdur, deməli
    /// azərbaycanca və ingiliscə eyni fondur.</para>
    /// </summary>
    public string Hash()
    {
        var canonical = string.Join(Separator,
        [
            "v" + PromptVersion,
            SceneKey,
            ExperienceKey,
            Place,
            Light,
            Palette,
            Mood,
            string.Join(',', SetPieces),
            CompanionSpecies,
            CompanionColor,
            SafeSceneryPromptBuilder.AspectRatio
        ]);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// Ekran oxuyucusu üçün mətn — NƏZARƏTLİ şablondan, modelin çıxışından yox.
    /// </summary>
    public string AltText() => Place switch
    {
        SceneryKeys.NightWindow => Localized.T(Language,
            "Gecə pəncərəsi: ulduzlu səma və Ay.",
            "A night window with a starry sky and the Moon."),
        SceneryKeys.SignalDesk => Localized.T(Language,
            "Ev laboratoriyası: kiçik antena və isti lampa işığı.",
            "A home science desk with a small antenna and warm lamplight."),
        SceneryKeys.KitTable => Localized.T(Language,
            "Masanın üstündə səfər alətləri düzülüb.",
            "Expedition tools laid out on a table."),
        SceneryKeys.LaunchField => Localized.T(Language,
            "Yaşıl meydanda kiçik raket uçuşa hazırdır.",
            "A small rocket ready to launch on a green field."),
        SceneryKeys.MoonPlain => Localized.T(Language,
            "Geniş Ay krateri və ulduzlu qara səma.",
            "A wide Moon crater under a black starry sky."),
        SceneryKeys.MoonBaseOutside => Localized.T(Language,
            "Ay düzündə kiçik günbəzli baza.",
            "A small domed base on the lunar plain."),
        SceneryKeys.MoonBaseInside => Localized.T(Language,
            "Ay bazasının yumru dəhlizi.",
            "A rounded corridor inside the Moon base."),
        SceneryKeys.MoonFork => Localized.T(Language,
            "İki yolun ayrıldığı Ay təpəsi: mağara və krater.",
            "A lunar ridge where the cave path and the crater path split."),
        SceneryKeys.CrystalCave => Localized.T(Language,
            "Parlayan kristal mağarası.",
            "A glowing crystal cave."),
        SceneryKeys.ShadowedCrater => Localized.T(Language,
            "Kölgəli dərin Ay krateri.",
            "A deep shadowed Moon crater."),
        SceneryKeys.HiddenPassage => Localized.T(Language,
            "Dar gizli keçid və qabaqda yumşaq işıq.",
            "A narrow hidden passage with a soft glow ahead."),
        SceneryKeys.RoverSite => Localized.T(Language,
            "İzlərin apardığı yerdə dayanmış rover.",
            "A stranded rover at the end of the tracks."),
        SceneryKeys.MoonGarden => Localized.T(Language,
            "Ayda kiçik bağça günbəzi və yaşıl cücərtilər.",
            "A small garden dome on the Moon with green shoots."),
        SceneryKeys.ObservatoryHall => Localized.T(Language,
            "Ulduzlara baxan geniş pəncərəli rəsədxana.",
            "An observatory hall with a wide window onto the stars."),
        SceneryKeys.ObservatoryCore => Localized.T(Language,
            "Rəsədxananın mərkəzi: parçaları gözləyən yuvalar.",
            "The heart of the observatory, sockets waiting for the shards."),
        SceneryKeys.MoonlightReturn => Localized.T(Language,
            "Ay işığı yenidən düzənliyə yayılır.",
            "Moonlight spreading across the plain again."),
        SceneryKeys.MartianCanyon => Localized.T(Language,
            "Tozlu narıncı səma altında Mars kanyonu.",
            "A Martian canyon under a dusty orange sky."),
        SceneryKeys.MartianCrater => Localized.T(Language,
            "Mars krateri — dərin və yumru.",
            "A deep rounded Martian crater."),
        SceneryKeys.MartianMountain => Localized.T(Language,
            "Küləkli Mars dağı.",
            "A windy Martian mountain ridge."),
        SceneryKeys.CloudCastle => Localized.T(Language,
            "Buludlardan qurulmuş qala.",
            "A castle built of soft clouds."),
        SceneryKeys.FlowerValley => Localized.T(Language,
            "Nəhəng çiçəklərlə dolu vadi.",
            "A valley full of oversized flowers."),
        SceneryKeys.CrystalGarden => Localized.T(Language,
            "Alaqaranlıqda parlayan kristal bağça.",
            "A crystal garden glowing at dusk."),
        SceneryKeys.CoralReef => Localized.T(Language,
            "Günəşli mərcan rifi.",
            "A sunlit coral reef."),
        SceneryKeys.KelpForest => Localized.T(Language,
            "Yüksək dəniz otu meşəsi və işıq zolaqları.",
            "A tall kelp forest with beams of light."),
        SceneryKeys.OpenWater => Localized.T(Language,
            "Açıq mavi su və dərinliyə düşən işıq.",
            "Open blue water with light reaching into the deep."),
        SceneryKeys.ForestClearing => Localized.T(Language,
            "Günəşli meşə talası və dolanbac cığır.",
            "A sunny forest clearing with a winding path."),
        SceneryKeys.RiverBank => Localized.T(Language,
            "Sakit çay sahili və hamar daşlar.",
            "A calm river bank with smooth stones."),
        SceneryKeys.OldOak => Localized.T(Language,
            "Kökündə oyuğu olan qoca palıd.",
            "An old oak with a hollow at its base."),
        SceneryKeys.Hilltop => Localized.T(Language,
            "Meşəyə baxan otlu təpə.",
            "A grassy hilltop looking over the forest."),
        SceneryKeys.RobotWorkshop => Localized.T(Language,
            "İşıqlı emalatxana və yumru maşınlar.",
            "A bright workshop with rounded machines."),
        _ => Localized.T(Language, "Macəranın səhnəsi.", "A scene from the adventure.")
    };
}
