using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Media;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Recap;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Runway Dev API-nin SƏNƏDLƏŞDİRİLMİŞ cavab formasını təqlid edən nəqliyyat.
///
/// <para>Formalar rəsmi SDK tiplərindəndir (2026-09-11): yaratma cavabı yalnız
/// <c>id</c> və <c>estimatedCost</c>, tapşırıq isə <c>status</c>,
/// <c>output</c>, <c>cost</c> və uğursuzluqda <c>failureCode</c> daşıyır. Hazır
/// fayl API hostunda deyil, CDN-dədir.</para>
///
/// <para>Şəkil və video tapşırıqları AYRI izlənilir: id-si marşrutdan gəlir
/// (<c>img-1</c>, <c>vid-1</c>), hazır fayl isə həmin növün baytlarıdır.</para>
/// </summary>
public sealed class RunwayEmulator : HttpMessageHandler
{
    public const string ApiHost = "api.dev.runwayml.com";
    public const string CdnHost = "dnznrvs05pmza.cloudfront.net";

    private readonly ConcurrentDictionary<string, int> _pollsByTask = new(StringComparer.Ordinal);
    private int _creates;

    /// <summary>Tutulan sorğular və gövdələri.</summary>
    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    /// <summary>Növbəti yaratma sorğularına verilən xəta cavabları (məsələn 429).</summary>
    public Queue<HttpStatusCode> CreateFailures { get; } = new();

    /// <summary>Növbəti izləmə sorğularına verilən xəta cavabları (məsələn 503).</summary>
    public Queue<HttpStatusCode> PollFailures { get; } = new();

    /// <summary>Birinci yaratma sorğusunda əlaqə qopur — cavab gəlmir.</summary>
    public bool DropFirstCreate { get; set; }

    /// <summary>Tapşırıq neçə dəfə «işləyir» desin.</summary>
    public int RunningPolls { get; set; } = 1;

    /// <summary>Doludursa tapşırıq bu kodla uğursuz olur.</summary>
    public string? FailureCode { get; set; }

    public int Cost { get; set; } = 5;

    /// <summary>Növü ayrılmayan hallarda qaytarılan bayt.</summary>
    public byte[] Output { get; set; } = [];

    public byte[] ImageOutput { get; set; } = [];

    public byte[] VideoOutput { get; set; } = [];

    public int Creates => Volatile.Read(ref _creates);

    public IEnumerable<(HttpRequestMessage Request, string Body)> ApiRequests =>
        Requests.Where(r => r.Request.RequestUri!.Host == ApiHost);

    public IEnumerable<(HttpRequestMessage Request, string Body)> CdnRequests =>
        Requests.Where(r => r.Request.RequestUri!.Host != ApiHost);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        lock (Requests)
            Requests.Add((request, body));

        var path = request.RequestUri!.AbsolutePath;

        if (request.RequestUri.Host != ApiHost)
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(FileFor(path)) };

        if (request.Method == HttpMethod.Post)
        {
            var created = Interlocked.Increment(ref _creates);

            if (created == 1 && DropFirstCreate)
                throw new HttpRequestException("connection reset");

            if (CreateFailures.TryDequeue(out var failure))
                return new HttpResponseMessage(failure);

            var kind = path.EndsWith("text_to_image", StringComparison.Ordinal) ? "img" : "vid";

            return Json($$"""{"estimatedCost":{"credits":5},"id":"{{kind}}-{{created}}"}""");
        }

        if (PollFailures.TryDequeue(out var pollFailure))
            return new HttpResponseMessage(pollFailure);

        var taskId = path[(path.LastIndexOf('/') + 1)..];
        var polls = _pollsByTask.AddOrUpdate(taskId, 1, (_, seen) => seen + 1);

        if (polls <= RunningPolls)
            return Json($$"""{"id":"{{taskId}}","status":"RUNNING","progress":0.4}""");

        if (FailureCode is not null)
            return Json($$"""{"id":"{{taskId}}","status":"FAILED","cost":{"credits":0},"failure":"internal diagnostics","failureCode":"{{FailureCode}}"}""");

        return Json($$"""{"id":"{{taskId}}","status":"SUCCEEDED","cost":{"credits":{{Cost}}},"output":["https://{{CdnHost}}/{{taskId}}.bin?_jwt=signed"]}""");
    }

    /// <summary>Hazır fayl: hansı tapşırığın nəticəsidirsə, onun baytları.</summary>
    private byte[] FileFor(string path)
    {
        if (path.Contains("/img-", StringComparison.Ordinal) && ImageOutput.Length > 0)
            return ImageOutput;

        if (path.Contains("/vid-", StringComparison.Ordinal) && VideoOutput.Length > 0)
            return VideoOutput;

        return Output;
    }

    private static HttpResponseMessage Json(string payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };
}

/// <summary>
/// Runway adapterlərinin TAM axını — real cavab formaları ilə, şəbəkəsiz.
///
/// <para>Əvvəlki saxta cavablar yaratma sorğusuna <c>status</c> qaytarırdı, real
/// API isə qaytarmır. Bu fərq canlı sistemdə hər pullu tapşırığı «uğursuz»
/// sayıb atırdı, testlər isə yaşıl qalırdı. Burada adapterlər məhz
/// sənədləşdirilmiş müqaviləyə qarşı yoxlanılır.</para>
/// </summary>
public class PetBrainRunwayProviderTests
{
    private const string ApiKey = "runway-test-key-DO-NOT-LEAK";

    /// <summary>
    /// Rəsmin TAM axını: yarat → izlə → CDN-dən yüklə. Açar və versiya başlığı
    /// yalnız API sorğularındadır, CDN-ə getmir.
    /// </summary>
    [Fact]
    public async Task Sekil_TamAxin_AcarYalnizApiSorgularinda()
    {
        var emulator = new RunwayEmulator { ImageOutput = Png(720, 1280) };
        var spec = MarsScene();

        var result = await ImageProvider(emulator).RenderAsync(spec, SafePuzzleIllustrationPromptBuilder.Build(spec));

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(MediaModelCatalog.Gen4Image, result.Model);
        Assert.True(PuzzleIllustrationValidator.Validate(result.Bytes).IsValid);

        Assert.NotEmpty(emulator.ApiRequests);
        Assert.All(emulator.ApiRequests, r =>
        {
            Assert.Equal("Bearer", r.Request.Headers.Authorization?.Scheme);
            Assert.Equal("2024-11-06", Assert.Single(r.Request.Headers.GetValues("X-Runway-Version")));
        });

        var download = Assert.Single(emulator.CdnRequests);

        Assert.Null(download.Request.Headers.Authorization);
        Assert.False(download.Request.Headers.Contains("X-Runway-Version"));
    }

    /// <summary>
    /// Şəkil sorğusu <c>gen4_image</c> ilə, yalnız mətnlə gedir — referans şəkil
    /// tələb edən <c>gen4_image_turbo</c> ilə yox. Nisbət videonun ilk kadrı ilə
    /// eynidir.
    /// </summary>
    [Fact]
    public async Task Sekil_Gen4ImageIleMetndenGedir()
    {
        var emulator = new RunwayEmulator { ImageOutput = Png(720, 1280) };
        var spec = MarsScene();

        await ImageProvider(emulator).RenderAsync(spec, SafePuzzleIllustrationPromptBuilder.Build(spec));

        var create = Assert.Single(emulator.ApiRequests, r => r.Request.Method == HttpMethod.Post);

        Assert.EndsWith("/v1/text_to_image", create.Request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("\"model\":\"gen4_image\"", create.Body, StringComparison.Ordinal);
        Assert.Contains("\"ratio\":\"720:1280\"", create.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("referenceImages", create.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>429</c> — provayder sorğunu işləmədi, tapşırıq yaranmadı: BİR dəfə
    /// təkrarlanır. Qopan əlaqə isə təkrarlanmır — tapşırıq yaranmış ola bilərdi
    /// və ikinci sorğu ikinci pullu tapşırıq olardı.
    /// </summary>
    [Fact]
    public async Task Sekil_429TekrarlanirQopanElaqeTekrarlanmir()
    {
        var spec = MarsScene();
        var prompt = SafePuzzleIllustrationPromptBuilder.Build(spec);

        var throttled = new RunwayEmulator { ImageOutput = Png(720, 1280) };
        throttled.CreateFailures.Enqueue(HttpStatusCode.TooManyRequests);

        var retried = await ImageProvider(throttled).RenderAsync(spec, prompt);

        Assert.True(retried.Succeeded, retried.Reason);
        Assert.Equal(2, throttled.Creates);

        var dropped = new RunwayEmulator { ImageOutput = Png(720, 1280), DropFirstCreate = true };

        var failed = await ImageProvider(dropped).RenderAsync(spec, prompt);

        Assert.False(failed.Succeeded);
        Assert.Equal("transport", failed.Reason);
        Assert.Equal(1, dropped.Creates);
    }

    /// <summary>İzləmə xətası ödənilmiş tapşırığı ÖLDÜRMÜR — növbəti addımda yenidən soruşulur.</summary>
    [Fact]
    public async Task Sekil_IzlemeXetasiTapsiriqiOldurmur()
    {
        var emulator = new RunwayEmulator { ImageOutput = Png(720, 1280), RunningPolls = 0 };
        emulator.PollFailures.Enqueue(HttpStatusCode.ServiceUnavailable);

        var spec = MarsScene();
        var result = await ImageProvider(emulator).RenderAsync(spec, SafePuzzleIllustrationPromptBuilder.Build(spec));

        Assert.True(result.Succeeded, result.Reason);
    }

    /// <summary>Moderasiya rəddi QISA KODLA qeyd olunur, provayderin mətni isə saxlanmır.</summary>
    [Fact]
    public async Task Sekil_ModerasiyaRaddiKodlaQeydOlunur()
    {
        var emulator = new RunwayEmulator { RunningPolls = 0, FailureCode = "SAFETY.INPUT.TEXT" };

        var spec = MarsScene();
        var result = await ImageProvider(emulator).RenderAsync(spec, SafePuzzleIllustrationPromptBuilder.Build(spec));

        Assert.False(result.Succeeded);
        Assert.Equal("failed:SAFETY.INPUT.TEXT", result.Reason);
        Assert.Empty(emulator.CdnRequests);
    }

    /// <summary>
    /// Referans kadrın tipi BAYTLARDAN oxunur: JPEG rəsm <c>data:image/jpeg</c>
    /// kimi gedir. Sorğu image-to-video, <c>gen4_turbo</c>, portret və 10 saniyədir.
    /// </summary>
    [Fact]
    public async Task Video_ReferansKadrinTipiBaytdanOxunur()
    {
        var emulator = new RunwayEmulator();

        var started = await VideoProvider(emulator).StartAsync(MarsRecap(), "three shots", Jpeg(720, 1280));

        Assert.True(started.Started, started.Reason);
        Assert.Equal("vid-1", started.JobId);

        var create = Assert.Single(emulator.ApiRequests);

        Assert.EndsWith("/v1/image_to_video", create.Request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("\"promptImage\":\"data:image/jpeg;base64,", create.Body, StringComparison.Ordinal);
        Assert.Contains("\"model\":\"gen4_turbo\"", create.Body, StringComparison.Ordinal);
        Assert.Contains("\"ratio\":\"720:1280\"", create.Body, StringComparison.Ordinal);
        Assert.Contains("\"duration\":10", create.Body, StringComparison.Ordinal);
    }

    /// <summary>Pozuq referans kadrla pullu iş BAŞLAMIR.</summary>
    [Fact]
    public async Task Video_PozuqReferansIleIsBaslamir()
    {
        var emulator = new RunwayEmulator();

        var started = await VideoProvider(emulator).StartAsync(
            MarsRecap(), "three shots", "<html>salam</html>"u8.ToArray());

        Assert.False(started.Started);
        Assert.Equal("invalid-reference-image", started.Reason);
        Assert.Empty(emulator.Requests);
    }

    /// <summary>
    /// İzləmə: keçici xəta və işləyən tapşırıq «işləyir» sayılır (ödənilmiş iş
    /// atılmır), hazır tapşırıq CDN-dən başlıqsız yüklənir və HƏQİQİ kredit
    /// tapşırığın özündən oxunur.
    /// </summary>
    [Fact]
    public async Task Video_IzlemeVeYukleme()
    {
        var emulator = new RunwayEmulator { VideoOutput = FakeRecapVideoProvider.Mp4(720, 1280, 10.0), Cost = 50 };
        emulator.PollFailures.Enqueue(HttpStatusCode.BadGateway);

        var provider = VideoProvider(emulator);
        var started = await provider.StartAsync(MarsRecap(), "three shots", Png(720, 1280));

        Assert.Equal(PetBrainRecapProgress.Working, (await provider.PollAsync(started.JobId)).State);
        Assert.Equal(PetBrainRecapProgress.Working, (await provider.PollAsync(started.JobId)).State);

        var ready = await provider.PollAsync(started.JobId);

        Assert.Equal(PetBrainRecapProgress.Ready, ready.State);
        Assert.Equal(50, ready.RealizedCredits);
        Assert.True(RecapVideoValidator.Validate(ready.Bytes).IsValid);

        var download = Assert.Single(emulator.CdnRequests);
        Assert.Null(download.Request.Headers.Authorization);
    }

    private static PetBrainMediaOptions Budget() => new()
    {
        Provider = PetBrainMediaProvider.Runway,
        Profile = PetBrainMediaProfile.Budget,
        ImagePollMilliseconds = 50
    };

    private static RunwayTaskClient Client(RunwayEmulator emulator) =>
        new(
            new HttpClient(emulator),
            Options.Create(new RunwayOptions { ApiKey = ApiKey }),
            NullLogger<RunwayTaskClient>.Instance);

    private static RunwayPuzzleIllustrationProvider ImageProvider(RunwayEmulator emulator)
    {
        var breaker = new MediaCircuitBreaker();

        return new RunwayPuzzleIllustrationProvider(
            Client(emulator),
            new PetBrainMediaCostPolicy(Options.Create(Budget()), breaker),
            breaker,
            TimeProvider.System,
            NullLogger<RunwayPuzzleIllustrationProvider>.Instance);
    }

    private static RunwayRecapVideoProvider VideoProvider(RunwayEmulator emulator)
    {
        var options = Options.Create(Budget());

        return new RunwayRecapVideoProvider(
            Client(emulator),
            new PetBrainMediaCostPolicy(options, new MediaCircuitBreaker()),
            options,
            NullLogger<RunwayRecapVideoProvider>.Instance);
    }

    private static PuzzleSceneSpec MarsScene() =>
        PuzzleSceneSpec.For(
            PuzzleBlueprintCatalog.Find(PuzzleBlueprintCatalog.MarsSignalRouteKey)!,
            "az",
            ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!,
            species: "fox");

    private static AdventureRecapSpec MarsRecap() => new(
        RunId: Guid.NewGuid(),
        ChildProfileId: Guid.NewGuid(),
        ExperienceKey: ExperienceCatalog.MarsRoverRescue,
        SpecVersion: AdventureRecapSpec.CurrentVersion,
        Language: "az",
        DurationSeconds: 10,
        PetSpecies: "fox",
        PetColor: "warm-orange",
        PetCosmetic: "helmet-mars",
        Beats: [new("route", "canyon"), new("rescue", "solar-panel")],
        PuzzleMechanic: nameof(PetBrainPuzzleMechanic.OrderedRoute),
        PuzzleOutcome: "no-hints",
        Environment: "martian-canyon",
        Palette: "warm-orange",
        Mood: "hopeful-adventurous",
        CameraStyle: "gentle-storybook",
        SceneSpecHash: "scene");

    /// <summary>Minimal PNG: imza + IHDR ölçüləri — yoxlayıcı ölçünü buradan oxuyur.</summary>
    internal static byte[] Png(int width, int height)
    {
        var bytes = new byte[64];

        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);

        WriteBigEndian(bytes, 16, width);
        WriteBigEndian(bytes, 20, height);

        return bytes;
    }

    /// <summary>Minimal JPEG: SOI + SOF0 başlığı — yoxlayıcı ölçünü buradan oxuyur.</summary>
    private static byte[] Jpeg(int width, int height)
    {
        var bytes = new byte[32];

        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        bytes[3] = 0xC0;
        bytes[4] = 0x00;
        bytes[5] = 0x11;
        bytes[6] = 0x08;
        bytes[7] = (byte)(height >> 8);
        bytes[8] = (byte)height;
        bytes[9] = (byte)(width >> 8);
        bytes[10] = (byte)width;

        return bytes;
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
