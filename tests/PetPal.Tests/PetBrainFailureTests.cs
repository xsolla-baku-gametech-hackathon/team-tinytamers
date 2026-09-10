using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PetPal.Api.Ai;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Pet Brain-in nasazlıq davranışı.
///
/// <para>Əsas iddia budur: <b>model olmadan da hər şey işləyir.</b> Model
/// sıradan çıxsa, ləngisə, pozuq JSON qaytarsa və ya uşağa uyğun olmayan mətn
/// versə — uşaq XƏTA GÖRMÜR, çünki deterministik mətn onsuz da hazırdır.</para>
///
/// <para>Heç bir test real şəbəkəyə çıxmır.</para>
/// </summary>
public class PetBrainNarrativeFallbackTests
{
    private static NarrativeContext Context(string language = "az") => new(
        ChildId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Language: language,
        AgeBand: "9-10",
        Template: ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!,
        Difficulty: PetBrainDifficulty.Medium,
        PetName: "Luna",
        MemoryKeys: [ExperienceCatalog.MoonCrystalRescue]);

    /// <summary>AI söndürülübsə mətn kataloqdan gəlir və heç bir çağırış olmur.</summary>
    [Fact]
    public async Task AiSondurulub_DeterministikMetnQaytarir()
    {
        var narrative = await new TemplateNarrativeProvider().DescribeAsync(Context());

        var template = ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!;

        Assert.Equal(template.Title("az"), narrative.Title);
        Assert.Equal(template.Intro("az"), narrative.Intro);
        Assert.Equal(ExperienceNarrative.TemplateSource, narrative.Source);
    }

    /// <summary>
    /// Modelin hər cür nasazlığı eyni nəticə verir: deterministik şablon.
    /// </summary>
    [Theory]
    // Şəbəkə xətası
    [InlineData(null, null)]
    // HTTP 500
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    // Sürət limiti
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    // Pozuq JSON
    [InlineData(HttpStatusCode.OK, "{ this is not json")]
    // JSON var, amma sahələr əskikdir
    [InlineData(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"{\\\"title\\\":\\\"Yalnız başlıq\\\"}\"}}]}")]
    // Boş cavab
    [InlineData(HttpStatusCode.OK, "{\"choices\":[]}")]
    public async Task ModelNasazligi_SablonaQayidir(HttpStatusCode? status, string? body)
    {
        var provider = BuildProvider(status, body);

        var narrative = await provider.DescribeAsync(Context());

        Assert.Equal(ExperienceNarrative.TemplateSource, narrative.Source);
        Assert.Equal(ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!.Title("az"), narrative.Title);
    }

    /// <summary>
    /// Uşaq məzmununa uyğun OLMAYAN cavab qəbul edilmir — modelin öz filtri
    /// son söz deyil.
    /// </summary>
    [Theory]
    [InlineData("Qorxunc ölüm macərası")]
    [InlineData("Silahla döyüş")]
    [InlineData("Bax https://example.com")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("Şifrəni yaz")]
    public async Task TehlukeliMetn_SablonaQayidir(string unsafeTitle)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            title = unsafeTitle,
                            intro = "Gedək!"
                        })
                    }
                }
            }
        });

        var provider = BuildProvider(HttpStatusCode.OK, payload);

        var narrative = await provider.DescribeAsync(Context());

        Assert.Equal(ExperienceNarrative.TemplateSource, narrative.Source);
        Assert.DoesNotContain(unsafeTitle, narrative.Title);
    }

    /// <summary>Həddindən uzun başlıq da rədd olunur — kartda bir sətirdir.</summary>
    [Fact]
    public async Task HeddindenUzunBaslik_SablonaQayidir()
    {
        var payload = ModelReply(new string('a', 300), "Gedək!");

        var narrative = await BuildProvider(HttpStatusCode.OK, payload).DescribeAsync(Context());

        Assert.Equal(ExperienceNarrative.TemplateSource, narrative.Source);
    }

    /// <summary>Etibarlı cavab qəbul olunur və MƏNBƏYİ düzgün işarələnir.</summary>
    [Fact]
    public async Task EtibarliCavab_QebulOlunurVeMenbeyiIsarelenir()
    {
        var payload = ModelReply("Marsda gizli siqnal", "Robo bizi gözləyir, tez gedək!");

        var narrative = await BuildProvider(HttpStatusCode.OK, payload).DescribeAsync(Context());

        Assert.Equal(ExperienceNarrative.AiSource, narrative.Source);
        Assert.Equal("Marsda gizli siqnal", narrative.Title);
        Assert.Equal("Robo bizi gözləyir, tez gedək!", narrative.Intro);
    }

    /// <summary>Modelə uşağın sərbəst mətni GETMİR — yalnız təsdiqlənmiş açarlar.</summary>
    [Fact]
    public async Task ModeleGondherilen_YalnizTesdiqlenmisSahelerdir()
    {
        var handler = new StubHandler(HttpStatusCode.OK, ModelReply("Ad", "Giriş"));
        var provider = BuildProvider(handler);

        await provider.DescribeAsync(Context());

        Assert.NotNull(handler.LastRequestBody);

        var body = handler.LastRequestBody!;

        // Təsdiqlənmiş sahələr var.
        Assert.Contains(TraitKeys.Space, body, StringComparison.Ordinal);
        Assert.Contains("Luna", body, StringComparison.Ordinal);
        Assert.Contains("9-10", body, StringComparison.Ordinal);

        // Dəqiq yaş, uşağın adı və hər hansı sərbəst mətn YOXDUR.
        Assert.DoesNotContain("\"age\":9", body, StringComparison.OrdinalIgnoreCase);
    }

    private static string ModelReply(string title, string intro) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = System.Text.Json.JsonSerializer.Serialize(new { title, intro }) } }
            }
        });

    private static AiExperienceNarrativeProvider BuildProvider(HttpStatusCode? status, string? body) =>
        BuildProvider(new StubHandler(status, body));

    private static AiExperienceNarrativeProvider BuildProvider(StubHandler handler)
    {
        var options = Options.Create(new AiOptions
        {
            Provider = AiProvider.OpenAiCompatible,
            BaseUrl = "https://model.test/v1",
            Model = "test-model",
            TimeoutSeconds = 2
        });

        // Hər test öz keşi ilə işləyir — keş nəticələri bir-birinə sızmasın.
        return new AiExperienceNarrativeProvider(
            new HttpClient(handler),
            options,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<AiExperienceNarrativeProvider>.Instance);
    }

    /// <summary>Şəbəkəyə çıxmayan saxta model. <c>status</c> boşdursa şəbəkə xətası atır.</summary>
    private sealed class StubHandler(HttpStatusCode? status, string? body) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (status is null)
                throw new HttpRequestException("Model serveri əlçatmazdır (test).");

            return new HttpResponseMessage(status.Value)
            {
                Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json")
            };
        }
    }
}

/// <summary>
/// Davranış izləyicisinin idempotentliyi və gündəlik tavanı.
/// </summary>
public class PetBrainTrackerTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainTrackerTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>Eyni açarlı hadisə iki dəfə sayılmır — nə jurnalda, nə xassədə.</summary>
    [Fact]
    public async Task EyniAcarliHadise_XasseniIkiDefeDeyismir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "tracker-idem@petpal.test");

        await using var scope = _factory.Services.CreateAsyncScope();
        var tracker = scope.ServiceProvider.GetRequiredService<IBehaviorTracker>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var data = new PetBrainEventData(
            "mars-rover-rescue", "score:100", ProfileLearningRules.ForPuzzleSolved());

        var first = await tracker.TrackAsync(
            client.ChildId, PetBrainEventType.PuzzleSolved, data, "run-x:stage-2");
        await db.SaveChangesAsync();

        var second = await tracker.TrackAsync(
            client.ChildId, PetBrainEventType.PuzzleSolved, data, "run-x:stage-2");
        await db.SaveChangesAsync();

        Assert.True(first);
        Assert.False(second);

        var puzzles = await db.PlayerTraits.AsNoTracking().SingleAsync(t =>
            t.ChildProfileId == client.ChildId && t.Key == TraitKeys.Puzzles);

        Assert.Equal(TraitKeys.StartingScore + ProfileLearningRules.PuzzleSolvedPuzzles, puzzles.Score);

        Assert.Equal(1, await db.BehaviorEvents.CountAsync(e =>
            e.ChildProfileId == client.ChildId && e.IdempotencyKey == "run-x:stage-2"));
    }

    /// <summary>Naməlum açar səssizcə buraxılır — uydurma xassə yaranmır.</summary>
    [Fact]
    public async Task NamelumAcar_XasseYaratmir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "tracker-unknown@petpal.test");

        await using var scope = _factory.Services.CreateAsyncScope();
        var tracker = scope.ServiceProvider.GetRequiredService<IBehaviorTracker>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await tracker.TrackAsync(
            client.ChildId,
            PetBrainEventType.StageChoiceMade,
            new PetBrainEventData("x", "y",
                [new TraitAdjustment(PetBrainTraitCategory.Interest, "gambling", 5)]),
            null);

        await db.SaveChangesAsync();

        Assert.False(await db.PlayerTraits.AnyAsync(t =>
            t.ChildProfileId == client.ChildId && t.Key == "gambling"));
    }

    /// <summary>
    /// Bir sorğuda eyni açar gündəlik tavandan çox qazana bilmir — uşaq
    /// eyni işi təkrarlayıb profili şişirdə bilməsin.
    /// </summary>
    [Fact]
    public async Task GundelikTavan_BirAcarinArtimiMehdudlasdirir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "tracker-cap@petpal.test");

        await using var scope = _factory.Services.CreateAsyncScope();
        var tracker = scope.ServiceProvider.GetRequiredService<IBehaviorTracker>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var i = 0; i < 10; i++)
            await tracker.TrackAsync(
                client.ChildId,
                PetBrainEventType.PuzzleSolved,
                new PetBrainEventData("mars-rover-rescue", $"try:{i}", ProfileLearningRules.ForPuzzleSolved()),
                $"cap-test:{i}");

        await db.SaveChangesAsync();

        var puzzles = await db.PlayerTraits.AsNoTracking().SingleAsync(t =>
            t.ChildProfileId == client.ChildId && t.Key == TraitKeys.Puzzles);

        Assert.Equal(
            TraitKeys.StartingScore + ProfileLearningRules.DailyGainCapPerKey,
            puzzles.Score);
    }

    /// <summary>Xassə balı hər halda 0–100 aralığında qalır.</summary>
    [Fact]
    public async Task Xasse_YuzBaldanYuxariQalxmir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "tracker-clamp@petpal.test");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tracker = scope.ServiceProvider.GetRequiredService<IBehaviorTracker>();

        db.PlayerTraits.Add(new PlayerTrait
        {
            ChildProfileId = client.ChildId,
            Category = PetBrainTraitCategory.Interest,
            Key = TraitKeys.Puzzles,
            Score = 99,
            UpdatedAt = _factory.Clock.GetUtcNow().UtcDateTime
        });

        await db.SaveChangesAsync();

        await tracker.TrackAsync(
            client.ChildId,
            PetBrainEventType.PuzzleSolved,
            new PetBrainEventData("mars", "x", ProfileLearningRules.ForPuzzleSolved()),
            "clamp-test");

        await db.SaveChangesAsync();

        var puzzles = await db.PlayerTraits.AsNoTracking().SingleAsync(t =>
            t.ChildProfileId == client.ChildId && t.Key == TraitKeys.Puzzles);

        Assert.Equal(100, puzzles.Score);
    }
}

/// <summary>Nümayiş toxumu AÇIQ olan fixture.</summary>
public sealed class DemoSeedFactory : TestWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("PetBrain:SeedDemoData", "true");
    }
}

public class PetBrainDemoSeedTests : IClassFixture<DemoSeedFactory>
{
    private readonly DemoSeedFactory _factory;

    public PetBrainDemoSeedTests(DemoSeedFactory factory) => _factory = factory;

    /// <summary>
    /// Toxum iki profil yaradır və onlar EYNİ pet-i daşıyır — nümayişin bütün
    /// mənası budur: eyni oyun, eyni pet, fərqli uşaq.
    /// </summary>
    [Fact]
    public async Task NumayisToxumu_EyniPetliIkiProfilYaradir()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var children = await db.ChildProfiles
            .Include(c => c.Pet)
            .Where(c => c.DisplayName == "Aylin" || c.DisplayName == "Mia")
            .ToListAsync();

        Assert.Equal(2, children.Count);

        var pets = children.Select(c => c.Pet!).ToList();

        // Görünən hər şey eynidir: ad, növ, yaş, statlar.
        Assert.Single(pets.Select(p => p.Name).Distinct());
        Assert.Single(pets.Select(p => p.Species).Distinct());
        Assert.Single(pets.Select(p => p.Level).Distinct());
        Assert.Single(pets.Select(p => p.Happiness).Distinct());

        // Amma hər uşağın PET-i ÖZÜNÜNKÜDÜR — bir sətir paylaşılmır.
        Assert.Equal(2, pets.Select(p => p.Id).Distinct().Count());

        // Hər ikisi çıxıb: yumurta açmaq nümayişin mövzusu deyil.
        Assert.All(pets, pet => Assert.NotNull(pet.HatchedAt));
    }

    /// <summary>İki profil FƏRQLİ tövsiyə alır — nümayişin sərt tələbi.</summary>
    [Fact]
    public async Task NumayisProfilleri_FerqliMaceraAlir()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPetBrainService>();

        var aylin = await db.ChildProfiles.FirstAsync(c => c.DisplayName == "Aylin");
        var mia = await db.ChildProfiles.FirstAsync(c => c.DisplayName == "Mia");

        var aylinState = await service.GetStateAsync(aylin.Id);
        var miaState = await service.GetStateAsync(mia.Id);

        Assert.Equal(ExperienceCatalog.MarsRoverRescue, aylinState.Value!.Recommendation!.TemplateKey);
        Assert.Equal(ExperienceCatalog.DragonLostColors, miaState.Value!.Recommendation!.TemplateKey);
    }

    /// <summary>Toxum təkrar atılanda ikinci dəst yaranmır.</summary>
    [Fact]
    public async Task NumayisToxumu_Idempotentdir()
    {
        await DemoDataSeeder.SeedAsync(_factory.Services);
        await DemoDataSeeder.SeedAsync(_factory.Services);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == "demo@petpal.test"));
        Assert.Equal(1, await db.ChildProfiles.CountAsync(c => c.DisplayName == "Aylin"));
        Assert.Equal(1, await db.ChildProfiles.CountAsync(c => c.DisplayName == "Mia"));
    }
}

/// <summary>
/// Standart konfiqurasiya: nümayiş toxumu BAĞLIDIR.
/// </summary>
public class PetBrainDemoSeedDisabledTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainDemoSeedDisabledTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>
    /// Açar verilməyibsə heç bir nümayiş hesabı yaranmır — produksiyada
    /// nümayiş profilinin səssizcə peyda olması ən bahalı səhv olardı.
    /// </summary>
    [Fact]
    public async Task ToxumBagliOlanda_NumayisHesabiYaranmir()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(await db.Users.AnyAsync(u => u.Email == "demo@petpal.test"));
        Assert.False(await db.ChildProfiles.AnyAsync(c => c.DisplayName == "Aylin"));
    }

    /// <summary>
    /// Nümayiş rejimi bağlı olanda izah paneli UŞAĞA GÖNDƏRİLMİR — bal, namizəd
    /// cədvəli və direktor səbəbləri adi uşaq axınında görünməməlidir.
    /// </summary>
    [Fact]
    public async Task NumayisRejimiBagli_IzahPaneliGondermir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "no-demo@petpal.test");
        await client.HatchAsync(_factory);

        var state = await client.Http.GetFromJsonAsync<Shared.Dtos.PetBrain.PetBrainStateDto>("/api/pet-brain");

        Assert.NotNull(state);
        Assert.False(state!.DemoMode);
        Assert.Null(state.Debug);
    }
}

/// <summary>
/// AI mətn qatı AÇIQ, model isə sıradan çıxıb.
///
/// <para>Bu fixture bir boşluğu bağlayır: <c>UseAiNarrative</c> açıq olanda
/// <c>Program.cs</c> BAŞQA implementasiya qeydiyyatdan keçirir
/// (<c>AddHttpClient&lt;IExperienceNarrativeProvider, AiExperienceNarrativeProvider&gt;</c>).
/// Həmin yol səhv qurulsaydı, app YALNIZ bu konfiqurasiyada açılışda sınardı və
/// heç bir test onu tutmazdı.</para>
/// </summary>
public sealed class AiNarrativeFactory : TestWebAppFactory
{
    /// <summary>Model həmişə xəta qaytarır — uşağın bunu hiss ETMƏMƏSİ yoxlanılır.</summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("PetBrain:UseAiNarrative", "true");
        builder.UseSetting("Ai:Provider", "OpenAiCompatible");
        builder.UseSetting("Ai:BaseUrl", "https://model.test/v1");
        builder.UseSetting("Ai:Model", "test-model");

        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<IExperienceNarrativeProvider, AiExperienceNarrativeProvider>()
                .ConfigurePrimaryHttpMessageHandler(() => new BrokenModelHandler());
        });
    }

    private sealed class BrokenModelHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Model serveri əlçatmazdır (test).");
    }
}

public class PetBrainAiEnabledTests : IClassFixture<AiNarrativeFactory>
{
    private readonly AiNarrativeFactory _factory;

    public PetBrainAiEnabledTests(AiNarrativeFactory factory) => _factory = factory;

    /// <summary>AI qatı açıq olanda DI qrafiki qurulur və app açılır.</summary>
    [Fact]
    public void AiQatiAcigOlanda_ModelImplementasiyasiQeydiyyatdadir()
    {
        using var scope = _factory.Services.CreateScope();

        var provider = scope.ServiceProvider.GetRequiredService<IExperienceNarrativeProvider>();

        Assert.IsType<AiExperienceNarrativeProvider>(provider);
    }

    /// <summary>
    /// Model tamamilə sıradan çıxıb, amma uşaq XƏTA GÖRMÜR: tövsiyə gəlir,
    /// mətn deterministik şablondandır və macəra oynanır.
    /// </summary>
    [Fact]
    public async Task ModelSiradanCixib_UsaqXetaGormur()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "ai-down@petpal.test", "Aylin");
        await client.HatchAsync(_factory);

        var response = await client.Http.GetAsync("/api/pet-brain");
        response.EnsureSuccessStatusCode();

        var state = (await response.Content.ReadFromJsonAsync<Shared.Dtos.PetBrain.PetBrainStateDto>())!;

        Assert.NotNull(state.Recommendation);
        Assert.False(string.IsNullOrWhiteSpace(state.Recommendation!.Title));
        Assert.False(string.IsNullOrWhiteSpace(state.Recommendation.Intro));

        // Mənbə DÜRÜST göstərilir: model işləmədiyi üçün mətn şablondandır.
        Assert.Equal(ExperienceNarrative.TemplateSource, state.Recommendation.NarrativeSource);

        // Başlıq kataloqdakı mətnlə eynidir — yəni həqiqətən şablondan gəlib.
        var template = ExperienceCatalog.Find(state.Recommendation.TemplateKey)!;
        Assert.Equal(template.Title("az"), state.Recommendation.Title);

        // Və macəra başlaya bilir.
        var start = await client.Http.PostAsJsonAsync(
            "/api/pet-brain/runs",
            new Shared.Dtos.PetBrain.StartPetBrainRunRequest());

        start.EnsureSuccessStatusCode();
    }
}
