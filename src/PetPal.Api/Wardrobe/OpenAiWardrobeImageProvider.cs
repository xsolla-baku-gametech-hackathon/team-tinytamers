using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PetPal.Api.PetBrain.Puzzles;

namespace PetPal.Api.Wardrobe;

/// <summary>
/// OpenAI Image API adapteri — standart model <c>gpt-image-2.5-flare</c>.
///
/// <para>Müqavilə rəsmi sənəddən götürülüb (2026-09-11): sıfırdan çəkmək
/// <c>POST /images/generations</c> (JSON), redaktə <c>POST /images/edits</c>
/// (multipart, şəkil <c>image</c> sahəsində). Cavab <c>data[0].b64_json</c>-dur.
/// Təhlükəsizlik rəddi <c>error.code = "moderation_blocked"</c> ilə gəlir və
/// ayrıca tanınır.</para>
///
/// <para><c>moderation</c> HƏMİŞƏ <c>auto</c>-dur və konfiqurasiya ilə
/// dəyişmir: <c>low</c> uşaq tətbiqində seçim deyil. Fon <c>opaque</c>-dur —
/// şəkil güzgünün içində tam kadr kimi görünür.</para>
///
/// <para>Heç vaxt istisna atmır: şəbəkə, vaxt həddi, pozuq JSON — hamısı
/// <see cref="WardrobeImageResult.Failed"/> olur və uşağa «sonra yoxla» deyilir.</para>
/// </summary>
public sealed class OpenAiWardrobeImageProvider : IWardrobeImageProvider
{
    /// <summary>Base64 mətninin yuxarı həddi — bayt həddinin ~4/3 qarşılığı.</summary>
    private const int MaxBase64Length = (PuzzleIllustrationValidator.MaxBytes / 3 * 4) + 1024;

    private readonly HttpClient _http;
    private readonly WardrobeOptions _options;
    private readonly ILogger<OpenAiWardrobeImageProvider> _logger;

    public OpenAiWardrobeImageProvider(
        HttpClient http, IOptions<WardrobeOptions> options, ILogger<OpenAiWardrobeImageProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(10, _options.TimeoutSeconds));

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public bool IsEnabled => _options.IsEnabled;

    public string Name => "openai";

    public string Model => _options.EffectiveImageModel;

    public Task<WardrobeImageResult> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object>
        {
            ["model"] = Model,
            ["prompt"] = prompt,
            ["n"] = 1,
            ["size"] = _options.EffectiveSize,
            ["quality"] = _options.EffectiveQuality,
            ["output_format"] = _options.EffectiveOutputFormat,
            ["background"] = "opaque",
            ["moderation"] = "auto"
        };

        if (_options.EffectiveOutputFormat != "png")
            body["output_compression"] = _options.EffectiveCompression;

        var request = new HttpRequestMessage(HttpMethod.Post, "images/generations")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        return SendAsync(request, ct);
    }

    public Task<WardrobeImageResult> EditAsync(
        string prompt, byte[] reference, string referenceContentType, CancellationToken ct = default)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(Model), "model" },
            { new StringContent(prompt), "prompt" },
            { new StringContent("1"), "n" },
            { new StringContent(_options.EffectiveSize), "size" },
            { new StringContent(_options.EffectiveQuality), "quality" },
            { new StringContent(_options.EffectiveOutputFormat), "output_format" },
            { new StringContent("opaque"), "background" },
            { new StringContent("auto"), "moderation" }
        };

        if (_options.EffectiveOutputFormat != "png")
            form.Add(
                new StringContent(_options.EffectiveCompression.ToString(CultureInfo.InvariantCulture)),
                "output_compression");

        var image = new ByteArrayContent(reference);
        image.Headers.ContentType = new MediaTypeHeaderValue(referenceContentType);
        form.Add(image, "image", "pet" + Extension(referenceContentType));

        return SendAsync(new HttpRequestMessage(HttpMethod.Post, "images/edits") { Content = form }, ct);
    }

    private async Task<WardrobeImageResult> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using var owned = request;
            using var response = await _http.SendAsync(owned, ct);

            var payload = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                if (IsModerationBlock(payload))
                    return WardrobeImageResult.Refusal();

                _logger.LogInformation(
                    "Wardrobe: şəkil modeli HTTP {Status} qaytardı ({Model}).", (int)response.StatusCode, Model);

                return WardrobeImageResult.Failed($"http-{(int)response.StatusCode}");
            }

            using var document = JsonDocument.Parse(payload);

            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array ||
                data.GetArrayLength() == 0)
                return WardrobeImageResult.Failed("no-data");

            if (!data[0].TryGetProperty("b64_json", out var encoded) || encoded.ValueKind != JsonValueKind.String)
                return WardrobeImageResult.Failed("no-inline-bytes");

            var base64 = encoded.GetString() ?? string.Empty;

            if (base64.Length is 0 or > MaxBase64Length)
                return WardrobeImageResult.Failed("bad-payload-size");

            return TryDecode(base64, out var bytes)
                ? WardrobeImageResult.Ok(bytes)
                : WardrobeImageResult.Failed("bad-base64");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return WardrobeImageResult.Failed("timeout");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            _logger.LogDebug(ex, "Wardrobe: şəkil modeli cavab vermədi.");
            return WardrobeImageResult.Failed("provider-error");
        }
    }

    /// <summary>Modelin təhlükəsizlik rəddi — texniki xətadan fərqli işlənir.</summary>
    private static bool IsModerationBlock(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            return document.RootElement.TryGetProperty("error", out var error) &&
                   error.ValueKind == JsonValueKind.Object &&
                   error.TryGetProperty("code", out var code) &&
                   code.ValueKind == JsonValueKind.String &&
                   string.Equals(code.GetString(), "moderation_blocked", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
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

    private static string Extension(string contentType) => contentType switch
    {
        PuzzleIllustrationValidator.Webp => ".webp",
        PuzzleIllustrationValidator.Jpeg => ".jpg",
        _ => ".png"
    };
}
