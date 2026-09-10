using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PetPal.Api.Ai;

/// <summary>
/// OpenAI-uyğun <c>/chat/completions</c> protokolu — sxem və çağırış tək yerdədir,
/// çünki onu iki istifadəçi paylaşır: salamlama replikası
/// (<see cref="AiPetVoiceGenerator"/>) və söhbət (<see cref="PetChatService"/>).
///
/// <para><b>Əsas qayda:</b> <see cref="CompleteAsync"/> heç vaxt istisna atmır.
/// Şəbəkə xətası, timeout, 4xx/5xx, pozuq JSON — hamısında <c>null</c> qaytarır və
/// çağıran tərəf qayda əsaslı mətnə keçir. Uşaq ekranı modeldən asılı olmamalıdır.</para>
/// </summary>
public static class OpenAiChat
{
    public sealed class Message
    {
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;

        public static Message System(string content) => new() { Role = "system", Content = content };
        public static Message User(string content) => new() { Role = "user", Content = content };
        public static Message Assistant(string content) => new() { Role = "assistant", Content = content };
    }

    /// <summary>Cavab mətni, uğursuzluqda <c>null</c>.</summary>
    /// <param name="reasoningEffort">
    /// Reasoning modelləri üçün düşünmə büdcəsi. Boş/<c>null</c> olanda sahə
    /// sorğuya ƏLAVƏ EDİLMİR — Ollama kimi serverlər naməlum sahəni rədd edə bilər.
    /// </param>
    public static async Task<string?> CompleteAsync(
        HttpClient http,
        string model,
        IEnumerable<Message> messages,
        int maxTokens,
        double temperature,
        ILogger logger,
        CancellationToken ct,
        string? reasoningEffort = null)
    {
        try
        {
            var request = new Request
            {
                Model = model,
                Messages = [.. messages],
                MaxTokens = maxTokens,
                Temperature = temperature,
                ReasoningEffort = string.IsNullOrWhiteSpace(reasoningEffort) ? null : reasoningEffort
            };

            using var response = await http.PostAsJsonAsync("chat/completions", request, ct);
            if (!response.IsSuccessStatusCode)
            {
                // 429 ayrıca qeyd olunur: pulsuz səviyyədə limit real gözlənilən haldır
                // və "model işləmir" ilə qarışdırılmamalıdır.
                logger.LogDebug("Model cavab vermədi: HTTP {Status} ({Model})", (int)response.StatusCode, model);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<Response>(ct);
            return payload?.Choices?.FirstOrDefault()?.Message?.Content;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException
                                       or System.Text.Json.JsonException)
        {
            // Gözlənilən nasazlıqlar: server işləmir, ləngiyir və ya pozuq JSON qaytarır.
            // Debug səviyyəsi qəsdəndir — bu, xəta deyil, sadəcə fallback səbəbi.
            logger.LogDebug(ex, "Model çağırışı alınmadı ({Model}), qayda əsaslı mətnə keçilir.", model);
            return null;
        }
    }

    private sealed class Request
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("messages")] public List<Message> Messages { get; set; } = [];
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("temperature")] public double Temperature { get; set; }
        [JsonPropertyName("stream")] public bool Stream => false;

        [JsonPropertyName("reasoning_effort")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReasoningEffort { get; set; }
    }

    private sealed class Response
    {
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")] public Message? Message { get; set; }
    }
}
