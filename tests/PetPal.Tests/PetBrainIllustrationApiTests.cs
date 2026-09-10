using System.Net;
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
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// SAXTA rəsm provayderi — nə şəbəkə, nə API açarı, nə də pul lazımdır.
///
/// <para>Çağırış sayı SAYILIR: "bir səhnə → bir pullu sorğu" iddiası yalnız
/// belə yoxlana bilər.</para>
/// </summary>
public sealed class FakePuzzleIllustrationProvider : IPuzzleIllustrationProvider
{
    private int _calls;

    public bool IsEnabled { get; set; } = true;

    /// <summary>Neçə dəfə çağırıldı — idempotentlik testinin ölçdüyü rəqəm.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Modelə gedən promptlar — PII yoxlaması üçün saxlanılır.</summary>
    public List<string> Prompts { get; } = [];

    /// <summary>Nə qaytarsın: uğurlu portret, pozuq bayt, yataylıq, uğursuzluq.</summary>
    public string Behaviour { get; set; } = "ok";

    /// <summary>Sorğunun ləng gəlməsini təqlid edir — yarış şəraiti üçün.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public async Task<PuzzleIllustrationResult> RenderAsync(
        PuzzleSceneSpec spec, string prompt, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _calls);

        lock (Prompts)
            Prompts.Add(prompt);

        if (Delay > TimeSpan.Zero)
            await Task.Delay(Delay, ct);

        return Behaviour switch
        {
            "timeout" => PuzzleIllustrationResult.Failed("timeout", "fake", "fake-model"),
            "moderation" => PuzzleIllustrationResult.Failed("moderation", "fake", "fake-model"),
            "garbage" => PuzzleIllustrationResult.Ok(
                "<html>salam</html>"u8.ToArray(), "image/png", "fake", "fake-model"),
            "landscape" => PuzzleIllustrationResult.Ok(
                PngHeader(1536, 1024), "image/png", "fake", "fake-model"),
            _ => PuzzleIllustrationResult.Ok(PngHeader(1024, 1536), "image/png", "fake", "fake-model")
        };
    }

    /// <summary>Yoxlayıcı ölçünü IHDR-dən oxuyur — tam PNG lazım deyil.</summary>
    private static byte[] PngHeader(int width, int height)
    {
        var bytes = new byte[64];

        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);

        Write(bytes, 16, width);
        Write(bytes, 20, height);

        return bytes;

        static void Write(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }
    }
}

/// <summary>Rəsm qatını saxta provayderlə qaldıran fixture.</summary>
public sealed class PetBrainIllustrationFactory : TestWebAppFactory
{
    public FakePuzzleIllustrationProvider Provider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("PetBrain:UseAiIllustration", "true");
        builder.UseSetting("PetBrain:IllustrationModel", "fake-model");

        // Saxlanc test qovluğuna yönəldilir ki, repo çirklənməsin.
        builder.UseSetting("PetBrain:IllustrationStorageRoot", StorageRoot);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPuzzleIllustrationProvider>();
            services.AddSingleton<IPuzzleIllustrationProvider>(Provider);
        });
    }

    public string StorageRoot { get; } =
        Path.Combine(Path.GetTempPath(), $"petpal-scenes-{Guid.NewGuid():N}");

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(StorageRoot))
            Directory.Delete(StorageRoot, recursive: true);
    }
}

/// <summary>
/// Hekayə rəsminin HTTP davranışı.
///
/// <para>Bütün testlər saxta provayderlə işləyir: şəbəkə, API açarı və pul
/// tələb olunmur. Yoxlanan şey <b>rəsmin tapmacaya nə edə BİLMƏDİYİDİR</b>.</para>
/// </summary>
public class PetBrainIllustrationApiTests
{
    // Fixture PAYLAŞILMIR və bu, qəsdəndir: rədd edilmiş səhnə bazada
    // qalır (doğru davranışdır), ona görə paylaşılan fixture testləri
    // bir-birinin nəticəsindən asılı edərdi.
    private static PetBrainIllustrationFactory NewFactory() => new();

    /// <summary>
    /// Bir səhnə → BİR pullu sorğu (test 32).
    ///
    /// <para>İki uşaq eyni hekayə anını oynayır. Səhnə təsvirində uşağa aid heç
    /// nə olmadığı üçün hash eynidir — deməli ikinci generasiya BAŞLAMAMALIDIR
    /// və hər ikisi eyni faylı görməlidir.</para>
    /// </summary>
    [Fact]
    public async Task EyniSehne_IkinciDefeCekilmir()
    {
        using var factory = NewFactory();

        var aylin = await NewChildAsync(factory, "scene-a@petpal.test");
        var mia = await NewChildAsync(factory, "scene-b@petpal.test");

        var first = await ReachPuzzleAsync(aylin);
        var second = await ReachPuzzleAsync(mia);

        await WaitForSceneAsync(aylin, first.RunId);
        await WaitForSceneAsync(mia, second.RunId);

        // Ölçü SƏHNƏ sayına bağlanır, uşaq sayına yox.
        //
        // İki uşaq eyni profillə də FƏRQLİ macəra ala bilər: direktorun
        // yenilik/sürpriz oxu uşaq id-sindən asılıdır. Ona görə "iki uşaq →
        // bir sorğu" YANLIŞ gözləntidir; doğru invariant budur: NEÇƏ AYRI
        // səhnə varsa, o qədər sorğu — bir dənə də artıq yox.
        Assert.Equal(await DistinctScenesAsync(factory), factory.Provider.Calls);

        // Hər iki uşaq ÖZ endpoint-indən öz rəsmini oxuya bilir.
        Assert.NotNull(await ReadIllustrationAsync(aylin, first.Stage!.Puzzle!.PuzzleId));
        Assert.NotNull(await ReadIllustrationAsync(mia, second.Stage!.Puzzle!.PuzzleId));
    }

    /// <summary>
    /// EYNİ VAXTLI sorğular bir səhnə üçün BİR pullu iş başladır (test 32).
    ///
    /// <para>Uşaq ekranı yeniləyəndə (və ya şəbəkə təkrar göndərəndə) eyni
    /// tapmaca dəfələrlə oxunur. Hər oxunuş yeni generasiya başlatsaydı, bu,
    /// birbaşa pul itkisi olardı — və yaddaşdakı "artıq işləyir" yoxlaması
    /// proseslər arasında işləməzdi, ona görə təminat bazadadır.</para>
    /// </summary>
    [Fact]
    public async Task EyniVaxtliSorgular_BirIsBaslayir()
    {
        using var factory = NewFactory();

        // Provayder qəsdən LƏNGDİR: bütün paralel sorğular onun içindəykən gəlir.
        factory.Provider.Delay = TimeSpan.FromMilliseconds(150);

        var client = await NewChildAsync(factory, "scene-race@petpal.test");
        var run = await ReachPuzzleAsync(client);

        // Səkkiz eyni vaxtlı oxunuş — ekranı təkrar-təkrar yeniləyən uşaq.
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            client.Http.GetAsync($"/api/pet-brain/runs/{run.RunId}")));

        await WaitForSceneAsync(client, run.RunId);

        Assert.Equal(1, factory.Provider.Calls);
        Assert.Equal(1, await DistinctScenesAsync(factory));
    }

    /// <summary>Yad uşaq rəsmi GÖRƏ BİLMİR — 404, 403 deyil (test 33).</summary>
    [Fact]
    public async Task YadUsaq_ResmiGoreBilmir()
    {
        using var factory = NewFactory();

        var owner = await NewChildAsync(factory, "scene-owner@petpal.test");
        var stranger = await NewChildAsync(factory, "scene-stranger@petpal.test");

        var run = await ReachPuzzleAsync(owner);
        var puzzleId = run.Stage!.Puzzle!.PuzzleId;

        await WaitForSceneAsync(owner, run.RunId);

        var mine = await owner.Http.GetAsync($"/api/pet-brain/puzzles/{puzzleId}/illustration");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);

        var theirs = await stranger.Http.GetAsync($"/api/pet-brain/puzzles/{puzzleId}/illustration");
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);

        var anonymous = await factory.CreateClient()
            .GetAsync($"/api/pet-brain/puzzles/{puzzleId}/illustration");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    /// <summary>
    /// Provayderin URL-i heç yerdə görünmür: klient yalnız app-in ÖZ
    /// marşrutunu alır (test 33).
    /// </summary>
    [Fact]
    public async Task Klient_YalnizAppinOzMarsrutunuGorur()
    {
        using var factory = NewFactory();

        var client = await NewChildAsync(factory, "scene-url@petpal.test");
        var run = await ReachPuzzleAsync(client);

        run = await WaitForSceneAsync(client, run.RunId);

        var url = run.Stage!.Puzzle!.Scene.AssetUrl;

        Assert.StartsWith("/api/pet-brain/puzzles/", url, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", url, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Modelə uşağa aid HEÇ NƏ getmir (test 35-in rəsm qarşılığı).
    /// </summary>
    [Fact]
    public async Task Prompt_UsaginMelumatiniDasimir()
    {
        using var factory = NewFactory();

        var client = await NewChildAsync(factory, "scene-pii@petpal.test", "Aylin");
        var run = await ReachPuzzleAsync(client);

        await WaitForSceneAsync(client, run.RunId);

        string[] prompts;
        lock (factory.Provider.Prompts)
            prompts = [.. factory.Provider.Prompts];

        Assert.NotEmpty(prompts);

        foreach (var prompt in prompts)
        {
            Assert.DoesNotContain("Aylin", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(client.ChildId.ToString(), prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("scene-pii@petpal.test", prompt, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Provayderin uğursuzluğu tapmacanı DƏYİŞMİR (test 31).
    ///
    /// <para>Dörd fərqli uğursuzluq növü yoxlanılır — timeout, moderasiya rəddi,
    /// pozuq bayt, yanlış nisbət. Hər halda uşaq tapmacanı sona qədər həll edə
    /// bilməlidir və mükafat verilməlidir.</para>
    /// </summary>
    [Theory]
    [InlineData("timeout")]
    [InlineData("moderation")]
    [InlineData("garbage")]
    [InlineData("landscape")]
    public async Task ProvayderUgursuzlugu_TapmacaniPozmur(string behaviour)
    {
        using var factory = NewFactory();
        factory.Provider.Behaviour = behaviour;

        {
            var client = await NewChildAsync(factory, $"scene-fail-{behaviour}@petpal.test");
            var run = await ReachPuzzleAsync(client);

            var puzzle = run.Stage!.Puzzle!;

            // Səhnə ehtiyata düşür, amma lövhə TAM oynanandır.
            Assert.NotEmpty(puzzle.Nodes);
            Assert.NotEmpty(puzzle.Edges);
            Assert.NotEmpty(puzzle.Scene.AltText);

            run = await WaitForSceneAsync(client, run.RunId, expectReady: false);

            Assert.Equal(PetBrainIllustrationStatus.Fallback, run.Stage!.Puzzle!.Scene.IllustrationStatus);
            Assert.Empty(run.Stage.Puzzle.Scene.AssetUrl);

            // Ən vacib bənd: cavab hələ də DOĞRU qiymətləndirilir.
            var solved = await SolveRouteAsync(client, run);
            Assert.True(solved.CurrentStage > run.Stage.Index);
            Assert.Equal(0, solved.Mistakes);
        }
    }

    // ==================== Köməkçilər ====================

    /// <summary>Bazada neçə AYRI səhnə var — pullu sorğuların yuxarı həddi.</summary>
    private static async Task<int> DistinctScenesAsync(PetBrainIllustrationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.PuzzleIllustrations.CountAsync();
    }

    private static async Task<ApiTestClient> NewChildAsync(
        PetBrainIllustrationFactory factory, string email, string childName = "Ava")
    {
        var client = await ApiTestClient.CreateAsync(factory, email, childName);
        await client.HatchAsync(factory);
        await SeedSpaceProfileAsync(factory, client.ChildId);

        return client;
    }

    /// <summary>Kosmos profili — Mars marşrut lövhəsi gəlsin deyə.</summary>
    private static async Task SeedSpaceProfileAsync(PetBrainIllustrationFactory factory, Guid childId)
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

    private static async Task<PetBrainRunDto> ReachPuzzleAsync(ApiTestClient client)
    {
        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/runs", new StartPetBrainRunRequest());
        response.EnsureSuccessStatusCode();

        var run = (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        var guard = 0;
        while (run.Stage is not null && run.Stage.Kind != PetBrainStageKind.Puzzle && guard++ < 10)
        {
            var key = run.Stage.Kind == PetBrainStageKind.Intro
                ? "continue"
                : run.Stage.Options[0].Key;

            var step = await client.Http.PostAsJsonAsync(
                $"/api/pet-brain/runs/{run.RunId}/choices",
                new PetBrainChoiceRequest { StageIndex = run.CurrentStage, OptionKey = key });

            step.EnsureSuccessStatusCode();
            run = (await step.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
        }

        Assert.Equal(PetBrainStageKind.Puzzle, run.Stage!.Kind);
        return run;
    }

    /// <summary>
    /// Arxa fon işçisi səhnəni bitirənə qədər gözləyir.
    ///
    /// <para>Sabit gecikmə YOXDUR: run yenidən oxunur və vəziyyət dəyişənə
    /// qədər qısa aralıqlarla təkrarlanır. Bu, testi ləng maşında da sabit
    /// saxlayır.</para>
    /// </summary>
    private static async Task<PetBrainRunDto> WaitForSceneAsync(
        ApiTestClient client, Guid runId, bool expectReady = true)
    {
        PetBrainRunDto run = default!;

        for (var attempt = 0; attempt < 80; attempt++)
        {
            run = (await client.Http.GetFromJsonAsync<PetBrainRunDto>($"/api/pet-brain/runs/{runId}"))!;

            var status = run.Stage?.Puzzle?.Scene.IllustrationStatus;

            if (status is not null && status != PetBrainIllustrationStatus.Pending)
                return run;

            await Task.Delay(25);
        }

        Assert.Fail(expectReady
            ? "Səhnə hazır olmadı."
            : "Səhnə ehtiyat vəziyyətinə keçmədi.");

        return run;
    }

    private static async Task<byte[]?> ReadIllustrationAsync(ApiTestClient client, Guid puzzleId)
    {
        var response = await client.Http.GetAsync($"/api/pet-brain/puzzles/{puzzleId}/illustration");

        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsByteArrayAsync()
            : null;
    }

    /// <summary>Marşrutu GÖRÜNƏN məlumatdan həll edir — uşağın etdiyi kimi.</summary>
    private static async Task<PetBrainRunDto> SolveRouteAsync(ApiTestClient client, PetBrainRunDto run)
    {
        var puzzle = run.Stage!.Puzzle!;
        var route = ShortestValidRoute(puzzle);

        Assert.NotNull(route);

        var response = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest { StageIndex = run.Stage.Index, SelectedIds = route });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
    }

    private static List<string>? ShortestValidRoute(PetBrainPuzzleDto puzzle)
    {
        var start = puzzle.Nodes.First(n => n.Kind == PetBrainNodeKind.Start);
        var cost = puzzle.MoveCost ?? 1;
        var maximum = puzzle.MaximumEnergy ?? 0;

        List<string>? found = null;

        Walk([start.Id], puzzle.InitialEnergy ?? 0);
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
