using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Adventure V2-nin HTTP səthi: mərkəz, ön baxış, başlat/davam/təkrar,
/// vəziyyətin hissələri, addım ləqəbləri və epiloq.
///
/// <para>Hər marşrut eyni sahiblik qaydasını daşıyır: uşaq id-si claim-dən
/// gəlir və yad uşağın run-u <c>404</c> alır.</para>
/// </summary>
public class PetBrainAdventureApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainAdventureApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Merkez_FesilliMacerasiGosterir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "api-hub@petpal.test");

        var before = await HubCardAsync(client);

        Assert.Equal(6, before.ChapterCount);
        Assert.Equal(3, before.EndingsTotal);
        Assert.True(before.EstimatedMinutes >= 30);
        Assert.Equal(PetBrainAdventureProgress.NotStarted, before.Progress);
        Assert.NotEmpty(before.Mechanics);

        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var during = await HubCardAsync(client);

        Assert.Equal(PetBrainAdventureProgress.InProgress, during.Progress);
        Assert.Equal(run.RunId, during.OpenRunId);
        Assert.True(during.CanStart);
    }

    [Fact]
    public async Task OnBaxis_FesilleriVeElcatanligiVerir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "api-preview@petpal.test");

        var preview = await client.Http.GetFromJsonAsync<PetBrainAdventurePreviewDto>(
            $"/api/pet-brain/adventures/{ExperienceCatalog.MoonCrystalSecret}");

        Assert.NotNull(preview);
        Assert.Equal(6, preview!.Chapters.Count);
        Assert.All(preview.Chapters, c => Assert.False(string.IsNullOrWhiteSpace(c.Title)));
        Assert.All(preview.Chapters, c => Assert.True(string.IsNullOrEmpty(c.Summary)));
        Assert.NotEmpty(preview.Accessibility);
        Assert.False(string.IsNullOrWhiteSpace(preview.PaceLabel));
    }

    [Fact]
    public async Task NamelumMacera_404()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "api-unknown@petpal.test");

        var response = await client.Http.GetAsync("/api/pet-brain/adventures/no-such-adventure");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Girişsiz sorğu macəra mərkəzinə daxil ola bilmir.</summary>
    [Fact]
    public async Task GirissizSorgu_401()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/pet-brain/adventures");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Dayandırılmış macəra mərkəzdən BAŞLADILANDA davam edir — yenisi açılmır.</summary>
    [Fact]
    public async Task Merkezden_DayandirilmisMaceraDavamEdir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "api-hub-resume@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var pause = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/pause", new PetBrainPauseRequest());

        pause.EnsureSuccessStatusCode();

        var start = await client.Http.PostAsync(
            $"/api/pet-brain/adventures/{ExperienceCatalog.MoonCrystalSecret}/start", null);

        start.EnsureSuccessStatusCode();

        var resumed = (await start.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        Assert.Equal(run.RunId, resumed.RunId);
        Assert.Equal(PetBrainRunStatus.Active, resumed.Status);
    }

    /// <summary>
    /// Bitirilmiş macəra TƏKRAR oynanır: yeni run yaranır, köhnəsi tarixçədə qalır.
    /// </summary>
    [Fact]
    public async Task BitirilmisMacera_TekrarOynanirVeTarixceQalir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "api-replay@petpal.test");
        var first = await MoonSecretAdventure.StartAsync(_factory, client);

        first = await MoonSecretAdventure.PlayAsync(client, first);
        await PetBrainPlaythrough.CompleteAsync(client, first.RunId);

        var card = await HubCardAsync(client);

        Assert.Equal(PetBrainAdventureProgress.Completed, card.Progress);
        Assert.Equal(1, card.EndingsFound);
        Assert.True(card.CanStart);

        var start = await client.Http.PostAsync(
            $"/api/pet-brain/adventures/{ExperienceCatalog.MoonCrystalSecret}/start", null);

        start.EnsureSuccessStatusCode();

        var replay = (await start.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        Assert.NotEqual(first.RunId, replay.RunId);
        Assert.Equal(ExperienceCatalog.MoonCrystalSecret, replay.TemplateKey);
        Assert.NotNull(replay.Adventure);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var history = await db.ExperienceRuns
            .Where(r => r.ChildProfileId == client.ChildId && r.TemplateKey == ExperienceCatalog.MoonCrystalSecret)
            .ToListAsync();

        Assert.Contains(history, r => r.Id == first.RunId && r.Status == PetBrainRunStatus.Completed);
        Assert.Contains(history, r => r.Id == replay.RunId && r.Status == PetBrainRunStatus.Active);
    }

    [Fact]
    public async Task VeziyyetinHisseleri_AyriOxunur()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "api-parts@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        run = (await MoonSecretAdventure.AdvanceToAsync(client, run, "c2-power"))!;

        var objectives = await client.Http.GetFromJsonAsync<List<PetBrainObjectiveDto>>(
            $"/api/pet-brain/runs/{run.RunId}/objectives");
        var inventory = await client.Http.GetFromJsonAsync<List<PetBrainInventoryItemDto>>(
            $"/api/pet-brain/runs/{run.RunId}/inventory");
        var clues = await client.Http.GetFromJsonAsync<List<PetBrainClueDto>>(
            $"/api/pet-brain/runs/{run.RunId}/clues");
        var map = await client.Http.GetFromJsonAsync<List<PetBrainChapterDto>>(
            $"/api/pet-brain/runs/{run.RunId}/map");

        Assert.InRange(objectives!.Count, 1, 3);
        Assert.Contains(inventory!, i => i.ItemId == MoonKeys.ItemPowerCell);
        Assert.Contains(clues!, c => c.ClueId == MoonKeys.ClueSignalRhythm);
        Assert.Equal(6, map!.Count);
        Assert.Contains(map, c => c.Status == AdventureChapterStatus.Completed);
    }

    /// <summary>Yad uşağın run-u — mövcudluğu da təsdiqlənmir, <c>404</c>.</summary>
    [Fact]
    public async Task YadUsaginRunu_404()
    {
        var owner = await MoonSecretAdventure.ChildAsync(_factory, "api-owner@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, owner);

        var stranger = await MoonSecretAdventure.ChildAsync(_factory, "api-stranger@petpal.test");

        foreach (var part in new[] { "objectives", "inventory", "clues", "map" })
        {
            var response = await stranger.Http.GetAsync($"/api/pet-brain/runs/{run.RunId}/{part}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        var action = await stranger.Http.PostAsJsonAsync($"/api/pet-brain/runs/{run.RunId}/actions",
            new PetBrainChoiceRequest { StageIndex = 0, NodeId = run.Stage!.NodeId, OptionKey = "continue" });

        Assert.Equal(HttpStatusCode.NotFound, action.StatusCode);
    }

    [Fact]
    public async Task Actions_AddimAtir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "api-actions@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var response = await client.Http.PostAsJsonAsync($"/api/pet-brain/runs/{run.RunId}/actions",
            new PetBrainChoiceRequest
            {
                StageIndex = run.Stage!.Index,
                NodeId = run.Stage.NodeId,
                OptionKey = "continue",
                ClientRevision = run.Adventure!.Revision,
                IdempotencyKey = "api-action-1"
            });

        response.EnsureSuccessStatusCode();

        var after = (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        Assert.Equal("c1-listen", after.Stage!.NodeId);
        Assert.Equal(run.Adventure.Revision + 1, after.Adventure!.Revision);
    }

    [Fact]
    public async Task Hints_NovbetiPilleniVerir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "api-hints@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);
        var puzzle = (await MoonSecretAdventure.AdvanceToAsync(client, run, "c1-pattern"))!;

        var response = await client.Http.PostAsJsonAsync($"/api/pet-brain/runs/{run.RunId}/hints",
            new PetBrainChoiceRequest { StageIndex = puzzle.Stage!.Index, NodeId = puzzle.Stage.NodeId });

        response.EnsureSuccessStatusCode();

        var hinted = (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        Assert.Equal("c1-pattern", hinted.Stage!.NodeId);
        Assert.Equal(PetBrainHintLevel.DirectionalHint, hinted.Stage.HintLevel);
        Assert.False(string.IsNullOrWhiteSpace(hinted.Stage.Hint));
    }

    /// <summary>Tapmaca cavabı YALNIZ cari tapmacanın id-si ilə qəbul olunur.</summary>
    [Fact]
    public async Task PuzzleSubmit_YalnizCariTapmacaya()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "api-submit@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);
        var puzzle = (await MoonSecretAdventure.AdvanceToAsync(client, run, "c1-pattern"))!;

        var answer = new PetBrainChoiceRequest
        {
            StageIndex = puzzle.Stage!.Index,
            NodeId = puzzle.Stage.NodeId,
            SelectedIds = PetBrainPlaythrough.Solve(puzzle.Stage.Puzzle!)
        };

        var wrongId = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/puzzles/{Guid.NewGuid()}/submit", answer);

        Assert.Equal(HttpStatusCode.Conflict, wrongId.StatusCode);

        var right = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/puzzles/{puzzle.Stage.Puzzle!.PuzzleId}/submit", answer);

        right.EnsureSuccessStatusCode();

        var after = (await right.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        Assert.NotEqual("c1-pattern", after.Stage!.NodeId);
    }

    [Fact]
    public async Task Epiloq_SonluqUnvanVeQarmaqVerir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "api-epilogue@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client, AdventureVariants.Long);

        run = await MoonSecretAdventure.PlayAsync(client, run, "power-comms", "rover-team", "plan-share");

        var finished = await PetBrainPlaythrough.CompleteAsync(client, run.RunId);
        var epilogue = finished.Summary!.Epilogue;

        Assert.NotNull(epilogue);
        Assert.Equal(MoonKeys.EndingRobotFriend, epilogue!.EndingKey);
        Assert.False(string.IsNullOrWhiteSpace(epilogue.EarnedTitle));
        Assert.False(string.IsNullOrWhiteSpace(epilogue.PetRecall));
        Assert.False(string.IsNullOrWhiteSpace(epilogue.SequelHook));
        Assert.NotEmpty(epilogue.WorldChanges);
        Assert.Equal(1, epilogue.EndingsFound);
        Assert.Equal(3, epilogue.EndingsTotal);
        Assert.False(string.IsNullOrWhiteSpace(epilogue.ReplayHint));

        var reopened = await client.Http.GetFromJsonAsync<PetBrainRunDto>($"/api/pet-brain/runs/{run.RunId}");

        Assert.NotNull(reopened!.Summary!.Epilogue);
        Assert.Equal(epilogue.EarnedTitle, reopened.Summary.Epilogue!.EarnedTitle);
    }

    private static async Task<PetBrainAdventureSummaryDto> HubCardAsync(ApiTestClient client)
    {
        var list = await client.Http.GetFromJsonAsync<List<PetBrainAdventureSummaryDto>>("/api/pet-brain/adventures");

        Assert.NotNull(list);

        return list!.Single(a => a.Key == ExperienceCatalog.MoonCrystalSecret);
    }
}
