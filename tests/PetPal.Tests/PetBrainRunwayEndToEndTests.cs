using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Media;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Appı Runway adapteri ilə qaldıran fixture.
///
/// <para>Yalnız NƏQLİYYAT əvəzlənir: adapter, xərc siyasəti, arxa fon işçiləri,
/// saxlanc və endpoint-lər produksiyadakı kimi qalır. Beləliklə pullu sınaqdan
/// əvvəl bütün zəncir bir dəfə həqiqətən işlədilir — açar və pul olmadan.</para>
/// </summary>
public sealed class RunwayAppFactory : TestWebAppFactory
{
    public const string ApiKey = "runway-rehearsal-key";

    public RunwayEmulator Emulator { get; } = new();

    public string StorageRoot { get; } =
        Path.Combine(Path.GetTempPath(), $"petpal-runway-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("PetBrain:IllustrationStorageRoot", StorageRoot);
        builder.UseSetting("PetBrainMedia:Provider", "Runway");
        builder.UseSetting("PetBrainMedia:Profile", "Budget");
        builder.UseSetting("PetBrainMedia:RecapPollMilliseconds", "50");
        builder.UseSetting("PetBrainMedia:ImagePollMilliseconds", "50");
        builder.UseSetting("Runway:ApiKey", ApiKey);

        builder.ConfigureTestServices(services =>
            services.AddHttpClient<IRunwayTaskClient, RunwayTaskClient>()
                .ConfigurePrimaryHttpMessageHandler(() => Emulator));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(StorageRoot))
            Directory.Delete(StorageRoot, recursive: true);
    }
}

/// <summary>
/// Pullu sınağın MƏŞQİ: macəra başlayır → hekayə rəsmi Runway-dən gəlir → uşaq
/// onu öz endpoint-indən alır → macəra bitir → HƏMİN rəsm videonun ilk kadrı
/// kimi göndərilir → video yoxlanır, saxlanır və ekranın soruşduğu endpoint-dən
/// hazır qayıdır.
///
/// <para>Bu test canlı açarla ediləcək işin eynisini edir; fərq yalnız
/// nəqliyyatın emulyator olmasıdır.</para>
/// </summary>
public class PetBrainRunwayEndToEndTests
{
    [Fact]
    public async Task TamAxin_SekilVideonunIlkKadriOlur_VeIkisiDeEkranaCatir()
    {
        using var factory = new RunwayAppFactory();

        var image = PetBrainRunwayProviderTests.Png(720, 1280);

        factory.Emulator.ImageOutput = image;
        factory.Emulator.VideoOutput = FakeRecapVideoProvider.Mp4(720, 1280, 10.0);
        factory.Emulator.Cost = 50;

        var client = await NewChildAsync(factory, "runway-e2e@petpal.test");

        var run = await PetBrainPlaythrough.StartAsync(client);
        var puzzleId = run.UpcomingScene?.PuzzleId ?? run.Stage?.Puzzle?.PuzzleId;

        Assert.NotNull(puzzleId);

        var scene = await WaitForIllustrationAsync(client, puzzleId!.Value);

        Assert.Equal("image/png", scene.Content.Headers.ContentType?.MediaType);
        Assert.Equal(image, await scene.Content.ReadAsByteArrayAsync());

        run = await PetBrainPlaythrough.ContinueToEndAsync(client, run);
        await PetBrainPlaythrough.CompleteAsync(client, run.RunId);

        var recap = await WaitForRecapAsync(client, run.RunId);

        Assert.Equal(PetBrainRecapStatus.Ready, recap.Status);

        var video = await client.Http.GetAsync(recap.VideoUrl);

        Assert.Equal(HttpStatusCode.OK, video.StatusCode);
        Assert.Equal("video/mp4", video.Content.Headers.ContentType?.MediaType);
        Assert.Equal(factory.Emulator.VideoOutput, await video.Content.ReadAsByteArrayAsync());

        var creates = factory.Emulator.ApiRequests
            .Where(r => r.Request.Method == HttpMethod.Post)
            .ToList();

        var imageJobs = creates
            .Where(c => c.Request.RequestUri!.AbsolutePath.EndsWith("text_to_image", StringComparison.Ordinal))
            .ToList();

        var videoJob = Assert.Single(
            creates,
            c => c.Request.RequestUri!.AbsolutePath.EndsWith("image_to_video", StringComparison.Ordinal));

        Assert.NotEmpty(imageJobs);
        Assert.All(imageJobs, job =>
        {
            Assert.Contains("\"model\":\"gen4_image\"", job.Body, StringComparison.Ordinal);
            Assert.Contains("\"ratio\":\"720:1280\"", job.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("referenceImages", job.Body, StringComparison.Ordinal);
        });

        Assert.Contains("\"model\":\"gen4_turbo\"", videoJob.Body, StringComparison.Ordinal);
        Assert.Contains("\"duration\":10", videoJob.Body, StringComparison.Ordinal);

        Assert.Contains(
            $"\"promptImage\":\"data:image/png;base64,{Convert.ToBase64String(image)}\"",
            videoJob.Body,
            StringComparison.Ordinal);

        Assert.All(factory.Emulator.CdnRequests, r =>
        {
            Assert.Null(r.Request.Headers.Authorization);
            Assert.False(r.Request.Headers.Contains("X-Runway-Version"));
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var illustration = await db.PuzzleIllustrations
            .AsNoTracking()
            .FirstAsync(i => i.Status == PetBrainIllustrationStatus.Ready);

        Assert.Equal("runway", illustration.Provider);
        Assert.Equal(MediaModelCatalog.Gen4Image, illustration.Model);
        Assert.Equal(720, illustration.Width);
        Assert.Equal(1280, illustration.Height);

        var row = await db.AdventureRecaps.AsNoTracking().FirstAsync(r => r.ExperienceRunId == run.RunId);

        Assert.Equal(PetBrainRecapStatus.Ready, row.Status);
        Assert.Equal("runway", row.Provider);
        Assert.Equal(MediaModelCatalog.Gen4Turbo, row.Model);
        Assert.Equal(50, row.EstimatedCredits);
        Assert.Equal(50, row.RealizedCredits);
        Assert.Equal(10_000, row.DurationMs);
        Assert.Equal(720, row.Width);
        Assert.Equal(1280, row.Height);
    }

    /// <summary>
    /// Eyni macəranı təkrar açmaq PROVAYDERƏ getmir: rəsm də, video da keşdən
    /// gəlir. Canlı sistemdə bu, ikinci dəfə pul verilməməsi deməkdir.
    /// </summary>
    [Fact]
    public async Task TekrarBaxis_ProvaydereGetmir()
    {
        using var factory = new RunwayAppFactory();

        factory.Emulator.ImageOutput = PetBrainRunwayProviderTests.Png(720, 1280);
        factory.Emulator.VideoOutput = FakeRecapVideoProvider.Mp4(720, 1280, 10.0);

        var client = await NewChildAsync(factory, "runway-cache@petpal.test");

        var run = await PetBrainPlaythrough.PlayToEndAsync(client);
        await PetBrainPlaythrough.CompleteAsync(client, run.RunId);
        await WaitForRecapAsync(client, run.RunId);

        var callsAfterFirst = factory.Emulator.Creates;

        await PetBrainPlaythrough.CompleteAsync(client, run.RunId);
        var again = await WaitForRecapAsync(client, run.RunId);

        Assert.Equal(PetBrainRecapStatus.Ready, again.Status);
        Assert.Equal(callsAfterFirst, factory.Emulator.Creates);
    }

    // ==================== Köməkçilər ====================

    private static async Task<ApiTestClient> NewChildAsync(RunwayAppFactory factory, string email)
    {
        var client = await ApiTestClient.CreateAsync(factory, email, "Aylin");
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

    /// <summary>Rəsm hazır olana qədər gözləyir — ekranın etdiyi kimi (404 = hələ çəkilir).</summary>
    private static async Task<HttpResponseMessage> WaitForIllustrationAsync(ApiTestClient client, Guid puzzleId)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var response = await client.Http.GetAsync($"/api/pet-brain/puzzles/{puzzleId}/illustration");

            if (response.StatusCode == HttpStatusCode.OK)
                return response;

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            await Task.Delay(25);
        }

        Assert.Fail("Hekayə rəsmi hazır olmadı.");
        return new HttpResponseMessage();
    }

    private static async Task<PetBrainRecapDto> WaitForRecapAsync(ApiTestClient client, Guid runId)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var recap = (await client.Http.GetFromJsonAsync<PetBrainRecapDto>($"/api/pet-brain/runs/{runId}/recap"))!;

            if (recap.Status is not (PetBrainRecapStatus.Pending or PetBrainRecapStatus.Generating))
                return recap;

            await Task.Delay(25);
        }

        Assert.Fail("Recap yekunlaşmadı.");
        return new PetBrainRecapDto();
    }
}
