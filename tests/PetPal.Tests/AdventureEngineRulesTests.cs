using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Engine-in qalan saf qaydaları: versiya seçimi, ipucu nərdivanı və
/// tərifin qrafik quruluş səhvləri.
/// </summary>
public class AdventureEngineRulesTests
{

    /// <summary>
    /// Köhnə versiyada başlamış run KÖHNƏ tərifi oxuyur, yeni versiya
    /// yayımlansa da.
    /// </summary>
    [Fact]
    public void KohneVersiyadaBaslayanRun_KohneTerifiOxuyur()
    {
        var v1 = Minimal("versioned", version: 1, startId: "v1-start");
        var v2 = Minimal("versioned", version: 2, startId: "v2-start");

        var (definition, exact) = StoryCatalog.Resolve([v1, v2], "versioned", 1);

        Assert.True(exact);
        Assert.Same(v1, definition);
    }

    /// <summary>Yeni run (versiya 0) həmişə ƏN SON versiyanı alır.</summary>
    [Fact]
    public void YeniRun_EnSonVersiyaniAlir()
    {
        var v1 = Minimal("versioned", version: 1, startId: "v1-start");
        var v2 = Minimal("versioned", version: 2, startId: "v2-start");

        var (definition, exact) = StoryCatalog.Resolve([v1, v2], "versioned", 0);

        Assert.True(exact);
        Assert.Same(v2, definition);
    }

    /// <summary>
    /// Silinmiş versiyanın run-u ən son versiyanı alır, amma «dəqiq deyil»
    /// işarəsi ilə — çağıran təhlükəsiz bərpaya keçir.
    /// </summary>
    [Fact]
    public void SilinmisVersiya_EnSonaDuserVeDeqiqDeyil()
    {
        var v2 = Minimal("versioned", version: 2, startId: "v2-start");

        var (definition, exact) = StoryCatalog.Resolve([v2], "versioned", 1);

        Assert.False(exact);
        Assert.Same(v2, definition);
    }

    [Theory]
    [InlineData(0, 0, PetBrainHintLevel.None)]
    [InlineData(0, 2, PetBrainHintLevel.GentlePrompt)]
    [InlineData(1, 0, PetBrainHintLevel.DirectionalHint)]
    [InlineData(2, 0, PetBrainHintLevel.WorkedExample)]
    [InlineData(3, 1, PetBrainHintLevel.StepByStepHelp)]
    [InlineData(4, 0, PetBrainHintLevel.AssistedCompletion)]
    [InlineData(9, 5, PetBrainHintLevel.AssistedCompletion)]
    public void Nerdivan_PilleniDuzgunSecir(int hints, int attempts, PetBrainHintLevel expected) =>
        Assert.Equal(expected, HintLadder.LevelFor(hints, attempts));

    /// <summary>
    /// Nərdivan cavabın ən çox YARISINI açır — son pillədə belə tam həll
    /// klientə getmir.
    /// </summary>
    [Fact]
    public void Nerdivan_CavabinYarisindanCoxunuAcmir()
    {
        var (puzzle, solution) = Board(itemCount: 4);

        for (var hints = 0; hints <= 6; hints++)
        {
            var step = HintLadder.For(hints, 0, puzzle, solution, lowPressure: false, "istiqamət", "az");

            Assert.True(step.RevealedIds.Count <= solution.Ids.Count / 2,
                $"{hints} ipucunda {step.RevealedIds.Count} addım açıldı.");

            Assert.All(step.RevealedIds, id => Assert.Contains(id, solution.Ids));
        }
    }

    /// <summary>Açılan addımlar CAVABIN BAŞINDANDIR — ardıcıllıq qorunur.</summary>
    [Fact]
    public void Nerdivan_CavabinBasindanAcir()
    {
        var (puzzle, solution) = Board(itemCount: 4);

        var step = HintLadder.For(2, 0, puzzle, solution, lowPressure: false, "istiqamət", "az");

        Assert.Equal(PetBrainHintLevel.WorkedExample, step.Level);
        Assert.Equal([solution.Ids[0]], step.RevealedIds);
        Assert.Contains(puzzle.Items.First(i => i.Id == solution.Ids[0]).Label, step.Text);
    }

    /// <summary>Təzyiqsiz tapmacada «cavab» açılmır — orada doğru həll yoxdur.</summary>
    [Fact]
    public void TezyiqsizTapmaca_CavabAcmir()
    {
        var (puzzle, solution) = Board(itemCount: 4);

        var step = HintLadder.For(3, 0, puzzle, solution, lowPressure: true, "istiqamət", "az");

        Assert.Empty(step.RevealedIds);
    }

    /// <summary>Birgə tamamlama YALNIZ son pillədə təklif olunur.</summary>
    [Fact]
    public void BirgeTamamlama_YalnizSonPillede()
    {
        var (puzzle, solution) = Board(itemCount: 4);

        for (var hints = 0; hints < HintLadder.AssistAfterHints; hints++)
            Assert.False(HintLadder.For(hints, 0, puzzle, solution, false, "x", "az").AssistAvailable);

        Assert.True(HintLadder.For(HintLadder.AssistAfterHints, 0, puzzle, solution, false, "x", "az").AssistAvailable);
    }

    [Fact]
    public void BaslangicDuyunuYoxdursa_RedOlunur()
    {
        var definition = Minimal("broken", 1, "start") with { StartNodeId = "missing" };

        Assert.Contains(ExperienceGraphValidator.Validate(definition),
            p => p.Contains("Başlanğıc düyün tapılmadı", StringComparison.Ordinal));
    }

    [Fact]
    public void TekrarDuyunAcari_RedOlunur()
    {
        var baseline = Minimal("dupes", 1, "start");
        var definition = baseline with { Nodes = [.. baseline.Nodes, baseline.Nodes[^1]] };

        Assert.Contains(ExperienceGraphValidator.Validate(definition),
            p => p.Contains("Düyün açarı təkrarlanır", StringComparison.Ordinal));
    }

    [Fact]
    public void CatilmayanSonluq_RedOlunur()
    {
        var baseline = Minimal("orphan", 1, "start");

        var orphan = Ending("lonely-end", "lonely");
        var definition = baseline with { Nodes = [.. baseline.Nodes, orphan] };

        Assert.Contains(ExperienceGraphValidator.Validate(definition),
            p => p.Contains("«lonely-end» başlanğıcdan çatılmır", StringComparison.Ordinal));
    }

    /// <summary>
    /// DÖVRƏ rədd olunur. Ehtiyat keçid dövrəni «keçilə bilən» göstərir, ona
    /// görə o, ayrıca qayda ilə axtarılır.
    /// </summary>
    [Fact]
    public void Dovre_RedOlunur()
    {
        var a = new ExperienceNode("a", PetBrainStageKind.Intro, "A", "A", "a", "a",
            [], [new ExperienceTransition("b", Priority: 1000, IsFallback: true)], []);

        var b = new ExperienceNode("b", PetBrainStageKind.Choice, "B", "B", "b", "b",
            [
                new ExperienceOption("left", "Sol", "Left", "⬅", "", "", []),
                new ExperienceOption("right", "Sağ", "Right", "➡", "", "", [])
            ],
            [
                new ExperienceTransition("end-one", RequiredOptionKey: "left", Priority: 10),
                new ExperienceTransition("end-two", RequiredOptionKey: "right", Priority: 20),
                new ExperienceTransition("a", Priority: 1000, IsFallback: true)
            ],
            []);

        var definition = new ExperienceDefinition("loop", 1, "a",
            [a, b, Ending("end-one", "one"), Ending("end-two", "two")], []);

        Assert.Contains(ExperienceGraphValidator.Validate(definition),
            p => p.Contains("dövrə", StringComparison.Ordinal));
    }

    [Fact]
    public void NamelumTapmacaSablonu_RedOlunur()
    {
        var definition = WithPuzzle("unknown-family");

        Assert.Contains(ExperienceGraphValidator.Validate(definition),
            p => p.Contains("naməlum tapmaca şablonu", StringComparison.Ordinal));
    }

    /// <summary>Başqa macəranın hekayəsini daşıyan tapmaca rədd olunur.</summary>
    [Fact]
    public void YadMaceraninTapmacasi_RedOlunur()
    {
        var definition = WithPuzzle(PuzzleBlueprintCatalog.MarsSignalRouteKey);

        Assert.Contains(ExperienceGraphValidator.Validate(definition),
            p => p.Contains("bu macəraya aid deyil", StringComparison.Ordinal));
    }

    [Fact]
    public void NamelumMukafat_RedOlunur()
    {
        var definition = Minimal("rewards", 1, "start") with
        {
            Endings = [new AdventureEndingDefinition("one", "end-one", "Bir", "One", "Bir", "One", "no-such-reward")]
        };

        Assert.Contains(ExperienceGraphValidator.Validate(definition),
            p => p.Contains("naməlum mükafat", StringComparison.Ordinal));
    }

    /// <summary>İnkişafla açılan əşya macəra mükafatı ola bilməz — iki mənbə qarışardı.</summary>
    [Fact]
    public void InkisafEsyasi_MaceraMukafatiOlaBilmez()
    {
        var definition = Minimal("rewards", 1, "start") with
        {
            Endings = [new AdventureEndingDefinition("one", "end-one", "Bir", "One", "Bir", "One", "collar-classic")]
        };

        Assert.Contains(ExperienceGraphValidator.Validate(definition),
            p => p.Contains("macəra mükafatı deyil", StringComparison.Ordinal));
    }

    [Fact]
    public void BirDildeYoxOlanMetn_RedOlunur()
    {
        var baseline = Minimal("locale", 1, "start");
        var untranslated = baseline.Nodes[0] with { PromptEn = "" };
        var definition = baseline with { Nodes = [untranslated, .. baseline.Nodes.Skip(1)] };

        Assert.Contains(ExperienceGraphValidator.Validate(definition),
            p => p.Contains("bir dildə yoxdur", StringComparison.Ordinal));
    }

    /// <summary>Minimal tərifin özü ETİBARLIDIR — yuxarıdakı şikayətlər yalnız pozuntudandır.</summary>
    [Fact]
    public void MinimalTerif_Etibarlidir() =>
        Assert.Empty(ExperienceGraphValidator.Validate(Minimal("clean", 1, "start")));

    private static ExperienceDefinition Minimal(string key, int version, string startId)
    {
        var start = new ExperienceNode(startId, PetBrainStageKind.Choice, "Başla", "Start", "Gedək", "Let us go",
            [
                new ExperienceOption("one", "Bir", "One", "1", "", "", []),
                new ExperienceOption("two", "İki", "Two", "2", "", "", [])
            ],
            [
                new ExperienceTransition("end-one", RequiredOptionKey: "one", Priority: 10),
                new ExperienceTransition("end-two", RequiredOptionKey: "two", Priority: 20),
                new ExperienceTransition("end-one", Priority: 1000, IsFallback: true)
            ],
            []);

        return new ExperienceDefinition(key, version, startId,
            [start, Ending("end-one", "one"), Ending("end-two", "two")], []);
    }

    private static ExperienceDefinition WithPuzzle(string family)
    {
        var baseline = Minimal(ExperienceCatalog.MoonCrystalSecret, 1, "start");

        var puzzle = new ExperienceNode("puzzle", PetBrainStageKind.Puzzle, "Tapmaca", "Puzzle", "Həll et", "Solve it",
            [], [new ExperienceTransition("end-one", Priority: 1000, IsFallback: true)], [],
            PuzzleFamily: family);

        var start = baseline.Nodes[0] with
        {
            Transitions =
            [
                new ExperienceTransition("puzzle", RequiredOptionKey: "one", Priority: 10),
                new ExperienceTransition("end-two", RequiredOptionKey: "two", Priority: 20),
                new ExperienceTransition("end-one", Priority: 1000, IsFallback: true)
            ]
        };

        return baseline with { Nodes = [start, puzzle, .. baseline.Nodes.Skip(1)] };
    }

    private static ExperienceNode Ending(string id, string endingKey) =>
        new(id, PetBrainStageKind.Ending, "Son", "The end", "Bitdi", "Done", [], [], [], EndingKey: endingKey);

    private static (PetBrainPuzzleDto Puzzle, PuzzleSolution Solution) Board(int itemCount)
    {
        var items = Enumerable.Range(0, itemCount)
            .Select(i => new PetBrainPuzzleItemDto { Id = $"item-{i}", Label = $"Addım {i + 1}", Value = i })
            .ToList();

        var solution = new PuzzleSolution([.. items.Select(i => i.Id).Reverse()], PetBrainAnswerKind.OrderIds);

        return (new PetBrainPuzzleDto { Items = items }, solution);
    }
}
