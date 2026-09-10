using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>Marşrutun yoxlanış nəticəsi. Səbəb loga düşür, uşağa göstərilmir.</summary>
public sealed record RouteCheck(bool IsValid, string Reason)
{
    public static readonly RouteCheck Valid = new(true, string.Empty);

    public static RouteCheck Invalid(string reason) => new(false, reason);
}

/// <summary>
/// Marşrut qaydaları — SAF funksiyalar, I/O yoxdur.
///
/// <para>Bu, referans mexanikanın ürəyidir: düşünmə hərəkəti hekayənin
/// problemini BİRBAŞA həll edir. Uşaq ayrıca viktorina sualına cavab vermir —
/// o, dronu planlaşdırır: enerji çatmalıdır, antena bərpa olunmalıdır, sonra
/// Roboya çatmaq olar.</para>
///
/// <para>Bütün qaydalar GÖRÜNƏN məlumatdan işləyir (düyünün rolu, enerji
/// artımı, qonşuluq). Gizli «doğru marşrut» bayrağı yoxdur — olsaydı, klient
/// onu oxuyub cavabı çıxarardı.</para>
/// </summary>
public static class RouteRules
{
    /// <summary>
    /// Marşrutu addım-addım yoxlayır: başlanğıc, qonşuluq, enerji, məcburi
    /// uğrama və hədəf.
    /// </summary>
    public static RouteCheck Check(PetBrainPuzzleDto puzzle, IReadOnlyList<string> route)
    {
        if (route.Count < 2)
            return RouteCheck.Invalid("too-short");

        var nodes = puzzle.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);

        foreach (var id in route)
        {
            if (!nodes.ContainsKey(id))
                return RouteCheck.Invalid("unknown-node");
        }

        // Eyni düyündən iki dəfə keçmək qadağandır — marşrut sadə yoldur.
        if (route.Distinct(StringComparer.Ordinal).Count() != route.Count)
            return RouteCheck.Invalid("revisit");

        if (nodes[route[0]].Kind != PetBrainNodeKind.Start)
            return RouteCheck.Invalid("not-start");

        if (nodes[route[^1]].Kind != PetBrainNodeKind.Goal)
            return RouteCheck.Invalid("not-goal");

        var adjacency = BuildAdjacency(puzzle.Edges);

        var energy = puzzle.InitialEnergy ?? 0;
        var maximum = puzzle.MaximumEnergy ?? energy;
        var cost = puzzle.MoveCost ?? 1;

        // Başlanğıc düyünün özü də doldura bilər (adətən yox).
        energy = Recharge(energy, maximum, nodes[route[0]]);

        for (var i = 1; i < route.Count; i++)
        {
            var from = route[i - 1];
            var to = route[i];

            if (!adjacency.TryGetValue(from, out var neighbours) || !neighbours.Contains(to))
                return RouteCheck.Invalid("not-adjacent");

            var next = nodes[to];

            if (next.Kind == PetBrainNodeKind.Blocked)
                return RouteCheck.Invalid("blocked");

            energy -= cost;

            // Enerji hərəkətdən SONRA yoxlanılır: sıfıra düşmək olar, mənfiyə yox.
            if (energy < 0)
                return RouteCheck.Invalid("out-of-energy");

            energy = Recharge(energy, maximum, next);

            // Hədəfə çatdıq — ondan sonra hərəkət olmamalıdır.
            if (next.Kind == PetBrainNodeKind.Goal && i != route.Count - 1)
                return RouteCheck.Invalid("passed-goal");
        }

        // MƏCBURİ düyünlər hədəfdən ƏVVƏL uğranmalıdır. Tələ yolu məhz burada
        // dayanır: Roboya çatır, amma antenanı bərpa etmir.
        var beforeGoal = route.Take(route.Count - 1).ToHashSet(StringComparer.Ordinal);

        foreach (var required in puzzle.RequiredBeforeGoal)
        {
            if (!beforeGoal.Contains(required))
                return RouteCheck.Invalid("missing-required");
        }

        return RouteCheck.Valid;
    }

    /// <summary>
    /// Etibarlı marşrutların sayı. Düyün sayı kiçikdir (≤8), ona görə tam
    /// sadalama ucuzdur və yeganəlik EVRİSTİKA ilə deyil, dəqiq bilinir.
    /// </summary>
    public static int CountSolutions(PetBrainPuzzleDto puzzle, int maxLength)
    {
        var start = puzzle.Nodes.FirstOrDefault(n => n.Kind == PetBrainNodeKind.Start);
        if (start is null)
            return 0;

        var adjacency = BuildAdjacency(puzzle.Edges);
        var found = 0;

        Walk([start.Id]);
        return found;

        void Walk(List<string> path)
        {
            if (path.Count > maxLength)
                return;

            if (Check(puzzle, path).IsValid)
                found++;

            if (!adjacency.TryGetValue(path[^1], out var neighbours))
                return;

            foreach (var next in neighbours.OrderBy(n => n, StringComparer.Ordinal))
            {
                if (path.Contains(next, StringComparer.Ordinal))
                    continue;

                path.Add(next);
                Walk(path);
                path.RemoveAt(path.Count - 1);
            }
        }
    }

    /// <summary>Ən qısa etibarlı marşrut — generatorun saxladığı həll.</summary>
    public static IReadOnlyList<string>? FindSolution(PetBrainPuzzleDto puzzle, int maxLength)
    {
        var start = puzzle.Nodes.FirstOrDefault(n => n.Kind == PetBrainNodeKind.Start);
        if (start is null)
            return null;

        var adjacency = BuildAdjacency(puzzle.Edges);
        List<string>? best = null;

        Walk([start.Id]);
        return best;

        void Walk(List<string> path)
        {
            if (path.Count > maxLength)
                return;

            if (Check(puzzle, path).IsValid && (best is null || path.Count < best.Count))
                best = [.. path];

            if (!adjacency.TryGetValue(path[^1], out var neighbours))
                return;

            foreach (var next in neighbours.OrderBy(n => n, StringComparer.Ordinal))
            {
                if (path.Contains(next, StringComparer.Ordinal))
                    continue;

                path.Add(next);
                Walk(path);
                path.RemoveAt(path.Count - 1);
            }
        }
    }

    /// <summary>Keçidlər İKİ istiqamətlidir — dron geri də qayıda bilər.</summary>
    private static Dictionary<string, HashSet<string>> BuildAdjacency(IEnumerable<PetBrainEdgeDto> edges)
    {
        Dictionary<string, HashSet<string>> adjacency = new(StringComparer.Ordinal);

        foreach (var edge in edges)
        {
            Add(edge.From, edge.To);
            Add(edge.To, edge.From);
        }

        return adjacency;

        void Add(string from, string to)
        {
            if (!adjacency.TryGetValue(from, out var set))
                adjacency[from] = set = new HashSet<string>(StringComparer.Ordinal);

            set.Add(to);
        }
    }

    private static int Recharge(int energy, int maximum, PetBrainNodeDto node) =>
        node.Kind == PetBrainNodeKind.Recharge
            ? Math.Min(maximum, energy + (node.EnergyDelta ?? 0))
            : energy;
}
