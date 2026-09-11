using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Scenery;
using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;
using PetPal.Shared.Scenes;

namespace PetPal.Tests;

/// <summary>
/// Gündəlik səhnə həddi 1 olan fixture — kvota testi bir səhnə ilə dolur.
/// </summary>
public sealed class SceneQuotaFactory : TestWebAppFactory
{
    public FakePuzzleIllustrationProvider Provider { get; } = new();

    public string StorageRoot { get; } =
        Path.Combine(Path.GetTempPath(), $"petpal-scenery-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("PetBrain:IllustrationStorageRoot", StorageRoot);
        builder.UseSetting("PetBrainMedia:MaxPaidScenesPerDay", "1");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPuzzleIllustrationProvider>();
            services.AddSingleton<IPuzzleIllustrationProvider>(Provider);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(StorageRoot))
            Directory.Delete(StorageRoot, recursive: true);
    }
}

/// <summary>
/// Seçimlərə görə çəkilən ARXA FON və OBRAZ.
///
/// <para>Hamısı saxta provayderlə və ya ümumiyyətlə provayderSİZ işləyir:
/// şəbəkə, açar və pul lazım deyil. Yoxlanan dörd şeydir — seçim rəsmi
/// dəyişirmi, dəyişməməli olan şey onu dəyişmirmi (xərc), modelə nə gedir
/// (təhlükəsizlik) və rəsmi kim ala bilər (sahiblik).</para>
/// </summary>
public class PetBrainAdventureSceneryTests
{
    /// <summary>
    /// Hekayənin qaytardığı HƏR səhnə açarını ekran tanıyır. Əvvəl uzun
    /// macəranın açarları səssizcə «eniş» kadrına düşürdü — bu test həmin
    /// boşluğun geri qayıtmasına imkan vermir.
    /// </summary>
    [Fact]
    public void HekayeninHerSehneAcari_EkranaTanisdir()
    {
        var unknown = StorySceneVariants()
            .Where(key => !MoonSceneVariants.Knows(key))
            .ToList();

        Assert.Empty(unknown);
    }

    /// <summary>Müqavilədə ölü açar yoxdur — hər açarı hansısa hekayə işlədir.</summary>
    [Fact]
    public void MuqavileninHerAcari_HekayedeIslenir()
    {
        var used = StorySceneVariants().ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(MoonSceneVariants.All, key => !used.Contains(key));
    }

    [Fact]
    public void EyniVeziyyet_EyniArxaFon_DilHashaDusmur()
    {
        var az = AdventureSceneDirector.Backdrop(Moon("moon-base-dark", language: "az"));
        var en = AdventureSceneDirector.Backdrop(Moon("moon-base-dark", language: "en"));

        Assert.Equal(az.Hash(), en.Hash());
        Assert.NotEqual(az.AltText(), en.AltText());
    }

    /// <summary>Enerji qərarı bazanı OYADIR — fonun işığı və əşyaları dəyişir.</summary>
    [Fact]
    public void EnerjiQerari_BazaninFonunuDeyisir()
    {
        var dark = AdventureSceneDirector.Backdrop(Moon("moon-base-dark"));
        var map = AdventureSceneDirector.Backdrop(Moon("moon-map-wall",
            choices: ["power-map"], flags: [MoonKeys.FlagMapPowered]));
        var comms = AdventureSceneDirector.Backdrop(Moon("moon-comms-room",
            choices: ["power-comms"], flags: [MoonKeys.FlagCommsPowered]));

        Assert.Equal(SceneryKeys.LightStandby, dark.Light);
        Assert.Equal(SceneryKeys.LightAwake, map.Light);
        Assert.Contains(SceneryKeys.PieceGlowingMap, map.SetPieces);
        Assert.Contains(SceneryKeys.PieceLiveAntenna, comms.SetPieces);

        Assert.Equal(3, new[] { dark.Hash(), map.Hash(), comms.Hash() }.Distinct().Count());
    }

    /// <summary>
    /// Eyni məkanın düyünləri BİR fonu paylaşır: seçim dəyişməyibsə bazada
    /// gəzmək yeni pullu rəsm demək deyil.
    /// </summary>
    [Fact]
    public void EyniMekaninDuyunleri_BirFonuPaylasir()
    {
        var log = AdventureSceneDirector.Backdrop(Moon("moon-base-log"));
        var panel = AdventureSceneDirector.Backdrop(Moon("moon-power-panel"));

        Assert.Equal(log.Hash(), panel.Hash());
    }

    [Fact]
    public void YolSecimi_MekaniDeyisir_IsiqKristaliKrateriIsiqlandirir()
    {
        var cave = AdventureSceneDirector.Backdrop(Moon("moon-cave-echo", choices: ["route-cave"]));
        var dark = AdventureSceneDirector.Backdrop(Moon("moon-crater-dark", choices: ["route-crater"]));
        var lit = AdventureSceneDirector.Backdrop(Moon("moon-crater-dark",
            choices: ["route-crater"], items: [MoonKeys.ToolLightCrystal]));

        Assert.Equal(SceneryKeys.CrystalCave, cave.Place);
        Assert.Equal(SceneryKeys.ShadowedCrater, dark.Place);
        Assert.Equal(SceneryKeys.LightStar, dark.Light);
        Assert.Equal(SceneryKeys.LightLantern, lit.Light);
        Assert.NotEqual(dark.Hash(), lit.Hash());
    }

    [Fact]
    public void Marsda_SecilenErazi_FonOlur()
    {
        string PlaceOf(params string[] choices) =>
            AdventureSceneDirector.Backdrop(Linear(ExperienceCatalog.MarsRoverRescue, choices)).Place;

        Assert.Equal(SceneryKeys.MartianCanyon, PlaceOf());
        Assert.Equal(SceneryKeys.MartianCrater, PlaceOf("crater"));
        Assert.Equal(SceneryKeys.MartianMountain, PlaceOf("mountain"));

        var rescued = AdventureSceneDirector.Backdrop(
            Linear(ExperienceCatalog.MarsRoverRescue, "crater", "solar-panel"));

        Assert.Contains(SceneryKeys.PieceSolarArray, rescued.SetPieces);
    }

    [Fact]
    public void Ejdahada_SecilenRengVeYuva_FonaKecir()
    {
        var backdrop = AdventureSceneDirector.Backdrop(
            Linear(ExperienceCatalog.DragonLostColors, "sunset", "cloud-castle"));

        Assert.Equal(SceneryKeys.PaletteSunset, backdrop.Palette);
        Assert.Equal(SceneryKeys.CloudCastle, backdrop.Place);
    }

    /// <summary>
    /// Rol və çanta obrazı dəyişir; kristal parçası kimi tez-tez dəyişən
    /// inventar və gəzilən məkan isə DƏYİŞMİR — yoxsa obraz hər addımda
    /// yenidən çəkilərdi.
    /// </summary>
    [Fact]
    public void RolVeCanta_ObraziDeyisir_InventarVeMekanDeyismir()
    {
        var plain = AdventureSceneDirector.Portrait(Moon("moon-kit-table"));
        var role = AdventureSceneDirector.Portrait(Moon("moon-kit-table", choices: [MoonKeys.RoleScientist]));
        var kit = AdventureSceneDirector.Portrait(Moon("moon-launch",
            choices: [MoonKeys.RoleScientist], items: [MoonKeys.ToolScanner]));
        var later = AdventureSceneDirector.Portrait(Moon("moon-cave-lights",
            choices: [MoonKeys.RoleScientist], items: [MoonKeys.ToolScanner, MoonKeys.ItemShardOne]));

        Assert.Equal(SceneryKeys.RoleScientist, role.Role);
        Assert.Contains(SceneryKeys.GearScanner, kit.Gear);

        Assert.NotEqual(plain.Hash(), role.Hash());
        Assert.NotEqual(role.Hash(), kit.Hash());
        Assert.Equal(kit.Hash(), later.Hash());
    }

    [Fact]
    public void ObrazinEsyaSirasi_HashiDeyismir()
    {
        var one = AdventureSceneDirector.Portrait(Moon("moon-launch",
            items: [MoonKeys.ToolScanner, MoonKeys.ToolLightCrystal]));
        var other = AdventureSceneDirector.Portrait(Moon("moon-launch",
            items: [MoonKeys.ToolLightCrystal, MoonKeys.ToolScanner]));

        Assert.Equal(one.Hash(), other.Hash());
    }

    /// <summary>
    /// Obraz uşağın portreti DEYİL və modelə sərbəst mətn getmir: naməlum
    /// seçim açarı və gözlənilməz növ prompta çatmır.
    /// </summary>
    [Fact]
    public void Promptlar_InsanCekmir_SerbestMetnDasimir()
    {
        var context = Moon("moon-kit-table",
            choices: ["Aylin <b>gizli</b> söz", MoonKeys.RoleEngineer],
            species: "hacker");

        var portrait = AdventureSceneDirector.Portrait(context).BuildPrompt();
        var backdrop = AdventureSceneDirector.Backdrop(context).BuildPrompt();

        Assert.Contains("animal companion", portrait, StringComparison.Ordinal);

        foreach (var prompt in new[] { portrait, backdrop })
        {
            Assert.Contains("No children, no humans", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("Aylin", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("hacker", prompt, StringComparison.Ordinal);
            Assert.Contains("fox", prompt, StringComparison.Ordinal);
        }

        Assert.Contains("upper third and the lower third", backdrop, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gündəlik hədd dolanda yeni səhnə provayderə GETMİR, keşdəki səhnə isə
    /// pulsuz qalır. Sabah hədd sıfırlanır və gözləyən səhnə yenidən açılır.
    /// </summary>
    [Fact]
    public async Task GundelikHedd_DolandaYeniSehneEhtiyataDusur_SabahAcilir()
    {
        using var factory = new SceneQuotaFactory();

        var crater = AdventureSceneDirector.Backdrop(Linear(ExperienceCatalog.MarsRoverRescue, "crater"));
        var mountain = AdventureSceneDirector.Backdrop(Linear(ExperienceCatalog.MarsRoverRescue, "mountain"));

        Assert.Equal(PetBrainIllustrationStatus.Pending, (await EnsureAsync(factory, crater)).Status);
        await RenderAsync(factory, crater);

        var denied = await EnsureAsync(factory, mountain);

        Assert.Equal(PetBrainIllustrationStatus.Fallback, denied.Status);
        Assert.Equal("global-daily-quota", denied.FailureReason);

        await RenderAsync(factory, mountain);

        Assert.Equal(1, factory.Provider.Calls);
        Assert.Equal(PetBrainIllustrationStatus.Ready, (await EnsureAsync(factory, crater)).Status);

        factory.Clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal(PetBrainIllustrationStatus.Pending, (await EnsureAsync(factory, mountain)).Status);
    }

    /// <summary>
    /// Seçim fonu dəyişir, ekran yeni rəsmi alır, köhnə ünvan isə artıq
    /// verilmir. Yad uşaq və uydurulmuş hash heç nə ala bilmir.
    /// </summary>
    [Fact]
    public async Task Secimle_ArxaFonDeyisir_YalnizSahibineVeCariSehneVerilir()
    {
        using var factory = new PetBrainIllustrationFactory();

        var client = await MoonSecretAdventure.ChildAsync(factory, "scenery-owner@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(factory, client);

        Assert.NotEmpty(run.Stage!.Backdrop.AssetUrl);
        Assert.NotEmpty(run.Stage.Backdrop.AltText);
        Assert.NotEmpty(run.Stage.Portrait.AssetUrl);
        Assert.NotEmpty(run.Stage.Portrait.AltText);

        run = await WaitForArtAsync(client, run.RunId);

        Assert.Equal(PetBrainIllustrationStatus.Ready, run.Stage!.Backdrop.IllustrationStatus);
        Assert.Equal(PetBrainIllustrationStatus.Ready, run.Stage.Portrait.IllustrationStatus);

        var first = run.Stage.Backdrop.AssetUrl;

        Assert.Equal(HttpStatusCode.OK, (await client.Http.GetAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.Http.GetAsync(run.Stage.Portrait.AssetUrl)).StatusCode);

        var stranger = await MoonSecretAdventure.ChildAsync(factory, "scenery-stranger@petpal.test");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.GetAsync(first)).StatusCode);

        var forged = $"/api/pet-brain/runs/{run.RunId}/scenes/{new string('a', 64)}";
        Assert.Equal(HttpStatusCode.NotFound, (await client.Http.GetAsync(forged)).StatusCode);

        for (var step = 0; step < 12 && run.Stage!.Backdrop.AssetUrl == first; step++)
            run = await PetBrainPlaythrough.StepAsync(client, run);

        Assert.NotEqual(first, run.Stage!.Backdrop.AssetUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await client.Http.GetAsync(first)).StatusCode);
        Assert.Equal(1, factory.Provider.MostCallsForOneScene);
    }

    /// <summary>Rol seçimi obrazı YENİDƏN çəkdirir — ekran yeni ünvan alır.</summary>
    [Fact]
    public async Task RolSecimi_ObraziYenidenCekdirir()
    {
        using var factory = new PetBrainIllustrationFactory();

        var client = await MoonSecretAdventure.ChildAsync(factory, "scenery-role@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(factory, client);

        var before = run.Stage!.Portrait;

        var kit = await MoonSecretAdventure.AdvanceToAsync(client, run, "c1-kit", MoonKeys.RoleScientist);

        Assert.NotNull(kit);
        Assert.NotEqual(before.AssetUrl, kit.Stage!.Portrait.AssetUrl);
        Assert.NotEqual(before.AltText, kit.Stage.Portrait.AltText);
    }

    /// <summary>Hekayələrin qaytara biləcəyi bütün səhnə açarları — düyün, düyün effekti və variant effekti.</summary>
    private static IEnumerable<string> StorySceneVariants() =>
        StoryCatalog.Definitions
            .SelectMany(definition => definition.Nodes)
            .SelectMany(node =>
                new[] { node.SceneVariant }
                    .Concat(node.Effects.Where(IsScene).Select(e => e.Key))
                    .Concat(node.Options.SelectMany(o => o.Effects).Where(IsScene).Select(e => e.Key)))
            .Where(key => !string.IsNullOrEmpty(key))
            .Distinct(StringComparer.Ordinal);

    private static bool IsScene(ExperienceEffect effect) => effect.Kind == ExperienceEffectKind.SceneVariant;

    private static SceneryContext Moon(
        string sceneVariant,
        string[]? choices = null,
        string[]? flags = null,
        string[]? world = null,
        string[]? items = null,
        string language = "az",
        string species = "fox") =>
        new(ExperienceCatalog.MoonCrystalSecret, TraitKeys.Space, sceneVariant,
            Set(choices), Set(flags), Set(world), Set(items), species, language);

    private static SceneryContext Linear(string experienceKey, params string[] choices) =>
        SceneryContext.Linear(
            experienceKey,
            ExperienceCatalog.Find(experienceKey)!.Theme,
            sceneVariant: string.Empty,
            choices,
            species: "fox",
            language: "az");

    private static HashSet<string> Set(string[]? values) => new(values ?? [], StringComparer.Ordinal);

    private static async Task<PuzzleIllustration> EnsureAsync(TestWebAppFactory factory, IStoryScene scene)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<PuzzleIllustrationCoordinator>()
            .EnsureRowAsync(scene, CancellationToken.None);
    }

    private static async Task RenderAsync(TestWebAppFactory factory, IStoryScene scene)
    {
        using var scope = factory.Services.CreateScope();

        await scope.ServiceProvider
            .GetRequiredService<PuzzleIllustrationCoordinator>()
            .RenderAsync(scene.Hash(), scene, CancellationToken.None);
    }

    /// <summary>Arxa fon işçisi fonu və obrazı bitirənə qədər gözləyir — sabit gecikməsiz.</summary>
    private static async Task<PetBrainRunDto> WaitForArtAsync(ApiTestClient client, Guid runId)
    {
        PetBrainRunDto run = default!;

        for (var attempt = 0; attempt < 80; attempt++)
        {
            run = (await client.Http.GetFromJsonAsync<PetBrainRunDto>($"/api/pet-brain/runs/{runId}"))!;

            if (run.Stage is { } stage &&
                stage.Backdrop.IllustrationStatus != PetBrainIllustrationStatus.Pending &&
                stage.Portrait.IllustrationStatus != PetBrainIllustrationStatus.Pending)
                return run;

            await Task.Delay(25);
        }

        Assert.Fail("Arxa fon və obraz hazır olmadı.");

        return run;
    }
}
