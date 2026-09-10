using PetPal.Api.Common;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Xassə açarlarının QAPALI siyahısı və mövzu taksonomiyası.
///
/// <para>Bu fayl həm də təhlükəsizlik sərhədidir: klientdən gələn heç bir ad
/// buraya əlavə olunmur. Naməlum açar sadəcə nəzərə alınmır — yəni yeni bir
/// endpoint səhvən sərbəst mətn ötürsə belə, bazada uydurma xassə yaranmır.</para>
///
/// <para>Qorunan/həssas heç bir ölçü yoxdur: nə sağlamlıq, nə əhval-ruhiyyə,
/// nə ailə, nə din, nə də identiklik. Yalnız "hansı oyun mövzusunu sevir" və
/// "necə oynayır".</para>
/// </summary>
public static class TraitKeys
{
    // ---- Maraqlar (mövzu) ----
    public const string Space = "space";
    public const string Science = "science";
    public const string Puzzles = "puzzles";
    public const string Animals = "animals";
    public const string Fantasy = "fantasy";
    public const string Stories = "stories";
    public const string Nature = "nature";
    public const string Ocean = "ocean";

    // ---- Oyun üslubu ----
    public const string Creative = "creative";
    public const string Explorer = "explorer";
    public const string ProblemSolver = "problem-solver";
    public const string Caring = "caring";
    public const string Playful = "playful";

    /// <summary>Yeni profil bu balla başlayır — heç bir mövzu əvvəlcədən "sevilmir".</summary>
    public const int StartingScore = 30;

    public const int MinScore = 0;
    public const int MaxScore = 100;

    /// <summary>
    /// Bir hərəkətin dəyişə biləcəyi ƏN BÖYÜK bal. Qayda cədvəli bundan artıq
    /// dəyər yazsa belə burada kəsilir: profil bir toxunuşla sıçramamalıdır.
    /// </summary>
    public const int MaxDeltaPerAction = 5;

    public static readonly IReadOnlyList<string> Interests =
    [
        Space, Science, Puzzles, Animals, Fantasy, Stories, Nature, Ocean
    ];

    public static readonly IReadOnlyList<string> PlayStyles =
    [
        Creative, Explorer, ProblemSolver, Caring, Playful
    ];

    /// <summary>
    /// Uşaq məzmununun icazə verilən mövzuları. Kataloqdakı hər şablon bu
    /// siyahıdan bir mövzu daşımalıdır — testlə qorunur.
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedThemes =
    [
        Space, Animals, Ocean, Nature, Science, Fantasy, Stories, Puzzles, "robots", "art", "pet-care"
    ];

    public static bool IsKnown(PetBrainTraitCategory category, string key) => category switch
    {
        PetBrainTraitCategory.Interest => Interests.Contains(key, StringComparer.Ordinal),
        PetBrainTraitCategory.PlayStyle => PlayStyles.Contains(key, StringComparer.Ordinal),
        PetBrainTraitCategory.Mechanic => MechanicKeys.IsKnown(key),
        _ => false
    };

    /// <summary>Bu kateqoriyanın bütün təsdiqlənmiş açarları.</summary>
    public static IReadOnlyList<string> KeysOf(PetBrainTraitCategory category) => category switch
    {
        PetBrainTraitCategory.Interest => Interests,
        PetBrainTraitCategory.PlayStyle => PlayStyles,
        PetBrainTraitCategory.Mechanic => MechanicKeys.All,
        _ => []
    };

    public static PetBrainTraitCategory? CategoryOf(string key)
    {
        if (Interests.Contains(key)) return PetBrainTraitCategory.Interest;
        if (PlayStyles.Contains(key)) return PetBrainTraitCategory.PlayStyle;
        return null;
    }

    public static int Clamp(int score) => Math.Clamp(score, MinScore, MaxScore);

    /// <summary>Xassənin uşağın dilində adı. Naməlum açar öz kodunu qaytarır.</summary>
    public static string Label(string key, string language) => key switch
    {
        Space => Localized.T(language, "Kosmos", "Space"),
        Science => Localized.T(language, "Elm", "Science"),
        Puzzles => Localized.T(language, "Tapmacalar", "Puzzles"),
        Animals => Localized.T(language, "Heyvanlar", "Animals"),
        Fantasy => Localized.T(language, "Nağıl", "Fantasy"),
        Stories => Localized.T(language, "Hekayələr", "Stories"),
        Nature => Localized.T(language, "Təbiət", "Nature"),
        Ocean => Localized.T(language, "Okean", "Ocean"),
        Creative => Localized.T(language, "Yaradıcı", "Creative"),
        Explorer => Localized.T(language, "Kəşfiyyatçı", "Explorer"),
        ProblemSolver => Localized.T(language, "Həlledici", "Problem solver"),
        Caring => Localized.T(language, "Qayğıkeş", "Caring"),
        Playful => Localized.T(language, "Şən", "Playful"),
        _ => MechanicKeys.Label(key, language)
    };

    public static string Icon(string key) => key switch
    {
        Space => "🪐",
        Science => "🔬",
        Puzzles => "🧩",
        Animals => "🦊",
        Fantasy => "🐉",
        Stories => "📖",
        Nature => "🌳",
        Ocean => "🌊",
        Creative => "🎨",
        Explorer => "🧭",
        ProblemSolver => "💡",
        Caring => "💚",
        Playful => "🎈",
        _ => MechanicKeys.Icon(key)
    };

    public static PetBrainTraitDto ToDto(string key, int score, string language) => new()
    {
        Key = key,
        Label = Label(key, language),
        Icon = Icon(key),
        Score = Clamp(score)
    };
}
