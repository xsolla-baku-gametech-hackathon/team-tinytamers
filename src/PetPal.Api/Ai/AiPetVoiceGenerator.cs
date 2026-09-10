using System.Net.Http.Headers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace PetPal.Api.Ai;

/// <summary>
/// OpenAI-uyğun <c>/chat/completions</c> endpoint-i ilə pet replikası yaradır
/// (Groq, Ollama, LM Studio, vLLM və ya OpenAI-nin özü).
///
/// Üç qayda bu sinfin bütün dizaynını müəyyən edir:
///
/// 1. <b>Heç vaxt sındırmır.</b> Şəbəkə xətası, timeout, boş cavab — hamısında
///    qayda əsaslı replikaya qayıdır. Uşaq ekranı AI-dan asılı deyil.
/// 2. <b>Heç vaxt gözlətmir.</b> Timeout qısadır; model ləng cavab verirsə
///    replika qayda əsaslı olur.
/// 3. <b>Uşağın mətnini modelə vermir.</b> Prompt yalnız strukturlaşdırılmış
///    vəziyyətdən yığılır — bax <see cref="PetVoicePrompt"/>.
///
/// <para>Üçüncü qayda YALNIZ salamlama replikasına aiddir. Söhbət özəlliyi
/// (<see cref="PetChatService"/>) uşağın mətnini qəsdən modelə verir və buna görə
/// ayrıca filtr qatı daşıyır — bax <see cref="ChatGuard"/>.</para>
/// </summary>
public sealed class AiPetVoiceGenerator : IPetVoiceGenerator
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly IMemoryCache _cache;
    private readonly IPetVoiceGenerator _fallback;
    private readonly ILogger<AiPetVoiceGenerator> _logger;

    public AiPetVoiceGenerator(
        HttpClient http,
        IOptions<AiOptions> options,
        IMemoryCache cache,
        ILogger<AiPetVoiceGenerator> logger)
    {
        _options = options.Value;
        _http = http;
        _cache = cache;
        _logger = logger;
        _fallback = new RuleBasedPetVoiceGenerator();

        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds));

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public async Task<string> IdleAsync(PetVoiceContext context, CancellationToken ct = default)
    {
        if (!_options.IsEnabled)
            return await _fallback.IdleAsync(context, ct);

        // Eyni vəziyyət qısa müddətdə təkrarlanır (uşaq ekranlar arasında gedib-gəlir).
        // Keş həm modeli yükləmir, həm də replikanın hər saniyə dəyişməsinin
        // qarşısını alır — bu, uşaq üçün narahatedici olardı.
        var cacheKey = CacheKey(context);
        if (_cache.TryGetValue<string>(cacheKey, out var cached) && !string.IsNullOrWhiteSpace(cached))
            return cached!;

        var raw = await OpenAiChat.CompleteAsync(
            _http,
            _options.Model,
            [
                OpenAiChat.Message.System(PetVoicePrompt.SystemPrompt(context.Language)),
                OpenAiChat.Message.User(PetVoicePrompt.UserPrompt(context))
            ],
            _options.MaxOutputTokens,
            temperature: 0.8,
            _logger,
            ct,
            _options.ReasoningEffort);

        var generated = PetVoicePrompt.Sanitize(raw);
        if (generated is null)
            return await _fallback.IdleAsync(context, ct);

        _cache.Set(cacheKey, generated, TimeSpan.FromMinutes(Math.Max(1, _options.CacheMinutes)));
        return generated;
    }

    private static string CacheKey(PetVoiceContext c) =>
        $"petvoice:{c.PetName}:{c.Language}:{c.Mood}:{c.Level}:{c.StreakDays}:{c.GoalCompleted}/{c.GoalTarget}";
}
