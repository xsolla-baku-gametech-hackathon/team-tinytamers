using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>Bir seçimin xassəyə təsiri. Cədvəl qapalıdır — klient delta göndərə bilmir.</summary>
public sealed record TraitDelta(string Key, int Delta);

/// <summary>
/// Mərhələdəki bir variant. Açar SABİTDİR: server yalnız bu açarları qəbul edir,
/// naməlum açar <c>400</c> alır.
/// </summary>
public sealed record ExperienceOption(
    string Key,
    string LabelAz,
    string LabelEn,
    string Icon,
    string DetailAz,
    string DetailEn,
    IReadOnlyList<TraitDelta> Traits)
{
    public string Label(string language) => Localized.T(language, LabelAz, LabelEn);
    public string Detail(string language) => Localized.T(language, DetailAz, DetailEn);
}

public sealed record ExperienceStage(
    PetBrainStageKind Kind,
    string PromptAz,
    string PromptEn,
    string PetLineAz,
    string PetLineEn,
    IReadOnlyList<ExperienceOption> Options)
{
    public string Prompt(string language) => Localized.T(language, PromptAz, PromptEn);
    public string PetLine(string language) => Localized.T(language, PetLineAz, PetLineEn);
}

/// <summary>
/// Bir təcrübə şablonu. Mərhələlər MƏLUMATDIR, kod deyil — yəni yeni macərə
/// əlavə etmək üçün UI-a toxunmaq lazım gəlmir və direktorun namizəd hovuzu
/// süni şəkildə iki elementə daralmır.
/// </summary>
public sealed record ExperienceTemplate(
    string Key,

    /// <summary>
    /// Tərifin versiyası. Mərhələ sayı, variant açarları və ya qiymətləndirmə
    /// dəyişəndə ARTIRILIR.
    ///
    /// <para>Yarımçıq run başladığı versiyanı yadda saxlayır (bax
    /// <see cref="Entities.ExperienceRun.DefinitionVersion"/>), yəni deploy
    /// zamanı uşağın açıq macərası qəflətən başqa qaydalarla qiymətləndirilmir.</para>
    /// </summary>
    int Version,

    PetBrainExperienceType Type,
    string Theme,
    string ActivityType,

    /// <summary>UI-ın hansı səhnəni çəkəcəyi: <c>mars</c>, <c>dragon</c> və ya ümumi mövzu açarı.</summary>
    string SceneKey,
    string Icon,
    int TargetMinutes,

    /// <summary>Yaş həddi — TƏHLÜKƏSİZLİK sərhədidir, uşağın zəkası haqqında iddia deyil.</summary>
    int MinAge,
    string TitleAz,
    string TitleEn,
    string IntroAz,
    string IntroEn,
    string CelebrationAz,
    string CelebrationEn,

    /// <summary>İlk tamamlamada açılan kosmetik; boş = kosmetik yoxdur.</summary>
    string RewardCode,
    int XpReward,
    int BondReward,

    /// <summary>Birincisi ƏSAS maraqdır — tamamlama onu ən çox qaldırır.</summary>
    IReadOnlyList<string> InterestAffinity,
    IReadOnlyList<string> PlayStyleAffinity,
    IReadOnlyList<ExperienceStage> Stages)
{
    /// <summary>
    /// Bu macəranın işlətdiyi oyun MEXANİKALARI (<see cref="MechanicKeys"/>).
    ///
    /// <para>Mövzu affinitetindən qəsdən ayrıdır: uşaq kosmosu sevib marşrut
    /// tapmacasını sevməyə bilər. Direktor ikisini AYRI çəkilərlə oxuyur, ona
    /// görə «kosmos, amma bu dəfə qurma» kimi təklif mümkün olur.</para>
    ///
    /// <para>Boş buraxıla bilər — o halda mexanika uyğunluğu neytral sayılır
    /// və heç bir namizəd haqsız yerə irəli çıxmır.</para>
    /// </summary>
    public IReadOnlyList<string> MechanicAffinity { get; init; } = [];

    /// <summary>Ən çox işlədilən mexanika — izah cümləsi bunu adlandırır.</summary>
    public string? PrimaryMechanic => MechanicAffinity.Count > 0 ? MechanicAffinity[0] : null;

    /// <summary>
    /// Tamamlamanın uşağa NƏ verdiyi — mükafatın forması.
    ///
    /// <para>Mükafat İQTİSADİYYATI bununla dəyişmir: xp, bağ və kosmetik
    /// olduğu kimi qalır. Bu sahə mükafatın hansı ADLA təqdim olunduğunu və
    /// uşağın seçdiyi növün sıralamada irəli çıxmasını idarə edir.</para>
    ///
    /// <para>Elan dürüst olmalıdır: kosmetik açarı olmayan macəra
    /// <see cref="PetBrainRewardPreference.PetCosmetic"/> elan edə bilməz —
    /// testlə qorunur.</para>
    /// </summary>
    public PetBrainRewardPreference RewardFlavor { get; init; } = PetBrainRewardPreference.StoryPage;

    public string Title(string language) => Localized.T(language, TitleAz, TitleEn);
    public string Intro(string language) => Localized.T(language, IntroAz, IntroEn);
    public string Celebration(string language) => Localized.T(language, CelebrationAz, CelebrationEn);

    public string PrimaryInterest => InterestAffinity.Count > 0 ? InterestAffinity[0] : TraitKeys.Puzzles;

    /// <summary>Mərhələ sayı — tamamlama yalnız hamısı keçiləndən sonra mümkündür.</summary>
    public int StageCount => Stages.Count;

    public bool HasPuzzle => Stages.Any(s => s.Kind == PetBrainStageKind.Puzzle);
}

/// <summary>
/// Versiyalı tərif axtarışının nəticəsi.
/// </summary>
/// <param name="Template">Tapılan tərif; kataloqda ümumiyyətlə yoxdursa <c>null</c>.</param>
/// <param name="Exact">
/// Run-ın başladığı versiya ilə cari tərif eynidirmi. <c>false</c> olanda tərif
/// oxunur, amma çağıran onu «köhnəlmiş» sayır.
/// </param>
public sealed record ExperienceLookup(ExperienceTemplate? Template, bool Exact);

/// <summary>
/// Təsdiqlənmiş təcrübə kataloqu.
///
/// <para>Direktor YALNIZ buradan seçir. Kataloqdakı hər şablon tam oynanandır —
/// "tövsiyə olunur, amma açılmır" halı qəsdən yoxdur, çünki uşağa verilən vəd
/// pozulmamalıdır.</para>
/// </summary>
public static class ExperienceCatalog
{
    public const string MarsRoverRescue = "mars-rover-rescue";
    public const string DragonLostColors = "dragon-lost-colors";
    public const string MoonCrystalRescue = "moon-crystal-rescue";
    public const string OceanGlowQuest = "ocean-glow-quest";
    public const string ForestFriendsParade = "forest-friends-parade";
    public const string RobotLabPuzzle = "robot-lab-puzzle";

    /// <summary>Kosmetik mükafat kodları — <c>PetAccessories</c> kataloqu ilə eyni olmalıdır.</summary>
    public const string HelmetMars = "helmet-mars";
    public const string WingsRainbow = "wings-rainbow";

    private static ExperienceStage Intro(string az, string en, string petAz, string petEn) =>
        new(PetBrainStageKind.Intro, az, en, petAz, petEn, []);

    public static IReadOnlyList<ExperienceTemplate> Templates { get; } =
    [
        // ================= A: Marsda Robo Xilasetmə =================
        new(
            Key: MarsRoverRescue,
            Version: 1,
            Type: PetBrainExperienceType.Adventure,
            Theme: TraitKeys.Space,
            ActivityType: "exploration-puzzle",
            SceneKey: "mars",
            Icon: "🚀",
            TargetMinutes: 4,
            MinAge: 6,
            TitleAz: "Marsda Robo Xilasetmə",
            TitleEn: "Mars Rover Rescue",
            IntroAz: "Marsdan qəribə siqnal gəlir. Robo köməyimizi gözləyir!",
            IntroEn: "A strange signal is coming from Mars. Robo is waiting for our help!",
            CelebrationAz: "Robo yenidən işləyir! Bunu birlikdə bacardıq.",
            CelebrationEn: "Robo is working again! We did it together.",
            RewardCode: HelmetMars,
            XpReward: 30,
            BondReward: 8,
            InterestAffinity: [TraitKeys.Space, TraitKeys.Science, TraitKeys.Puzzles],
            PlayStyleAffinity: [TraitKeys.Explorer, TraitKeys.ProblemSolver],
            Stages:
            [
                Intro(
                    "Marsdan siqnal gəlir!",
                    "A signal from Mars!",
                    "Qırmızı planetdən zəif bir siqnal eşidirəm. Robo tək qalıb — gedək?",
                    "I hear a faint signal from the red planet. Robo is all alone — shall we go?"),

                new(PetBrainStageKind.Choice,
                    "Robonu hara axtaraq?",
                    "Where should we look for Robo?",
                    "Siqnal üç yerdən gələ bilər. Sən seç, mən arxanca gəlirəm!",
                    "The signal could come from three places. You choose, I am right behind you!",
                    [
                        new("crater", "Krater", "The crater", "🕳️",
                            "Dərin və qaranlıq", "Deep and dark",
                            [new(TraitKeys.Explorer, 2), new(TraitKeys.Space, 1)]),
                        new("canyon", "Kanyon", "The canyon", "🏜️",
                            "Uzun və dolanbac", "Long and winding",
                            [new(TraitKeys.Explorer, 2), new(TraitKeys.Puzzles, 1)]),
                        new("mountain", "Dağ", "The mountain", "⛰️",
                            "Hündür və küləkli", "Tall and windy",
                            [new(TraitKeys.Explorer, 2), new(TraitKeys.Science, 1)])
                    ]),

                new(PetBrainStageKind.Puzzle,
                    "Robonun kilidini aç",
                    "Unlock Robo",
                    "Robonun ekranında rəqəmlər var. Ardıcıllığı tap!",
                    "There are numbers on Robo's screen. Find the pattern!",
                    []),

                new(PetBrainStageKind.Choice,
                    "Robonu necə xilas edək?",
                    "How should we rescue Robo?",
                    "Robo işləyir, amma enerjisi yoxdur. Sənin planın nədir?",
                    "Robo works, but it has no power. What is your plan?",
                    [
                        new("battery", "Batareyanı dəyiş", "Swap the battery", "🔋",
                            "Sürətli və dəqiq", "Fast and precise",
                            [new(TraitKeys.ProblemSolver, 2), new(TraitKeys.Science, 1)]),
                        new("solar-panel", "Günəş paneli qur", "Build a solar panel", "☀️",
                            "Öz həllini icad et", "Invent your own fix",
                            [new(TraitKeys.Creative, 2), new(TraitKeys.ProblemSolver, 1)]),
                        new("carry-to-ship", "Gəmiyə apar", "Carry Robo to the ship", "🛸",
                            "Robonu qoruyaraq", "Keeping Robo safe",
                            [new(TraitKeys.Caring, 2), new(TraitKeys.Explorer, 1)])
                    ])
            ])
        {
            MechanicAffinity = [MechanicKeys.Route, MechanicKeys.StoryChoice, MechanicKeys.Exploration],
            RewardFlavor = PetBrainRewardPreference.PetCosmetic,
        },

        // ================= B: Rənglərini İtirmiş Əjdaha =================
        new(
            Key: DragonLostColors,
            Version: 1,
            Type: PetBrainExperienceType.Creative,
            Theme: TraitKeys.Fantasy,
            ActivityType: "creative-design",
            SceneKey: "dragon",
            Icon: "🐉",
            TargetMinutes: 4,
            MinAge: 5,
            TitleAz: "Rənglərini İtirmiş Əjdaha",
            TitleEn: "The Dragon Who Lost Its Colors",
            IntroAz: "Balaca əjdaha bütün rənglərini itirib. Onları birlikdə geri qaytaraq!",
            IntroEn: "A little dragon has lost all its colors. Let us bring them back together!",
            CelebrationAz: "Əjdaha yenidən parıldayır — bu rəngləri sən seçdin!",
            CelebrationEn: "The dragon is shining again — and you chose these colors!",
            RewardCode: WingsRainbow,
            XpReward: 25,
            BondReward: 8,
            InterestAffinity: [TraitKeys.Fantasy, TraitKeys.Animals, TraitKeys.Stories],
            PlayStyleAffinity: [TraitKeys.Creative, TraitKeys.Caring],
            Stages:
            [
                Intro(
                    "Balaca əjdaha ağarıb!",
                    "The little dragon turned pale!",
                    "Qorxma, ona heç nə olmayıb — sadəcə rəngləri uçub gedib. Kömək edək?",
                    "Do not worry, it is perfectly fine — its colors just floated away. Shall we help?"),

                new(PetBrainStageKind.Choice,
                    "Hansı rəng ailəsini seçirsən?",
                    "Which color family do you pick?",
                    "Burada doğru cavab yoxdur — yalnız sənin zövqün var.",
                    "There is no right answer here — only your taste.",
                    [
                        new("sunset", "Gün batımı", "Sunset", "🌅",
                            "Narıncı və moruq", "Orange and berry",
                            [new(TraitKeys.Creative, 2), new(TraitKeys.Fantasy, 1)]),
                        new("ocean", "Okean", "Ocean", "🌊",
                            "Mavi və firuzəyi", "Blue and turquoise",
                            [new(TraitKeys.Creative, 2), new(TraitKeys.Ocean, 1)]),
                        new("forest", "Meşə", "Forest", "🌿",
                            "Yaşıl və zümrüd", "Green and emerald",
                            [new(TraitKeys.Creative, 2), new(TraitKeys.Nature, 1)]),
                        new("berry", "Moruq bağı", "Berry garden", "🫐",
                            "Bənövşəyi və çəhrayı", "Purple and pink",
                            [new(TraitKeys.Creative, 2), new(TraitKeys.Fantasy, 1)])
                    ]),

                // Naxış mərhələsi TAPMACADIR, amma TƏZYİQSİZ: variantları uşağa
                // xas generator qurur (bax Puzzles/), doğru-səhv isə yoxdur.
                // Seçilən parçaların açarları birbaşa qanadın naxışına düşür.
                new(PetBrainStageKind.Puzzle,
                    "Qanadlara hansı naxış düşsün?",
                    "What pattern goes on the wings?",
                    "Qanadlar boş kətan kimidir. Nə çəkək?",
                    "The wings are like an empty canvas. What shall we draw?",
                    []),

                new(PetBrainStageKind.Choice,
                    "Əjdaha harada yaşasın?",
                    "Where should the dragon live?",
                    "Ona rahat bir ev lazımdır. Sən necə düşünürsən?",
                    "It needs a cosy home. What do you think?",
                    [
                        new("cloud-castle", "Bulud qalası", "Cloud castle", "☁️",
                            "Göydə, sakit", "High up and calm",
                            [new(TraitKeys.Fantasy, 2), new(TraitKeys.Stories, 1)]),
                        new("crystal-cave", "Kristal mağara", "Crystal cave", "💎",
                            "Parlaq və sirli", "Sparkling and secret",
                            [new(TraitKeys.Fantasy, 2), new(TraitKeys.Explorer, 1)]),
                        new("flower-valley", "Çiçək vadisi", "Flower valley", "🌸",
                            "Yumşaq və rəngli", "Soft and colorful",
                            [new(TraitKeys.Nature, 2), new(TraitKeys.Caring, 1)])
                    ]),

                new(PetBrainStageKind.Choice,
                    "Əjdahaya hansı adı verək?",
                    "What shall we name the dragon?",
                    "Ad seçmək ən vacib işdir — o, bu adı həmişəlik daşıyacaq!",
                    "Choosing a name is the most important job — it will keep this name forever!",
                    [
                        new("alov", "Alov", "Ember", "🔥",
                            "İsti və cəsur", "Warm and brave",
                            [new(TraitKeys.Stories, 2), new(TraitKeys.Fantasy, 1)]),
                        new("zumrud", "Zümrüd", "Emerald", "💚",
                            "Sakit və müdrik", "Calm and wise",
                            [new(TraitKeys.Stories, 2), new(TraitKeys.Nature, 1)]),
                        new("shafaq", "Şəfəq", "Aurora", "🌄",
                            "İşıqlı və şən", "Bright and cheerful",
                            [new(TraitKeys.Stories, 2), new(TraitKeys.Creative, 1)]),
                        new("bulud", "Bulud", "Cloudy", "☁️",
                            "Yumşaq və mehriban", "Soft and kind",
                            [new(TraitKeys.Stories, 2), new(TraitKeys.Caring, 1)])
                    ])
            ])
        {
            MechanicAffinity = [MechanicKeys.Pattern, MechanicKeys.Building, MechanicKeys.Decorating],
            RewardFlavor = PetBrainRewardPreference.PetCosmetic,
        },

        // ================= Ayda Kristal (kosmos — Marsın "yaxın qonşusu") =================
        new(
            Key: MoonCrystalRescue,
            Version: 1,
            Type: PetBrainExperienceType.Adventure,
            Theme: TraitKeys.Space,
            ActivityType: "exploration-puzzle",
            SceneKey: "moon",
            Icon: "🌙",
            TargetMinutes: 4,
            MinAge: 6,
            TitleAz: "Ayda Kristal Axtarışı",
            TitleEn: "Moon Crystal Hunt",
            IntroAz: "Ayın kraterlərində parlayan bir kristal var. Onu tapaq!",
            IntroEn: "A glowing crystal is hiding in the craters of the Moon. Let us find it!",
            CelebrationAz: "Kristal bizdədir! Ay bu gecə daha parlaq görünür.",
            CelebrationEn: "We have the crystal! The Moon looks brighter tonight.",
            RewardCode: "",
            XpReward: 20,
            BondReward: 5,
            InterestAffinity: [TraitKeys.Space, TraitKeys.Science],
            PlayStyleAffinity: [TraitKeys.Explorer, TraitKeys.ProblemSolver],
            Stages:
            [
                Intro(
                    "Ayda nəsə parlayır",
                    "Something glows on the Moon",
                    "Teleskopda kiçik bir işıq gördüm. Gedib baxaq?",
                    "I saw a tiny light in the telescope. Shall we go and look?"),

                new(PetBrainStageKind.Choice,
                    "Hansı krateri yoxlayaq?",
                    "Which crater should we check?",
                    "Üç krater var və hər birinin öz sirri var.",
                    "There are three craters, and each keeps its own secret.",
                    [
                        new("north-crater", "Şimal krateri", "North crater", "❄️",
                            "Soyuq və sakit", "Cold and quiet",
                            [new(TraitKeys.Explorer, 2), new(TraitKeys.Space, 1)]),
                        new("deep-crater", "Dərin krater", "Deep crater", "🕳️",
                            "Dibi görünmür", "You cannot see the bottom",
                            [new(TraitKeys.Explorer, 2), new(TraitKeys.Puzzles, 1)]),
                        new("bright-crater", "İşıqlı krater", "Bright crater", "✨",
                            "Günəşə baxır", "It faces the Sun",
                            [new(TraitKeys.Explorer, 2), new(TraitKeys.Science, 1)])
                    ]),

                new(PetBrainStageKind.Puzzle,
                    "Kristalın şifrəsini tap",
                    "Crack the crystal code",
                    "Kristal yalnız doğru rəqəmi eşidəndə açılır!",
                    "The crystal only opens when it hears the right number!",
                    []),

                new(PetBrainStageKind.Choice,
                    "Kristalı necə aparaq?",
                    "How should we carry the crystal?",
                    "O çox kövrəkdir — ehtiyatlı olmalıyıq.",
                    "It is very fragile — we must be careful.",
                    [
                        new("soft-box", "Yumşaq qutuda", "In a soft box", "📦",
                            "Təhlükəsiz seçim", "The safe choice",
                            [new(TraitKeys.Caring, 2), new(TraitKeys.ProblemSolver, 1)]),
                        new("magnet-glove", "Maqnit əlcəyi ilə", "With a magnet glove", "🧤",
                            "Ağıllı alət", "A clever tool",
                            [new(TraitKeys.ProblemSolver, 2), new(TraitKeys.Science, 1)]),
                        new("ice-sled", "Buz xizəyində", "On an ice sled", "🛷",
                            "Öz üsulunu icad et", "Invent your own way",
                            [new(TraitKeys.Creative, 2), new(TraitKeys.Explorer, 1)])
                    ])
            ])
        {
            MechanicAffinity = [MechanicKeys.Route, MechanicKeys.Observation, MechanicKeys.StoryChoice, MechanicKeys.Memory],
            RewardFlavor = PetBrainRewardPreference.StoryPage,
        },

        // ================= Okeanda İşıq =================
        new(
            Key: OceanGlowQuest,
            Version: 1,
            Type: PetBrainExperienceType.Adventure,
            Theme: TraitKeys.Ocean,
            ActivityType: "exploration-puzzle",
            SceneKey: "ocean",
            Icon: "🐬",
            TargetMinutes: 4,
            MinAge: 5,
            TitleAz: "Okeanın İşıqları",
            TitleEn: "Lights of the Ocean",
            IntroAz: "Dərin suda işıq saçan balıqlar yolunu itirib. Onlara yol göstərək!",
            IntroEn: "Glowing fish have lost their way in the deep. Let us guide them home!",
            CelebrationAz: "Balıqlar evə çatdı — okean yenidən parlayır!",
            CelebrationEn: "The fish are home — the ocean is glowing again!",
            RewardCode: "",
            XpReward: 22,
            BondReward: 5,
            InterestAffinity: [TraitKeys.Ocean, TraitKeys.Animals, TraitKeys.Nature],
            PlayStyleAffinity: [TraitKeys.Explorer, TraitKeys.Caring],
            Stages:
            [
                Intro(
                    "Dərinlikdən işıq gəlir",
                    "A light comes from the deep",
                    "Suyun altında kiçik işıqlar görünür. Onlara yaxınlaşaq?",
                    "Little lights are shining under the water. Shall we swim closer?"),

                new(PetBrainStageKind.Choice,
                    "Hansı yolla üzək?",
                    "Which way do we swim?",
                    "Üç cığır var — hansını seçirsən?",
                    "There are three paths — which one do you pick?",
                    [
                        new("coral-path", "Mərcan cığırı", "Coral path", "🪸",
                            "Rəngli və dar", "Colorful and narrow",
                            [new(TraitKeys.Explorer, 2), new(TraitKeys.Nature, 1)]),
                        new("kelp-forest", "Yosun meşəsi", "Kelp forest", "🌿",
                            "Sıx və sakit", "Thick and calm",
                            [new(TraitKeys.Explorer, 2), new(TraitKeys.Animals, 1)]),
                        new("open-water", "Açıq su", "Open water", "💧",
                            "Geniş və sürətli", "Wide and fast",
                            [new(TraitKeys.Explorer, 2), new(TraitKeys.Playful, 1)])
                    ]),

                new(PetBrainStageKind.Puzzle,
                    "İşıq ritmini oxu",
                    "Read the rhythm of the lights",
                    "Balıqlar rəqəmlərlə danışır. Növbəti işıq neçədir?",
                    "The fish speak in numbers. What is the next light?",
                    []),

                new(PetBrainStageKind.Choice,
                    "Onlara necə yol göstərək?",
                    "How do we guide them home?",
                    "İndi onlara evi tapmağa kömək edək.",
                    "Now let us help them find their home.",
                    [
                        new("lantern-trail", "Fənər cığırı", "A lantern trail", "🏮",
                            "Addım-addım işıq", "Light, step by step",
                            [new(TraitKeys.ProblemSolver, 2), new(TraitKeys.Creative, 1)]),
                        new("sing-song", "Mahnı ilə", "With a song", "🎵",
                            "Səslə çağır", "Call them with sound",
                            [new(TraitKeys.Creative, 2), new(TraitKeys.Stories, 1)]),
                        new("swim-together", "Birlikdə üz", "Swim together", "🐟",
                            "Yanlarında qal", "Stay right beside them",
                            [new(TraitKeys.Caring, 2), new(TraitKeys.Animals, 1)])
                    ])
            ])
        {
            MechanicAffinity = [MechanicKeys.Sequencing, MechanicKeys.Exploration, MechanicKeys.Nurturing],
            RewardFlavor = PetBrainRewardPreference.Collection,
        },

        // ================= Meşə Dostları Karnavalı =================
        new(
            Key: ForestFriendsParade,
            Version: 1,
            Type: PetBrainExperienceType.Creative,
            Theme: TraitKeys.Animals,
            ActivityType: "creative-design",
            SceneKey: "forest",
            Icon: "🦋",
            TargetMinutes: 3,
            MinAge: 5,
            TitleAz: "Meşə Dostları Karnavalı",
            TitleEn: "Forest Friends Parade",
            IntroAz: "Meşədə bayram var və dostlarımız hazırlaşır. Onlara kömək edək!",
            IntroEn: "There is a festival in the forest and our friends are getting ready. Let us help!",
            CelebrationAz: "Karnaval başladı — hamı sənin seçdiyin kimi geyinib!",
            CelebrationEn: "The parade has started — everyone is dressed just as you chose!",
            RewardCode: "",
            XpReward: 22,
            BondReward: 5,
            InterestAffinity: [TraitKeys.Animals, TraitKeys.Nature, TraitKeys.Stories],
            PlayStyleAffinity: [TraitKeys.Creative, TraitKeys.Caring],
            Stages:
            [
                Intro(
                    "Meşədə bayram!",
                    "A festival in the forest!",
                    "Dovşan, tülkü və kirpi karnavala hazırlaşır. Kömək edək?",
                    "Bunny, fox and hedgehog are getting ready for the parade. Shall we help?"),

                new(PetBrainStageKind.Choice,
                    "Onlara hansı geyimi seçək?",
                    "Which costumes do we pick?",
                    "Hər seçim gözəldir — sadəcə fərqlidir.",
                    "Every choice is lovely — they are just different.",
                    [
                        new("leaf-capes", "Yarpaq plaşları", "Leaf capes", "🍂",
                            "Meşədən toplanmış", "Gathered from the forest",
                            [new(TraitKeys.Creative, 2), new(TraitKeys.Nature, 1)]),
                        new("flower-crowns", "Çiçək tacları", "Flower crowns", "🌼",
                            "Rəngli və yumşaq", "Colorful and soft",
                            [new(TraitKeys.Creative, 2), new(TraitKeys.Caring, 1)]),
                        new("star-cloaks", "Ulduz örtükləri", "Star cloaks", "✨",
                            "Gecə üçün parlaq", "Bright for the night",
                            [new(TraitKeys.Creative, 2), new(TraitKeys.Fantasy, 1)])
                    ]),

                new(PetBrainStageKind.Choice,
                    "Karnavalda hansı musiqi olsun?",
                    "What music plays at the parade?",
                    "Musiqi bayramın ürəyidir!",
                    "Music is the heart of a festival!",
                    [
                        new("drum-beat", "Nağara ritmi", "Drum beat", "🥁",
                            "Güclü və şən", "Strong and happy",
                            [new(TraitKeys.Playful, 2), new(TraitKeys.Creative, 1)]),
                        new("flute-song", "Tütək havası", "Flute song", "🪈",
                            "Sakit və yumşaq", "Gentle and soft",
                            [new(TraitKeys.Stories, 2), new(TraitKeys.Creative, 1)]),
                        new("bird-choir", "Quş xoru", "Bird choir", "🐦",
                            "Meşənin öz səsi", "The forest's own voice",
                            [new(TraitKeys.Animals, 2), new(TraitKeys.Nature, 1)])
                    ]),

                new(PetBrainStageKind.Choice,
                    "Karnaval haradan keçsin?",
                    "Where does the parade go?",
                    "Son qərar sənindir!",
                    "The final call is yours!",
                    [
                        new("river-bank", "Çay kənarı", "Along the river", "🏞️",
                            "Su səsi ilə", "With the sound of water",
                            [new(TraitKeys.Nature, 2), new(TraitKeys.Explorer, 1)]),
                        new("old-oak", "Qoca palıdın altı", "Under the old oak", "🌳",
                            "Kölgəli və sakit", "Shady and calm",
                            [new(TraitKeys.Stories, 2), new(TraitKeys.Nature, 1)]),
                        new("hilltop", "Təpənin başı", "On the hilltop", "⛰️",
                            "Hər yer görünür", "You can see everything",
                            [new(TraitKeys.Explorer, 2), new(TraitKeys.Playful, 1)])
                    ])
            ])
        {
            MechanicAffinity = [MechanicKeys.Building, MechanicKeys.Decorating, MechanicKeys.Nurturing],
            RewardFlavor = PetBrainRewardPreference.RoomDecor,
        },

        // ================= Robot Laboratoriyası =================
        new(
            Key: RobotLabPuzzle,
            Version: 1,
            Type: PetBrainExperienceType.Adventure,
            Theme: "robots",
            ActivityType: "logic-puzzle",
            SceneKey: "lab",
            Icon: "🤖",
            TargetMinutes: 4,
            MinAge: 7,
            TitleAz: "Dost Robotun Laboratoriyası",
            TitleEn: "The Friendly Robot's Lab",
            IntroAz: "Laboratoriyadakı dost robot qarışdırıb — hər şeyi yenidən qaydaya salaq!",
            IntroEn: "The friendly robot in the lab got muddled — let us sort everything out!",
            CelebrationAz: "Laboratoriya işləyir! Robot sənə təşəkkür edir.",
            CelebrationEn: "The lab is running! The robot says thank you.",
            RewardCode: "",
            XpReward: 24,
            BondReward: 5,
            InterestAffinity: [TraitKeys.Science, TraitKeys.Puzzles],
            PlayStyleAffinity: [TraitKeys.ProblemSolver, TraitKeys.Creative],
            Stages:
            [
                Intro(
                    "Laboratoriyada qarışıqlıq var",
                    "The lab is in a muddle",
                    "Dost robot bütün düymələri qarışdırıb. Kömək edək?",
                    "The friendly robot mixed up all the buttons. Shall we help?"),

                new(PetBrainStageKind.Choice,
                    "Əvvəlcə nəyi düzəldək?",
                    "What do we fix first?",
                    "Üç sistem var — hansından başlayaq?",
                    "There are three systems — where do we start?",
                    [
                        new("power-line", "Enerji xətti", "The power line", "⚡",
                            "Hər şeyin başlanğıcı", "Where everything begins",
                            [new(TraitKeys.ProblemSolver, 2), new(TraitKeys.Science, 1)]),
                        new("sorting-arm", "Çeşidləmə qolu", "The sorting arm", "🦾",
                            "Səliqə gətirir", "It brings order",
                            [new(TraitKeys.Puzzles, 2), new(TraitKeys.ProblemSolver, 1)]),
                        new("paint-nozzle", "Rəng püskürgəci", "The paint nozzle", "🎨",
                            "Ən rəngli hissə", "The most colorful part",
                            [new(TraitKeys.Creative, 2), new(TraitKeys.Science, 1)])
                    ]),

                new(PetBrainStageKind.Puzzle,
                    "Robotun kodunu bərpa et",
                    "Restore the robot's code",
                    "Ekranda bir rəqəm əskikdir. Onu tapa bilərsən?",
                    "One number is missing on the screen. Can you find it?",
                    []),

                new(PetBrainStageKind.Choice,
                    "Robota nə hədiyyə edək?",
                    "What gift do we leave the robot?",
                    "O bizə kömək etdi — indi biz ona bir şey verək.",
                    "It helped us — now let us give something back.",
                    [
                        new("new-lamp", "Yeni lampa", "A new lamp", "💡",
                            "Faydalı hədiyyə", "A useful gift",
                            [new(TraitKeys.ProblemSolver, 2), new(TraitKeys.Caring, 1)]),
                        new("painted-badge", "Boyalı nişan", "A painted badge", "🏅",
                            "Öz əlinlə", "Made by you",
                            [new(TraitKeys.Creative, 2), new(TraitKeys.Caring, 1)]),
                        new("story-book", "Nağıl kitabı", "A story book", "📚",
                            "Gecələr üçün", "For the evenings",
                            [new(TraitKeys.Stories, 2), new(TraitKeys.Caring, 1)])
                    ])
            ])
        {
            MechanicAffinity = [MechanicKeys.Sequencing, MechanicKeys.Pattern, MechanicKeys.Experimentation, MechanicKeys.Memory],
            RewardFlavor = PetBrainRewardPreference.CreativeTool,
        }
    ];

    public static ExperienceTemplate? Find(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : Templates.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.Ordinal));

    public static bool IsKnown(string? key) => Find(key) is not null;

    /// <summary>
    /// Yarımçıq run üçün tərif axtarışı.
    ///
    /// <para><paramref name="version"/> <c>0</c> olanda sətir versiyalaşdırmadan
    /// ƏVVƏL yaranıb — o, cari tərifə bağlanır, çünki köhnə davranış onsuz da
    /// buradakı ilə eynidir.</para>
    ///
    /// <para>Versiya uyğun gəlmirsə (deploy zamanı kataloq dəyişib), tərif
    /// <b>yenə də</b> qaytarılır, amma <c>Exact</c> <c>false</c> olur. Çağıran
    /// buna görə TƏHLÜKƏSİZ davranış seçir: yarımçıq run bitirilə bilir, yeni
    /// tapmaca isə cari qaydalarla verilir. Uşağın açıq macərası heç vaxt
    /// <c>404</c> ilə itmir.</para>
    /// </summary>
    public static ExperienceLookup Resolve(string? key, int version)
    {
        var template = Find(key);

        if (template is null)
            return new ExperienceLookup(null, Exact: false);

        return new ExperienceLookup(template, version is 0 || version == template.Version);
    }

    /// <summary>Bir təcrübənin verdiyi kosmetiklər — açılış qaydası bunlara ayrıca baxır.</summary>
    public static IReadOnlyList<string> RewardCodes { get; } =
        [.. Templates.Select(t => t.RewardCode).Where(c => !string.IsNullOrEmpty(c)).Distinct()];
}
