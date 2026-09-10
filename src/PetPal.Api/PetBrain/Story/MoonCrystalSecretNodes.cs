using PetPal.Api.PetBrain.Puzzles;
using PetPal.Shared.Enums;
using static PetPal.Api.PetBrain.Story.Moon;

namespace PetPal.Api.PetBrain.Story;

/// <summary>
/// «Ay Kristalının Sirri» — chapter 1–3-ün düyünləri.
///
/// <para>Hər chapter eyni daxili ritmi izləyir: qarmaq → məqsəd → araşdırma →
/// kiçik qərar → gameplay → gözlənilməz dönüş → əsas tapmaca → nəticə →
/// mükafat → xülasə və checkpoint. Ritm qəsdən təkrarlanır (uşaq nə
/// gözləyəcəyini bilir), MƏZMUNU isə hər dəfə dəyişir — eyni tapmacanın rəngini
/// dəyişib təkrar vermək qadağandır.</para>
/// </summary>
internal static class MoonChaptersOneToThree
{
    public static IEnumerable<ExperienceNode> Nodes()
    {
        foreach (var node in ChapterOne()) yield return node;
        foreach (var node in ChapterTwo()) yield return node;
        foreach (var node in ChapterThree()) yield return node;
    }

    private static IEnumerable<ExperienceNode> ChapterOne()
    {
        const string chapter = MoonKeys.Chapter1;

        yield return new ExperienceNode(
            Id: "c1-hook",
            Kind: PetBrainStageKind.Intro,
            PromptAz: "Pet nəsə eşidir",
            PromptEn: "Your pet hears something",
            PetLineAz: "Dayan… eşidirsən? Pəncərədən gələn bu səs bütün gecə təkrarlanır.",
            PetLineEn: "Wait… do you hear it? That sound at the window has repeated all night.",
            Options: [],
            Transitions: [Fallback("c1-listen")],
            Effects: [Start(MoonKeys.ObjectiveFindSignal), Scene("moon-window-night")])
        {
            ChapterId = chapter,
            PetAbilityKey = MoonKeys.AbilityListen
        };

        yield return new ExperienceNode(
            Id: "c1-listen",
            Kind: PetBrainStageKind.CooperativePetAction,
            PromptAz: "Birlikdə dinləyək",
            PromptEn: "Let us listen together",
            PetLineAz: "Qulağımı pəncərəyə dayadım. Sən də nəfəsini tut — səs yenidən gəlir…",
            PetLineEn: "I pressed my ear to the window. Hold your breath too — the sound is coming back…",
            Options: [],
            Transitions: [Fallback("c1-heard")],
            Effects: [Scene("moon-window-listen")])
        {
            ChapterId = chapter,
            PetAbilityKey = MoonKeys.AbilityListen
        };

        yield return new ExperienceNode(
            Id: "c1-heard",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Səs bir qayda ilə təkrarlanır",
            PromptEn: "The sound repeats with a rule",
            PetLineAz: "Bu, təsadüfi səs deyil! Uzun, qısa, cüt… və yenidən baştan. Kimsə danışır.",
            PetLineEn: "This is not a random noise! Long, short, double… and back to the start. Someone is talking.",
            Options: [],
            Transitions: [Fallback("c1-pattern")],
            Effects: [Clue(MoonKeys.ClueSignalRhythm), Scene("moon-signal-wave")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c1-pattern",
            Kind: PetBrainStageKind.Puzzle,
            PromptAz: "Siqnalın naxışını tut",
            PromptEn: "Catch the pattern of the signal",
            PetLineAz: "Naxışı davam etdirsək, siqnal bizə cavab verəcək. Sən dava et!",
            PetLineEn: "If we continue the pattern, the signal will answer us. You carry it on!",
            Options: [],
            Transitions:
            [
                new ExperienceTransition("c1-source-clean",
                    RequiredResult: PetBrainStageResult.Solved,
                    RequiredFlag: MoonKeys.FlagCleanSolve,
                    Priority: 5),

                new ExperienceTransition("c1-source", RequiredResult: PetBrainStageResult.Solved, Priority: 10),
                Fallback("c1-source")
            ],
            Effects: [Scene("moon-signal-desk")],
            PuzzleFamily: PuzzleBlueprintCatalog.MoonSignalPatternKey)
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c1-source-clean",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Siqnal dərhal cavab verdi",
            PromptEn: "The signal answered at once",
            PetLineAz: "Bir dəfəyə tutdun! Cavab gəldi və mənbə göründü: Ay. Özü də Ayın qaranlıq tərəfi.",
            PetLineEn: "You caught it in one go! The answer came and the source appeared: the Moon. Its dark side.",
            Options: [],
            Transitions: [Fallback("c1-role")],
            Effects: [Done(MoonKeys.ObjectiveFindSignal), Score(MoonKeys.EndingExplorer, 1), Scene("moon-map-glow")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c1-source",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Mənbə tapıldı",
            PromptEn: "The source is found",
            PetLineAz: "Naxış tamamlandı və cavab gəldi. Siqnal Aydandır — özü də Ayın qaranlıq tərəfindən.",
            PetLineEn: "The pattern is complete and the answer came. The signal is from the Moon — its dark side.",
            Options: [],
            Transitions: [Fallback("c1-role")],
            Effects: [Done(MoonKeys.ObjectiveFindSignal), Scene("moon-map-glow")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c1-role",
            Kind: PetBrainStageKind.Choice,
            PromptAz: "Bu missiyada rolun nədir?",
            PromptEn: "What is your role on this mission?",
            PetLineAz: "Hər rol Ayda başqa qapı açır. Sən özünü kim kimi görürsən?",
            PetLineEn: "Each role opens a different door on the Moon. Who do you see yourself as?",
            Options:
            [
                Option(MoonKeys.RoleScientist, "Tədqiqatçı", "Researcher", "🔬",
                    "Sən oxuyub anlayırsan", "You read things and understand them",
                    new TraitDelta(TraitKeys.Science, 3), new TraitDelta(TraitKeys.ProblemSolver, 1)),

                Option(MoonKeys.RoleEngineer, "Mühəndis", "Engineer", "🛠️",
                    "Sən sınanı düzəldirsən", "You repair what is broken",
                    new TraitDelta(TraitKeys.ProblemSolver, 3), new TraitDelta(TraitKeys.Creative, 1)),

                Option(MoonKeys.RoleTracker, "İzçi", "Tracker", "🧭",
                    "Sən izi tapırsan", "You find the trail",
                    new TraitDelta(TraitKeys.Explorer, 3), new TraitDelta(TraitKeys.Space, 1))
            ],
            Transitions:
            [
                On(MoonKeys.RoleScientist, "c1-kit"),
                On(MoonKeys.RoleEngineer, "c1-kit"),
                On(MoonKeys.RoleTracker, "c1-kit"),
                Fallback("c1-kit")
            ],
            Effects: [Start(MoonKeys.ObjectivePackKit)])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c1-kit",
            Kind: PetBrainStageKind.Choice,
            PromptAz: "Çantaya birinci nə qoyaq?",
            PromptEn: "What goes into the kit first?",
            PetLineAz: "Çantaya yalnız iki alət sığır. Birincini sən seç.",
            PetLineEn: "Only two tools fit in the kit. You pick the first one.",
            Options:
            [
                Option("pick-scanner", "Enerji skaneri", "Energy scanner", "📡",
                    "Gizli enerjini görür", "It sees hidden energy",
                    new TraitDelta(TraitKeys.Science, 2)),

                Option("pick-repair", "Mini təmir robotu", "Mini repair bot", "🔧",
                    "Sınanı düzəldir", "It fixes what is broken",
                    new TraitDelta(TraitKeys.ProblemSolver, 2)),

                Option("pick-light", "İşıq kristalı", "Light crystal", "🔆",
                    "Qaranlığı işıqlandırır", "It lights the dark",
                    new TraitDelta(TraitKeys.Creative, 2))
            ],
            Transitions:
            [
                On("pick-scanner", "c1-kit-second-scanner"),
                On("pick-repair", "c1-kit-second-repair"),
                On("pick-light", "c1-kit-second-light"),
                Fallback("c1-kit-second-scanner")
            ],
            Effects: [Scene("moon-kit-table")])
        {
            ChapterId = chapter
        };

        yield return KitSecond(
            "c1-kit-second-scanner", chapter, MoonKeys.ToolScanner,
            "Skaner çantadadır", "The scanner is packed",
            ("pick-repair-2", MoonKeys.ToolRepairBot, "Mini təmir robotu", "Mini repair bot", "🔧"),
            ("pick-light-2", MoonKeys.ToolLightCrystal, "İşıq kristalı", "Light crystal", "🔆"));

        yield return KitSecond(
            "c1-kit-second-repair", chapter, MoonKeys.ToolRepairBot,
            "Robot çantadadır", "The bot is packed",
            ("pick-scanner-2", MoonKeys.ToolScanner, "Enerji skaneri", "Energy scanner", "📡"),
            ("pick-light-2", MoonKeys.ToolLightCrystal, "İşıq kristalı", "Light crystal", "🔆"));

        yield return KitSecond(
            "c1-kit-second-light", chapter, MoonKeys.ToolLightCrystal,
            "Kristal çantadadır", "The crystal is packed",
            ("pick-scanner-2", MoonKeys.ToolScanner, "Enerji skaneri", "Energy scanner", "📡"),
            ("pick-repair-2", MoonKeys.ToolRepairBot, "Mini təmir robotu", "Mini repair bot", "🔧"));

        yield return new ExperienceNode(
            Id: "c1-launch",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Gəmi hazırdır",
            PromptEn: "The ship is ready",
            PetLineAz: "Çanta bağlandı, gəmi hazırdır. Ay bizi gözləyir!",
            PetLineEn: "The kit is closed and the ship is ready. The Moon is waiting for us!",
            Options: [],
            Transitions: [Fallback("c1-recap")],
            Effects: [Done(MoonKeys.ObjectivePackKit), Scene("moon-launch")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c1-recap",
            Kind: PetBrainStageKind.ChapterRecap,
            PromptAz: "Birinci fəsil bitdi",
            PromptEn: "Chapter one is done",
            PetLineAz: "Siqnalı oxuduq, rolunu seçdik və çantanı yığdıq. İstəsən burada dayana bilərik — qayıdanda buradan davam edərik.",
            PetLineEn: "We read the signal, chose your role and packed the kit. We can stop here if you like — we will pick up right at this spot.",
            Options: [],
            Transitions: [Fallback("c2-arrival")],
            Effects: [Remember("moon-role")])
        {
            ChapterId = chapter,
            IsCheckpoint = true,
            CompletesChapterId = chapter
        };
    }

    /// <summary>
    /// Çantanın İKİNCİ aləti — birinci seçimə görə qalan iki variant.
    ///
    /// <para>Üç ayrı düyün yazmaq əvəzinə bir qurucu var, çünki fərq yalnız
    /// artıq götürülmüş alətdədir. Hər üçü eyni yerə — buraxılışa gedir.</para>
    /// </summary>
    private static ExperienceNode KitSecond(
        string id,
        string chapter,
        string firstTool,
        string promptAz,
        string promptEn,
        (string Key, string Tool, string LabelAz, string LabelEn, string Icon) left,
        (string Key, string Tool, string LabelAz, string LabelEn, string Icon) right)
    {
        ExperienceOption Pick(
            (string Key, string Tool, string LabelAz, string LabelEn, string Icon) tool,
            string detailAz, string detailEn, TraitDelta trait) =>
            Option(tool.Key, tool.LabelAz, tool.LabelEn, tool.Icon, detailAz, detailEn, trait) with
            {
                Effects = [Item(tool.Tool), Step(MoonKeys.ObjectivePackKit)]
            };

        return new ExperienceNode(
            Id: id,
            Kind: PetBrainStageKind.Choice,
            PromptAz: $"{promptAz}. İkinci nə olsun?",
            PromptEn: $"{promptEn}. What is the second?",
            PetLineAz: "Bir yer də qaldı. Nəyi geridə qoymağa hazırsan?",
            PetLineEn: "One slot left. What are you ready to leave behind?",
            Options:
            [
                Pick(left, "Bunu da götürək", "Let us take this too",
                    new TraitDelta(TraitKeys.ProblemSolver, 1)),

                Pick(right, "Yox, bunu götürək", "No, take this one",
                    new TraitDelta(TraitKeys.Creative, 1))
            ],
            Transitions:
            [
                On(left.Key, "c1-launch"),
                On(right.Key, "c1-launch"),
                Fallback("c1-launch")
            ],
            Effects: [Item(firstTool), Step(MoonKeys.ObjectivePackKit)])
        {
            ChapterId = chapter
        };
    }

    private static IEnumerable<ExperienceNode> ChapterTwo()
    {
        const string chapter = MoonKeys.Chapter2;

        yield return new ExperienceNode(
            Id: "c2-arrival",
            Kind: PetBrainStageKind.Intro,
            PromptAz: "Baza tamamilə səssizdir",
            PromptEn: "The base is completely silent",
            PetLineAz: "Burada heç kim yoxdur… işıqlar sönüb, amma qapı açıqdır. Kimsə tələsik çıxıb.",
            PetLineEn: "Nobody is here… the lights are out, but the door is open. Someone left in a hurry.",
            Options: [],
            Transitions: [Fallback("c2-explore")],
            Effects: [Start(MoonKeys.ObjectivePowerBase), Scene("moon-base-dark")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c2-explore",
            Kind: PetBrainStageKind.Exploration,
            PromptAz: "Bazada nəyi yoxlayaq?",
            PromptEn: "What do we look at in the base?",
            PetLineAz: "Üç şey diqqətimi çəkir. Hansına baxaq?",
            PetLineEn: "Three things catch my eye. Which one shall we check?",
            Options:
            [
                Option("look-desk", "İş masası", "The desk", "🗄️",
                    "Üstündə açıq qeyd dəftəri var", "An open logbook lies on it",
                    new TraitDelta(TraitKeys.Science, 2)),

                Option("look-corridor", "Dəhliz", "The corridor", "🚪",
                    "Orada nəsə tərpənir", "Something is moving there",
                    new TraitDelta(TraitKeys.Explorer, 2)),

                Option("look-panel", "Enerji paneli", "The power panel", "⚡",
                    "Yalnız bir xana qalıb", "Only one cell is left",
                    new TraitDelta(TraitKeys.ProblemSolver, 2))
            ],
            Transitions:
            [
                On("look-desk", "c2-log"),
                On("look-corridor", "c2-bot"),
                On("look-panel", "c2-cell"),
                Fallback("c2-log")
            ],
            Effects: [])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c2-log",
            Kind: PetBrainStageKind.Investigation,
            PromptAz: "Qeyd dəftəri yarımçıq kəsilir",
            PromptEn: "The logbook stops mid-sentence",
            PetLineAz: "Bax: «Dalğa yaxınlaşır. Kristalı qorumalıyıq.» Sonrası yoxdur.",
            PetLineEn: "Look: \"The wave is coming. We must protect the crystal.\" Nothing after that.",
            Options: [],
            Transitions: [Fallback("c2-cell")],
            Effects: [Clue(MoonKeys.ClueBaseLog), Scene("moon-base-log")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c2-bot",
            Kind: PetBrainStageKind.ObjectInteraction,
            PromptAz: "Dəhlizdə kiçik bir robot ilişib",
            PromptEn: "A little robot is stuck in the corridor",
            PetLineAz: "Balaca robot təkərinə görə tərpənə bilmir. Kömək edək?",
            PetLineEn: "The little robot cannot move because of its wheel. Shall we help?",
            Options:
            [
                Option("bot-fix", "Təmir edək", "Let us fix it", "🔧",
                    "Bir neçə dəqiqə çəkər", "It will take a few minutes",
                    new TraitDelta(TraitKeys.Caring, 3)),

                Option("bot-later", "Sonra qayıdaq", "Come back later", "⏭️",
                    "İndi bazanı işə salaq", "Let us power the base first",
                    new TraitDelta(TraitKeys.ProblemSolver, 1))
            ],
            Transitions:
            [
                On("bot-fix", "c2-bot-fixed"),
                On("bot-later", "c2-cell"),
                Fallback("c2-cell")
            ],
            Effects: [Start(MoonKeys.SideServiceBot), Scene("moon-corridor-bot")])
        {
            ChapterId = chapter,
            IsOptional = true,
            NpcKey = MoonKeys.NpcServiceBot
        };

        yield return new ExperienceNode(
            Id: "c2-bot-fixed",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Robot yenidən hərəkət edir",
            PromptEn: "The robot moves again",
            PetLineAz: "Baxsana, quyruğu var! O, bizə bir enerji xanası uzatdı — deyəsən təşəkkür edir.",
            PetLineEn: "Look, it has a tail! It handed us a power cell — I think that is a thank you.",
            Options: [],
            Transitions: [Fallback("c2-cell")],
            Effects:
            [
                Done(MoonKeys.SideServiceBot),
                Item(MoonKeys.ItemPowerCell),
                Score(MoonKeys.EndingRobotFriend, 2),
                World(MoonKeys.WorldBaseAwake),
                Scene("moon-corridor-happy")
            ])
        {
            ChapterId = chapter,
            NpcKey = MoonKeys.NpcServiceBot
        };

        yield return new ExperienceNode(
            Id: "c2-cell",
            Kind: PetBrainStageKind.Narration,
            PromptAz: "Bir enerji xanası qalıb",
            PromptEn: "One power cell is left",
            PetLineAz: "Panelin içində bir dənə xana var. Onu yalnız BİR sistemə verə bilərik.",
            PetLineEn: "There is one cell in the panel. We can give it to only ONE system.",
            Options: [],
            Transitions: [Fallback("c2-power")],
            Effects: [Item(MoonKeys.ItemPowerCell), Scene("moon-power-panel")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c2-power",
            Kind: PetBrainStageKind.Choice,
            PromptAz: "Enerjini hansı sistemə verək?",
            PromptEn: "Which system gets the energy?",
            PetLineAz: "Bu, çətin qərardır. Hansını seçsək, digər ikisi qaranlıqda qalacaq.",
            PetLineEn: "This is a hard call. Whichever we pick, the other two stay dark.",
            Options:
            [
                Option("power-map", "Xəritə sisteminə", "To the map system", "🗺️",
                    "Gizli yollar görünəcək", "Hidden paths will appear",
                    new TraitDelta(TraitKeys.Explorer, 3), new TraitDelta(TraitKeys.Space, 1)) with
                {
                    Requires = HasItem(MoonKeys.ItemPowerCell),
                    Effects = [Spend(MoonKeys.ItemPowerCell)]
                },

                Option("power-comms", "Rabitə sisteminə", "To the comms system", "📻",
                    "Kimsə bizə cavab verə bilər", "Someone might answer us",
                    new TraitDelta(TraitKeys.Science, 3), new TraitDelta(TraitKeys.Caring, 1)) with
                {
                    Requires = HasItem(MoonKeys.ItemPowerCell),
                    Effects = [Spend(MoonKeys.ItemPowerCell)]
                },

                Option("power-repair", "Təmir stansiyasına", "To the repair station", "🛠️",
                    "Alətimiz güclənəcək", "Our tool will get stronger",
                    new TraitDelta(TraitKeys.ProblemSolver, 3), new TraitDelta(TraitKeys.Creative, 1)) with
                {
                    Requires = HasItem(MoonKeys.ItemPowerCell),
                    Effects = [Spend(MoonKeys.ItemPowerCell)]
                }
            ],
            Transitions:
            [
                On("power-map", "c2-map-on"),
                On("power-comms", "c2-comms-on"),
                On("power-repair", "c2-repair-on"),
                Fallback("c2-map-on")
            ],
            Effects: [])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c2-map-on",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Xəritə işıqlandı",
            PromptEn: "The map lit up",
            PetLineAz: "Bütün divar bir xəritəyə çevrildi! Burada gizli bir keçid var — heç kim onu qeyd etməyib.",
            PetLineEn: "The whole wall turned into a map! There is a hidden passage here that nobody wrote down.",
            Options: [],
            Transitions: [Fallback("c2-route")],
            Effects:
            [
                Flag(MoonKeys.FlagMapPowered),
                Item(MoonKeys.ItemMoonMap),
                Done(MoonKeys.ObjectivePowerBase),
                Score(MoonKeys.EndingExplorer, 2),
                Scene("moon-map-wall")
            ])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c2-comms-on",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Rabitə xırıltı ilə açıldı",
            PromptEn: "The comms crackled open",
            PetLineAz: "Dinlə! Zəif bir səs: «…burada… mən hələ də buradayam…» Kimsə cavab verir!",
            PetLineEn: "Listen! A faint voice: \"…here… I am still here…\" Someone is answering!",
            Options: [],
            Transitions: [Fallback("c2-route")],
            Effects:
            [
                Flag(MoonKeys.FlagCommsPowered),
                Done(MoonKeys.ObjectivePowerBase),
                Score(MoonKeys.EndingRobotFriend, 2),
                Scene("moon-comms-room")
            ])
        {
            ChapterId = chapter,
            NpcKey = MoonKeys.NpcRover
        };

        yield return new ExperienceNode(
            Id: "c2-repair-on",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Təmir stansiyası işə düşdü",
            PromptEn: "The repair station started up",
            PetLineAz: "Alətini stansiyaya qoydum və o, onu gücləndirdi. İndi o, əvvəlkindən çox şey bacarır.",
            PetLineEn: "I put your tool in the station and it made it stronger. Now it can do much more than before.",
            Options: [],
            Transitions: [Fallback("c2-route")],
            Effects:
            [
                Flag(MoonKeys.FlagRepairPowered),
                Item(MoonKeys.ItemUpgradedTool),
                Done(MoonKeys.ObjectivePowerBase),
                Score(MoonKeys.EndingGuardian, 2),
                Scene("moon-repair-station")
            ])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c2-route",
            Kind: PetBrainStageKind.Puzzle,
            PromptAz: "Enerjini xəttə çək",
            PromptEn: "Route the energy",
            PetLineAz: "Kabellər qopub. Enerji paylayıcı qutudan keçməsə, seçdiyimiz sistem oyanmayacaq.",
            PetLineEn: "The cables are torn. If the energy does not pass the relay box, the system we chose will not wake.",
            Options: [],
            Transitions:
            [
                new ExperienceTransition("c2-truth", RequiredResult: PetBrainStageResult.Solved, Priority: 10),
                Fallback("c2-truth")
            ],
            Effects: [Scene("moon-power-lines")],
            PuzzleFamily: PuzzleBlueprintCatalog.MoonBasePowerKey)
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c2-truth",
            Kind: PetBrainStageKind.Narration,
            PromptAz: "Kristal üç yerə bölünüb",
            PromptEn: "The crystal broke into three",
            PetLineAz: "Ekranda kristalın şəkli var… amma o, bütöv deyil. Üç parçaya bölünüb və hər parça ayrı yerdədir.",
            PetLineEn: "There is a picture of the crystal on the screen… but it is not whole. It is in three pieces, each somewhere else.",
            Options: [],
            Transitions: [Fallback("c2-recap")],
            Effects: [Clue(MoonKeys.ClueThreePieces), Scene("moon-crystal-broken")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c2-recap",
            Kind: PetBrainStageKind.ChapterRecap,
            PromptAz: "İkinci fəsil bitdi",
            PromptEn: "Chapter two is done",
            PetLineAz: "Bazanı oyatdıq və kristalın parçalandığını öyrəndik. Növbəti addım: birinci parçanı tapmaq.",
            PetLineEn: "We woke the base and learned the crystal is broken. Next step: find the first shard.",
            Options: [],
            Transitions: [Fallback("c3-fork")],
            Effects: [])
        {
            ChapterId = chapter,
            IsCheckpoint = true,
            CompletesChapterId = chapter
        };
    }

    private static IEnumerable<ExperienceNode> ChapterThree()
    {
        const string chapter = MoonKeys.Chapter3;

        yield return new ExperienceNode(
            Id: "c3-fork",
            Kind: PetBrainStageKind.RouteSelection,
            PromptAz: "İki yol açılır",
            PromptEn: "Two roads open",
            PetLineAz: "Birinci parça bu iki yerdən birindədir. Hansına gedirik? Digərinə sonra da qayıda bilərik.",
            PetLineEn: "The first shard is in one of these two places. Which way? We can come back to the other later.",
            Options:
            [
                Option("route-cave", "Kristal mağarası", "The crystal cave", "🕳️",
                    "İşıq və səs orada oynayır", "Light and sound play in there",
                    new TraitDelta(TraitKeys.Creative, 3), new TraitDelta(TraitKeys.Stories, 1)),

                Option("route-crater", "Kölgəli krater", "The shadowed crater", "🌑",
                    "Orada təkər izləri var", "There are wheel tracks there",
                    new TraitDelta(TraitKeys.Explorer, 3), new TraitDelta(TraitKeys.ProblemSolver, 1)),

                Option("route-hidden", "Gizli keçid", "The hidden passage", "🗺️",
                    "Xəritədə qeyd olunmayan yol", "A path nobody wrote down",
                    new TraitDelta(TraitKeys.Explorer, 2), new TraitDelta(TraitKeys.Science, 2)) with
                {
                    Requires = HasItem(MoonKeys.ItemMoonMap)
                }
            ],
            Transitions:
            [
                On("route-cave", "c3-cave-enter"),
                On("route-crater", "c3-crater-enter"),
                On("route-hidden", "c3-hidden"),
                Fallback("c3-cave-enter")
            ],
            Effects: [Start(MoonKeys.ObjectiveFirstShard), Scene("moon-fork")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c3-hidden",
            Kind: PetBrainStageKind.Navigation,
            PromptAz: "Xəritədəki xətt bizi apardı",
            PromptEn: "The line on the map led us through",
            PetLineAz: "Bu keçid heç bir kitabda yoxdur! Divarda köhnə bir qeyd var — bazadakı ilə eyni əl yazısı.",
            PetLineEn: "This passage is in no book! There is an old note on the wall — the same handwriting as at the base.",
            Options: [],
            Transitions: [Fallback("c3-cave-enter")],
            Effects:
            [
                Clue(MoonKeys.ClueBaseLog),
                Score(MoonKeys.EndingExplorer, 2),
                Scene("moon-hidden-passage")
            ])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c3-cave-enter",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Mağara nəfəs alır",
            PromptEn: "The cave is breathing",
            PetLineAz: "Divarlar hər səsi geri qaytarır və işıq həmin ritmlə yanıb-sönür. Mağara bizimlə danışır!",
            PetLineEn: "The walls send every sound back and the light pulses with it. The cave is talking to us!",
            Options: [],
            Transitions: [Fallback("c3-cave-order")],
            Effects: [Flag(MoonKeys.FlagCaveRoute), Clue(MoonKeys.ClueCaveSong), Scene("moon-cave-echo")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c3-cave-creature",
            Kind: PetBrainStageKind.Choice,
            PromptAz: "Kiçik bir canlı işığa sığınıb",
            PromptEn: "A small creature is hiding in the light",
            PetLineAz: "Bax, orada balaca, işıqlı bir canlı var. Qorxub. Nə edək?",
            PetLineEn: "Look, there is a tiny glowing creature there. It is scared. What do we do?",
            Options:
            [
                Option("cave-help", "Ona körpü quraq", "Build it a bridge", "🌉",
                    "Kristal parçalarından", "Out of crystal pieces",
                    new TraitDelta(TraitKeys.Caring, 3), new TraitDelta(TraitKeys.Creative, 1)),

                Option("cave-watch", "Sakitcə izləyək", "Watch quietly", "🤫",
                    "Bəlkə özü yol göstərər", "Maybe it will lead the way",
                    new TraitDelta(TraitKeys.Explorer, 2), new TraitDelta(TraitKeys.Science, 1))
            ],
            Transitions:
            [
                On("cave-help", "c3-cave-bridge"),
                On("cave-watch", "c3-cave-follow"),
                Fallback("c3-cave-follow")
            ],
            Effects: [])
        {
            ChapterId = chapter,
            NpcKey = MoonKeys.NpcCaveCreature
        };

        yield return new ExperienceNode(
            Id: "c3-cave-order",
            Kind: PetBrainStageKind.Puzzle,
            PromptAz: "Mağaranın işıqlarını sıraya düz",
            PromptEn: "Line up the cave lights",
            PetLineAz: "Səs kiçikdən böyüyə gedir — işıqları da elə yandırsaq, mağara bizə yol açacaq.",
            PetLineEn: "The sound grows from small to big — light the crystals the same way and the cave will open a path.",
            Options: [],
            Transitions:
            [
                new ExperienceTransition("c3-cave-creature", RequiredResult: PetBrainStageResult.Solved, Priority: 10),
                Fallback("c3-cave-creature")
            ],
            Effects: [Scene("moon-cave-lights")],
            PuzzleFamily: PuzzleBlueprintCatalog.SequenceOrderKey)
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c3-cave-bridge",
            Kind: PetBrainStageKind.Building,
            PromptAz: "Kristal körpüsü qurulur",
            PromptEn: "A crystal bridge takes shape",
            PetLineAz: "Parçaları bir-birinin üstünə qoyduq və işıq onların içindən keçdi. Canlı körpünü keçdi!",
            PetLineEn: "We stacked the pieces and the light ran through them. The creature crossed the bridge!",
            Options: [],
            Transitions: [Fallback("c3-cave-shard")],
            Effects:
            [
                Score(MoonKeys.EndingGuardian, 1),
                Score(MoonKeys.EndingRobotFriend, 1),
                Scene("moon-cave-bridge")
            ])
        {
            ChapterId = chapter,
            NpcKey = MoonKeys.NpcCaveCreature,
            PetAbilityKey = MoonKeys.AbilityGlow
        };

        yield return new ExperienceNode(
            Id: "c3-cave-follow",
            Kind: PetBrainStageKind.StealthObservation,
            PromptAz: "Canlı bizi apardı",
            PromptEn: "The creature led the way",
            PetLineAz: "Səssiz qaldıq və o, özü qabağa düşdü. Bizi düz parçanın yanına apardı!",
            PetLineEn: "We stayed quiet and it went ahead on its own. It took us right to the shard!",
            Options: [],
            Transitions: [Fallback("c3-cave-shard")],
            Effects: [Score(MoonKeys.EndingExplorer, 1), Scene("moon-cave-follow")])
        {
            ChapterId = chapter,
            NpcKey = MoonKeys.NpcCaveCreature
        };

        yield return new ExperienceNode(
            Id: "c3-cave-shard",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Birinci parça bizdədir",
            PromptEn: "The first shard is ours",
            PetLineAz: "Bax! Soyuq və sakit parlayır. Bu, kristalın birinci parçasıdır.",
            PetLineEn: "Look! It glows cool and quiet. This is the crystal's first shard.",
            Options: [],
            Transitions: [Fallback("c3-recap")],
            Effects: [Item(MoonKeys.ItemShardOne), Done(MoonKeys.ObjectiveFirstShard), Scene("moon-shard-cave")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c3-crater-enter",
            Kind: PetBrainStageKind.Navigation,
            PromptAz: "Krater qaranlıq və dərindir",
            PromptEn: "The crater is dark and deep",
            PetLineAz: "Burada işıq az, amma yerdə təkər izləri var. Kimsə buradan keçib!",
            PetLineEn: "There is little light here, but there are wheel tracks on the ground. Someone came through!",
            Options: [],
            Transitions: [Fallback("c3-crater-path")],
            Effects: [Flag(MoonKeys.FlagCraterRoute), Clue(MoonKeys.ClueCraterTracks), Scene("moon-crater-dark")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c3-crater-path",
            Kind: PetBrainStageKind.Choice,
            PromptAz: "Aşağı necə enək?",
            PromptEn: "How do we get down?",
            PetLineAz: "Dik yamacdır. Ehtiyatlı olmalıyıq.",
            PetLineEn: "It is a steep slope. We need to be careful.",
            Options:
            [
                Option("crater-light", "Kristalla işıqlandır", "Light it with the crystal", "🔆",
                    "Hər addımı görək", "So we see every step",
                    new TraitDelta(TraitKeys.Creative, 2)) with
                {
                    Requires = HasItem(MoonKeys.ToolLightCrystal)
                },

                Option("crater-rope", "Kəndirlə enək", "Go down on a rope", "🪢",
                    "Yavaş, amma etibarlı", "Slow, but safe",
                    new TraitDelta(TraitKeys.ProblemSolver, 2)),

                Option("crater-pet", "Pet qabaqda getsin", "Let your pet lead", "🐾",
                    "O, qaranlıqda yaxşı görür", "It sees well in the dark",
                    new TraitDelta(TraitKeys.Caring, 2))
            ],
            Transitions:
            [
                On("crater-light", "c3-crater-lit"),
                On("crater-rope", "c3-crater-slow"),
                On("crater-pet", "c3-crater-slow"),
                Fallback("c3-crater-slow")
            ],
            Effects: [])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c3-crater-lit",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Kristal bütün krateri işıqlandırdı",
            PromptEn: "The crystal lit the whole crater",
            PetLineAz: "Vay! İşıq divarlara dəydi və orada başqa izlər də göründü — biri yuxarı gedir.",
            PetLineEn: "Wow! The light hit the walls and more tracks appeared — one of them leads upward.",
            Options: [],
            Transitions: [Fallback("c3-crater-safe")],
            Effects: [Clue(MoonKeys.ClueRoverLast), Score(MoonKeys.EndingExplorer, 1), Scene("moon-crater-lit")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c3-crater-slow",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Yavaş, amma sağ-salamat",
            PromptEn: "Slow, but safe",
            PetLineAz: "Addım-addım endik. Uzun çəkdi, amma aşağıda parıltı görünür!",
            PetLineEn: "We came down step by step. It took a while, but there is a glimmer below!",
            Options: [],
            Transitions: [Fallback("c3-crater-safe")],
            Effects: [Score(MoonKeys.EndingGuardian, 1), Scene("moon-crater-descend")])
        {
            ChapterId = chapter,
            PetAbilityKey = MoonKeys.AbilityDig
        };

        yield return new ExperienceNode(
            Id: "c3-crater-safe",
            Kind: PetBrainStageKind.Puzzle,
            PromptAz: "Təhlükəsiz keçidi seç",
            PromptEn: "Pick the safe crossing",
            PetLineAz: "Bəzi yollar bağlıdır. Açıq olanların ən qısasını tapaq — enerjimiz az qalıb.",
            PetLineEn: "Some paths are blocked. Let us find the shortest open one — our energy is running low.",
            Options: [],
            Transitions:
            [
                new ExperienceTransition("c3-crater-shard", RequiredResult: PetBrainStageResult.Solved, Priority: 10),
                Fallback("c3-crater-shard")
            ],
            Effects: [Scene("moon-crater-crossing")],
            PuzzleFamily: PuzzleBlueprintCatalog.RouteLogicKey)
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c3-crater-shard",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Birinci parça bizdədir",
            PromptEn: "The first shard is ours",
            PetLineAz: "Bax, daşların arasında! Soyuq və sakit parlayır — bu, kristalın birinci parçasıdır.",
            PetLineEn: "Look, between the rocks! It glows cool and quiet — this is the crystal's first shard.",
            Options: [],
            Transitions: [Fallback("c3-recap")],
            Effects: [Item(MoonKeys.ItemShardOne), Done(MoonKeys.ObjectiveFirstShard), Scene("moon-shard-crater")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c3-recap",
            Kind: PetBrainStageKind.ChapterRecap,
            PromptAz: "Üçüncü fəsil bitdi",
            PromptEn: "Chapter three is done",
            PetLineAz: "Birinci parça çantadadır. Amma o izlər… kimsə bizdən əvvəl buradaydı. Onu tapmalıyıq.",
            PetLineEn: "The first shard is in the kit. But those tracks… someone was here before us. We need to find them.",
            Options: [],
            Transitions: [Fallback("c4-tracks")],
            Effects: [Remember("moon-route")])
        {
            ChapterId = chapter,
            IsCheckpoint = true,
            CompletesChapterId = chapter
        };
    }
}
