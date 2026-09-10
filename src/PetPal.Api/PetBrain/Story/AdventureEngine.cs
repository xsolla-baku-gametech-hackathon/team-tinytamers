using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Story;

/// <summary>Bir addımın GÖRÜNƏN nəticəsi — ekran nəyi bildirməlidir.</summary>
/// <param name="GrantedItems">Yeni düşən əşyalar.</param>
/// <param name="LostItems">Sərf olunan əşyalar.</param>
/// <param name="NewClues">Jurnala yeni düşən ipuçları.</param>
/// <param name="CompletedObjectiveIds">Bu addımda tamamlanan məqsədlər.</param>
/// <param name="CompletedChapterId">Bu addımda bağlanan chapter; boş = bağlanmadı.</param>
public sealed record AdventureStepReport(
    IReadOnlyList<string> GrantedItems,
    IReadOnlyList<string> LostItems,
    IReadOnlyList<string> NewClues,
    IReadOnlyList<string> CompletedObjectiveIds,
    IReadOnlyList<string> WorldFlagsRaised,
    string CompletedChapterId)
{
    public static AdventureStepReport Empty { get; } =
        new([], [], [], [], [], string.Empty);

    public bool IsEmpty =>
        GrantedItems.Count == 0
        && LostItems.Count == 0
        && NewClues.Count == 0
        && CompletedObjectiveIds.Count == 0
        && WorldFlagsRaised.Count == 0
        && string.IsNullOrEmpty(CompletedChapterId);
}

/// <summary>
/// Effektlərin tətbiqi — <b>saf</b>: baza yoxdur, saat yoxdur, təsadüf yoxdur.
///
/// <para><see cref="StoryRuntime"/> ilə eyni prinsipdədir və qəsdən ondan
/// ayrıdır: runtime «hara gedirik» sualına, engine isə «nə dəyişdi» sualına
/// cavab verir. İkisini birləşdirmək keçid hesablamasını vəziyyət yazmaqla
/// qarışdırardı və təkrar göndərilən sorğu effekti iki dəfə tətbiq edə
/// bilərdi.</para>
///
/// <para>Bütün effektlər <b>ideal-potentdir</b>: eyni düyünə ikinci dəfə daxil
/// olmaq nə bayrağı ikiqat qoyur, nə əşyanı ikiqat verir, nə də sonluq balını
/// ikinci dəfə artırır. Zəmanət düyün səviyyəsindədir — bir düyünün effektləri
/// ziyarət edilmiş düyünlər çoxluğuna görə bir dəfə işləyir.</para>
/// </summary>
public static class AdventureEngine
{
    /// <summary>
    /// Düyünə DAXİL OLARKƏN effektləri tətbiq edir və yeni vəziyyəti qaytarır.
    ///
    /// <para><paramref name="alreadyVisited"/> doğru olanda yalnız oxunan
    /// hesablamalar aparılır və heç nə dəyişmir: uşaq geriyə qayıdıb eyni
    /// səhnəni yenidən görsə də, kristalı ikinci dəfə almır.</para>
    /// </summary>
    public static (AdventureState State, AdventureStepReport Report) Enter(
        ExperienceDefinition definition,
        AdventureState state,
        ExperienceNode node,
        bool alreadyVisited)
    {
        if (alreadyVisited)
            return (state with { CurrentNodeId = node.Id }, AdventureStepReport.Empty);

        var (applied, report) = Apply(definition, state, node.Effects, node.Id, node.NpcKey);

        var visitedNodes = new HashSet<string>(applied.VisitedNodeIds, StringComparer.Ordinal) { node.Id };
        var closedChapters = new HashSet<string>(applied.CompletedChapterIds, StringComparer.Ordinal);
        var closedChapter = report.CompletedChapterId;

        if (!string.IsNullOrEmpty(node.CompletesChapterId) && closedChapters.Add(node.CompletesChapterId))
            closedChapter = node.CompletesChapterId;

        var entered = applied with
        {
            VisitedNodeIds = visitedNodes,
            CompletedChapterIds = closedChapters,
            CurrentNodeId = node.Id
        };

        return (entered, report with { CompletedChapterId = closedChapter });
    }

    /// <summary>
    /// Seçilmiş variantın effektləri — yalnız bu düyməni basan uşağa aiddir.
    ///
    /// <para>Variant açarı da vəziyyətə yazılır, ona görə sonrakı chapter-lər
    /// «hansı yolu seçmişdi» sualını <see cref="ExperienceCondition.RequiredChoice"/>
    /// ilə soruşa bilir — gecikmiş nəticənin daşıyıcısı budur.</para>
    /// </summary>
    public static (AdventureState State, AdventureStepReport Report) Choose(
        ExperienceDefinition definition,
        AdventureState state,
        ExperienceOption option,
        string nodeId)
    {
        var (applied, report) = Apply(definition, state, option.Effects, nodeId, string.Empty);

        return (WithChoice(applied, option.Key), report);
    }

    private static (AdventureState State, AdventureStepReport Report) Apply(
        ExperienceDefinition definition,
        AdventureState state,
        IReadOnlyList<ExperienceEffect> effects,
        string nodeId,
        string npcKey)
    {
        if (effects.Count == 0)
            return (state, AdventureStepReport.Empty);

        var flags = new HashSet<string>(state.Flags, StringComparer.Ordinal);
        var worldFlags = new HashSet<string>(state.WorldFlags, StringComparer.Ordinal);
        var chapters = new HashSet<string>(state.CompletedChapterIds, StringComparer.Ordinal);
        var inventory = state.Inventory.ToList();
        var clues = state.Clues.ToList();
        var objectives = state.Objectives.ToList();
        var scores = new Dictionary<string, int>(state.EndingScores, StringComparer.Ordinal);
        var npcs = new Dictionary<string, string>(state.NpcStates, StringComparer.Ordinal);

        List<string> granted = [];
        List<string> lost = [];
        List<string> newClues = [];
        List<string> completedObjectives = [];
        List<string> raisedWorldFlags = [];
        var closedChapter = string.Empty;

        foreach (var effect in effects)
        {
            if (string.IsNullOrWhiteSpace(effect.Key))
                continue;

            switch (effect.Kind)
            {
                case ExperienceEffectKind.SetFlag:
                    flags.Add(effect.Key);
                    break;

                case ExperienceEffectKind.SetWorldFlag:
                    if (worldFlags.Add(effect.Key))
                        raisedWorldFlags.Add(effect.Key);
                    break;

                case ExperienceEffectKind.GrantItem:
                    if (Grant(definition, inventory, effect, nodeId))
                        granted.Add(effect.Key);
                    break;

                case ExperienceEffectKind.ConsumeItem:
                    if (Consume(definition, inventory, effect))
                        lost.Add(effect.Key);
                    break;

                case ExperienceEffectKind.DiscoverClue:
                    if (Discover(definition, clues, effect, nodeId))
                        newClues.Add(effect.Key);
                    break;

                case ExperienceEffectKind.StartObjective:
                    Activate(definition, objectives, effect.Key);
                    break;

                case ExperienceEffectKind.AdvanceObjective:
                    if (Advance(definition, objectives, effect.Key, effect.Amount))
                        completedObjectives.Add(effect.Key);
                    break;

                case ExperienceEffectKind.CompleteObjective:
                    if (Complete(definition, objectives, effect.Key))
                        completedObjectives.Add(effect.Key);
                    break;

                case ExperienceEffectKind.SkipOptionalObjective:
                    Skip(definition, objectives, effect.Key);
                    break;

                case ExperienceEffectKind.ScoreEnding:
                    scores[effect.Key] = scores.GetValueOrDefault(effect.Key) + effect.Amount;
                    break;

                case ExperienceEffectKind.CompleteChapter:
                    if (chapters.Add(effect.Key))
                        closedChapter = effect.Key;
                    break;

                case ExperienceEffectKind.SetNpcState:
                    npcs[npcKey.Length > 0 ? npcKey : effect.Key] = effect.Key;
                    break;
            }
        }

        var next = state with
        {
            Flags = flags,
            WorldFlags = worldFlags,
            CompletedChapterIds = chapters,
            Inventory = inventory,
            Clues = clues,
            Objectives = objectives,
            EndingScores = scores,
            NpcStates = npcs
        };

        var report = new AdventureStepReport(
            granted, lost, newClues, completedObjectives, raisedWorldFlags, closedChapter);

        return (next, report);
    }

    /// <summary>Seçilmiş variantı vəziyyətə yazır — sonrakı şərtlər onu oxuyur.</summary>
    public static AdventureState WithChoice(AdventureState state, string choiceKey)
    {
        if (string.IsNullOrWhiteSpace(choiceKey))
            return state;

        var chosen = new HashSet<string>(state.SelectedChoiceIds, StringComparer.Ordinal) { choiceKey };

        return state with { SelectedChoiceIds = chosen };
    }

    /// <summary>Bu düyündəki təkrar cəhdi sayır — yumşaq uğursuzluq yolunu açan sayğac.</summary>
    public static AdventureState WithRetry(AdventureState state, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return state;

        var retries = new Dictionary<string, int>(state.RetryCounts, StringComparer.Ordinal)
        {
            [nodeId] = state.RetriesAt(nodeId) + 1
        };

        return state with { RetryCounts = retries };
    }

    /// <summary>
    /// «Yeni» nişanlarını götürür — hər addımın ƏVVƏLİNDƏ çağırılır.
    ///
    /// <para>«Yeni» = SON addımda tapılmış. Nişanı jurnalın açılmasına
    /// bağlasaydıq, oxumaq da vəziyyəti dəyişən bir əməliyyat olardı: nömrə
    /// artar, o biri cihazdakı açıq ekran isə səbəbsiz münaqişə alardı. Tapıntı
    /// onsuz da addımın «nə dəyişdi» zolağında elan olunur.</para>
    /// </summary>
    public static AdventureState WithCluesSeen(AdventureState state) =>
        state with { Clues = [.. state.Clues.Select(c => c with { IsNew = false })] };

    /// <summary>
    /// Toplanan bala görə QAZANILAN sonluq.
    ///
    /// <para>Bərabərlik determinist həll olunur: tərifdəki sıra qərar verir,
    /// təsadüf yox. Eyni oyun eyni sonluğu verməlidir, yoxsa uşağın seçimləri
    /// mənasızlaşar.</para>
    ///
    /// <para>Heç bir sonluq həddi ödəmirsə ilk sonluq qaytarılır — uşaq
    /// macərəni bitirdiyi halda sonluqsuz qalmır.</para>
    /// </summary>
    public static AdventureEndingDefinition? ResolveEnding(
        ExperienceDefinition definition, AdventureState state)
    {
        if (definition.Endings.Count == 0)
            return null;

        AdventureEndingDefinition? best = null;
        var bestScore = int.MinValue;

        foreach (var ending in definition.Endings)
        {
            var score = state.EndingScores.GetValueOrDefault(ending.EndingKey);

            if (score < ending.MinimumScore)
                continue;

            if (score > bestScore)
            {
                best = ending;
                bestScore = score;
            }
        }

        return best ?? definition.Endings[0];
    }

    /// <summary>
    /// Macəranın irəliləməsi — 0–100.
    ///
    /// <para>Addım sayına görə hesablamaq budaqlanan hekayədə yanlışdır:
    /// yollar müxtəlif uzunluqdadır. Tamamlanan CHAPTER isə hər yolda eyni
    /// mənanı daşıyır, ona görə göstərici ondan qurulur.</para>
    /// </summary>
    public static int ProgressPercent(ExperienceDefinition definition, AdventureState state)
    {
        if (!definition.HasChapters)
            return 0;

        var done = definition.Chapters.Count(c => state.CompletedChapterIds.Contains(c.ChapterId));

        return (int)Math.Round(100.0 * done / definition.Chapters.Count);
    }

    /// <summary>Bu chapter-in bütün ƏSAS məqsədləri tamamlanıbmı.</summary>
    public static bool ChapterObjectivesMet(
        ExperienceDefinition definition, AdventureState state, string chapterId)
    {
        var chapter = definition.Chapter(chapterId);

        if (chapter is null)
            return true;

        return chapter.MainObjectiveIds.All(state.HasCompletedObjective);
    }

    /// <summary>
    /// İzləyicidə göstəriləcək məqsədlər — ən çox üç dənə.
    ///
    /// <para>Uzun siyahı uşağı yükləyir və macəranı tapşırıq idarəçiliyinə
    /// çevirir. Sıra: əvvəl əsas, sonra yan; gizli olanlar tamamlanana qədər
    /// heç görünmür.</para>
    /// </summary>
    public static IReadOnlyList<RunObjective> VisibleObjectives(
        ExperienceDefinition definition, AdventureState state, string chapterId, int limit = 3)
    {
        var active = state.Objectives
            .Where(o => o.Status == AdventureObjectiveStatus.Active)
            .Select(o => (Run: o, Definition: definition.Objective(o.ObjectiveId)))
            .Where(pair => pair.Definition is not null && !pair.Definition!.IsHidden)
            .Where(pair => string.IsNullOrEmpty(chapterId)
                           || string.IsNullOrEmpty(pair.Definition!.ChapterId)
                           || string.Equals(pair.Definition.ChapterId, chapterId, StringComparison.Ordinal))
            .OrderBy(pair => pair.Definition!.IsOptional ? 1 : 0)
            .ThenBy(pair => pair.Definition!.ObjectiveId, StringComparer.Ordinal)
            .Take(limit)
            .Select(pair => pair.Run);

        return [.. active];
    }

    private static bool Grant(
        ExperienceDefinition definition, List<RunItem> inventory, ExperienceEffect effect, string nodeId)
    {
        if (definition.Item(effect.Key) is null)
            return false;

        var index = inventory.FindIndex(i => string.Equals(i.ItemId, effect.Key, StringComparison.Ordinal));
        var amount = Math.Max(1, effect.Amount);

        if (index >= 0)
        {
            inventory[index] = inventory[index] with { Quantity = inventory[index].Quantity + amount };
            return true;
        }

        inventory.Add(new RunItem(effect.Key, amount, nodeId));

        return true;
    }

    /// <summary>
    /// Əşyanı sərf edir — <b>hekayə əşyası heç vaxt sıfıra düşmür</b>.
    ///
    /// <para>Bu, tərifin diqqətindən asılı olmayan zəmanətdir: uşağın macərası
    /// bir predmetin əlindən çıxması ilə bloklana bilməz. Yalnız
    /// <c>IsConsumable</c> əşya azalır.</para>
    /// </summary>
    private static bool Consume(
        ExperienceDefinition definition, List<RunItem> inventory, ExperienceEffect effect)
    {
        if (definition.Item(effect.Key) is not { } item || item.IsQuestItem || !item.IsConsumable)
            return false;

        var index = inventory.FindIndex(i => string.Equals(i.ItemId, effect.Key, StringComparison.Ordinal));

        if (index < 0)
            return false;

        var left = inventory[index].Quantity - Math.Max(1, effect.Amount);

        if (left <= 0)
            inventory.RemoveAt(index);
        else
            inventory[index] = inventory[index] with { Quantity = left };

        return true;
    }

    private static bool Discover(
        ExperienceDefinition definition, List<RunClue> clues, ExperienceEffect effect, string nodeId)
    {
        if (definition.Clue(effect.Key) is null)
            return false;

        if (clues.Any(c => string.Equals(c.ClueId, effect.Key, StringComparison.Ordinal)))
            return false;

        clues.Add(new RunClue(effect.Key, nodeId, IsNew: true));

        return true;
    }

    private static void Activate(
        ExperienceDefinition definition, List<RunObjective> objectives, string objectiveId)
    {
        if (definition.Objective(objectiveId) is null)
            return;

        var index = objectives.FindIndex(o =>
            string.Equals(o.ObjectiveId, objectiveId, StringComparison.Ordinal));

        if (index < 0)
        {
            objectives.Add(new RunObjective(objectiveId, AdventureObjectiveStatus.Active, 0));
            return;
        }

        if (objectives[index].Status == AdventureObjectiveStatus.Locked)
            objectives[index] = objectives[index] with { Status = AdventureObjectiveStatus.Active };
    }

    private static bool Advance(
        ExperienceDefinition definition, List<RunObjective> objectives, string objectiveId, int amount)
    {
        if (definition.Objective(objectiveId) is not { } spec)
            return false;

        Activate(definition, objectives, objectiveId);

        var index = objectives.FindIndex(o =>
            string.Equals(o.ObjectiveId, objectiveId, StringComparison.Ordinal));

        if (index < 0 || objectives[index].Status is AdventureObjectiveStatus.Completed)
            return false;

        var count = objectives[index].CurrentCount + Math.Max(1, amount);
        var finished = count >= spec.RequiredCount;

        objectives[index] = objectives[index] with
        {
            CurrentCount = Math.Min(count, spec.RequiredCount),
            Status = finished ? AdventureObjectiveStatus.Completed : AdventureObjectiveStatus.Active
        };

        return finished;
    }

    private static bool Complete(
        ExperienceDefinition definition, List<RunObjective> objectives, string objectiveId)
    {
        if (definition.Objective(objectiveId) is not { } spec)
            return false;

        Activate(definition, objectives, objectiveId);

        var index = objectives.FindIndex(o =>
            string.Equals(o.ObjectiveId, objectiveId, StringComparison.Ordinal));

        if (index < 0 || objectives[index].Status == AdventureObjectiveStatus.Completed)
            return false;

        objectives[index] = objectives[index] with
        {
            Status = AdventureObjectiveStatus.Completed,
            CurrentCount = spec.RequiredCount
        };

        return true;
    }

    /// <summary>
    /// Yan tapşırığı bağlayır. ƏSAS məqsəd buraxıla bilməz — tərif səhvən
    /// belə yazılsa da, engine imtina edir.
    /// </summary>
    private static void Skip(
        ExperienceDefinition definition, List<RunObjective> objectives, string objectiveId)
    {
        if (definition.Objective(objectiveId) is not { IsOptional: true })
            return;

        var index = objectives.FindIndex(o =>
            string.Equals(o.ObjectiveId, objectiveId, StringComparison.Ordinal));

        if (index < 0)
        {
            objectives.Add(new RunObjective(objectiveId, AdventureObjectiveStatus.Skipped, 0));
            return;
        }

        if (objectives[index].Status != AdventureObjectiveStatus.Completed)
            objectives[index] = objectives[index] with { Status = AdventureObjectiveStatus.Skipped };
    }
}
