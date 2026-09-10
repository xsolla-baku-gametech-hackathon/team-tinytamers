using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PetPal.Api.Ai;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// OpenAI-uyğun şəkil endpoint-inə (<c>/images/generations</c>) sorğu atan provayder.
///
/// <para>Modelə gedən yeganə şey <see cref="SafePuzzleIllustrationPromptBuilder"/>
/// -in qurduğu promptdur — orada nə uşaq adı, nə id, nə xatirə mətni, nə də
/// tapmacanın həlli var.</para>
///
/// <para>Gələn cavab base64 baytlardır. <b>Xarici URL qəbul edilmir</b>: link
/// qaytaran cavab rədd olunur, çünki o, uşağın cihazını naməlum domenə
/// yönəldərdi. Bayt bizim tərəfə köçürülür və sahiblik yoxlanan endpoint-dən
/// paylanır.</para>
///
/// <para>Hər uğursuzluq — timeout, HTTP xətası, pozuq JSON, boş sahə, həddindən
/// böyük fayl — səssizcə deterministik səhnəyə qayıdır.</para>
/// </summary>
public sealed class AiPuzzleIllustrationProvider : IPuzzleIllustrationProvider
{
    /// <summary>Base64 mətninin yuxarı həddi — bayt həddinin ~4/3 qarşılığı.</summary>
    private const int MaxBase64Length = (PuzzleIllustrationValidator.MaxBytes / 3 * 4) + 1024;

    private readonly HttpClient _http;
    private readonly AiOptions _ai;
    private readonly PetBrainOptions _options;
    private readonly ILogger<AiPuzzleIllustrationProvider> _logger;

    public AiPuzzleIllustrationProvider(
        HttpClient http,
        IOptions<AiOptions> ai,
        IOptions<PetBrainOptions> options,
        ILogger<AiPuzzleIllustrationProvider> logger)
    {
        _http = http;
        _ai = ai.Value;
        _options = options.Value;
        _logger = logger;

        _http.BaseAddress = new Uri(_ai.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.IllustrationTimeoutSeconds));

        if (!string.IsNullOrWhiteSpace(_ai.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _ai.ApiKey);
    }

    public bool IsEnabled =>
        _options.UseAiIllustration && !string.IsNullOrWhiteSpace(_options.IllustrationModel);

    public async Task<PuzzleIllustrationResult> RenderAsync(
        PuzzleSceneSpec spec, string prompt, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return PuzzleIllustrationResult.Failed("disabled");

        var provider = "openai-compatible";
        var model = _options.IllustrationModel;

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                model,
                prompt,
                n = 1,

                // Portret ölçü — kətan 390×690-dır, yəni yatay rəsm kəsilərdi.
                size = "1024x1536",
                response_format = "b64_json"
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "images/generations")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return PuzzleIllustrationResult.Failed($"http-{(int)response.StatusCode}", provider, model);

            var payload = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(payload);

            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array ||
                data.GetArrayLength() == 0)
                return PuzzleIllustrationResult.Failed("no-data", provider, model);

            var first = data[0];

            // Provayderin moderasiya nəticəsi varsa ONA HÖRMƏT EDİLİR — bizim
            // öz yoxlamamız onu əvəz etmir, üstünə gəlir.
            if (first.TryGetProperty("safety_result", out var safety) &&
                safety.ValueKind == JsonValueKind.String &&
                !string.Equals(safety.GetString(), "ok", StringComparison.OrdinalIgnoreCase))
                return PuzzleIllustrationResult.Failed("moderation", provider, model);

            if (!first.TryGetProperty("b64_json", out var encoded) || encoded.ValueKind != JsonValueKind.String)
            {
                // Link qaytaran cavab QƏSDƏN rədd edilir: uşağın cihazı
                // naməlum domenə sorğu atmamalıdır.
                return PuzzleIllustrationResult.Failed("no-inline-bytes", provider, model);
            }

            var base64 = encoded.GetString() ?? string.Empty;

            if (base64.Length is 0 or > MaxBase64Length)
                return PuzzleIllustrationResult.Failed("bad-payload-size", provider, model);

            if (!TryDecode(base64, out var bytes))
                return PuzzleIllustrationResult.Failed("bad-base64", provider, model);

            // Format və ölçü yoxlaması AYRICA qatdadır (bax
            // PuzzleIllustrationValidator) — provayderin başlığına inanılmır.
            return PuzzleIllustrationResult.Ok(bytes, PuzzleIllustrationValidator.Png, provider, model);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return PuzzleIllustrationResult.Failed("timeout", provider, model);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogDebug(ex, "PetBrain: şəkil provayderi cavab vermədi.");
            return PuzzleIllustrationResult.Failed("provider-error", provider, model);
        }
    }

    private static bool TryDecode(string base64, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }
}
