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
/// «Ay Kristalının Sirri» testləri üçün ortaq qurulma.
///
/// <para>Run BİRBAŞA qurulur: yoxlanılan şey direktorun bu macərəni seçib
/// seçməməsi deyil (onun öz testləri var), macəranın ÖZÜdür.</para>
/// </summary>
public static class MoonSecretAdventure
{
    public static async Task<ApiTestClient> ChildAsync(TestWebAppFactory factory, string email)
    {
        var client = await ApiTestClient.CreateAsync(factory, email, "Aylin");
        await client.HatchAsync(factory);

        return client;
    }

    /// <summary>Macərəni başladır — kataloqdan, tam vəziyyət sətri ilə.</summary>
    public static async Task<PetBrainRunDto> StartAsync(
        TestWebAppFactory factory,
        ApiTestClient client,
        string variant = AdventureVariants.Standard)
    {
        var graph = MoonCrystalSecret.Definition;
        var template = ExperienceCatalog.Find(ExperienceCatalog.MoonCrystalSecret)!;

        Guid runId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = factory.Clock.GetUtcNow().UtcDateTime;

            var stale = await db.ExperienceRuns
                .Where(r => r.ChildProfileId == client.ChildId
                            && (r.Status == PetBrainRunStatus.Active || r.Status == PetBrainRunStatus.Paused))
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
                StartedAt = now
            };

            var state = AdventureStateMapper.ToState(
                null, run.StoryFlags, PetBrainBondTier.NewFriend, graph.StartNodeId);

            var (entered, _) = AdventureEngine.Enter(
                graph, state with { Variant = variant }, graph.Start, alreadyVisited: false);

            var stateRow = new AdventureRunState
            {
                ExperienceRun = run,
                ChildProfileId = client.ChildId,
                CurrentChapterId = graph.Start.ChapterId,
                Variant = variant,
                LastPlayedAt = now,
                CheckpointNodeId = graph.StartNodeId,
                CheckpointChapterId = graph.Start.ChapterId,
                CheckpointAt = now
            };

            AdventureStateMapper.Write(stateRow, entered);

            db.ExperienceRuns.Add(run);
            db.AdventureRunStates.Add(stateRow);

            await db.SaveChangesAsync();

            runId = run.Id;
        }

        var dto = await client.Http.GetFromJsonAsync<PetBrainRunDto>($"/api/pet-brain/runs/{runId}");

        return dto!;
    }

    /// <summary>
    /// Macərəni SONA qədər oynayır; seçim ekranlarında verilmiş açarları işlədir.
    ///
    /// <para>Açar cari ekranda YOXDURSA atlanır və ilk variant seçilir — belə
    /// olmasaydı, test bir şərtli variantın görünməməsinə görə sınardı,
    /// halbuki bu, məhz düzgün davranışdır.</para>
    /// </summary>
    public static async Task<PetBrainRunDto> PlayAsync(
        ApiTestClient client, PetBrainRunDto run, params string[] preferred)
    {
        var guard = 0;

        while (!PetBrainPlaythrough.IsFinished(run) && guard++ < 90)
        {
            string? option = null;

            if (run.Stage!.Options.Count > 0)
                option = preferred.FirstOrDefault(p =>
                    run.Stage.Options.Any(o => string.Equals(o.Key, p, StringComparison.Ordinal)));

            run = await PetBrainPlaythrough.StepAsync(client, run, option);
        }

        Assert.True(PetBrainPlaythrough.IsFinished(run), "Macəra sona çatmadı.");

        return run;
    }

    /// <summary>Adı verilmiş düyünə qədər aparır; çatmasa <c>null</c>.</summary>
    public static async Task<PetBrainRunDto?> AdvanceToAsync(
        ApiTestClient client, PetBrainRunDto run, string nodeId, params string[] preferred)
    {
        var guard = 0;

        while (!PetBrainPlaythrough.IsFinished(run) && guard++ < 90)
        {
            if (string.Equals(run.Stage!.NodeId, nodeId, StringComparison.Ordinal))
                return run;

            string? option = null;

            if (run.Stage.Options.Count > 0)
                option = preferred.FirstOrDefault(p =>
                    run.Stage.Options.Any(o => string.Equals(o.Key, p, StringComparison.Ordinal)));

            run = await PetBrainPlaythrough.StepAsync(client, run, option);
        }

        return string.Equals(run.Stage?.NodeId, nodeId, StringComparison.Ordinal) ? run : null;
    }
}
