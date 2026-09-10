using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Pet-in yoldaşlıq xarakteri — uşağın SABİT xassələrindən çıxarılır.
///
/// <para>Ayrıca saxlanılmır və qəsdən: ikinci, sürətlə sürüşən "şəxsiyyət"
/// obyekti saxlasaydıq, o, profil ilə ziddiyyətə düşərdi. Burada isə tək
/// həqiqət mənbəyi xassə cədvəlidir.</para>
///
/// <para>Növ, yaş və mərhələ TOXUNULMAZ qalır: xarakter pet-in kim olduğunu
/// deyil, uşaqla münasibətinin RƏNGİNİ təsvir edir.</para>
/// </summary>
public static class CompanionPersonality
{
    /// <summary>
    /// Lider bu qədər fərqlə qabaqda olmasa xarakter DƏYİŞMİR.
    ///
    /// <para>Histerezis olmasaydı, iki yaxın bal arasında etiket hər sessiyada
    /// atlayardı — uşaq üçün pet "fikrini dəyişən" görünərdi.</para>
    /// </summary>
    public const int SwitchMargin = 6;

    /// <summary>Bu balın altında heç bir istiqamət kifayət qədər güclü deyil.</summary>
    public const int MinimumSignal = 45;

    /// <summary>
    /// Cari xarakter. <paramref name="current"/> əvvəlki etiketdir — histerezis
    /// məhz onun üstündə işləyir.
    /// </summary>
    public static PetBrainPersonality Derive(
        IReadOnlyDictionary<string, int> interests,
        IReadOnlyDictionary<string, int> playStyles,
        PetBrainPersonality current = PetBrainPersonality.Balanced)
    {
        var scores = new Dictionary<PetBrainPersonality, int>
        {
            [PetBrainPersonality.CuriousScientist] = Average(
                Get(interests, TraitKeys.Science),
                Get(interests, TraitKeys.Puzzles),
                Get(playStyles, TraitKeys.ProblemSolver)),

            [PetBrainPersonality.CreativeCompanion] = Average(
                Get(playStyles, TraitKeys.Creative),
                Get(interests, TraitKeys.Stories),
                Get(interests, TraitKeys.Fantasy)),

            [PetBrainPersonality.ExplorerCompanion] = Average(
                Get(playStyles, TraitKeys.Explorer),
                Get(interests, TraitKeys.Space),
                Get(interests, TraitKeys.Nature)),

            [PetBrainPersonality.CaringCompanion] = Average(
                Get(playStyles, TraitKeys.Caring),
                Get(interests, TraitKeys.Animals),
                Get(playStyles, TraitKeys.Caring))
        };

        var ranked = scores
            .OrderByDescending(p => p.Value)
            .ThenBy(p => p.Key.ToString(), StringComparer.Ordinal)
            .ToList();

        var leader = ranked[0];

        if (leader.Value < MinimumSignal)
            return PetBrainPersonality.Balanced;

        // Hazırkı etiket hələ liderdirsə heç nə dəyişmir.
        if (current == leader.Key)
            return leader.Key;

        // Yeni namizəd yalnız AYDIN fərqlə qabaqdadırsa keçid baş verir.
        var currentScore = scores.TryGetValue(current, out var value) ? value : 0;

        return leader.Value - currentScore >= SwitchMargin
            ? leader.Key
            : current;
    }

    public static string Label(PetBrainPersonality personality, string language) => personality switch
    {
        PetBrainPersonality.CuriousScientist => Localized.T(language, "Maraqlı Alim", "Curious Scientist"),
        PetBrainPersonality.CreativeCompanion => Localized.T(language, "Yaradıcı Yoldaş", "Creative Companion"),
        PetBrainPersonality.ExplorerCompanion => Localized.T(language, "Kəşfiyyatçı Yoldaş", "Explorer Companion"),
        PetBrainPersonality.CaringCompanion => Localized.T(language, "Qayğıkeş Yoldaş", "Caring Companion"),
        _ => Localized.T(language, "Hər Şeyə Hazır Yoldaş", "All-Round Companion")
    };

    public static string Icon(PetBrainPersonality personality) => personality switch
    {
        PetBrainPersonality.CuriousScientist => "🔬",
        PetBrainPersonality.CreativeCompanion => "🎨",
        PetBrainPersonality.ExplorerCompanion => "🧭",
        PetBrainPersonality.CaringCompanion => "💚",
        _ => "✨"
    };

    private static int Get(IReadOnlyDictionary<string, int> scores, string key) =>
        scores.TryGetValue(key, out var value) ? TraitKeys.Clamp(value) : TraitKeys.StartingScore;

    private static int Average(params int[] values) =>
        values.Length == 0 ? 0 : (int)Math.Round(values.Average(), MidpointRounding.AwayFromZero);
}
