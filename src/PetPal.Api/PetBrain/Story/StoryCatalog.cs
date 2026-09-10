using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Story;

/// <summary>
/// Budaqlanan tərifləri saxlayan kataloq.
///
/// <para>Kataloqda olmayan şablon KÖHNƏ xətti yolla işləməyə davam edir —
/// keçid birdəfəlik deyil, şablon-şablondur. Yarımçıq run-lar isə heç bir
/// halda pozulmur: run özü hansı modeldə olduğunu daşıyır (bax
/// <see cref="Entities.ExperienceRun.CurrentNodeId"/>).</para>
/// </summary>
public static class StoryCatalog
{
    public static IReadOnlyList<ExperienceDefinition> Definitions { get; } =
    [
        MoonCrystalHunt.Definition
    ];

    public static ExperienceDefinition? Find(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : Definitions.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// Run üçün tərif — BAŞLADIĞI versiya ilə.
    ///
    /// <para>Versiya uyğun gəlmirsə tərif yenə qaytarılır (uşağın macərası
    /// itməməlidir), amma çağıran bunu bilir və düyün tapılmayanda təhlükəsiz
    /// bərpaya keçir.</para>
    /// </summary>
    public static (ExperienceDefinition? Definition, bool Exact) Resolve(string? key, int version)
    {
        var definition = Find(key);

        if (definition is null)
            return (null, false);

        return (definition, version is 0 || version == definition.Version);
    }

    public static bool IsGraph(string? key) => Find(key) is not null;
}
