using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Story;

/// <summary>İnventardakı bir sətir — nə, neçə dənə, haradan.</summary>
public sealed record RunItem(string ItemId, int Quantity, string AcquiredAtNodeId);

/// <summary>Jurnaldakı bir sətir — nə, harada tapıldı, hələ oxunmayıbmı.</summary>
public sealed record RunClue(string ClueId, string DiscoveredAtNodeId, bool IsNew);

/// <summary>Bir məqsədin GEDİŞATI — tərif deyil, uşağın vəziyyəti.</summary>
public sealed record RunObjective(
    string ObjectiveId,
    AdventureObjectiveStatus Status,
    int CurrentCount);

/// <summary>
/// Macəranın icra VƏZİYYƏTİ — şərtlərin oxuduğu yeganə mənbə.
///
/// <para>Tərifdən (nə mümkündür) qəsdən ayrıdır: bu, uşağın harada olduğunu
/// deyir. Ayrılıq olmadan macəranın mətnini dəyişmək yarımçıq run-ların
/// vəziyyətini də dəyişərdi.</para>
///
/// <para>Struktur DƏYİŞMƏZDİR (<c>record</c> + oxunan çoxluqlar): engine hər
/// addımda yenisini qurur, köhnəsini yerində dəyişmir. Beləliklə «effekt
/// yarımçıq tətbiq olundu» vəziyyəti mümkün deyil.</para>
/// </summary>
public sealed record AdventureState
{
    public required IReadOnlySet<string> Flags { get; init; }
    public required IReadOnlySet<string> WorldFlags { get; init; }
    public required IReadOnlySet<string> VisitedNodeIds { get; init; }
    public required IReadOnlySet<string> CompletedChapterIds { get; init; }
    public required IReadOnlySet<string> SelectedChoiceIds { get; init; }
    public required IReadOnlyList<RunItem> Inventory { get; init; }
    public required IReadOnlyList<RunClue> Clues { get; init; }
    public required IReadOnlyList<RunObjective> Objectives { get; init; }
    public required IReadOnlyDictionary<string, int> EndingScores { get; init; }
    public required IReadOnlyDictionary<string, int> RetryCounts { get; init; }
    public required IReadOnlyDictionary<string, string> NpcStates { get; init; }

    /// <summary>Run başlayanda seçilmiş və İÇƏRİDƏ DƏYİŞMƏYƏN fərdiləşdirmə variantı.</summary>
    public required string Variant { get; init; }

    public required PetBrainBondTier BondTier { get; init; }

    /// <summary>Şərtin baxdığı cari düyün — <see cref="ExperienceCondition.MinimumRetries"/> üçün.</summary>
    public string CurrentNodeId { get; init; } = string.Empty;

    public static AdventureState Empty { get; } = new()
    {
        Flags = new HashSet<string>(StringComparer.Ordinal),
        WorldFlags = new HashSet<string>(StringComparer.Ordinal),
        VisitedNodeIds = new HashSet<string>(StringComparer.Ordinal),
        CompletedChapterIds = new HashSet<string>(StringComparer.Ordinal),
        SelectedChoiceIds = new HashSet<string>(StringComparer.Ordinal),
        Inventory = [],
        Clues = [],
        Objectives = [],
        EndingScores = new Dictionary<string, int>(StringComparer.Ordinal),
        RetryCounts = new Dictionary<string, int>(StringComparer.Ordinal),
        NpcStates = new Dictionary<string, string>(StringComparer.Ordinal),
        Variant = AdventureVariants.Standard,
        BondTier = PetBrainBondTier.NewFriend
    };

    public bool HasItem(string itemId) =>
        Inventory.Any(i => string.Equals(i.ItemId, itemId, StringComparison.Ordinal) && i.Quantity > 0);

    public int ItemCount(string itemId) =>
        Inventory.FirstOrDefault(i => string.Equals(i.ItemId, itemId, StringComparison.Ordinal))?.Quantity ?? 0;

    public bool HasClue(string clueId) =>
        Clues.Any(c => string.Equals(c.ClueId, clueId, StringComparison.Ordinal));

    public bool HasCompletedObjective(string objectiveId) =>
        Objectives.Any(o => string.Equals(o.ObjectiveId, objectiveId, StringComparison.Ordinal)
                            && o.Status == AdventureObjectiveStatus.Completed);

    public RunObjective? Objective(string objectiveId) =>
        Objectives.FirstOrDefault(o => string.Equals(o.ObjectiveId, objectiveId, StringComparison.Ordinal));

    public int RetriesAt(string nodeId) => RetryCounts.GetValueOrDefault(nodeId);

    /// <summary>
    /// Şərt ödənirmi.
    ///
    /// <para>Bu, qrafın YEGANƏ qərar nöqtəsidir: heç bir yerdə «əgər uşaq
    /// bunu edibsə» məntiqi əl ilə yazılmır, hamısı buradan keçir. Bir yerdə
    /// olmasının səbəbi budur ki, testdə hər şərt növü bir dəfə yoxlanılanda
    /// bütün macəralar üçün yoxlanılmış olur.</para>
    /// </summary>
    public bool Satisfies(ExperienceCondition condition)
    {
        if (condition.IsAlwaysTrue)
            return true;

        if (!string.IsNullOrEmpty(condition.RequiredFlag) && !Flags.Contains(condition.RequiredFlag))
            return false;

        if (!string.IsNullOrEmpty(condition.ForbiddenFlag) && Flags.Contains(condition.ForbiddenFlag))
            return false;

        if (!string.IsNullOrEmpty(condition.RequiredItem) && !HasItem(condition.RequiredItem))
            return false;

        if (!string.IsNullOrEmpty(condition.RequiredClue) && !HasClue(condition.RequiredClue))
            return false;

        if (!string.IsNullOrEmpty(condition.RequiredObjective)
            && !HasCompletedObjective(condition.RequiredObjective))
            return false;

        if (!string.IsNullOrEmpty(condition.RequiredWorldFlag)
            && !WorldFlags.Contains(condition.RequiredWorldFlag))
            return false;

        if (!string.IsNullOrEmpty(condition.RequiredChoice)
            && !SelectedChoiceIds.Contains(condition.RequiredChoice))
            return false;

        if (!string.IsNullOrEmpty(condition.RequiredChapter)
            && !CompletedChapterIds.Contains(condition.RequiredChapter))
            return false;

        if (!string.IsNullOrEmpty(condition.VisitedNode) && !VisitedNodeIds.Contains(condition.VisitedNode))
            return false;

        if (!string.IsNullOrEmpty(condition.RequiredVariant)
            && !string.Equals(condition.RequiredVariant, Variant, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrEmpty(condition.ForbiddenVariant)
            && string.Equals(condition.ForbiddenVariant, Variant, StringComparison.Ordinal))
            return false;

        if (condition.MinimumBondTier is { } tier && BondTier < tier)
            return false;

        if (condition.MinimumRetries > 0 && RetriesAt(CurrentNodeId) < condition.MinimumRetries)
            return false;

        return true;
    }
}
