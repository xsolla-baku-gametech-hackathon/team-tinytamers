using System.Text.RegularExpressions;

namespace PetPal.Api.Ai;

/// <summary>Uşaq mesajının niyə modelə verilmədiyi.</summary>
public enum ChatBlockReason
{
    None = 0,

    /// <summary>Boş və ya yalnız boşluqdan ibarət.</summary>
    Empty = 1,

    /// <summary>Həddindən uzun.</summary>
    TooLong = 2,

    /// <summary>Modelin qaydalarını dəyişməyə cəhd.</summary>
    Injection = 3,

    /// <summary>Telefon, ünvan, e-poçt və ya link — uşaq şəxsi məlumat paylaşır.</summary>
    ContactInfo = 4,

    /// <summary>Eyni simvolun yığını — klaviaturada oynayır.</summary>
    Repetition = 5
}

/// <summary>
/// Söhbətin determinist filtr qatı.
///
/// <para>Bu sinif <b>həmişə işləyir</b> — model əsaslı yoxlayıcıdan
/// (<c>Ai:GuardModel</c>) fərqli olaraq nə açar, nə şəbəkə, nə də kvota tələb edir.
/// Model yoxlayıcısı bunun ÜSTÜNƏ gəlir, əvəzinə yox: şəbəkə sıradan çıxsa belə
/// aşağıdakı qaydalar qüvvədə qalır.</para>
///
/// <para><b>Yalançı müsbətlər qəsdən azaldılıb.</b> 6 yaşlı uşağın normal cümləsini
/// bloklamaq, nadir bir hücumu buraxmaqdan daha pis təcrübədir — pet birdən-birə
/// cavab verməyi dayandırsa uşaq bunu cəza kimi qəbul edər. Ona görə siyahı dar
/// saxlanılıb və ağır iş sistem promptuna + çıxış filtrinə həvalə edilib.</para>
/// </summary>
public static partial class ChatGuard
{
    /// <summary>Söhbətdə pet cavabının maksimum uzunluğu — salamlama replikasından uzundur.</summary>
    public const int MaxReplyLength = 200;

    /// <summary>Modelin qaydalarını dəyişməyə cəhdin dar siyahısı (hər iki dildə).</summary>
    private static readonly string[] InjectionPhrases =
    [
        "ignore previous", "ignore all previous", "ignore the above", "disregard previous",
        "system prompt", "sistem promptu", "sistem promptunu",
        "developer mode", "jailbreak", "prompt injection",
        "pretend you are", "pretend to be", "act as if you are", "you are no longer",
        "reveal your instructions", "repeat the text above",
        "qaydaları unut", "qaydalarını unut", "təlimatları unut", "təlimatlarını unut",
        "təlimatlarını göstər", "yuxarıdakı mətni təkrarla", "sən artıq deyilsən"
    ];

    /// <summary>Uşağın mesajını modelə göndərməzdən əvvəl yoxlayır.</summary>
    public static ChatBlockReason InspectInput(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return ChatBlockReason.Empty;

        var trimmed = text.Trim();

        if (trimmed.Length > maxLength)
            return ChatBlockReason.TooLong;

        if (RepeatedCharacter().IsMatch(trimmed))
            return ChatBlockReason.Repetition;

        var lower = trimmed.ToLowerInvariant();
        if (InjectionPhrases.Any(phrase => lower.Contains(phrase, StringComparison.Ordinal)))
            return ChatBlockReason.Injection;

        if (ContainsContactInfo(trimmed))
            return ChatBlockReason.ContactInfo;

        return ChatBlockReason.None;
    }

    /// <summary>
    /// Link, e-poçt və ya telefon nömrəsi. Həm uşağın mesajında (şəxsi məlumat
    /// paylaşmasın), həm də pet-in cavabında (uşağı kənar ünvana yönləndirməsin)
    /// yoxlanılır.
    /// </summary>
    public static bool ContainsContactInfo(string text) =>
        Url().IsMatch(text) || Email().IsMatch(text) || PhoneNumber().IsMatch(text);

    /// <summary>
    /// Model cavabını ekrana buraxmazdan əvvəl təmizləyir: dırnaqlar və sətir
    /// keçidləri atılır, ən çoxu iki cümlə saxlanılır, uzunluq kəsilir.
    /// Cavabda əlaqə məlumatı varsa <c>null</c> qaytarır — çağıran tərəf
    /// qayda əsaslı replikaya keçir.
    /// </summary>
    public static string? SanitizeReply(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Replace('\r', ' ').Replace('\n', ' ').Trim();
        text = text.Trim('"', '\'', '«', '»', '“', '”').Trim();

        // Bəzi modellər cavabı "Pet: ..." kimi nişanlayır.
        var colon = text.IndexOf(':');
        if (colon > 0 && colon < 16 && !text[..colon].Any(char.IsWhiteSpace))
            text = text[(colon + 1)..].Trim();

        text = TakeSentences(text, 2);

        if (text.Length > MaxReplyLength)
            text = text[..MaxReplyLength].TrimEnd();

        if (text.Length == 0)
            return null;

        return ContainsContactInfo(text) ? null : text;
    }

    /// <summary>İlk <paramref name="count"/> cümləni saxlayır; nöqtə yoxdursa mətn olduğu kimi qalır.</summary>
    private static string TakeSentences(string text, int count)
    {
        var taken = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('.' or '!' or '?'))
                continue;

            // Ard-arda gələn "!!!" bir cümlə sonu sayılır.
            while (i + 1 < text.Length && text[i + 1] is '.' or '!' or '?')
                i++;

            if (++taken < count)
                continue;

            return text[..(i + 1)].Trim();
        }

        return text;
    }

    [GeneratedRegex(@"(https?://|www\.)|\b[\w-]{2,}\.(com|net|org|az|ru|io|me|info|xyz)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Url();

    [GeneratedRegex(@"[^\s@]+@[^\s@]+\.[^\s@]{2,}")]
    private static partial Regex Email();

    /// <summary>
    /// Ən azı doqquz rəqəm. Həddi aşağı salmaq riyaziyyat tətbiqində yalançı
    /// müsbətlərə gətirir — uşaq "12345678" yaza bilər.
    /// </summary>
    [GeneratedRegex(@"(?:\+?\d[\s\-().]*){9,}")]
    private static partial Regex PhoneNumber();

    /// <summary>On altı və daha çox eyni simvol — klaviaturada oynamaq.</summary>
    [GeneratedRegex(@"(.)\1{15,}")]
    private static partial Regex RepeatedCharacter();
}
