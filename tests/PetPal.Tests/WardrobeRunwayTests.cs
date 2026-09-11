using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PetPal.Api.Ai;
using PetPal.Api.PetBrain.Media;
using PetPal.Api.Wardrobe;
using PetPal.Shared.Dtos.Wardrobe;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>SAXTA Runway klienti — sorğunu tutur, tapşırığı ssenari ilə bitirir, pul xərcləmir.</summary>
public sealed class FakeRunwayTaskClient : IRunwayTaskClient
{
    public bool IsConfigured { get; set; } = true;

    public List<RunwayTextToImageRequest> Requests { get; } = [];

    /// <summary>İzləmənin qaytardığı yekun — standart olaraq uğurlu, 6 kredit.</summary>
    public RunwayTask Finished { get; set; } =
        new("task-1", RunwayTaskState.Succeeded, "https://cdn.example.test/out.png", string.Empty, 6);

    public byte[]? Download { get; set; } = WardrobeTestImages.Portrait(1088, 1920);

    public Task<RunwayTask> CreateImageAsync(string model, string prompt, string ratio, CancellationToken ct = default) =>
        Task.FromResult(RunwayTask.Failed("unused"));

    public Task<RunwayTask> CreateVideoAsync(
        string model, string prompt, string promptImageDataUri, string ratio, int durationSeconds,
        CancellationToken ct = default) =>
        Task.FromResult(RunwayTask.Failed("unused"));

    public Task<RunwayTask> CreateTextToImageAsync(RunwayTextToImageRequest request, CancellationToken ct = default)
    {
        Requests.Add(request);
        return Task.FromResult(new RunwayTask("task-1", RunwayTaskState.Pending, null, string.Empty));
    }

    public Task<RunwayTask> PollAsync(string taskId, CancellationToken ct = default) => Task.FromResult(Finished);

    public Task<byte[]?> DownloadAsync(string url, int maxBytes, CancellationToken ct = default) =>
        Task.FromResult(Download);
}

/// <summary>
/// Dizayn studiyasının Runway yolu: eyni GPT Image 2.5 Flare modeli, mövcud
/// Runway açarı ilə.
///
/// <para>Yoxlanan şey pulun hara getdiyidir: xərc tavanı və kəsici pullu işi
/// başlamadan dayandırırmı, Runway-in təhlükəsizlik rəddi texniki xətadan
/// ayrılırmı, sorğu modelin gözlədiyi sahələrlə gedirmi.</para>
/// </summary>
public class WardrobeRunwayTests
{
    [Fact]
    public void Konfiqurasiya_RunwayYolu_TehlukesizStandartlarlaQurulur()
    {
        var options = new WardrobeOptions
        {
            Provider = WardrobeProvider.Runway,
            Quality = "auto",
            RunwayRatio = "999:1"
        };

        Assert.True(options.UsesRunway);
        Assert.True(options.IsEnabled);
        Assert.False(options.UsesOpenAi);
        Assert.Equal("gpt_image_2_5_flare", options.EffectiveRunwayModel);
        Assert.Equal("1088:1920", options.EffectiveRunwayRatio);
        Assert.Equal("medium", options.EffectiveQuality);
        Assert.Equal(6, options.RunwayCreditsFor(withReference: true));
        Assert.Equal(5, options.RunwayCreditsFor(withReference: false));
        Assert.Empty(options.EffectiveModerationKey);

        Assert.Equal("moderation-only",
            new WardrobeOptions { Provider = WardrobeProvider.Runway, ModerationApiKey = "moderation-only" }
                .EffectiveModerationKey);
    }

    [Fact]
    public async Task RunwayYolu_FlareModeli_IstinadSekliIleGedir()
    {
        var client = new FakeRunwayTaskClient();

        var result = await Provider(client).EditAsync("dress the pet", WardrobeTestImages.Portrait(), "image/png");

        Assert.True(result.Succeeded);

        var request = Assert.Single(client.Requests);

        Assert.Equal("gpt_image_2_5_flare", request.Model);
        Assert.Equal("1088:1920", request.Ratio);
        Assert.Equal("medium", request.Quality);
        Assert.StartsWith("data:image/png;base64,", Assert.Single(request.ReferenceImageDataUris!), StringComparison.Ordinal);
        Assert.True(request.MaxPromptLength > RunwayTaskClient.MaxPromptLength);
    }

    [Fact]
    public async Task RunwayYolu_BazaPortreti_IstinadsizGedir()
    {
        var client = new FakeRunwayTaskClient
        {
            Finished = new RunwayTask("task-1", RunwayTaskState.Succeeded, "https://cdn.example.test/base.png", string.Empty, 5)
        };

        var result = await Provider(client).GenerateAsync("base portrait");

        Assert.True(result.Succeeded);
        Assert.Null(Assert.Single(client.Requests).ReferenceImageDataUris);
    }

    [Theory]
    [InlineData("failed:SAFETY.INPUT.TEXT", true)]
    [InlineData("failed:SAFETY.OUTPUT.IMAGE", true)]
    [InlineData("failed:INPUT_PREPROCESSING.SAFETY.TEXT", true)]
    [InlineData("failed:INTERNAL.BAD_OUTPUT.CODE01", false)]
    [InlineData("deadline", false)]
    public async Task RunwayYolu_TehlukesizlikReddini_TexnikiXetadanAyirir(string reason, bool refused)
    {
        var client = new FakeRunwayTaskClient
        {
            Finished = new RunwayTask("task-1", RunwayTaskState.Failed, null, reason)
        };

        var result = await Provider(client).GenerateAsync("anything");

        Assert.False(result.Succeeded);
        Assert.Equal(refused, result.Refused);
    }

    /// <summary>Bahalı keyfiyyət kredit tavanını keçir — pullu iş ümumiyyətlə başlamır.</summary>
    [Fact]
    public async Task RunwayYolu_BahaliKeyfiyyet_TavaniKecir_SorguGetmir()
    {
        var client = new FakeRunwayTaskClient();

        var result = await Provider(client, options => options.Quality = "high")
            .EditAsync("dress the pet", WardrobeTestImages.Portrait(), "image/png");

        Assert.Equal("over-credit-cap", result.Reason);
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task RunwayYolu_XercKesicisiAciqdirsa_SorguGetmir()
    {
        var client = new FakeRunwayTaskClient();
        var breaker = new MediaCircuitBreaker();
        breaker.Open("test");

        var result = await Provider(client, breaker: breaker).GenerateAsync("anything");

        Assert.Equal("circuit-open", result.Reason);
        Assert.Empty(client.Requests);
    }

    /// <summary>Provayder razılaşdırılandan baha hesablasa yeni pullu işlər dayanır.</summary>
    [Fact]
    public async Task RunwayYolu_BahaHesablanarsa_XercKesicisiAcilir()
    {
        var client = new FakeRunwayTaskClient
        {
            Finished = new RunwayTask("task-1", RunwayTaskState.Succeeded, "https://cdn.example.test/out.png", string.Empty, 40)
        };
        var breaker = new MediaCircuitBreaker();

        await Provider(client, breaker: breaker).GenerateAsync("anything");

        Assert.True(breaker.IsOpen);
    }

    [Fact]
    public void RunwayYolu_AcarYoxdursa_Sonuludur() =>
        Assert.False(Provider(new FakeRunwayTaskClient { IsConfigured = false }).IsEnabled);

    [Fact]
    public async Task RunwayKlienti_MetndenSekil_KeyfiyyetVeIstinadiGonderir_UzunPromptaIcazeVerir()
    {
        var handler = new CapturingHandler(_ => Json("{\"id\":\"task-9\"}"));
        var client = RunwayClient(handler);

        var created = await client.CreateTextToImageAsync(new RunwayTextToImageRequest(
            "gpt_image_2_5_flare", new string('a', 1500), "1088:1920", "medium", ["data:image/png;base64,AAAA"], 4000));

        Assert.Equal(RunwayTaskState.Pending, created.State);
        Assert.EndsWith("/text_to_image", handler.Requests[0].RequestUri!.AbsolutePath, StringComparison.Ordinal);

        var body = handler.Bodies[0];

        Assert.Contains("\"model\":\"gpt_image_2_5_flare\"", body, StringComparison.Ordinal);
        Assert.Contains("\"ratio\":\"1088:1920\"", body, StringComparison.Ordinal);
        Assert.Contains("\"quality\":\"medium\"", body, StringComparison.Ordinal);
        Assert.Contains("\"referenceImages\":[{\"uri\":\"data:image/png;base64,AAAA\"}]", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunwayKlienti_BosSaheleriGondermir_StandartPromptHeddiQalir()
    {
        var handler = new CapturingHandler(_ => Json("{\"id\":\"task-10\"}"));
        var client = RunwayClient(handler);

        await client.CreateTextToImageAsync(new RunwayTextToImageRequest("gen4_image", "a calm meadow", "720:1280"));

        Assert.DoesNotContain("quality", handler.Bodies[0], StringComparison.Ordinal);
        Assert.DoesNotContain("referenceImages", handler.Bodies[0], StringComparison.Ordinal);

        var tooLong = await client.CreateTextToImageAsync(
            new RunwayTextToImageRequest("gen4_image", new string('a', 1500), "720:1280"));

        Assert.Equal("prompt-length", tooLong.FailureReason);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("SAFE", ModerationVerdict.Allowed)]
    [InlineData(" Safe! ", ModerationVerdict.Allowed)]
    [InlineData("UNSAFE", ModerationVerdict.Flagged)]
    [InlineData("unsafe.", ModerationVerdict.Flagged)]
    [InlineData("I think it is safe", ModerationVerdict.Unavailable)]
    [InlineData("", ModerationVerdict.Unavailable)]
    [InlineData(null, ModerationVerdict.Unavailable)]
    public void CatModeli_HokmuYalnizBirSozdenOxuyur(string? reply, ModerationVerdict expected) =>
        Assert.Equal(expected, ChatModelWardrobeModeration.Verdict(reply));

    [Fact]
    public async Task CatModeli_Moderasiyasi_CatEndpointineGedir()
    {
        var handler = new CapturingHandler(_ =>
            Json("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"UNSAFE\"}}]}"));

        var moderation = new ChatModelWardrobeModeration(
            new HttpClient(handler),
            Options.Create(new AiOptions
            {
                Provider = AiProvider.OpenAiCompatible,
                BaseUrl = "https://chat.example.test/v1",
                ApiKey = "chat-key",
                Model = "guard-model"
            }),
            Options.Create(new WardrobeOptions()),
            NullLogger<ChatModelWardrobeModeration>.Instance);

        Assert.Equal(ModerationVerdict.Flagged, await moderation.CheckAsync("qırmızı pelerin"));
        Assert.EndsWith("/chat/completions", handler.Requests[0].RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("guard-model", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("pelerin", handler.Bodies[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// Mənaca ön yoxlama qurulmayıbsa dizayn DAVAM EDİR — bu, yerləşdirmənin
    /// açıq qərarıdır, «yoxlama sıradan çıxdı» ilə qarışdırılmır.
    /// </summary>
    [Fact]
    public async Task OnYoxlamaQurulmayanda_DizaynCekilir()
    {
        using var factory = new WardrobeFactory();
        factory.Moderation.Verdict = ModerationVerdict.NotConfigured;

        var client = await ApiTestClient.CreateAsync(factory, "wardrobe-no-precheck@petpal.test", "Aylin");
        await client.HatchAsync(factory);

        client.SwitchToParent();
        (await client.Http.PutAsJsonAsync(
            $"/api/parent/children/{client.ChildId}/wardrobe", new WardrobeSettingsRequest { Enabled = true }))
            .EnsureSuccessStatusCode();
        client.SwitchToChild();

        var response = await client.Http.PostAsJsonAsync(
            "/api/pet/wardrobe/designs", new CreateWardrobeDesignRequest { Text = "qırmızı pelerin" });
        response.EnsureSuccessStatusCode();

        var design = (await response.Content.ReadFromJsonAsync<WardrobeDesignDto>())!;

        for (var attempt = 0; attempt < 200 && design.Status == WardrobeDesignStatus.Pending; attempt++)
        {
            await Task.Delay(25);

            var state = await client.Http.GetFromJsonAsync<WardrobeStateDto>("/api/pet/wardrobe");
            design = state!.Designs.First(d => d.Id == design.Id);
        }

        Assert.Equal(WardrobeDesignStatus.Ready, design.Status);
        Assert.Equal(1, factory.Moderation.Calls);
    }

    private static RunwayWardrobeImageProvider Provider(
        FakeRunwayTaskClient client,
        Action<WardrobeOptions>? configure = null,
        MediaCircuitBreaker? breaker = null)
    {
        var options = new WardrobeOptions { Provider = WardrobeProvider.Runway, PollMilliseconds = 50 };
        configure?.Invoke(options);

        return new RunwayWardrobeImageProvider(
            client,
            Options.Create(options),
            breaker ?? new MediaCircuitBreaker(),
            TimeProvider.System,
            NullLogger<RunwayWardrobeImageProvider>.Instance);
    }

    private static RunwayTaskClient RunwayClient(CapturingHandler handler) =>
        new(new HttpClient(handler),
            Options.Create(new RunwayOptions { ApiKey = "runway-test-key" }),
            NullLogger<RunwayTaskClient>.Instance);

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
