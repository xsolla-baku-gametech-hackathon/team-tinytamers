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
/// <para><see cref="OutputUrl"/> QISA ÖMÜRLÜDÜR (provayderə görə 24–48 saat) və
/// klientə heç vaxt verilmir — bayt bizim tərəfə köçürülür.</para>
///
/// <para><see cref="Cost"/> provayderin yekunlaşmış tapşırıqda bildirdiyi
/// HƏQİQİ kreditdir; bildirməyibsə <c>null</c>.</para>
/// </summary>
public sealed record RunwayTask(
    string Id,
    RunwayTaskState State,
    string? OutputUrl,
    string FailureReason,
    int? Cost = null)
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
/// <para><b>Müqavilə 2026-09-11-də rəsmi SDK-dan yoxlanılıb</b>
/// (<c>runwayml/sdk-node</c>, <c>runwayml/sdk-python</c>):</para>
/// <list type="bullet">
///   <item>Yaratma cavabı YALNIZ <c>id</c> və <c>estimatedCost</c> daşıyır —
///   <c>status</c> YOXDUR. Id gəlibsə tapşırıq artıq yaradılıb və PULLUDUR,
///   ona görə o, «gözləyir» sayılır və izlənir. Bunu «uğursuz» saymaq ödənilmiş
///   nəticəni atmaq olardı.</item>
///   <item>Vəziyyətlər: <c>PENDING</c>, <c>THROTTLED</c>, <c>RUNNING</c>,
///   <c>SUCCEEDED</c>, <c>FAILED</c>, <c>CANCELLED</c>. Naməlum vəziyyət
///   uğursuz sayılır.</item>
///   <item><c>promptText</c> ən çox 1000 UTF-16 simvoldur.</item>
/// </list>
///
/// <para><b>Açar yalnız API sorğularına gedir.</b> Başlıqlar hər API sorğusuna
/// AYRICA yazılır, klientin standart başlığı kimi YOX: hazır faylın ünvanı
/// provayderin CDN-idir və ora nə açar, nə versiya başlığı getməməlidir.
/// Yönləndirmə də izlənmir — klientin əsas handler-i <c>Program.cs</c>-də
/// <c>AllowAutoRedirect = false</c> ilə qurulur. Açar nə loga, nə xəta
/// mətninə, nə də klientə düşür.</para>
/// </summary>
public sealed class RunwayTaskClient : IRunwayTaskClient
{
    /// <summary>Runway-in <c>promptText</c> həddi (UTF-16 simvol).</summary>
    public const int MaxPromptLength = 1000;

    private const string TextToImagePath = "text_to_image";
    private const string ImageToVideoPath = "image_to_video";
    private const string TaskPath = "tasks";
    private const string VersionHeader = "X-Runway-Version";

    /// <summary>Provayderin xəta kodunun saxlanan hissəsi — səbəb sütunu 60 simvoldur.</summary>
    private const int MaxFailureCodeLength = 48;

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
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public Task<RunwayTask> CreateImageAsync(
        string model, string prompt, string ratio, CancellationToken ct = default) =>
        CreateAsync(TextToImagePath, prompt, new { model, promptText = prompt, ratio }, ct);

    public Task<RunwayTask> CreateVideoAsync(
        string model, string prompt, string promptImageDataUri, string ratio, int durationSeconds,
        CancellationToken ct = default) =>
        CreateAsync(ImageToVideoPath, prompt, new
        {
            model,
            promptImage = promptImageDataUri,
            promptText = prompt,
            ratio,
            duration = durationSeconds
        }, ct);

    /// <summary>
    /// Tapşırığı YARADIR.
    ///
    /// <para>Prompt həddi aşırsa sorğu GETMİR: provayder onu onsuz da rədd
    /// edərdi, səbəb isə «səhnə alınmadı» kimi görünərdi.</para>
    ///
    /// <para>HTTP xətasının gövdəsi OXUNMUR: provayderin diaqnostikası daxili
    /// məlumat daşıya bilər və loga düşməməlidir.</para>
    /// </summary>
    private async Task<RunwayTask> CreateAsync(string path, string prompt, object body, CancellationToken ct)
    {
        if (!IsConfigured)
            return RunwayTask.Failed("not-configured");

        if (prompt.Length is 0 or > MaxPromptLength)
            return RunwayTask.Failed("prompt-length");

        try
        {
            using var request = ApiRequest(HttpMethod.Post, path);
            request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Runway: {Path} sorğusu {Status} qaytardı.", path, (int)response.StatusCode);
                return RunwayTask.Failed($"http-{(int)response.StatusCode}");
            }

            return ParseCreated(await response.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (IsTransport(ex, ct))
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
            using var request = ApiRequest(HttpMethod.Get, $"{TaskPath}/{Uri.EscapeDataString(taskId)}");
            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return RunwayTask.Failed($"http-{(int)response.StatusCode}");

            return ParseTask(await response.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (IsTransport(ex, ct))
        {
            return RunwayTask.Failed("transport");
        }
    }

    /// <summary>
    /// Hazır faylı yükləyir.
    ///
    /// <para>Yalnız HTTPS. Sorğu çılpaqdır — nə açar, nə versiya başlığı.
    /// Elan olunmuş uzunluq həddi aşırsa fayl heç yüklənmir; elan olunmayan,
    /// amma böyük fayl isə axın zamanı KƏSİLİR.</para>
    /// </summary>
    public async Task<byte[]?> DownloadAsync(string url, int maxBytes, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            if (response.Content.Headers.ContentLength is { } declared && declared > maxBytes)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();

            var chunk = new byte[81920];
            int read;

            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            {
                buffer.Write(chunk, 0, read);

                if (buffer.Length > maxBytes)
                    return null;
            }

            return buffer.ToArray();
        }
        catch (Exception ex) when (IsTransport(ex, ct))
        {
            return null;
        }
    }

    /// <summary>API sorğusu — açar və versiya başlığı YALNIZ burada yazılır.</summary>
    private HttpRequestMessage ApiRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.Add(VersionHeader, _options.ApiVersion);

        return request;
    }

    /// <summary>
    /// Nəqliyyat xətasıdırmı. Çağıranın öz ləğvi BURAYA DÜŞMÜR — o, yuxarı
    /// ötürülür ki, prosesin dayanması «provayder cavab vermədi» kimi
    /// qeydə alınmasın və iş yenidən başlatmadan sonra davam etsin.
    /// </summary>
    private static bool IsTransport(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException or JsonException or IOException ||
        (ex is TaskCanceledException && !ct.IsCancellationRequested);

    /// <summary>Yaratma cavabı: id varsa tapşırıq «gözləyir», yoxdursa uğursuzdur.</summary>
    private static RunwayTask ParseCreated(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var id = StringOf(document.RootElement, "id");

        return string.IsNullOrWhiteSpace(id)
            ? RunwayTask.Failed("no-task-id")
            : new RunwayTask(id, RunwayTaskState.Pending, null, string.Empty);
    }

    /// <summary>
    /// Tapşırığın oxunması. Naməlum vəziyyət <b>uğursuz</b> sayılır (fail
    /// closed) — "bəlkə hazırdır" fərziyyəsi ilə pozuq fayl saxlamaqdansa
    /// ehtiyata düşmək yaxşıdır. «Uğurlu, amma fayl yoxdur» da uğur deyil.
    /// </summary>
    private static RunwayTask ParseTask(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        var id = StringOf(root, "id");
        var status = StringOf(root, "status").ToUpperInvariant();

        var cost = Credits(root, "cost");

        var state = status switch
        {
            "PENDING" or "THROTTLED" => RunwayTaskState.Pending,
            "RUNNING" => RunwayTaskState.Running,
            "SUCCEEDED" => RunwayTaskState.Succeeded,
            _ => RunwayTaskState.Failed
        };

        if (state == RunwayTaskState.Failed)
            return new RunwayTask(id, state, null, FailureOf(root, status), cost);

        if (state != RunwayTaskState.Succeeded)
            return new RunwayTask(id, state, null, string.Empty, cost);

        var output = root.TryGetProperty("output", out var outputElement) &&
                     outputElement.ValueKind == JsonValueKind.Array &&
                     outputElement.GetArrayLength() > 0 &&
                     outputElement[0].ValueKind == JsonValueKind.String
            ? outputElement[0].GetString()
            : null;

        return string.IsNullOrWhiteSpace(output)
            ? new RunwayTask(id, RunwayTaskState.Failed, null, "empty-output", cost)
            : new RunwayTask(id, state, output, string.Empty, cost);
    }

    /// <summary>
    /// Uğursuzluğun səbəbi — provayderin qısa KODU (məsələn moderasiya), mətni yox.
    ///
    /// <para>Mətn (<c>failure</c>) diaqnostika daşıya bilər və saxlanmır. Kod
    /// icazəli simvollara endirilir və qısaldılır — o, yalnız böyüklərin
    /// nümayiş qatına düşür.</para>
    /// </summary>
    private static string FailureOf(JsonElement root, string status)
    {
        if (status == "CANCELLED")
            return "cancelled";

        if (status != "FAILED")
            return "unknown-status";

        var code = new string(StringOf(root, "failureCode")
            .Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
            .Take(MaxFailureCodeLength)
            .ToArray());

        return code.Length > 0 ? $"failed:{code}" : "failed";
    }

    /// <summary>
    /// Kredit sahəsi. <b>Canlı API obyekt qaytarır</b> (<c>"cost": {"credits": 5}</c>,
    /// 2026-09-11-də ölçülüb); adi rəqəm də qəbul edilir ki, forma dəyişsə
    /// həqiqi xərc oxunmamış qalmasın.
    /// </summary>
    private static int? Credits(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var element))
            return null;

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var direct))
            return direct;

        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty("credits", out var credits) &&
               credits.ValueKind == JsonValueKind.Number &&
               credits.TryGetInt32(out var nested)
            ? nested
            : null;
    }

    private static string StringOf(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(name, out var element) &&
        element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : string.Empty;
}
