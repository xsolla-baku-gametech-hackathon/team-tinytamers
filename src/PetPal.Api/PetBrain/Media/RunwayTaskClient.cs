using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PetPal.Api.PetBrain.Media;

/// <summary>Runway tapşırığının vəziyyəti — bizim qapalı təsvirimiz.</summary>
public enum RunwayTaskState
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3
}

/// <summary>
/// Tapşırığın nəticəsi.
///
/// <para><see cref="OutputUrl"/> QISA ÖMÜRLÜDÜR və klientə heç vaxt verilmir —
/// bayt bizim tərəfə köçürülür.</para>
/// </summary>
public sealed record RunwayTask(
    string Id,
    RunwayTaskState State,
    string? OutputUrl,
    string FailureReason)
{
    public static RunwayTask Failed(string reason) => new(string.Empty, RunwayTaskState.Failed, null, reason);
}

/// <summary>Runway Dev API-nin REST səthi — tapşırıq yaradır və izləyir.</summary>
public interface IRunwayTaskClient
{
    bool IsConfigured { get; }

    Task<RunwayTask> CreateImageAsync(string model, string prompt, string ratio, CancellationToken ct = default);

    Task<RunwayTask> CreateVideoAsync(
        string model, string prompt, string promptImageDataUri, string ratio, int durationSeconds,
        CancellationToken ct = default);

    Task<RunwayTask> PollAsync(string taskId, CancellationToken ct = default);

    /// <summary>Hazır faylı YÜKLƏYİR — provayderin URL-i app-dan kənara çıxmır.</summary>
    Task<byte[]?> DownloadAsync(string url, int maxBytes, CancellationToken ct = default);
}

/// <summary>
/// Tipli <c>HttpClient</c> inteqrasiyası.
///
/// <para>Nə JavaScript, nə Python sidecar — SDK üçün ayrıca proses qaldırmaq
/// uşaq tətbiqinin təhlükəsizlik səthini genişləndirərdi.</para>
///
/// <para><b>Açar heç vaxt görünmür:</b> nə loga, nə xəta mətninə, nə də
/// klientə. Aşağıdakı kodda <c>_options.ApiKey</c> yalnız başlığa yazılır.</para>
/// </summary>
public sealed class RunwayTaskClient : IRunwayTaskClient
{
    /// <summary>Sənədləşdirilmiş marşrutlar — bir yerdə saxlanılır.</summary>
    private const string TextToImagePath = "text_to_image";
    private const string ImageToVideoPath = "image_to_video";
    private const string TaskPath = "tasks";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly RunwayOptions _options;
    private readonly ILogger<RunwayTaskClient> _logger;

    public RunwayTaskClient(
        HttpClient http, IOptions<RunwayOptions> options, ILogger<RunwayTaskClient> logger)
    {
        _options = options.Value;
        _http = http;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        // Versiya başlığı MƏCBURİDİR — onsuz API sorğunu rədd edir.
        _http.DefaultRequestHeaders.Remove("X-Runway-Version");
        _http.DefaultRequestHeaders.Add("X-Runway-Version", _options.ApiVersion);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public Task<RunwayTask> CreateImageAsync(
        string model, string prompt, string ratio, CancellationToken ct = default) =>
        CreateAsync(TextToImagePath, new { model, promptText = prompt, ratio }, ct);

    public Task<RunwayTask> CreateVideoAsync(
        string model, string prompt, string promptImageDataUri, string ratio, int durationSeconds,
        CancellationToken ct = default) =>
        CreateAsync(ImageToVideoPath, new
        {
            model,
            promptImage = promptImageDataUri,
            promptText = prompt,
            ratio,
            duration = durationSeconds
        }, ct);

    private async Task<RunwayTask> CreateAsync(string path, object body, CancellationToken ct)
    {
        if (!IsConfigured)
            return RunwayTask.Failed("not-configured");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json")
            };

            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                // Gövdə OXUNMUR: provayderin diaqnostikası açar və ya daxili
                // məlumat daşıya bilər, o isə loga düşməməlidir.
                _logger.LogWarning("Runway: {Path} sorğusu {Status} qaytardı.", path, (int)response.StatusCode);
                return RunwayTask.Failed($"http-{(int)response.StatusCode}");
            }

            return Parse(await response.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning("Runway: {Path} sorğusu alınmadı ({Kind}).", path, ex.GetType().Name);
            return RunwayTask.Failed("transport");
        }
    }

    public async Task<RunwayTask> PollAsync(string taskId, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(taskId))
            return RunwayTask.Failed("not-configured");

        try
        {
            using var response = await _http.GetAsync($"{TaskPath}/{Uri.EscapeDataString(taskId)}", ct);

            if (!response.IsSuccessStatusCode)
                return RunwayTask.Failed($"http-{(int)response.StatusCode}");

            return Parse(await response.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return RunwayTask.Failed("transport");
        }
    }

    public async Task<byte[]?> DownloadAsync(string url, int maxBytes, CancellationToken ct = default)
    {
        // Yalnız HTTPS və yalnız provayderin öz hostu. Yönləndirmə İZLƏNMİR:
        // gözlənilməz host uşağın faylı kimi saxlanmamalıdır.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
            return null;

        try
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            // Elan olunmuş uzunluq artıq həddi aşırsa, heç yükləmirik.
            if (response.Content.Headers.ContentLength is { } declared && declared > maxBytes)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();

            var chunk = new byte[81920];
            int read;

            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            {
                buffer.Write(chunk, 0, read);

                // Elan olunmamış, amma böyük fayl — axını KƏSİRİK.
                if (buffer.Length > maxBytes)
                    return null;
            }

            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Cavabın oxunması. Naməlum vəziyyət <b>uğursuz</b> sayılır (fail closed) —
    /// "bəlkə hazırdır" fərziyyəsi ilə pozuq fayl saxlamaqdansa ehtiyata düşmək
    /// yaxşıdır.
    /// </summary>
    private static RunwayTask Parse(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        var id = root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString() ?? string.Empty
            : string.Empty;

        var status = root.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String
            ? statusElement.GetString() ?? string.Empty
            : string.Empty;

        var state = status.ToUpperInvariant() switch
        {
            "PENDING" or "THROTTLED" => RunwayTaskState.Pending,
            "RUNNING" or "PROCESSING" => RunwayTaskState.Running,
            "SUCCEEDED" or "COMPLETED" => RunwayTaskState.Succeeded,
            _ => RunwayTaskState.Failed
        };

        string? output = null;

        if (state == RunwayTaskState.Succeeded &&
            root.TryGetProperty("output", out var outputElement) &&
            outputElement.ValueKind == JsonValueKind.Array &&
            outputElement.GetArrayLength() > 0 &&
            outputElement[0].ValueKind == JsonValueKind.String)
        {
            output = outputElement[0].GetString();
        }

        // Uğurlu deyilir, amma fayl yoxdur — bu, uğur DEYİL.
        if (state == RunwayTaskState.Succeeded && string.IsNullOrWhiteSpace(output))
            return new RunwayTask(id, RunwayTaskState.Failed, null, "empty-output");

        return new RunwayTask(id, state, output, state == RunwayTaskState.Failed ? status : string.Empty);
    }
}
