using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PetPal.Api.Wardrobe;

/// <summary>Moderasiyanın hökmü.</summary>
public enum ModerationVerdict
{
    Allowed = 0,
    Flagged = 1,

    /// <summary>Yoxlama aparıla bilmədi — şəkil ÇƏKİLMİR (fail closed).</summary>
    Unavailable = 2
}

/// <summary>
/// Uşağın mətnini şəkil modelinə çatmazdan ƏVVƏL yoxlayan qat.
///
/// <para>Determinist filtr dar siyahıdır; bu qat isə mənanı oxuyur. Söhbətdən
/// fərqli olaraq burada <b>fail closed</b>-dur: yoxlama işləmirsə şəkil də
/// çəkilmir. Səbəb — söhbətdə alternativ qayda əsaslı replika var, şəkildə
/// isə «yoxlanmamış» şəklin ehtiyatı yoxdur.</para>
/// </summary>
public interface IWardrobeModeration
{
    Task<ModerationVerdict> CheckAsync(string text, CancellationToken ct = default);
}

/// <summary>Xidmət qurulmayıbsa heç nə yoxlanmır və heç nə çəkilmir.</summary>
public sealed class DisabledWardrobeModeration : IWardrobeModeration
{
    public Task<ModerationVerdict> CheckAsync(string text, CancellationToken ct = default) =>
        Task.FromResult(ModerationVerdict.Unavailable);
}

/// <summary>
/// OpenAI <c>POST /moderations</c> — pulsuzdur (rəsmi sənəd, 2026-09-11).
/// Standart model <c>omni-moderation-latest</c>-dir.
/// </summary>
public sealed class OpenAiWardrobeModeration : IWardrobeModeration
{
    private readonly HttpClient _http;
    private readonly WardrobeOptions _options;
    private readonly ILogger<OpenAiWardrobeModeration> _logger;

    public OpenAiWardrobeModeration(
        HttpClient http, IOptions<WardrobeOptions> options, ILogger<OpenAiWardrobeModeration> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(2, _options.ModerationTimeoutSeconds));

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public async Task<ModerationVerdict> CheckAsync(string text, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                "moderations", new { model = _options.ModerationModel, input = text }, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Wardrobe: moderasiya HTTP {Status} qaytardı.", (int)response.StatusCode);
                return ModerationVerdict.Unavailable;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

            if (!document.RootElement.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array ||
                results.GetArrayLength() == 0 ||
                !results[0].TryGetProperty("flagged", out var flagged) ||
                flagged.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return ModerationVerdict.Unavailable;

            return flagged.GetBoolean() ? ModerationVerdict.Flagged : ModerationVerdict.Allowed;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ModerationVerdict.Unavailable;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogDebug(ex, "Wardrobe: moderasiya cavab vermədi.");
            return ModerationVerdict.Unavailable;
        }
    }
}
