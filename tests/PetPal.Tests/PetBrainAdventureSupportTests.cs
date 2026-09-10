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
/// Macəranın DƏSTƏK qatı: yumşaq uğursuzluq, ipucu nərdivanı, jurnal
/// qeydi, pet-in təklifi və ana ekrandakı iz.
///
/// <para>Bunların hər biri uşağın ilişdiyi və ya qayıtdığı anı idarə edir —
/// yəni ən az oynanan, amma ən çox ağrı verən yollardır.</para>
/// </summary>
public class PetBrainAdventureSupportTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainAdventureSupportTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>
    /// Tapmacanı ilk cəhddə, ipucusuz həll edən uşaq AYRI səhnəyə gedir.
    ///
    /// <para>Bu budaq əvvəl ölü idi: server bir macəranın açarını qoyurdu,
    /// flaqman isə başqasını gözləyirdi.</para>
    /// </summary>
    [Fact]
    public async Task IlkCehddeHell_AyriSehneyeAparir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "support-clean@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var puzzle = await MoonSecretAdventure.AdvanceToAsync(client, run, "c1-pattern");
        Assert.NotNull(puzzle);

        var after = await PetBrainPlaythrough.StepAsync(client, puzzle!);

        Assert.Equal("c1-source-clean", after.Stage!.NodeId);
    }

    /// <summary>
    /// Hər ipucu istəyi bir pillə qalxır və son pillədə pet birgə tamamlamanı
    /// təklif edir.
    /// </summary>
    [Fact]
    public async Task IpucuNerdivani_HerIstekdePilleQalxir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "support-ladder@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var puzzle = await MoonSecretAdventure.AdvanceToAsync(client, run, "c1-pattern");
        Assert.NotNull(puzzle);

        List<PetBrainHintLevel> levels = [];
        var current = puzzle!;

        for (var i = 0; i < 4; i++)
        {
            current = await HintAsync(client, current);
            levels.Add(current.Stage!.HintLevel);
        }

        Assert.Equal(
            [
                PetBrainHintLevel.DirectionalHint,
                PetBrainHintLevel.WorkedExample,
                PetBrainHintLevel.StepByStepHelp,
                PetBrainHintLevel.AssistedCompletion
            ],
            levels);

        Assert.True(current.Stage!.AssistAvailable);
        Assert.NotEmpty(current.Stage.HintRevealIds);

        Assert.True(
            current.Stage.HintRevealIds.Count < current.Stage.Puzzle!.AnswerSchema.Min,
            "Son pillədə belə tam həll klientə getməməlidir.");
    }

    /// <summary>
    /// Birgə tamamlama hekayəni irəli aparır, amma mənimsəməyə YAZILMIR.
    /// </summary>
    [Fact]
    public async Task BirgeTamamlama_HekayeniAparirMenimsemeniDeyismir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "support-assist@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var puzzle = await MoonSecretAdventure.AdvanceToAsync(client, run, "c1-pattern");
        Assert.NotNull(puzzle);

        var current = puzzle!;

        for (var i = 0; i < HintLadder.AssistAfterHints; i++)
            current = await HintAsync(client, current);

        var response = await client.Http.PostAsJsonAsync($"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest
            {
                StageIndex = current.Stage!.Index,
                NodeId = current.Stage.NodeId,
                AcceptAssist = true
            });

        response.EnsureSuccessStatusCode();

        var after = (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        Assert.Equal("c1-source", after.Stage!.NodeId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var issued = await db.IssuedPuzzles.SingleAsync(p =>
            p.ExperienceRunId == run.RunId && p.StageIndex == current.Stage.Index);

        Assert.True(issued.CompletedWithAssist);
        Assert.Equal(PetBrainPuzzleStatus.Solved, issued.Status);

        var outcome = await db.RunStageOutcomes.SingleAsync(o => o.ExperienceRunId == run.RunId && o.NodeId == "c1-pattern");

        Assert.Equal(PetBrainStageResult.Assisted, outcome.Result);

        Assert.False(await db.MechanicMasteries.AnyAsync(m =>
            m.ChildProfileId == client.ChildId && m.Mechanic == MechanicKeys.Pattern));
    }

    /// <summary>Vaxtından əvvəl birgə tamamlama RƏDD olunur — pilləni server sayır.</summary>
    [Fact]
    public async Task VaxtindanEvvelBirgeTamamlama_RedOlunur()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "support-early@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var puzzle = await MoonSecretAdventure.AdvanceToAsync(client, run, "c1-pattern");
        Assert.NotNull(puzzle);

        var response = await client.Http.PostAsJsonAsync($"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest
            {
                StageIndex = puzzle!.Stage!.Index,
                NodeId = puzzle.Stage.NodeId,
                AcceptAssist = true
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Köməksiz həll mənimsəməni YENİLƏYİR — yeni mexanikalar da sayılır.</summary>
    [Fact]
    public async Task KomeksizHell_MenimsemeniYenileyir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "support-mastery@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var puzzle = await MoonSecretAdventure.AdvanceToAsync(client, run, "c1-pattern");
        Assert.NotNull(puzzle);

        await PetBrainPlaythrough.StepAsync(client, puzzle!);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(await db.MechanicMasteries.AnyAsync(m =>
            m.ChildProfileId == client.ChildId && m.Mechanic == MechanicKeys.Pattern));
    }

    /// <summary>
    /// Krater yolundan gələn uşaq izləri jurnalda saxlayıb — müşahidə
    /// tapmacasının yanında o qeyd görünür. Mağara yolundan gələndə yoxdur.
    /// </summary>
    [Fact]
    public async Task JurnalQeydi_YalnizTapilmisIpucuIleGorunur()
    {
        var crater = await MoonSecretAdventure.ChildAsync(_factory, "support-journal-crater@petpal.test");
        var craterRun = await MoonSecretAdventure.StartAsync(_factory, crater);
        var craterPuzzle = await MoonSecretAdventure.AdvanceToAsync(crater, craterRun, "c4-recall", "route-crater");

        Assert.NotNull(craterPuzzle);
        Assert.False(string.IsNullOrWhiteSpace(craterPuzzle!.Stage!.JournalNote));

        var cave = await MoonSecretAdventure.ChildAsync(_factory, "support-journal-cave@petpal.test");
        var caveRun = await MoonSecretAdventure.StartAsync(_factory, cave);
        var cavePuzzle = await MoonSecretAdventure.AdvanceToAsync(cave, caveRun, "c4-recall", "route-cave");

        Assert.NotNull(cavePuzzle);
        Assert.True(string.IsNullOrWhiteSpace(cavePuzzle!.Stage!.JournalNote));
    }

    /// <summary>
    /// Elmə meyilli uşaqda pet «Tədqiqatçı» rolunu TƏKLİF edir — sıra dəyişmir,
    /// digər rollar da yerindədir.
    /// </summary>
    [Fact]
    public async Task PetinTeklifi_UsaginUslubunaUygundur()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "support-suggest@petpal.test");
        await SeedTraitAsync(client, PetBrainTraitCategory.Interest, TraitKeys.Science, 95);

        var run = await MoonSecretAdventure.StartAsync(_factory, client);
        var role = await MoonSecretAdventure.AdvanceToAsync(client, run, "c1-role");

        Assert.NotNull(role);

        var options = role!.Stage!.Options;

        Assert.Equal(3, options.Count);
        Assert.Single(options, o => o.Suggested);
        Assert.True(options.Single(o => o.Key == MoonKeys.RoleScientist).Suggested);
    }

    /// <summary>Fərdiləşdirmə söndürülübsə pet heç nə təklif etmir.</summary>
    [Fact]
    public async Task FerdilesdirmeSondurulubse_TeklifYoxdur()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "support-nosuggest@petpal.test");
        await SeedTraitAsync(client, PetBrainTraitCategory.Interest, TraitKeys.Science, 95);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.PersonalizationSettings.Add(new ChildPersonalizationSettings
            {
                ChildProfileId = client.ChildId,
                PersonalizationEnabled = false
            });

            await db.SaveChangesAsync();
        }

        var run = await MoonSecretAdventure.StartAsync(_factory, client);
        var role = await MoonSecretAdventure.AdvanceToAsync(client, run, "c1-role");

        Assert.NotNull(role);
        Assert.DoesNotContain(role!.Stage!.Options, o => o.Suggested);
    }

    /// <summary>
    /// Ana ekran çipi HARADA qaldığımızı deyir və dayandırılmış macərəni də
    /// unutmur.
    /// </summary>
    [Fact]
    public async Task AnaEkranCipi_FesliVeDayanmaniGosterir()
    {
        var client = await MoonSecretAdventure.ChildAsync(_factory, "support-home@petpal.test");
        var run = await MoonSecretAdventure.StartAsync(_factory, client);

        var chapterTwo = await MoonSecretAdventure.AdvanceToAsync(client, run, "c2-arrival");
        Assert.NotNull(chapterTwo);

        var active = await HomeChipAsync(client);

        Assert.True(active.HasActiveRun);
        Assert.Contains("2/6", active.ChapterLabel);
        Assert.True(active.ProgressPercent > 0);
        Assert.False(active.IsPaused);

        var pause = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/pause", new PetBrainPauseRequest { PlayedSeconds = 60 });

        pause.EnsureSuccessStatusCode();

        var paused = await HomeChipAsync(client);

        Assert.True(paused.HasActiveRun);
        Assert.True(paused.IsPaused);
        Assert.Contains("2/6", paused.ChapterLabel);
    }

    private static async Task<PetBrainRunDto> HintAsync(ApiTestClient client, PetBrainRunDto run)
    {
        var response = await client.Http.PostAsJsonAsync($"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest
            {
                StageIndex = run.Stage!.Index,
                NodeId = run.Stage.NodeId,
                RequestHint = true
            });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
    }

    private async Task<PetBrainHomeChipDto> HomeChipAsync(ApiTestClient client)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPetBrainService>();

        var chip = await service.GetHomeChipAsync(client.ChildId);

        Assert.NotNull(chip);

        return chip!;
    }

    private async Task SeedTraitAsync(
        ApiTestClient client, PetBrainTraitCategory category, string key, int score)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.PlayerTraits.Add(new PlayerTrait
        {
            ChildProfileId = client.ChildId,
            Category = category,
            Key = key,
            Score = score,
            UpdatedAt = _factory.Clock.GetUtcNow().UtcDateTime
        });

        await db.SaveChangesAsync();
    }
}
