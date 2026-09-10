using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PetPal.Api.Ai;
using PetPal.Api.Common;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Könüllü AI zənginləşdirməsi: yalnız BAŞLIQ və GİRİŞ cümləsi.
///
/// <para><b>Modelin edə BİLMƏDİKLƏRİ</b> (hamısı serverin deterministik qərarıdır):
/// şablon seçmək, çətinlik təyin etmək, mükafat vermək, kontent açmaq, doğru
/// cavabı müəyyən etmək, yeni mərhələ növü yaratmaq, HTML və ya link qaytarmaq.</para>
///
/// <para><b>Modelə gedən yeganə məlumat</b>: dil, yaş ZOLAĞI, təsdiqlənmiş şablon
/// açarı və mövzusu, çətinlik, təmizlənmiş pet adı və strukturlu yaddaş açarları.
/// Uşağın söhbəti, şəkilləri, əlaqə məlumatı və ya sərbəst mətni HEÇ VAXT.</para>
///
/// <para>Hər uğursuzluq — timeout, HTTP xətası, pozuq JSON, əskik sahə, çox uzun
/// mətn, qadağan olunmuş söz, ləğv — səssizcə deterministik şablona qayıdır.
/// Uşaq heç bir xəta görmür, çünki mətn onsuz da hazırdır.</para>
/// </summary>
public sealed class AiExperienceNarrativeProvider : IExperienceNarrativeProvider
{
    /// <summary>Başlıq qısa olmalıdır — kartda bir sətirdir.</summary>
    private const int MaxTitleLength = 48;

    /// <summary>Giriş cümləsi danışıq buludunda yerləşməlidir.</summary>
    private const int MaxIntroLength = 160;

    /// <summary>
    /// Uşaq məzmununda olmamalı sözlər. Bu, modelin öz filtrini ƏVƏZ ETMİR —
    /// sadəcə son qapıdır və şübhəli mətn şablona qaytarılır.
    /// </summary>
    private static readonly string[] BannedFragments =
    [
        "http://", "https://", "www.", "<", ">", "{", "}",
        "öl", "ölüm", "qan", "silah", "qorx", "dəhşət", "xəstə",
        "kill", "death", "blood", "weapon", "gun", "scary", "horror", "die",
        "password", "şifrə", "ünvan", "address", "telefon", "phone", "email", "e-poçt"
    ];

    private readonly HttpClient _http;
    private readonly AiOptions _ai;
    private readonly IMemoryCache _cache;
    private readonly IExperienceNarrativeProvider _fallback = new TemplateNarrativeProvider();
    private readonly ILogger<AiExperienceNarrativeProvider> _logger;

    public AiExperienceNarrativeProvider(
        HttpClient http,
        IOptions<AiOptions> ai,
        IMemoryCache cache,
        ILogger<AiExperienceNarrativeProvider> logger)
    {
        _http = http;
        _ai = ai.Value;
        _cache = cache;
        _logger = logger;

        _http.BaseAddress = new Uri(_ai.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(1, _ai.TimeoutSeconds));

        if (!string.IsNullOrWhiteSpace(_ai.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _ai.ApiKey);
    }

    public async Task<ExperienceNarrative> DescribeAsync(NarrativeContext context, CancellationToken ct = default)
    {
        if (!_ai.IsEnabled)
            return await _fallback.DescribeAsync(context, ct);

        var cacheKey = CacheKey(context);

        if (_cache.TryGetValue<ExperienceNarrative>(cacheKey, out var cached) && cached is not null)
            return cached;

        var raw = await OpenAiChat.CompleteAsync(
            _http,
            _ai.Model,
            [
                OpenAiChat.Message.System(SystemPrompt(context.Language)),
                OpenAiChat.Message.User(UserPrompt(context))
            ],
            _ai.MaxOutputTokens,
            temperature: 0.7,
            _logger,
            ct,
            _ai.ReasoningEffort);

        var narrative = Parse(raw, context);

        if (narrative is null)
            return await _fallback.DescribeAsync(context, ct);

        _cache.Set(cacheKey, narrative, TimeSpan.FromMinutes(Math.Max(1, _ai.CacheMinutes)));
        return narrative;
    }

    /// <summary>
    /// Keş açarı — mətnin GİRİŞLƏRİNİN tam əksi.
    ///
    /// <para>Prompt pet-in adını və yaddaş açarlarını daşıyır, ona görə açar da
    /// onları daşımalıdır. Əvvəllər açar yalnız şablon, dil, çətinlik və yaş
    /// zolağından ibarət idi — bu isə bir uşaq üçün yaranan ŞƏXSİ mətni eyni
    /// zolaqdakı başqa uşağa qaytarırdı.</para>
    ///
    /// <para>Uşağa aid hissə açarda AÇIQ deyil: xam ad və yaddaş yerinə qısa
    /// hash yazılır (bax <see cref="NarrativeContext.PersonalizationHash"/>).</para>
    /// </summary>
    internal static string CacheKey(NarrativeContext context) =>
        $"petbrain-narrative:{context.Template.Key}:{context.Template.Version}:{context.Language}:" +
        $"{context.Difficulty}:{context.AgeBand}:{context.PersonalizationHash()}";

    /// <summary>
    /// Cavabı sxemə görə yoxlayır. Bir şey də uyğun gəlməsə <c>null</c> —
    /// yarımçıq mətn ekrana buraxılmır.
    /// </summary>
    private ExperienceNarrative? Parse(string? raw, NarrativeContext context)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var json = ExtractJson(raw);
        if (json is null)
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (!root.TryGetProperty("title", out var titleElement)
                || !root.TryGetProperty("intro", out var introElement))
                return null;

            var title = Clean(titleElement.GetString(), MaxTitleLength);
            var intro = Clean(introElement.GetString(), MaxIntroLength);

            if (title is null || intro is null)
                return null;

            return new ExperienceNarrative(title, intro, ExperienceNarrative.AiSource);
        }
        catch (JsonException)
        {
            _logger.LogDebug("Pet Brain mətni pozuq JSON qaytardı ({Template}) — şablona keçilir.",
                context.Template.Key);
            return null;
        }
    }

    /// <summary>Bəzi modellər JSON-u mətnin içinə qoyur — ilk obyekt götürülür.</summary>
    private static string? ExtractJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');

        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
    }

    /// <summary>Uzunluq, idarəedici simvol və qadağan olunmuş fraqment yoxlaması.</summary>
    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = new string([.. value.Where(c => !char.IsControl(c))]).Trim();
        text = text.Trim('"', '«', '»', '“', '”');

        if (text.Length == 0 || text.Length > maxLength)
            return null;

        foreach (var banned in BannedFragments)
        {
            if (text.Contains(banned, StringComparison.OrdinalIgnoreCase))
                return null;
        }

        return text;
    }

    private static string SystemPrompt(string language) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? """
              Sən uşaq oyununda macəranın adını və giriş cümləsini yazırsan. Oxucu 5–10 yaşlı uşaqdır.

              Qaydalar:
              - Yalnız Azərbaycan dilində yaz.
              - Yalnız JSON qaytar: {"title": "...", "intro": "..."}
              - title ən çoxu 48 simvol, intro ən çoxu 160 simvol və bir cümlə.
              - Verilən mövzudan kənara çıxma, yeni personaj və qayda uydurma.
              - Zorakılıq, silah, qorxu, ölüm, xəstəlik, reklam və link olmasın.
              - Şəxsi məlumat (ünvan, məktəb, telefon, şifrə) soruşma və yazma.
              - İzahat, dırnaq və ya kod bloku əlavə etmə.
              """
            : """
              You write the name and opening line of an adventure in a children's game. The reader is 5–10 years old.

              Rules:
              - Write in English only.
              - Return JSON only: {"title": "...", "intro": "..."}
              - title at most 48 characters, intro at most 160 characters and one sentence.
              - Stay on the given theme; do not invent new characters or rules.
              - No violence, weapons, fear, death, illness, advertising or links.
              - Never ask for or write personal data (address, school, phone, password).
              - No explanation, quotes or code fences.
              """;

    /// <summary>
    /// Yalnız TƏSDİQLƏNMİŞ sahələr. Uşağın mətni burada yoxdur və ola bilməz.
    /// </summary>
    private static string UserPrompt(NarrativeContext context)
    {
        var az = Localized.Normalize(context.Language) == Localized.Azerbaijani;
        var builder = new StringBuilder();

        builder.AppendLine($"- {(az ? "Mövzu" : "Theme")}: {context.Template.Theme}");
        builder.AppendLine($"- {(az ? "Fəaliyyət" : "Activity")}: {context.Template.ActivityType}");
        builder.AppendLine($"- {(az ? "Yaş zolağı" : "Age band")}: {context.AgeBand}");
        builder.AppendLine($"- {(az ? "Çətinlik" : "Difficulty")}: {context.Difficulty}");
        builder.AppendLine($"- {(az ? "Pet-in adı" : "Pet name")}: {PetVoicePrompt.SanitizeName(context.PetName)}");

        if (context.MemoryKeys.Count > 0)
            builder.AppendLine($"- {(az ? "Tanış mövzular" : "Familiar topics")}: " +
                               string.Join(", ", context.MemoryKeys));

        builder.AppendLine();
        builder.Append(az
            ? "Bu macəra üçün ad və bir giriş cümləsi yaz."
            : "Write a name and one opening line for this adventure.");

        return builder.ToString();
    }
}
