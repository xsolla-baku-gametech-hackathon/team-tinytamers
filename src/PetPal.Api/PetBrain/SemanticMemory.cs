using PetPal.Api.Common;
using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Bir semantik nəticə üçün namizəd — hansı naxış, neçə müşahidə.
/// </summary>
public sealed record SemanticCandidate(string FactKey, string ValueKey, int Support, int Importance);

/// <summary>
/// Epizodlardan NAXIŞ çıxaran qat — saf qaydalar, I/O yoxdur.
///
/// <para><b>Nə üçün ayrı səviyyə?</b> Episodik yaddaş çox olur və tez köhnəlir:
/// «Ayda şimal kraterini seçdin» bir aydan sonra maraqlı deyil. Naxış isə
/// əksinədir — «kəşf seçimlərinə üstünlük verir» zamanla DAHA doğru olur və
/// yaddaş təmizlənəndə itməməlidir.</para>
///
/// <para><b>Bir epizoddan naxış çıxarılmır.</b> Şərt ən azı
/// <see cref="MinSupport"/> müstəqil müşahidədir: «bir dəfə seçdi, deməli
/// sevir» məhz Pet Brain-in qaçmalı olduğu səhvdir.</para>
///
/// <para><b>Xam mətn yoxdur.</b> Nəticələr yalnız təsdiqlənmiş açarlardan
/// çıxarılır — uşağın yazdığı heç nə buraya düşmür.</para>
/// </summary>
public static class SemanticMemory
{
    /// <summary>Naxış sayılmaq üçün lazım olan ən az müşahidə.</summary>
    public const int MinSupport = 2;

    /// <summary>Naxışın vacibliyi — episodik faktdan yuxarıdır, çünki daha uzun yaşayır.</summary>
    public const int PatternImportance = 75;

    // ---- Təsdiqlənmiş naxış açarları ----

    /// <summary>Uşaq kəşf/marşrut seçimlərinə üstünlük verir.</summary>
    public const string PrefersExploring = "prefers-exploring";

    /// <summary>Uşaq yaradıcı, təzyiqsiz seçimlərə üstünlük verir.</summary>
    public const string PrefersCreating = "prefers-creating";

    /// <summary>Uşaq həll etməyi, planlamağı seçir.</summary>
    public const string PrefersSolving = "prefers-solving";

    /// <summary>Uşaq başqasına kömək edən seçimləri seçir.</summary>
    public const string PrefersHelping = "prefers-helping";

    /// <summary>Tapmacada dəstək (ipucu) faydalı olur — bu, CƏZA deyil.</summary>
    public const string SupportHelps = "support-helps";

    /// <summary>
    /// Naxış açarını uşağın seçim açarından çıxarır; naməlum açar üçün <c>null</c>.
    ///
    /// <para>Cədvəl qapalıdır: yeni seçim açarı gələndə naxış səssizcə
    /// uydurulmur.</para>
    /// </summary>
    public static string? PatternFor(string? traitKey) => traitKey switch
    {
        TraitKeys.Explorer => PrefersExploring,
        TraitKeys.Creative => PrefersCreating,
        TraitKeys.ProblemSolver => PrefersSolving,
        TraitKeys.Caring => PrefersHelping,
        _ => null
    };

    /// <summary>
    /// Uşağın SEÇİM tarixçəsindən çıxan naxışlar.
    ///
    /// <para>Giriş: nəticə sətirlərindən yığılmış seçim açarları və onların
    /// kataloqdakı xassə təsirləri. Çıxış: kifayət qədər dəstəyi olan
    /// naxışlar.</para>
    /// </summary>
    public static IReadOnlyList<SemanticCandidate> FromChoices(IEnumerable<string> traitKeys)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var key in traitKeys)
        {
            if (PatternFor(key) is not { } pattern)
                continue;

            counts[pattern] = counts.GetValueOrDefault(pattern) + 1;
        }

        return
        [
            .. counts
                .Where(pair => pair.Value >= MinSupport)
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new SemanticCandidate(pair.Key, string.Empty, pair.Value, PatternImportance))
        ];
    }

    /// <summary>
    /// Dəstəyin faydalı olduğu naxışı — ipucu istəyib SONRA həll edən uşaq.
    ///
    /// <para>Bu, bilərəkdən müsbət çərçivədədir: «kömək istəmək işə yarayır»
    /// faktı uşağa kömək istəməyi asanlaşdırır. Heç bir yerdə «çox ipucu
    /// istəyir» kimi oxunmur.</para>
    /// </summary>
    public static SemanticCandidate? FromSupport(int solvedWithHints) =>
        solvedWithHints >= MinSupport
            ? new SemanticCandidate(SupportHelps, string.Empty, solvedWithHints, PatternImportance - 10)
            : null;

    /// <summary>Naxışın uşağın dilində cümləsi — açardan qurulur, uydurulmur.</summary>
    public static string Render(string factKey, string language, string petName) => factKey switch
    {
        PrefersExploring => Localized.T(language,
            "Sən həmişə yeni yolu seçirsən — mən də səninlə kəşf etməyi sevirəm.",
            "You always pick the new path — I love exploring with you."),

        PrefersCreating => Localized.T(language,
            "Sən hər dəfə öz həllini qurursan. Bu, çox xoşuma gəlir!",
            "You build your own way every time. I really like that!"),

        PrefersSolving => Localized.T(language,
            "Sən əvvəlcə düşünüb sonra hərəkət edirsən — planların işləyir.",
            "You think first and then move — your plans work."),

        PrefersHelping => Localized.T(language,
            "Sən həmişə kiminsə qayğısına qalırsan. Mən bunu unutmuram.",
            "You always look after someone. I do not forget that."),

        SupportHelps => Localized.T(language,
            "Birlikdə baxanda tapmacalar asanlaşır — kömək istəmək ağıllı işdir.",
            "Puzzles get easier when we look together — asking for help is smart."),

        _ => Localized.T(language,
            $"{petName} sənin haqqında bir şey öyrəndi.",
            $"{petName} learned something about you.")
    };

    /// <summary>Naxış açarının bilinməsi — naməlum açar heç yerdə göstərilmir.</summary>
    public static bool IsKnown(string? factKey) => factKey is
        PrefersExploring or PrefersCreating or PrefersSolving or PrefersHelping or SupportHelps;
}
