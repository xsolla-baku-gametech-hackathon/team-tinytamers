using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Faylı itmiş media: sətir «hazır» deyir, saxlancda isə fayl yoxdur.
///
/// <para>Belə hal paketi yenidən quranda və ya diski təmizləyəndə yaranır.
/// Səhnə keşi əbədidir, ona görə itmiş fayl bərpa olunmasa tapmaca həmişə
/// sadə kadrda qalar, recap isə ilk kadrını tapmazdı. Video isə «hazır»
/// göründüyü üçün ekran açılmayan düymə göstərərdi.</para>
/// </summary>
public class PetBrainMediaLostFileTests
{
    [Fact]
    public async Task SehneninFayliItende_NovbetiIstifadedeBirDefeYenidenCekilir()
    {
        using var factory = new PetBrainIllustrationFactory();
        var spec = MarsScene();

        await EnsureAsync(factory, spec);
        await RenderAsync(factory, spec);

        var ready = await RowAsync(factory, spec);
        Assert.Equal(PetBrainIllustrationStatus.Ready, ready.Status);

        File.Delete(Path.Combine(factory.StorageRoot, "pet-brain-scenes", ready.AssetKey));

        var reopened = await EnsureAsync(factory, spec);

        Assert.Equal(PetBrainIllustrationStatus.Pending, reopened.Status);
        Assert.Empty(reopened.AssetKey);

        await RenderAsync(factory, spec);

        Assert.Equal(PetBrainIllustrationStatus.Ready, (await RowAsync(factory, spec)).Status);
        Assert.Equal(2, factory.Provider.Calls);
    }

    /// <summary>Faylı yerində olan hazır səhnəyə toxunulmur — ikinci pullu sorğu getmir.</summary>
    [Fact]
    public async Task SehneninFayliYerindedirse_YenidenCekilmir()
    {
        using var factory = new PetBrainIllustrationFactory();
        var spec = MarsScene();

        await EnsureAsync(factory, spec);
        await RenderAsync(factory, spec);

        var again = await EnsureAsync(factory, spec);

        Assert.Equal(PetBrainIllustrationStatus.Ready, again.Status);
        Assert.Equal(1, factory.Provider.Calls);
    }

    /// <summary>
    /// Faylı itmiş video storyboard-a düşür: nə açılmayan düymə, nə də
    /// avtomatik yenidən çəkmə — rəfə və yekuna nə qədər baxılsa da pul getmir.
    /// </summary>
    [Fact]
    public async Task VideonunFayliItende_StoryboardaDusur_BaxmaqPulXerclemir()
    {
        using var factory = new PetBrainRecapFactory();

        var client = await ApiTestClient.CreateAsync(factory, "lost-video@petpal.test", "Aylin");
        await client.HatchAsync(factory);

        var run = await PetBrainPlaythrough.PlayToEndAsync(client);
        await PetBrainPlaythrough.CompleteAsync(client, run.RunId);

        var ready = await WaitForShelfAsync(client, run.RunId, PetBrainRecapStatus.Ready);

        Assert.Equal(HttpStatusCode.OK, (await client.Http.GetAsync(ready.Recap.VideoUrl)).StatusCode);

        foreach (var file in Directory.GetFiles(Path.Combine(factory.StorageRoot, "pet-brain-recaps")))
            File.Delete(file);

        for (var look = 0; look < 3; look++)
        {
            var entry = Assert.Single(await ShelfAsync(client), e => e.RunId == run.RunId);

            Assert.Equal(PetBrainRecapStatus.Fallback, entry.Recap.Status);
            Assert.Empty(entry.Recap.VideoUrl);
            Assert.Equal(3, entry.Recap.Shots.Count);
        }

        var recap = await client.Http.GetFromJsonAsync<PetBrainRecapDto>($"/api/pet-brain/runs/{run.RunId}/recap");

        Assert.Equal(PetBrainRecapStatus.Fallback, recap!.Status);
        Assert.Empty(recap.VideoUrl);

        var video = await client.Http.GetAsync($"/api/pet-brain/runs/{run.RunId}/recap/video");

        Assert.Equal(HttpStatusCode.NotFound, video.StatusCode);
        Assert.Equal(1, factory.Video.Starts);
    }

    /// <summary>
    /// Faylı itmiş videonu eyni seçimlərlə bitən YENİ macəra bir dəfə yenidən
    /// sifariş edir: hash eynidir, sətir də eynidir. Baxış isə yenə çəkdirmir.
    /// </summary>
    [Fact]
    public async Task VideonunFayliItende_EyniMacaraniYenidenBitirmek_BirDefeYenidenCekir()
    {
        using var factory = new PetBrainRecapFactory();

        var client = await ApiTestClient.CreateAsync(factory, "lost-video-replay@petpal.test", "Aylin");
        await client.HatchAsync(factory);

        var first = await PetBrainPlaythrough.PlayToEndAsync(client);
        await PetBrainPlaythrough.CompleteAsync(client, first.RunId);
        await WaitForShelfAsync(client, first.RunId, PetBrainRecapStatus.Ready);

        foreach (var file in Directory.GetFiles(Path.Combine(factory.StorageRoot, "pet-brain-recaps")))
            File.Delete(file);

        Assert.Equal(
            PetBrainRecapStatus.Fallback,
            Assert.Single(await ShelfAsync(client), e => e.RunId == first.RunId).Recap.Status);

        var start = await client.Http.PostAsJsonAsync(
            "/api/pet-brain/runs", new StartPetBrainRunRequest { TemplateKey = first.TemplateKey });
        start.EnsureSuccessStatusCode();

        var replay = await PetBrainPlaythrough.ContinueToEndAsync(
            client, (await start.Content.ReadFromJsonAsync<PetBrainRunDto>())!);

        await PetBrainPlaythrough.CompleteAsync(client, replay.RunId);

        var healed = await WaitForShelfAsync(client, replay.RunId, PetBrainRecapStatus.Ready);

        Assert.Equal(HttpStatusCode.OK, (await client.Http.GetAsync(healed.Recap.VideoUrl)).StatusCode);
        Assert.Equal(2, factory.Video.Starts);
        Assert.Equal(1, await RecapRowsAsync(factory));

        await ShelfAsync(client);
        await client.Http.GetAsync($"/api/pet-brain/runs/{replay.RunId}/recap");

        Assert.Equal(2, factory.Video.Starts);
    }

    private static PuzzleSceneSpec MarsScene() =>
        PuzzleSceneSpec.For(
            PuzzleBlueprintCatalog.Find(PuzzleBlueprintCatalog.MarsSignalRouteKey)!,
            "az",
            ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!,
            species: "fox");

    private static async Task<PuzzleIllustration> EnsureAsync(PetBrainIllustrationFactory factory, PuzzleSceneSpec spec)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<PuzzleIllustrationCoordinator>()
            .EnsureRowAsync(spec, CancellationToken.None);
    }

    private static async Task RenderAsync(PetBrainIllustrationFactory factory, PuzzleSceneSpec spec)
    {
        using var scope = factory.Services.CreateScope();

        await scope.ServiceProvider
            .GetRequiredService<PuzzleIllustrationCoordinator>()
            .RenderAsync(spec.Hash(), spec, CancellationToken.None);
    }

    private static async Task<PuzzleIllustration> RowAsync(PetBrainIllustrationFactory factory, PuzzleSceneSpec spec)
    {
        using var scope = factory.Services.CreateScope();
        var hash = spec.Hash();

        return await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .PuzzleIllustrations
            .AsNoTracking()
            .FirstAsync(i => i.SceneSpecHash == hash);
    }

    private static async Task<int> RecapRowsAsync(PetBrainRecapFactory factory)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AdventureRecaps.CountAsync();
    }

    private static async Task<List<PetBrainRecapEntryDto>> ShelfAsync(ApiTestClient client) =>
        (await client.Http.GetFromJsonAsync<List<PetBrainRecapEntryDto>>("/api/pet-brain/recaps"))!;

    private static async Task<PetBrainRecapEntryDto> WaitForShelfAsync(
        ApiTestClient client, Guid runId, PetBrainRecapStatus expected)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var entry = (await ShelfAsync(client)).FirstOrDefault(e => e.RunId == runId);

            if (entry?.Recap.Status == expected)
                return entry;

            await Task.Delay(25);
        }

        Assert.Fail($"Rəfdə video {expected} olmadı.");
        return new PetBrainRecapEntryDto();
    }
}
