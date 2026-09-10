using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PetPal.Api.Ai;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

public class PetVoiceAiTests
{
    private static PetVoiceContext Context(string language = "az") => new(
        Language: language,
        ChildName: "Ayan",
        PetName: "Max",
        Mood: PetMood.Happy,
        Level: 4,
        Happiness: 80,
        Energy: 70,
        Fullness: 60,
        Cleanliness: 90,
        StreakDays: 3,
        GoalCompleted: 2,
        GoalTarget: 5);

    // ---------- Təmizləmə ----------

    [Theory]
    [InlineData("\"Salam, Ayan!\"", "Salam, Ayan!")]
    [InlineData("  Salam!  ", "Salam!")]
    [InlineData("Salam!\nƏlavə izahat burada.", "Salam!")]
    [InlineData("«Necəsən?»", "Necəsən?")]
    public void Sanitize_ModelCavabiniTemizleyir(string raw, string expected)
    {
        Assert.Equal(expected, PetVoicePrompt.Sanitize(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Sanitize_BosCavabdaNullQaytarir(string? raw)
    {
        Assert.Null(PetVoicePrompt.Sanitize(raw));
    }

    [Fact]
    public void Sanitize_HeddindenUzunMetniKesir()
    {
        var raw = new string('a', PetVoicePrompt.MaxReplyLength + 50);

        var result = PetVoicePrompt.Sanitize(raw);

        Assert.NotNull(result);
        Assert.True(result!.Length <= PetVoicePrompt.MaxReplyLength);
    }

    [Fact]
    public void SanitizeName_IdareEdiciSimvollariAtirVeUzunluguMehdudlasdirir()
    {
        Assert.Equal("Ayan", PetVoicePrompt.SanitizeName("Ay\nan"));
        Assert.Equal("?", PetVoicePrompt.SanitizeName("   "));
        Assert.True(PetVoicePrompt.SanitizeName(new string('x', 100)).Length <= 24);
    }

    // ---------- Prompt ----------

    [Fact]
    public void SystemPrompt_DileGoreDeyisir()
    {
        Assert.Contains("Azərbaycan dilində", PetVoicePrompt.SystemPrompt("az"));
        Assert.Contains("English only", PetVoicePrompt.SystemPrompt("en"));
    }

    [Fact]
    public void UserPrompt_YalnizStrukturlasdirilmisVeziyyetDasiyir()
    {
        var prompt = PetVoicePrompt.UserPrompt(Context());

        Assert.Contains("Ayan", prompt);
        Assert.Contains("Max", prompt);
        Assert.Contains("şən", prompt);
        Assert.Contains("2/5", prompt);
    }

    // ---------- Qayda əsaslı ----------

    [Fact]
    public async Task RuleBased_HemiseMetnQaytarir()
    {
        var generator = new RuleBasedPetVoiceGenerator();

        var message = await generator.IdleAsync(Context());

        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    // ---------- AI: nasazlıqda geri düşmə ----------

    [Fact]
    public async Task Ai_SonduruldukdeQaydaEsasliCavabQaytarir()
    {
        var generator = Build(new AiOptions { Provider = AiProvider.None }, _ => throw new HttpRequestException("çağırılmamalıdır"));

        var message = await generator.IdleAsync(Context());

        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    /// <summary>
    /// Ən vacib test: model serveri işləmirsə uşaq bunu hiss etməməlidir.
    /// </summary>
    [Fact]
    public async Task Ai_ServerCavabVermirseAppSinmir()
    {
        var generator = Build(EnabledOptions(), _ => throw new HttpRequestException("qoşulma alınmadı"));

        var message = await generator.IdleAsync(Context());

        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public async Task Ai_ServerXetaQaytarirsaQaydaEsasliyaKecir()
    {
        var generator = Build(EnabledOptions(), _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var message = await generator.IdleAsync(Context());

        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public async Task Ai_BosCavabdaQaydaEsasliyaKecir()
    {
        var generator = Build(EnabledOptions(), _ => Json("""{"choices":[{"message":{"content":"   "}}]}"""));

        var message = await generator.IdleAsync(Context());

        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public async Task Ai_UgurluCavabTemizlenmisSekildeIstifadeOlunur()
    {
        var generator = Build(EnabledOptions(), _ => Json("""{"choices":[{"message":{"content":"\"Bu gün əla gedirsən!\""}}]}"""));

        var message = await generator.IdleAsync(Context());

        Assert.Equal("Bu gün əla gedirsən!", message);
    }

    [Fact]
    public async Task Ai_EyniVeziyyetUcunModelYalnizBirDefeCagirilir()
    {
        var calls = 0;
        var generator = Build(EnabledOptions(), _ =>
        {
            calls++;
            return Json("""{"choices":[{"message":{"content":"Salam!"}}]}""");
        });

        await generator.IdleAsync(Context());
        await generator.IdleAsync(Context());

        Assert.Equal(1, calls);
    }

    // ---------- Köməkçilər ----------

    private static AiOptions EnabledOptions() => new()
    {
        Provider = AiProvider.OpenAiCompatible,
        BaseUrl = "http://localhost:11434/v1",
        Model = "test-model",
        TimeoutSeconds = 2
    };

    private static AiPetVoiceGenerator Build(AiOptions options, Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new StubHandler(respond)),
            Options.Create(options),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<AiPetVoiceGenerator>.Instance);

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
