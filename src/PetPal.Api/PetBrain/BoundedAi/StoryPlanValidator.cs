using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.BoundedAi;

/// <summary>Bir rədd səbəbi — telemetriyada etiket kimi işlədilir, ona görə qapalıdır.</summary>
public static class StoryPlanRejection
{
    public const string Empty = "empty";
    public const string UnknownExperience = "unknown-experience";
    public const string UnknownId = "unknown-id";
    public const string TooManyNodes = "too-many-nodes";
    public const string MissingStart = "missing-start";
    public const string Unreachable = "unreachable";
    public const string DeadEnd = "dead-end";
    public const string NoFallback = "no-fallback";
    public const string TooFewEndings = "too-few-endings";
    public const string DuplicateNode = "duplicate-node";
    public const string PuzzleNotAllowed = "puzzle-not-allowed";
    public const string GraphInvalid = "graph-invalid";
}

/// <summary>Yoxlamanın nəticəsi: qəbul edilmiş tərif, yoxsa rədd səbəbi.</summary>
public sealed record StoryPlanReview(
    ExperienceDefinition? Definition,
    string? RejectionReason,
    IReadOnlyList<string> Problems)
{
    public bool Accepted => Definition is not null;

    public static StoryPlanReview Reject(string reason, params string[] problems) =>
        new(null, reason, problems);

    public static StoryPlanReview Accept(ExperienceDefinition definition) =>
        new(definition, null, []);
}

/// <summary>
/// Modelin planını QƏBUL ETMƏZDƏN ƏVVƏL yoxlayan server qatı — saf sinif.
///
/// <para><b>Fail closed.</b> Bir yoxlama da uğursuz olsa plan bütövlükdə rədd
/// edilir və deterministik planlayıcı işə düşür. Yarımçıq qəbul yoxdur: «bu
/// düyün pisdir, qalanı yaxşıdır» qərarını vermək üçün əsasımız yoxdur.</para>
///
/// <para><b>Nə yoxlanılır:</b> sxem (boş plan, təkrar açar), bütün ID-lərin
/// allowlist-də olması, tapmacanın bu macərəyə icazəli olması, qrafın
/// çatılanlığı, dalanın olmaması, ehtiyat keçidin varlığı, düyün həddi, sonluq
/// sayı. Qrafın özü isə mövcud <see cref="ExperienceGraphValidator"/>-dan
/// keçir — <b>eyni validator</b>, çünki mənbənin insan, yoxsa model olması
/// zəmanəti dəyişdirməməlidir.</para>
///
/// <para><b>Modelin toxuna BİLMƏDİKLƏRİ (sahə səviyyəsində mövcud deyil):</b>
/// mükafat məbləği, tapmacanın doğru cavabı, çətinlik, ekran vaxtı, yaş həddi,
/// mətnin özü.</para>
/// </summary>
public static class StoryPlanValidator
{
    /// <summary>Modelin təklif edə biləcəyi ən çox düyün.</summary>
    public const int MaxNodes = 24;

    public static StoryPlanReview Review(StoryPlan? plan, ExperienceDefinition reference)
    {
        if (plan is null || plan.Nodes.Count == 0)
            return StoryPlanReview.Reject(StoryPlanRejection.Empty, "Plan boşdur.");

        // Model MACƏRA seçə bilmir: o, yalnız mövcud bir macəranın quruluşunu
        // təklif edir.
        if (!string.Equals(plan.ExperienceKey, reference.Key, StringComparison.Ordinal))
            return StoryPlanReview.Reject(
                StoryPlanRejection.UnknownExperience, $"«{plan.ExperienceKey}» gözlənilən macəra deyil.");

        if (plan.Nodes.Count > MaxNodes)
            return StoryPlanReview.Reject(
                StoryPlanRejection.TooManyNodes, $"{plan.Nodes.Count} düyün {MaxNodes} həddini keçir.");

        var ids = plan.Nodes.Select(n => n.Id).ToList();

        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            return StoryPlanReview.Reject(StoryPlanRejection.DuplicateNode, "Düyün açarı təkrarlanır.");

        if (!ids.Contains(plan.StartNodeId, StringComparer.Ordinal))
            return StoryPlanReview.Reject(
                StoryPlanRejection.MissingStart, $"Başlanğıc düyün yoxdur: {plan.StartNodeId}.");

        // ---- Bütün ID-lər ALLOWLIST-də olmalıdır ----
        if (UnknownIds(plan, reference) is { Count: > 0 } unknown)
            return StoryPlanReview.Reject(StoryPlanRejection.UnknownId, [.. unknown]);

        // ---- Plan tərifə çevrilir və EYNİ validatordan keçir ----
        var definition = Compile(plan, reference);
        var problems = ExperienceGraphValidator.Validate(definition);

        if (problems.Count > 0)
            return StoryPlanReview.Reject(ReasonFor(problems), [.. problems]);

        return StoryPlanReview.Accept(definition);
    }

    /// <summary>Allowlist-də olmayan hər ID — hansı düyündə olduğu ilə birlikdə.</summary>
    private static List<string> UnknownIds(StoryPlan plan, ExperienceDefinition reference)
    {
        List<string> unknown = [];

        if (!StoryPlanAllowlist.Characters.Contains(plan.CharacterId, StringComparer.Ordinal))
            unknown.Add($"personaj: {plan.CharacterId}");

        foreach (var node in plan.Nodes)
        {
            if (!StoryPlanAllowlist.Beats.Contains(node.BeatId, StringComparer.Ordinal))
                unknown.Add($"{node.Id} → an: {node.BeatId}");

            if (!StoryPlanAllowlist.Scenes.Contains(node.SceneId, StringComparer.Ordinal))
                unknown.Add($"{node.Id} → səhnə: {node.SceneId}");

            if (!StoryPlanAllowlist.Tones.Contains(node.ToneId, StringComparer.Ordinal))
                unknown.Add($"{node.Id} → ton: {node.ToneId}");

            if (!string.IsNullOrEmpty(node.MemoryCallbackId)
                && !StoryPlanAllowlist.MemoryCallbacks.Contains(node.MemoryCallbackId, StringComparer.Ordinal))
                unknown.Add($"{node.Id} → yaddaş: {node.MemoryCallbackId}");

            if (node.Kind == PetBrainStageKind.Ending
                && !StoryPlanAllowlist.Endings.Contains(node.EndingId, StringComparer.Ordinal))
                unknown.Add($"{node.Id} → sonluq: {node.EndingId}");

            if (node.Kind == PetBrainStageKind.Puzzle)
            {
                if (!StoryPlanAllowlist.PuzzleAdapters.Contains(node.PuzzleAdapterId, StringComparer.Ordinal))
                    unknown.Add($"{node.Id} → tapmaca: {node.PuzzleAdapterId}");

                // Tapmaca bu MACƏRAYA icazəli olmalıdır: Ay hekayəsi Marsın
                // tapmacasını ala bilməz — bu qayda modelə də aiddir.
                else if (reference.AllowedPuzzleFamilies.Count > 0
                         && !reference.AllowedPuzzleFamilies.Contains(node.PuzzleAdapterId, StringComparer.Ordinal))
                    unknown.Add($"{node.Id} → tapmaca bu macərəya icazəli deyil: {node.PuzzleAdapterId}");
            }

            foreach (var transition in node.Transitions)
            {
                if (!StoryPlanAllowlist.TransitionTypes.Contains(transition.TypeId, StringComparer.Ordinal))
                    unknown.Add($"{node.Id} → keçid növü: {transition.TypeId}");
            }
        }

        return unknown;
    }

    /// <summary>Qraf problemindən telemetriya üçün qapalı səbəb açarı.</summary>
    private static string ReasonFor(IReadOnlyList<string> problems)
    {
        var joined = string.Join(' ', problems);

        if (joined.Contains("dalandır", StringComparison.Ordinal))
            return StoryPlanRejection.DeadEnd;

        if (joined.Contains("ehtiyat keçid", StringComparison.Ordinal))
            return StoryPlanRejection.NoFallback;

        if (joined.Contains("çatılmır", StringComparison.Ordinal))
            return StoryPlanRejection.Unreachable;

        if (joined.Contains("sonluq olmalıdır", StringComparison.Ordinal))
            return StoryPlanRejection.TooFewEndings;

        return StoryPlanRejection.GraphInvalid;
    }

    /// <summary>
    /// Planı tərifə çevirir — MƏTN kataloqdan gəlir, modeldən yox.
    ///
    /// <para>Model yalnız «hansı hazır parça, hansı sıra ilə» deyir. Uşağın
    /// oxuduğu hər söz istinad tərifindəki düyünlərdəndir; belə parça yoxdursa
    /// düyün neytral kataloq mətni alır.</para>
    /// </summary>
    private static ExperienceDefinition Compile(StoryPlan plan, ExperienceDefinition reference)
    {
        var nodes = plan.Nodes.Select(node =>
        {
            // Mətn üçün istinad: eyni səhnə variantına sahib mövcud düyün.
            var source = reference.Nodes.FirstOrDefault(n =>
                string.Equals(n.SceneVariant, node.SceneId, StringComparison.Ordinal));

            var transitions = node.Transitions.Select(t => new ExperienceTransition(
                TargetNodeId: t.TargetNodeId,
                RequiredOptionKey: t.TypeId == "choice" ? t.OptionId : string.Empty,
                RequiredResult: t.TypeId == "solved" ? PetBrainStageResult.Solved : null,
                Priority: t.TypeId == "always" ? 1000 : 10,
                IsFallback: t.TypeId == "always")).ToList();

            return new ExperienceNode(
                Id: node.Id,
                Kind: node.Kind,
                PromptAz: source?.PromptAz ?? string.Empty,
                PromptEn: source?.PromptEn ?? string.Empty,
                PetLineAz: source?.PetLineAz ?? string.Empty,
                PetLineEn: source?.PetLineEn ?? string.Empty,
                Options: source?.Options ?? [],
                Transitions: transitions,
                Effects: source?.Effects ?? [],
                SceneVariant: node.SceneId,
                PuzzleFamily: node.PuzzleAdapterId,
                MemoryCallbackKey: node.MemoryCallbackId,
                EndingKey: node.EndingId);
        }).ToList();

        return new ExperienceDefinition(
            Key: reference.Key,

            // Versiya ARTIRILIR: modelin planı ilə oynanan run özünü ayırd
            // edə bilməlidir və köhnə run-lar öz versiyası ilə qalır.
            Version: reference.Version + 1000,
            StartNodeId: plan.StartNodeId,
            Nodes: nodes,
            AllowedPuzzleFamilies: reference.AllowedPuzzleFamilies);
    }
}
