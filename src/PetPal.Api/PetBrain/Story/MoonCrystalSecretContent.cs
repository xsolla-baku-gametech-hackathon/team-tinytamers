using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Story;

/// <summary>
/// «Ay Kristalının Sirri» — chapter-lər, məqsədlər, əşyalar, ipuçları, sonluqlar.
///
/// <para>Düyünlərdən ayrı fayldadır, çünki bunlar macəranın <b>lüğətidir</b>:
/// düyünlər onlara istinad edir, onlar isə heç nəyə. Ayrı saxlanması həm
/// oxunuşu, həm də «bu əşya harada işlənir» sualının cavabını asanlaşdırır.</para>
/// </summary>
public static class MoonCrystalSecretContent
{
    public static IReadOnlyList<AdventureChapterDefinition> Chapters { get; } =
    [
        new(MoonKeys.Chapter1, Order: 1,
            TitleAz: "Göydən gələn siqnal",
            TitleEn: "A signal from the sky",
            SummaryAz: "Pet qəribə bir siqnal eşitdi. Onun haradan gəldiyini tapdıq və Aya yola düşdük.",
            SummaryEn: "Your pet heard a strange signal. We found where it came from and set off for the Moon.",
            StartNodeId: "c1-hook",
            EstimatedMinutes: 7,
            MainObjectiveIds: [MoonKeys.ObjectiveFindSignal, MoonKeys.ObjectivePackKit],
            OptionalObjectiveIds: []),

        new(MoonKeys.Chapter2, Order: 2,
            TitleAz: "Səssiz Ay bazası",
            TitleEn: "The silent Moon base",
            SummaryAz: "Tərk edilmiş bazanı yenidən işə saldıq və kristalın üç yerə bölündüyünü öyrəndik.",
            SummaryEn: "We woke the abandoned base and learned the crystal had broken into three pieces.",
            StartNodeId: "c2-arrival",
            EstimatedMinutes: 9,
            MainObjectiveIds: [MoonKeys.ObjectivePowerBase],
            OptionalObjectiveIds: [MoonKeys.SideServiceBot]),

        new(MoonKeys.Chapter3, Order: 3,
            TitleAz: "İki yol",
            TitleEn: "Two roads",
            SummaryAz: "Kristal mağarası, yoxsa kölgəli krater? Seçdiyimiz yol bizə ilk parçanı verdi.",
            SummaryEn: "The crystal cave or the shadowed crater? The road we picked gave us the first shard.",
            StartNodeId: "c3-fork",
            EstimatedMinutes: 10,
            MainObjectiveIds: [MoonKeys.ObjectiveFirstShard],
            OptionalObjectiveIds: []),

        new(MoonKeys.Chapter4, Order: 4,
            TitleAz: "İtib-batmış rover",
            TitleEn: "The lost rover",
            SummaryAz: "Roveri izlərindən tapdıq və ona nə olduğunu öyrəndik.",
            SummaryEn: "We tracked the rover down and learned what had happened to it.",
            StartNodeId: "c4-tracks",
            EstimatedMinutes: 10,
            MainObjectiveIds: [MoonKeys.ObjectiveFindRover],
            OptionalObjectiveIds: [MoonKeys.SideRoverMemory, MoonKeys.SideMoonGarden]),

        new(MoonKeys.Chapter5, Order: 5,
            TitleAz: "Kristal rəsədxanası",
            TitleEn: "The crystal observatory",
            SummaryAz: "İpuçlarını birləşdirdik və kristalın niyə parçalandığını başa düşdük.",
            SummaryEn: "We put the clues together and understood why the crystal had broken.",
            StartNodeId: "c5-board",
            EstimatedMinutes: 9,
            MainObjectiveIds: [MoonKeys.ObjectiveReadTruth],
            OptionalObjectiveIds: []),

        new(MoonKeys.Chapter6, Order: 6,
            TitleAz: "Ay işığının qayıdışı",
            TitleEn: "The return of the moonlight",
            SummaryAz: "Hər şeyi bir araya gətirdik və Ay yenidən işıqlandı.",
            SummaryEn: "We brought it all together and the Moon lit up again.",
            StartNodeId: "c6-assemble",
            EstimatedMinutes: 8,
            MainObjectiveIds: [MoonKeys.ObjectiveRestoreLight],
            OptionalObjectiveIds: [])
    ];

    public static IReadOnlyList<AdventureObjectiveDefinition> Objectives { get; } =
    [
        new(MoonKeys.ObjectiveFindSignal, MoonKeys.Chapter1, AdventureObjectiveKind.SolvePuzzle,
            "Siqnalın mənbəyini tap", "Find where the signal comes from",
            "Naxışı oxu və siqnalın Aydan gəldiyini təsdiqlə.",
            "Read the pattern and confirm the signal comes from the Moon."),

        new(MoonKeys.ObjectivePackKit, MoonKeys.Chapter1, AdventureObjectiveKind.CollectItems,
            "Kosmik çantanı yığ", "Pack the space kit",
            "Üç alətdən ikisini seç — hamısını götürmək olmur.",
            "Choose two of the three tools — you cannot take them all.",
            RequiredCount: 2),

        new(MoonKeys.ObjectivePowerBase, MoonKeys.Chapter2, AdventureObjectiveKind.HelpNpc,
            "Bazanı işə sal", "Bring the base back to life",
            "Enerjini hansı sistemə verəcəyinə qərar ver.",
            "Decide which system gets the energy."),

        new(MoonKeys.ObjectiveFirstShard, MoonKeys.Chapter3, AdventureObjectiveKind.FindItem,
            "İlk kristal parçasını tap", "Find the first crystal shard",
            "Seçdiyin yol səni ona aparacaq.",
            "The road you choose will take you to it."),

        new(MoonKeys.ObjectiveFindRover, MoonKeys.Chapter4, AdventureObjectiveKind.FindItem,
            "Roveri tap", "Find the rover",
            "İzləri oxu və onun harada dayandığını tap.",
            "Read the tracks and find where it stopped."),

        new(MoonKeys.ObjectiveReadTruth, MoonKeys.Chapter5, AdventureObjectiveKind.FindClue,
            "Həqiqəti oxu", "Read the truth",
            "İpuçlarını lövhədə birləşdir.",
            "Put the clues together on the board."),

        new(MoonKeys.ObjectiveRestoreLight, MoonKeys.Chapter6, AdventureObjectiveKind.BuildObject,
            "Ay işığını qaytar", "Bring the moonlight back",
            "Parçaları yerinə qoy və rəsədxananı işə sal.",
            "Put the shards in place and start the observatory."),

        new(MoonKeys.SideRoverMemory, MoonKeys.Chapter4, AdventureObjectiveKind.FindItem,
            "Roverin yaddaşını bərpa et", "Restore the rover's memory",
            "İtmiş yaddaş hissəsini tap və ona qaytar.",
            "Find the lost memory piece and give it back.",
            IsOptional: true),

        new(MoonKeys.SideMoonGarden, MoonKeys.Chapter4, AdventureObjectiveKind.HelpNpc,
            "Ay bağçasına enerji ver", "Power the Moon garden",
            "Bağçanın işıqları sönüb. Onları yandır.",
            "The garden lights went out. Turn them on.",
            IsOptional: true),

        new(MoonKeys.SideServiceBot, MoonKeys.Chapter2, AdventureObjectiveKind.HelpNpc,
            "Kiçik servis robotunu təmir et", "Repair the little service robot",
            "O, bazanın dəhlizində dayanıb qalıb.",
            "It is stuck in the corridor of the base.",
            IsOptional: true)
    ];

    public static IReadOnlyList<AdventureItemDefinition> Items { get; } =
    [
        new(MoonKeys.ToolScanner, "Enerji skaneri", "Energy scanner",
            "Gizli enerji izlərini göstərir.", "It shows hidden trails of energy.", "📡",
            IsQuestItem: false),

        new(MoonKeys.ToolRepairBot, "Mini təmir robotu", "Mini repair bot",
            "Sınmış şeyləri özü düzəldir.", "It fixes broken things on its own.", "🔧",
            IsQuestItem: false),

        new(MoonKeys.ToolLightCrystal, "İşıq kristalı", "Light crystal",
            "Qaranlıqda yolu işıqlandırır.", "It lights the way in the dark.", "🔆",
            IsQuestItem: false),

        new(MoonKeys.ItemPowerCell, "Enerji xanası", "Power cell",
            "Bir sistemi işə salmağa çatır.", "Enough to start one system.", "🔋",
            IsConsumable: true),

        new(MoonKeys.ItemMoonMap, "Ay xəritəsi", "Moon map",
            "Gizli yolları göstərir.", "It shows the hidden paths.", "🗺️"),

        new(MoonKeys.ItemRoverMemory, "Roverin yaddaş hissəsi", "Rover memory piece",
            "Onun gördüklərini saxlayır.", "It holds what the rover saw.", "💾"),

        new(MoonKeys.ItemShardOne, "Birinci kristal parçası", "First crystal shard",
            "Soyuq və sakit parlayır.", "It glows cool and quiet.", "💎", IsQuestItem: true),

        new(MoonKeys.ItemShardTwo, "İkinci kristal parçası", "Second crystal shard",
            "Toxunanda titrəyir.", "It hums when you touch it.", "💠", IsQuestItem: true),

        new(MoonKeys.ItemShardThree, "Üçüncü kristal parçası", "Third crystal shard",
            "Ən böyüyü və ən istisi.", "The biggest and the warmest.", "🔷", IsQuestItem: true),

        new(MoonKeys.ItemGardenSeed, "Ay toxumu", "Moon seed",
            "Bağçadan bir yadigar.", "A keepsake from the garden.", "🌱"),

        new(MoonKeys.ItemUpgradedTool, "Gücləndirilmiş alət", "Upgraded tool",
            "Təmir stansiyası onu yaxşılaşdırdı.", "The repair station made it better.", "⚙️")
    ];

    public static IReadOnlyList<AdventureClueDefinition> Clues { get; } =
    [
        new(MoonKeys.ClueSignalRhythm, "Siqnalın ritmi", "The rhythm of the signal",
            "Siqnal təsadüfi deyil — o, təkrarlanan bir naxışdır. Deməli onu kimsə göndərir.",
            "The signal is not random — it repeats. That means someone is sending it.",
            "🎵", AdventureClueImportance.Key,
            RelatedObjectiveId: MoonKeys.ObjectiveFindSignal,
            RelatedPuzzleNodeId: "c1-pattern"),

        new(MoonKeys.ClueBaseLog, "Bazanın son qeydi", "The base's last log",
            "«Dalğa yaxınlaşır. Kristalı qorumalıyıq.» Qeyd yarımçıq kəsilib.",
            "\"The wave is coming. We must protect the crystal.\" The log stops there.",
            "📓", AdventureClueImportance.Key),

        new(MoonKeys.ClueThreePieces, "Üç parça", "Three pieces",
            "Kristal üç yerə bölünüb və hər parça ayrı yerdədir.",
            "The crystal is in three pieces, and each one is somewhere else.",
            "🧩", AdventureClueImportance.Key,
            RelatedPuzzleNodeId: "c5-shards"),

        new(MoonKeys.ClueCaveSong, "Mağaranın nəğməsi", "The cave's song",
            "Mağara divarları səsi qaytarır və işıq həmin ritmlə yanır.",
            "The cave walls send the sound back, and the light pulses with it.",
            "🔊", AdventureClueImportance.Helpful,
            RelatedPuzzleNodeId: "c6-pattern"),

        new(MoonKeys.ClueCraterTracks, "Kraterdəki izlər", "Tracks in the crater",
            "Təkər izləri kraterdən çıxıb şərqə gedir — kimsə buradan keçib.",
            "Wheel tracks leave the crater heading east — someone came through here.",
            "🛞", AdventureClueImportance.Helpful,
            RelatedPuzzleNodeId: "c4-recall"),

        new(MoonKeys.ClueRoverLast, "Roverin son gördüyü", "What the rover saw last",
            "Rover göydə parlaq bir zolaq görüb, sonra hər şey susub.",
            "The rover saw a bright streak in the sky, and then everything went quiet.",
            "👁️", AdventureClueImportance.Key),

        new(MoonKeys.ClueMeteorWave, "Meteorit dalğası", "The meteor wave",
            "Parlaq zolaq meteorit dalğası idi. Kristal onu ƏVVƏLCƏDƏN görüb.",
            "The bright streak was a meteor wave. The crystal saw it coming.",
            "☄️", AdventureClueImportance.Key,
            RelatedObjectiveId: MoonKeys.ObjectiveReadTruth,
            RelatedPuzzleNodeId: "c6-pattern"),

        new(MoonKeys.ClueShieldCode, "Qoruyucu sistemin açarı", "The shield's key",
            "Qoruyucu sistem kristalın öz ritmi ilə açılır.",
            "The shield opens with the crystal's own rhythm.",
            "🛡️", AdventureClueImportance.Helpful,
            RelatedPuzzleNodeId: "c6-pattern")
    ];

    /// <summary>
    /// Üç sonluq — hamısı MÜSBƏT, hamısı çatılan.
    ///
    /// <para>Bal həddi qəsdən aşağıdır (hər biri 3): sonluq uşağın nə qədər
    /// «yaxşı» oynadığını deyil, macəra boyu nəyi SEÇDİYİNİ ölçür. Heç bir
    /// yol digərindən yüksək bal vermir — sadəcə fərqli sonluğa yığır.</para>
    ///
    /// <para>Birinci sıradakı qoruyucu həm də ehtiyat cavabdır: bal heç yerə
    /// çatmasa (uşaq hər chapter-də başqa yol seçib), macəra yenə də bitir.</para>
    /// </summary>
    public static IReadOnlyList<AdventureEndingDefinition> Endings { get; } =
    [
        new(MoonKeys.EndingGuardian, "c6-ending-guardian",
            "Ayın Qoruyucusu", "Guardian of the Moon",
            "Ayın Qoruyucusu", "Guardian of the Moon",
            RewardCode: "halo-guardian", MinimumScore: 3),

        new(MoonKeys.EndingExplorer, "c6-ending-explorer",
            "Kristal Tədqiqatçısı", "Crystal Researcher",
            "Kristal Tədqiqatçısı", "Crystal Researcher",
            RewardCode: "visor-explorer", MinimumScore: 3),

        new(MoonKeys.EndingRobotFriend, "c6-ending-robots",
            "Robotların Dostu", "Friend of the Robots",
            "Robotların Dostu", "Friend of the Robots",
            RewardCode: "badge-robot-friend", MinimumScore: 3)
    ];
}
