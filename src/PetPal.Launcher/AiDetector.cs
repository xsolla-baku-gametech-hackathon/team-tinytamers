using System.Text.Json;

namespace PetPal.Launcher;

/// <summary>Aşkarlanmış model serveri — API-yə environment dəyişəni kimi ötürülür.</summary>
internal sealed record AiSetup(string BaseUrl, string Model);

/// <summary>
/// Lokal model serverini aşkarlayır.
///
/// Niyə avtomatik: masaüstü paketində istifadəçi konfiqurasiya faylı redaktə etmir —
/// tək ikonaya basır. Ollama onsuz da işləyirsə və söhbət modeli varsa, AI pet
/// dialoqu özü açılmalıdır; yoxdursa app qayda əsaslı replikalarla işləməyə davam edir.
///
/// Model qəsdən <b>çəkilmir</b>: model faylı gigabaytlarladır və açılış ekranında
/// gözlənilməz endirmə başlatmaq düzgün olmazdı. Yalnız artıq mövcud olan işlədilir.
/// </summary>
internal static class AiDetector
{
    /// <summary>Tərcih sırası — biri varsa o seçilir.</summary>
    private static readonly string[] Preferred =
    [
        "llama3.2", "llama3.1", "qwen2.5", "phi3", "gemma2", "mistral"
    ];

    /// <summary>Embedding modelləri söhbət üçün yararsızdır — adına görə kənarlaşdırılır.</summary>
    private static readonly string[] EmbeddingHints = ["embed", "bge-", "nomic-", "minilm", "e5-"];

    /// <summary>
    /// Minimum parametr sayı (milyardla).
    ///
    /// Bu hədd ölçülmüş nəticəyə əsaslanır: <c>qwen2.5:0.5b</c> Azərbaycanca
    /// pozuq mətn qaytarır ("Ayan, s?nin adin, Max, ?hval 4"). Belə replikanı
    /// uşağa göstərmək qayda əsaslı mətndən pisdir, ona görə kiçik modellər
    /// aşkarlansa da işlədilmir — AI sadəcə söndürülü qalır.
    /// </summary>
    private const double MinimumBillionParameters = 3.0;

    public static async Task<AiSetup?> DetectAsync(Func<Task<bool>> ensureContainerAsync)
    {
        if (!await IsPortOpenAsync())
        {
            // Ollama işləmir; PetPal-ın öz konteyneri varsa qaldırmağa dəyər.
            if (!await ensureContainerAsync() || !await IsPortOpenAsync())
                return null;
        }

        var model = await PickModelAsync();
        if (model is null)
        {
            LauncherLog.Write($"{OllamaBase} cavab verir, amma söhbət modeli yoxdur — AI söndürülü qalır.");
            return null;
        }

        LauncherLog.Write($"AI aşkarlandı: {model} ({OllamaBase}).");
        return new AiSetup(OllamaBase + "/v1", model);
    }

    private const string OllamaBase = "http://localhost:11434";

    private static async Task<bool> IsPortOpenAsync()
    {
        using var client = new System.Net.Sockets.TcpClient();
        try
        {
            await client.ConnectAsync("localhost", 11434).WaitAsync(TimeSpan.FromSeconds(2));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string?> PickModelAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        List<string> names;
        try
        {
            using var response = await http.GetAsync($"{OllamaBase}/api/tags");
            if (!response.IsSuccessStatusCode)
                return null;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("models", out var models))
                return null;

            names = [.. models.EnumerateArray()
                .Select(m => m.TryGetProperty("name", out var n) ? n.GetString() : null)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)];
        }
        catch
        {
            return null;
        }

        var usable = names
            .Where(n => !EmbeddingHints.Any(h => n.Contains(h, StringComparison.OrdinalIgnoreCase)))
            .Where(IsBigEnough)
            .ToList();

        if (usable.Count == 0)
            return null;

        foreach (var preferred in Preferred)
        {
            var match = usable.FirstOrDefault(n => n.StartsWith(preferred, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        return usable[0];
    }

    /// <summary>
    /// Teqdən parametr sayını oxuyur (<c>qwen2.5:3b</c> → 3). Teq ölçü bildirmirsə
    /// (<c>llama3.2:latest</c>) model yalnız tərcih siyahısındadırsa qəbul edilir —
    /// naməlum kiçik modelə uşaq replikasını etibar etmirik.
    /// </summary>
    private static bool IsBigEnough(string name)
    {
        var tag = name.Contains(':') ? name[(name.IndexOf(':') + 1)..] : string.Empty;

        if (tag.EndsWith('b') &&
            double.TryParse(tag[..^1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var billions))
        {
            return billions >= MinimumBillionParameters;
        }

        return Preferred.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }
}
