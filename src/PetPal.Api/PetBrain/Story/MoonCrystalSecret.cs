using PetPal.Api.PetBrain.Puzzles;

namespace PetPal.Api.PetBrain.Story;

/// <summary>
/// <b>«Ay Kristalının Sirri»</b> — macəranın SÖZLÜYÜ.
///
/// <para>Bütün açarlar bir yerdədir və <c>const</c>-dur, çünki qrafın altı
/// faylı, tapmacalar, testlər və mükafat qatı eyni sətirlərə istinad edir.
/// Yazılış səhvi bir saatlıq macəranın ortasında bağlı qapı deməkdir; sabit
/// açar isə səhvi kompilyasiya vaxtına çəkir.</para>
/// </summary>
public static class MoonKeys
{
    public const string Chapter1 = "ch1-signal";
    public const string Chapter2 = "ch2-base";
    public const string Chapter3 = "ch3-two-roads";
    public const string Chapter4 = "ch4-rover";
    public const string Chapter5 = "ch5-observatory";
    public const string Chapter6 = "ch6-moonlight";

    public const string RoleScientist = "role-scientist";
    public const string RoleEngineer = "role-engineer";
    public const string RoleTracker = "role-tracker";

    public const string ToolScanner = "tool-scanner";
    public const string ToolRepairBot = "tool-repair-bot";
    public const string ToolLightCrystal = "tool-light-crystal";

    public const string ItemPowerCell = "item-power-cell";
    public const string ItemMoonMap = "item-moon-map";
    public const string ItemRoverMemory = "item-rover-memory";
    public const string ItemShardOne = "item-shard-one";
    public const string ItemShardTwo = "item-shard-two";
    public const string ItemShardThree = "item-shard-three";
    public const string ItemGardenSeed = "item-garden-seed";
    public const string ItemUpgradedTool = "item-upgraded-tool";

    public const string ClueSignalRhythm = "clue-signal-rhythm";
    public const string ClueBaseLog = "clue-base-log";
    public const string ClueThreePieces = "clue-three-pieces";
    public const string ClueCaveSong = "clue-cave-song";
    public const string ClueCraterTracks = "clue-crater-tracks";
    public const string ClueRoverLast = "clue-rover-last";
    public const string ClueMeteorWave = "clue-meteor-wave";
    public const string ClueShieldCode = "clue-shield-code";

    public const string ObjectiveFindSignal = "obj-find-signal";
    public const string ObjectivePackKit = "obj-pack-kit";
    public const string ObjectivePowerBase = "obj-power-base";
    public const string ObjectiveFirstShard = "obj-first-shard";
    public const string ObjectiveFindRover = "obj-find-rover";
    public const string ObjectiveReadTruth = "obj-read-truth";
    public const string ObjectiveRestoreLight = "obj-restore-light";

    public const string SideRoverMemory = "side-rover-memory";
    public const string SideMoonGarden = "side-moon-garden";
    public const string SideServiceBot = "side-service-bot";

    public const string FlagMapPowered = "flag-map-powered";
    public const string FlagCommsPowered = "flag-comms-powered";
    public const string FlagRepairPowered = "flag-repair-powered";
    public const string FlagCaveRoute = "flag-cave-route";
    public const string FlagCraterRoute = "flag-crater-route";
    public const string FlagRoverHelps = "flag-rover-helps";
    public const string FlagCleanSolve = StoryRuntime.CleanSolveFlag;

    /// <summary>Macəradan SONRA da yaşayan bayraqlar — digər ekranlar onları oxuyur.</summary>
    public const string WorldRoverAwake = "world-rover-awake";
    public const string WorldGardenAlive = "world-garden-alive";
    public const string WorldObservatoryOn = "world-observatory-on";
    public const string WorldBaseAwake = "world-base-awake";

    public const string EndingGuardian = "moon-guardian";
    public const string EndingExplorer = "moon-explorer";
    public const string EndingRobotFriend = "moon-robot-friend";

    public const string NpcRover = "npc-rover";
    public const string NpcServiceBot = "npc-service-bot";
    public const string NpcCaveCreature = "npc-cave-creature";

    public const string AbilityListen = "pet-listen";
    public const string AbilityDig = "pet-dig";
    public const string AbilityGlow = "pet-glow";
}

/// <summary>
/// Tərifi yazmağı OXUNAQLI edən qısaltmalar.
///
/// <para>Qrafın özü 40 düyündür; hər keçidi tam konstruktorla yazmaq faylı
/// oxunmaz edərdi və səhvi gizlədərdi. Bu köməkçilər heç bir məntiq
/// daşımır — yalnız adlandırırlar.</para>
/// </summary>
internal static class Moon
{
    public static ExperienceOption Option(
        string key, string labelAz, string labelEn, string icon,
        string detailAz, string detailEn, params TraitDelta[] traits) =>
        new(key, labelAz, labelEn, icon, detailAz, detailEn, traits);

    public static ExperienceTransition On(string option, string target, int priority = 10) =>
        new(target, RequiredOptionKey: option, Priority: priority);

    public static ExperienceTransition OnIf(
        string option, string target, ExperienceCondition requires, int priority = 5) =>
        new(target, RequiredOptionKey: option, Priority: priority) { Requires = requires };

    public static ExperienceTransition If(
        ExperienceCondition requires, string target, int priority = 20) =>
        new(target, Priority: priority) { Requires = requires };

    public static ExperienceTransition Fallback(string target) =>
        new(target, Priority: 1000, IsFallback: true);

    public static ExperienceEffect Flag(string key) =>
        new(ExperienceEffectKind.SetFlag, key);

    public static ExperienceEffect World(string key) =>
        new(ExperienceEffectKind.SetWorldFlag, key);

    public static ExperienceEffect Item(string key, int amount = 1) =>
        new(ExperienceEffectKind.GrantItem, key) { Amount = amount };

    public static ExperienceEffect Clue(string key) =>
        new(ExperienceEffectKind.DiscoverClue, key);

    public static ExperienceEffect Spend(string item, int amount = 1) =>
        new(ExperienceEffectKind.ConsumeItem, item) { Amount = amount };

    public static ExperienceEffect Start(string objective) =>
        new(ExperienceEffectKind.StartObjective, objective);

    public static ExperienceEffect Done(string objective) =>
        new(ExperienceEffectKind.CompleteObjective, objective);

    public static ExperienceEffect Step(string objective, int amount = 1) =>
        new(ExperienceEffectKind.AdvanceObjective, objective) { Amount = amount };

    public static ExperienceEffect Score(string ending, int amount) =>
        new(ExperienceEffectKind.ScoreEnding, ending) { Amount = amount };

    public static ExperienceEffect Scene(string key) =>
        new(ExperienceEffectKind.SceneVariant, key);

    public static ExperienceEffect Remember(string key) =>
        new(ExperienceEffectKind.RememberChoice, key);

    public static ExperienceCondition HasItem(string item) => new() { RequiredItem = item };

    public static ExperienceCondition HasClue(string clue) => new() { RequiredClue = clue };

    public static ExperienceCondition Chose(string choice) => new() { RequiredChoice = choice };

    public static ExperienceCondition WithFlag(string flag) => new() { RequiredFlag = flag };

    public static ExperienceCondition OnlyIn(string variant) => new() { RequiredVariant = variant };

    public static ExperienceCondition NotIn(string variant) => new() { ForbiddenVariant = variant };
}

/// <summary>
/// <b>«Ay Kristalının Sirri»</b> — engine-in tam sübutu.
///
/// <para>Altı chapter, qırxa yaxın düyün, altı tapmaca (altı fərqli
/// mexanikada), üç yan tapşırıq, iki əsas yol və üç sonluq. Bir sessiyada
/// oynanmaq üçün NƏZƏRDƏ TUTULMAYIB: hər chapter öz checkpoint-i ilə bitir və
/// uşaq istədiyi yerdə dayanıb sonra davam edir.</para>
///
/// <para><b>Seçim burada bəzək deyil.</b> Chapter 1-də yığılan çanta chapter
/// 6-da hansı finalı ala biləcəyini müəyyən edir; chapter 2-dəki enerji
/// qərarı chapter 4-də rovere yanaşmanı dəyişir; chapter 3-dəki yol chapter
/// 5-də əlavə həll açır. Heç bir seçim «səhv» deyil — hər biri BAŞQA yoldur.</para>
/// </summary>
public static class MoonCrystalSecret
{
    public static ExperienceDefinition Definition { get; } = new(
        Key: ExperienceCatalog.MoonCrystalSecret,
        Version: 1,
        StartNodeId: "c1-hook",
        AllowedPuzzleFamilies:
        [
            PuzzleBlueprintCatalog.MoonSignalPatternKey,
            PuzzleBlueprintCatalog.MoonBasePowerKey,
            PuzzleBlueprintCatalog.SequenceOrderKey,
            PuzzleBlueprintCatalog.RouteLogicKey,
            PuzzleBlueprintCatalog.MoonTrackRecallKey,
            PuzzleBlueprintCatalog.MoonShardMatchKey
        ],
        Nodes: [.. MoonChaptersOneToThree.Nodes(), .. MoonChaptersFourToSix.Nodes()])
    {
        Chapters = MoonCrystalSecretContent.Chapters,
        Objectives = MoonCrystalSecretContent.Objectives,
        Items = MoonCrystalSecretContent.Items,
        Clues = MoonCrystalSecretContent.Clues,
        Endings = MoonCrystalSecretContent.Endings,
        SequelHookAz = "Gecə yarısı rover yeni siqnal göndərdi: «Marsda da bir kristal oyanır…» Növbəti səfər artıq gözləyir.",
        SequelHookEn = "At midnight the rover sent a new signal: \"A crystal is waking on Mars too…\" The next trip is already waiting."
    };
}
