using System.Text.RegularExpressions;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// Generasiya nəticəsinin MÜSTƏQİL yoxlayıcısı.
///
/// <para>Qəsdən generatordan ayrıdır: generator səhv etsə (məsələn iki doğru
/// cavablı tapmaca qursa), onu tutan qat bu olmalıdır. Yoxlamadan keçməyən
/// namizəd uşağa GÖSTƏRİLMİR — deterministik yenidən cəhd, sonra isə hazır
/// ehtiyat variantı işə düşür.</para>
/// </summary>
public static partial class PuzzleValidator
{
    /// <summary>Element id-si: kiçik hərf, rəqəm, defis. Başqa heç nə.</summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,23}$")]
    private static partial Regex IdPattern();

    public const int MinItems = 3;
    public const int MaxItems = 8;
    public const int MaxLabelLength = 40;
    public const int MaxTextLength = 160;

    /// <summary>
    /// Texniki fraqmentlər — bunlar ALT SƏTİR kimi axtarılır, çünki söz deyil:
    /// link, teq və simvol istinadı mətnin ortasında da təhlükəlidir.
    /// </summary>
    private static readonly string[] BannedSubstrings =
    [
        "http://", "https://", "www.", "<", ">", "{", "}", "&#"
    ];

    /// <summary>
    /// Uşaq məzmununda olmamalı SÖZLƏR.
    ///
    /// <para><b>Tam söz kimi axtarılır, alt sətir kimi yox</b> — və bu, ölçülmüş
    /// bir səhvin nəticəsidir: alt sətir üsulu ilə <c>qan</c> qadağası
    /// <b>«qanad»</b> sözünü (əjdahanın qanadları!) bloklayırdı, yəni tamamilə
    /// təhlükəsiz tapmaca uşağa çatmırdı. Azərbaycan dilində qısa köklər başqa
    /// sözlərin içinə asanlıqla düşür, ona görə sərhəd yoxlaması məcburidir.</para>
    /// </summary>
    private static readonly string[] BannedWords =
    [
        "öl", "ölüm", "qan", "silah", "qorxu", "dəhşət", "xəstə",
        "kill", "death", "blood", "weapon", "gun", "scary", "horror",
        "password", "şifrə", "ünvan", "address", "telefon", "phone", "email"
    ];

    /// <summary>Qadağan olunmuş sözlərin tam söz kimi yoxlanması.</summary>
    private static readonly Regex BannedWordPattern = new(
        $@"\b(?:{string.Join('|', BannedWords.Select(Regex.Escape))})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Uğursuzluq səbəbi — loga düşür, uşağa GÖSTƏRİLMİR.</summary>
    public static string? Validate(PuzzleBlueprint blueprint, PetBrainPuzzleDto puzzle, PuzzleSolution solution)
    {
        // ---- Mexanika və sxem ----
        if (!PuzzleBlueprintCatalog.IsKnown(blueprint.Key))
            return "unknown-blueprint";

        if (puzzle.Mechanic != blueprint.Mechanic)
            return "mechanic-mismatch";

        if (puzzle.AnswerSchema.Kind != blueprint.AnswerKind)
            return "schema-mismatch";

        // ---- Lövhə ----
        // Qraf lövhəsində element siyahısı YOXDUR: onun yerinə düyünlər və
        // keçidlər gəlir, ona görə yoxlamalar da ayrıdır.
        List<string> ids;

        if (blueprint.IsGraphBoard)
        {
            var board = ValidateBoard(puzzle);
            if (board is not null)
                return board;

            ids = puzzle.Nodes.Select(n => n.Id).ToList();
        }
        else
        {
            if (puzzle.Items.Count < MinItems || puzzle.Items.Count > ItemCap(blueprint))
                return "item-count";

            ids = puzzle.Items.Select(i => i.Id).ToList();

            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
                return "duplicate-item-id";

            if (ids.Any(id => !IdPattern().IsMatch(id)))
                return "bad-item-id";

            foreach (var item in puzzle.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Label) || item.Label.Length > MaxLabelLength)
                    return "bad-label";

                // Rəng tək daşıyıcı olmamalıdır — forma işarəsi məcburidir.
                if (!PuzzleBlueprintCatalog.Shapes.Contains(item.Shape))
                    return "bad-shape";
            }

            if (blueprint.Mechanic == PetBrainPuzzleMechanic.PictureAssembly && ValidatePicture(puzzle) is { } picture)
                return picture;
        }

        // ---- Mətn ----
        if (string.IsNullOrWhiteSpace(puzzle.Title) || puzzle.Title.Length > MaxLabelLength * 2)
            return "bad-title";

        if (string.IsNullOrWhiteSpace(puzzle.Instruction) || puzzle.Instruction.Length > MaxTextLength)
            return "bad-instruction";

        if (puzzle.StoryPrompt.Length > MaxTextLength)
            return "bad-story-prompt";

        foreach (var text in TextsOf(puzzle))
        {
            if (BannedSubstrings.Any(f => text.Contains(f, StringComparison.OrdinalIgnoreCase)))
                return "unsafe-text";

            if (BannedWordPattern.IsMatch(text))
                return "unsafe-text";
        }

        // ---- Həll ----
        if (solution.Ids.Count == 0)
            return "empty-solution";

        if (solution.Ids.Distinct(StringComparer.Ordinal).Count() != solution.Ids.Count)
            return "duplicate-solution-id";

        if (solution.Ids.Any(id => !ids.Contains(id, StringComparer.Ordinal)))
            return "solution-outside-items";

        if (solution.Kind != blueprint.AnswerKind)
            return "solution-kind";

        if (solution.Ids.Count < puzzle.AnswerSchema.Min || solution.Ids.Count > puzzle.AnswerSchema.Max)
            return "solution-count";

        // ---- Yeganəlik ----
        // Tək cavablı müqavilədə BİRDƏN ÇOX doğru həll olmamalıdır: uşaq
        // "doğru" cavab verib "səhvdir" eşitsəydi, bu, ən pis geri dönüş olardı.
        if (!blueprint.LowPressure && CountSolutions(blueprint, puzzle) != 1)
            return "not-unique";

        // ---- Müstəqil qiymətləndirici təsdiqi ----
        // Saxlanan həll ELƏ qiymətləndirici ilə yoxlanılır ki, cavab yolu ilə
        // generasiya yolu bir-birindən asılı qalmasın.
        var confirmed = PuzzleAnswerEvaluator.Evaluate(blueprint, puzzle, solution, solution.Ids);
        if (!confirmed.IsCorrect)
            return "evaluator-disagrees";

        // ---- Cavab sızması ----
        // Təzyiqsiz yolda gizlədiləsi cavab yoxdur.
        //
        // Qraf lövhəsi də kənardadır və bu, QƏSDƏNDİR: orada gizli söz yoxdur.
        // Hekayə uşağa açıq deyir ki, antenaya uğramaq lazımdır, düyünlərin
        // adları isə onsuz da lövhədə yazılıb. Çətinlik sözü tapmaqda deyil —
        // enerjini bölməkdə və tələ yolunu görməkdədir. Bu yoxlamanı ora da
        // tətbiq etsək, hekayəni izah edən hər cümləni «sızma» sayardıq.
        if (!blueprint.LowPressure && !blueprint.IsGraphBoard && LeaksAnswer(puzzle, solution))
            return "answer-leak";

        return null;
    }

    /// <summary>Qraf lövhəsinin quruluş yoxlaması — düyünlər, keçidlər, enerji.</summary>
    private static string? ValidateBoard(PetBrainPuzzleDto puzzle)
    {
        var nodes = puzzle.Nodes;

        if (nodes.Count < MinItems || nodes.Count > MaxItems)
            return "node-count";

        var ids = nodes.Select(n => n.Id).ToList();

        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            return "duplicate-node-id";

        if (ids.Any(id => !IdPattern().IsMatch(id)))
            return "bad-node-id";

        if (nodes.Count(n => n.Kind == PetBrainNodeKind.Start) != 1)
            return "start-count";

        if (nodes.Count(n => n.Kind == PetBrainNodeKind.Goal) != 1)
            return "goal-count";

        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Label) || node.Label.Length > MaxLabelLength)
                return "bad-label";

            // Mövqe normallaşdırılmışdır — overlay bunu faizlə yerləşdirir.
            if (node.X is < 0 or > 100 || node.Y is < 0 or > 100)
                return "bad-position";

            // Doldurma düyünü enerji VERMƏLİDİR, başqa düyün isə verməməlidir:
            // əks halda lövhədəki işarə uşağı aldadardı.
            if (node.Kind == PetBrainNodeKind.Recharge)
            {
                if (node.EnergyDelta is null or <= 0)
                    return "bad-recharge";
            }
            else if (node.EnergyDelta is not null)
            {
                return "unexpected-energy";
            }
        }

        var known = ids.ToHashSet(StringComparer.Ordinal);
        HashSet<string> pairs = new(StringComparer.Ordinal);

        foreach (var edge in puzzle.Edges)
        {
            if (!known.Contains(edge.From) || !known.Contains(edge.To))
                return "edge-outside-board";

            if (string.Equals(edge.From, edge.To, StringComparison.Ordinal))
                return "self-edge";

            // Keçid iki istiqamətlidir, ona görə cüt sıradan ASILI OLMADAN
            // təkrar sayılır.
            var canonical = string.CompareOrdinal(edge.From, edge.To) < 0
                ? $"{edge.From}|{edge.To}"
                : $"{edge.To}|{edge.From}";

            if (!pairs.Add(canonical))
                return "duplicate-edge";
        }

        if (puzzle.InitialEnergy is null or < 0)
            return "bad-energy";

        if (puzzle.MaximumEnergy is null || puzzle.MaximumEnergy < puzzle.InitialEnergy)
            return "bad-maximum-energy";

        if (puzzle.MoveCost is null or < 1)
            return "bad-move-cost";

        foreach (var required in puzzle.RequiredBeforeGoal)
        {
            var node = nodes.FirstOrDefault(n => string.Equals(n.Id, required, StringComparison.Ordinal));

            if (node is null)
                return "required-outside-board";

            // Məcburi düyün lövhədə də MƏCBURİ görünməlidir — gizli şərt yoxdur.
            if (node.Kind != PetBrainNodeKind.Required)
                return "required-not-marked";
        }

        return null;
    }

    /// <summary>
    /// Neçə etibarlı həll var. Element sayı kiçikdir (≤8), ona görə tam
    /// sadalama ucuzdur və dəqiqdir — evristikaya ehtiyac yoxdur.
    /// </summary>
    public static int CountSolutions(PuzzleBlueprint blueprint, PetBrainPuzzleDto puzzle)
    {
        var items = puzzle.Items;

        return blueprint.Mechanic switch
        {
            // Marşrut: bütün sadə yollar sadalanır və QAYDALAR tətbiq edilir.
            // Yeganəlik burada evristika deyil, sayılmış faktdır.
            PetBrainPuzzleMechanic.OrderedRoute =>
                RouteRules.CountSolutions(puzzle, puzzle.AnswerSchema.Max),

            // Sıralama: dəyərlər fərqlidirsə yeganə artan sıra var.
            PetBrainPuzzleMechanic.SequenceOrder =>
                items.Select(i => i.Value).Distinct().Count() == items.Count ? 1 : 2,

            PetBrainPuzzleMechanic.RouteLogic => CountShortestOpenRoutes(items),

            PetBrainPuzzleMechanic.SignalPattern =>
                items.Select(i => i.Value).Distinct().Count() == items.Count ? 1 : 2,

            PetBrainPuzzleMechanic.ObservationRecall =>
                items.Count(i => string.Equals(i.Icon, ChangedMarkIcon, StringComparison.Ordinal))
                == puzzle.AnswerSchema.Min ? 1 : 2,

            PetBrainPuzzleMechanic.MatchingPairs =>
                items.Select(i => i.Value).Distinct().Count() == items.Count
                && puzzle.MatchTargets.Select(t => t.Value).Distinct().Count() == puzzle.MatchTargets.Count
                && puzzle.MatchTargets.Count == items.Count
                    ? 1
                    : 2,

            PetBrainPuzzleMechanic.PictureAssembly =>
                items.Select(i => i.Value).Distinct().Count() == items.Count ? 1 : 2,

            _ => 1
        };
    }

    /// <summary>
    /// Element tavanı. Şəkil yığımı 12 parçaya qədər gedir — onun elementləri
    /// seçim siyahısı deyil, bir şəklin hissələridir; cavabın ümumi tavanı isə
    /// yenə qorunur.
    /// </summary>
    private static int ItemCap(PuzzleBlueprint blueprint) =>
        blueprint.Mechanic == PetBrainPuzzleMechanic.PictureAssembly
            ? Math.Min(blueprint.MaxItems, PuzzleAnswerEvaluator.MaxAnswerIds)
            : Math.Min(MaxItems, blueprint.MaxItems);

    /// <summary>
    /// Şəkil yığımının quruluşu: çərçivə ölçüsü icazəlidir, hər yer tam bir
    /// parçaya aiddir və qab yığılmış halda gəlmir — yoxsa tapmaca olmazdı.
    /// </summary>
    private static string? ValidatePicture(PetBrainPuzzleDto puzzle)
    {
        if (puzzle.GridColumns is not (>= 2 and <= 4) || puzzle.GridRows is not (>= 2 and <= 4))
            return "bad-grid";

        var count = puzzle.GridColumns.Value * puzzle.GridRows.Value;

        if (puzzle.Items.Count != count)
            return "grid-item-mismatch";

        var slots = puzzle.Items.Select(i => i.Value ?? -1).ToList();

        if (slots.Any(slot => slot < 0 || slot >= count) || slots.Distinct().Count() != count)
            return "bad-tile-slot";

        if (slots.SequenceEqual(Enumerable.Range(0, count)))
            return "already-assembled";

        return null;
    }

    /// <summary>Müşahidə tapmacasında dəyişmiş izin GÖRÜNƏN nişanı.</summary>
    public const string ChangedMarkIcon = "❔";

    /// <summary>
    /// Marşrut qaydası: <b>açıq</b> yollar arasında <b>ən qısası</b>.
    ///
    /// <para>Qayda GÖRÜNƏN məlumatdan hesablanır — bağlılıq ikonla, uzunluq isə
    /// dəyərlə bildirilir. Gizli "doğru" bayrağı YOXDUR: olsaydı, klient onu
    /// oxuyub cavabı çıxarardı.</para>
    /// </summary>
    private static int CountShortestOpenRoutes(IReadOnlyList<PetBrainPuzzleItemDto> items)
    {
        var open = items
            .Where(i => !string.Equals(i.Icon, PuzzleBlueprintCatalog.HazardIcon, StringComparison.Ordinal))
            .ToList();

        if (open.Count == 0)
            return 0;

        var shortest = open.Min(i => i.Value ?? int.MaxValue);
        return open.Count(i => (i.Value ?? int.MaxValue) == shortest);
    }

    /// <summary>
    /// Cavab görünən mətndən oxuna bilirmi. Məsələn ipucu birbaşa doğru
    /// elementin adını yazsaydı, tapmaca mənasını itirərdi.
    /// </summary>
    private static bool LeaksAnswer(PetBrainPuzzleDto puzzle, PuzzleSolution solution)
    {
        var visible = string.Join(' ', TextsOf(puzzle));

        return solution.Ids.Any(id => visible.Contains(id, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> TextsOf(PetBrainPuzzleDto puzzle)
    {
        yield return puzzle.Title;
        yield return puzzle.Instruction;

        if (!string.IsNullOrEmpty(puzzle.StoryPrompt))
            yield return puzzle.StoryPrompt;

        if (!string.IsNullOrEmpty(puzzle.Hint))
            yield return puzzle.Hint;

        if (!string.IsNullOrEmpty(puzzle.Scene.AltText))
            yield return puzzle.Scene.AltText;

        foreach (var item in puzzle.Items)
            yield return item.Label;

        // Düyün adları da uşağa GÖRÜNÜR — deməli eyni süzgəcdən keçməlidir.
        foreach (var node in puzzle.Nodes)
            yield return node.Label;
    }
}
