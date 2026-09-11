using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Recap;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Rəsm də, video da SAXTADIR. Video provayderi Runway kimi ilk kadrı tələb
/// edir, rəsm isə test buraxana qədər «çəkilə» bilir — yəni uşağın macərəni
/// rəsmdən tez bitirdiyi an təkrarlanır.
/// </summary>
public sealed class PetBrainLateSceneFactory : TestWebAppFactory
{
    public FakePuzzleIllustrationProvider Scene { get; } = new();

    public FakeRecapVideoProvider Video { get; } = new() { RequireReference = true };

    public string StorageRoot { get; } =
        Path.Combine(Path.GetTempPath(), $"petpal-late-scene-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("PetBrain:UseAiIllustration", "true");
        builder.UseSetting("PetBrain:IllustrationModel", "fake-model");
        builder.UseSetting("PetBrain:IllustrationStorageRoot", StorageRoot);
        builder.UseSetting("PetBrainMedia:Provider", "Runway");
        builder.UseSetting("PetBrainMedia:Profile", "Budget");
        builder.UseSetting("PetBrainMedia:RecapPollMilliseconds", "50");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPuzzleIllustrationProvider>();
            services.AddSingleton<IPuzzleIllustrationProvider>(Scene);

            services.RemoveAll<IRecapVideoProvider>();
            services.AddSingleton<IRecapVideoProvider>(Video);
        });
    }

    protected override void Dispose(bool disposing)
    {
        Scene.Release();
        base.Dispose(disposing);

        if (disposing && Directory.Exists(StorageRoot))
            Directory.Delete(StorageRoot, recursive: true);
    }
}

/// <summary>
/// Video İTMİR: macəra bitəndən sonra hazırlansa da uşaq onu «Macəra
/// videoları» rəfində tapır, ilk kadrı gec gələn video isə gözləyir və ya
/// sonradan yaranır.
///
/// <para>Rəfin ikinci vədi pul ilə bağlıdır: ona baxmaq YENİ pullu iş
/// başlatmır — AI bağlı ikən bitmiş köhnə macəralar storyboard ilə qalır.</para>
/// </summary>
public class PetBrainRecapShelfTests
{
    /// <summary>
    /// Rəf yalnız uşağın ÖZ bitmiş macəralarını göstərir: ad, işarə, səhnə,
    /// tarix, storyboard və hazır olanda ÖZ run-ının video ünvanı.
    /// </summary>
    [Fact]
    public async Task Ref_OzBitmisMacerani_VideosuIleGosterir()
    {
        using var factory = new PetBrainRecapFactory();

        var owner = await NewChildAsync(factory, "shelf-owner@petpal.test");
        var other = await NewChildAsync(factory, "shelf-other@petpal.test");

        var run = await PetBrainPlaythrough.PlayToEndAsync(owner);
        await PetBrainPlaythrough.CompleteAsync(owner, run.RunId);

        await PetBrainPlaythrough.StartAsync(other);

        var entry = await WaitForShelfAsync(owner, run.RunId, PetBrainRecapStatus.Ready);

        Assert.False(string.IsNullOrWhiteSpace(entry.Title));
        Assert.False(string.IsNullOrWhiteSpace(entry.Icon));
        Assert.False(string.IsNullOrWhiteSpace(entry.SceneKey));
        Assert.NotNull(entry.CompletedAt);
        Assert.Equal(3, entry.Recap.Shots.Count);
        Assert.All(entry.Recap.Shots, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
        Assert.Equal($"/api/pet-brain/runs/{run.RunId}/recap/video", entry.Recap.VideoUrl);

        Assert.Empty(await ShelfAsync(other));
    }

    /// <summary>
    /// AI bağlı ikən bitmiş macəra rəfdə storyboard ilə qalır və rəfə baxmaq
    /// onun üçün pullu iş BAŞLATMIR — açar sonradan qoşulsa belə.
    /// </summary>
    [Fact]
    public async Task Ref_BaxmaqKreditXerclemir()
    {
        using var factory = new PetBrainRecapFactory();
        factory.Video.IsEnabled = false;

        var client = await NewChildAsync(factory, "shelf-free@petpal.test");
        var run = await PetBrainPlaythrough.PlayToEndAsync(client);
        await PetBrainPlaythrough.CompleteAsync(client, run.RunId);

        factory.Video.IsEnabled = true;

        for (var look = 0; look < 3; look++)
        {
            var entry = Assert.Single(await ShelfAsync(client), e => e.RunId == run.RunId);

            Assert.Equal(PetBrainRecapStatus.Fallback, entry.Recap.Status);
            Assert.Equal(3, entry.Recap.Shots.Count);
            Assert.Empty(entry.Recap.VideoUrl);

            await Task.Delay(100);
        }

        Assert.Equal(0, factory.Video.Starts);
    }

    /// <summary>
    /// Uşaq macərəni rəsm hələ çəkilərkən bitirir. Video ehtiyata DÜŞMÜR —
    /// ilk kadrını gözləyir, cəhd sayılmır və pul getmir; rəsm hazır olan kimi
    /// yaranır və rəfdə görünür.
    /// </summary>
    [Fact]
    public async Task SehneCekilerken_VideoGozleyirVeSonraYaranir()
    {
        using var factory = new PetBrainLateSceneFactory();
        factory.Scene.WaitForRelease = true;

        var client = await NewChildAsync(factory, "late-scene@petpal.test");
        var run = await PetBrainPlaythrough.PlayToEndAsync(client);
        var summary = (await PetBrainPlaythrough.CompleteAsync(client, run.RunId)).Summary!;

        Assert.Equal(PetBrainRecapStatus.Pending, summary.Recap.Status);

        await WaitUntilAsync(async () =>
            (await RecapRowAsync(factory, run.RunId))?.FailureReason == RecapCoordinator.AwaitingScene);

        var waiting = await RecapRowAsync(factory, run.RunId);

        Assert.Equal(PetBrainRecapStatus.Pending, waiting!.Status);
        Assert.Equal(0, waiting.Attempts);
        Assert.Equal(0, factory.Video.Starts);

        factory.Scene.Release();

        var entry = await WaitForShelfAsync(client, run.RunId, PetBrainRecapStatus.Ready);

        Assert.Equal(1, factory.Video.Starts);
        Assert.True(factory.Video.ReceivedReferenceImage);
        Assert.Equal($"/api/pet-brain/runs/{run.RunId}/recap/video", entry.Recap.VideoUrl);
    }

    /// <summary>
    /// İlk kadr olmadığı üçün ehtiyata düşmüş video əbədi itmir: rəsm sonradan
    /// hazır olanda rəf onu yenidən açır və video yaranır. Provayderə əvvəl heç
    /// nə getməmişdi — bu, ikinci pullu iş deyil.
    /// </summary>
    [Fact]
    public async Task GecGelenSehne_RefdeVideonuBerpaEdir()
    {
        using var factory = new PetBrainLateSceneFactory();
        factory.Scene.IsEnabled = false;

        var client = await NewChildAsync(factory, "late-shelf@petpal.test");
        var run = await PetBrainPlaythrough.PlayToEndAsync(client);
        await PetBrainPlaythrough.CompleteAsync(client, run.RunId);

        await WaitUntilAsync(async () =>
            (await RecapRowAsync(factory, run.RunId))?.FailureReason == RecapCoordinator.NoReferenceImage);

        Assert.Equal(0, factory.Video.Starts);

        await MakeSceneReadyAsync(factory, run.RunId);

        var entry = await WaitForShelfAsync(client, run.RunId, PetBrainRecapStatus.Ready);

        Assert.Equal(1, factory.Video.Starts);
        Assert.True(factory.Video.ReceivedReferenceImage);
        Assert.Equal($"/api/pet-brain/runs/{run.RunId}/recap/video", entry.Recap.VideoUrl);
    }

    // ==================== Köməkçilər ====================

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

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (await condition())
                return;

            await Task.Delay(25);
        }

        Assert.Fail("Gözlənilən vəziyyət yaranmadı.");
    }

    private static async Task<AdventureRecap?> RecapRowAsync(TestWebAppFactory factory, Guid runId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .AdventureRecaps.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ExperienceRunId == runId);
    }

    /// <summary>
    /// Videonun ilk kadrı olan səhnəni HAZIR edir — sanki rəsm gec də olsa
    /// çəkildi. Recap ilk həll olunmuş tapmacanın səhnəsini işlədir.
    /// </summary>
    private static async Task MakeSceneReadyAsync(PetBrainLateSceneFactory factory, Guid runId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hash = await db.IssuedPuzzles
            .AsNoTracking()
            .Where(p => p.ExperienceRunId == runId && p.Status == PetBrainPuzzleStatus.Solved)
            .OrderBy(p => p.StageIndex)
            .Select(p => p.SceneSpecHash)
            .FirstAsync();

        var folder = Path.Combine(factory.StorageRoot, "pet-brain-scenes");
        Directory.CreateDirectory(folder);

        await File.WriteAllBytesAsync(
            Path.Combine(folder, $"{hash}.png"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var scene = await db.PuzzleIllustrations.FirstAsync(i => i.SceneSpecHash == hash);

        scene.Status = PetBrainIllustrationStatus.Ready;
        scene.AssetKey = $"{hash}.png";
        scene.ContentType = "image/png";
        scene.Width = 720;
        scene.Height = 1280;
        scene.FailureReason = string.Empty;

        await db.SaveChangesAsync();
    }

    private static async Task<ApiTestClient> NewChildAsync(TestWebAppFactory factory, string email)
    {
        var client = await ApiTestClient.CreateAsync(factory, email, "Ava");
        await client.HatchAsync(factory);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var (key, category, score) in new (string, PetBrainTraitCategory, int)[]
                 {
                     (TraitKeys.Space, PetBrainTraitCategory.Interest, 88),
                     (TraitKeys.Science, PetBrainTraitCategory.Interest, 74),
                     (TraitKeys.Puzzles, PetBrainTraitCategory.Interest, 70),
                     (TraitKeys.ProblemSolver, PetBrainTraitCategory.PlayStyle, 86),
                     (TraitKeys.Explorer, PetBrainTraitCategory.PlayStyle, 72)
                 })
        {
            db.PlayerTraits.Add(new PlayerTrait
            {
                Id = Guid.NewGuid(),
                ChildProfileId = client.ChildId,
                Key = key,
                Category = category,
                Score = score,
                UpdatedAt = factory.Clock.GetUtcNow().UtcDateTime
            });
        }

        await db.SaveChangesAsync();

        return client;
    }
}
