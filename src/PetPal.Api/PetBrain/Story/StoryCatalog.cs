using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Story;

/// <summary>
/// Budaqlanan tərifləri saxlayan kataloq — VERSİYALI.
///
/// <para>Kataloqda olmayan şablon KÖHNƏ xətti yolla işləməyə davam edir —
/// keçid birdəfəlik deyil, şablon-şablondur. Yarımçıq run-lar isə heç bir
/// halda pozulmur: run özü hansı modeldə olduğunu daşıyır (bax
/// <see cref="Entities.ExperienceRun.CurrentNodeId"/>).</para>
///
/// <para><b>Bir açar, bir neçə versiya.</b> Macəranın yeni versiyası
/// yayımlananda köhnəsi siyahıdan SİLİNMİR: həmin versiyada başlamış run-lar
/// bitənə qədər onu oxuyur. Yeni run-lar isə həmişə ən son versiyanı alır.
/// Köhnə versiyanı silmək yalnız onu işlədən açıq run qalmayanda təhlükəsizdir.</para>
/// </summary>
public static class StoryCatalog
{
    public static IReadOnlyList<ExperienceDefinition> Definitions { get; } =
    [
        MoonCrystalHunt.PreviousDefinition,
        MoonCrystalHunt.Definition,
        MoonCrystalSecret.PreviousDefinition,
        MoonCrystalSecret.Definition
    ];

    /// <summary>Açarın ƏN SON versiyası — yeni run-lar yalnız bunu alır.</summary>
    public static ExperienceDefinition? Find(string? key) => Latest(Definitions, key);

    /// <summary>
    /// Run üçün tərif — BAŞLADIĞI versiya ilə.
    ///
    /// <para>Dəqiq versiya kataloqdadırsa o qaytarılır: yenilənmə açıq macəranı
    /// səssizcə yeni qaydalara keçirmir. Yoxdursa ən son versiya qaytarılır
    /// (uşağın macərası itməməlidir), amma <c>Exact</c> yanlış olur və çağıran
    /// düyün tapılmayanda təhlükəsiz bərpaya keçir.</para>
    /// </summary>
    public static (ExperienceDefinition? Definition, bool Exact) Resolve(string? key, int version) =>
        Resolve(Definitions, key, version);

    /// <summary>
    /// <see cref="Resolve(string?, int)"/>-in saf forması — verilmiş siyahı üzərində.
    ///
    /// <para>Ayrıca olması testlər üçündür: kataloq statikdir, amma versiya
    /// qaydası iki versiyalı bir siyahıda yoxlanmalıdır.</para>
    /// </summary>
    public static (ExperienceDefinition? Definition, bool Exact) Resolve(
        IEnumerable<ExperienceDefinition> definitions, string? key, int version)
    {
        if (string.IsNullOrWhiteSpace(key))
            return (null, false);

        var candidates = definitions
            .Where(d => string.Equals(d.Key, key, StringComparison.Ordinal))
            .ToList();

        if (candidates.Count == 0)
            return (null, false);

        if (version > 0 && candidates.FirstOrDefault(d => d.Version == version) is { } exact)
            return (exact, true);

        var latest = candidates.MaxBy(d => d.Version)!;

        return (latest, version is 0 || version == latest.Version);
    }

    public static bool IsGraph(string? key) => Find(key) is not null;

    private static ExperienceDefinition? Latest(IEnumerable<ExperienceDefinition> definitions, string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : definitions
                .Where(d => string.Equals(d.Key, key, StringComparison.Ordinal))
                .MaxBy(d => d.Version);
}
