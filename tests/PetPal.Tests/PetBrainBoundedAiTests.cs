using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.BoundedAi;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Məhdud AI — modelin təklif etdiyi plan SERVER tərəfindən yoxlanılır.
///
/// <para>Buradakı testlərin hamısı bir cümləni müdafiə edir: <b>model heç nə
/// uydura bilmir</b>. O, yalnız təsdiqlənmiş ID-ləri yenidən düzür; bir ID
/// siyahıdan kənara çıxan kimi plan BÜTÖVLÜKDƏ rədd olunur və deterministik
/// tərif işə düşür.</para>
/// </summary>
public class StoryPlanValidatorTests
{
    private static readonly ExperienceDefinition Reference = MoonCrystalHunt.Definition;

    // ==================== Qəbul ====================

    /// <summary>Etibarlı plan QƏBUL edilir və tərifə çevrilir.</summary>
    [Fact]
    public void EtibarliPlan_QebulEdilir()
    {
        var review = StoryPlanValidator.Review(ValidPlan(), Reference);

        Assert.True(review.Accepted, string.Join(" | ", review.Problems));
        Assert.Equal(Reference.Key, review.Definition!.Key);

        // Qəbul edilən plan da EYNİ qraf validatorundan keçir.
        Assert.Empty(ExperienceGraphValidator.Validate(review.Definition));
    }

    /// <summary>
    /// Qəbul edilən planın MƏTNİ kataloqdandır — modeldən deyil.
    ///
    /// <para>Model səhnə ID-si seçir, cümləni isə server yazır. Ona görə
    /// uşağın oxuduğu hər söz onsuz da nəzərdən keçirilmiş mətndir.</para>
    /// </summary>
    [Fact]
    public void QebulEdilenPlan_MetniKataloqdanGoturur()
    {
        var review = StoryPlanValidator.Review(ValidPlan(), Reference);
        var node = review.Definition!.Find("n1")!;

        var source = Reference.Nodes.First(n => n.SceneVariant == "moon-arrival");

        Assert.Equal(source.PromptAz, node.PromptAz);
        Assert.Equal(source.PetLineAz, node.PetLineAz);
    }

    /// <summary>Qəbul edilən planın VERSİYASI ayrılır — run onu ayırd edə bilir.</summary>
    [Fact]
    public void QebulEdilenPlan_AyriVersiyaAlir()
    {
        var review = StoryPlanValidator.Review(ValidPlan(), Reference);

        Assert.NotEqual(Reference.Version, review.Definition!.Version);
    }

    // ==================== Rədd halları ====================

    [Fact]
    public void BosPlan_RedOlunur() =>
        Assert.Equal(
            StoryPlanRejection.Empty,
            StoryPlanValidator.Review(null, Reference).RejectionReason);

    /// <summary>Model MACƏRA seçə bilmir — yalnız mövcud birinin quruluşunu təklif edir.</summary>
    [Fact]
    public void YadMacera_RedOlunur()
    {
        var plan = ValidPlan() with { ExperienceKey = "uydurma-macera" };

        Assert.Equal(
            StoryPlanRejection.UnknownExperience,
            StoryPlanValidator.Review(plan, Reference).RejectionReason);
    }

    /// <summary>Naməlum SƏHNƏ id-si rədd olunur.</summary>
    [Fact]
    public void NamelumSehne_RedOlunur()
    {
        var plan = WithFirstNode(ValidPlan(), n => n with { SceneId = "uydurma-sehne" });

        var review = StoryPlanValidator.Review(plan, Reference);

        Assert.Equal(StoryPlanRejection.UnknownId, review.RejectionReason);
        Assert.Contains(review.Problems, p => p.Contains("uydurma-sehne", StringComparison.Ordinal));
    }

    /// <summary>Naməlum TON, AN, PERSONAJ və YADDAŞ açarı da rədd olunur.</summary>
    [Theory]
    [InlineData("tone")]
    [InlineData("beat")]
    [InlineData("character")]
    [InlineData("memory")]
    public void NamelumAcar_RedOlunur(string field)
    {
        var plan = field switch
        {
            "tone" => WithFirstNode(ValidPlan(), n => n with { ToneId = "uydurma" }),
            "beat" => WithFirstNode(ValidPlan(), n => n with { BeatId = "uydurma" }),
            "memory" => WithFirstNode(ValidPlan(), n => n with { MemoryCallbackId = "uydurma" }),
            _ => ValidPlan() with { CharacterId = "uydurma" }
        };

        Assert.Equal(
            StoryPlanRejection.UnknownId,
            StoryPlanValidator.Review(plan, Reference).RejectionReason);
    }

    /// <summary>
    /// Naməlum KEÇİD NÖVÜ rədd olunur — model şərt dili uydura bilmir.
    /// </summary>
    [Fact]
    public void NamelumKecidNovu_RedOlunur()
    {
        var plan = WithFirstNode(ValidPlan(), n => n with
        {
            Transitions = [new StoryPlanTransition("if-child-is-sad", "n2", string.Empty)]
        });

        Assert.Equal(
            StoryPlanRejection.UnknownId,
            StoryPlanValidator.Review(plan, Reference).RejectionReason);
    }

    /// <summary>
    /// <b>Yad hekayənin tapmacası rədd olunur.</b> Ay macərası Marsın
    /// tapmacasını ala bilməz — qayda modelə də aiddir.
    /// </summary>
    [Fact]
    public void YadTapmaca_RedOlunur()
    {
        var plan = ValidPlan() with
        {
            Nodes =
            [
                Node("n1", PetBrainStageKind.Intro, "moon-arrival",
                    [new StoryPlanTransition("always", "n2", string.Empty)]),

                Node("n2", PetBrainStageKind.Puzzle, "moon-mirror-field",
                    [new StoryPlanTransition("always", "n3", string.Empty)])
                    with { PuzzleAdapterId = PuzzleBlueprintCatalog.MarsSignalRouteKey },

                Ending("n3", "moon-summit-finale", MoonCrystalHunt.ExplorerEnding),
                Ending("n4", "moon-lantern-finale", MoonCrystalHunt.CaringEnding)
            ]
        };

        var review = StoryPlanValidator.Review(plan, Reference);

        Assert.Equal(StoryPlanRejection.UnknownId, review.RejectionReason);
        Assert.Contains(review.Problems, p => p.Contains("icazəli deyil", StringComparison.Ordinal));
    }

    /// <summary>DALAN rədd olunur — uşaq boş ekranla qalmamalıdır.</summary>
    [Fact]
    public void Dalan_RedOlunur()
    {
        var plan = ValidPlan() with
        {
            Nodes =
            [
                Node("n1", PetBrainStageKind.Intro, "moon-arrival",
                    [new StoryPlanTransition("always", "n2", string.Empty)]),

                // n2-nin çıxışı YOXDUR.
                Node("n2", PetBrainStageKind.Consequence, "moon-deep-hum", []),

                Ending("n3", "moon-summit-finale", MoonCrystalHunt.ExplorerEnding),
                Ending("n4", "moon-lantern-finale", MoonCrystalHunt.CaringEnding)
            ]
        };

        Assert.Equal(
            StoryPlanRejection.DeadEnd,
            StoryPlanValidator.Review(plan, Reference).RejectionReason);
    }

    /// <summary>ÇATILMAYAN düyün rədd olunur — yazılıb, amma görünməyən ekran.</summary>
    [Fact]
    public void CatilmayanDuyun_RedOlunur()
    {
        var plan = ValidPlan() with
        {
            Nodes =
            [
                Node("n1", PetBrainStageKind.Intro, "moon-arrival",
                    [new StoryPlanTransition("always", "n2", string.Empty)]),

                Ending("n2", "moon-summit-finale", MoonCrystalHunt.ExplorerEnding),

                // n3-ə heç kim getmir.
                Ending("n3", "moon-lantern-finale", MoonCrystalHunt.CaringEnding)
            ]
        };

        Assert.Equal(
            StoryPlanRejection.Unreachable,
            StoryPlanValidator.Review(plan, Reference).RejectionReason);
    }

    /// <summary>Həddindən çox düyün rədd olunur.</summary>
    [Fact]
    public void CoxDuyun_RedOlunur()
    {
        var nodes = Enumerable.Range(0, StoryPlanValidator.MaxNodes + 2)
            .Select(i => Node($"n{i}", PetBrainStageKind.Consequence, "moon-arrival",
                [new StoryPlanTransition("always", $"n{i + 1}", string.Empty)]))
            .ToList();

        Assert.Equal(
            StoryPlanRejection.TooManyNodes,
            StoryPlanValidator.Review(ValidPlan() with { Nodes = nodes }, Reference).RejectionReason);
    }

    /// <summary>Təkrar düyün açarı rədd olunur.</summary>
    [Fact]
    public void TekrarDuyunAcari_RedOlunur()
    {
        var plan = ValidPlan();
        var plan2 = plan with { Nodes = [.. plan.Nodes, plan.Nodes[0]] };

        Assert.Equal(
            StoryPlanRejection.DuplicateNode,
            StoryPlanValidator.Review(plan2, Reference).RejectionReason);
    }

    /// <summary>Naməlum başlanğıc düyün rədd olunur.</summary>
    [Fact]
    public void NamelumBaslangic_RedOlunur() =>
        Assert.Equal(
            StoryPlanRejection.MissingStart,
            StoryPlanValidator.Review(ValidPlan() with { StartNodeId = "yoxdur" }, Reference).RejectionReason);

    // ==================== Köməkçilər ====================

    private static StoryPlan ValidPlan() => new(
        ExperienceKey: Reference.Key,
        CharacterId: "pet",
        StartNodeId: "n1",
        Nodes:
        [
            Node("n1", PetBrainStageKind.Intro, "moon-arrival",
                [new StoryPlanTransition("always", "n2", string.Empty)]),

            // Səhnəni yenidən işlədən düyün onun BÜTÜN variantlarını
            // yönləndirməlidir: yönləndirilməyən variant ekranda görünüb heç
            // nə etməzdi və validator bunu tutur.
            Node("n2", PetBrainStageKind.Choice, "moon-carry",
            [
                new StoryPlanTransition("choice", "n3", "summit-route"),
                new StoryPlanTransition("choice", "n3", "signal-relay"),
                new StoryPlanTransition("choice", "n4", "lantern-cradle"),
                new StoryPlanTransition("always", "n4", string.Empty)
            ]),

            Ending("n3", "moon-summit-finale", MoonCrystalHunt.ExplorerEnding),
            Ending("n4", "moon-lantern-finale", MoonCrystalHunt.CaringEnding)
        ]);

    private static StoryPlanNode Node(
        string id, PetBrainStageKind kind, string scene, IReadOnlyList<StoryPlanTransition> transitions) =>
        new(id, "arrival", scene, "calm", kind, string.Empty, string.Empty, string.Empty, transitions);

    private static StoryPlanNode Ending(string id, string scene, string endingKey) =>
        new(id, "farewell", scene, "calm", PetBrainStageKind.Ending,
            string.Empty, string.Empty, endingKey, []);

    private static StoryPlan WithFirstNode(StoryPlan plan, Func<StoryPlanNode, StoryPlanNode> change) =>
        plan with { Nodes = [change(plan.Nodes[0]), .. plan.Nodes.Skip(1)] };
}

/// <summary>Allowlist bütövlükdə qapalıdır və kataloqla uyğun gəlir.</summary>
public class StoryPlanAllowlistTests
{
    /// <summary>Tapmaca adapterləri MÖVCUD kataloqdandır — uydurma açar yoxdur.</summary>
    [Fact]
    public void TapmacaAdapterleri_KataloqdanGelir() =>
        Assert.All(
            StoryPlanAllowlist.PuzzleAdapters,
            key => Assert.True(PuzzleBlueprintCatalog.IsKnown(key), $"«{key}» kataloqda yoxdur."));

    /// <summary>Sonluqlar MÖVCUD təriflərdəndir.</summary>
    [Fact]
    public void Sonluqlar_TeriflerdenGelir()
    {
        var known = StoryCatalog.Definitions
            .SelectMany(d => d.EndingKeys)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(StoryPlanAllowlist.Endings, key => Assert.Contains(key, known));
    }

    /// <summary>Modelə göndərilən sorğuda uşağa aid HEÇ NƏ yoxdur.</summary>
    [Fact]
    public void Sorgu_UsagaAidMelumatDasimir()
    {
        var request = StoryPlanAllowlist.RequestFor("az", "9-10", "space", ExperienceCatalog.MoonCrystalRescue);

        // Sahə səviyyəsində: nə id, nə ad, nə söhbət, nə yaddaş cümləsi.
        var fields = typeof(StoryPlanRequest).GetProperties().Select(p => p.Name).ToList();

        foreach (var forbidden in new[] { "ChildId", "ChildName", "PetName", "Memories", "Chat", "Age" })
            Assert.DoesNotContain(forbidden, fields);

        Assert.Equal("9-10", request.AgeBand);
    }
}

/// <summary>Məhdud AI açarı BAĞLI olanda heç nə dəyişmir.</summary>
public class BoundedAiDisabledTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public BoundedAiDisabledTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>
    /// Standart konfiqurasiyada plan provayderi <b>heç nə təklif etmir</b> və
    /// deterministik tərif işlənir.
    /// </summary>
    [Fact]
    public async Task AcarBagli_DeterministikTerifIslenir()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<StoryPlanCoordinator>();
        var provider = scope.ServiceProvider.GetRequiredService<IStoryPlanProvider>();

        Assert.IsType<NoStoryPlanProvider>(provider);

        var resolved = await coordinator.ResolveAsync(MoonCrystalHunt.Definition, MindStub());

        Assert.Same(MoonCrystalHunt.Definition, resolved);
    }

    private static PetPal.Api.PetBrain.Mind.PetMindContext MindStub() =>
        PetPal.Tests.MindStub.Build();
}
