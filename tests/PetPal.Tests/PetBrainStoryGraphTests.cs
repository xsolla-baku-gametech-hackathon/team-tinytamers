using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Qrafın QURULUŞ zəmanətləri — saf, bazasız testlər.
///
/// <para>Budaqlanan hekayədə ən pis nasazlıq görünməzdir: bir yolu oynayan
/// uşaq boş ekranla qalır, digərləri isə heç nə görmür. Ona görə tərif
/// kataloqa düşməzdən əvvəl bütöv yoxlanılır.</para>
/// </summary>
public class ExperienceGraphValidatorTests
{
    /// <summary>Kataloqdakı HƏR tərif etibarlıdır — bu, buraxılış şərtidir.</summary>
    [Fact]
    public void KataloqdakiButunTerifler_Etibarlidir()
    {
        foreach (var definition in StoryCatalog.Definitions)
        {
            var problems = ExperienceGraphValidator.Validate(definition);

            Assert.True(problems.Count == 0,
                $"«{definition.Key}»: {string.Join(" | ", problems)}");
        }
    }

    /// <summary>Hər tərifin başlanğıcı və ən azı iki sonluğu var.</summary>
    [Fact]
    public void HerTerif_BaslangicVeSonluqDasiyir()
    {
        foreach (var definition in StoryCatalog.Definitions)
        {
            Assert.NotNull(definition.Find(definition.StartNodeId));

            Assert.True(
                definition.EndingKeys.Count >= ExperienceGraphValidator.MinEndings,
                $"«{definition.Key}» üçün sonluq azdır.");
        }
    }

    /// <summary>Hər düyün başlanğıcdan ÇATILANDIR — yazılıb, amma görünməyən ekran yoxdur.</summary>
    [Fact]
    public void ButunDuyunler_BaslangicdanCatilir()
    {
        var definition = MoonCrystalHunt.Definition;

        HashSet<string> seen = new(StringComparer.Ordinal) { definition.StartNodeId };
        Queue<string> queue = new();
        queue.Enqueue(definition.StartNodeId);

        while (queue.Count > 0)
        {
            var node = definition.Find(queue.Dequeue())!;

            foreach (var transition in node.Transitions)
            {
                if (seen.Add(transition.TargetNodeId))
                    queue.Enqueue(transition.TargetNodeId);
            }
        }

        Assert.Equal(definition.Nodes.Count, seen.Count);
    }

    /// <summary>Dalan AŞKARLANIR: çıxışsız, sonluq olmayan düyün rədd olunur.</summary>
    [Fact]
    public void Dalan_AskarlanirVeRedOlunur()
    {
        var broken = new ExperienceDefinition(
            Key: "test-dead-end",
            Version: 1,
            StartNodeId: "a",
            AllowedPuzzleFamilies: [],
            Nodes:
            [
                Node("a", PetBrainStageKind.Intro, [new ExperienceTransition("b", IsFallback: true)]),
                Node("b", PetBrainStageKind.Consequence, []),
                Ending("end", "only")
            ]);

        var problems = ExperienceGraphValidator.Validate(broken);

        Assert.Contains(problems, p => p.Contains("dalandır", StringComparison.Ordinal));
    }

    /// <summary>Naməlum düyünə keçid rədd olunur.</summary>
    [Fact]
    public void NamelumKecid_RedOlunur()
    {
        var broken = new ExperienceDefinition(
            Key: "test-unknown",
            Version: 1,
            StartNodeId: "a",
            AllowedPuzzleFamilies: [],
            Nodes:
            [
                Node("a", PetBrainStageKind.Intro, [new ExperienceTransition("yoxdur", IsFallback: true)]),
                Ending("end-1", "one"),
                Ending("end-2", "two")
            ]);

        var problems = ExperienceGraphValidator.Validate(broken);

        Assert.Contains(problems, p => p.Contains("naməlum düyünə", StringComparison.Ordinal));
    }

    /// <summary>Ehtiyat keçidi olmayan düyün rədd olunur — şərtlər tutmasa ekran boş qalardı.</summary>
    [Fact]
    public void EhtiyatKecidsizDuyun_RedOlunur()
    {
        var broken = new ExperienceDefinition(
            Key: "test-no-fallback",
            Version: 1,
            StartNodeId: "a",
            AllowedPuzzleFamilies: [],
            Nodes:
            [
                Node("a", PetBrainStageKind.Intro, [new ExperienceTransition("end-1", RequiredFlag: "x")]),
                Ending("end-1", "one"),
                Ending("end-2", "two")
            ]);

        var problems = ExperienceGraphValidator.Validate(broken);

        Assert.Contains(problems, p => p.Contains("ehtiyat keçid yoxdur", StringComparison.Ordinal));
    }

    /// <summary>Heç bir keçidə bağlı olmayan variant rədd olunur — bəzək seçim deyil.</summary>
    [Fact]
    public void BagliOlmayanVariant_RedOlunur()
    {
        var broken = new ExperienceDefinition(
            Key: "test-orphan-option",
            Version: 1,
            StartNodeId: "a",
            AllowedPuzzleFamilies: [],
            Nodes:
            [
                new ExperienceNode(
                    "a", PetBrainStageKind.Choice, "s", "s", "p", "p",
                    Options:
                    [
                        new ExperienceOption("real", "A", "A", "🅰️", "", "", []),
                        new ExperienceOption("orphan", "B", "B", "🅱️", "", "", [])
                    ],
                    Transitions:
                    [
                        new ExperienceTransition("end-1", RequiredOptionKey: "real"),
                        new ExperienceTransition("end-2", IsFallback: true)
                    ],
                    Effects: []),
                Ending("end-1", "one"),
                Ending("end-2", "two")
            ]);

        var problems = ExperienceGraphValidator.Validate(broken);

        Assert.Contains(problems, p => p.Contains("orphan", StringComparison.Ordinal));
    }

    /// <summary>Sonluq açarı təkrarlanmır.</summary>
    [Fact]
    public void TekrarSonluqAcari_RedOlunur()
    {
        var broken = new ExperienceDefinition(
            Key: "test-duplicate-ending",
            Version: 1,
            StartNodeId: "a",
            AllowedPuzzleFamilies: [],
            Nodes:
            [
                Node("a", PetBrainStageKind.Intro, [new ExperienceTransition("end-1", IsFallback: true)]),
                Ending("end-1", "same"),
                Ending("end-2", "same")
            ]);

        var problems = ExperienceGraphValidator.Validate(broken);

        Assert.Contains(problems, p => p.Contains("təkrarlanır", StringComparison.Ordinal));
    }

    private static ExperienceNode Node(
        string id, PetBrainStageKind kind, IReadOnlyList<ExperienceTransition> transitions) =>
        new(id, kind, "prompt", "prompt", "pet", "pet", [], transitions, []);

    private static ExperienceNode Ending(string id, string key) =>
        new(id, PetBrainStageKind.Ending, "son", "end", "pet", "pet", [], [], [], EndingKey: key);
}

/// <summary>
/// Qrafın icra qatı — determinist və bayraq-şüurlu.
/// </summary>
public class StoryRuntimeTests
{
    /// <summary>Eyni giriş HƏMİŞƏ eyni düyünə aparır.</summary>
    [Fact]
    public void EyniGiris_EyniDuyuneAparir()
    {
        var graph = MoonCrystalHunt.Definition;
        var choose = graph.Find("choose-crater")!;

        var input = new StoryInput("deep-crater", PetBrainStageResult.None,
            new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal("deep-result", StoryRuntime.Next(graph, choose, input)!.Id);
        Assert.Equal("deep-result", StoryRuntime.Next(graph, choose, input)!.Id);
    }

    /// <summary>Üç başlanğıc seçimi ÜÇ FƏRQLİ düyünə aparır — seçim bəzək deyil.</summary>
    [Fact]
    public void UcSecim_UcFerqliDuyuneAparir()
    {
        var graph = MoonCrystalHunt.Definition;
        var choose = graph.Find("choose-crater")!;

        var targets = new[] { "north-crater", "deep-crater", "bright-crater" }
            .Select(key => StoryRuntime.Next(graph, choose,
                new StoryInput(key, PetBrainStageResult.None, new HashSet<string>(StringComparer.Ordinal)))!.Id)
            .ToList();

        Assert.Equal(3, targets.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Hər başlanğıc seçimi AYRI hekayə bayrağı və AYRI səhnə variantı verir.</summary>
    [Fact]
    public void HerBudaq_AyriBayraqVeSehneVerir()
    {
        var graph = MoonCrystalHunt.Definition;

        var north = graph.Find("north-result")!;
        var deep = graph.Find("deep-result")!;
        var bright = graph.Find("bright-result")!;

        Assert.Equal([MoonCrystalHunt.NorthFlag], StoryRuntime.FlagsOf(north));
        Assert.Equal([MoonCrystalHunt.DeepFlag], StoryRuntime.FlagsOf(deep));
        Assert.Equal([MoonCrystalHunt.BrightFlag], StoryRuntime.FlagsOf(bright));

        var variants = new[] { north, deep, bright }
            .Select(StoryRuntime.SceneVariantOf)
            .ToList();

        Assert.Equal(3, variants.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(variants, string.IsNullOrEmpty);
    }

    /// <summary>Tapmacanın nəticəsi sonrakı düyünü DƏYİŞİR.</summary>
    [Fact]
    public void TapmacaNeticesi_SonrakiDuyunuDeyisir()
    {
        var graph = MoonCrystalHunt.Definition;
        var puzzle = graph.Find("crystal-puzzle")!;

        var clean = StoryRuntime.Next(graph, puzzle, new StoryInput(
            string.Empty, PetBrainStageResult.Solved,
            new HashSet<string>(StringComparer.Ordinal) { MoonCrystalHunt.CleanSolveFlag }))!;

        var ordinary = StoryRuntime.Next(graph, puzzle, new StoryInput(
            string.Empty, PetBrainStageResult.Solved,
            new HashSet<string>(StringComparer.Ordinal)))!;

        Assert.Equal("crystal-awake-clean", clean.Id);
        Assert.Equal("crystal-awake", ordinary.Id);
        Assert.NotEqual(clean.Id, ordinary.Id);
    }

    /// <summary>Üç sonluq da ÇATILANDIR.</summary>
    [Fact]
    public void UcSonluq_Catilir()
    {
        var graph = MoonCrystalHunt.Definition;
        var carry = graph.Find("carry-choice")!;

        var endings = new[] { "summit-route", "signal-relay", "lantern-cradle" }
            .Select(key => StoryRuntime.Next(graph, carry,
                new StoryInput(key, PetBrainStageResult.None, new HashSet<string>(StringComparer.Ordinal)))!)
            .ToList();

        Assert.All(endings, e => Assert.True(e.IsEnding));

        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                MoonCrystalHunt.ExplorerEnding,
                MoonCrystalHunt.ScientistEnding,
                MoonCrystalHunt.CaringEnding
            },
            endings.Select(e => e.EndingKey).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// Naməlum variant gələndə EHTİYAT keçid işləyir — uşaq boş ekranla qalmır.
    /// </summary>
    [Fact]
    public void NamelumVariant_EhtiyataDuşur()
    {
        var graph = MoonCrystalHunt.Definition;
        var choose = graph.Find("choose-crater")!;

        var next = StoryRuntime.Next(graph, choose, new StoryInput(
            "uydurma", PetBrainStageResult.None, new HashSet<string>(StringComparer.Ordinal)));

        Assert.NotNull(next);
    }

    /// <summary>
    /// Qarşıdakı tapmaca yalnız BİRMƏNALI olanda verilir.
    ///
    /// <para>Ayda üç krater ayrı düyünlərə gedir, amma üçü də eyni addımda
    /// eyni tapmacaya çıxır — deməli rəsm indidən çəkilə bilər.</para>
    /// </summary>
    [Fact]
    public void QarsidakiTapmaca_BirmenaliOlandaVerilir()
    {
        var graph = MoonCrystalHunt.Definition;

        var fromIntro = StoryRuntime.UpcomingPuzzle(graph, graph.Start);

        Assert.NotNull(fromIntro);
        Assert.Equal("crystal-puzzle", fromIntro!.Value.Puzzle.Id);
        Assert.Equal(3, fromIntro.Value.Depth);

        // Tapmacadan SONRA daha tapmaca yoxdur.
        Assert.Null(StoryRuntime.UpcomingPuzzle(graph, graph.Find("crystal-awake")!));
    }
}

/// <summary>
/// İLK seçim SON seçimin nəticəsini dəyişir.
///
/// <para>Bu, budaqlanan hekayənin ən asan itirilən vədidir: seçimlər ayrı-ayrı
/// ekranlar yaradır, amma bir-birinə toxunmursa, hekayə hələ də xəttidir —
/// sadəcə üç fərqli rəngdə.</para>
/// </summary>
public class MoonBranchCarryOverTests
{
    /// <summary>
    /// Dərin kraterdən gələn uşaq üçün «zirvədən apar» başqa yerə çıxır:
    /// aşağıdan qalxmaq mümkün deyil.
    /// </summary>
    [Fact]
    public void DerinKrater_ZirveSeciminiDeyisir()
    {
        var graph = MoonCrystalHunt.Definition;
        var carry = graph.Find("carry-choice")!;

        var fromDeep = StoryRuntime.Next(graph, carry, new StoryInput(
            "summit-route", PetBrainStageResult.None,
            new HashSet<string>(StringComparer.Ordinal) { MoonCrystalHunt.DeepFlag }))!;

        var fromNorth = StoryRuntime.Next(graph, carry, new StoryInput(
            "summit-route", PetBrainStageResult.None,
            new HashSet<string>(StringComparer.Ordinal) { MoonCrystalHunt.NorthFlag }))!;

        Assert.NotEqual(fromDeep.Id, fromNorth.Id);
        Assert.Equal("carry-too-steep", fromDeep.Id);
        Assert.Equal(MoonCrystalHunt.ExplorerEnding, fromNorth.EndingKey);
    }

    /// <summary>
    /// Yolun dəyişməsi uşağı DALANDA qoymur — o, yenə bir sonluğa çatır.
    /// </summary>
    [Fact]
    public void DeyisenYol_YeneSonluqlaBitir()
    {
        var graph = MoonCrystalHunt.Definition;
        var rerouted = graph.Find("carry-too-steep")!;

        var ending = StoryRuntime.Next(graph, rerouted, new StoryInput(
            string.Empty, PetBrainStageResult.None,
            new HashSet<string>(StringComparer.Ordinal) { MoonCrystalHunt.DeepFlag }))!;

        Assert.True(ending.IsEnding);
        Assert.Equal(MoonCrystalHunt.CaringEnding, ending.EndingKey);
    }

    /// <summary>
    /// Şimal və parlaq budaqlarda «zirvədən apar» hələ də kəşfiyyatçı
    /// sonluğuna aparır — dəyişiklik yalnız ONA aid olan budağa toxunur.
    /// </summary>
    [Theory]
    [InlineData(MoonCrystalHunt.NorthFlag)]
    [InlineData(MoonCrystalHunt.BrightFlag)]
    public void DigerBudaqlar_ToxunulmazQalir(string flag)
    {
        var graph = MoonCrystalHunt.Definition;
        var carry = graph.Find("carry-choice")!;

        var next = StoryRuntime.Next(graph, carry, new StoryInput(
            "summit-route", PetBrainStageResult.None,
            new HashSet<string>(StringComparer.Ordinal) { flag }))!;

        Assert.Equal(MoonCrystalHunt.ExplorerEnding, next.EndingKey);
    }
}
