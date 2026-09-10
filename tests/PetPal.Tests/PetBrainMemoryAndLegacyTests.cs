using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Yaddaşın AXTARIŞ balı — pet özünü təkrarlamamalı və ana uyğun danışmalıdır.
/// </summary>
public class MemoryRetrievalTests
{
    private static readonly DateTime Now = new(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);

    private static PetMemory Memory(
        PetBrainMemoryKind kind = PetBrainMemoryKind.ExperienceCompleted,
        string factKey = "moon-crystal-rescue",
        int importance = 70,
        int ageDays = 1,
        DateTime? lastUsed = null,
        params string[] tags) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        FactKey = factKey,
        Importance = importance,
        CreatedAt = Now.AddDays(-ageDays),
        LastUsedAt = lastUsed,
        Tags = [.. tags]
    };

    /// <summary>Cari MÖVZUYA aid xatirə önə keçir.</summary>
    [Fact]
    public void MovzuyaUygunXatire_OneKecir()
    {
        var space = Memory(tags: TraitKeys.Space);
        var ocean = Memory(tags: TraitKeys.Ocean);

        var picked = MemoryPolicy.Retrieve([ocean, space], Now, TraitKeys.Space, limit: 1);

        Assert.Same(space, picked[0]);
    }

    /// <summary>Seçim anında SEÇİM xatirəsi önə keçir.</summary>
    [Fact]
    public void SecimAninda_SecimXatiresiOneKecir()
    {
        var choice = Memory(PetBrainMemoryKind.ChoiceMade, importance: 55);
        var completed = Memory(PetBrainMemoryKind.ExperienceCompleted, importance: 70);

        var picked = MemoryPolicy.Retrieve([completed, choice], Now, intent: "choice", limit: 1);

        Assert.Same(choice, picked[0]);
    }

    /// <summary>
    /// YAXINLARDA işlədilmiş xatirə arxaya keçir — pet eyni cümləni dalbadal
    /// deməməlidir.
    /// </summary>
    [Fact]
    public void YaxinlardaIsledilmis_ArxayaKecir()
    {
        var used = Memory(importance: 90, lastUsed: Now.AddHours(-1));
        var fresh = Memory(importance: 60);

        var picked = MemoryPolicy.Retrieve([used, fresh], Now, limit: 1);

        Assert.Same(fresh, picked[0]);
    }

    /// <summary>Köhnə hadisə yeni hadisədən geri qalır — bərabər vaciblikdə.</summary>
    [Fact]
    public void KohneHadise_YenidenGeriQalir()
    {
        var old = Memory(ageDays: 200);
        var recent = Memory(ageDays: 1);

        var picked = MemoryPolicy.Retrieve([old, recent], Now, limit: 1);

        Assert.Same(recent, picked[0]);
    }

    /// <summary>Vaxtı keçmiş xatirə heç vaxt seçilmir.</summary>
    [Fact]
    public void VaxtiKecmis_Secilmir()
    {
        var expired = Memory();
        expired.ExpiresAt = Now.AddDays(-1);

        Assert.Empty(MemoryPolicy.Retrieve([expired], Now));
    }

    /// <summary>
    /// MÜXTƏLİFLİK qorunur: eyni növdən üç cümlə bir ekranda pet-i lentə
    /// çevirir.
    /// </summary>
    [Fact]
    public void Muxteliflik_EyniNovuTekrarlamir()
    {
        var memories = new[]
        {
            Memory(PetBrainMemoryKind.ExperienceCompleted, importance: 90),
            Memory(PetBrainMemoryKind.ExperienceCompleted, importance: 88),
            Memory(PetBrainMemoryKind.ChoiceMade, importance: 50),
            Memory(PetBrainMemoryKind.CosmeticUnlocked, importance: 40)
        };

        var picked = MemoryPolicy.Retrieve(memories, Now, limit: 3);

        Assert.Equal(3, picked.Count);
        Assert.Equal(3, picked.Select(m => m.Kind).Distinct().Count());
    }

    /// <summary>SEMANTİK nəticə təmizlənərkən qorunur — naxış epizoddan bahalıdır.</summary>
    [Fact]
    public void SemantikNetice_TemizlenmedeQorunur()
    {
        List<PetMemory> memories =
        [
            .. Enumerable.Range(0, MemoryPolicy.RetentionLimit + 10)
                .Select(i => Memory(importance: 60, ageDays: i + 1))
        ];

        var pattern = Memory(PetBrainMemoryKind.PatternLearned, SemanticMemory.PrefersExploring, 75, 300);
        pattern.Tier = PetBrainMemoryTier.Semantic;
        memories.Add(pattern);

        Assert.DoesNotContain(pattern, MemoryPolicy.Prune(memories));
    }
}

/// <summary>
/// Semantik yaddaş — bir epizoddan NAXIŞ çıxarılmır.
/// </summary>
public class SemanticMemoryTests
{
    /// <summary>Tək müşahidə naxış YARATMIR — «bir dəfə seçdi, deməli sevir» səhvi.</summary>
    [Fact]
    public void TekMusahide_NaxisYaratmir() =>
        Assert.Empty(SemanticMemory.FromChoices([TraitKeys.Explorer]));

    /// <summary>Təkrarlanan seçim naxış yaradır.</summary>
    [Fact]
    public void TekrarlananSecim_NaxisYaradir()
    {
        var patterns = SemanticMemory.FromChoices([TraitKeys.Explorer, TraitKeys.Explorer]);

        Assert.Single(patterns);
        Assert.Equal(SemanticMemory.PrefersExploring, patterns[0].FactKey);
        Assert.Equal(2, patterns[0].Support);
    }

    /// <summary>Naməlum açar naxış yaratmır — uydurma nəticə çıxarılmır.</summary>
    [Fact]
    public void NamelumAcar_NaxisYaratmir() =>
        Assert.Empty(SemanticMemory.FromChoices(["uydurma", "uydurma", "uydurma"]));

    /// <summary>
    /// Dəstək naxışı MÜSBƏT çərçivədədir — «çox ipucu istəyir» kimi oxunmur.
    /// </summary>
    [Fact]
    public void DestekNaxisi_MusbetCerciveededir()
    {
        Assert.Null(SemanticMemory.FromSupport(1));

        var support = SemanticMemory.FromSupport(3);

        Assert.NotNull(support);
        Assert.Equal(SemanticMemory.SupportHelps, support!.FactKey);

        foreach (var language in new[] { "az", "en" })
        {
            var line = SemanticMemory.Render(SemanticMemory.SupportHelps, language, "Luna");

            foreach (var banned in new[] { "çox ipucu", "bacarmır", "zəif", "too many hints", "weak", "cannot" })
                Assert.DoesNotContain(banned, line, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Hər naxış HƏR İKİ dildə cümlə verir.</summary>
    [Fact]
    public void HerNaxis_HerIkiDildeCumleVerir()
    {
        foreach (var key in new[]
                 {
                     SemanticMemory.PrefersExploring, SemanticMemory.PrefersCreating,
                     SemanticMemory.PrefersSolving, SemanticMemory.PrefersHelping,
                     SemanticMemory.SupportHelps
                 })
        {
            var az = SemanticMemory.Render(key, "az", "Luna");
            var en = SemanticMemory.Render(key, "en", "Luna");

            Assert.True(SemanticMemory.IsKnown(key));
            Assert.False(string.IsNullOrWhiteSpace(az));
            Assert.False(string.IsNullOrWhiteSpace(en));
            Assert.NotEqual(az, en);
        }
    }
}

/// <summary>
/// Valideynin yaddaş üzərində nəzarəti — görmək, unutdurmaq, sıfırlamaq.
/// </summary>
public class PetMemoryAdminTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetMemoryAdminTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>Valideyn uşağın bütün xatirələrini CÜMLƏ şəklində görür.</summary>
    [Fact]
    public async Task Valideyn_XatireleriGorur()
    {
        var client = await SeedMemoriesAsync("memadmin-list@petpal.test", 3);

        client.SwitchToParent();

        var memories = await client.Http.GetFromJsonAsync<List<PetBrainMemoryDto>>(
            $"/api/parent/pet-brain/children/{client.ChildId}/memories");

        Assert.NotNull(memories);
        Assert.Equal(3, memories!.Count);
        Assert.All(memories, m => Assert.False(string.IsNullOrWhiteSpace(m.Text)));
        Assert.All(memories, m => Assert.NotEqual(Guid.Empty, m.Id));
    }

    /// <summary>Valideyn BİR xatirəni unutdura bilir.</summary>
    [Fact]
    public async Task Valideyn_BirXatireniUnutdurur()
    {
        var client = await SeedMemoriesAsync("memadmin-forget@petpal.test", 3);

        client.SwitchToParent();

        var memories = (await client.Http.GetFromJsonAsync<List<PetBrainMemoryDto>>(
            $"/api/parent/pet-brain/children/{client.ChildId}/memories"))!;

        var response = await client.Http.DeleteAsync(
            $"/api/parent/pet-brain/children/{client.ChildId}/memories/{memories[0].Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(2, await db.PetMemories.CountAsync(m => m.ChildProfileId == client.ChildId));
    }

    /// <summary>
    /// Sıfırlama YALNIZ yaddaşa toxunur — xassələr və macəra tarixçəsi qalır.
    /// </summary>
    [Fact]
    public async Task Sifirlama_YalnizYaddasaToxunur()
    {
        var client = await SeedMemoriesAsync("memadmin-reset@petpal.test", 4);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.PlayerTraits.Add(new PlayerTrait
            {
                ChildProfileId = client.ChildId,
                Category = PetBrainTraitCategory.Interest,
                Key = TraitKeys.Space,
                Score = 80,
                UpdatedAt = _factory.Clock.GetUtcNow().UtcDateTime
            });

            await db.SaveChangesAsync();
        }

        client.SwitchToParent();

        var response = await client.Http.DeleteAsync(
            $"/api/parent/pet-brain/children/{client.ChildId}/memories");

        response.EnsureSuccessStatusCode();

        await using var check = _factory.Services.CreateAsyncScope();
        var verify = check.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(0, await verify.PetMemories.CountAsync(m => m.ChildProfileId == client.ChildId));
        Assert.Equal(1, await verify.PlayerTraits.CountAsync(t => t.ChildProfileId == client.ChildId));
    }

    /// <summary>YAD uşağın yaddaşı nə oxunur, nə silinir.</summary>
    [Fact]
    public async Task YadUsaq_YaddasaToxunaBilmir()
    {
        var owner = await SeedMemoriesAsync("memadmin-owner@petpal.test", 2);
        var stranger = await SeedMemoriesAsync("memadmin-stranger@petpal.test", 2);

        stranger.SwitchToParent();

        var read = await stranger.Http.GetAsync(
            $"/api/parent/pet-brain/children/{owner.ChildId}/memories");

        var wipe = await stranger.Http.DeleteAsync(
            $"/api/parent/pet-brain/children/{owner.ChildId}/memories");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wipe.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(2, await db.PetMemories.CountAsync(m => m.ChildProfileId == owner.ChildId));
    }

    /// <summary>Uşaq sessiyası valideyn marşrutuna DÜŞMÜR.</summary>
    [Fact]
    public async Task UsaqTokeni_ValideynMarsrutunaDusmur()
    {
        var client = await SeedMemoriesAsync("memadmin-childtoken@petpal.test", 1);

        var response = await client.Http.GetAsync(
            $"/api/parent/pet-brain/children/{client.ChildId}/memories");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<ApiTestClient> SeedMemoriesAsync(string email, int count)
    {
        var client = await ApiTestClient.CreateAsync(_factory, email, "Aylin");
        await client.HatchAsync(_factory);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = _factory.Clock.GetUtcNow().UtcDateTime;

        for (var i = 0; i < count; i++)
            db.PetMemories.Add(new PetMemory
            {
                ChildProfileId = client.ChildId,
                Kind = PetBrainMemoryKind.ExperienceCompleted,
                FactKey = ExperienceCatalog.MoonCrystalRescue,
                ValueKey = $"v{i}",
                Importance = 60 + i,
                CreatedAt = now.AddMinutes(-i)
            });

        await db.SaveChangesAsync();

        return client;
    }
}

/// <summary>
/// LEGACY macəralar toxunulmaz qalır.
///
/// <para>Qraf yalnız bir şablona qoşulub; qalan beşi hələ də xətti şablonla
/// oynanır və V2-nin heç bir hissəsi onları pozmamalıdır. Bu, miqrasiyanın
/// şərtidir — «yeni model işləyir» kifayət deyil, «köhnəsi də işləyir»
/// lazımdır.</para>
/// </summary>
public class PetBrainLegacyCompatibilityTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainLegacyCompatibilityTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>Kataloqdakı altı şablondan yalnız biri qrafa keçib.</summary>
    [Fact]
    public void KataloqdakiSablonlar_ColuXettiQalir()
    {
        var graph = ExperienceCatalog.Templates.Count(t => StoryCatalog.IsGraph(t.Key));
        var linear = ExperienceCatalog.Templates.Count(t => !StoryCatalog.IsGraph(t.Key));

        Assert.True(graph >= 1, "Ən azı bir şablon qrafda olmalıdır.");
        Assert.True(linear >= 1, "Xətti şablonlar hələ də dəstəklənməlidir.");
    }

    /// <summary>
    /// XƏTTİ macəra sona qədər oynanır, tamamlanır və mükafat verilir —
    /// qraf açıq olsa da.
    /// </summary>
    [Theory]
    [InlineData(ExperienceCatalog.MarsRoverRescue)]
    [InlineData(ExperienceCatalog.DragonLostColors)]
    [InlineData(ExperienceCatalog.OceanGlowQuest)]
    [InlineData(ExperienceCatalog.ForestFriendsParade)]
    [InlineData(ExperienceCatalog.RobotLabPuzzle)]
    public async Task XettiMacera_SonaQederOynanir(string templateKey)
    {
        Assert.False(StoryCatalog.IsGraph(templateKey), $"{templateKey} artıq qrafdadır — test köhnəlib.");

        var client = await ApiTestClient.CreateAsync(_factory, $"legacy-{templateKey}@petpal.test", "Aylin");
        await client.HatchAsync(_factory);

        var run = await SeedLinearRunAsync(client, templateKey);

        // Xətti run-un DÜYÜNÜ yoxdur — model məhz bununla ayırd olunur.
        Assert.Empty(run.Stage!.NodeId);

        run = await PetBrainPlaythrough.ContinueToEndAsync(client, run);

        var completed = await PetBrainPlaythrough.CompleteAsync(client, run.RunId);

        Assert.Equal(PetBrainRunStatus.Completed, completed.Status);
        Assert.NotNull(completed.Summary);
        Assert.True(completed.Summary!.XpEarned > 0);

        // Xətti macərada sonluq açarı YOXDUR — o, qrafın anlayışıdır.
        Assert.Empty(completed.EndingKey);
    }

    /// <summary>
    /// Qraf açarı BAĞLANANDA artıq başlamış qraf run-u öz modeli ilə davam
    /// edir — açıq macəra ekranda boş qalmır.
    /// </summary>
    [Fact]
    public async Task BaslamisQrafRun_AcarBaglanandaDaOxunur()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "legacy-graph-run@petpal.test", "Mia");
        await client.HatchAsync(_factory);

        Guid runId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var graph = MoonCrystalHunt.Definition;

            var run = new ExperienceRun
            {
                ChildProfileId = client.ChildId,
                TemplateKey = ExperienceCatalog.MoonCrystalRescue,
                DefinitionVersion = graph.Version,
                CurrentNodeId = graph.StartNodeId,
                ExperienceType = PetBrainExperienceType.Adventure,
                Theme = TraitKeys.Space,
                Difficulty = PetBrainDifficulty.Medium,
                Status = PetBrainRunStatus.Active,
                StartedAt = _factory.Clock.GetUtcNow().UtcDateTime
            };

            db.ExperienceRuns.Add(run);
            await db.SaveChangesAsync();

            runId = run.Id;
        }

        // GraphOf run-a baxır, açara YOX: başladığı model ilə bitirilir.
        var reloaded = (await client.Http.GetFromJsonAsync<PetBrainRunDto>(
            $"/api/pet-brain/runs/{runId}"))!;

        Assert.NotNull(reloaded.Stage);
        Assert.Equal("intro", reloaded.Stage!.NodeId);
    }

    private async Task<PetBrainRunDto> SeedLinearRunAsync(ApiTestClient client, string templateKey)
    {
        Guid runId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var template = ExperienceCatalog.Find(templateKey)!;

            var run = new ExperienceRun
            {
                ChildProfileId = client.ChildId,
                TemplateKey = template.Key,
                DefinitionVersion = template.Version,
                ExperienceType = template.Type,
                Theme = template.Theme,
                Difficulty = PetBrainDifficulty.Easy,
                Status = PetBrainRunStatus.Active,
                StartedAt = _factory.Clock.GetUtcNow().UtcDateTime
            };

            db.ExperienceRuns.Add(run);
            await db.SaveChangesAsync();

            runId = run.Id;
        }

        return (await client.Http.GetFromJsonAsync<PetBrainRunDto>($"/api/pet-brain/runs/{runId}"))!;
    }
}
