using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PetPal.Api.Ai;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Pet Brain V2 üçün TƏMƏL zəmanətlər.
///
/// <para>Buradakı testlərin hər biri məhz bir P0 riskə cavabdır: şəxsi mətnin
/// başqa uşağa getməsi, gündəlik tavanın sorğular arasında sıfırlanması, bir
/// uşaqda iki açıq macəra, deploy zamanı yarımçıq run-un qaydalarının
/// dəyişməsi, yad hekayənin tapmacası və xarakterin hər sorğuda sürüşməsi.</para>
/// </summary>
public class PetBrainNarrativeCacheIsolationTests
{
    private static readonly Guid AylinId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MiaId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static NarrativeContext Context(
        Guid childId, string petName = "Luna", params string[] memoryKeys) => new(
        ChildId: childId,
        Language: "az",
        AgeBand: "9-10",
        Template: ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!,
        Difficulty: PetBrainDifficulty.Medium,
        PetName: petName,
        MemoryKeys: memoryKeys.Length == 0 ? [ExperienceCatalog.MoonCrystalRescue] : memoryKeys);

    /// <summary>
    /// İki uşaq eyni şablonu, dili, çətinliyi və yaş zolağını paylaşsa da,
    /// şəxsiləşdirilmiş mətni PAYLAŞMIR.
    /// </summary>
    [Fact]
    public void IkiUsaq_EyniKesSetriniPaylasmir()
    {
        var aylin = AiExperienceNarrativeProvider.CacheKey(Context(AylinId));
        var mia = AiExperienceNarrativeProvider.CacheKey(Context(MiaId));

        Assert.NotEqual(aylin, mia);
    }

    /// <summary>Eyni kontekst eyni açar verir — keş həqiqətən işləməlidir.</summary>
    [Fact]
    public void EyniKontekst_EyniAcarVerir() =>
        Assert.Equal(
            AiExperienceNarrativeProvider.CacheKey(Context(AylinId)),
            AiExperienceNarrativeProvider.CacheKey(Context(AylinId)));

    /// <summary>Pet-in adı prompta düşür, deməli açara da düşməlidir.</summary>
    [Fact]
    public void PetAdiDeyisende_AcarDeyisir() =>
        Assert.NotEqual(
            AiExperienceNarrativeProvider.CacheKey(Context(AylinId, "Luna")),
            AiExperienceNarrativeProvider.CacheKey(Context(AylinId, "Bulud")));

    /// <summary>Yaddaş açarı dəyişəndə köhnə şəxsi mətn qaytarılmır.</summary>
    [Fact]
    public void YaddasDeyisende_AcarDeyisir() =>
        Assert.NotEqual(
            AiExperienceNarrativeProvider.CacheKey(Context(AylinId, "Luna", "space")),
            AiExperienceNarrativeProvider.CacheKey(Context(AylinId, "Luna", "space", "ocean")));

    /// <summary>Yaddaş açarlarının SIRASI açarı dəyişmir — yoxsa keş heç vaxt tutmazdı.</summary>
    [Fact]
    public void YaddasSirasi_AcariDeyismir() =>
        Assert.Equal(
            AiExperienceNarrativeProvider.CacheKey(Context(AylinId, "Luna", "space", "ocean")),
            AiExperienceNarrativeProvider.CacheKey(Context(AylinId, "Luna", "ocean", "space")));

    /// <summary>Açarda nə xam pet adı, nə də yaddaş açarı görünür.</summary>
    [Fact]
    public void Acar_XamSexsiMetnDasimir()
    {
        var key = AiExperienceNarrativeProvider.CacheKey(Context(AylinId, "Bulud", "moon-crystal-rescue"));

        Assert.DoesNotContain("Bulud", key, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("moon-crystal-rescue", key, StringComparison.Ordinal);
        Assert.DoesNotContain(AylinId.ToString("N"), key, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Uçdan-uca reqressiya: model Aylin üçün şəxsi başlıq qaytarır, sonra
    /// Mia eyni şablonu istəyir. Mia Aylinin mətnini ALMAMALIDIR.
    /// </summary>
    [Fact]
    public async Task ModelMetni_BasqaUsagaSizmir()
    {
        var handler = new SequenceHandler(
        [
            Reply("Aylinin Marsı", "Luna səni Marsda gözləyir."),
            Reply("Mianın Marsı", "Bulud səni Marsda gözləyir.")
        ]);

        var provider = BuildProvider(handler);

        var first = await provider.DescribeAsync(Context(AylinId, "Luna"));
        var second = await provider.DescribeAsync(Context(MiaId, "Bulud"));

        Assert.Equal("Aylinin Marsı", first.Title);
        Assert.Equal("Mianın Marsı", second.Title);

        // İkinci uşaq üçün modelə HƏQİQƏTƏN müraciət olunub — yəni keş onu
        // birincinin cavabı ilə "qısa yoldan" keçirməyib.
        Assert.Equal(2, handler.Calls);
    }

    /// <summary>Eyni uşaq eyni kontekstlə ikinci dəfə soruşanda model ÇAĞIRILMIR.</summary>
    [Fact]
    public async Task EyniUsaq_IkinciDefeKesdenGelir()
    {
        var handler = new SequenceHandler([Reply("Aylinin Marsı", "Luna səni Marsda gözləyir.")]);
        var provider = BuildProvider(handler);

        var first = await provider.DescribeAsync(Context(AylinId, "Luna"));
        var second = await provider.DescribeAsync(Context(AylinId, "Luna"));

        Assert.Equal(first.Title, second.Title);
        Assert.Equal(1, handler.Calls);
    }

    private static string Reply(string title, string intro) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = System.Text.Json.JsonSerializer.Serialize(new { title, intro })
                    }
                }
            }
        });

    private static AiExperienceNarrativeProvider BuildProvider(SequenceHandler handler) =>
        new(new HttpClient(handler),
            Options.Create(new AiOptions
            {
                Provider = AiProvider.OpenAiCompatible,
                BaseUrl = "https://model.test/v1",
                Model = "test-model",
                TimeoutSeconds = 2
            }),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<AiExperienceNarrativeProvider>.Instance);

    /// <summary>Növbə ilə hazır cavab verən, şəbəkəyə çıxmayan model.</summary>
    private sealed class SequenceHandler(IReadOnlyList<string> replies) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = replies[Math.Min(Calls, replies.Count - 1)];
            Calls++;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}

/// <summary>
/// Gündəlik xassə tavanı ARTIQ sorğu daxilində deyil, gündədir.
/// </summary>
public class PetBrainDailyCapTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainDailyCapTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>
    /// AYRI sorğular (ayrı DI scope) birlikdə də tavanı keçmir.
    ///
    /// <para>Köhnə sayğac sorğu ömrü boyu yaşayırdı, ona görə hər yeni sorğu
    /// sıfırdan başlayır və eyni açar günə istənilən qədər artırıla bilirdi.</para>
    /// </summary>
    [Fact]
    public async Task AyriSorgular_GundelikTavaniKecmir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "daily-cap@petpal.test", "Aylin");
        var now = _factory.Clock.GetUtcNow().UtcDateTime;

        var granted = 0;

        for (var i = 0; i < 10; i++)
            granted += await ReserveInOwnScopeAsync(client.ChildId, now, requested: 3);

        Assert.Equal(ProfileLearningRules.DailyGainCapPerKey, granted);
    }

    /// <summary>PARALEL sorğular da birlikdə tavanı keçmir.</summary>
    [Fact]
    public async Task ParalelSorgular_GundelikTavaniKecmir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "daily-cap-parallel@petpal.test", "Mia");
        var now = _factory.Clock.GetUtcNow().UtcDateTime;

        var results = new int[8];

        // Sıra ilə deyil, EYNİ ANDA: hər biri öz scope-unda, öz bağlantısında.
        await Parallel.ForAsync(0, results.Length, async (index, ct) =>
            results[index] = await ReserveInOwnScopeAsync(client.ChildId, now, requested: 2, ct));

        Assert.Equal(ProfileLearningRules.DailyGainCapPerKey, results.Sum());
    }

    /// <summary>Yeni gün sayğacı sıfırlayır — tavan GÜNLÜKDÜR, ömürlük deyil.</summary>
    [Fact]
    public async Task YeniGun_SayğacıSifirlayir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "daily-cap-nextday@petpal.test", "Nur");
        var today = _factory.Clock.GetUtcNow().UtcDateTime;

        var first = await ReserveInOwnScopeAsync(client.ChildId, today, requested: 99);
        var second = await ReserveInOwnScopeAsync(client.ChildId, today.AddDays(1), requested: 99);

        Assert.Equal(ProfileLearningRules.DailyGainCapPerKey, first);
        Assert.Equal(ProfileLearningRules.DailyGainCapPerKey, second);
    }

    /// <summary>
    /// Bir uşağın tavanı BAŞQA uşağın payını yemir.
    /// </summary>
    [Fact]
    public async Task TavanUsaqBasinaSaxlanilir()
    {
        var aylin = await ApiTestClient.CreateAsync(_factory, "cap-a@petpal.test", "Aylin");
        var mia = await ApiTestClient.CreateAsync(_factory, "cap-b@petpal.test", "Mia");
        var now = _factory.Clock.GetUtcNow().UtcDateTime;

        await ReserveInOwnScopeAsync(aylin.ChildId, now, requested: 99);
        var miaGranted = await ReserveInOwnScopeAsync(mia.ChildId, now, requested: 99);

        Assert.Equal(ProfileLearningRules.DailyGainCapPerKey, miaGranted);
    }

    private async Task<int> ReserveInOwnScopeAsync(
        Guid childId, DateTime now, int requested, CancellationToken ct = default)
    {
        using var scope = _factory.Services.CreateScope();
        var ledger = scope.ServiceProvider.GetRequiredService<TraitDailyLedger>();

        return await ledger.ReserveAsync(
            childId,
            PetBrainTraitCategory.Interest,
            TraitKeys.Space,
            now,
            requested,
            ProfileLearningRules.DailyGainCapPerKey,
            ct);
    }
}

/// <summary>
/// Bir uşaqda EYNİ ANDA yalnız bir açıq macəra ola bilər — zəmanət bazadadır.
/// </summary>
public class PetBrainActiveRunInvariantTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainActiveRunInvariantTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>
    /// İki paralel "başla" sorğusu: biri macərəni yaradır, digəri EYNİ macərəni
    /// alır. Nə ikinci run yaranır, nə də idarə olunmayan baza xətası çıxır.
    /// </summary>
    [Fact]
    public async Task ParalelBaslatma_YalnizBirAcikRunYaradir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "parallel-start@petpal.test", "Aylin");
        await client.HatchAsync(_factory);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            client.Http.PostAsJsonAsync("/api/pet-brain/runs", new StartPetBrainRunRequest())));

        foreach (var response in responses)
            Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
                $"Gözlənilməz cavab: {(int)response.StatusCode}");

        var runIds = new List<Guid>();
        foreach (var response in responses.Where(r => r.StatusCode == HttpStatusCode.OK))
            runIds.Add((await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!.RunId);

        Assert.NotEmpty(runIds);
        Assert.Single(runIds.Distinct());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var active = await db.ExperienceRuns
            .CountAsync(r => r.ChildProfileId == client.ChildId && r.Status == PetBrainRunStatus.Active);

        Assert.Equal(1, active);
    }

    /// <summary>
    /// İkinci açıq run BAZA səviyyəsində rədd olunur — servis qatı yan keçilsə də.
    /// </summary>
    [Fact]
    public async Task IkinciAcikRun_BazadaRedOlunur()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "unique-active@petpal.test", "Mia");
        await client.HatchAsync(_factory);

        (await client.Http.PostAsJsonAsync("/api/pet-brain/runs", new StartPetBrainRunRequest()))
            .EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.ExperienceRuns.Add(new ExperienceRun
        {
            ChildProfileId = client.ChildId,
            TemplateKey = ExperienceCatalog.OceanGlowQuest,
            DefinitionVersion = 1,
            ExperienceType = PetBrainExperienceType.Adventure,
            Theme = TraitKeys.Ocean,
            Difficulty = PetBrainDifficulty.Easy,
            Status = PetBrainRunStatus.Active,
            StartedAt = _factory.Clock.GetUtcNow().UtcDateTime
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    /// <summary>
    /// Tamamlanmış və yarımçıq run-lar limitə düşmür: indeks yalnız AÇIQ
    /// macərəyə baxır, tarixçə isə istənilən qədər ola bilər.
    /// </summary>
    [Fact]
    public async Task BitmisRunlar_LimiteDusmur()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "history-runs@petpal.test", "Nur");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = _factory.Clock.GetUtcNow().UtcDateTime;

        foreach (var status in new[]
                 {
                     PetBrainRunStatus.Completed, PetBrainRunStatus.Completed, PetBrainRunStatus.Abandoned
                 })
            db.ExperienceRuns.Add(new ExperienceRun
            {
                ChildProfileId = client.ChildId,
                TemplateKey = ExperienceCatalog.MoonCrystalRescue,
                DefinitionVersion = 1,
                ExperienceType = PetBrainExperienceType.Adventure,
                Theme = TraitKeys.Space,
                Difficulty = PetBrainDifficulty.Easy,
                Status = status,
                StartedAt = now,
                CompletedAt = now
            });

        await db.SaveChangesAsync();

        Assert.Equal(3, await db.ExperienceRuns.CountAsync(r => r.ChildProfileId == client.ChildId));
    }
}

/// <summary>
/// Versiya semantikası: yarımçıq macərə başladığı qaydalarla davam edir.
/// </summary>
public class PetBrainVersioningTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainVersioningTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>Başlayan run kataloqun cari versiyasını yadda saxlayır.</summary>
    [Fact]
    public async Task Baslayan_Run_TerifVersiyasiniSaxlayir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "version-start@petpal.test", "Aylin");
        await client.HatchAsync(_factory);

        var start = await client.Http.PostAsJsonAsync("/api/pet-brain/runs", new StartPetBrainRunRequest());
        start.EnsureSuccessStatusCode();

        var dto = (await start.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var run = await db.ExperienceRuns.AsNoTracking().FirstAsync(r => r.Id == dto.RunId);
        var template = ExperienceCatalog.Find(run.TemplateKey)!;

        // Budaqlanan macərada həqiqətin mənbəyi QRAFIN versiyasıdır: run onunla
        // oynanır və onunla bitirilməlidir.
        var expected = StoryCatalog.Find(run.TemplateKey)?.Version ?? template.Version;

        Assert.Equal(expected, run.DefinitionVersion);
    }

    /// <summary>
    /// Deploy kataloqu dəyişsə də (run başqa versiya ilə yazılıb), uşağın
    /// yarımçıq macərası açılır və oynanmağa davam edir — <c>404</c> ilə itmir.
    /// </summary>
    [Fact]
    public async Task KohneVersiyaliRun_BerpaOlunur()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "version-resume@petpal.test", "Mia");
        await client.HatchAsync(_factory);

        var start = await client.Http.PostAsJsonAsync("/api/pet-brain/runs", new StartPetBrainRunRequest());
        var run = (await start.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.ExperienceRuns.FirstAsync(r => r.Id == run.RunId);

            row.DefinitionVersion = 99;
            await db.SaveChangesAsync();
        }

        var resumed = await client.Http.GetAsync($"/api/pet-brain/runs/{run.RunId}");
        resumed.EnsureSuccessStatusCode();

        var dto = (await resumed.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        Assert.Equal(run.RunId, dto.RunId);
        Assert.Equal(PetBrainRunStatus.Active, dto.Status);
        Assert.NotNull(dto.Stage);
    }

    /// <summary>
    /// Versiyalaşdırmadan ƏVVƏL yazılmış sətir (<c>0</c>) cari tərifə bağlanır.
    /// </summary>
    [Fact]
    public void SifirVersiya_CariTerifeBaglanir()
    {
        var lookup = ExperienceCatalog.Resolve(ExperienceCatalog.MarsRoverRescue, version: 0);

        Assert.NotNull(lookup.Template);
        Assert.True(lookup.Exact);
    }

    /// <summary>Uyğun gəlməyən versiya tərifi İTİRMİR, yalnız «dəqiq deyil» deyir.</summary>
    [Fact]
    public void UygunsuzVersiya_TerifiItirmir()
    {
        var lookup = ExperienceCatalog.Resolve(ExperienceCatalog.MarsRoverRescue, version: 99);

        Assert.NotNull(lookup.Template);
        Assert.False(lookup.Exact);
    }

    /// <summary>Naməlum açar heç bir versiyada tapılmır.</summary>
    [Fact]
    public void NamelumAcar_TapilmirVeDeqiqDeyil()
    {
        var lookup = ExperienceCatalog.Resolve("uydurma-macera", version: 1);

        Assert.Null(lookup.Template);
        Assert.False(lookup.Exact);
    }

    /// <summary>
    /// Köhnə versiyalı tapmaca CARİ qaydalarla qiymətləndirilmir: sətir
    /// oxunmaz sayılır və mərhələ üçün yeni tapmaca verilir.
    ///
    /// <para>Əks halda uşaq bir lövhəyə baxar, server isə başqa şərtlə
    /// yoxlayardı — doğru cavab səhv sayıla bilərdi.</para>
    /// </summary>
    [Fact]
    public async Task KohneVersiyaliTapmaca_YenidenVerilir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "puzzle-version@petpal.test", "Nur");
        await client.HatchAsync(_factory);

        // Profil QƏSDƏN toxumlanır: boş profildə direktorun seçimi uşağın
        // id-sindən asılı olur və tapmacasız şablon da düşə bilər — test isə
        // məhz TAPMACANIN versiyasını yoxlayır.
        await SeedPuzzleLovingProfileAsync(client.ChildId);

        var start = await client.Http.PostAsJsonAsync("/api/pet-brain/runs", new StartPetBrainRunRequest());
        var run = (await start.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        Assert.True(
            ExperienceCatalog.Find(run.TemplateKey)!.HasPuzzle,
            $"{run.TemplateKey} şablonunda tapmaca yoxdur — toxum düzgün işləmədi.");

        // Tapmaca run başlayanda öncədən verilir (bax UpcomingScene).
        Guid staleId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var issued = await db.IssuedPuzzles.FirstAsync(p => p.ExperienceRunId == run.RunId);

            staleId = issued.Id;
            issued.BlueprintVersion = 99;

            await db.SaveChangesAsync();
        }

        var resumed = await client.Http.GetAsync($"/api/pet-brain/runs/{run.RunId}");
        resumed.EnsureSuccessStatusCode();

        await using var check = _factory.Services.CreateAsyncScope();
        var verify = check.ServiceProvider.GetRequiredService<AppDbContext>();

        var current = await verify.IssuedPuzzles
            .AsNoTracking()
            .Where(p => p.ExperienceRunId == run.RunId)
            .ToListAsync();

        Assert.DoesNotContain(current, p => p.Id == staleId);
        Assert.All(current, p => Assert.Equal(
            PuzzleBlueprintCatalog.Find(p.BlueprintKey)!.Version, p.BlueprintVersion));
    }

    /// <summary>Kosmos/məntiq profili — tapmacalı macəra seçilsin deyə.</summary>
    private async Task SeedPuzzleLovingProfileAsync(Guid childId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = _factory.Clock.GetUtcNow().UtcDateTime;

        void Add(PetBrainTraitCategory category, string key, int score) =>
            db.PlayerTraits.Add(new PlayerTrait
            {
                ChildProfileId = childId,
                Category = category,
                Key = key,
                Score = score,
                UpdatedAt = now
            });

        Add(PetBrainTraitCategory.Interest, TraitKeys.Space, 95);
        Add(PetBrainTraitCategory.Interest, TraitKeys.Science, 88);
        Add(PetBrainTraitCategory.Interest, TraitKeys.Puzzles, 92);
        Add(PetBrainTraitCategory.Interest, TraitKeys.Fantasy, 10);
        Add(PetBrainTraitCategory.Interest, TraitKeys.Animals, 10);
        Add(PetBrainTraitCategory.Interest, TraitKeys.Stories, 10);
        Add(PetBrainTraitCategory.Interest, TraitKeys.Ocean, 10);
        Add(PetBrainTraitCategory.Interest, TraitKeys.Nature, 10);

        Add(PetBrainTraitCategory.PlayStyle, TraitKeys.ProblemSolver, 92);
        Add(PetBrainTraitCategory.PlayStyle, TraitKeys.Explorer, 85);
        Add(PetBrainTraitCategory.PlayStyle, TraitKeys.Creative, 10);
        Add(PetBrainTraitCategory.PlayStyle, TraitKeys.Playful, 10);
        Add(PetBrainTraitCategory.PlayStyle, TraitKeys.Caring, 10);

        await db.SaveChangesAsync();
    }
}

/// <summary>
/// Tapmaca HEKAYƏYƏ aid olmalıdır: mexanika uyğun gəlsə də mətn yad ola bilməz.
/// </summary>
public class PetBrainPuzzleCompatibilityTests
{
    private static readonly Guid ChildId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    /// <summary>Marsın hekayəsinə aid sözlər — başqa macərada görünməməlidir.</summary>
    private static readonly string[] MarsWords =
        ["Robo", "Mars", "antena", "antenna", "eniş modulu", "lander", "günəş stansiyası", "solar station"];

    /// <summary>
    /// Hər şablon üçün, hər oyun üslubu profilində, hər çətinlikdə: verilən
    /// tapmaca YALNIZ öz macərasına aid ola bilər.
    /// </summary>
    [Theory]
    [InlineData(ExperienceCatalog.MarsRoverRescue)]
    [InlineData(ExperienceCatalog.MoonCrystalRescue)]
    [InlineData(ExperienceCatalog.OceanGlowQuest)]
    [InlineData(ExperienceCatalog.RobotLabPuzzle)]
    [InlineData(ExperienceCatalog.DragonLostColors)]
    [InlineData(ExperienceCatalog.ForestFriendsParade)]
    public void HerSablon_OzHekayesinAidTapmacaAlir(string templateKey)
    {
        var template = ExperienceCatalog.Find(templateKey)!;
        var generator = new DeterministicPuzzleGenerator();

        foreach (var profile in Profiles())
        foreach (var difficulty in Enum.GetValues<PetBrainDifficulty>())
        {
            var generated = generator.Generate(ContextFor(template, profile, difficulty));

            Assert.True(
                generated.Blueprint.SupportsExperience(templateKey),
                $"{templateKey} → {generated.Blueprint.Key} uyğun deyil.");

            if (templateKey == ExperienceCatalog.MarsRoverRescue)
                continue;

            var text = string.Join(
                ' ',
                generated.Public.Title,
                generated.Public.StoryPrompt,
                generated.Public.Instruction,
                generated.Public.Hint,
                generated.Public.Scene.AltText,
                string.Join(' ', generated.Public.Nodes.Select(n => n.Label)),
                string.Join(' ', generated.Public.Items.Select(i => i.Label)));

            foreach (var word in MarsWords)
                Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Ay macərasında marşrut tapmacası ALINDIQDA o, Ayın öz paketindəndir.
    /// </summary>
    [Fact]
    public void AyMacerasi_AyinMarsrutPaketiniAlir()
    {
        var template = ExperienceCatalog.Find(ExperienceCatalog.MoonCrystalRescue)!;
        var generator = new DeterministicPuzzleGenerator();

        var generated = generator.Generate(
            ContextFor(template, Solver(), PetBrainDifficulty.Medium));

        Assert.Equal(PuzzleBlueprintCatalog.MoonCrystalRouteKey, generated.Blueprint.Key);
        Assert.Contains("kristal", generated.Public.Title, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Mars şablonu Marsın öz marşrutunu alır — dəyişiklik köhnə davranışı
    /// pozmayıb.
    /// </summary>
    [Fact]
    public void MarsMacerasi_MarsMarsrutunuAlir()
    {
        var template = ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!;
        var generator = new DeterministicPuzzleGenerator();

        var generated = generator.Generate(
            ContextFor(template, Solver(), PetBrainDifficulty.Medium));

        Assert.Equal(PuzzleBlueprintCatalog.MarsSignalRouteKey, generated.Blueprint.Key);
        Assert.Contains("Robo", generated.Public.Title, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tapmaca mərhələsi olan HƏR şablon üçün kataloqda ən azı bir uyğun
    /// mexanika var — yoxsa uşaq ehtiyat variantına düşərdi.
    /// </summary>
    [Fact]
    public void TapmacaliSablonlar_UygunMexanikaTapir()
    {
        foreach (var template in ExperienceCatalog.Templates.Where(t => t.HasPuzzle))
            Assert.Contains(PuzzleBlueprintCatalog.Blueprints, b =>
                b.ExperienceType == template.Type
                && b.MinAge <= 10
                && b.SupportsExperience(template.Key));
    }

    private static IEnumerable<Dictionary<string, int>[]> Profiles()
    {
        yield return Solver();
        yield return Explorer();
        yield return CreativeChild();
    }

    private static Dictionary<string, int>[] Solver() =>
    [
        new()
        {
            [TraitKeys.Space] = 88, [TraitKeys.Science] = 80,
            [TraitKeys.Puzzles] = 92, [TraitKeys.Ocean] = 30,
            [TraitKeys.Nature] = 25, [TraitKeys.Animals] = 20,
            [TraitKeys.Fantasy] = 20, [TraitKeys.Stories] = 25
        },
        new()
        {
            [TraitKeys.ProblemSolver] = 92, [TraitKeys.Explorer] = 70,
            [TraitKeys.Creative] = 20, [TraitKeys.Playful] = 20, [TraitKeys.Caring] = 20
        }
    ];

    private static Dictionary<string, int>[] Explorer() =>
    [
        new()
        {
            [TraitKeys.Space] = 40, [TraitKeys.Science] = 30,
            [TraitKeys.Puzzles] = 35, [TraitKeys.Ocean] = 90,
            [TraitKeys.Nature] = 85, [TraitKeys.Animals] = 60,
            [TraitKeys.Fantasy] = 30, [TraitKeys.Stories] = 35
        },
        new()
        {
            [TraitKeys.ProblemSolver] = 25, [TraitKeys.Explorer] = 95,
            [TraitKeys.Creative] = 25, [TraitKeys.Playful] = 30, [TraitKeys.Caring] = 30
        }
    ];

    private static Dictionary<string, int>[] CreativeChild() =>
    [
        new()
        {
            [TraitKeys.Space] = 20, [TraitKeys.Science] = 25,
            [TraitKeys.Puzzles] = 20, [TraitKeys.Ocean] = 30,
            [TraitKeys.Nature] = 40, [TraitKeys.Animals] = 88,
            [TraitKeys.Fantasy] = 92, [TraitKeys.Stories] = 85
        },
        new()
        {
            [TraitKeys.ProblemSolver] = 20, [TraitKeys.Explorer] = 30,
            [TraitKeys.Creative] = 95, [TraitKeys.Playful] = 70, [TraitKeys.Caring] = 65
        }
    ];

    private static PuzzleGenerationContext ContextFor(
        ExperienceTemplate template, Dictionary<string, int>[] profile, PetBrainDifficulty difficulty) => new(
        ChildId: ChildId,
        RunId: Guid.Parse("55555555-5555-5555-5555-555555555555"),
        StageIndex: 2,
        Age: 10,
        Language: "az",
        TemplateKey: template.Key,
        ExperienceType: template.Type,
        Theme: template.Theme,
        Interests: profile[0],
        PlayStyles: profile[1],
        Difficulty: difficulty,
        MechanicTiers: new Dictionary<string, PetBrainDifficulty>(),
        MasteryTargetDifficulty: 5,
        Assisted: false,
        RecentSignatures: new HashSet<string>(StringComparer.Ordinal));
}

/// <summary>
/// Xarakter SABİTDİR: kiçik bal dəyişikliyi onu hər sorğuda atlatmır.
/// </summary>
public class PetBrainPersonalityPersistenceTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainPersonalityPersistenceTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>Ekrana çıxan xarakter bazada da SAXLANILIR.</summary>
    [Fact]
    public async Task Xarakter_BazadaSaxlanilir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "personality-store@petpal.test", "Aylin");
        await client.HatchAsync(_factory);
        await SetTraitsAsync(client.ChildId, scientist: 80, explorer: 30);

        var state = await ReadStateAsync(client);

        Assert.Equal(PetBrainPersonality.CuriousScientist, state.Personality);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var child = await db.ChildProfiles.AsNoTracking().FirstAsync(c => c.Id == client.ChildId);

        Assert.Equal(PetBrainPersonality.CuriousScientist, child.Personality);
        Assert.NotNull(child.PersonalityChangedAt);
    }

    /// <summary>
    /// Rəqib istiqamət CÜZİ fərqlə qabağa keçəndə xarakter dəyişmir —
    /// histerezis məhz bunun üçündür.
    /// </summary>
    [Fact]
    public async Task CuziFerq_XarakteriDeyismir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "personality-hysteresis@petpal.test", "Mia");
        await client.HatchAsync(_factory);

        await SetTraitsAsync(client.ChildId, scientist: 80, explorer: 30);
        Assert.Equal(PetBrainPersonality.CuriousScientist, (await ReadStateAsync(client)).Personality);

        // Kəşfiyyatçı ox indi bir-iki bal qabaqdadır — SwitchMargin-dən azdır.
        await SetTraitsAsync(client.ChildId, scientist: 78, explorer: 80);

        Assert.Equal(PetBrainPersonality.CuriousScientist, (await ReadStateAsync(client)).Personality);
    }

    /// <summary>AYDIN fərq isə keçidi doğrudan həyata keçirir.</summary>
    [Fact]
    public async Task AydinFerq_XarakteriDeyisir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "personality-switch@petpal.test", "Nur");
        await client.HatchAsync(_factory);

        await SetTraitsAsync(client.ChildId, scientist: 80, explorer: 30);
        Assert.Equal(PetBrainPersonality.CuriousScientist, (await ReadStateAsync(client)).Personality);

        await SetTraitsAsync(client.ChildId, scientist: 45, explorer: 95);

        Assert.Equal(PetBrainPersonality.ExplorerCompanion, (await ReadStateAsync(client)).Personality);
    }

    private static async Task<PetBrainStateDto> ReadStateAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync("/api/pet-brain");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PetBrainStateDto>())!;
    }

    private async Task SetTraitsAsync(Guid childId, int scientist, int explorer)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = _factory.Clock.GetUtcNow().UtcDateTime;

        await UpsertAsync(PetBrainTraitCategory.Interest, TraitKeys.Science, scientist);
        await UpsertAsync(PetBrainTraitCategory.Interest, TraitKeys.Puzzles, scientist);
        await UpsertAsync(PetBrainTraitCategory.PlayStyle, TraitKeys.ProblemSolver, scientist);

        await UpsertAsync(PetBrainTraitCategory.Interest, TraitKeys.Space, explorer);
        await UpsertAsync(PetBrainTraitCategory.Interest, TraitKeys.Nature, explorer);
        await UpsertAsync(PetBrainTraitCategory.PlayStyle, TraitKeys.Explorer, explorer);

        await db.SaveChangesAsync();

        async Task UpsertAsync(PetBrainTraitCategory category, string key, int score)
        {
            var trait = await db.PlayerTraits.FirstOrDefaultAsync(t =>
                t.ChildProfileId == childId && t.Category == category && t.Key == key);

            if (trait is null)
            {
                trait = new PlayerTrait { ChildProfileId = childId, Category = category, Key = key };
                db.PlayerTraits.Add(trait);
            }

            trait.Score = score;
            trait.UpdatedAt = now;
        }
    }
}

/// <summary>
/// Yaddaşın saxlama limiti PRODUKSİYA yazı yolunda tətbiq olunur.
/// </summary>
public class PetBrainMemoryPruneTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainMemoryPruneTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>
    /// Limitdən çox xatirəsi olan uşaq bir macərəni bitirəndə köhnə və az
    /// vacib sətirlər TƏMİZLƏNİR — yaddaş sonsuz böyümür.
    /// </summary>
    [Fact]
    public async Task TamamlamaZamani_LimitdenArtiqXatireSilinir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "memory-prune@petpal.test", "Aylin");
        await client.HatchAsync(_factory);

        var now = _factory.Clock.GetUtcNow().UtcDateTime;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            for (var i = 0; i < MemoryPolicy.RetentionLimit + 15; i++)
                db.PetMemories.Add(new PetMemory
                {
                    ChildProfileId = client.ChildId,
                    Kind = PetBrainMemoryKind.ChoiceMade,
                    FactKey = $"legacy-{i}",
                    ValueKey = $"value-{i}",
                    Importance = 5,
                    CreatedAt = now.AddDays(-i - 1)
                });

            await db.SaveChangesAsync();
        }

        // Bütün mərhələləri keçmiş run birbaşa qurulur: bu testin mövzusu
        // tapmacanın həlli deyil, TAMAMLAMA yolunun yaddaşı təmizləməsidir.
        var runId = await SeedFinishedRunAsync(client.ChildId, now);

        var complete = await client.Http.PostAsync($"/api/pet-brain/runs/{runId}/complete", null);
        complete.EnsureSuccessStatusCode();

        await using var check = _factory.Services.CreateAsyncScope();
        var verify = check.ServiceProvider.GetRequiredService<AppDbContext>();

        var count = await verify.PetMemories.CountAsync(m => m.ChildProfileId == client.ChildId);

        Assert.True(
            count <= MemoryPolicy.RetentionLimit,
            $"Yaddaş təmizlənmədi — {count} sətir qaldı.");

        // Ən vacib xatirələr QALIR: təmizlik yalnız quyruğu kəsir.
        Assert.Contains(
            await verify.PetMemories.Where(m => m.ChildProfileId == client.ChildId).ToListAsync(),
            m => m.Kind == PetBrainMemoryKind.FirstAdventure);
    }

    private async Task<Guid> SeedFinishedRunAsync(Guid childId, DateTime now)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var template = ExperienceCatalog.Find(ExperienceCatalog.ForestFriendsParade)!;

        var run = new ExperienceRun
        {
            ChildProfileId = childId,
            TemplateKey = template.Key,
            DefinitionVersion = template.Version,
            ExperienceType = template.Type,
            Theme = template.Theme,
            Difficulty = PetBrainDifficulty.Easy,
            Status = PetBrainRunStatus.Active,
            CurrentStage = template.StageCount,
            Choices = [.. template.Stages.Select(s => s.Options.FirstOrDefault()?.Key ?? "continue")],
            StartedAt = now
        };

        db.ExperienceRuns.Add(run);
        await db.SaveChangesAsync();

        return run.Id;
    }
}
