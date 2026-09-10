using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Story;

/// <summary>
/// Qrafın QURULUŞ yoxlaması — saf funksiya, I/O yoxdur.
///
/// <para>Budaqlanan hekayədə ən pis nasazlıq görünməzdir: uşaq bir seçim edir
/// və ekran boş qalır, çünki keçid mövcud olmayan düyünə gedir. Bu, yalnız
/// həmin yolu oynayanda üzə çıxır. Ona görə tərif kataloqa düşməzdən ƏVVƏL
/// bütöv yoxlanılır və qayda testdə tətbiq olunur.</para>
///
/// <para>Eyni yoxlayıcı sonradan modelin təklif etdiyi qraf üçün də işləyəcək
/// (bax <c>PetBrainV2:BoundedAiEnabled</c>): validator bir dənədir, mənbə isə
/// fərqli ola bilər.</para>
/// </summary>
public static class ExperienceGraphValidator
{
    /// <summary>Bir macərada icazə verilən ən çox düyün — sonsuz qraf gəlməsin.</summary>
    public const int MaxNodes = 64;

    /// <summary>Uşağın bir macərada atacağı ən çox addım.</summary>
    public const int MaxPathLength = 24;

    /// <summary>Bir macərada olmalı ən az sonluq sayı.</summary>
    public const int MinEndings = 2;

    /// <summary>Bütün pozuntular; boş siyahı = tərif etibarlıdır.</summary>
    public static IReadOnlyList<string> Validate(ExperienceDefinition definition)
    {
        List<string> problems = [];

        if (definition.Nodes.Count == 0)
        {
            problems.Add("Tərifdə heç bir düyün yoxdur.");
            return problems;
        }

        if (definition.Nodes.Count > MaxNodes)
            problems.Add($"Düyün sayı {MaxNodes} həddini keçir.");

        var ids = definition.Nodes.Select(n => n.Id).ToList();

        foreach (var duplicate in ids.GroupBy(id => id, StringComparer.Ordinal).Where(g => g.Count() > 1))
            problems.Add($"Düyün açarı təkrarlanır: {duplicate.Key}.");

        var index = definition.Nodes
            .GroupBy(n => n.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        if (!index.ContainsKey(definition.StartNodeId))
            problems.Add($"Başlanğıc düyün tapılmadı: {definition.StartNodeId}.");

        foreach (var node in definition.Nodes)
            ValidateNode(node, index, problems);

        ValidateReachability(definition, index, problems);
        ValidateEndings(definition, problems);
        ValidateDepth(definition, problems);

        return problems;
    }

    private static void ValidateNode(
        ExperienceNode node, IReadOnlyDictionary<string, ExperienceNode> index, List<string> problems)
    {
        foreach (var transition in node.Transitions)
        {
            if (!index.ContainsKey(transition.TargetNodeId))
                problems.Add($"«{node.Id}» naməlum düyünə keçir: {transition.TargetNodeId}.");

            if (transition.TargetNodeId == node.Id)
                problems.Add($"«{node.Id}» özünə keçir — uşaq eyni ekranda qalardı.");
        }

        if (node.IsEnding)
        {
            if (node.Transitions.Count > 0)
                problems.Add($"Sonluq «{node.Id}» hara isə keçir — sonluq son olmalıdır.");

            if (string.IsNullOrWhiteSpace(node.EndingKey))
                problems.Add($"Sonluq «{node.Id}» açarsızdır.");

            return;
        }

        // DALAN yoxlaması: sonluq olmayan hər düyünün çıxışı olmalıdır və
        // şərtlərin heç biri tutmayanda gedəcəyi bir yer qalmalıdır.
        if (node.Transitions.Count == 0)
        {
            problems.Add($"«{node.Id}» dalandır — çıxış keçidi yoxdur.");
            return;
        }

        if (!node.Transitions.Any(t => t.IsFallback))
            problems.Add($"«{node.Id}» üçün ehtiyat keçid yoxdur — şərtlər tutmasa ekran boş qalardı.");

        if (node.Kind == PetBrainStageKind.Choice && node.Options.Count == 0)
            problems.Add($"Seçim düyünü «{node.Id}» variantsızdır.");

        if (node.Kind != PetBrainStageKind.Choice && node.Options.Count > 0)
            problems.Add($"«{node.Id}» seçim düyünü deyil, amma variantları var.");

        // Hər variantın gedəcəyi yer olmalıdır: variant görünüb heç nəyi
        // dəyişmirsə, uşağın seçimi bəzəkdir.
        foreach (var option in node.Options)
        {
            var hasRoute = node.Transitions.Any(t =>
                string.Equals(t.RequiredOptionKey, option.Key, StringComparison.Ordinal));

            if (!hasRoute)
                problems.Add($"«{node.Id}» variantı «{option.Key}» heç bir keçidə bağlı deyil.");
        }
    }

    private static void ValidateReachability(
        ExperienceDefinition definition,
        IReadOnlyDictionary<string, ExperienceNode> index,
        List<string> problems)
    {
        if (!index.ContainsKey(definition.StartNodeId))
            return;

        HashSet<string> seen = new(StringComparer.Ordinal);
        Queue<string> queue = new();

        queue.Enqueue(definition.StartNodeId);
        seen.Add(definition.StartNodeId);

        while (queue.Count > 0)
        {
            var node = index[queue.Dequeue()];

            foreach (var transition in node.Transitions)
            {
                if (index.ContainsKey(transition.TargetNodeId) && seen.Add(transition.TargetNodeId))
                    queue.Enqueue(transition.TargetNodeId);
            }
        }

        foreach (var node in definition.Nodes.Where(n => !seen.Contains(n.Id)))
            problems.Add($"«{node.Id}» başlanğıcdan çatılmır — yazılıb, amma heç kim görməyəcək.");
    }

    private static void ValidateEndings(ExperienceDefinition definition, List<string> problems)
    {
        var endings = definition.Nodes.Where(n => n.IsEnding).ToList();

        if (endings.Count < MinEndings)
            problems.Add($"Ən azı {MinEndings} sonluq olmalıdır; indi {endings.Count} var.");

        var keys = endings.Select(n => n.EndingKey).ToList();

        foreach (var duplicate in keys
                     .Where(k => !string.IsNullOrWhiteSpace(k))
                     .GroupBy(k => k, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1))
            problems.Add($"Sonluq açarı təkrarlanır: {duplicate.Key}.");
    }

    private static void ValidateDepth(ExperienceDefinition definition, List<string> problems)
    {
        if (definition.LongestPath > MaxPathLength)
            problems.Add($"Ən uzun yol {MaxPathLength} addımı keçir.");
    }
}
