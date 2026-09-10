using System.Runtime.CompilerServices;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Şəxsiləşdirilmiş tapmaca generatoru — SAF qat.
///
/// <para>Ən vacib iddia budur: <b>eyni kontekst həmişə eyni tapmacanı verir</b>,
/// amma tapmaca uşaqdan-uşağa MEXANİKA səviyyəsində dəyişir. İkincisi olmasa
/// "şəxsiləşdirmə" sadəcə ad dəyişməkdir.</para>
/// </summary>
public class PetBrainPuzzleTests
{
    private static readonly Guid AylinId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MiaId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RunId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly DeterministicPuzzleGenerator Generator = new();

    /// <summary>Həlledici/kəşfiyyatçı profil — məntiq tapmacası gözlənilir.</summary>
    private static PuzzleGenerationContext SolverContext(
        Guid? childId = null, Guid? runId = null, bool assisted = false) => new(
        ChildId: childId ?? AylinId,
        RunId: runId ?? RunId,
        StageIndex: 2,
        Age: 9,
        Language: "az",
        ExperienceType: PetBrainExperienceType.Adventure,
        Theme: TraitKeys.Space,
        Interests: new Dictionary<string, int>
        {
            [TraitKeys.Space] = 85,
            [TraitKeys.Science] = 75,
            [TraitKeys.Puzzles] = 90
        },
        PlayStyles: new Dictionary<string, int>
        {
            [TraitKeys.ProblemSolver] = 85,
            [TraitKeys.Explorer] = 75,
            [TraitKeys.Creative] = 10,
            [TraitKeys.Playful] = 25,
            [TraitKeys.Caring] = 25
        },
        Difficulty: PetBrainDifficulty.Medium,
        MasteryTargetDifficulty: 5,
        Assisted: assisted,
        RecentSignatures: new HashSet<string>());

    /// <summary>Kəşfiyyatçı profil — ELEMENT lövhəsi (marşrut məntiqi) gözlənilir.</summary>
    private static PuzzleGenerationContext ExplorerContext() => SolverContext() with
    {
        PlayStyles = new Dictionary<string, int>
        {
            [TraitKeys.Explorer] = 90,
            [TraitKeys.ProblemSolver] = 20,
            [TraitKeys.Playful] = 10,
            [TraitKeys.Caring] = 10
        }
    };

    /// <summary>Yaradıcı profil — təzyiqsiz yığım gözlənilir.</summary>
    private static PuzzleGenerationContext CreativeContext() => new(
        ChildId: MiaId,
        RunId: RunId,
        StageIndex: 2,
        Age: 8,
        Language: "az",
        ExperienceType: PetBrainExperienceType.Creative,
        Theme: TraitKeys.Fantasy,
        Interests: new Dictionary<string, int>
        {
            [TraitKeys.Fantasy] = 90,
            [TraitKeys.Animals] = 85,
            [TraitKeys.Stories] = 82,
            [TraitKeys.Puzzles] = 10
        },
        PlayStyles: new Dictionary<string, int>
        {
            [TraitKeys.Creative] = 88,
            [TraitKeys.Caring] = 60,
            [TraitKeys.ProblemSolver] = 20
        },
        Difficulty: PetBrainDifficulty.Medium,
        MasteryTargetDifficulty: 4,
        Assisted: false,
        RecentSignatures: new HashSet<string>());

    // ==================== Determinizm ====================

    /// <summary>Eyni kontekst → eyni tapmaca, eyni həll, eyni toxum.</summary>
    [Fact]
    public void EyniKontekst_EyniTapmacaniVerir()
    {
        var first = Generator.Generate(SolverContext());

        for (var i = 0; i < 5; i++)
        {
            var again = Generator.Generate(SolverContext());

            Assert.Equal(first.SeedHex, again.SeedHex);
            Assert.Equal(first.Signature, again.Signature);
            Assert.Equal(first.Blueprint.Key, again.Blueprint.Key);
            Assert.Equal(first.Public.TargetValue, again.Public.TargetValue);
            Assert.Equal(first.Solution.Ids, again.Solution.Ids);
            Assert.Equal(
                first.Public.Items.Select(x => $"{x.Id}:{x.Value}"),
                again.Public.Items.Select(x => $"{x.Id}:{x.Value}"));
        }
    }

    /// <summary>
    /// Toxumun KANONİK dəyəri sabitdir.
    ///
    /// <para>Bu, "eyni proses daxilində eyni nəticə" testindən güclüdür: burada
    /// gözlənilən hex açıq yazılıb, yəni kodlaşdırma (UTF-8, big-endian Guid,
    /// little-endian int, 0x1F ayırıcı) səhvən dəyişsə test dərhal sınır və
    /// köhnə run-lar bərpa olunmayan qalmır.</para>
    /// </summary>
    [Fact]
    public void ToxumMuqavilesi_SabitQalir()
    {
        var hex = PuzzleSeed.Hex(
            AylinId, RunId, PuzzleBlueprintCatalog.MarsSignalRouteKey,
            blueprintVersion: 1, targetDifficulty: 5, attempt: 0);

        Assert.Equal("b76bf6c05edbb2480d58d21e347558707acda1a2b7a6562b14af8bac627684fc", hex);
    }

    /// <summary>Toxum bütün sahələrdən asılıdır — biri dəyişsə nəticə də dəyişir.</summary>
    [Fact]
    public void Toxum_HerSahedenAsilidir()
    {
        var baseline = PuzzleSeed.Hex(AylinId, RunId, "mars-signal-route", 1, 5, 0);

        Assert.NotEqual(baseline, PuzzleSeed.Hex(MiaId, RunId, "mars-signal-route", 1, 5, 0));
        Assert.NotEqual(baseline, PuzzleSeed.Hex(AylinId, Guid.NewGuid(), "mars-signal-route", 1, 5, 0));
        Assert.NotEqual(baseline, PuzzleSeed.Hex(AylinId, RunId, "route-logic", 1, 5, 0));
        Assert.NotEqual(baseline, PuzzleSeed.Hex(AylinId, RunId, "mars-signal-route", 2, 5, 0));
        Assert.NotEqual(baseline, PuzzleSeed.Hex(AylinId, RunId, "mars-signal-route", 1, 6, 0));
        Assert.NotEqual(baseline, PuzzleSeed.Hex(AylinId, RunId, "mars-signal-route", 1, 5, 1));
    }

    /// <summary>
    /// Qonşu sahələr bir-birinə "yapışa" bilməz: ayırıcı olmasaydı
    /// <c>("ab", "c")</c> ilə <c>("a", "bc")</c> eyni toxum verərdi.
    /// </summary>
    [Fact]
    public void Toxum_SaheSerhedleriniQarisdirmir()
    {
        Assert.NotEqual(
            PuzzleSeed.Hex(AylinId, RunId, "ab", 1, 5, 0),
            PuzzleSeed.Hex(AylinId, RunId, "a", 1, 5, 0));
    }

    /// <summary>Fərqli run-lar adətən fərqli tapmaca verir — uşaq eynini görmür.</summary>
    [Fact]
    public void FerqliRunlar_FerqliBarmaqIziVerir()
    {
        var signatures = Enumerable.Range(0, 6)
            .Select(i => Generator.Generate(SolverContext(runId: Guid.Parse($"33333333-3333-3333-3333-00000000000{i}"))))
            .Select(p => p.Signature)
            .ToList();

        // Ən azı yarısı fərqli olmalıdır; tam unikallıq İDDİA EDİLMİR.
        Assert.True(signatures.Distinct().Count() >= signatures.Count / 2,
            $"Altı run-dan yalnız {signatures.Distinct().Count()} fərqli tapmaca çıxdı.");
    }

    /// <summary>Son tapmaca təkrarlanırsa generator BAŞQA namizədə keçir.</summary>
    [Fact]
    public void TekrarlananTapmaca_YenidenQurulur()
    {
        var first = Generator.Generate(SolverContext());

        var avoided = Generator.Generate(SolverContext() with
        {
            RecentSignatures = new HashSet<string> { first.Signature }
        });

        Assert.NotEqual(first.Signature, avoided.Signature);
    }

    // ==================== Etibarlılıq ====================

    /// <summary>
    /// Hər qurulan tapmaca yoxlamadan keçir və tək cavablı müqavilədə
    /// YEGANƏ həlli olur.
    /// </summary>
    [Theory]
    [InlineData(PetBrainDifficulty.Easy, 6)]
    [InlineData(PetBrainDifficulty.Medium, 8)]
    [InlineData(PetBrainDifficulty.Hard, 10)]
    public void QurulanTapmaca_HemiseEtibarlidir(PetBrainDifficulty tier, int age)
    {
        for (var i = 0; i < 30; i++)
        {
            var context = SolverContext(childId: Guid.NewGuid()) with
            {
                Difficulty = tier,
                Age = age
            };

            var puzzle = Generator.Generate(context);

            Assert.Null(PuzzleValidator.Validate(puzzle.Blueprint, puzzle.Public, puzzle.Solution));

            if (!puzzle.Blueprint.LowPressure)
                Assert.Equal(1, PuzzleValidator.CountSolutions(puzzle.Blueprint, puzzle.Public));
        }
    }

    /// <summary>Yaradıcı yolda da hər tapmaca etibarlıdır — sadəcə yeganəlik tələb olunmur.</summary>
    [Fact]
    public void YaradiciTapmaca_EtibarlidirVeTezyiqsizdir()
    {
        for (var i = 0; i < 20; i++)
        {
            var puzzle = Generator.Generate(CreativeContext() with { ChildId = Guid.NewGuid() });

            Assert.Null(PuzzleValidator.Validate(puzzle.Blueprint, puzzle.Public, puzzle.Solution));
            Assert.True(puzzle.Public.LowPressure);
            Assert.False(puzzle.Public.HintAvailable);
        }
    }

    /// <summary>Yoxlayıcı POZUQ element lövhəsini tutur — generator səhv etsə uşağa çatmır.</summary>
    [Fact]
    public void Yoxlayici_PozuqTapmacaniTutur()
    {
        var good = Generator.Generate(ExplorerContext());

        // İki eyni id — element siyahısı pozulur.
        var duplicated = Clone(good);
        duplicated.Public.Items[1].Id = duplicated.Public.Items[0].Id;
        Assert.NotNull(PuzzleValidator.Validate(duplicated.Blueprint, duplicated.Public, duplicated.Solution));

        // Həll elementlərin arasında deyil.
        var outside = Clone(good);
        Assert.NotNull(PuzzleValidator.Validate(
            outside.Blueprint, outside.Public,
            new PuzzleSolution(["item-zzz"], PetBrainAnswerKind.SelectIds)));

        // Uşaq məzmununa uyğun olmayan mətn.
        var unsafeText = Clone(good);
        unsafeText.Public.Title = "Qorxunc silah tapmacası";
        Assert.NotNull(PuzzleValidator.Validate(unsafeText.Blueprint, unsafeText.Public, unsafeText.Solution));

        // Forma işarəsi taksonomiyadan kənardır.
        var badShape = Clone(good);
        badShape.Public.Items[0].Shape = "star-burst";
        Assert.NotNull(PuzzleValidator.Validate(badShape.Blueprint, badShape.Public, badShape.Solution));
    }

    /// <summary>
    /// Qraf lövhəsində yoxlayıcı BAŞQA qaydalar tətbiq edir — element sayı
    /// deyil, düyünlərin rolu, keçidlərin bütövlüyü və enerji büdcəsi.
    ///
    /// <para>Ən vacib bənd sonuncudur: MƏCBURİ düyün lövhədə də məcburi
    /// görünməlidir. Əks halda uşaq ekranda görmədiyi gizli şərtə görə
    /// uğursuz olardı — bu, tapmaca deyil, tələ olardı.</para>
    /// </summary>
    [Fact]
    public void Yoxlayici_PozuqMarsrutLovhesiniTutur()
    {
        var good = Generator.Generate(SolverContext());
        Assert.True(good.Blueprint.IsGraphBoard);
        Assert.Null(PuzzleValidator.Validate(good.Blueprint, good.Public, good.Solution));

        // Başlanğıc düyünü yoxdur.
        var noStart = Clone(good);
        noStart.Public.Nodes.First(n => n.Kind == PetBrainNodeKind.Start).Kind = PetBrainNodeKind.Path;
        Assert.Equal("start-count", PuzzleValidator.Validate(noStart.Blueprint, noStart.Public, noStart.Solution));

        // Keçid lövhədən kənar düyünə gedir.
        var strayEdge = Clone(good);
        strayEdge.Public.Edges[0].To = "nowhere";
        Assert.Equal("edge-outside-board",
            PuzzleValidator.Validate(strayEdge.Blueprint, strayEdge.Public, strayEdge.Solution));

        // Doldurma düyünü enerji vermir — lövhədəki işarə uşağı aldadardı.
        var emptyRecharge = Clone(good);
        emptyRecharge.Public.Nodes.First(n => n.Kind == PetBrainNodeKind.Recharge).EnergyDelta = 0;
        Assert.Equal("bad-recharge",
            PuzzleValidator.Validate(emptyRecharge.Blueprint, emptyRecharge.Public, emptyRecharge.Solution));

        // Məcburi şərt GİZLİDİR: düyün adi yol kimi görünür.
        var hiddenRule = Clone(good);
        hiddenRule.Public.Nodes.First(n => n.Kind == PetBrainNodeKind.Required).Kind = PetBrainNodeKind.Path;
        Assert.Equal("required-not-marked",
            PuzzleValidator.Validate(hiddenRule.Blueprint, hiddenRule.Public, hiddenRule.Solution));

        // Enerji çatmır — heç bir marşrut qalmır, yəni tapmaca həllsizdir.
        var starved = Clone(good);
        starved.Public.InitialEnergy = 0;
        starved.Public.MaximumEnergy = 0;
        Assert.Equal("not-unique", PuzzleValidator.Validate(starved.Blueprint, starved.Public, starved.Solution));
    }

    /// <summary>
    /// TƏLƏ yolu formaca DÜZGÜNDÜR, məzmunca səhv: uşaq onu göndərə bilir və
    /// niyə işləmədiyini öyrənir.
    ///
    /// <para>Bu, qəsdən belədir. Sxem tələni "uyğun deyil" deyə rədd etsəydi,
    /// uşaq heç nə anlamazdı — halbuki dərs elə budur: Roboya çatmaq azdır,
    /// əvvəlcə antena bərpa olunmalıdır.</para>
    /// </summary>
    [Fact]
    public void TeleYolu_SehvSayilir_RedEdilmir()
    {
        var puzzle = Generator.Generate(SolverContext());
        var board = puzzle.Public;

        var decoy = board.Nodes.FirstOrDefault(n => n.Kind == PetBrainNodeKind.Decoy);
        Assert.NotNull(decoy);

        var start = board.Nodes.First(n => n.Kind == PetBrainNodeKind.Start);
        var goal = board.Nodes.First(n => n.Kind == PetBrainNodeKind.Goal);

        // Tələ birbaşa eniş modulundan ayrılmaya bilər — ona qədər olan yolu yığırıq.
        var route = new List<string> { start.Id };

        if (!board.Edges.Any(e => Touches(e, start.Id, decoy!.Id)))
        {
            var bridge = board.Nodes.First(n =>
                n.Kind == PetBrainNodeKind.Path && board.Edges.Any(e => Touches(e, n.Id, decoy!.Id)));

            route.Add(bridge.Id);
        }

        route.Add(decoy!.Id);
        route.Add(goal.Id);

        var result = PuzzleAnswerEvaluator.Evaluate(puzzle.Blueprint, board, puzzle.Solution, route);

        Assert.False(result.Rejected, "Tələ yolu formaca düzgündür — rədd edilməməlidir.");
        Assert.False(result.IsCorrect, "Antenadan keçməyən marşrut siqnalı bərpa etmir.");
    }

    private static bool Touches(PetBrainEdgeDto edge, string a, string b) =>
        (edge.From == a && edge.To == b) || (edge.From == b && edge.To == a);

    // ==================== Şəxsiləşdirmə ====================

    /// <summary>
    /// Şəxsiləşdirmə MEXANİKAYA çatır: həlledici profil məntiq tapmacası,
    /// yaradıcı profil isə təzyiqsiz yığım alır.
    /// </summary>
    [Fact]
    public void Profil_MexanikaniDeyisir()
    {
        var solver = Generator.Generate(SolverContext());
        var creative = Generator.Generate(CreativeContext());

        Assert.NotEqual(solver.Blueprint.Mechanic, creative.Blueprint.Mechanic);

        Assert.False(solver.Blueprint.LowPressure);
        Assert.Equal(PetBrainPuzzleMechanic.LightFragments, creative.Blueprint.Mechanic);
    }

    /// <summary>
    /// Kəşfiyyatçı ilə həlledici FƏRQLİ məntiq mexanikası alır — ikisi də
    /// "macəra" olsa da düşünmə tələbi başqadır.
    /// </summary>
    [Fact]
    public void KesfiyyatciVeHelledici_FerqliMexanikaAlir()
    {
        var solver = Generator.Generate(SolverContext() with
        {
            PlayStyles = new Dictionary<string, int>
            {
                [TraitKeys.ProblemSolver] = 90,
                [TraitKeys.Explorer] = 20,
                [TraitKeys.Playful] = 10,
                [TraitKeys.Caring] = 10
            }
        });

        var explorer = Generator.Generate(SolverContext() with
        {
            PlayStyles = new Dictionary<string, int>
            {
                [TraitKeys.ProblemSolver] = 20,
                [TraitKeys.Explorer] = 90,
                [TraitKeys.Playful] = 10,
                [TraitKeys.Caring] = 10
            }
        });

        Assert.Equal(PetBrainPuzzleMechanic.OrderedRoute, solver.Blueprint.Mechanic);
        Assert.Equal(PetBrainPuzzleMechanic.RouteLogic, explorer.Blueprint.Mechanic);
    }

    /// <summary>
    /// Mövzu LÜĞƏTİ çəkir — kosmos və okean eyni ikonları işlətmir.
    ///
    /// <para>Yoxlama kəşfiyyatçı profili ilə aparılır, çünki lüğət ELEMENT
    /// siyahısına düşür. Referans marşrut lövhəsi qəsdən kənardadır: onun
    /// düyünləri Marsın öz coğrafiyasıdır (eniş modulu, antena, Robo) və
    /// uşağın maraq mövzusuna görə "okean antenasına" çevrilməməlidir —
    /// hekayə şəxsiləşdirmədən daha vacibdir.</para>
    /// </summary>
    [Fact]
    public void Movzu_LugetiDeyisir()
    {
        var explorer = SolverContext() with
        {
            PlayStyles = new Dictionary<string, int>
            {
                [TraitKeys.Explorer] = 90,
                [TraitKeys.ProblemSolver] = 20,
                [TraitKeys.Playful] = 10,
                [TraitKeys.Caring] = 10
            }
        };

        var space = Generator.Generate(explorer);
        var ocean = Generator.Generate(explorer with { Theme = TraitKeys.Ocean });

        Assert.Equal(PetBrainPuzzleMechanic.RouteLogic, space.Blueprint.Mechanic);

        Assert.NotEqual(
            space.Public.Items.Select(i => i.Icon),
            ocean.Public.Items.Select(i => i.Icon));
    }

    /// <summary>Yaş TAVANDIR — kiçik uşaq yüksək çətinlik almır.</summary>
    [Fact]
    public void Yas_CetinliyeTavanQoyur()
    {
        var young = DeterministicPuzzleGenerator.TargetDifficulty(SolverContext() with
        {
            Age = 6,
            Difficulty = PetBrainDifficulty.Hard,
            MasteryTargetDifficulty = 10
        });

        Assert.True(young <= 4, $"6 yaşlı uşaq üçün hədəf {young} — tavan pozulub.");
    }

    /// <summary>Dəstək rejimi çətinliyi YUMŞALDIR və element sayını azaldır.</summary>
    [Fact]
    public void DestekRejimi_TapmacaniYumsaldir()
    {
        var normal = Generator.Generate(SolverContext());
        var assisted = Generator.Generate(SolverContext(assisted: true));

        Assert.True(assisted.Public.Items.Count <= normal.Public.Items.Count);
        Assert.True(
            DeterministicPuzzleGenerator.TargetDifficulty(SolverContext(assisted: true))
            < DeterministicPuzzleGenerator.TargetDifficulty(SolverContext()));
    }

    // ==================== Cavabın həqiqəti ====================

    /// <summary>Doğru cavab qəbul olunur, səhv cavab RƏDD olunur.</summary>
    [Fact]
    public void Qiymetlendirici_YalnizDogruHelliQebulEdir()
    {
        var puzzle = Generator.Generate(SolverContext());

        var correct = PuzzleAnswerEvaluator.Evaluate(
            puzzle.Blueprint, puzzle.Public, puzzle.Solution, puzzle.Solution.Ids);

        Assert.True(correct.IsCorrect);

        var wrongIds = puzzle.Public.Items
            .Select(i => i.Id)
            .Where(id => !puzzle.Solution.Ids.Contains(id))
            .Take(puzzle.Public.AnswerSchema.Min)
            .ToList();

        if (wrongIds.Count == puzzle.Public.AnswerSchema.Min)
        {
            var wrong = PuzzleAnswerEvaluator.Evaluate(
                puzzle.Blueprint, puzzle.Public, puzzle.Solution, wrongIds);

            Assert.False(wrong.IsCorrect);
            Assert.False(wrong.Rejected);
        }
    }

    /// <summary>Formaca pozuq cavab RƏDD olunur — bu, səhv deyil, etibarsızlıqdır.</summary>
    [Fact]
    public void Qiymetlendirici_PozuqCavabiRedEdir()
    {
        var puzzle = Generator.Generate(SolverContext());
        var solution = puzzle.Solution;

        foreach (var invalid in new List<string>?[]
                 {
                     null,
                     [],
                     ["item-zzz"],
                     [solution.Ids[0], solution.Ids[0]],
                     [.. Enumerable.Range(0, 20).Select(i => $"item-{i}")]
                 })
        {
            var result = PuzzleAnswerEvaluator.Evaluate(puzzle.Blueprint, puzzle.Public, solution, invalid);
            Assert.True(result.Rejected, $"Bu cavab rədd edilməli idi: {string.Join(',', invalid ?? [])}");
        }
    }

    /// <summary>
    /// Ardıcıllıq sxemində SIRA əhəmiyyətlidir — eyni elementlər, başqa sıra
    /// doğru sayılmır.
    /// </summary>
    [Fact]
    public void ArdicilliqSxemi_SiraniNezereAlir()
    {
        var context = SolverContext() with
        {
            PlayStyles = new Dictionary<string, int>
            {
                [TraitKeys.Playful] = 90,
                [TraitKeys.Caring] = 80,
                [TraitKeys.ProblemSolver] = 10,
                [TraitKeys.Explorer] = 10
            }
        };

        var puzzle = Generator.Generate(context);
        Assert.Equal(PetBrainPuzzleMechanic.SequenceOrder, puzzle.Blueprint.Mechanic);

        var reversed = puzzle.Solution.Ids.Reverse().ToList();

        Assert.False(PuzzleAnswerEvaluator
            .Evaluate(puzzle.Blueprint, puzzle.Public, puzzle.Solution, reversed).IsCorrect);
    }

    /// <summary>
    /// Təzyiqsiz yolda hər ETİBARLI seçim qəbul edilir — amma forma yoxlamaları
    /// hələ də işləyir.
    /// </summary>
    [Fact]
    public void TezyiqsizYol_HerEtibarliSecimiQebulEdir()
    {
        var puzzle = Generator.Generate(CreativeContext());
        var schema = puzzle.Public.AnswerSchema;

        foreach (var combination in puzzle.Public.Items.Select(i => i.Id).Chunk(schema.Min))
        {
            if (combination.Length != schema.Min)
                continue;

            Assert.True(PuzzleAnswerEvaluator
                .Evaluate(puzzle.Blueprint, puzzle.Public, puzzle.Solution, combination).IsCorrect);
        }

        // Naməlum id yenə də rədd olunur.
        Assert.True(PuzzleAnswerEvaluator
            .Evaluate(puzzle.Blueprint, puzzle.Public, puzzle.Solution, ["nope"]).Rejected);
    }

    // ==================== Mənbə qaydaları ====================

    /// <summary>
    /// Generator PROSESDƏN ASILI olan təsadüfilik işlətməməlidir.
    ///
    /// <para><c>string.GetHashCode()</c> hər prosesdə başqa nəticə verir,
    /// <c>Random.Shared</c> isə toxumlanmır və paylaşılır. İkisindən biri
    /// səhvən əlavə olunsa, verilmiş run-lar bərpa olunmayan qalar.</para>
    /// </summary>
    [Fact]
    public void TapmacaQati_ProsesdenAsiliTesadufiliyiIsletmir()
    {
        foreach (var file in new[]
                 {
                     "PuzzleSeed.cs", "DeterministicPuzzleGenerator.cs",
                     "PuzzleBlueprintCatalog.cs", "PuzzleValidator.cs", "PuzzleAnswerEvaluator.cs"
                 })
        {
            // Şərhlər çıxarılır: bu qaydaların NİYƏ olduğunu izah edən mətn
            // qadağanın özü kimi oxunmamalıdır.
            var source = StripComments(ReadPuzzleSource(file));

            Assert.DoesNotContain("GetHashCode()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Random.Shared", source, StringComparison.Ordinal);
            Assert.DoesNotContain("new Random(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DateTime.Now", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DateTime.UtcNow", source, StringComparison.Ordinal);
        }
    }

    /// <summary>Kataloqdakı hər mexanika ayrı davranış verir — dördü də fərqlidir.</summary>
    [Fact]
    public void Kataloq_DordFerqliMexanikaSaxlayir()
    {
        var mechanics = PuzzleBlueprintCatalog.Blueprints.Select(b => b.Mechanic).ToList();

        Assert.True(mechanics.Count >= 3, "Ən azı üç mexanika olmalıdır.");
        Assert.Equal(mechanics.Count, mechanics.Distinct().Count());

        // Yaradıcı janr üçün ən azı bir təzyiqsiz mexanika var.
        Assert.Contains(PuzzleBlueprintCatalog.Blueprints,
            b => b.ExperienceType == PetBrainExperienceType.Creative && b.LowPressure);

        // Macəra janrı üçün ən azı iki məntiq mexanikası var.
        Assert.True(PuzzleBlueprintCatalog.Blueprints
            .Count(b => b.ExperienceType == PetBrainExperienceType.Adventure && !b.LowPressure) >= 2);
    }

    [Fact]
    public void Kataloq_NamelumAcariQebulEtmir()
    {
        Assert.Null(PuzzleBlueprintCatalog.Find("uydurma-mexanika"));
        Assert.False(PuzzleBlueprintCatalog.IsKnown("uydurma-mexanika"));
        Assert.False(PuzzleBlueprintCatalog.IsKnown(null));
    }

    private static GeneratedPuzzle Clone(GeneratedPuzzle source) => source with
    {
        Public = new Shared.Dtos.PetBrain.PetBrainPuzzleDto
        {
            PuzzleId = source.Public.PuzzleId,
            Mechanic = source.Public.Mechanic,
            Title = source.Public.Title,
            Instruction = source.Public.Instruction,
            TargetValue = source.Public.TargetValue,
            AnswerSchema = source.Public.AnswerSchema,
            HintAvailable = source.Public.HintAvailable,
            Hint = source.Public.Hint,
            LowPressure = source.Public.LowPressure,
            Items = [.. source.Public.Items.Select(i => new Shared.Dtos.PetBrain.PetBrainPuzzleItemDto
            {
                Id = i.Id,
                Label = i.Label,
                Icon = i.Icon,
                Shape = i.Shape,
                Value = i.Value
            })],
            StoryPrompt = source.Public.StoryPrompt,
            InitialEnergy = source.Public.InitialEnergy,
            MaximumEnergy = source.Public.MaximumEnergy,
            MoveCost = source.Public.MoveCost,
            RequiredBeforeGoal = [.. source.Public.RequiredBeforeGoal],
            AssistHighlight = source.Public.AssistHighlight,
            Nodes = [.. source.Public.Nodes.Select(n => new Shared.Dtos.PetBrain.PetBrainNodeDto
            {
                Id = n.Id,
                Kind = n.Kind,
                X = n.X,
                Y = n.Y,
                EnergyDelta = n.EnergyDelta,
                Label = n.Label,
                Icon = n.Icon
            })],
            Edges = [.. source.Public.Edges.Select(e => new Shared.Dtos.PetBrain.PetBrainEdgeDto
            {
                From = e.From,
                To = e.To
            })],
            Slots = [.. source.Public.Slots.Select(s => new Shared.Dtos.PetBrain.PetBrainSlotDto
            {
                Id = s.Id,
                X = s.X,
                Y = s.Y
            })],
            ClueIcons = [.. source.Public.ClueIcons],
            Scene = new Shared.Dtos.PetBrain.PetBrainSceneDto
            {
                IllustrationStatus = source.Public.Scene.IllustrationStatus,
                AssetUrl = source.Public.Scene.AssetUrl,
                AltText = source.Public.Scene.AltText,
                OverlayLayout = source.Public.Scene.OverlayLayout
            }
        }
    };

    /// <summary>C# şərhləri (<c>///</c>, <c>//</c>, <c>/* */</c>) çıxarılır.</summary>
    private static string StripComments(string source)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(
            source, @"/\*.*?\*/", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);

        return System.Text.RegularExpressions.Regex.Replace(
            text, @"^\s*//.*$", string.Empty, System.Text.RegularExpressions.RegexOptions.Multiline);
    }

    private static string ReadPuzzleSource(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.Api", "PetBrain", "Puzzles", name));
}
