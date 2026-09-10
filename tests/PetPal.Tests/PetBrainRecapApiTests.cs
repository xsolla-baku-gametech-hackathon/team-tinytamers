using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Recap;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// SAXTA video provayderi — nə şəbəkə, nə açar, nə də pul.
///
/// <para>İki mərhələli müqaviləni təqlid edir: iş <b>başladılır</b> (id verilir),
/// sonra izlənir. Başlatma sayı SAYILIR — «bir seçim dəsti → bir pullu iş»
/// iddiası yalnız belə yoxlana bilər.</para>
/// </summary>
public sealed class FakeRecapVideoProvider : IRecapVideoProvider
{
    private int _starts;

    public bool IsEnabled { get; set; } = true;

    /// <summary>Neçə dəfə PULLU iş başladı.</summary>
    public int Starts => Volatile.Read(ref _starts);

    /// <summary>Modelə gedən promptlar — PII yoxlaması üçün.</summary>
    public List<string> Prompts { get; } = [];

    /// <summary>Referans kadr gəldimi — vizual davamlılıq üçün vacibdir.</summary>
    public bool ReceivedReferenceImage { get; private set; }

    /// <summary>Nə qaytarsın: hazır video, pozuq bayt, uğursuzluq.</summary>
    public string Behaviour { get; set; } = "ok";

    /// <summary>Neçə dəfə "işləyir" desin — sonra nəticə verir.</summary>
    public int WorkingPolls { get; set; }

    /// <summary>Provayderin bildirdiyi HƏQİQİ kredit (0 = təxminlə eyni).</summary>
    public int RealizedCredits { get; set; }

    private int _polls;

    public Task<RecapJobStart> StartAsync(
        AdventureRecapSpec spec, string prompt, byte[]? referenceImage, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return Task.FromResult(RecapJobStart.Failed("disabled"));

        if (Behaviour == "start-failed")
            return Task.FromResult(RecapJobStart.Failed("http-503"));

        Interlocked.Increment(ref _starts);
        ReceivedReferenceImage = referenceImage is { Length: > 0 };

        lock (Prompts)
            Prompts.Add(prompt);

        return Task.FromResult(new RecapJobStart(true, $"job-{spec.Hash()[..8]}", "fake", "fake-video", string.Empty));
    }

    public Task<RecapJobProgress> PollAsync(string jobId, CancellationToken ct = default)
    {
        if (Interlocked.Increment(ref _polls) <= WorkingPolls)
            return Task.FromResult(RecapJobProgress.Working());

        return Task.FromResult(Behaviour switch
        {
            "timeout" => RecapJobProgress.Failed("timeout"),
            "moderation" => RecapJobProgress.Failed("moderation"),
            "garbage" => RecapJobProgress.Ready("<html>salam</html>"u8.ToArray(), 50),
            "landscape" => RecapJobProgress.Ready(Mp4(1280, 720, 10.0), 50),
            "wrong-duration" => RecapJobProgress.Ready(Mp4(720, 1280, 4.0), 50),
            _ => RecapJobProgress.Ready(Mp4(720, 1280, 10.0), RealizedCredits == 0 ? 50 : RealizedCredits)
        });
    }

    /// <summary>Minimal MP4: <c>ftyp</c> + <c>mvhd</c> + <c>tkhd</c>.</summary>
    public static byte[] Mp4(int width, int height, double seconds)
    {
        List<byte> bytes = [];
        const int timescale = 1000;

        bytes.AddRange([0, 0, 0, 16]);
        bytes.AddRange("ftyp"u8.ToArray());
        bytes.AddRange("isom"u8.ToArray());
        bytes.AddRange([0, 0, 0, 0]);

        bytes.AddRange("mvhd"u8.ToArray());
        bytes.AddRange([0, 0, 0, 0]);
        bytes.AddRange(Be(0));
        bytes.AddRange(Be(0));
        bytes.AddRange(Be(timescale));
        bytes.AddRange(Be((uint)Math.Round(seconds * timescale)));

        bytes.AddRange("tkhd"u8.ToArray());
        bytes.AddRange([0, 0, 0, 0]);
        bytes.AddRange(Be(0));
        bytes.AddRange(Be(0));
        bytes.AddRange(Be(1));
        bytes.AddRange(Be(0));
        bytes.AddRange(Be((uint)Math.Round(seconds * timescale)));
        bytes.AddRange(new byte[8]);
        bytes.AddRange(new byte[8]);
        bytes.AddRange(new byte[36]);
        bytes.AddRange(Be((uint)width << 16));
        bytes.AddRange(Be((uint)height << 16));

        return [.. bytes];

        static byte[] Be(uint value) =>
            [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
    }
}

/// <summary>Recap qatını saxta provayderlə qaldıran fixture.</summary>
public sealed class PetBrainRecapFactory : TestWebAppFactory
{
    public FakeRecapVideoProvider Video { get; } = new();

    public string StorageRoot { get; } =
        Path.Combine(Path.GetTempPath(), $"petpal-recaps-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("PetBrain:IllustrationStorageRoot", StorageRoot);

        // Xərc siyasəti CANLI olsun deyə profil seçilir; API açarı isə YOXDUR,
        // yəni heç bir şəbəkə çağırışı mümkün deyil. Video provayderi onsuz da
        // saxta ilə əvəzlənir.
        builder.UseSetting("PetBrainMedia:Provider", "Runway");
        builder.UseSetting("PetBrainMedia:Profile", "Budget");

        // İzləmə addımı testdə qısadır — məntiq eynidir, gözləmə isə yoxdur.
        builder.UseSetting("PetBrainMedia:RecapPollMilliseconds", "50");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRecapVideoProvider>();
            services.AddSingleton<IRecapVideoProvider>(Video);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(StorageRoot))
            Directory.Delete(StorageRoot, recursive: true);
    }
}

/// <summary>
/// Recap videosunun HTTP davranışı.
///
/// <para>Ən vacib iddia budur: <b>video heç nəyi dəyişə bilmir</b>. Uğursuzluq,
/// rədd, kvota və gecikmə — heç biri XP-ni, bağı, xatirəni və kosmetiki geri
/// almır.</para>
/// </summary>
public class PetBrainRecapApiTests
{
    private static PetBrainRecapFactory NewFactory() => new();

    /// <summary>
    /// AI bağlı olanda da uşaq TAM 10 saniyəlik recap görür (test 30).
    /// </summary>
    [Fact]
    public async Task AiBagliOlanda_DeterministikRecapQalir()
    {
        using var factory = NewFactory();
        factory.Video.IsEnabled = false;

        var client = await NewChildAsync(factory, "recap-off@petpal.test");
        var run = await PlayToEndAsync(client);
        var summary = (await CompleteAsync(client, run.RunId)).Summary!;

        Assert.Equal(PetBrainRecapStatus.Fallback, summary.Recap.Status);
        Assert.Empty(summary.Recap.VideoUrl);
        Assert.Equal(10, summary.Recap.DurationSeconds);
        Assert.Equal(3, summary.Recap.Shots.Count);

        // Altyazılar HƏMİŞƏ doludur — uşaq öz seçimlərini oxuyur.
        Assert.All(summary.Recap.Shots, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));

        // Mükafat toxunulmazdır.
        Assert.True(summary.XpEarned > 0);
        Assert.True(summary.BondEarned > 0);
        Assert.Equal(0, factory.Video.Starts);
    }

    /// <summary>
    /// Hazır video yalnız app-in ÖZ marşrutundan verilir və sahiblik yoxlanılır
    /// (test 39).
    /// </summary>
    [Fact]
    public async Task HazirVideo_YalnizSahibineVerilir()
    {
        using var factory = NewFactory();

        var owner = await NewChildAsync(factory, "recap-owner@petpal.test");
        var stranger = await NewChildAsync(factory, "recap-stranger@petpal.test");

        var run = await PlayToEndAsync(owner);
        await CompleteAsync(owner, run.RunId);

        var ready = await WaitForRecapAsync(owner, run.RunId);

        Assert.Equal(PetBrainRecapStatus.Ready, ready.Status);
        Assert.StartsWith("/api/pet-brain/runs/", ready.VideoUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("http", ready.VideoUrl, StringComparison.OrdinalIgnoreCase);

        var mine = await owner.Http.GetAsync(ready.VideoUrl);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Equal("video/mp4", mine.Content.Headers.ContentType?.MediaType);

        var theirs = await stranger.Http.GetAsync(ready.VideoUrl);
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);

        var anonymous = await factory.CreateClient().GetAsync(ready.VideoUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    /// <summary>Range sorğusu dəstəklənir — oynadıcı faylı hissə-hissə alır (test 39).</summary>
    [Fact]
    public async Task Video_RangeSorgusunuDestekleyir()
    {
        using var factory = NewFactory();

        var client = await NewChildAsync(factory, "recap-range@petpal.test");
        var run = await PlayToEndAsync(client);
        await CompleteAsync(client, run.RunId);

        var ready = await WaitForRecapAsync(client, run.RunId);

        using var request = new HttpRequestMessage(HttpMethod.Get, ready.VideoUrl);
        request.Headers.Range = new RangeHeaderValue(0, 15);

        var response = await client.Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(16, (await response.Content.ReadAsByteArrayAsync()).Length);
    }

    /// <summary>
    /// Modelə uşağa aid heç nə və tapmacanın həlli GETMİR; referans kadr isə
    /// GEDİR (test 35).
    /// </summary>
    [Fact]
    public async Task Prompt_UsaqMelumatiDasimir()
    {
        using var factory = NewFactory();

        var client = await NewChildAsync(factory, "recap-pii@petpal.test", "Aylin");
        var run = await PlayToEndAsync(client);
        await CompleteAsync(client, run.RunId);

        await WaitForRecapAsync(client, run.RunId);

        string[] prompts;
        lock (factory.Video.Prompts)
            prompts = [.. factory.Video.Prompts];

        Assert.NotEmpty(prompts);

        foreach (var prompt in prompts)
        {
            Assert.DoesNotContain("Aylin", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(client.ChildId.ToString(), prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("recap-pii@petpal.test", prompt, StringComparison.OrdinalIgnoreCase);

            // Marşrutun həlli (düyün sırası) prompta düşmür.
            Assert.DoesNotContain("lander", prompt, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Təkrar və eyni vaxtlı tamamlama BİR recap işi yaradır (test 24, 37).
    /// </summary>
    [Fact]
    public async Task TekrarTamamlama_BirIsYaradir()
    {
        using var factory = NewFactory();

        // Provayder qəsdən ləngdir: paralel sorğular onun içindəykən gəlir.
        factory.Video.WorkingPolls = 2;

        var client = await NewChildAsync(factory, "recap-once@petpal.test");
        var run = await PlayToEndAsync(client);

        // Beş eyni vaxtlı tamamlama — uşaq düyməyə təkrar basır.
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            client.Http.PostAsync($"/api/pet-brain/runs/{run.RunId}/complete", null)));

        await WaitForRecapAsync(client, run.RunId);

        Assert.Equal(1, factory.Video.Starts);
        Assert.Equal(1, await RecapCountAsync(factory));
    }

    /// <summary>
    /// Eyni run-ı təkrar açmaq KEŞDƏN gəlir və heç nə xərcləmir (test 40).
    /// </summary>
    [Fact]
    public async Task TekrarBaxis_KesdenGelir()
    {
        using var factory = NewFactory();

        var client = await NewChildAsync(factory, "recap-cache@petpal.test");
        var run = await PlayToEndAsync(client);
        await CompleteAsync(client, run.RunId);

        var first = await WaitForRecapAsync(client, run.RunId);
        var startsAfterFirst = factory.Video.Starts;

        // Təkrar tamamlama — hazır yekun qayıdır.
        var again = (await CompleteAsync(client, run.RunId)).Summary!;

        Assert.Equal(first.VideoUrl, again.Recap.VideoUrl);
        Assert.Equal(PetBrainRecapStatus.Ready, again.Recap.Status);
        Assert.Equal(startsAfterFirst, factory.Video.Starts);
    }

    /// <summary>
    /// Provayderin hər cür uğursuzluğu mükafatı GERİ ALMIR (test 38).
    /// </summary>
    [Theory]
    [InlineData("start-failed", PetBrainRecapStatus.Fallback)]
    [InlineData("timeout", PetBrainRecapStatus.Fallback)]
    [InlineData("moderation", PetBrainRecapStatus.Fallback)]
    [InlineData("garbage", PetBrainRecapStatus.Rejected)]
    [InlineData("landscape", PetBrainRecapStatus.Rejected)]
    [InlineData("wrong-duration", PetBrainRecapStatus.Rejected)]
    public async Task Ugursuzluq_MukafatiGeriAlmir(string behaviour, PetBrainRecapStatus expected)
    {
        using var factory = NewFactory();
        factory.Video.Behaviour = behaviour;

        var client = await NewChildAsync(factory, $"recap-fail-{behaviour}@petpal.test");
        var run = await PlayToEndAsync(client);

        var summary = (await CompleteAsync(client, run.RunId)).Summary!;

        // Mükafat DƏRHAL və tam verilib.
        // Mükafat həmişə verilir. Kosmetik yalnız Mars/Əjdaha şablonlarında
        // var, direktor isə başqa macəra seçə bilər — ona görə burada XP və
        // bağ yoxlanılır: onlar HƏR tamamlamada verilir.
        Assert.True(summary.XpEarned > 0);
        Assert.True(summary.BondEarned > 0);

        var recap = await WaitForRecapAsync(client, run.RunId, expectReady: false);

        Assert.Equal(expected, recap.Status);
        Assert.Empty(recap.VideoUrl);

        // Deterministik recap YERİNDƏDİR.
        Assert.Equal(3, recap.Shots.Count);
        Assert.Equal(10, recap.DurationSeconds);

        // Mükafat bazada da qalıb.
        var pet = await client.Http.GetFromJsonAsync<PetPal.Shared.Dtos.Pets.PetDto>("/api/pet");
        Assert.NotNull(pet);
        Assert.True(pet.Bond > 10);
    }

    /// <summary>
    /// Gündəlik kvota dolanda YENİ pullu iş başlamır (10C: xərc nəzarəti).
    /// </summary>
    [Fact]
    public async Task GundelikKvota_YeniIsiDayandirir()
    {
        using var factory = NewFactory();

        var client = await NewChildAsync(factory, "recap-quota@petpal.test");

        // Kvotanı süni şəkildə doldururuq: üç pullu recap sətri.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            for (var i = 0; i < 3; i++)
            {
                db.AdventureRecaps.Add(new AdventureRecap
                {
                    Id = Guid.NewGuid(),
                    RecapSpecHash = $"quota-{i:D2}",
                    ChildProfileId = client.ChildId,
                    ExperienceRunId = Guid.NewGuid(),
                    ExperienceKey = ExperienceCatalog.MarsRoverRescue,
                    Status = PetBrainRecapStatus.Ready,
                    RequestedAt = factory.Clock.GetUtcNow().UtcDateTime
                });
            }

            await db.SaveChangesAsync();
        }

        var run = await PlayToEndAsync(client);
        var summary = (await CompleteAsync(client, run.RunId)).Summary!;

        Assert.Equal(PetBrainRecapStatus.Fallback, summary.Recap.Status);
        Assert.Equal(0, factory.Video.Starts);

        // Uşaq üçün nəticə EYNİDİR: mükafat da, xülasə də yerindədir.
        Assert.True(summary.XpEarned > 0);
        Assert.Equal(3, summary.Recap.Shots.Count);
    }

    /// <summary>
    /// Yenidən başlatma işi İTİRMİR və ikinci dəfə pul xərcləmir (test 36).
    ///
    /// <para>Prosesin yenidən qalxması <c>RecapWorker</c>-in açılış süpürgəsi
    /// ilə təqlid edilir: sətir <c>Generating</c> qalır, tapşırıq id-si isə
    /// bazadadır — işçi məhz onu izləyir, yenisini yaratmır.</para>
    /// </summary>
    [Fact]
    public async Task YenidenBaslatma_IkinciDefePulXerclemir()
    {
        using var factory = NewFactory();

        var client = await NewChildAsync(factory, "recap-restart@petpal.test");
        var run = await PlayToEndAsync(client);
        await CompleteAsync(client, run.RunId);

        await WaitForRecapAsync(client, run.RunId);

        var startsBefore = factory.Video.Starts;
        Assert.Equal(1, startsBefore);

        // "Proses yenidən qalxdı": sətir yenə Generating-dir, tapşırıq id-si var.
        string jobId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.AdventureRecaps.FirstAsync(r => r.ExperienceRunId == run.RunId);

            jobId = row.ProviderJobId;
            Assert.NotEmpty(jobId);

            row.Status = PetBrainRecapStatus.Generating;
            row.AssetKey = string.Empty;
            await db.SaveChangesAsync();
        }

        // İşçinin gördüyü işi əl ilə təkrarlayırıq — eyni scope, eyni koordinator.
        using (var scope = factory.Services.CreateScope())
        {
            var factoryService = scope.ServiceProvider.GetRequiredService<IRecapSpecFactory>();
            var spec = await factoryService.BuildAsync(run.RunId);

            Assert.NotNull(spec);

            var coordinator = scope.ServiceProvider.GetRequiredService<RecapCoordinator>();
            await coordinator.AdvanceAsync(spec, CancellationToken.None);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.AdventureRecaps.AsNoTracking().FirstAsync(r => r.ExperienceRunId == run.RunId);

            Assert.Equal(PetBrainRecapStatus.Ready, row.Status);

            // Eyni tapşırıq DAVAM ETDİRİLDİ — yenisi yaradılmadı.
            Assert.Equal(jobId, row.ProviderJobId);
        }

        Assert.Equal(startsBefore, factory.Video.Starts);
    }

    /// <summary>
    /// Həqiqi xərc razılaşdırılandan çox olsa, DÖVRƏ AÇILIR — səssizcə artıq
    /// xərclənmir (10C).
    /// </summary>
    [Fact]
    public async Task HeqiqiXercTexmindenCoxdursa_DovreAcilir()
    {
        using var factory = NewFactory();
        factory.Video.RealizedCredits = 500;

        var client = await NewChildAsync(factory, "recap-overspend@petpal.test");
        var run = await PlayToEndAsync(client);
        await CompleteAsync(client, run.RunId);

        await WaitForRecapAsync(client, run.RunId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.AdventureRecaps.AsNoTracking().FirstAsync(r => r.ExperienceRunId == run.RunId);

        Assert.Equal(500, row.RealizedCredits);

        // Təxmin sıfırdırsa (provayder bağlıdır) dövrə açılmır — bu test
        // yalnız provayder AÇIQ olanda mənalıdır.
        var breaker = factory.Services.GetRequiredService<PetPal.Api.PetBrain.Media.MediaCircuitBreaker>();

        if (row.EstimatedCredits > 0)
            Assert.True(breaker.IsOpen);
    }

    // ==================== Köməkçilər ====================

    private static async Task<int> RecapCountAsync(PetBrainRecapFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.AdventureRecaps.CountAsync();
    }

    private static async Task<ApiTestClient> NewChildAsync(
        PetBrainRecapFactory factory, string email, string childName = "Ava")
    {
        var client = await ApiTestClient.CreateAsync(factory, email, childName);
        await client.HatchAsync(factory);
        await SeedSpaceProfileAsync(factory, client.ChildId);

        return client;
    }

    private static async Task SeedSpaceProfileAsync(PetBrainRecapFactory factory, Guid childId)
    {
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
                ChildProfileId = childId,
                Key = key,
                Category = category,
                Score = score,
                UpdatedAt = factory.Clock.GetUtcNow().UtcDateTime
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<PetBrainRunDto> PlayToEndAsync(ApiTestClient client)
    {
        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/runs", new StartPetBrainRunRequest());
        response.EnsureSuccessStatusCode();

        var run = (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        var guard = 0;
        while (run.Stage is not null && guard++ < 40)
        {
            var request = run.Stage.Kind switch
            {
                PetBrainStageKind.Intro => new PetBrainChoiceRequest
                {
                    StageIndex = run.Stage.Index,
                    OptionKey = "continue"
                },

                PetBrainStageKind.Choice => new PetBrainChoiceRequest
                {
                    StageIndex = run.Stage.Index,
                    OptionKey = run.Stage.Options[0].Key
                },

                _ => new PetBrainChoiceRequest
                {
                    StageIndex = run.Stage.Index,
                    SelectedIds = Solve(run.Stage.Puzzle!)
                }
            };

            var step = await client.Http.PostAsJsonAsync(
                $"/api/pet-brain/runs/{run.RunId}/choices", request);

            step.EnsureSuccessStatusCode();
            run = (await step.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
        }

        Assert.True(run.CurrentStage >= run.StageCount, "Macəra sona çatmadı.");
        return run;
    }

    private static async Task<PetBrainRunDto> CompleteAsync(ApiTestClient client, Guid runId)
    {
        var response = await client.Http.PostAsync($"/api/pet-brain/runs/{runId}/complete", null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
    }

    /// <summary>Arxa fon işçisi yekunlaşana qədər gözləyir.</summary>
    private static async Task<PetBrainRecapDto> WaitForRecapAsync(
        ApiTestClient client, Guid runId, bool expectReady = true)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var run = (await client.Http.GetFromJsonAsync<PetBrainRunDto>($"/api/pet-brain/runs/{runId}"))!;
            var recap = run.Summary?.Recap;

            if (recap is not null &&
                recap.Status is not (PetBrainRecapStatus.Pending or PetBrainRecapStatus.Generating))
                return recap;

            await Task.Delay(25);
        }

        Assert.Fail(expectReady ? "Recap hazır olmadı." : "Recap yekunlaşmadı.");
        return new PetBrainRecapDto();
    }

    /// <summary>Tapmacanı GÖRÜNƏN məlumatdan həll edir.</summary>
    private static List<string> Solve(PetBrainPuzzleDto puzzle)
    {
        if (puzzle.Mechanic != PetBrainPuzzleMechanic.OrderedRoute)
            return [.. puzzle.Items.Take(Math.Max(1, puzzle.AnswerSchema.Min)).Select(i => i.Id)];

        var start = puzzle.Nodes.First(n => n.Kind == PetBrainNodeKind.Start);
        var cost = puzzle.MoveCost ?? 1;
        var maximum = puzzle.MaximumEnergy ?? 0;

        List<string>? found = null;

        Walk([start.Id], puzzle.InitialEnergy ?? 0);

        Assert.NotNull(found);
        return found;

        void Walk(List<string> path, int energy)
        {
            if (found is not null || path.Count > puzzle.AnswerSchema.Max)
                return;

            var here = puzzle.Nodes.First(n => n.Id == path[^1]);

            if (here.Kind == PetBrainNodeKind.Recharge)
                energy = Math.Min(maximum, energy + (here.EnergyDelta ?? 0));

            if (here.Kind == PetBrainNodeKind.Goal)
            {
                if (puzzle.RequiredBeforeGoal.All(path.Contains))
                    found = [.. path];

                return;
            }

            foreach (var next in puzzle.Edges
                         .Where(e => e.From == here.Id || e.To == here.Id)
                         .Select(e => e.From == here.Id ? e.To : e.From)
                         .OrderBy(id => id, StringComparer.Ordinal))
            {
                if (path.Contains(next) || energy - cost < 0)
                    continue;

                path.Add(next);
                Walk(path, energy - cost);
                path.RemoveAt(path.Count - 1);
            }
        }
    }
}
