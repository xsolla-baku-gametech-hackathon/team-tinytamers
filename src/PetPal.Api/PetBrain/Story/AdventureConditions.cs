using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Story;

/// <summary>
/// Bir keçidin və ya variantın ŞƏRTİ.
///
/// <para>Şərt hələ də <b>ifadə deyil</b>: nə skript, nə hesablama, nə də
/// müqayisə operatoru. Hər sahə qapalı taksonomiyadan bir açar tələb edir və
/// boş sahəyə ümumiyyətlə baxılmır. Bu, V1-dəki iki sahəlik şərtin (variant
/// açarı və bayraq) eyni prinsiplə genişləndirilməsidir — yeni dil deyil.</para>
///
/// <para>Bütün dolu sahələr eyni anda ödənməlidir (VƏ məntiqi). «YA» lazım
/// olanda iki ayrı keçid yazılır: budaqlanma qrafda görünməlidir, şərtin
/// içində gizlənməməlidir.</para>
/// </summary>
public sealed record ExperienceCondition
{
    public static ExperienceCondition None { get; } = new();

    /// <summary>Bu hekayə bayrağı qoyulmuş olmalıdır.</summary>
    public string RequiredFlag { get; init; } = string.Empty;

    /// <summary>Bu bayraq qoyulmuş OLMAMALIDIR.</summary>
    public string ForbiddenFlag { get; init; } = string.Empty;

    /// <summary>Bu əşya inventarda olmalıdır.</summary>
    public string RequiredItem { get; init; } = string.Empty;

    /// <summary>Bu ipucu jurnalda olmalıdır.</summary>
    public string RequiredClue { get; init; } = string.Empty;

    /// <summary>Bu məqsəd TAMAMLANMIŞ olmalıdır.</summary>
    public string RequiredObjective { get; init; } = string.Empty;

    /// <summary>Bu dünya bayrağı qalxmış olmalıdır.</summary>
    public string RequiredWorldFlag { get; init; } = string.Empty;

    /// <summary>Bu variant əvvəllər hansısa seçim düyünündə seçilmiş olmalıdır.</summary>
    public string RequiredChoice { get; init; } = string.Empty;

    /// <summary>Bu chapter tamamlanmış olmalıdır.</summary>
    public string RequiredChapter { get; init; } = string.Empty;

    /// <summary>Bu düyün əvvəllər ziyarət edilmiş olmalıdır.</summary>
    public string VisitedNode { get; init; } = string.Empty;

    /// <summary>
    /// Fərdiləşdirmə variantı — <see cref="AdventureVariants"/> açarları.
    ///
    /// <para>Variant run BAŞLAYANDA seçilir və İÇƏRİDƏ DƏYİŞMİR: uşaq oynadığı
    /// hekayənin qəfil qısalmasını və ya uzanmasını görməməlidir.</para>
    /// </summary>
    public string RequiredVariant { get; init; } = string.Empty;

    /// <summary>Bu variantda keçid BAĞLIDIR.</summary>
    public string ForbiddenVariant { get; init; } = string.Empty;

    /// <summary>Bağ ən azı bu pillədə olmalıdır.</summary>
    public PetBrainBondTier? MinimumBondTier { get; init; }

    /// <summary>
    /// Cari düyündə ən azı bu qədər təkrar cəhd olmalıdır — <b>yumşaq uğursuzluq</b>
    /// yolu üçün: uşaq neçənci dəfə çalışdıqdan sonra alternativ həll açılsın.
    /// </summary>
    public int MinimumRetries { get; init; }

    /// <summary>Heç bir sahə doldurulmayıbsa şərt həmişə ödənir.</summary>
    public bool IsAlwaysTrue =>
        string.IsNullOrEmpty(RequiredFlag)
        && string.IsNullOrEmpty(ForbiddenFlag)
        && string.IsNullOrEmpty(RequiredItem)
        && string.IsNullOrEmpty(RequiredClue)
        && string.IsNullOrEmpty(RequiredObjective)
        && string.IsNullOrEmpty(RequiredWorldFlag)
        && string.IsNullOrEmpty(RequiredChoice)
        && string.IsNullOrEmpty(RequiredChapter)
        && string.IsNullOrEmpty(VisitedNode)
        && string.IsNullOrEmpty(RequiredVariant)
        && string.IsNullOrEmpty(ForbiddenVariant)
        && MinimumBondTier is null
        && MinimumRetries <= 0;

    /// <summary>Şərtin toxunduğu bütün açarlar — validator onların mövcudluğunu yoxlayır.</summary>
    public IEnumerable<(string Kind, string Key)> References()
    {
        if (!string.IsNullOrEmpty(RequiredItem)) yield return ("item", RequiredItem);
        if (!string.IsNullOrEmpty(RequiredClue)) yield return ("clue", RequiredClue);
        if (!string.IsNullOrEmpty(RequiredObjective)) yield return ("objective", RequiredObjective);
        if (!string.IsNullOrEmpty(RequiredChapter)) yield return ("chapter", RequiredChapter);
        if (!string.IsNullOrEmpty(VisitedNode)) yield return ("node", VisitedNode);
        if (!string.IsNullOrEmpty(RequiredVariant)) yield return ("variant", RequiredVariant);
        if (!string.IsNullOrEmpty(ForbiddenVariant)) yield return ("variant", ForbiddenVariant);
    }
}

/// <summary>
/// Fərdiləşdirmə variantlarının QAPALI siyahısı.
///
/// <para>Variant macəranın uzunluğunu və sıxlığını dəyişir, hekayənin
/// BÜTÖVLÜYÜNÜ isə yox: hər variantda eyni chapter-lər, eyni əsas məqsədlər və
/// eyni sonluqlar əlçatandır. Fərq yalnız könüllü səhnələrin sayındadır.</para>
///
/// <para>Variant açarı qapalı olduğu üçün validator hər variantı ayrıca
/// oynada bilir — «uzun variantda dalan var» halı testdə tutulur.</para>
/// </summary>
public static class AdventureVariants
{
    /// <summary>Yalnız əsas yol — hər chapter 5–7 dəqiqə.</summary>
    public const string Short = "short";

    /// <summary>Əsas yol və bir neçə yan səhnə — 7–10 dəqiqə.</summary>
    public const string Standard = "standard";

    /// <summary>Əlavə kəşf, əlavə NPC dialoqu, gizli ipuçları — 10–12 dəqiqə.</summary>
    public const string Long = "long";

    public static IReadOnlyList<string> All { get; } = [Short, Standard, Long];

    public static bool IsKnown(string? variant) =>
        !string.IsNullOrEmpty(variant) && All.Contains(variant, StringComparer.Ordinal);

    /// <summary>Uşağın sessiya üstünlüyünün variant qarşılığı.</summary>
    public static string For(PetBrainSessionLength length) => length switch
    {
        PetBrainSessionLength.Short => Short,
        PetBrainSessionLength.Long => Long,
        _ => Standard
    };
}
