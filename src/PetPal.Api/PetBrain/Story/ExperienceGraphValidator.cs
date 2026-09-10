using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.Pets;
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
    public const int MaxNodes = 120;

    /// <summary>
    /// Uşağın bir macərada atacağı ən çox addım.
    ///
    /// <para>Hədd chapter-lə birlikdə qalxdı: altı chapter-lik macərada uzun
    /// variant 60-a yaxın addım atır. Hədd yenə lazımdır — o, sonsuz qrafı
    /// deyil, <b>uşağın vaxtını</b> qoruyur.</para>
    /// </summary>
    public const int MaxPathLength = 80;

    /// <summary>Bir chapter-in icazə verilən ən çox təxmini müddəti.</summary>
    public const int MaxChapterMinutes = 12;

    /// <summary>Bir macərada olmalı ən az sonluq sayı.</summary>
    public const int MinEndings = 2;

    /// <summary>
    /// Uşağa VARIANT təqdim edən ekran növləri.
    ///
    /// <para>Hamısı eyni server yolundan keçir (variant açarı yoxlanılır,
    /// effektlər tətbiq olunur) və fərqi yalnız TƏQDİMATDADIR: kəşf səhnəsi
    /// baxış nöqtələri, yol seçimi isə iki marşrut göstərir. Bir enum dəyəri
    /// əvəzinə bir neçəsinin olması UI-a hər birini öz dili ilə çəkməyə imkan
    /// verir — «Davam et» düyməli eynilik məhz bundan yaranırdı.</para>
    /// </summary>
    public static bool RequiresOptions(PetBrainStageKind kind) => kind is
        PetBrainStageKind.Choice
        or PetBrainStageKind.Exploration
        or PetBrainStageKind.RouteSelection;

    /// <summary>
    /// Variant DAŞIYA BİLƏN növlər.
    ///
    /// <para><see cref="RequiresOptions"/>-dan geniş olması qəsdəndir: bir
    /// obyektlə qarşılıqlı təsir sual verə də bilər («təmir edək?»), verməyə
    /// də — «robot artıq işləyir» səhnəsi kimi. Qayda ekranın nə OLDUĞUNU
    /// deyil, nə TƏLƏB ETDİYİNİ yoxlayır.</para>
    /// </summary>
    public static bool MayPresentOptions(PetBrainStageKind kind) =>
        RequiresOptions(kind)
        || kind is PetBrainStageKind.ObjectInteraction
            or PetBrainStageKind.Investigation
            or PetBrainStageKind.Building
            or PetBrainStageKind.Navigation
            or PetBrainStageKind.Caring
            or PetBrainStageKind.CooperativePetAction;

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
        ValidateChapters(definition, index, problems);
        ValidateContentKeys(definition, problems);
        ValidateReferences(definition, index, problems);
        ValidateItemsMatter(definition, problems);
        ValidateVariantsPlayable(definition, index, problems);
        ValidateAcyclic(definition, index, problems);
        ValidatePuzzles(definition, problems);
        ValidateRewards(definition, problems);
        ValidateObjectivesCompletable(definition, problems);

        return problems;
    }

    /// <summary>
    /// HƏR məqsəd — yan tapşırıq da — hansısa düyündə və ya variantda
    /// tamamlana bilməlidir.
    ///
    /// <para>Əsas məqsəd fəsil qaydasında yoxlanılır; yan tapşırıq isə orada
    /// yoxlanılmırdı və nəticədə «roverin yaddaşını bərpa et» heç vaxt
    /// bağlanmırdı: uşaq onu etsə də, fəslin yekununda «buraxılıb» kimi
    /// görünürdü. Tamamlanmayan məqsəd uşağa verilmiş yalan vəddir.</para>
    /// </summary>
    private static void ValidateObjectivesCompletable(ExperienceDefinition definition, List<string> problems)
    {
        var completing = definition.Nodes
            .SelectMany(n => n.Effects.Concat(n.Options.SelectMany(o => o.Effects)))
            .Where(e => e.Kind is ExperienceEffectKind.CompleteObjective or ExperienceEffectKind.AdvanceObjective)
            .Select(e => e.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var objective in definition.Objectives.Where(o => !completing.Contains(o.ObjectiveId)))
            problems.Add($"«{objective.ObjectiveId}» məqsədi heç yerdə tamamlana bilmir.");
    }

    /// <summary>
    /// Qrafda DÖVRƏ olmamalıdır — uşaq eyni səhnələri sonsuz görə bilməz.
    ///
    /// <para>Ehtiyat keçid hər düyündə məcburi olduğu üçün dövrə həmişə
    /// «keçilə bilən» görünür: dalan yoxdur, amma çıxış da yoxdur. Dövrəni
    /// açıq-aşkar axtarmaq lazımdır, çünki digər qaydaların heç biri onu
    /// tutmur.</para>
    /// </summary>
    private static void ValidateAcyclic(
        ExperienceDefinition definition,
        IReadOnlyDictionary<string, ExperienceNode> index,
        List<string> problems)
    {
        Dictionary<string, int> marks = new(StringComparer.Ordinal);

        foreach (var node in definition.Nodes)
        {
            if (Visit(node.Id) is { } loop)
            {
                problems.Add($"Qrafda dövrə var: «{loop}» özünə qayıdır — uşaq eyni səhnələri sonsuz görə bilər.");
                return;
            }
        }

        string? Visit(string nodeId)
        {
            if (!index.TryGetValue(nodeId, out var node))
                return null;

            switch (marks.GetValueOrDefault(nodeId))
            {
                case 1:
                    return nodeId;
                case 2:
                    return null;
            }

            marks[nodeId] = 1;

            foreach (var target in Reachable(definition, node))
            {
                if (string.Equals(target, nodeId, StringComparison.Ordinal))
                    continue;

                if (Visit(target) is { } loop)
                    return loop;
            }

            marks[nodeId] = 2;

            return null;
        }
    }

    /// <summary>
    /// Tapmaca düyününün istədiyi şablon QEYDİYYATDA olmalıdır və bu macəraya
    /// UYĞUN gəlməlidir.
    ///
    /// <para>Naməlum şablon generatorda ehtiyat variantına düşürdü və uşaq Ay
    /// macərasının ortasında hekayədən kənar bir tapmaca görürdü — xəta yox,
    /// sadəcə mənasız ekran. Burada bu, tərif mərhələsində tutulur.</para>
    /// </summary>
    private static void ValidatePuzzles(ExperienceDefinition definition, List<string> problems)
    {
        foreach (var node in definition.Nodes.Where(n => n.Kind == PetBrainStageKind.Puzzle))
        {
            if (string.IsNullOrEmpty(node.PuzzleFamily))
                continue;

            if (PuzzleBlueprintCatalog.Find(node.PuzzleFamily) is not { } blueprint)
            {
                problems.Add($"«{node.Id}» naməlum tapmaca şablonu istəyir: {node.PuzzleFamily}.");
                continue;
            }

            if (!blueprint.SupportsExperience(definition.Key))
                problems.Add($"«{node.Id}» tapmacası «{node.PuzzleFamily}» bu macəraya aid deyil.");

            if (definition.AllowedPuzzleFamilies.Count > 0
                && !definition.AllowedPuzzleFamilies.Contains(node.PuzzleFamily, StringComparer.Ordinal))
                problems.Add($"«{node.Id}» tapmacası «{node.PuzzleFamily}» icazəli siyahıda yoxdur.");
        }
    }

    /// <summary>
    /// Sonluğun vəd etdiyi kosmetik MÖVCUD olmalıdır və yalnız macəra ilə
    /// verilən növdən olmalıdır.
    ///
    /// <para>Yazılış səhvi olan kod heç bir xəta vermir — uşaq sadəcə yekun
    /// ekranında vəd edilən nişanı almır. Qırx dəqiqəlik macəranın sonunda
    /// bu, ən pis sürprizdir.</para>
    /// </summary>
    private static void ValidateRewards(ExperienceDefinition definition, List<string> problems)
    {
        foreach (var ending in definition.Endings.Where(e => !string.IsNullOrEmpty(e.RewardCode)))
        {
            var accessory = PetAccessories.Find(ending.RewardCode);

            if (accessory is null)
                problems.Add($"«{ending.EndingKey}» sonluğu naməlum mükafat vəd edir: {ending.RewardCode}.");
            else if (accessory.Unlock != AccessoryUnlock.Experience)
                problems.Add($"«{ending.EndingKey}» sonluğunun mükafatı macəra mükafatı deyil: {ending.RewardCode}.");
        }
    }

    /// <summary>
    /// Chapter qaydaları.
    ///
    /// <para>Ən vacibi sonuncudur: <b>hər chapter checkpoint-ə çatmalıdır</b>.
    /// Checkpoint-siz chapter uşağın 10 dəqiqəsini girov saxlayır — dayandırıb
    /// qayıdanda o, chapter-in əvvəlinə atılır.</para>
    /// </summary>
    private static void ValidateChapters(
        ExperienceDefinition definition,
        IReadOnlyDictionary<string, ExperienceNode> index,
        List<string> problems)
    {
        if (!definition.HasChapters)
            return;

        foreach (var duplicate in definition.Chapters
                     .GroupBy(c => c.ChapterId, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1))
            problems.Add($"Chapter açarı təkrarlanır: {duplicate.Key}.");

        foreach (var duplicate in definition.Chapters
                     .GroupBy(c => c.Order)
                     .Where(g => g.Count() > 1))
            problems.Add($"Chapter sırası təkrarlanır: {duplicate.Key}.");

        foreach (var chapter in definition.Chapters)
        {
            if (!index.ContainsKey(chapter.StartNodeId))
                problems.Add($"«{chapter.ChapterId}» chapter-inin başlanğıc düyünü yoxdur: {chapter.StartNodeId}.");

            if (chapter.EstimatedMinutes is < 1 or > MaxChapterMinutes)
                problems.Add(
                    $"«{chapter.ChapterId}» üçün {chapter.EstimatedMinutes} dəqiqə uyğun deyil — "
                    + $"hədd 1–{MaxChapterMinutes}.");

            var nodes = definition.NodesOf(chapter.ChapterId);

            if (nodes.Count == 0)
            {
                problems.Add($"«{chapter.ChapterId}» chapter-ində düyün yoxdur.");
                continue;
            }

            if (!nodes.Any(n => n.IsCheckpoint))
                problems.Add($"«{chapter.ChapterId}» checkpoint-ə çatmır — uşaq dayanıb davam edə bilməz.");

            if (!nodes.Any(n => n.Kind == PetBrainStageKind.Choice))
                problems.Add($"«{chapter.ChapterId}» heç bir seçim təqdim etmir.");

            foreach (var objectiveId in chapter.MainObjectiveIds.Concat(chapter.OptionalObjectiveIds))
            {
                if (definition.Objective(objectiveId) is null)
                    problems.Add($"«{chapter.ChapterId}» naməlum məqsədə istinad edir: {objectiveId}.");
            }

            foreach (var objectiveId in chapter.MainObjectiveIds)
            {
                var completed = nodes.Any(n => n.Effects.Any(e =>
                    e.Kind is ExperienceEffectKind.CompleteObjective or ExperienceEffectKind.AdvanceObjective
                    && string.Equals(e.Key, objectiveId, StringComparison.Ordinal)));

                if (!completed)
                    problems.Add(
                        $"«{objectiveId}» əsas məqsədi «{chapter.ChapterId}» daxilində tamamlana bilmir.");
            }
        }

        foreach (var node in definition.Nodes.Where(n => !string.IsNullOrEmpty(n.ChapterId)))
        {
            if (definition.Chapter(node.ChapterId) is null)
                problems.Add($"«{node.Id}» naməlum chapter-ə aiddir: {node.ChapterId}.");
        }

        foreach (var node in definition.Nodes.Where(n => !string.IsNullOrEmpty(n.CompletesChapterId)))
        {
            if (definition.Chapter(node.CompletesChapterId) is null)
                problems.Add($"«{node.Id}» naməlum chapter-i bağlayır: {node.CompletesChapterId}.");
        }

        foreach (var chapter in definition.Chapters)
        {
            if (!definition.Nodes.Any(n =>
                    string.Equals(n.CompletesChapterId, chapter.ChapterId, StringComparison.Ordinal)))
                problems.Add($"«{chapter.ChapterId}» heç bir düyündə bağlanmır.");
        }
    }

    /// <summary>Uşağa GÖRÜNƏN hər mətn hər iki dildə dolu olmalıdır.</summary>
    private static void ValidateContentKeys(ExperienceDefinition definition, List<string> problems)
    {
        foreach (var node in definition.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.PromptAz) || string.IsNullOrWhiteSpace(node.PromptEn))
                problems.Add($"«{node.Id}» üçün başlıq bir dildə yoxdur.");

            if (string.IsNullOrWhiteSpace(node.PetLineAz) || string.IsNullOrWhiteSpace(node.PetLineEn))
                problems.Add($"«{node.Id}» üçün pet replikası bir dildə yoxdur.");

            foreach (var option in node.Options)
            {
                if (string.IsNullOrWhiteSpace(option.LabelAz) || string.IsNullOrWhiteSpace(option.LabelEn))
                    problems.Add($"«{node.Id}» variantı «{option.Key}» bir dildə adsızdır.");
            }
        }

        foreach (var item in definition.Items)
        {
            if (string.IsNullOrWhiteSpace(item.NameAz) || string.IsNullOrWhiteSpace(item.NameEn))
                problems.Add($"«{item.ItemId}» əşyası bir dildə adsızdır.");
        }

        foreach (var clue in definition.Clues)
        {
            if (string.IsNullOrWhiteSpace(clue.TitleAz) || string.IsNullOrWhiteSpace(clue.TitleEn))
                problems.Add($"«{clue.ClueId}» ipucusu bir dildə adsızdır.");
        }
    }

    /// <summary>
    /// Effekt və şərtlərin toxunduğu HƏR açar tərifdə mövcud olmalıdır.
    ///
    /// <para>Yazılış səhvi olan açar (məsələn <c>moon-lens</c> əvəzinə
    /// <c>moon-lense</c>) heç bir xəta vermir — sadəcə əşya heç vaxt düşmür və
    /// uşaq bir saat sonra bağlı qapı qarşısında qalır. Bu yoxlama məhz onu
    /// tutur.</para>
    /// </summary>
    private static void ValidateReferences(
        ExperienceDefinition definition,
        IReadOnlyDictionary<string, ExperienceNode> index,
        List<string> problems)
    {
        foreach (var duplicate in definition.Objectives
                     .GroupBy(o => o.ObjectiveId, StringComparer.Ordinal).Where(g => g.Count() > 1))
            problems.Add($"Məqsəd açarı təkrarlanır: {duplicate.Key}.");

        foreach (var duplicate in definition.Items
                     .GroupBy(i => i.ItemId, StringComparer.Ordinal).Where(g => g.Count() > 1))
            problems.Add($"Əşya açarı təkrarlanır: {duplicate.Key}.");

        foreach (var duplicate in definition.Clues
                     .GroupBy(c => c.ClueId, StringComparer.Ordinal).Where(g => g.Count() > 1))
            problems.Add($"İpucu açarı təkrarlanır: {duplicate.Key}.");

        foreach (var node in definition.Nodes)
        {
            foreach (var effect in node.Effects)
                CheckEffect(node.Id, effect);

            foreach (var option in node.Options)
            {
                foreach (var effect in option.Effects)
                    CheckEffect($"{node.Id}/{option.Key}", effect);

                CheckCondition($"{node.Id}/{option.Key}", option.Requires);
            }

            foreach (var transition in node.Transitions)
                CheckCondition($"{node.Id}→{transition.TargetNodeId}", transition.Requires);

            CheckCondition(node.Id, node.Requires);
        }

        foreach (var ending in definition.Endings)
        {
            if (!index.TryGetValue(ending.NodeId, out var node))
                problems.Add($"«{ending.EndingKey}» sonluğu naməlum düyünə bağlıdır: {ending.NodeId}.");
            else if (!node.IsEnding)
                problems.Add($"«{ending.EndingKey}» sonluq olmayan düyünə bağlıdır: {ending.NodeId}.");
        }

        void CheckEffect(string owner, ExperienceEffect effect)
        {
            var missing = effect.Kind switch
            {
                ExperienceEffectKind.GrantItem or ExperienceEffectKind.ConsumeItem =>
                    definition.Item(effect.Key) is null ? "əşya" : null,

                ExperienceEffectKind.DiscoverClue =>
                    definition.Clue(effect.Key) is null ? "ipucu" : null,

                ExperienceEffectKind.StartObjective
                    or ExperienceEffectKind.AdvanceObjective
                    or ExperienceEffectKind.CompleteObjective
                    or ExperienceEffectKind.SkipOptionalObjective =>
                    definition.Objective(effect.Key) is null ? "məqsəd" : null,

                ExperienceEffectKind.CompleteChapter =>
                    definition.Chapter(effect.Key) is null ? "chapter" : null,

                ExperienceEffectKind.ScoreEnding =>
                    definition.Endings.Count > 0 && definition.Ending(effect.Key) is null ? "sonluq" : null,

                _ => null
            };

            if (missing is not null)
                problems.Add($"«{owner}» naməlum {missing} açarına toxunur: {effect.Key}.");

            if (effect.Kind == ExperienceEffectKind.SkipOptionalObjective
                && definition.Objective(effect.Key) is { IsOptional: false })
                problems.Add($"«{owner}» ƏSAS məqsədi buraxmağa çalışır: {effect.Key}.");
        }

        void CheckCondition(string owner, ExperienceCondition condition)
        {
            foreach (var (kind, key) in condition.References())
            {
                var known = kind switch
                {
                    "item" => definition.Item(key) is not null,
                    "clue" => definition.Clue(key) is not null,
                    "objective" => definition.Objective(key) is not null,
                    "chapter" => definition.Chapter(key) is not null,
                    "node" => index.ContainsKey(key),
                    "variant" => AdventureVariants.IsKnown(key),
                    _ => true
                };

                if (!known)
                    problems.Add($"«{owner}» şərti naməlum {kind} açarını oxuyur: {key}.");
            }
        }
    }

    /// <summary>
    /// Hər əşya və ipucu bir şeyi DƏYİŞMƏLİDİR.
    ///
    /// <para>Yalnız verilib heç yerdə oxunmayan əşya mənasız kolleksiyadır:
    /// uşaq onu daşıyır, amma o, heç bir qapını açmır. Qayda tərifdə tutulur,
    /// çünki oyun içində bunu görmək üçün bütün yolları oynamaq lazımdır.</para>
    /// </summary>
    private static void ValidateItemsMatter(ExperienceDefinition definition, List<string> problems)
    {
        var readKeys = definition.Nodes
            .SelectMany(n => n.Transitions.Select(t => t.Requires)
                .Concat(n.Options.Select(o => o.Requires))
                .Append(n.Requires))
            .SelectMany(c => c.References())
            .ToList();

        foreach (var item in definition.Items)
        {
            if (item.IsQuestItem)
                continue;

            var consumed = definition.Nodes.SelectMany(n => n.Effects.Concat(n.Options.SelectMany(o => o.Effects)))
                .Any(e => e.Kind == ExperienceEffectKind.ConsumeItem
                          && string.Equals(e.Key, item.ItemId, StringComparison.Ordinal));

            var read = readKeys.Any(r => r.Kind == "item"
                                         && string.Equals(r.Key, item.ItemId, StringComparison.Ordinal));

            if (!read && !consumed)
                problems.Add($"«{item.ItemId}» əşyası heç yerdə oxunmur — mənasız kolleksiya.");
        }

        foreach (var clue in definition.Clues)
        {
            var read = readKeys.Any(r => r.Kind == "clue"
                                         && string.Equals(r.Key, clue.ClueId, StringComparison.Ordinal));

            if (!read && string.IsNullOrEmpty(clue.RelatedPuzzleNodeId))
                problems.Add($"«{clue.ClueId}» ipucusu heç bir keçidə və ya tapmacaya bağlı deyil.");

            if (!string.IsNullOrEmpty(clue.RelatedPuzzleNodeId)
                && definition.Find(clue.RelatedPuzzleNodeId) is not { Kind: PetBrainStageKind.Puzzle })
                problems.Add($"«{clue.ClueId}» mövcud olmayan tapmacaya bağlıdır: {clue.RelatedPuzzleNodeId}.");
        }

        ValidateQuestItemsReachable(definition, problems);
    }

    /// <summary>
    /// HEKAYƏ əşyaları hər sonluq yolunda ƏLDƏ EDİLƏ BİLİR.
    ///
    /// <para>«Oxunurmu» sualından güclü tələbdir: uşaq finala kristalın üç
    /// parçası olmadan gəlsəydi, macəra texniki olaraq bitərdi, hekayə isə
    /// yalan olardı. Yoxlama başlanğıcdan hər sonluğa gedən yolları gəzir və
    /// yolda əşyanın verilib-verilmədiyinə baxır.</para>
    ///
    /// <para>Yol sayı üstlü arta bilər, ona görə hesablama düyün başına bir
    /// dəfə aparılır: «bu düyünə çatanda hansı əşyaların HAMISI mütləq
    /// əlimdədir» çoxluğu irəli daşınır və birləşmədə KƏSİŞMƏ götürülür.</para>
    /// </summary>
    private static void ValidateQuestItemsReachable(ExperienceDefinition definition, List<string> problems)
    {
        var questItems = definition.Items.Where(i => i.IsQuestItem).Select(i => i.ItemId).ToList();

        if (questItems.Count == 0 || definition.Find(definition.StartNodeId) is null)
            return;

        Dictionary<string, HashSet<string>> guaranteed = new(StringComparer.Ordinal);
        Queue<string> queue = new();

        guaranteed[definition.StartNodeId] = GrantedBy(definition.Find(definition.StartNodeId)!);
        queue.Enqueue(definition.StartNodeId);

        var guard = 0;

        while (queue.Count > 0 && guard++ < MaxNodes * MaxNodes)
        {
            var nodeId = queue.Dequeue();
            var node = definition.Find(nodeId)!;
            var carried = guaranteed[nodeId];

            foreach (var transition in node.Transitions)
            {
                if (definition.Find(transition.TargetNodeId) is not { } target)
                    continue;

                var incoming = new HashSet<string>(carried, StringComparer.Ordinal);
                incoming.UnionWith(GrantedBy(target));

                if (guaranteed.TryGetValue(target.Id, out var known))
                {
                    var before = known.Count;
                    known.IntersectWith(incoming);

                    if (known.Count == before)
                        continue;
                }
                else
                {
                    guaranteed[target.Id] = incoming;
                }

                queue.Enqueue(target.Id);
            }
        }

        foreach (var ending in definition.Nodes.Where(n => n.IsEnding))
        {
            if (!guaranteed.TryGetValue(ending.Id, out var held))
                continue;

            foreach (var missing in questItems.Where(item => !held.Contains(item)))
                problems.Add(
                    $"«{ending.Id}» sonluğuna «{missing}» olmadan çatmaq mümkündür — hekayə əşyası zəmanətli deyil.");
        }

        HashSet<string> GrantedBy(ExperienceNode node) =>
            [.. node.Effects
                .Where(e => e.Kind == ExperienceEffectKind.GrantItem)
                .Select(e => e.Key)
                .Where(key => questItems.Contains(key, StringComparer.Ordinal))];
    }

    /// <summary>
    /// HƏR fərdiləşdirmə variantı sonluğa çatmalıdır.
    ///
    /// <para>Şərtli düyünlərin ən təhlükəli səhvi budur: «uzun» profil üçün
    /// yazılmış səhnə əsas yolun üstünə düşür və «qısa» profildə keçid
    /// tapılmır. Yoxlama hər variantı ayrıca gəzir.</para>
    /// </summary>
    private static void ValidateVariantsPlayable(
        ExperienceDefinition definition,
        IReadOnlyDictionary<string, ExperienceNode> index,
        List<string> problems)
    {
        if (!index.ContainsKey(definition.StartNodeId))
            return;

        foreach (var variant in AdventureVariants.All)
        {
            HashSet<string> seen = new(StringComparer.Ordinal) { definition.StartNodeId };
            Queue<string> queue = new();
            queue.Enqueue(definition.StartNodeId);

            var reachedEnding = false;

            while (queue.Count > 0)
            {
                var node = index[queue.Dequeue()];

                if (node.IsEnding)
                {
                    reachedEnding = true;
                    continue;
                }

                var moved = false;

                foreach (var transition in node.Transitions)
                {
                    if (!string.IsNullOrEmpty(transition.Requires.RequiredVariant)
                        && !string.Equals(transition.Requires.RequiredVariant, variant, StringComparison.Ordinal))
                        continue;

                    if (string.Equals(transition.Requires.ForbiddenVariant, variant, StringComparison.Ordinal))
                        continue;

                    if (!index.TryGetValue(transition.TargetNodeId, out var target))
                        continue;

                    if (!string.IsNullOrEmpty(target.Requires.RequiredVariant)
                        && !string.Equals(target.Requires.RequiredVariant, variant, StringComparison.Ordinal))
                        continue;

                    if (string.Equals(target.Requires.ForbiddenVariant, variant, StringComparison.Ordinal))
                        continue;

                    moved = true;

                    if (seen.Add(target.Id))
                        queue.Enqueue(target.Id);
                }

                if (!moved)
                    problems.Add($"«{variant}» variantında «{node.Id}» düyünündən çıxış yoxdur.");
            }

            if (!reachedEnding)
                problems.Add($"«{variant}» variantı heç bir sonluğa çatmır.");
        }
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

        foreach (var fallback in node.Transitions.Where(t => t.IsFallback))
        {
            if (!fallback.Requires.IsAlwaysTrue)
                problems.Add($"«{node.Id}» ehtiyat keçidi şərtlidir — ehtiyat şərtsiz olmalıdır.");

            if (index.TryGetValue(fallback.TargetNodeId, out var target) && !target.Requires.IsAlwaysTrue)
                problems.Add($"«{node.Id}» ehtiyat keçidi şərtli düyünə gedir: {target.Id}.");
        }

        if (RequiresOptions(node.Kind) && node.Options.Count == 0)
            problems.Add($"Seçim düyünü «{node.Id}» variantsızdır.");

        if (!MayPresentOptions(node.Kind) && node.Options.Count > 0)
            problems.Add($"«{node.Id}» seçim daşıya bilməz, amma variantları var.");

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

    /// <summary>
    /// Bu düyündən GEDİLƏ BİLƏN düyünlər.
    ///
    /// <para>Adətən bu, sadəcə keçidlərin hədəfləridir. Sonluq qərar düyünü
    /// isə istisnadır: onun yolunu keçid şərtləri deyil, toplanan bal seçir
    /// (bax <see cref="ExperienceNode.ResolvesEnding"/>), ona görə oradan
    /// BÜTÜN sonluqlara çatmaq mümkündür. Bunu nəzərə almasaydıq, yoxlayıcı
    /// iki etibarlı sonluğu «heç kim görməyəcək» sayardı.</para>
    /// </summary>
    private static IEnumerable<string> Reachable(ExperienceDefinition definition, ExperienceNode node)
    {
        foreach (var transition in node.Transitions)
            yield return transition.TargetNodeId;

        if (!node.ResolvesEnding)
            yield break;

        foreach (var ending in definition.Endings)
            yield return ending.NodeId;
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

            foreach (var target in Reachable(definition, node))
            {
                if (index.ContainsKey(target) && seen.Add(target))
                    queue.Enqueue(target);
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
