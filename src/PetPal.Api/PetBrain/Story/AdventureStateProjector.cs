using PetPal.Api.Entities;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Story;

/// <summary>
/// Vəziyyəti EKRANA çevirən qat — saf, bazasız.
///
/// <para>Servisdən ayrı olması qəsdəndir: burada heç bir qərar verilmir,
/// yalnız serverin artıq bildiyi şey uşağın dilinə tərcümə olunur. Ona görə
/// tam unit-test olunur və bir dəfə düzəldilən mətn hər ekranda düzəlir.</para>
/// </summary>
public static class AdventureStateProjector
{
    /// <summary>HUD-da eyni anda görünən ən çox məqsəd sayı.</summary>
    public const int VisibleObjectiveLimit = 3;

    /// <summary>Bərpa kartında göstərilən ən çox əşya.</summary>
    public const int ResumeItemLimit = 3;

    public static PetBrainAdventureStateDto Project(
        ExperienceDefinition definition,
        AdventureRunState row,
        AdventureState state,
        ExperienceNode? current,
        string language)
    {
        var chapterId = current?.ChapterId ?? row.CurrentChapterId;
        var chapter = definition.Chapter(chapterId);

        var dto = new PetBrainAdventureStateDto
        {
            Revision = row.Revision,
            CurrentChapterId = chapterId,
            CurrentChapterTitle = chapter?.Title(language) ?? string.Empty,
            CurrentChapterOrder = chapter?.Order ?? 0,
            ChapterCount = definition.Chapters.Count,
            ProgressPercent = AdventureEngine.ProgressPercent(definition, state),
            RemainingMinutes = RemainingMinutes(definition, state, chapter),
            Chapters = [.. definition.OrderedChapters.Select(c => ToDto(c, state, language))],
            Objectives = [.. VisibleObjectives(definition, state, chapterId, language)],
            Inventory = [.. Inventory(definition, state, language)],
            Clues = [.. Clues(definition, state, language)],
            UnreadClues = state.Clues.Count(c => c.IsNew),
            HasCheckpoint = !string.IsNullOrEmpty(row.CheckpointNodeId),
            IsAtCheckpoint = current?.IsCheckpoint ?? false,
            Variant = state.Variant
        };

        return dto;
    }

    /// <summary>
    /// Qalan təxmini dəqiqə.
    ///
    /// <para>Cari chapter YARIMÇIQ sayılır: uşaq onun ortasındadırsa, tam
    /// müddətini qalan vaxta yazmaq «hələ çox var» hissi yaradardı. Yarısı
    /// təxmini, amma dürüst cavabdır.</para>
    /// </summary>
    private static int RemainingMinutes(
        ExperienceDefinition definition, AdventureState state, AdventureChapterDefinition? current)
    {
        var remaining = definition.Chapters
            .Where(c => !state.CompletedChapterIds.Contains(c.ChapterId))
            .Sum(c => c.EstimatedMinutes);

        if (current is not null && !state.CompletedChapterIds.Contains(current.ChapterId))
            remaining -= current.EstimatedMinutes / 2;

        return Math.Max(0, remaining);
    }

    private static PetBrainChapterDto ToDto(
        AdventureChapterDefinition chapter, AdventureState state, string language)
    {
        var completed = state.CompletedChapterIds.Contains(chapter.ChapterId);

        var active = !completed
                     && state.VisitedNodeIds.Count > 0
                     && state.CurrentNodeId.Length > 0;

        return new PetBrainChapterDto
        {
            ChapterId = chapter.ChapterId,
            Order = chapter.Order,
            Title = chapter.Title(language),

            Summary = completed ? chapter.Summary(language) : string.Empty,
            EstimatedMinutes = chapter.EstimatedMinutes,
            Status = completed
                ? AdventureChapterStatus.Completed
                : active
                    ? AdventureChapterStatus.Active
                    : AdventureChapterStatus.Locked
        };
    }

    public static IEnumerable<PetBrainObjectiveDto> VisibleObjectives(
        ExperienceDefinition definition, AdventureState state, string chapterId, string language) =>
        AdventureEngine
            .VisibleObjectives(definition, state, chapterId, VisibleObjectiveLimit)
            .Select(run => ToDto(definition, run, language))
            .Where(dto => dto is not null)!;

    public static PetBrainObjectiveDto? ToDto(
        ExperienceDefinition definition, RunObjective run, string language)
    {
        if (definition.Objective(run.ObjectiveId) is not { } spec)
            return null;

        return new PetBrainObjectiveDto
        {
            ObjectiveId = spec.ObjectiveId,
            Title = spec.Title(language),
            Description = spec.Description(language),
            Status = run.Status,
            CurrentCount = run.CurrentCount,
            RequiredCount = spec.RequiredCount,
            IsOptional = spec.IsOptional
        };
    }

    public static IEnumerable<PetBrainInventoryItemDto> Inventory(
        ExperienceDefinition definition, AdventureState state, string language) =>
        state.Inventory
            .Select(item => (Run: item, Spec: definition.Item(item.ItemId)))
            .Where(pair => pair.Spec is not null)
            .Select(pair => new PetBrainInventoryItemDto
            {
                ItemId = pair.Spec!.ItemId,
                Name = pair.Spec.Name(language),
                Description = pair.Spec.Description(language),
                Icon = pair.Spec.Icon,
                Quantity = pair.Run.Quantity,
                IsQuestItem = pair.Spec.IsQuestItem
            });

    public static IEnumerable<PetBrainClueDto> Clues(
        ExperienceDefinition definition, AdventureState state, string language) =>
        state.Clues
            .Select(clue => (Run: clue, Spec: definition.Clue(clue.ClueId)))
            .Where(pair => pair.Spec is not null)
            .OrderByDescending(pair => pair.Spec!.Importance)
            .Select(pair => new PetBrainClueDto
            {
                ClueId = pair.Spec!.ClueId,
                Title = pair.Spec.Title(language),
                Text = pair.Spec.Text(language),
                Icon = pair.Spec.Icon,
                Importance = pair.Spec.Importance,
                IsNew = pair.Run.IsNew
            });

    /// <summary>Bir addımın dəyişdirdiklərini ekranın dilinə çevirir.</summary>
    public static PetBrainStepChangesDto Changes(
        ExperienceDefinition definition,
        AdventureState state,
        AdventureStepReport report,
        string language)
    {
        var dto = new PetBrainStepChangesDto { CompletedChapterId = report.CompletedChapterId };

        foreach (var itemId in report.GrantedItems)
        {
            if (definition.Item(itemId) is not { } spec)
                continue;

            dto.GainedItems.Add(new PetBrainInventoryItemDto
            {
                ItemId = spec.ItemId,
                Name = spec.Name(language),
                Description = spec.Description(language),
                Icon = spec.Icon,
                Quantity = state.ItemCount(itemId),
                IsQuestItem = spec.IsQuestItem,
                IsNew = true
            });
        }

        foreach (var itemId in report.LostItems)
        {
            if (definition.Item(itemId) is { } spec)
                dto.LostItemNames.Add(spec.Name(language));
        }

        foreach (var clueId in report.NewClues)
        {
            if (definition.Clue(clueId) is not { } spec)
                continue;

            dto.NewClues.Add(new PetBrainClueDto
            {
                ClueId = spec.ClueId,
                Title = spec.Title(language),
                Text = spec.Text(language),
                Icon = spec.Icon,
                Importance = spec.Importance,
                IsNew = true
            });
        }

        foreach (var objectiveId in report.CompletedObjectiveIds)
        {
            if (state.Objective(objectiveId) is { } run && ToDto(definition, run, language) is { } objective)
                dto.CompletedObjectives.Add(objective);
        }

        return dto;
    }

    /// <summary>
    /// Chapter yekun ekranı — nə etdik, nə qazandıq, sonra nə var.
    /// </summary>
    public static PetBrainChapterCompleteDto ChapterComplete(
        ExperienceDefinition definition,
        AdventureState state,
        string chapterId,
        IReadOnlyList<string> keyChoices,
        string language)
    {
        var chapter = definition.Chapter(chapterId);

        var next = definition.OrderedChapters
            .FirstOrDefault(c => chapter is not null && c.Order == chapter.Order + 1);

        var dto = new PetBrainChapterCompleteDto
        {
            ChapterId = chapterId,
            Title = chapter?.Title(language) ?? string.Empty,
            Summary = chapter?.Summary(language) ?? string.Empty,
            KeyChoices = [.. keyChoices],
            NextChapterTitle = next?.Title(language) ?? string.Empty,
            NextChapterMinutes = next?.EstimatedMinutes ?? 0,
            IsFinalChapter = next is null
        };

        if (chapter is null)
            return dto;

        foreach (var objectiveId in chapter.MainObjectiveIds.Concat(chapter.OptionalObjectiveIds))
        {
            if (state.Objective(objectiveId) is not { } run)
                continue;

            if (ToDto(definition, run, language) is not { } objective)
                continue;

            if (run.Status == AdventureObjectiveStatus.Completed)
                dto.CompletedObjectives.Add(objective);
            else if (objective.IsOptional)
                dto.MissedOptional.Add(objective);
        }

        return dto;
    }

    /// <summary>
    /// Bərpa kartı — uşaq bir həftə sonra qayıtsa da harada qaldığını bilsin.
    /// </summary>
    public static PetBrainResumeDto Resume(
        ExperienceDefinition definition,
        ExperienceRun run,
        AdventureRunState row,
        AdventureState state,
        ExperienceTemplate template,
        string language)
    {
        var chapterId = row.CheckpointChapterId.Length > 0 ? row.CheckpointChapterId : row.CurrentChapterId;
        var chapter = definition.Chapter(chapterId);

        var objective = AdventureEngine
            .VisibleObjectives(definition, state, chapterId: string.Empty, limit: 1)
            .Select(o => definition.Objective(o.ObjectiveId))
            .FirstOrDefault(o => o is not null);

        return new PetBrainResumeDto
        {
            RunId = run.Id,
            TemplateKey = template.Key,
            Title = template.Title(language),
            SceneKey = template.SceneKey,
            Icon = template.Icon,
            ChapterTitle = chapter?.Title(language) ?? string.Empty,
            ChapterOrder = chapter?.Order ?? 0,
            ChapterCount = definition.Chapters.Count,
            ProgressPercent = AdventureEngine.ProgressPercent(definition, state),
            RemainingMinutes = RemainingMinutes(definition, state, chapter),
            LastEventSummary = LastEventSummary(definition, state, language),
            CurrentObjective = objective?.Title(language) ?? string.Empty,
            KeyItems =
            [
                .. Inventory(definition, state, language)
                    .OrderByDescending(i => i.IsQuestItem)
                    .Take(ResumeItemLimit)
            ],
            LastPlayedAt = row.LastPlayedAt,
            Status = run.Status
        };
    }

    /// <summary>
    /// «Sonuncu dəfə nə oldu» — SAXLANAN faktlardan qurulmuş determinist cümlə.
    ///
    /// <para>Model çağırılmır: xülasə uşağın həqiqətən keçdiyi son fəslin
    /// mətnidir. Beləliklə eyni run həmişə eyni xülasəni verir və heç bir
    /// uydurma hekayə yaranmır.</para>
    /// </summary>
    private static string LastEventSummary(
        ExperienceDefinition definition, AdventureState state, string language)
    {
        var lastCompleted = definition.OrderedChapters
            .LastOrDefault(c => state.CompletedChapterIds.Contains(c.ChapterId));

        return lastCompleted?.Summary(language) ?? string.Empty;
    }
}
