using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// <b>Adventure Engine V2</b> — uçdan-uca zəmanətlər.
///
/// <para>Hər test bir VƏDİ yoxlayır: macəra bir neçə sessiyada oynanır, seçim
/// sonrakı fəslə təsir edir, inventar və jurnal həqiqətən işlənir, hər fəsil
/// checkpoint verir və üç sonluğun hamısı çatılandır.</para>
/// </summary>
public class PetBrainAdventureV2Tests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainAdventureV2Tests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>Macəra ən azı beş fəsil və iyirmi beş düyün daşıyır.</summary>
    [Fact]
    public void Maceranin_Olcusu_TelebeUygundur()
    {
        var graph = MoonCrystalSecret.Definition;

        Assert.True(graph.Chapters.Count >= 5, $"Fəsil azdır: {graph.Chapters.Count}.");
        Assert.True(graph.Nodes.Count >= 25, $"Düyün azdır: {graph.Nodes.Count}.");
        Assert.True(graph.EstimatedTotalMinutes >= 30, $"Məzmun qısadır: {graph.EstimatedTotalMinutes} dəq.");
        Assert.True(graph.Endings.Count >= 3, $"Sonluq azdır: {graph.Endings.Count}.");

        Assert.True(
            graph.Objectives.Count(o => o.IsOptional) >= 2,
            "Ən azı iki yan tapşırıq olmalıdır.");

        Assert.All(graph.Chapters, chapter =>
            Assert.InRange(chapter.EstimatedMinutes, 5, 12));
    }

    /// <summary>HƏR fəsil öz checkpoint-inə malikdir — uşaq istənilən fəsildə dayana bilər.</summary>
    [Fact]
    public void HerFesil_CheckpointDasiyir()
    {
        var graph = MoonCrystalSecret.Definition;

        Assert.All(graph.Chapters, chapter =>
            Assert.Contains(graph.NodesOf(chapter.ChapterId), n => n.IsCheckpoint));
    }

    /// <summary>Tapmacalar ən azı dörd FƏRQLİ mexanika işlədir.</summary>
    [Fact]
    public void Tapmacalar_DordFerqliMexanikaIsledir()
    {
        var graph = MoonCrystalSecret.Definition;

        var families = graph.Nodes
            .Where(n => n.Kind == PetBrainStageKind.Puzzle)
            .Select(n => n.PuzzleFamily)
            .Where(f => !string.IsNullOrEmpty(f))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(families.Count >= 6, $"Tapmaca ailəsi azdır: {families.Count}.");

        var mechanics = families
            .Select(f => Api.PetBrain.Puzzles.PuzzleBlueprintCatalog.Find(f)!.Mechanic)
            .Distinct()
            .ToList();

        Assert.True(mechanics.Count >= 4, $"Mexanika azdır: {mechanics.Count}.");
    }

    /// <summary>Macəra başdan sona oynanır və sonluğa çatır.</summary>
    [Fact]
    public async Task Macera_SonaQederOynanilir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-full@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        run = await MoonSecretAdventure.PlayAsync(client, run);

        Assert.Equal(PetBrainStageKind.Ending, run.Stage!.Kind);
        Assert.False(string.IsNullOrEmpty(run.EndingKey));

        var finished = await PetBrainPlaythrough.CompleteAsync(client, run.RunId);

        Assert.Equal(PetBrainRunStatus.Completed, finished.Status);
        Assert.NotNull(finished.Summary);
    }

    /// <summary>
    /// ÜÇ sonluğun hamısı çatılandır — hər biri fərqli seçim dəsti ilə.
    /// </summary>
    [Theory]
    [InlineData("power-repair", "plan-shield", MoonKeys.EndingGuardian)]
    [InlineData("power-map", "plan-join", MoonKeys.EndingExplorer)]
    [InlineData("power-comms", "plan-share", MoonKeys.EndingRobotFriend)]
    public async Task ButunSonluqlar_Catilir(string power, string plan, string expected)
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, $"v2-end-{expected}@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        run = await MoonSecretAdventure.PlayAsync(client, run, power, plan, "rover-team", "rover-memory");

        Assert.Equal(expected, run.EndingKey);
    }

    /// <summary>
    /// İki fərqli yol REAL fərqli səhnələr göstərir — eyni hekayənin rəngi
    /// dəyişmir, məzmunu dəyişir.
    /// </summary>
    [Fact]
    public async Task IkiYol_FerqliSehneleriGosterir()
    {
        var cave = await NodesOnRouteAsync("v2-cave@petpal.test", "route-cave");
        var crater = await NodesOnRouteAsync("v2-crater@petpal.test", "route-crater");

        Assert.NotEqual(cave, crater);

        Assert.Contains(cave, n => n.StartsWith("c3-cave", StringComparison.Ordinal));
        Assert.Contains(crater, n => n.StartsWith("c3-crater", StringComparison.Ordinal));
        Assert.DoesNotContain(cave, n => n.StartsWith("c3-crater", StringComparison.Ordinal));
        Assert.DoesNotContain(crater, n => n.StartsWith("c3-cave", StringComparison.Ordinal));
    }

    /// <summary>
    /// İkinci fəsildə rabitəni işə salan uşaq DÖRDÜNCÜ fəsildə əlavə variant
    /// görür. Bu, «seçim sonrakı fəslə təsir edir» vədinin sübutudur.
    /// </summary>
    [Fact]
    public async Task RabiteSecimi_DorduncuFesildeYeniVariantAcir()
    {
        var withComms = await RoverOptionsAsync("v2-comms@petpal.test", "power-comms");
        var withMap = await RoverOptionsAsync("v2-nocomms@petpal.test", "power-map");

        Assert.Contains("rover-team", withComms);
        Assert.DoesNotContain("rover-team", withMap);
    }

    /// <summary>
    /// Xəritəni işə salan uşaq üçüncü fəsildə GİZLİ yol görür — inventar
    /// route açır.
    /// </summary>
    [Fact]
    public async Task Xerite_UcuncuFesildeGizliYolAcir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-hidden@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var fork = await MoonSecretAdventure.AdvanceToAsync(client, run, "c3-fork", "power-map");

        Assert.NotNull(fork);
        Assert.Contains(fork!.Stage!.Options, o => o.Key == "route-hidden");

        var other = await MoonSecretAdventure.ChildAsync(_factory, "v2-nohidden@petpal.test");
        var otherRun = await MoonSecretAdventure.StartAsync(_factory, other);
        var otherFork = await MoonSecretAdventure.AdvanceToAsync(client: other, otherRun, "c3-fork", "power-comms");

        Assert.NotNull(otherFork);
        Assert.DoesNotContain(otherFork!.Stage!.Options, o => o.Key == "route-hidden");
    }

    /// <summary>Çantaya yığılan alətlər inventarda GÖRÜNÜR və sayı ikidir.</summary>
    [Fact]
    public async Task Canta_IkiAletDasiyir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-kit@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var afterKit = await MoonSecretAdventure.AdvanceToAsync(client, run, "c2-arrival");

        Assert.NotNull(afterKit);
        Assert.NotNull(afterKit!.Adventure);

        var tools = afterKit.Adventure!.Inventory
            .Where(i => i.ItemId.StartsWith("tool-", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, tools.Count);
    }

    /// <summary>Enerji xanası SƏRF olunur — seçim resursu həqiqətən xərcləyir.</summary>
    [Fact]
    public async Task EnerjiXanasi_SecimdeSerfOlunur()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-cell@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var atChoice = await MoonSecretAdventure.AdvanceToAsync(client, run, "c2-power");

        Assert.NotNull(atChoice);
        Assert.Contains(atChoice!.Adventure!.Inventory, i => i.ItemId == MoonKeys.ItemPowerCell);

        var after = await PetBrainPlaythrough.StepAsync(client, atChoice, "power-map");

        Assert.DoesNotContain(after.Adventure!.Inventory, i => i.ItemId == MoonKeys.ItemPowerCell);
        Assert.Contains(after.Adventure.Inventory, i => i.ItemId == MoonKeys.ItemMoonMap);
    }

    /// <summary>Jurnal doldurulur və ipuçları macəra boyu qalır.</summary>
    [Fact]
    public async Task Jurnal_IpuclariniSaxlayir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-clues@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var later = await MoonSecretAdventure.AdvanceToAsync(client, run, "c3-fork");

        Assert.NotNull(later);
        Assert.Contains(later!.Adventure!.Clues, c => c.ClueId == MoonKeys.ClueSignalRhythm);
        Assert.True(later.Adventure.Clues.Count >= 2);
    }

    /// <summary>İzləyicidə eyni anda ən çox ÜÇ məqsəd görünür.</summary>
    [Fact]
    public async Task Izleyici_UcdenCoxMeqsedGostermir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-obj@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var guard = 0;

        while (!PetBrainPlaythrough.IsFinished(run) && guard++ < 90)
        {
            Assert.True(run.Adventure!.Objectives.Count <= 3,
                $"«{run.Stage!.NodeId}» ekranında {run.Adventure.Objectives.Count} məqsəd var.");

            run = await PetBrainPlaythrough.StepAsync(client, run);
        }
    }

    /// <summary>Yan tapşırıq BURAXILA bilər — macəra yenə sonluğa çatır.</summary>
    [Fact]
    public async Task YanTapsiriq_BuraxilaBiler()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-skip@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        run = await MoonSecretAdventure.PlayAsync(client, run, "bot-later", "side-skip");

        Assert.Equal(PetBrainStageKind.Ending, run.Stage!.Kind);
    }

    /// <summary>Fəsil bitəndə YEKUN ekranı gəlir və checkpoint yazılır.</summary>
    [Fact]
    public async Task FesilBitende_YekunVeCheckpointGelir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-chapter@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var recap = await MoonSecretAdventure.AdvanceToAsync(client, run, "c1-recap");

        Assert.NotNull(recap);
        Assert.True(recap!.Adventure!.IsAtCheckpoint);

        Assert.NotNull(recap.ChapterComplete);
        Assert.Equal(MoonKeys.Chapter1, recap.ChapterComplete!.ChapterId);
        Assert.False(string.IsNullOrWhiteSpace(recap.ChapterComplete.Summary));
        Assert.False(string.IsNullOrWhiteSpace(recap.ChapterComplete.NextChapterTitle));
        Assert.NotEmpty(recap.ChapterComplete.CompletedObjectives);
        Assert.True(recap.Adventure.ProgressPercent > 0);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stateRow = await db.AdventureRunStates.FirstAsync(s => s.ExperienceRunId == run.RunId);

        Assert.Equal("c1-recap", stateRow.CheckpointNodeId);
        Assert.Equal(MoonKeys.Chapter1, stateRow.CheckpointChapterId);
    }

    /// <summary>
    /// Dayandır → davam et: vəziyyət İTMİR.
    ///
    /// <para>Bu, macəranın bir neçə sessiyada oynanması vədinin birbaşa
    /// sübutudur.</para>
    /// </summary>
    [Fact]
    public async Task PauzaVeDavam_VeziyyetiItirmir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-pause@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var atFork = await MoonSecretAdventure.AdvanceToAsync(client, run, "c3-fork", "power-map");
        Assert.NotNull(atFork);

        var inventoryBefore = atFork!.Adventure!.Inventory.Select(i => i.ItemId).OrderBy(i => i).ToList();
        var cluesBefore = atFork.Adventure.Clues.Select(c => c.ClueId).OrderBy(c => c).ToList();
        var progressBefore = atFork.Adventure.ProgressPercent;

        var paused = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/pause", new PetBrainPauseRequest { PlayedSeconds = 240 });

        paused.EnsureSuccessStatusCode();

        var card = (await paused.Content.ReadFromJsonAsync<PetBrainResumeDto>())!;

        Assert.Equal(PetBrainRunStatus.Paused, card.Status);
        Assert.False(string.IsNullOrWhiteSpace(card.ChapterTitle));
        Assert.True(card.RemainingMinutes > 0);

        var resumed = await client.Http.PostAsync($"/api/pet-brain/runs/{run.RunId}/resume", null);
        resumed.EnsureSuccessStatusCode();

        var back = (await resumed.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        Assert.Equal(
            inventoryBefore,
            back.Adventure!.Inventory.Select(i => i.ItemId).OrderBy(i => i).ToList());

        Assert.Equal(
            cluesBefore,
            back.Adventure.Clues.Select(c => c.ClueId).OrderBy(c => c).ToList());

        Assert.Equal(progressBefore, back.Adventure.ProgressPercent);

        back = await MoonSecretAdventure.PlayAsync(client, back);
        Assert.Equal(PetBrainStageKind.Ending, back.Stage!.Kind);
    }

    /// <summary>Bərpa kartı harada qaldığımızı və nə etməli olduğumuzu göstərir.</summary>
    [Fact]
    public async Task BerpaKarti_LazimliMelumatiDasiyir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-resume-card@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var later = await MoonSecretAdventure.AdvanceToAsync(client, run, "c2-arrival");
        Assert.NotNull(later);

        var card = await client.Http.GetFromJsonAsync<PetBrainResumeDto>("/api/pet-brain/resume");

        Assert.NotNull(card);
        Assert.Equal(run.RunId, card!.RunId);
        Assert.Equal(6, card.ChapterCount);
        Assert.False(string.IsNullOrWhiteSpace(card.CurrentObjective));
        Assert.NotEmpty(card.KeyItems);
        Assert.False(string.IsNullOrWhiteSpace(card.LastEventSummary));
    }

    /// <summary>Eyni idempotentlik açarı ilə iki sorğu bir dəfə tətbiq olunur.</summary>
    [Fact]
    public async Task EyniIdempotencyAcari_IkinciDefeTetbiqOlunmur()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-idem@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var atKit = await MoonSecretAdventure.AdvanceToAsync(client, run, "c1-kit-second-scanner", "pick-scanner");
        Assert.NotNull(atKit);

        var request = new PetBrainChoiceRequest
        {
            StageIndex = atKit!.Stage!.Index,
            NodeId = atKit.Stage.NodeId,
            OptionKey = "pick-light-2",
            IdempotencyKey = "step-abc",
            ClientRevision = atKit.Adventure!.Revision
        };

        var first = await client.Http.PostAsJsonAsync($"/api/pet-brain/runs/{run.RunId}/choices", request);
        first.EnsureSuccessStatusCode();

        var afterFirst = (await first.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
        var toolsAfterFirst = afterFirst.Adventure!.Inventory.Count(i => i.ItemId.StartsWith("tool-"));

        var second = await client.Http.PostAsJsonAsync($"/api/pet-brain/runs/{run.RunId}/choices", request);
        second.EnsureSuccessStatusCode();

        var afterSecond = (await second.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        Assert.Equal(
            toolsAfterFirst,
            afterSecond.Adventure!.Inventory.Count(i => i.ItemId.StartsWith("tool-")));
    }

    /// <summary>Köhnə vəziyyət nömrəsi ilə gələn addım MÜNAQİŞƏ alır.</summary>
    [Fact]
    public async Task KohneRevision_MunaqiseQaytarir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-stale@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var response = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest
            {
                StageIndex = run.Stage!.Index,
                NodeId = run.Stage.NodeId,
                OptionKey = "continue",
                ClientRevision = run.Adventure!.Revision - 5
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// İki fərqli profil GÖRÜNƏN fərqli təcrübə alır: qısa variantda yan
    /// səhnə təklifi ümumiyyətlə verilmir.
    /// </summary>
    [Fact]
    public async Task QisaVeUzunVariant_FerqliSehneSayiVerir()
    {
        var shortNodes = await VisitedNodesAsync("v2-short@petpal.test", AdventureVariants.Short);
        var longNodes = await VisitedNodesAsync("v2-long@petpal.test", AdventureVariants.Long);

        Assert.DoesNotContain("c4-side-offer", shortNodes);
        Assert.Contains("c4-side-offer", longNodes);
        Assert.True(longNodes.Count > shortNodes.Count);
    }

    /// <summary>
    /// Tamamlama STRUKTURLU xatirələr yaradır: sonluq, dünya bayrağı və
    /// tamamlanan yan tapşırıq ayrı-ayrı yazılır.
    /// </summary>
    [Fact]
    public async Task Tamamlama_StrukturluXatireYaradir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-memory@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client, AdventureVariants.Long);

        run = await MoonSecretAdventure.PlayAsync(client, run, "power-comms", "bot-fix", "side-garden", "rover-team");
        await PetBrainPlaythrough.CompleteAsync(client, run.RunId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var memories = await db.PetMemories
            .Where(m => m.ChildProfileId == client.ChildId
                        && m.FactKey == Api.PetBrain.ExperienceCatalog.MoonCrystalSecret)
            .ToListAsync();

        Assert.Contains(memories, m => m.Tags.Contains("ending"));
        Assert.Contains(memories, m => m.Tags.Contains("world"));
        Assert.Contains(memories, m => m.Tags.Contains("side-quest"));
    }

    /// <summary>Sonluq ÖZ kosmetikasını verir — üç sonluq üç fərqli nişan.</summary>
    [Fact]
    public async Task Sonluq_OzKosmetikasiniVerir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "v2-cosmetic@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        run = await MoonSecretAdventure.PlayAsync(client, run, "power-map", "plan-join", "rover-memory");
        var finished = await PetBrainPlaythrough.CompleteAsync(client, run.RunId);

        Assert.Equal(MoonKeys.EndingExplorer, finished.EndingKey);
        Assert.Equal("visor-explorer", finished.Summary!.UnlockedAccessoryCode);
    }

    private async Task<List<string>> NodesOnRouteAsync(string email, string route)
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, email);
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        run = await MoonSecretAdventure.PlayAsync(client, run, route);

        return await VisitedAsync(client);
    }

    private async Task<List<string>> RoverOptionsAsync(string email, string power)
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, email);
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var atRover = await MoonSecretAdventure.AdvanceToAsync(client, run, "c4-rover-choice", power);

        Assert.NotNull(atRover);

        return [.. atRover!.Stage!.Options.Select(o => o.Key)];
    }

    private async Task<List<string>> VisitedNodesAsync(string email, string variant)
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, email);
        var run = await MoonSecretAdventure.StartAsync(_factory, client, variant);

        await MoonSecretAdventure.PlayAsync(client, run);

        return await VisitedAsync(client);
    }

    private async Task<List<string>> VisitedAsync(ApiTestClient client)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var row = await db.AdventureRunStates
            .AsNoTracking()
            .Where(s => s.ChildProfileId == client.ChildId)
            .OrderByDescending(s => s.LastPlayedAt)
            .FirstAsync();

        return [.. row.VisitedNodeIds.OrderBy(n => n, StringComparer.Ordinal)];
    }
}
