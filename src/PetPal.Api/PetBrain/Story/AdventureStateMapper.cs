using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Story;

/// <summary>
/// Saxlanan sətirlərlə saf <see cref="AdventureState"/> arasındakı KÖRPÜ.
///
/// <para>Bir yerdə olması qəsdəndir: engine bazanı, baza isə engine-i
/// tanımır. Format sadə və <b>bağışlayandır</b> — pozulmuş sətir sükutla
/// atılır, çünki bir sətrin xarab olması uşağın macərasını sındırmamalıdır.
/// Ən pis halda bir əşya itir; ən yaxşı halda heç nə.</para>
/// </summary>
public static class AdventureStateMapper
{
    private const char Separator = '|';

    /// <summary>Saxlanan vəziyyəti engine-in oxuduğu formaya çevirir.</summary>
    public static AdventureState ToState(
        AdventureRunState? row,
        IEnumerable<string> storyFlags,
        PetBrainBondTier bondTier,
        string currentNodeId)
    {
        if (row is null)
            return AdventureState.Empty with
            {
                Flags = storyFlags.ToHashSet(StringComparer.Ordinal),
                BondTier = bondTier,
                CurrentNodeId = currentNodeId
            };

        return new AdventureState
        {
            Flags = storyFlags.ToHashSet(StringComparer.Ordinal),
            WorldFlags = row.WorldFlags.ToHashSet(StringComparer.Ordinal),
            VisitedNodeIds = row.VisitedNodeIds.ToHashSet(StringComparer.Ordinal),
            CompletedChapterIds = row.CompletedChapterIds.ToHashSet(StringComparer.Ordinal),
            SelectedChoiceIds = row.SelectedChoiceIds.ToHashSet(StringComparer.Ordinal),
            Inventory = ReadItems(row.Inventory),
            Clues = ReadClues(row.Clues),
            Objectives = ReadObjectives(row.Objectives),
            EndingScores = ReadCounts(row.EndingScores),
            RetryCounts = ReadCounts(row.RetryCounts),
            NpcStates = ReadPairs(row.NpcStates),
            Variant = AdventureVariants.IsKnown(row.Variant) ? row.Variant : AdventureVariants.Standard,
            BondTier = bondTier,
            CurrentNodeId = currentNodeId
        };
    }

    /// <summary>
    /// Vəziyyəti sətirə yazır və <see cref="AdventureRunState.Revision"/>-u artırır.
    ///
    /// <para>Revision-un məhz burada artması vacibdir: hər yazma bir addımdır
    /// və klientin əlindəki nömrə köhnəlir. Ayrı-ayrı yerlərdə artırılsaydı,
    /// bir yol onu unudardı.</para>
    /// </summary>
    public static void Write(AdventureRunState row, AdventureState state)
    {
        row.WorldFlags = [.. state.WorldFlags];
        row.VisitedNodeIds = [.. state.VisitedNodeIds];
        row.CompletedChapterIds = [.. state.CompletedChapterIds];
        row.SelectedChoiceIds = [.. state.SelectedChoiceIds];
        row.Inventory = [.. state.Inventory.Select(i => Join(i.ItemId, i.Quantity.ToString(), i.AcquiredAtNodeId))];
        row.Clues = [.. state.Clues.Select(c => Join(c.ClueId, c.DiscoveredAtNodeId, c.IsNew ? "1" : "0"))];
        row.Objectives =
            [.. state.Objectives.Select(o => Join(o.ObjectiveId, ((int)o.Status).ToString(), o.CurrentCount.ToString()))];
        row.EndingScores = [.. state.EndingScores.Select(p => Join(p.Key, p.Value.ToString()))];
        row.RetryCounts = [.. state.RetryCounts.Select(p => Join(p.Key, p.Value.ToString()))];
        row.NpcStates = [.. state.NpcStates.Select(p => Join(p.Key, p.Value))];
        row.Variant = state.Variant;
        row.Revision++;
    }

    private static string Join(params string[] parts) => string.Join(Separator, parts);

    private static IReadOnlyList<RunItem> ReadItems(IEnumerable<string> rows)
    {
        List<RunItem> items = [];

        foreach (var parts in Split(rows, 3))
        {
            if (int.TryParse(parts[1], out var quantity) && quantity > 0)
                items.Add(new RunItem(parts[0], quantity, parts[2]));
        }

        return items;
    }

    private static IReadOnlyList<RunClue> ReadClues(IEnumerable<string> rows) =>
        [.. Split(rows, 3).Select(parts => new RunClue(parts[0], parts[1], parts[2] == "1"))];

    private static IReadOnlyList<RunObjective> ReadObjectives(IEnumerable<string> rows)
    {
        List<RunObjective> objectives = [];

        foreach (var parts in Split(rows, 3))
        {
            if (!int.TryParse(parts[1], out var status) || !int.TryParse(parts[2], out var count))
                continue;

            if (!Enum.IsDefined(typeof(AdventureObjectiveStatus), status))
                continue;

            objectives.Add(new RunObjective(parts[0], (AdventureObjectiveStatus)status, count));
        }

        return objectives;
    }

    private static IReadOnlyDictionary<string, int> ReadCounts(IEnumerable<string> rows)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);

        foreach (var parts in Split(rows, 2))
        {
            if (int.TryParse(parts[1], out var value))
                counts[parts[0]] = value;
        }

        return counts;
    }

    private static IReadOnlyDictionary<string, string> ReadPairs(IEnumerable<string> rows)
    {
        Dictionary<string, string> pairs = new(StringComparer.Ordinal);

        foreach (var parts in Split(rows, 2))
            pairs[parts[0]] = parts[1];

        return pairs;
    }

    private static IEnumerable<string[]> Split(IEnumerable<string> rows, int expected)
    {
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row))
                continue;

            var parts = row.Split(Separator);

            if (parts.Length == expected && !string.IsNullOrWhiteSpace(parts[0]))
                yield return parts;
        }
    }
}
