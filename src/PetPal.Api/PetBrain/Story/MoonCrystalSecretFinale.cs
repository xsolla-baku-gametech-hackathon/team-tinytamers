using PetPal.Api.PetBrain.Puzzles;
using PetPal.Shared.Enums;
using static PetPal.Api.PetBrain.Story.Moon;

namespace PetPal.Api.PetBrain.Story;

/// <summary>
/// «Ay Kristalının Sirri» — chapter 4–6.
///
/// <para>Burada macəranın vədləri ÖDƏNİR: chapter 2-dəki enerji seçimi
/// chapter 4-də görünür, chapter 3-dəki yol chapter 5-dəki lövhəni dəyişir,
/// və finalda hər ikisi eyni anda oxunur. Gecikmiş nəticə məhz budur —
/// seçimin sonda görünməsi.</para>
/// </summary>
internal static class MoonChaptersFourToSix
{
    public static IEnumerable<ExperienceNode> Nodes()
    {
        foreach (var node in ChapterFour()) yield return node;
        foreach (var node in ChapterFive()) yield return node;
        foreach (var node in ChapterSix()) yield return node;
    }

    private static IEnumerable<ExperienceNode> ChapterFour()
    {
        const string chapter = MoonKeys.Chapter4;

        yield return new ExperienceNode(
            Id: "c4-tracks",
            Kind: PetBrainStageKind.Intro,
            PromptAz: "İzlər şərqə gedir",
            PromptEn: "The tracks lead east",
            PetLineAz: "Bu izlər təzə deyil, amma silinməyib də. Onları izləsək, sahibini taparıq.",
            PetLineEn: "These tracks are not fresh, but they have not faded either. Follow them and we find their owner.",
            Options: [],
            Transitions:
            [
                If(Chose(MoonKeys.RoleTracker), "c4-tracker-eye", 10),
                Fallback("c4-recall")
            ],
            Effects: [Start(MoonKeys.ObjectiveFindRover), Scene("moon-tracks-east")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c4-tracker-eye",
            Kind: PetBrainStageKind.Navigation,
            PromptAz: "İzçi gözü",
            PromptEn: "A tracker's eye",
            PetLineAz: "Sən izçisən — izləri dərhal tanıdın: bu, kraterdən keçən roverin təkərləridir. Jurnala yazdım!",
            PetLineEn: "You are the tracker — you knew the marks at once: the wheels of a rover that crossed the crater. I wrote it down!",
            Options: [],
            Transitions: [Fallback("c4-recall")],
            Effects:
            [
                Clue(MoonKeys.ClueCraterTracks),
                Score(MoonKeys.EndingExplorer, 1),
                Scene("moon-tracker-eye")
            ])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c4-recall",
            Kind: PetBrainStageKind.Puzzle,
            PromptAz: "İzlərdə nə dəyişib?",
            PromptEn: "What changed in the tracks?",
            PetLineAz: "Jurnalındakı qeydi aç və indiki izlərlə tutuşdur. Nəsə fərqlidir.",
            PetLineEn: "Open the note in your journal and compare it with the tracks now. Something is different.",
            Options: [],
            Transitions:
            [
                new ExperienceTransition("c4-found", RequiredResult: PetBrainStageResult.Solved, Priority: 10),
                Fallback("c4-found")
            ],
            Effects: [Scene("moon-track-compare")],
            PuzzleFamily: PuzzleBlueprintCatalog.MoonTrackRecallKey)
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c4-found",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Rover tapıldı",
            PromptEn: "The rover is found",
            PetLineAz: "Orada! Qum təpəsinin arxasında dayanıb. İşıqları sönük, amma bir şey hələ də zəif yanıb-sönür.",
            PetLineEn: "There! It is parked behind the dune. Its lights are dim, but something still blinks faintly.",
            Options: [],
            Transitions: [Fallback("c4-rover-choice")],
            Effects:
            [
                Done(MoonKeys.ObjectiveFindRover),
                Item(MoonKeys.ItemShardTwo),
                Scene("moon-rover-found")
            ])
        {
            ChapterId = chapter,
            NpcKey = MoonKeys.NpcRover
        };

        yield return new ExperienceNode(
            Id: "c4-rover-choice",
            Kind: PetBrainStageKind.Choice,
            PromptAz: "Roverlə nə edək?",
            PromptEn: "What do we do with the rover?",
            PetLineAz: "O, çox şey görüb. Amma indi zəifdir. Sənin planın nədir?",
            PetLineEn: "It has seen a lot. But it is weak now. What is your plan?",
            Options:
            [
                Option("rover-home", "Bazaya qaytaraq", "Take it back to base", "🏠",
                    "Orada təhlükəsizdir", "It will be safe there",
                    new TraitDelta(TraitKeys.Caring, 3)),

                Option("rover-memory", "Əvvəl yaddaşını bərpa edək", "Restore its memory first", "💾",
                    "Nə gördüyünü öyrənək", "Let us learn what it saw",
                    new TraitDelta(TraitKeys.Science, 3)),

                Option("rover-team", "Birlikdə işləyək", "Work together", "🤝",
                    "O, bizə yol göstərsin", "Let it guide us",
                    new TraitDelta(TraitKeys.ProblemSolver, 2), new TraitDelta(TraitKeys.Caring, 1)) with
                {
                    Requires = WithFlag(MoonKeys.FlagCommsPowered)
                }
            ],
            Transitions:
            [
                On("rover-home", "c4-rover-home"),
                On("rover-memory", "c4-rover-memory"),
                On("rover-team", "c4-rover-team"),
                Fallback("c4-rover-home")
            ],
            Effects: [])
        {
            ChapterId = chapter,
            NpcKey = MoonKeys.NpcRover
        };

        yield return new ExperienceNode(
            Id: "c4-rover-home",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Rover bazaya çatdı",
            PromptEn: "The rover reached the base",
            PetLineAz: "Onu ehtiyatla apardıq. Bazada isindi və bizə son gördüyünü göstərdi.",
            PetLineEn: "We took it back carefully. It warmed up at the base and showed us what it saw last.",
            Options: [],
            Transitions:
            [
                If(NotIn(AdventureVariants.Short), "c4-side-offer", 10),
                Fallback("c4-recap")
            ],
            Effects:
            [
                Clue(MoonKeys.ClueRoverLast),
                Score(MoonKeys.EndingGuardian, 2),
                World(MoonKeys.WorldRoverAwake),
                Scene("moon-rover-base")
            ])
        {
            ChapterId = chapter,
            NpcKey = MoonKeys.NpcRover
        };

        yield return new ExperienceNode(
            Id: "c4-rover-memory",
            Kind: PetBrainStageKind.ObjectInteraction,
            PromptAz: "Yaddaş yavaş-yavaş qayıdır",
            PromptEn: "The memory comes back slowly",
            PetLineAz: "Ekranda şəkillər göründü: göydə parlaq bir zolaq… sonra hər şey susub.",
            PetLineEn: "Pictures appeared on its screen: a bright streak in the sky… and then everything went quiet.",
            Options: [],
            Transitions:
            [
                If(NotIn(AdventureVariants.Short), "c4-side-offer", 10),
                Fallback("c4-recap")
            ],
            Effects:
            [
                Clue(MoonKeys.ClueRoverLast),
                Item(MoonKeys.ItemRoverMemory),
                Start(MoonKeys.SideRoverMemory),
                Done(MoonKeys.SideRoverMemory),
                Score(MoonKeys.EndingExplorer, 2),
                World(MoonKeys.WorldRoverAwake),
                Scene("moon-rover-memory")
            ])
        {
            ChapterId = chapter,
            NpcKey = MoonKeys.NpcRover
        };

        yield return new ExperienceNode(
            Id: "c4-rover-team",
            Kind: PetBrainStageKind.CooperativePetAction,
            PromptAz: "Rover komandaya qoşuldu",
            PromptEn: "The rover joined the team",
            PetLineAz: "Rabitə açıq idi, ona görə o, bizi eşitdi! İndi üçümüzük — və o, yolu bizdən yaxşı bilir.",
            PetLineEn: "The comms were open, so it heard us! Now there are three of us — and it knows the way better than we do.",
            Options: [],
            Transitions:
            [
                If(NotIn(AdventureVariants.Short), "c4-side-offer", 10),
                Fallback("c4-recap")
            ],
            Effects:
            [
                Clue(MoonKeys.ClueRoverLast),
                Flag(MoonKeys.FlagRoverHelps),
                Score(MoonKeys.EndingRobotFriend, 3),
                World(MoonKeys.WorldRoverAwake),
                Scene("moon-rover-team")
            ])
        {
            ChapterId = chapter,
            NpcKey = MoonKeys.NpcRover,
            PetAbilityKey = MoonKeys.AbilityListen
        };

        yield return new ExperienceNode(
            Id: "c4-side-offer",
            Kind: PetBrainStageKind.Choice,
            PromptAz: "Yaxınlıqda nəsə var",
            PromptEn: "There is something nearby",
            PetLineAz: "Bir az vaxtımız var. Yaxınlıqda köhnə Ay bağçası görünür — istəsən baxarıq, istəməsən düz gedərik.",
            PetLineEn: "We have a little time. There is an old Moon garden nearby — we can look, or we can go straight on.",
            Options:
            [
                Option("side-garden", "Bağçaya baxaq", "Visit the garden", "🌱",
                    "Bir neçə dəqiqə çəkər", "It will take a few minutes",
                    new TraitDelta(TraitKeys.Caring, 2), new TraitDelta(TraitKeys.Nature, 1)),

                Option("side-skip", "Düz rəsədxanaya", "Straight to the observatory", "➡️",
                    "Kristal gözləyir", "The crystal is waiting",
                    new TraitDelta(TraitKeys.ProblemSolver, 1))
            ],
            Transitions:
            [
                On("side-garden", "c4-garden"),
                On("side-skip", "c4-recap"),
                Fallback("c4-recap")
            ],
            Effects: [])
        {
            ChapterId = chapter,
            IsOptional = true,
            Requires = NotIn(AdventureVariants.Short)
        };

        yield return new ExperienceNode(
            Id: "c4-garden",
            Kind: PetBrainStageKind.Caring,
            PromptAz: "Ay bağçası susub",
            PromptEn: "The Moon garden is quiet",
            PetLineAz: "Bitkilər hələ sağdır, sadəcə işıqsız qalıblar. Bir az enerji bəs edər.",
            PetLineEn: "The plants are still alive, they just lost their light. A little energy would be enough.",
            Options: [],
            Transitions: [Fallback("c4-recap")],
            Effects:
            [
                Start(MoonKeys.SideMoonGarden),
                Done(MoonKeys.SideMoonGarden),
                Item(MoonKeys.ItemGardenSeed),
                World(MoonKeys.WorldGardenAlive),
                Score(MoonKeys.EndingGuardian, 1),
                Scene("moon-garden-glow")
            ])
        {
            ChapterId = chapter,
            IsOptional = true,
            PetAbilityKey = MoonKeys.AbilityGlow
        };

        yield return new ExperienceNode(
            Id: "c4-recap",
            Kind: PetBrainStageKind.ChapterRecap,
            PromptAz: "Dördüncü fəsil bitdi",
            PromptEn: "Chapter four is done",
            PetLineAz: "İkinci parça bizdədir və artıq bilirik ki, göydə nəsə baş verib. Rəsədxanada cavabı taparıq.",
            PetLineEn: "We have the second shard and we know something happened in the sky. The observatory will have the answer.",
            Options: [],
            Transitions: [Fallback("c5-board")],
            Effects: [Remember("moon-rover")])
        {
            ChapterId = chapter,
            IsCheckpoint = true,
            CompletesChapterId = chapter
        };
    }

    private static IEnumerable<ExperienceNode> ChapterFive()
    {
        const string chapter = MoonKeys.Chapter5;

        yield return new ExperienceNode(
            Id: "c5-board",
            Kind: PetBrainStageKind.Investigation,
            PromptAz: "İpucu lövhəsi",
            PromptEn: "The clue board",
            PetLineAz: "Bütün tapdıqlarımızı bura yığaq. Bax — hamısı bir şeyi göstərir.",
            PetLineEn: "Let us put everything we found up here. Look — they all point at one thing.",
            Options: [],
            Transitions:
            [
                If(Chose(MoonKeys.RoleScientist), "c5-connect-science", 5),
                If(HasClue(MoonKeys.ClueRoverLast), "c5-connect-rover", 10),
                If(HasClue(MoonKeys.ClueBaseLog), "c5-connect-log", 20),
                Fallback("c5-shards")
            ],
            Effects: [Start(MoonKeys.ObjectiveReadTruth), Scene("moon-clue-board")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c5-connect-science",
            Kind: PetBrainStageKind.Investigation,
            PromptAz: "Tədqiqatçı ölçmələri oxuyur",
            PromptEn: "The researcher reads the measurements",
            PetLineAz: "Sən tədqiqatçısan — lövhədəki rəqəmləri oxudun: bu dalğa Aya çox nadir hallarda gəlir. Deməli kristal onu gözləyirmiş!",
            PetLineEn: "You are the researcher — you read the numbers on the board: this wave reaches the Moon very rarely. So the crystal was waiting for it!",
            Options: [],
            Transitions:
            [
                If(HasClue(MoonKeys.ClueRoverLast), "c5-connect-rover", 10),
                If(HasClue(MoonKeys.ClueBaseLog), "c5-connect-log", 20),
                Fallback("c5-shards")
            ],
            Effects:
            [
                Clue(MoonKeys.ClueMeteorWave),
                Score(MoonKeys.EndingExplorer, 1),
                Scene("moon-board-science")
            ])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c5-connect-rover",
            Kind: PetBrainStageKind.Investigation,
            PromptAz: "Roverin gördüyü lövhəyə düşür",
            PromptEn: "What the rover saw goes on the board",
            PetLineAz: "Rover göydə parlaq zolaq görüb. Bu, adi işıq deyil — bir şey Aya tərəf gəlirdi.",
            PetLineEn: "The rover saw a bright streak in the sky. That was no ordinary light — something was coming toward the Moon.",
            Options: [],
            Transitions:
            [
                If(HasClue(MoonKeys.ClueBaseLog), "c5-connect-log", 10),
                Fallback("c5-shards")
            ],
            Effects: [Score(MoonKeys.EndingExplorer, 1), Scene("moon-board-rover")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c5-connect-log",
            Kind: PetBrainStageKind.Investigation,
            PromptAz: "Bazanın xəbərdarlığı yerinə oturur",
            PromptEn: "The base's warning falls into place",
            PetLineAz: "«Dalğa yaxınlaşır. Kristalı qorumalıyıq.» Onlar bilirdilər! Deməli kristal təsadüfən sınmayıb.",
            PetLineEn: "\"The wave is coming. We must protect the crystal.\" They knew! So the crystal did not break by accident.",
            Options: [],
            Transitions: [Fallback("c5-shards")],
            Effects: [Score(MoonKeys.EndingGuardian, 1), Scene("moon-board-log")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c5-shards",
            Kind: PetBrainStageKind.Puzzle,
            PromptAz: "Parçaları yuvasına qaytar",
            PromptEn: "Return the shards to their sockets",
            PetLineAz: "Rəsədxananın masasında yuvalar var. Hər parça öz yuvasına düşməlidir.",
            PetLineEn: "There are sockets on the observatory table. Each shard belongs in its own.",
            Options: [],
            Transitions:
            [
                new ExperienceTransition("c5-truth", RequiredResult: PetBrainStageResult.Solved, Priority: 10),
                Fallback("c5-truth")
            ],
            Effects: [Scene("moon-socket-table")],
            PuzzleFamily: PuzzleBlueprintCatalog.MoonShardMatchKey)
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c5-truth",
            Kind: PetBrainStageKind.Narration,
            PromptAz: "Kristal oğurlanmayıb",
            PromptEn: "The crystal was not stolen",
            PetLineAz: "İndi başa düşdüm! Meteorit dalğası gəlirdi və kristal onu əvvəlcədən gördü. O, rəsədxananı qorumaq üçün ÖZÜNÜ üç yerə böldü.",
            PetLineEn: "Now I understand! A meteor wave was coming and the crystal saw it first. It broke ITSELF into three to shield the observatory.",
            Options: [],
            Transitions: [Fallback("c5-decision")],
            Effects:
            [
                Clue(MoonKeys.ClueMeteorWave),
                Done(MoonKeys.ObjectiveReadTruth),
                Item(MoonKeys.ItemShardThree),
                Scene("moon-truth-reveal")
            ])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c5-decision",
            Kind: PetBrainStageKind.Choice,
            PromptAz: "İndi nə edək?",
            PromptEn: "What do we do now?",
            PetLineAz: "Üç parça bizdədir. Qərar sənindir — və düzü, hər üç yol da mənə doğru görünür.",
            PetLineEn: "We have all three shards. The call is yours — and honestly, all three roads look right to me.",
            Options:
            [
                Option("plan-shield", "Əvvəl qoruyucunu təmir edək", "Repair the shield first", "🛡️",
                    "Dalğa bir daha gələ bilər", "The wave could come again",
                    new TraitDelta(TraitKeys.Caring, 3), new TraitDelta(TraitKeys.Science, 1)),

                Option("plan-join", "Kristalı dərhal birləşdirək", "Join the crystal now", "💎",
                    "Sirri açıq görək", "Let us see the secret clearly",
                    new TraitDelta(TraitKeys.Science, 3), new TraitDelta(TraitKeys.Explorer, 1)),

                Option("plan-share", "Enerjini bazaya paylayaq", "Share the energy with the base", "🤖",
                    "Robotlar yenidən oyansın", "So the robots wake up again",
                    new TraitDelta(TraitKeys.Caring, 2), new TraitDelta(TraitKeys.Creative, 2)),

                Option("plan-song", "Mağaranın ritmini işlədək", "Use the cave's rhythm", "🔊",
                    "Kristal öz nəğməsini tanıyır", "The crystal knows its own song",
                    new TraitDelta(TraitKeys.Creative, 3), new TraitDelta(TraitKeys.Stories, 1)) with
                {
                    Requires = HasClue(MoonKeys.ClueCaveSong)
                }
            ],
            Transitions:
            [
                On("plan-shield", "c5-plan-shield"),
                On("plan-join", "c5-plan-join"),
                On("plan-share", "c5-plan-share"),
                On("plan-song", "c5-plan-song"),
                Fallback("c5-plan-shield")
            ],
            Effects: [])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c5-plan-shield",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Qoruyucu sistem hazırlanır",
            PromptEn: "The shield is being made ready",
            PetLineAz: "Qoruyucunun açarını tapdıq: o, kristalın öz ritmi ilə açılır. Finalda bu lazım olacaq.",
            PetLineEn: "We found the shield's key: it opens with the crystal's own rhythm. We will need this at the end.",
            Options: [],
            Transitions: [Fallback("c5-recap")],
            Effects: [Clue(MoonKeys.ClueShieldCode), Score(MoonKeys.EndingGuardian, 3), Scene("moon-shield-prep")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c5-plan-join",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Parçalar bir-birini tanıyır",
            PromptEn: "The shards recognise each other",
            PetLineAz: "Onları yaxınlaşdırdım və üçü də eyni anda işıqlandı. Kristal bizə yeni bir xəritə göstərir!",
            PetLineEn: "I brought them close and all three lit at once. The crystal is showing us a new map!",
            Options: [],
            Transitions: [Fallback("c5-recap")],
            Effects: [Score(MoonKeys.EndingExplorer, 3), Scene("moon-shards-join")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c5-plan-share",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Enerji bazaya axır",
            PromptEn: "The energy flows to the base",
            PetLineAz: "Bazanın işıqları bir-bir yandı. Dəhlizdə nəsə tərpəndi — orada bizdən başqa da kimsə var!",
            PetLineEn: "The base lights came on one by one. Something moved in the corridor — we are not the only ones here!",
            Options: [],
            Transitions: [Fallback("c5-recap")],
            Effects:
            [
                Score(MoonKeys.EndingRobotFriend, 3),
                World(MoonKeys.WorldBaseAwake),
                Scene("moon-base-awake")
            ])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c5-plan-song",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Mağaranın nəğməsi işə yaradı",
            PromptEn: "The cave's song worked",
            PetLineAz: "Mağarada eşitdiyimiz ritmi təkrarladım və kristal cavab verdi! O, öz nəğməsini tanıyır.",
            PetLineEn: "I repeated the rhythm we heard in the cave and the crystal answered! It knows its own song.",
            Options: [],
            Transitions: [Fallback("c5-recap")],
            Effects:
            [
                Clue(MoonKeys.ClueShieldCode),
                Score(MoonKeys.EndingGuardian, 2),
                Score(MoonKeys.EndingExplorer, 1),
                Scene("moon-cave-song-echo")
            ])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c5-recap",
            Kind: PetBrainStageKind.ChapterRecap,
            PromptAz: "Beşinci fəsil bitdi",
            PromptEn: "Chapter five is done",
            PetLineAz: "Artıq həqiqəti bilirik və planımız var. Qalan bir şeydir: Ay işığını geri qaytarmaq.",
            PetLineEn: "We know the truth now and we have a plan. One thing is left: bring the moonlight back.",
            Options: [],
            Transitions: [Fallback("c6-assemble")],
            Effects: [Remember("moon-plan")])
        {
            ChapterId = chapter,
            IsCheckpoint = true,
            CompletesChapterId = chapter
        };
    }

    private static IEnumerable<ExperienceNode> ChapterSix()
    {
        const string chapter = MoonKeys.Chapter6;

        yield return new ExperienceNode(
            Id: "c6-assemble",
            Kind: PetBrainStageKind.FinaleChallenge,
            PromptAz: "Final başlayır",
            PromptEn: "The finale begins",
            PetLineAz: "Rəsədxananın mərkəzindəyik. Üç parça, bir alət və bir plan. Gəl bunu birlikdə edək.",
            PetLineEn: "We are at the heart of the observatory. Three shards, one tool and one plan. Let us do this together.",
            Options: [],
            Transitions: [Fallback("c6-tool")],
            Effects: [Start(MoonKeys.ObjectiveRestoreLight), Scene("moon-observatory-core")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c6-tool",
            Kind: PetBrainStageKind.Choice,
            PromptAz: "Alətini işə sal",
            PromptEn: "Put your tool to work",
            PetLineAz: "İndi çantandakı alət lazımdır. Hansını işlədək?",
            PetLineEn: "Now we need the tool from your kit. Which one do we use?",
            Options:
            [
                Option("use-scanner", "Enerji skaneri", "Energy scanner", "📡",
                    "Dövrədəki qırığı tapaq", "Let us find the break in the circuit",
                    new TraitDelta(TraitKeys.Science, 2)) with
                {
                    Requires = HasItem(MoonKeys.ToolScanner)
                },

                Option("use-repair", "Mini təmir robotu", "Mini repair bot", "🔧",
                    "O, özü düzəltsin", "Let it do the repair",
                    new TraitDelta(TraitKeys.ProblemSolver, 2)) with
                {
                    Requires = HasItem(MoonKeys.ToolRepairBot)
                },

                Option("use-light", "İşıq kristalı", "Light crystal", "🔆",
                    "Yuvaları işıqlandıraq", "Let us light up the sockets",
                    new TraitDelta(TraitKeys.Creative, 2)) with
                {
                    Requires = HasItem(MoonKeys.ToolLightCrystal)
                },

                Option("use-upgraded", "Gücləndirilmiş alət", "The upgraded tool", "⚙️",
                    "Stansiya onu yaxşılaşdırmışdı", "The station made it better",
                    new TraitDelta(TraitKeys.ProblemSolver, 3)) with
                {
                    Requires = HasItem(MoonKeys.ItemUpgradedTool)
                },

                Option("use-engineer", "Mühəndis kimi dövrəni qur", "Build the circuit as the engineer", "🛠️",
                    "Bunu yalnız sən bacarırsan", "Only you know how to do this",
                    new TraitDelta(TraitKeys.ProblemSolver, 3)) with
                {
                    Requires = Chose(MoonKeys.RoleEngineer),
                    Effects = [Score(MoonKeys.EndingRobotFriend, 1)]
                },

                Option("use-hands", "Əl ilə edək", "Do it by hand", "🙌",
                    "Alətsiz də bacararıq", "We can manage without a tool",
                    new TraitDelta(TraitKeys.ProblemSolver, 1), new TraitDelta(TraitKeys.Caring, 1))
            ],
            Transitions:
            [
                On("use-scanner", "c6-tool-worked"),
                On("use-repair", "c6-tool-worked"),
                On("use-light", "c6-tool-worked"),
                On("use-upgraded", "c6-tool-upgraded"),
                On("use-engineer", "c6-tool-worked"),
                On("use-hands", "c6-tool-hands"),
                Fallback("c6-tool-hands")
            ],
            Effects: [])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c6-tool-worked",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Alət işə yaradı",
            PromptEn: "The tool did its job",
            PetLineAz: "Gördün? Çantanı yığanda verdiyin qərar məhz indi işə yaradı. Dövrə hazırdır!",
            PetLineEn: "See that? The choice you made packing the kit is exactly what paid off now. The circuit is ready!",
            Options: [],
            Transitions:
            [
                If(WithFlag(MoonKeys.FlagRoverHelps), "c6-helper", 10),
                Fallback("c6-helper-alone")
            ],
            Effects: [Step(MoonKeys.ObjectiveRestoreLight), Scene("moon-circuit-ready")])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c6-tool-upgraded",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Gücləndirilmiş alət hər şeyi bir anda etdi",
            PromptEn: "The upgraded tool did it all at once",
            PetLineAz: "Bazada təmir stansiyasını işə saldığın üçün bu alət indi qat-qat güclüdür. Dövrə bir anda bağlandı!",
            PetLineEn: "Because you powered the repair station back at the base, this tool is far stronger now. The circuit closed instantly!",
            Options: [],
            Transitions:
            [
                If(WithFlag(MoonKeys.FlagRoverHelps), "c6-helper", 10),
                Fallback("c6-helper-alone")
            ],
            Effects:
            [
                Step(MoonKeys.ObjectiveRestoreLight),
                Score(MoonKeys.EndingGuardian, 1),
                Score(MoonKeys.EndingRobotFriend, 1),
                Scene("moon-circuit-upgraded")
            ])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c6-tool-hands",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Əl ilə də alındı",
            PromptEn: "Hands were enough",
            PetLineAz: "Alət olmadan daha uzun çəkdi, amma sən dözdün və dövrəni özün bağladın. Bu, daha da qiymətlidir.",
            PetLineEn: "It took longer without a tool, but you stuck with it and closed the circuit yourself. That counts for more.",
            Options: [],
            Transitions:
            [
                If(WithFlag(MoonKeys.FlagRoverHelps), "c6-helper", 10),
                Fallback("c6-helper-alone")
            ],
            Effects:
            [
                Step(MoonKeys.ObjectiveRestoreLight),
                Score(MoonKeys.EndingGuardian, 1),
                Scene("moon-circuit-hands")
            ])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c6-helper",
            Kind: PetBrainStageKind.Narration,
            PromptAz: "Kömək gəlir",
            PromptEn: "Help arrives",
            PetLineAz: "Rover yanımızdadır! Chapter-lər əvvəl ona göstərdiyin qayğı indi geri qayıdır — o, ağır qapağı özü qaldırdı.",
            PetLineEn: "The rover is with us! The care you showed it chapters ago comes back now — it lifted the heavy hatch itself.",
            Options: [],
            Transitions:
            [
                If(HasItem(MoonKeys.ItemRoverMemory), "c6-memory-playback", 10),
                If(HasItem(MoonKeys.ItemGardenSeed), "c6-plant-seed", 20),
                Fallback("c6-pattern")
            ],
            Effects: [Score(MoonKeys.EndingRobotFriend, 2), Scene("moon-rover-helps")])
        {
            ChapterId = chapter,
            NpcKey = MoonKeys.NpcRover,
            Requires = WithFlag(MoonKeys.FlagRoverHelps)
        };

        yield return new ExperienceNode(
            Id: "c6-helper-alone",
            Kind: PetBrainStageKind.CooperativePetAction,
            PromptAz: "İkimiz bacardıq",
            PromptEn: "The two of us managed",
            PetLineAz: "Qapaq ağırdır, amma mən altından girdim, sən isə yuxarıdan itələdin. Komanda buna deyərlər!",
            PetLineEn: "The hatch is heavy, but I got underneath and you pushed from above. That is what a team is!",
            Options: [],
            Transitions:
            [
                If(HasItem(MoonKeys.ItemRoverMemory), "c6-memory-playback", 10),
                If(HasItem(MoonKeys.ItemGardenSeed), "c6-plant-seed", 20),
                Fallback("c6-pattern")
            ],
            Effects: [Scene("moon-pet-helps")])
        {
            ChapterId = chapter,
            PetAbilityKey = MoonKeys.AbilityDig
        };

        yield return new ExperienceNode(
            Id: "c6-memory-playback",
            Kind: PetBrainStageKind.ObjectInteraction,
            PromptAz: "Roverin yaddaşını səsləndir",
            PromptEn: "Play back the rover's memory",
            PetLineAz: "Roverin yaddaş hissəsi hələ bizdədir! Onu qoşdum və o, siqnalın ritmini dəqiq yazıbmış.",
            PetLineEn: "We still have the rover's memory piece! I plugged it in and it had recorded the signal's rhythm exactly.",
            Options: [],
            Transitions:
            [
                If(HasItem(MoonKeys.ItemGardenSeed), "c6-plant-seed", 10),
                Fallback("c6-pattern")
            ],
            Effects: [Score(MoonKeys.EndingExplorer, 2), Scene("moon-memory-playback")])
        {
            ChapterId = chapter,
            NpcKey = MoonKeys.NpcRover
        };

        yield return new ExperienceNode(
            Id: "c6-plant-seed",
            Kind: PetBrainStageKind.Caring,
            PromptAz: "Ay toxumunu əkək",
            PromptEn: "Plant the Moon seed",
            PetLineAz: "Bağçadan gətirdiyimiz toxumu rəsədxananın pəncərəsinin altına əkdim. İşıq qayıdanda o da böyüyəcək.",
            PetLineEn: "I planted the seed we brought from the garden under the observatory window. When the light returns, it will grow too.",
            Options: [],
            Transitions: [Fallback("c6-pattern")],
            Effects:
            [
                Score(MoonKeys.EndingGuardian, 2),
                World(MoonKeys.WorldGardenAlive),
                Scene("moon-plant-seed")
            ])
        {
            ChapterId = chapter,
            PetAbilityKey = MoonKeys.AbilityDig
        };

        yield return new ExperienceNode(
            Id: "c6-pattern",
            Kind: PetBrainStageKind.Puzzle,
            PromptAz: "Kristalın ritmini qaytar",
            PromptEn: "Give the crystal its rhythm back",
            PetLineAz: "Rəsədxana yalnız düzgün ritmlə oyanır. Sən bu naxışı artıq eşitmisən — indi onu geri qaytar.",
            PetLineEn: "The observatory only wakes to the right rhythm. You have heard this pattern before — now give it back.",
            Options: [],
            Transitions:
            [
                new ExperienceTransition("c6-light", RequiredResult: PetBrainStageResult.Solved, Priority: 10),
                Fallback("c6-light")
            ],
            Effects: [Scene("moon-rhythm-core")],
            PuzzleFamily: PuzzleBlueprintCatalog.MoonSignalPatternKey)
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c6-light",
            Kind: PetBrainStageKind.Consequence,
            PromptAz: "Rəsədxana oyandı",
            PromptEn: "The observatory woke up",
            PetLineAz: "İşıq mərkəzdən yayıldı və bütün Ay bir anda parladı. Biz bacardıq!",
            PetLineEn: "Light spread from the centre and the whole Moon flared at once. We did it!",
            Options: [],
            Transitions: [Fallback("c6-ending-route")],
            Effects:
            [
                Done(MoonKeys.ObjectiveRestoreLight),
                World(MoonKeys.WorldObservatoryOn),
                Scene("moon-light-returns")
            ])
        {
            ChapterId = chapter
        };

        yield return new ExperienceNode(
            Id: "c6-ending-route",
            Kind: PetBrainStageKind.Narration,
            PromptAz: "Ay bizə baxır",
            PromptEn: "The Moon is looking back at us",
            PetLineAz: "Bir an dayandıq və ətrafa baxdıq. Bu macərada etdiklərin indi görünür.",
            PetLineEn: "We stopped for a moment and looked around. Everything you did on this journey shows now.",
            Options: [],
            Transitions: [Fallback("c6-ending-guardian")],
            Effects: [Scene("moon-quiet-moment")])
        {
            ChapterId = chapter,
            ResolvesEnding = true
        };

        yield return Ending(
            "c6-ending-guardian", chapter, MoonKeys.EndingGuardian,
            "Ayın Qoruyucusu", "Guardian of the Moon",
            "Qoruyucu sistem tam bərpa olundu. Ay bir daha tək qalmayacaq — və bunu sən etdin.",
            "The shield is fully restored. The Moon will never be alone again — and you did that.",
            "moon-ending-guardian");

        yield return Ending(
            "c6-ending-explorer", chapter, MoonKeys.EndingExplorer,
            "Kristal Tədqiqatçısı", "Crystal Researcher",
            "Kristalın sirrini öyrəndik və o, bizə yeni bir xəritə verdi. Növbəti səfər artıq gözləyir.",
            "We learned the crystal's secret and it gave us a new map. The next trip is already waiting.",
            "moon-ending-explorer");

        yield return Ending(
            "c6-ending-robots", chapter, MoonKeys.EndingRobotFriend,
            "Robotların Dostu", "Friend of the Robots",
            "Baza yenidən işləyir və robotlar oyandı. Onlar səni xatırlayacaq.",
            "The base is running again and the robots are awake. They will remember you.",
            "moon-ending-robots");
    }

    private static ExperienceNode Ending(
        string id, string chapter, string endingKey,
        string titleAz, string titleEn,
        string petAz, string petEn,
        string scene) =>
        new(
            Id: id,
            Kind: PetBrainStageKind.Ending,
            PromptAz: titleAz,
            PromptEn: titleEn,
            PetLineAz: petAz,
            PetLineEn: petEn,
            Options: [],
            Transitions: [],
            Effects: [Remember("moon-ending")],
            SceneVariant: scene,
            EndingKey: endingKey)
        {
            ChapterId = chapter,
            IsCheckpoint = true,
            CompletesChapterId = chapter
        };
}
