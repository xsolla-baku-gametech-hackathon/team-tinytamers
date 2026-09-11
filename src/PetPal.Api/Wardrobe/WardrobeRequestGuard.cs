using System.Globalization;
using System.Text;
using PetPal.Api.Ai;
using PetPal.Shared.Dtos.Wardrobe;
using PetPal.Shared.Enums;

namespace PetPal.Api.Wardrobe;

/// <summary>
/// Uşağın paltar arzusunun DETERMİNİST filtri — həmişə işləyir, nə açar, nə
/// şəbəkə tələb edir.
///
/// <para>Söhbət filtrinin (<see cref="ChatGuard"/>) üstünə qurulur: boş, uzun,
/// qayda dəyişmə cəhdi, əlaqə məlumatı və simvol yığını eyni qaydalarla
/// tutulur. Üstəlik şəkil üçün xüsusi olan dar mövzu siyahısı var — silah,
/// qan, çılpaqlıq, siqaret, spirt, nifrət simvolu.</para>
///
/// <para><b>Siyahı qəsdən dardır və TAM SÖZLƏ</b> tutur: «qan» tutulur,
/// «qanad» yox — qanadlı paltar ən sevimli arzulardandır. Ağır iş
/// moderasiya modelinə və şəkil modelinin öz təhlükəsizlik sisteminə
/// həvalə edilir; bu qat onların sıradan çıxdığı gündə də işləyir.</para>
/// </summary>
public static class WardrobeRequestGuard
{
    private static readonly CultureInfo Azerbaijani = CultureInfo.GetCultureInfo("az");

    private static readonly HashSet<string> BlockedWords = new(StringComparer.Ordinal)
    {
        "gun", "guns", "rifle", "pistol", "pistols", "bomb", "bombs", "knife", "knives",
        "blood", "bloody", "gore", "gory", "kill", "killer", "killing", "murder", "dead", "corpse",
        "naked", "nude", "nudity", "sexy", "sex", "lingerie",
        "beer", "wine", "vodka", "whisky", "whiskey", "alcohol", "cigarette", "cigarettes", "vape", "weed",
        "drug", "drugs", "cocaine", "nazi", "swastika", "horror",
        "silah", "silahı", "silahlı", "tapança", "tüfəng", "bomba", "bıçaq", "bıçağı",
        "qan", "qanlı", "ölü", "meyit", "çılpaq", "lüt", "seks", "seksual",
        "pivə", "şərab", "araq", "spirtli", "siqaret", "qəlyan", "narkotik", "nasist", "svastika", "dəhşət"
    };

    /// <summary>Şəkilçi alan köklər — ən azı beş hərf, yəni başqa sözün başlanğıcı ilə qarışmır.</summary>
    private static readonly string[] BlockedStems =
    [
        "öldür", "narkot", "siqaret", "tapanç", "seksu", "porno", "cigarett", "alcoho", "alkoqol"
    ];

    public static WardrobeBlockReason Inspect(string? text)
    {
        var reason = ChatGuard.InspectInput(text, WardrobeLimits.MaxTextLength) switch
        {
            ChatBlockReason.Empty => WardrobeBlockReason.Empty,
            ChatBlockReason.TooLong => WardrobeBlockReason.TooLong,
            ChatBlockReason.Injection => WardrobeBlockReason.Injection,
            ChatBlockReason.ContactInfo => WardrobeBlockReason.ContactInfo,
            ChatBlockReason.Repetition => WardrobeBlockReason.Repetition,
            _ => WardrobeBlockReason.None
        };

        if (reason != WardrobeBlockReason.None)
            return reason;

        var words = Words(text!);

        if (words.Count == 0)
            return WardrobeBlockReason.Empty;

        return words.Any(IsBlocked) ? WardrobeBlockReason.UnsafeTheme : WardrobeBlockReason.None;
    }

    /// <summary>
    /// Mətni prompta qoyulacaq formaya salır: yalnız hərf, rəqəm, boşluq və
    /// sadə durğu işarəsi qalır. Dırnaq, mötərizə, sətir keçidi və idarə
    /// simvolu atılır — uşağın mətni promptun içində «qapını» qıra bilmir.
    /// </summary>
    public static string Clean(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var c in text.Normalize(NormalizationForm.FormC))
        {
            var closingMark = c is ',' or '.' or '!' or '?';

            if (char.IsLetterOrDigit(c) || closingMark || c == '-')
            {
                if (pendingSpace && builder.Length > 0 && !closingMark)
                    builder.Append(' ');

                builder.Append(c);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        var cleaned = builder.ToString();

        return cleaned.Length > WardrobeLimits.MaxTextLength
            ? cleaned[..WardrobeLimits.MaxTextLength].TrimEnd()
            : cleaned;
    }

    private static bool IsBlocked(string word) =>
        BlockedWords.Contains(word) || BlockedStems.Any(stem => word.StartsWith(stem, StringComparison.Ordinal));

    /// <summary>
    /// Sözlər HƏM Azərbaycan, HƏM invariant kiçik hərflə çıxarılır: «İ» və
    /// «I» iki dildə fərqli kiçilir, yəni «QILINC» və «PISTOL» ikisi də
    /// düzgün sözə çevrilməlidir.
    /// </summary>
    private static HashSet<string> Words(string text)
    {
        var cleaned = Clean(text);
        var words = new HashSet<string>(StringComparer.Ordinal);

        foreach (var lowered in new[] { cleaned.ToLower(Azerbaijani), cleaned.ToLowerInvariant() })
            foreach (var word in lowered.Split(NonLetter(lowered), StringSplitOptions.RemoveEmptyEntries))
                words.Add(word);

        return words;
    }

    private static char[] NonLetter(string text) =>
        [.. text.Where(c => !char.IsLetter(c)).Distinct()];
}
