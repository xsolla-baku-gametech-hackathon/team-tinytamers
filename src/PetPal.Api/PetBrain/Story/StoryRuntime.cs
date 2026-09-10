using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Story;

/// <summary>Bir addımın girişi — hamısı serverdə hesablanmış.</summary>
/// <param name="OptionKey">Seçilmiş variant; seçim düyünü deyilsə boş.</param>
/// <param name="Result">Tapmacanın nəticəsi; tapmaca deyilsə <c>None</c>.</param>
/// <param name="Flags">Bu ana qədər qoyulmuş hekayə bayraqları.</param>
public sealed record StoryInput(
    string OptionKey,
    PetBrainStageResult Result,
    IReadOnlySet<string> Flags);

/// <summary>
/// Qrafın icra qatı — <b>saf</b>: baza yoxdur, saat yoxdur, təsadüf yoxdur.
///
/// <para>Eyni düyün + eyni giriş + eyni bayraqlar HƏMİŞƏ eyni növbəti düyünü
/// verir. Bu, təkcə səliqə deyil: yenilənmə, təkrar göndərilən sorğu və
/// bərpa eyni nəticəni verməlidir, yoxsa uşaq başqa hekayəyə düşərdi.</para>
/// </summary>
public static class StoryRuntime
{
    /// <summary>
    /// Növbəti düyün. Uyğun keçid yoxdursa ehtiyat keçid işləyir; o da yoxdursa
    /// <c>null</c> — bu, tərifin nasazlığıdır və validator onu buraxmır.
    /// </summary>
    public static ExperienceNode? Next(
        ExperienceDefinition definition, ExperienceNode current, StoryInput input)
    {
        // Sıralama DETERMİNİSTDİR: əvvəl prioritet, sonra tərifdəki yazılış
        // sırası. Ehtiyat keçid həmişə sonda yoxlanılır.
        var ordered = current.Transitions
            .Select((transition, index) => (transition, index))
            .OrderBy(t => t.transition.IsFallback ? 1 : 0)
            .ThenBy(t => t.transition.Priority)
            .ThenBy(t => t.index);

        foreach (var (transition, _) in ordered)
        {
            if (!Matches(transition, input))
                continue;

            if (definition.Find(transition.TargetNodeId) is { } target)
                return target;
        }

        return null;
    }

    private static bool Matches(ExperienceTransition transition, StoryInput input)
    {
        if (transition.IsFallback)
            return true;

        if (!string.IsNullOrEmpty(transition.RequiredOptionKey)
            && !string.Equals(transition.RequiredOptionKey, input.OptionKey, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrEmpty(transition.RequiredFlag) && !input.Flags.Contains(transition.RequiredFlag))
            return false;

        if (transition.RequiredResult is { } required && required != input.Result)
            return false;

        return true;
    }

    /// <summary>
    /// Düyünə DAXİL OLARKƏN tətbiq olunan bayraqlar.
    ///
    /// <para>Effektlər ideal-potentdir: eyni düyünə təkrar daxil olmaq bayrağı
    /// ikiqat qoymur, çünki bayraq dəsti çoxluqdur.</para>
    /// </summary>
    public static IReadOnlyList<string> FlagsOf(ExperienceNode node) =>
        [.. node.Effects
            .Where(e => e.Kind == ExperienceEffectKind.SetFlag)
            .Select(e => e.Key)
            .Where(key => !string.IsNullOrWhiteSpace(key))];

    /// <summary>Bu düyünün yaddaşa yazılmasını istədiyi fakt açarları.</summary>
    public static IReadOnlyList<string> RememberKeysOf(ExperienceNode node) =>
        [.. node.Effects
            .Where(e => e.Kind == ExperienceEffectKind.RememberChoice)
            .Select(e => e.Key)
            .Where(key => !string.IsNullOrWhiteSpace(key))];

    /// <summary>Düyünün istədiyi səhnə variantı; effekt onu üstələyə bilər.</summary>
    public static string SceneVariantOf(ExperienceNode node) =>
        node.Effects.FirstOrDefault(e => e.Kind == ExperienceEffectKind.SceneVariant)?.Key
        ?? node.SceneVariant;

    /// <summary>
    /// Verilmiş bayraqlarla başlanğıcdan bu düyünə qədər keçilə bilən ən qısa
    /// addım sayı — irəliləmə göstəricisi üçün təxmin.
    /// </summary>
    public static int EstimatedTotalSteps(ExperienceDefinition definition) => definition.LongestPath;

    /// <summary>
    /// Qarşıdakı tapmaca — <b>yalnız BİRMƏNALI olduqda</b>.
    ///
    /// <para>Tapmaca uşaq ona çatmazdan xeyli əvvəl verilir və rəsmi arxa fonda
    /// çəkilməyə başlayır, yoxsa uşaq tapmacanı açanda gözləyərdi. Budaqlanan
    /// hekayədə isə "qarşıdakı tapmaca" həmişə məlum deyil.</para>
    ///
    /// <para>Ona görə qayda dardır: irəlidəki bütün yollar EYNİ tapmaca düyününə
    /// və EYNİ dərinlikdə çatmalıdır. Ay macərasında üç krater seçimi ayrı
    /// nəticə düyünlərinə gedir, amma üçü də eyni addımda eyni tapmacaya çıxır —
    /// deməli tapmacanı indidən vermək təhlükəsizdir.</para>
    ///
    /// <para>Şərt pozulan kimi <c>null</c> qayıdır: səhv tapmacanı əvvəlcədən
    /// vermək həm yanlış rəsm çəkdirər, həm də pul xərcləyər.</para>
    /// </summary>
    public static (ExperienceNode Puzzle, int Depth)? UpcomingPuzzle(
        ExperienceDefinition definition, ExperienceNode from)
    {
        HashSet<string> seen = new(StringComparer.Ordinal) { from.Id };
        Queue<(ExperienceNode Node, int Depth)> queue = new();
        List<(string Id, int Depth)> puzzles = [];

        queue.Enqueue((from, 0));

        while (queue.Count > 0)
        {
            var (node, depth) = queue.Dequeue();

            if (depth >= ExperienceGraphValidator.MaxPathLength)
                continue;

            foreach (var transition in node.Transitions)
            {
                if (definition.Find(transition.TargetNodeId) is not { } target)
                    continue;

                if (target.Kind == PetBrainStageKind.Puzzle)
                {
                    puzzles.Add((target.Id, depth + 1));
                    continue;
                }

                // Sonluqdan sonra tapmaca olmur; dövrəyə də girmirik.
                if (target.IsEnding || !seen.Add(target.Id))
                    continue;

                queue.Enqueue((target, depth + 1));
            }
        }

        if (puzzles.Count == 0)
            return null;

        var ids = puzzles.Select(p => p.Id).Distinct(StringComparer.Ordinal).ToList();
        var depths = puzzles.Select(p => p.Depth).Distinct().ToList();

        if (ids.Count != 1 || depths.Count != 1)
            return null;

        return definition.Find(ids[0]) is { } puzzle ? (puzzle, depths[0]) : null;
    }
}
