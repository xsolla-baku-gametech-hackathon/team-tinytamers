using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PetPal.Api.PetBrain.Media;

namespace PetPal.Tests;

/// <summary>
/// Sorğuları TUTAN saxta nəqliyyat — şəbəkəyə çıxmır.
///
/// <para>Nə göndərdiyimizi yoxlamaq üçün lazımdır: başlıqlar, gövdə və
/// <b>açarın heç bir yerə düşməməsi</b>.</para>
/// </summary>
public sealed class CapturingHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        Bodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken));

        return _respond(request);
    }
}

/// <summary>
/// Runway adapterinin sorğu müqaviləsi.
///
/// <para>Bu testlər şəbəkəsiz işləyir və məhz o şeyləri qoruyur ki, onlar
/// yalnız canlı sistemdə görünərdi: versiya başlığı, marşrut, gövdə sahələri
/// və açarın gizli qalması.</para>
/// </summary>
public class PetBrainRunwayClientTests
{
    private const string ApiKey = "runway-test-key-DO-NOT-LEAK";

    /// <summary>
    /// Versiya başlığı MƏCBURİDİR və açar YALNIZ Authorization-dadır (10C).
    /// </summary>
    [Fact]
    public async Task Sorgu_VersiyaBasligiDasiyir_AcarSizmir()
    {
        var handler = new CapturingHandler(_ => Json("""{"id":"task-1","status":"PENDING"}"""));
        var client = NewClient(handler);

        await client.CreateImageAsync(MediaModelCatalog.Gen4Image, "safe prompt", "720:1280");

        var request = Assert.Single(handler.Requests);

        Assert.Equal("2024-11-06", Assert.Single(request.Headers.GetValues("X-Runway-Version")));
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);

        // Açar GÖVDƏYƏ düşmür — seriallaşdırılan obyektdə belə sahə yoxdur.
        Assert.DoesNotContain(ApiKey, Assert.Single(handler.Bodies), StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, request.RequestUri!.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Şəkil sorğusu sənədləşdirilmiş marşruta və sahələrə gedir.</summary>
    [Fact]
    public async Task Sekil_DuzgunMarsrutaVeSaheləreGedir()
    {
        var handler = new CapturingHandler(_ => Json("""{"id":"task-2","status":"PENDING"}"""));
        var client = NewClient(handler);

        await client.CreateImageAsync(MediaModelCatalog.Gen4Image, "a warm canyon", "720:1280");

        Assert.EndsWith("/text_to_image", handler.Requests[0].RequestUri!.AbsolutePath, StringComparison.Ordinal);

        var body = handler.Bodies[0];

        Assert.Contains("\"model\":\"gen4_image\"", body, StringComparison.Ordinal);
        Assert.Contains("\"promptText\":\"a warm canyon\"", body, StringComparison.Ordinal);
        Assert.Contains("\"ratio\":\"720:1280\"", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Video sorğusu image-to-video-dur: referans kadr, portret nisbət və
    /// 10 saniyə.
    /// </summary>
    [Fact]
    public async Task Video_ReferansKadrIleGedir()
    {
        var handler = new CapturingHandler(_ => Json("""{"id":"task-3","estimatedCost":{"credits":50}}"""));
        var client = NewClient(handler);

        var task = await client.CreateVideoAsync(
            MediaModelCatalog.Gen4Turbo, "three shots", "data:image/png;base64,AAAA", "720:1280", 10);

        Assert.EndsWith("/image_to_video", handler.Requests[0].RequestUri!.AbsolutePath, StringComparison.Ordinal);

        var body = handler.Bodies[0];

        Assert.Contains("\"model\":\"gen4_turbo\"", body, StringComparison.Ordinal);
        Assert.Contains("\"promptImage\":\"data:image/png;base64,AAAA\"", body, StringComparison.Ordinal);
        Assert.Contains("\"ratio\":\"720:1280\"", body, StringComparison.Ordinal);
        Assert.Contains("\"duration\":10", body, StringComparison.Ordinal);

        Assert.Equal(RunwayTaskState.Pending, task.State);
        Assert.Equal("task-3", task.Id);
    }

    /// <summary>
    /// Naməlum vəziyyət UĞURSUZ sayılır (fail closed) — «bəlkə hazırdır»
    /// fərziyyəsi ilə pozuq fayl saxlamaqdansa ehtiyata düşmək düzgündür.
    /// </summary>
    [Theory]
    [InlineData("""{"id":"t","status":"SUCCEEDED","output":["https://cdn.example/x.mp4"]}""", RunwayTaskState.Succeeded)]
    [InlineData("""{"id":"t","status":"PENDING"}""", RunwayTaskState.Pending)]
    [InlineData("""{"id":"t","status":"FAILED"}""", RunwayTaskState.Failed)]
    [InlineData("""{"id":"t","status":"SOMETHING_NEW"}""", RunwayTaskState.Failed)]
    public async Task Veziyyet_QapaliSekildeOxunur(string payload, RunwayTaskState expected)
    {
        var client = NewClient(new CapturingHandler(_ => Json(payload)));

        Assert.Equal(expected, (await client.PollAsync("t")).State);
    }

    /// <summary>«Uğurlu, amma fayl yoxdur» UĞUR SAYILMIR.</summary>
    [Fact]
    public async Task BosCixis_UgurSayilmir()
    {
        var client = NewClient(new CapturingHandler(_ =>
            Json("""{"id":"t","status":"SUCCEEDED","output":[]}""")));

        var task = await client.PollAsync("t");

        Assert.Equal(RunwayTaskState.Failed, task.State);
        Assert.Equal("empty-output", task.FailureReason);
    }

    /// <summary>HTTP xətasının gövdəsi OXUNMUR — diaqnostika loga sızmır.</summary>
    [Fact]
    public async Task HttpXetasi_SebebKimiKodQaytarir()
    {
        var client = NewClient(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{\"secret\":\"" + ApiKey + "\"}", Encoding.UTF8, "application/json")
        }));

        var task = await client.CreateImageAsync(MediaModelCatalog.Gen4Image, "p", "720:1280");

        Assert.Equal(RunwayTaskState.Failed, task.State);
        Assert.Equal("http-429", task.FailureReason);
        Assert.DoesNotContain(ApiKey, task.FailureReason, StringComparison.Ordinal);
    }

    /// <summary>Açar yoxdursa heç bir sorğu GETMİR.</summary>
    [Fact]
    public async Task AcarYoxdursa_SorguGetmir()
    {
        var handler = new CapturingHandler(_ => Json("""{"id":"t","status":"PENDING"}"""));
        var client = NewClient(handler, apiKey: string.Empty);

        Assert.False(client.IsConfigured);

        var task = await client.CreateImageAsync(MediaModelCatalog.Gen4Image, "p", "720:1280");

        Assert.Equal("not-configured", task.FailureReason);
        Assert.Empty(handler.Requests);
    }

    /// <summary>
    /// HTTPS olmayan ünvandan fayl YÜKLƏNMİR — gözlənilməz host uşağın faylı
    /// kimi saxlanmamalıdır.
    /// </summary>
    [Theory]
    [InlineData("http://cdn.example/x.mp4")]
    [InlineData("file:///C:/windows/win.ini")]
    [InlineData("not-a-url")]
    public async Task TehlukesizOlmayanUnvan_YuklenmirAsync(string url)
    {
        var handler = new CapturingHandler(_ => Json("{}"));
        var client = NewClient(handler);

        Assert.Null(await client.DownloadAsync(url, 1024));
        Assert.Empty(handler.Requests);
    }

    /// <summary>Elan olunmuş ölçü həddi aşırsa fayl HEÇ yüklənmir.</summary>
    [Fact]
    public async Task HeddenBoyukFayl_Yuklenmir()
    {
        var big = new byte[4096];

        var client = NewClient(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(big)
        }));

        Assert.Null(await client.DownloadAsync("https://cdn.example/x.mp4", maxBytes: 1024));
    }

    /// <summary>
    /// Real API-nin yaratma cavabı YALNIZ id və təxmini xərc daşıyır — status
    /// yoxdur. Bu cavab «gözləyir» sayılmalıdır: tapşırıq artıq yaradılıb və
    /// pulludur, onu «uğursuz» saymaq ödənilmiş nəticəni atmaq olardı.
    /// </summary>
    [Fact]
    public async Task YaratmaCavabi_StatussuzdurVeTapsiriqGozleyir()
    {
        var client = NewClient(new CapturingHandler(_ => Json("""{"id":"task-9","estimatedCost":{"credits":5}}""")));

        var task = await client.CreateImageAsync(MediaModelCatalog.Gen4Image, "a calm canyon", "720:1280");

        Assert.Equal(RunwayTaskState.Pending, task.State);
        Assert.Equal("task-9", task.Id);
    }

    /// <summary>Id-siz yaratma cavabı uğur SAYILMIR — izləniləsi tapşırıq yoxdur.</summary>
    [Fact]
    public async Task IdsizYaratmaCavabi_UgursuzSayilir()
    {
        var client = NewClient(new CapturingHandler(_ => Json("""{"estimatedCost":{"credits":5}}""")));

        var task = await client.CreateImageAsync(MediaModelCatalog.Gen4Image, "p", "720:1280");

        Assert.Equal(RunwayTaskState.Failed, task.State);
        Assert.Equal("no-task-id", task.FailureReason);
    }

    /// <summary>
    /// Hazır faylın yüklənməsi provayderin CDN-inə gedir — ora nə açar, nə də
    /// versiya başlığı getməməlidir.
    /// </summary>
    [Fact]
    public async Task Yukleme_AcarVeVersiyaBasligiDasimir()
    {
        var handler = new CapturingHandler(request => request.RequestUri!.Host == "api.dev.runwayml.com"
            ? Json("""{"id":"t"}""")
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });

        var client = NewClient(handler);

        await client.CreateImageAsync(MediaModelCatalog.Gen4Image, "p", "720:1280");
        var bytes = await client.DownloadAsync("https://dnznrvs05pmza.cloudfront.net/x.png?_jwt=signed", 1024);

        Assert.Equal([1, 2, 3], bytes);

        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);

        var download = handler.Requests[1];

        Assert.Null(download.Headers.Authorization);
        Assert.False(download.Headers.Contains("X-Runway-Version"));
    }

    /// <summary>
    /// Uğursuz tapşırıq provayderin QISA KODU ilə qeyd olunur — mətni
    /// (diaqnostika) saxlanmır.
    /// </summary>
    [Theory]
    [InlineData("""{"id":"t","status":"FAILED","failure":"secret internals","failureCode":"SAFETY.INPUT.TEXT"}""", "failed:SAFETY.INPUT.TEXT")]
    [InlineData("""{"id":"t","status":"FAILED","failure":"secret internals"}""", "failed")]
    [InlineData("""{"id":"t","status":"CANCELLED"}""", "cancelled")]
    [InlineData("""{"id":"t","status":"SOMETHING_NEW"}""", "unknown-status")]
    public async Task UgursuzTapsiriq_QisaKodlaQeydOlunur(string payload, string expected)
    {
        var task = await NewClient(new CapturingHandler(_ => Json(payload))).PollAsync("t");

        Assert.Equal(RunwayTaskState.Failed, task.State);
        Assert.Equal(expected, task.FailureReason);
    }

    /// <summary>Provayderin kodu təmizlənir və səbəb sütununa (60 simvol) sığır.</summary>
    [Fact]
    public async Task UzunXetaKodu_TemizlenirVeSutunaSigir()
    {
        var code = new string('A', 200) + "<script>";

        var task = await NewClient(new CapturingHandler(_ =>
            Json($$"""{"id":"t","status":"FAILED","failureCode":"{{code}}"}"""))).PollAsync("t");

        Assert.True(task.FailureReason.Length <= 60, task.FailureReason);
        Assert.DoesNotContain("<", task.FailureReason, StringComparison.Ordinal);
    }

    /// <summary>Provayderin bildirdiyi HƏQİQİ kredit oxunur — dövrə kəsicisi ona baxır.</summary>
    [Fact]
    public async Task HeqiqiKredit_TapsiriqdanOxunur()
    {
        var task = await NewClient(new CapturingHandler(_ =>
            Json("""{"id":"t","status":"SUCCEEDED","output":["https://cdn.example/x.mp4"],"cost":50}"""))).PollAsync("t");

        Assert.Equal(RunwayTaskState.Succeeded, task.State);
        Assert.Equal(50, task.Cost);
    }

    /// <summary>Runway-in 1000 simvolluq həddini aşan prompt GÖNDƏRİLMİR.</summary>
    [Fact]
    public async Task UzunPrompt_SorguGetmir()
    {
        var handler = new CapturingHandler(_ => Json("""{"id":"t"}"""));

        var task = await NewClient(handler).CreateImageAsync(
            MediaModelCatalog.Gen4Image, new string('a', RunwayTaskClient.MaxPromptLength + 1), "720:1280");

        Assert.Equal("prompt-length", task.FailureReason);
        Assert.Empty(handler.Requests);
    }

    private static RunwayTaskClient NewClient(CapturingHandler handler, string apiKey = ApiKey) =>
        new(
            new HttpClient(handler),
            Options.Create(new RunwayOptions
            {
                BaseUrl = "https://api.dev.runwayml.com/v1",
                ApiVersion = "2024-11-06",
                ApiKey = apiKey
            }),
            NullLogger<RunwayTaskClient>.Instance);

    private static HttpResponseMessage Json(string payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };
}
