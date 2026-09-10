using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// <b>Ay Kristalı V2</b> — budaqlanan arxitekturanın uçdan-uca sübutu.
///
/// <para>Buradakı testlərin hər biri məhz bir vədi yoxlayır: seçim REAL fərq
/// yaradır, tapmaca hekayəyə aiddir, üç fərqli sonluq var, xülasə həqiqi
/// qeydlərdən qurulur və yaddaş növbəti sessiyaya keçir.</para>
/// </summary>
public class PetBrainMoonV2Tests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainMoonV2Tests(TestWebAppFactory factory) => _factory = factory;

    // ==================== Budaqlar ====================

    /// <summary>
    /// Üç başlanğıc seçimi ÜÇ FƏRQLİ nəticə ekranına aparır: fərqli pet
    /// replikası, fərqli səhnə variantı, fərqli hekayə bayrağı.
    /// </summary>
    [Fact]
    public async Task UcKrater_UcFerqliNeticeEkraniVerir()
    {
        List<(string Node, string Scene, string PetLine)> results = [];

        foreach (var crater in new[] { "north-crater", "deep-crater", "bright-crater" })
        {
            var client = await MoonChildAsync($"moon-branch-{crater}@petpal.test");
            var run = await StartMoonAsync(client);

            run = await PetBrainPlaythrough.StepAsync(client, run);
            run = await PetBrainPlaythrough.StepAsync(client, run, crater);

            Assert.Equal(PetBrainStageKind.Consequence, run.Stage!.Kind);

            results.Add((run.Stage.NodeId, run.Stage.SceneVariant, run.Stage.PetLine));
        }

        Assert.Equal(3, results.Select(r => r.Node).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, results.Select(r => r.Scene).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, results.Select(r => r.PetLine).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Seçim hekayə BAYRAĞI qoyur və o, bazada saxlanılır.</summary>
    [Fact]
    public async Task Secim_HekayeBayragiQoyur()
    {
        var client = await MoonChildAsync("moon-flag@petpal.test");
        var run = await StartMoonAsync(client);

        run = await PetBrainPlaythrough.StepAsync(client, run);
        await PetBrainPlaythrough.StepAsync(client, run, "deep-crater");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.ExperienceRuns.AsNoTracking().FirstAsync(r => r.Id == run.RunId);

        Assert.Contains(MoonCrystalHunt.DeepFlag, row.StoryFlags);
        Assert.DoesNotContain(MoonCrystalHunt.NorthFlag, row.StoryFlags);
    }

    // ==================== Tapmaca ====================

    /// <summary>
    /// Ay macərasının tapmacası AYIN hekayəsindəndir — Mars/Robo mətni yoxdur.
    /// </summary>
    [Fact]
    public async Task AyTapmacasi_MarsMetniDasimir()
    {
        var client = await MoonChildAsync("moon-puzzle@petpal.test");
        var run = await StartMoonAsync(client);

        run = await PetBrainPlaythrough.AdvanceToPuzzleAsync(client, run);

        var puzzle = run.Stage!.Puzzle!;
        var text = string.Join(' ',
            puzzle.Title, puzzle.StoryPrompt, puzzle.Instruction, puzzle.Scene.AltText,
            string.Join(' ', puzzle.Nodes.Select(n => n.Label)));

        Assert.Equal(PuzzleBlueprintCatalog.MoonCrystalRouteKey, puzzle.BlueprintKey);
        Assert.Contains("kristal", text, StringComparison.OrdinalIgnoreCase);

        foreach (var banned in new[] { "Robo", "Mars", "antena" })
            Assert.DoesNotContain(banned, text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Tapmaca həll olunanda hekayə davam edir və nəticə ekranı gəlir.</summary>
    [Fact]
    public async Task TapmacaHellOlunanda_HekayeDavamEdir()
    {
        var client = await MoonChildAsync("moon-solve@petpal.test");
        var run = await StartMoonAsync(client);

        run = await PetBrainPlaythrough.AdvanceToPuzzleAsync(client, run);
        run = await PetBrainPlaythrough.StepAsync(client, run);

        Assert.Equal(PetBrainStageKind.Consequence, run.Stage!.Kind);
        Assert.StartsWith("crystal-awake", run.Stage.NodeId, StringComparison.Ordinal);
    }

    // ==================== Sonluqlar ====================

    /// <summary>
    /// Üç daşıma seçimi ÜÇ FƏRQLİ sonluğa aparır — hər biri çatılandır.
    /// </summary>
    [Theory]
    [InlineData("summit-route", MoonCrystalHunt.ExplorerEnding)]
    [InlineData("signal-relay", MoonCrystalHunt.ScientistEnding)]
    [InlineData("lantern-cradle", MoonCrystalHunt.CaringEnding)]
    public async Task DasimaSecimi_OzSonluğunaAparir(string carry, string expectedEnding)
    {
        var client = await MoonChildAsync($"moon-ending-{carry}@petpal.test");
        var run = await StartMoonAsync(client);

        run = await PetBrainPlaythrough.ContinueToEndAsync(client, run, "north-crater", carry);

        Assert.Equal(PetBrainStageKind.Ending, run.Stage!.Kind);
        Assert.Equal(expectedEnding, run.EndingKey);

        var completed = await PetBrainPlaythrough.CompleteAsync(client, run.RunId);

        Assert.Equal(PetBrainRunStatus.Completed, completed.Status);
        Assert.Equal(expectedEnding, completed.EndingKey);
    }

    /// <summary>Mükafat DƏQİQ BİR DƏFƏ verilir — sonluqda da.</summary>
    [Fact]
    public async Task Mukafat_YalnizBirDefeVerilir()
    {
        var client = await MoonChildAsync("moon-reward-once@petpal.test");
        var run = await StartMoonAsync(client);

        run = await PetBrainPlaythrough.ContinueToEndAsync(client, run, "north-crater", "summit-route");

        var first = await PetBrainPlaythrough.CompleteAsync(client, run.RunId);
        var second = await PetBrainPlaythrough.CompleteAsync(client, run.RunId);

        Assert.True(first.Summary!.XpEarned > 0);
        Assert.Equal(0, second.Summary!.XpEarned);
        Assert.Equal(0, second.Summary.BondEarned);
    }

    // ==================== Bərpa və idempotentlik ====================

    /// <summary>Yenilənmədən sonra uşaq EYNİ düyündən davam edir.</summary>
    [Fact]
    public async Task Yenilenme_EyniDuyundenDavamEdir()
    {
        var client = await MoonChildAsync("moon-resume@petpal.test");
        var run = await StartMoonAsync(client);

        run = await PetBrainPlaythrough.StepAsync(client, run);
        run = await PetBrainPlaythrough.StepAsync(client, run, "bright-crater");

        var reloaded = (await client.Http.GetFromJsonAsync<PetBrainRunDto>(
            $"/api/pet-brain/runs/{run.RunId}"))!;

        Assert.Equal(run.Stage!.NodeId, reloaded.Stage!.NodeId);
        Assert.Equal(run.Stage.SceneVariant, reloaded.Stage.SceneVariant);
        Assert.Equal(run.StepsTaken, reloaded.StepsTaken);
    }

    /// <summary>
    /// Eyni addım İKİ DƏFƏ tətbiq olunmur: köhnə düyün açarı ilə gələn sorğu
    /// <c>409</c> alır və nəticə sətri təkrarlanmır.
    /// </summary>
    [Fact]
    public async Task TekrarGonderilenAddim_IkinciDefeTetbiqOlunmur()
    {
        var client = await MoonChildAsync("moon-idempotent@petpal.test");
        var run = await StartMoonAsync(client);

        var stage = run.Stage!;

        var first = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest { StageIndex = stage.Index, NodeId = stage.NodeId });

        first.EnsureSuccessStatusCode();

        var replay = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest { StageIndex = stage.Index, NodeId = stage.NodeId });

        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(1, await db.RunStageOutcomes
            .CountAsync(o => o.ExperienceRunId == run.RunId && o.NodeId == stage.NodeId));
    }

    // ==================== Xülasə və yaddaş ====================

    /// <summary>
    /// Xülasə REAL nəticə sətirlərindən qurulur: altyazılar uşağın həqiqətən
    /// seçdiyi krateri və sonluğu deyir.
    /// </summary>
    [Fact]
    public async Task Xulase_RealSecimlerdenQurulur()
    {
        var client = await MoonChildAsync("moon-recap@petpal.test");
        var run = await StartMoonAsync(client);

        run = await PetBrainPlaythrough.ContinueToEndAsync(client, run, "deep-crater", "signal-relay");

        var summary = (await PetBrainPlaythrough.CompleteAsync(client, run.RunId)).Summary!;
        var captions = string.Join(" | ", summary.Recap.Shots.Select(s => s.Caption));

        Assert.Equal(3, summary.Recap.Shots.Count);
        Assert.Contains("dərin krater", captions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("siqnal", captions, StringComparison.OrdinalIgnoreCase);

        // Seçilməyən yol HEÇ VAXT göstərilmir.
        Assert.DoesNotContain("şimal krateri", captions, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("zirvə", captions, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Yol göstəricisi uşağın ATDIĞI addımları göstərir — indeks təxmini yox.</summary>
    [Fact]
    public async Task YolGostericisi_RealAddimlariGosterir()
    {
        var client = await MoonChildAsync("moon-path@petpal.test");
        var run = await StartMoonAsync(client);

        run = await PetBrainPlaythrough.ContinueToEndAsync(client, run, "bright-crater", "lantern-cradle");

        Assert.NotEmpty(run.Path);
        Assert.Contains(run.Path, p => p.WasChoice && p.Label.Contains("Parlaq", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(run.Path, p => p.WasChoice && p.Label.Contains("Fənər", StringComparison.OrdinalIgnoreCase));

        // Sıra 1-dən başlayır və kəsilmir.
        Assert.Equal(
            Enumerable.Range(1, run.Path.Count).ToList(),
            run.Path.Select(p => p.Ordinal).ToList());

        // Rəng TƏK daşıyıcı deyil: hər addımın işarəsi və etiketi var.
        Assert.All(run.Path, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Icon));
            Assert.False(string.IsNullOrWhiteSpace(p.Label));
        });
    }

    /// <summary>
    /// Tamamlanmış macəra STRUKTURLU episodik yaddaş yaradır və o, növbəti
    /// sessiyada pet-in davranışına düşür.
    /// </summary>
    [Fact]
    public async Task Tamamlama_YaddasYaradirVeSonrakiSessiyada_Isledilir()
    {
        var client = await MoonChildAsync("moon-memory@petpal.test");
        var run = await StartMoonAsync(client);

        run = await PetBrainPlaythrough.ContinueToEndAsync(client, run, "north-crater", "summit-route");
        await PetBrainPlaythrough.CompleteAsync(client, run.RunId);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var memories = await db.PetMemories
                .AsNoTracking()
                .Where(m => m.ChildProfileId == client.ChildId)
                .ToListAsync();

            // Sonluq strukturlu FAKT kimi yazılır — sərbəst mətn deyil.
            Assert.Contains(memories, m =>
                m.Kind == PetBrainMemoryKind.ChoiceMade
                && m.ValueKey == MoonCrystalHunt.ExplorerEnding);
        }

        // Növbəti sessiya: pet xatirəni GÖRÜR.
        var state = (await client.Http.GetFromJsonAsync<PetBrainStateDto>("/api/pet-brain"))!;

        Assert.NotEmpty(state.Memories);
    }

    /// <summary>
    /// Yaddaş çağırışı ekranda görünür: pet əvvəlki seçimi xatırlayır.
    /// </summary>
    [Fact]
    public async Task YaddasCagirisi_IkinciMaceradaGorunur()
    {
        var client = await MoonChildAsync("moon-callback@petpal.test");

        var first = await StartMoonAsync(client);
        first = await PetBrainPlaythrough.ContinueToEndAsync(client, first, "deep-crater", "lantern-cradle");
        await PetBrainPlaythrough.CompleteAsync(client, first.RunId);

        // İkinci Ay macərası: daşıma ekranında pet keçmişi xatırlayır.
        var second = await StartMoonAsync(client);
        second = await PetBrainPlaythrough.AdvanceToNodeAsync(client, second, "carry-choice");

        Assert.Equal("carry-choice", second.Stage!.NodeId);
        Assert.False(string.IsNullOrWhiteSpace(second.Stage.MemoryCallback));
    }

    // ==================== Köməkçilər ====================

    /// <summary>
    /// Ay macərasını istəyən profil — kosmos/həlledici.
    ///
    /// <para>Ay ilk dəfə tövsiyə olunmaya bilər (Mars da kosmos mövzusundadır),
    /// ona görə testlər run-ı BİRBAŞA Ay şablonu ilə başladır: yoxlanılan şey
    /// direktorun seçimi deyil, hekayənin özüdür.</para>
    /// </summary>
    private async Task<ApiTestClient> MoonChildAsync(string email)
    {
        var client = await ApiTestClient.CreateAsync(_factory, email, "Aylin");
        await client.HatchAsync(_factory);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = _factory.Clock.GetUtcNow().UtcDateTime;

        foreach (var (category, key, score) in new (PetBrainTraitCategory, string, int)[]
                 {
                     (PetBrainTraitCategory.Interest, TraitKeys.Space, 95),
                     (PetBrainTraitCategory.Interest, TraitKeys.Science, 88),
                     (PetBrainTraitCategory.Interest, TraitKeys.Puzzles, 90),
                     (PetBrainTraitCategory.PlayStyle, TraitKeys.ProblemSolver, 92),
                     (PetBrainTraitCategory.PlayStyle, TraitKeys.Explorer, 84)
                 })
            db.PlayerTraits.Add(new PlayerTrait
            {
                ChildProfileId = client.ChildId,
                Category = category,
                Key = key,
                Score = score,
                UpdatedAt = now
            });

        await db.SaveChangesAsync();

        return client;
    }

    /// <summary>
    /// Ay macərasını başladır.
    ///
    /// <para>Direktor başqa şablon təklif edərsə, uşaq Ayı seçə bilmir — bu,
    /// qəsdən belədir. Ona görə test Ay tövsiyə olunana qədər tövsiyəni
    /// «başqa fikir» ilə dəyişir; limit dolarsa run birbaşa qurulur.</para>
    /// </summary>
    private async Task<PetBrainRunDto> StartMoonAsync(ApiTestClient client)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var state = (await client.Http.GetFromJsonAsync<PetBrainStateDto>("/api/pet-brain"))!;
            var recommendation = state.Recommendation;

            if (recommendation is null)
                break;

            if (recommendation.TemplateKey == ExperienceCatalog.MoonCrystalRescue)
            {
                var started = await client.Http.PostAsJsonAsync("/api/pet-brain/runs",
                    new StartPetBrainRunRequest { DecisionId = recommendation.DecisionId });

                started.EnsureSuccessStatusCode();

                return (await started.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
            }

            if (!recommendation.CanShowAnother)
                break;

            var another = await client.Http.PostAsJsonAsync("/api/pet-brain/recommendation/feedback",
                new PetBrainFeedbackRequest
                {
                    DecisionId = recommendation.DecisionId,
                    Feedback = PetBrainRecommendationFeedback.ShowAnother
                });

            another.EnsureSuccessStatusCode();
        }

        return await SeedMoonRunAsync(client);
    }

    /// <summary>Ay run-unu birbaşa qurur — direktorun seçimi bu testlərin mövzusu deyil.</summary>
    private async Task<PetBrainRunDto> SeedMoonRunAsync(ApiTestClient client)
    {
        Guid runId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var template = ExperienceCatalog.Find(ExperienceCatalog.MoonCrystalRescue)!;
            var graph = MoonCrystalHunt.Definition;

            var stale = await db.ExperienceRuns
                .Where(r => r.ChildProfileId == client.ChildId && r.Status == PetBrainRunStatus.Active)
                .ToListAsync();

            foreach (var row in stale)
                row.Status = PetBrainRunStatus.Abandoned;

            var run = new ExperienceRun
            {
                ChildProfileId = client.ChildId,
                TemplateKey = template.Key,
                DefinitionVersion = graph.Version,
                CurrentNodeId = graph.StartNodeId,
                ExperienceType = template.Type,
                Theme = template.Theme,
                Difficulty = PetBrainDifficulty.Medium,
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

