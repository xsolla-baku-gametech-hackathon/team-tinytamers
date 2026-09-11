using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Şəkil yığımı — macəranın hekayə rəsmi parçalara bölünür, uşaq onu yığır.
///
/// <para>Üç iddia qorunur: tapmaca HƏR macəradadır, cavabın həqiqəti serverdədir
/// (rəsm yalnız görüntüdür) və hekayəsi olan macərada şəkil yığımı əsas
/// tapmacanın EYNİ rəsmini işlədir — yəni əlavə pullu sorğu yaranmır.</para>
/// </summary>
public class PetBrainPictureAssemblyTests
{
    private static readonly Guid ChildId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RunId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    /// <summary>Parça sayı pillə ilə böyüyür, dəstək rejimi bir pillə kiçildir.</summary>
    [Theory]
    [InlineData(PetBrainDifficulty.Easy, false, 6)]
    [InlineData(PetBrainDifficulty.Medium, false, 9)]
    [InlineData(PetBrainDifficulty.Hard, false, 12)]
    [InlineData(PetBrainDifficulty.Hard, true, 9)]
    [InlineData(PetBrainDifficulty.Medium, true, 6)]
    public void ParcaSayi_PilleyeGoreBoyuyur(PetBrainDifficulty tier, bool assisted, int expected)
    {
        var generated = Generate(Context(tier, assisted));

        Assert.Equal(PuzzleBlueprintCatalog.SceneJigsawKey, generated.Blueprint.Key);
        Assert.False(generated.UsedFallback);
        Assert.Equal(expected, generated.Public.Items.Count);
        Assert.Equal(expected, generated.Public.GridColumns * generated.Public.GridRows);
    }

    /// <summary>
    /// Qab QARIŞIQ gəlir, hər yer tam bir parçaya aiddir və müstəqil
    /// yoxlayıcı yeganə həlli təsdiqləyir.
    /// </summary>
    [Fact]
    public void Tapmaca_QarisiqGelir_VeTekHelliVar()
    {
        var generated = Generate(Context(PetBrainDifficulty.Medium));
        var slots = generated.Public.Items.Select(i => i.Value!.Value).ToList();

        Assert.NotEqual(Enumerable.Range(0, slots.Count), slots);
        Assert.Equal(Enumerable.Range(0, slots.Count), slots.Order());

        Assert.Null(PuzzleValidator.Validate(generated.Blueprint, generated.Public, generated.Solution));
        Assert.Equal(1, PuzzleValidator.CountSolutions(generated.Blueprint, generated.Public));

        Assert.Equal(
            generated.Public.Items.OrderBy(i => i.Value).Select(i => i.Id),
            generated.Solution.Ids);
    }

    /// <summary>
    /// Qabda heç bir parça öz yerinin nömrəsində durmur — yoxsa uşaq
    /// parçaları soldan sağa düzməklə şəkli baxmadan yığardı. Çox run və hər
    /// pillə üzrə yoxlanılır, çünki təsadüfi qarışdırma bunu yalnız bəzən pozur.
    /// </summary>
    [Fact]
    public void Qab_HecBirParcaOzYerindeDurmur()
    {
        foreach (var tier in new[] { PetBrainDifficulty.Easy, PetBrainDifficulty.Medium, PetBrainDifficulty.Hard })
        {
            for (var seed = 0; seed < 200; seed++)
            {
                var context = Context(tier) with { RunId = new Guid(seed, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7) };
                var slots = Generate(context).Public.Items.Select(i => i.Value!.Value).ToList();

                Assert.DoesNotContain(slots.Select((slot, position) => slot == position), fixedPoint => fixedPoint);
            }
        }
    }

    /// <summary>
    /// Server yalnız TAM və DÜZGÜN yığılmış şəkli qəbul edir: yeri dəyişən iki
    /// parça səhvdir (rədd deyil — normal cəhd), yarımçıq şəkil isə rədd edilir.
    /// </summary>
    [Fact]
    public void Qiymetlendirici_YalnizTamYigilmisSekliQebulEdir()
    {
        var generated = Generate(Context(PetBrainDifficulty.Medium));

        var right = generated.Solution.Ids.ToList();
        var swapped = right.ToList();
        (swapped[0], swapped[1]) = (swapped[1], swapped[0]);

        Assert.True(Evaluate(generated, right).IsCorrect);

        var wrong = Evaluate(generated, swapped);
        Assert.False(wrong.IsCorrect);
        Assert.False(wrong.Rejected);

        Assert.True(Evaluate(generated, [.. right.Take(3)]).Rejected);
    }

    /// <summary>Eyni run eyni şəkli verir; başqa run başqa düzülüş alır.</summary>
    [Fact]
    public void EyniRun_EyniDuzulus_BaskaRun_BaskaDuzulus()
    {
        var first = Generate(Context(PetBrainDifficulty.Medium));
        var again = Generate(Context(PetBrainDifficulty.Medium));
        var other = Generate(Context(PetBrainDifficulty.Medium) with { RunId = Guid.NewGuid() });

        Assert.Equal(first.Signature, again.Signature);
        Assert.Equal(first.Solution.Ids, again.Solution.Ids);
        Assert.NotEqual(first.Signature, other.Signature);
    }

    /// <summary>
    /// Adi tapmaca mərhələsində şəkil yığımı TƏKLİF OLUNMUR — o, hekayə
    /// tapmacasının yerini tutmamalıdır, yalnız öz mərhələsində gəlir.
    /// </summary>
    [Fact]
    public void AdiMerhelede_SekilYigimiTeklifOlunmur()
    {
        var context = Context(PetBrainDifficulty.Medium) with { PreferredBlueprintKey = string.Empty };

        Assert.DoesNotContain(
            DeterministicPuzzleGenerator.Rank(context),
            b => b.Key == PuzzleBlueprintCatalog.SceneJigsawKey);
    }

    /// <summary>Kataloqdakı HƏR macərada şəkil yığımı var — xəttidə mərhələ, qrafda düyün.</summary>
    [Fact]
    public void HerMacerada_SekilYigimiVar()
    {
        foreach (var template in ExperienceCatalog.Templates)
        {
            var graph = StoryCatalog.Find(template.Key);

            var hasPicture = graph is not null
                ? graph.Nodes.Any(n => n.Kind == PetBrainStageKind.Puzzle
                                       && n.PuzzleFamily == PuzzleBlueprintCatalog.SceneJigsawKey)
                : template.Stages.Any(s => s.Kind == PetBrainStageKind.Puzzle
                                           && s.PuzzleFamily == PuzzleBlueprintCatalog.SceneJigsawKey);

            Assert.True(hasPicture, $"{template.Key}: şəkil yığımı yoxdur.");
        }
    }

    /// <summary>
    /// Qrafın köhnə versiyası SİLİNMİR: yarımçıq run-lar dəyişməmiş qrafı
    /// oxuyur, yeni run-lar isə şəkil yığımlı versiyanı alır.
    /// </summary>
    [Fact]
    public void KohneQrafVersiyasi_YarimciqRunlarUcunQalir()
    {
        foreach (var (current, previous) in new[]
                 {
                     (MoonCrystalHunt.Definition, MoonCrystalHunt.PreviousDefinition),
                     (MoonCrystalSecret.Definition, MoonCrystalSecret.PreviousDefinition)
                 })
        {
            Assert.Same(current, StoryCatalog.Find(current.Key));
            Assert.True(current.Version > previous.Version);

            var (resolved, exact) = StoryCatalog.Resolve(current.Key, previous.Version);

            Assert.True(exact);
            Assert.Same(previous, resolved);
            Assert.DoesNotContain(previous.Nodes, n => n.PuzzleFamily == PuzzleBlueprintCatalog.SceneJigsawKey);
        }
    }

    /// <summary>
    /// Hekayəsi olan macərada şəkil yığımı əsas tapmacanın EYNİ səhnəsini
    /// işlədir: eyni hash → eyni keşlənmiş rəsm → əlavə pullu sorğu yoxdur.
    /// </summary>
    [Fact]
    public void HekayeMacerasinda_SekilYigimiEyniResmiIsledir()
    {
        var mars = ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!;

        var story = PuzzleSceneSpec.For(
            PuzzleBlueprintCatalog.Find(PuzzleBlueprintCatalog.MarsSignalRouteKey)!, "az", mars, "fox");

        var picture = PuzzleSceneSpec.For(
            PuzzleBlueprintCatalog.Find(PuzzleBlueprintCatalog.SceneJigsawKey)!, "az", mars, "fox");

        Assert.Equal(story.Hash(), picture.Hash());
    }

    /// <summary>
    /// Hekayə tapmacası olmayan macəra ÖZ təsdiqlənmiş səhnəsini alır — ümumi
    /// «sakit mənzərə» yox. Alt mətn də nəzarətli şablondandır.
    /// </summary>
    [Fact]
    public void HekayesizMacera_OzSehnesiniAlir()
    {
        var forest = ExperienceCatalog.Find(ExperienceCatalog.ForestFriendsParade)!;

        var spec = PuzzleSceneSpec.For(
            PuzzleBlueprintCatalog.Find(PuzzleBlueprintCatalog.SceneJigsawKey)!, "az", forest, "bunny");

        var prompt = SafePuzzleIllustrationPromptBuilder.Build(spec);

        Assert.Contains("forest clearing", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("calm imaginary landscape", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not reveal a solution", prompt, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(spec.AltText()));
    }

    /// <summary>
    /// Macərada şəkil yığımı həqiqətən OYNANIR: server onu verir, uşağın
    /// yığdığı sıranı yoxlayır, tapmaca həll olunmuş kimi saxlanılır və macəra
    /// sona çatır.
    /// </summary>
    [Fact]
    public async Task Macerada_SekilYigimiOynanirVeServerYoxlayir()
    {
        using var factory = new TestWebAppFactory();

        var client = await ApiTestClient.CreateAsync(factory, "picture-run@petpal.test", "Leyla");
        await client.HatchAsync(factory);

        var run = await PetBrainPlaythrough.StartAsync(client);
        PetBrainPuzzleDto? picture = null;

        for (var step = 0; step < 90 && !PetBrainPlaythrough.IsFinished(run); step++)
        {
            if (run.Stage!.Puzzle is { Mechanic: PetBrainPuzzleMechanic.PictureAssembly } found)
            {
                picture = found;
                break;
            }

            run = await PetBrainPlaythrough.StepAsync(client, run);
        }

        Assert.NotNull(picture);
        Assert.Equal(PuzzleBlueprintCatalog.SceneJigsawKey, picture!.Scene.OverlayLayout);
        Assert.False(string.IsNullOrWhiteSpace(picture.Scene.AltText));
        Assert.Equal(picture.Items.Count, picture.GridColumns * picture.GridRows);

        var assembled = picture.Items.OrderBy(i => i.Value).Select(i => i.Id).ToList();
        var next = await SubmitAsync(client, run, assembled);

        Assert.NotEqual(picture.PuzzleId, next.Stage?.Puzzle?.PuzzleId);

        using (var scope = factory.Services.CreateScope())
        {
            var issued = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .IssuedPuzzles.AsNoTracking().FirstAsync(p => p.Id == picture.PuzzleId);

            Assert.Equal(PetBrainPuzzleStatus.Solved, issued.Status);
            Assert.Equal(PuzzleBlueprintCatalog.SceneJigsawKey, issued.BlueprintKey);
        }

        var finished = await PetBrainPlaythrough.ContinueToEndAsync(client, next);
        var completed = await PetBrainPlaythrough.CompleteAsync(client, finished.RunId);

        Assert.NotNull(completed.Summary);
    }

    private static async Task<PetBrainRunDto> SubmitAsync(ApiTestClient client, PetBrainRunDto run, List<string> ids)
    {
        var response = await client.Http.PostAsJsonAsync($"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest
            {
                StageIndex = run.Stage!.Index,
                NodeId = string.IsNullOrEmpty(run.Stage.NodeId) ? null : run.Stage.NodeId,
                SelectedIds = ids
            });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
    }

    private static PuzzleAnswerResult Evaluate(GeneratedPuzzle generated, IReadOnlyList<string> answer) =>
        PuzzleAnswerEvaluator.Evaluate(generated.Blueprint, generated.Public, generated.Solution, answer);

    private static GeneratedPuzzle Generate(PuzzleGenerationContext context) =>
        new DeterministicPuzzleGenerator().Generate(context);

    private static PuzzleGenerationContext Context(PetBrainDifficulty tier, bool assisted = false) => new(
        ChildId: ChildId,
        RunId: RunId,
        StageIndex: 4,
        Age: 10,
        Language: "az",
        TemplateKey: ExperienceCatalog.MarsRoverRescue,
        ExperienceType: PetBrainExperienceType.Adventure,
        Theme: TraitKeys.Space,
        Interests: new Dictionary<string, int>(),
        PlayStyles: new Dictionary<string, int>(),
        Difficulty: tier,
        MasteryTargetDifficulty: tier switch
        {
            PetBrainDifficulty.Easy => 2,
            PetBrainDifficulty.Hard => 8,
            _ => 5
        },
        MechanicTiers: new Dictionary<string, PetBrainDifficulty>(),
        Assisted: assisted,
        RecentSignatures: new HashSet<string>(StringComparer.Ordinal),
        PreferredBlueprintKey: PuzzleBlueprintCatalog.SceneJigsawKey);
}
