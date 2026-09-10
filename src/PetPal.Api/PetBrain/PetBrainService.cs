using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Learning;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Recap;
using PetPal.Api.Pets;
using PetPal.Api.Progress;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Pet Brain-in əməliyyat qatı: qərarları toplayır, mərhələləri yoxlayır və
/// mükafatı dəqiq bir dəfə tətbiq edir.
///
/// <para>Qərarın ÖZÜ burada deyil — o, saf siniflərdədir
/// (<see cref="AdaptivePetDirector"/>, <see cref="ProfileLearningRules"/>,
/// <see cref="ExperienceDifficulty"/>, <see cref="MemoryPolicy"/>). Bu sinif
/// yalnız bazanı, saatı və avtorizasiyanı bağlayır.</para>
/// </summary>
public class PetBrainService : IPetBrainService
{
    /// <summary>Yeniliyin baxdığı run sayı.</summary>
    private const int RecentRunWindow = 8;

    /// <summary>Nümayiş panelində göstərilən son hadisə sayı.</summary>
    private const int DebugEventLimit = 8;

    /// <summary>Təkrar yoxlamasının baxdığı son tapmaca sayı.</summary>
    private const int RecentPuzzleWindow = 6;

    /// <summary>Saxlanan məzmun sabit formatda olmalıdır — bax IssuedPuzzle.</summary>
    private static readonly JsonSerializerOptions PuzzleJson = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly IPetService _pets;
    private readonly IBehaviorTracker _tracker;
    private readonly IExperienceNarrativeProvider _narrative;
    private readonly IPersonalizedPuzzleGenerator _puzzles;
    private readonly IDailyGoalService _dailyGoals;
    private readonly TimeProvider _clock;
    private readonly PetBrainOptions _options;
    private readonly ScreenTimeOptions _screenTime;
    private readonly PuzzleIllustrationCoordinator _illustrations;
    private readonly PuzzleIllustrationQueue _sceneQueue;
    private readonly IRecapSpecFactory _recapSpecs;
    private readonly RecapCoordinator _recaps;
    private readonly RecapQueue _recapQueue;
    private readonly ILogger<PetBrainService> _logger;

    public PetBrainService(
        AppDbContext db,
        IPetService pets,
        IBehaviorTracker tracker,
        IExperienceNarrativeProvider narrative,
        IPersonalizedPuzzleGenerator puzzles,
        IDailyGoalService dailyGoals,
        TimeProvider clock,
        IOptions<PetBrainOptions> options,
        IOptions<ScreenTimeOptions> screenTime,
        PuzzleIllustrationCoordinator illustrations,
        PuzzleIllustrationQueue sceneQueue,
        IRecapSpecFactory recapSpecs,
        RecapCoordinator recaps,
        RecapQueue recapQueue,
        ILogger<PetBrainService> logger)
    {
        _db = db;
        _pets = pets;
        _tracker = tracker;
        _narrative = narrative;
        _puzzles = puzzles;
        _dailyGoals = dailyGoals;
        _clock = clock;
        _options = options.Value;
        _screenTime = screenTime.Value;
        _illustrations = illustrations;
        _sceneQueue = sceneQueue;
        _recapSpecs = recapSpecs;
        _recaps = recaps;
        _recapQueue = recapQueue;
        _logger = logger;
    }

    // ==================== Giriş ekranı ====================

    public async Task<ServiceResult<PetBrainStateDto>> GetStateAsync(Guid childId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return ServiceResult<PetBrainStateDto>.Ok(new PetBrainStateDto { Enabled = false });

        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return ServiceResult<PetBrainStateDto>.NotFound(
                Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var now = _clock.GetUtcNow().UtcDateTime;
        var language = child.LanguageCode;
        var pet = child.Pet;

        PetProgression.Refresh(pet, now);

        var greeting = PetBrainGreeting.Build(pet, child.DisplayName, language, child.Memories, now);

        // Salamlamada işlənən xatirə qeyd olunur — pet hər açılışda eyni cümləni
        // təkrarlamamalıdır (bax MemoryPolicy.PickForGreeting).
        if (greeting.UsedMemory is not null)
            greeting.UsedMemory.LastUsedAt = now;

        var interests = ScoresOf(child, PetBrainTraitCategory.Interest);
        var playStyles = ScoresOf(child, PetBrainTraitCategory.PlayStyle);
        var personality = CompanionPersonality.Derive(interests, playStyles);

        var state = new PetBrainStateDto
        {
            Enabled = true,
            PetIsHatched = pet.HatchedAt is not null,
            PetName = pet.Name,
            Greeting = greeting.Text,
            Bond = BondRules.Clamp(pet.Bond),
            Personality = personality,
            PersonalityLabel = CompanionPersonality.Label(personality, language),
            Interests = TopTraits(interests, TraitKeys.Interests, language),
            PlayStyles = TopTraits(playStyles, TraitKeys.PlayStyles, language),
            Memories = [.. MemoryPolicy.Select(child.Memories, now)
                .Select(m => MemoryPolicy.ToDto(m, language, pet.Name))],
            DemoMode = _options.DemoMode
        };

        // Ekran vaxtı YENİ run-a maneədir; başlanmış run isə bitirilə bilir.
        var goal = await _dailyGoals.GetOrCreateTodayAsync(child, ct);
        var screenTimeState = ScreenTimeGuard.Evaluate(child, goal, now, _screenTime.Enforced);

        state.ScreenTimeBlocked = screenTimeState != ScreenTimeState.Allowed;
        state.ScreenTimeMessage = ScreenTimeGuard.MessageFor(screenTimeState, language, child);

        var activeRun = await ActiveRunAsync(childId, ct);
        if (activeRun is not null)
            state.ActiveRun = await ToDtoAsync(activeRun, child, ct);

        var history = await RecentRunsAsync(childId, ct);
        var lastPerformance = LastPerformance(history);
        var difficulty = DifficultyFor(child, lastPerformance);

        var context = await BuildContextAsync(child, interests, playStyles, difficulty, ct);
        var decision = AdaptivePetDirector.Decide(context);

        if (decision is not null)
        {
            state.Recommendation = await ToDtoAsync(decision, child, ct);

            // "Tövsiyə göründü" hadisəsi gün ərzində BİR DƏFƏ sayılır — ekranı
            // yeniləmək profili şişirtməməlidir.
            await _tracker.TrackAsync(
                childId,
                PetBrainEventType.RecommendationViewed,
                new PetBrainEventData(decision.Template.Key, decision.Template.Theme, []),
                $"rec-view:{decision.Template.Key}:{now:yyyyMMdd}",
                ct);
        }

        if (_options.DemoMode)
            state.Debug = await BuildDebugAsync(childId, decision, difficulty, lastPerformance, language,
                state.Recommendation?.NarrativeSource ?? ExperienceNarrative.TemplateSource, ct);

        await _db.SaveChangesAsync(ct);

        return ServiceResult<PetBrainStateDto>.Ok(state);
    }

    // ==================== Run başlatmaq ====================

    public async Task<ServiceResult<PetBrainRunDto>> StartRunAsync(
        Guid childId, StartPetBrainRunRequest request, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Disabled<PetBrainRunDto>();

        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return ServiceResult<PetBrainRunDto>.NotFound(
                Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var language = child.LanguageCode;
        var now = _clock.GetUtcNow().UtcDateTime;

        PetProgression.Refresh(child.Pet, now);

        // Yumurta macəraya çıxmır — mövcud qulluq/söhbət qaydası ilə eyni.
        if (child.Pet.HatchedAt is null)
            return ServiceResult<PetBrainRunDto>.Fail(PetVoice.StillAnEgg(language));

        // Naməlum şablon dərhal rədd olunur — kataloqdan kənar heç nə başlamır.
        if (!string.IsNullOrWhiteSpace(request.TemplateKey) && !ExperienceCatalog.IsKnown(request.TemplateKey))
            return ServiceResult<PetBrainRunDto>.NotFound(
                Localized.T(language, "Belə macəra yoxdur.", "There is no such adventure."));

        // Ekran vaxtı YALNIZ yeni fəaliyyəti bloklayır.
        var goal = await _dailyGoals.GetOrCreateTodayAsync(child, ct);
        var screenTime = ScreenTimeGuard.Evaluate(child, goal, now, _screenTime.Enforced);

        if (screenTime != ScreenTimeState.Allowed)
        {
            await _db.SaveChangesAsync(ct);
            return ServiceResult<PetBrainRunDto>.Forbidden(
                ScreenTimeGuard.MessageFor(screenTime, language, child));
        }

        // Yarımçıq run varsa yenisi açılmır — uşaq iki macərada eyni anda olmamalıdır.
        var existing = await ActiveRunAsync(childId, ct);
        if (existing is not null)
            return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(existing, child, ct));

        var interests = ScoresOf(child, PetBrainTraitCategory.Interest);
        var playStyles = ScoresOf(child, PetBrainTraitCategory.PlayStyle);

        var history = await RecentRunsAsync(childId, ct);
        var lastPerformance = LastPerformance(history);
        var difficulty = DifficultyFor(child, lastPerformance);

        var context = await BuildContextAsync(child, interests, playStyles, difficulty, ct);
        var decision = AdaptivePetDirector.Decide(context);

        if (decision is null)
            return ServiceResult<PetBrainRunDto>.Fail(
                Localized.T(language, "Hazırda uyğun macəra yoxdur.", "No suitable adventure right now."));

        // Klient kataloqdan istədiyini seçə bilmir: göndərilən açar serverin
        // hazırkı qərarı ilə üst-üstə düşməlidir.
        if (!string.IsNullOrWhiteSpace(request.TemplateKey)
            && !string.Equals(request.TemplateKey, decision.Template.Key, StringComparison.Ordinal))
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language,
                    "Bu macəra artıq növbədə deyil — ekranı yenilə.",
                    "That adventure is no longer the current one — refresh the screen."));

        var template = decision.Template;

        var run = new ExperienceRun
        {
            ChildProfileId = childId,
            TemplateKey = template.Key,
            ExperienceType = template.Type,
            Theme = template.Theme,
            Difficulty = difficulty,
            Status = PetBrainRunStatus.Active,
            CurrentStage = 0,
            StartedAt = now,

            // Toxum run-a bağlıdır: uşaq ekranı yeniləyib asan sual ovlaya bilmir.
            // Tapmacanın toxumu BURADA saxlanılmır: o, run-ın öz id-sindən və
            // şablon açarından SHA-256 ilə çıxarılır (bax PuzzleSeed) və
            // verilmiş tapmaca ilə birlikdə IssuedPuzzles cədvəlinə düşür.
            Assisted = lastPerformance is not null && ExperienceDifficulty.NeedsAssist(lastPerformance)
        };

        _db.ExperienceRuns.Add(run);

        await _tracker.TrackAsync(
            childId,
            PetBrainEventType.RecommendationSelected,
            new PetBrainEventData(template.Key, template.Theme, ProfileLearningRules.ForSelection(template)),
            null,
            ct);

        await _tracker.TrackAsync(
            childId,
            PetBrainEventType.ExperienceStarted,
            new PetBrainEventData(template.Key, difficulty.ToString(), []),
            $"run-start:{run.Id:N}",
            ct);

        await _db.SaveChangesAsync(ct);

        return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));
    }

    // ==================== Run oxumaq ====================

    public async Task<ServiceResult<PetBrainRunDto>> GetRunAsync(
        Guid childId, Guid runId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Disabled<PetBrainRunDto>();

        var (run, child, error) = await LoadRunAsync(childId, runId, ct);
        if (error is not null)
            return error;

        // Tamamlanmış run YEKUNU ilə qayıdır.
        //
        // Bu, təkcə səliqə deyil: recap videosu arxa fonda hazır olur və uşaq
        // ekranı yeniləyəndə onu GÖRMƏLİDİR. Yekun yalnız tamamlama cavabında
        // olsaydı, video heç vaxt ekrana çıxmazdı — yenilənmə isə xülasəni
        // tamamilə itirərdi.
        if (run!.Status == PetBrainRunStatus.Completed &&
            ExperienceCatalog.Find(run.TemplateKey) is { } completed)
            return ServiceResult<PetBrainRunDto>.Ok(
                await BuildCompletedDtoAsync(run, child!, completed, ct));

        return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child!, ct));
    }

    // ==================== Mərhələ cavabı ====================

    public async Task<ServiceResult<PetBrainRunDto>> SubmitChoiceAsync(
        Guid childId, Guid runId, PetBrainChoiceRequest request, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Disabled<PetBrainRunDto>();

        var (run, child, error) = await LoadRunAsync(childId, runId, ct);
        if (error is not null)
            return error;

        var language = child!.LanguageCode;

        if (run!.Status != PetBrainRunStatus.Active)
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language, "Bu macəra artıq bitib.", "This adventure is already finished."));

        var template = ExperienceCatalog.Find(run.TemplateKey);
        if (template is null)
            return ServiceResult<PetBrainRunDto>.NotFound(
                Localized.T(language, "Belə macəra yoxdur.", "There is no such adventure."));

        if (run.CurrentStage >= template.StageCount)
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language, "Bütün mərhələlər bitib.", "Every stage is already done."));

        // Mərhələ atlamaq və iki dəfə basmaq eyni yolla bağlanır: klientin
        // gözlədiyi mərhələ serverinki ilə üst-üstə düşməlidir.
        if (request.StageIndex != run.CurrentStage)
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language, "Bu mərhələ artıq keçilib.", "That stage has already been played."));

        var stage = template.Stages[run.CurrentStage];
        var now = _clock.GetUtcNow().UtcDateTime;

        // ---- İpucu istəyi: mərhələ İRƏLİLƏMİR ----
        if (request.RequestHint)
        {
            if (stage.Kind != PetBrainStageKind.Puzzle)
                return ServiceResult<PetBrainRunDto>.Fail(
                    Localized.T(language, "Bu mərhələdə ipucu yoxdur.", "There is no hint on this stage."));

            run.HintsUsed++;

            // İpucu sayğacı tapmacanın özündə də saxlanılır: yenilənmədən sonra
            // ipucu ekranda QALMALIDIR, yoxsa uşaq onu itirmiş olur.
            var hinted = await EnsurePuzzleAsync(run, template, child, ct);
            hinted.HintsUsed++;

            await _tracker.TrackAsync(
                childId,
                PetBrainEventType.HintRequested,
                // İpucu MARAĞI AZALTMIR — bax ProfileLearningRules.
                new PetBrainEventData(template.Key, $"stage:{run.CurrentStage}", []),
                $"hint:{run.Id:N}:{run.CurrentStage}:{run.HintsUsed}",
                ct);

            await _db.SaveChangesAsync(ct);
            return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct, showHint: true));
        }

        // Tapmaca cavabı id SİYAHISIDIR, seçim mərhələsi isə tək açar — ona görə
        // iki mərhələ növü ayrı sahələr işlədir və biri digərinin yerinə keçmir.
        if (stage.Kind == PetBrainStageKind.Puzzle)
            return await SubmitPuzzleAsync(run, child, template, request.SelectedIds, now, ct);

        if (string.IsNullOrWhiteSpace(request.OptionKey))
            return ServiceResult<PetBrainRunDto>.Fail(
                Localized.T(language, "Bir variant seçilməlidir.", "Please choose an option."));

        return stage.Kind switch
        {
            PetBrainStageKind.Choice => await SubmitChoiceStageAsync(run, child, template, stage, request.OptionKey!, ct),
            _ => await AdvanceIntroAsync(run, child, template, ct)
        };
    }

    private async Task<ServiceResult<PetBrainRunDto>> AdvanceIntroAsync(
        ExperienceRun run, ChildProfile child, ExperienceTemplate template, CancellationToken ct)
    {
        run.Choices.Add("continue");
        run.CurrentStage++;

        await _db.SaveChangesAsync(ct);
        return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));
    }

    private async Task<ServiceResult<PetBrainRunDto>> SubmitChoiceStageAsync(
        ExperienceRun run,
        ChildProfile child,
        ExperienceTemplate template,
        ExperienceStage stage,
        string optionKey,
        CancellationToken ct)
    {
        var language = child.LanguageCode;

        var option = stage.Options.FirstOrDefault(o => string.Equals(o.Key, optionKey, StringComparison.Ordinal));
        if (option is null)
            return ServiceResult<PetBrainRunDto>.Fail(
                Localized.T(language, "Belə variant yoxdur.", "There is no such option."));

        var stageIndex = run.CurrentStage;

        run.Choices.Add(option.Key);
        run.CurrentStage++;

        await _tracker.TrackAsync(
            child.Id,
            PetBrainEventType.StageChoiceMade,
            new PetBrainEventData(template.Key, option.Key, ProfileLearningRules.ForChoice(option)),
            $"choice:{run.Id:N}:{stageIndex}",
            ct);

        await _db.SaveChangesAsync(ct);
        return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));
    }

    /// <summary>
    /// Tapmaca cavabı.
    ///
    /// <para>Doğruluq YALNIZ burada, SAXLANMIŞ həllə qarşı müəyyən edilir.
    /// Klientin gövdəsində nə <c>IsCorrect</c>, nə <c>Score</c>, nə də
    /// <c>Reward</c> sahəsi var — belə sahələr DTO-da ümumiyyətlə mövcud
    /// deyil, ona görə saxta doğru cavab göndərmək mümkün deyil.</para>
    /// </summary>
    private async Task<ServiceResult<PetBrainRunDto>> SubmitPuzzleAsync(
        ExperienceRun run,
        ChildProfile child,
        ExperienceTemplate template,
        IReadOnlyList<string>? selectedIds,
        DateTime now,
        CancellationToken ct)
    {
        var language = child.LanguageCode;
        var stageIndex = run.CurrentStage;

        var issued = await EnsurePuzzleAsync(run, template, child, ct);

        // Naməlum mexanika heç yerdə qəbul edilmir (fail closed).
        var blueprint = PuzzleBlueprintCatalog.Find(issued.BlueprintKey);
        if (blueprint is null)
            return ServiceResult<PetBrainRunDto>.Fail(
                Localized.T(language, "Bu tapmaca artıq keçərli deyil.", "This puzzle is no longer valid."));

        if (issued.Status != PetBrainPuzzleStatus.Issued)
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language, "Bu tapmaca artıq həll olunub.", "This puzzle is already solved."));

        var result = PuzzleAnswerEvaluator.Evaluate(
            blueprint, ReadPublic(issued), ReadSolution(issued), selectedIds);

        // Formaya uyğun olmayan cavab (naməlum id, təkrar, yanlış say) NƏ səhv
        // sayılır, NƏ də cəhd — bu, uşağın səhvi deyil, sorğunun səhvidir.
        if (result.Rejected)
            return ServiceResult<PetBrainRunDto>.Fail(
                Localized.T(language, "Bu cavab uyğun deyil.", "That answer does not fit."));

        issued.Attempts++;

        if (!result.IsCorrect)
        {
            run.Mistakes++;

            await _tracker.TrackAsync(
                child.Id,
                PetBrainEventType.PuzzleFailed,
                // Səhv cavab HEÇ BİR marağı azaltmır — bacarmamaq sevməmək deyil.
                new PetBrainEventData(template.Key, $"stage:{stageIndex}", []),
                $"puzzle-miss:{run.Id:N}:{stageIndex}:{run.Mistakes}",
                ct);

            await _db.SaveChangesAsync(ct);

            // Mərhələ İRƏLİLƏMİR: uşaq yenidən cəhd edir, macəra bitmir.
            return ServiceResult<PetBrainRunDto>.Ok(
                await ToDtoAsync(run, child, ct, puzzleMissed: true));
        }

        issued.Status = PetBrainPuzzleStatus.Solved;
        issued.SolvedAt = now;

        // Yaradıcı yığımda seçilmiş parçalar SƏHNƏYƏ düşür — uşaq seçdiyini
        // qanadın üstündə görməlidir. Məntiq tapmacalarında isə yalnız
        // "həll olundu" qeyd edilir.
        if (blueprint.LowPressure && selectedIds is not null)
            run.Choices.AddRange(selectedIds);
        else
            run.Choices.Add("solved");

        run.CurrentStage++;

        await _tracker.TrackAsync(
            child.Id,
            PetBrainEventType.PuzzleSolved,
            new PetBrainEventData(
                template.Key,
                $"stage:{stageIndex}:{issued.BlueprintKey}",
                ProfileLearningRules.ForPuzzleSolved()),
            $"puzzle-solved:{run.Id:N}:{stageIndex}",
            ct);

        await _db.SaveChangesAsync(ct);
        return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));
    }

    // ==================== Tamamlama ====================

    public async Task<ServiceResult<PetBrainRunDto>> CompleteRunAsync(
        Guid childId, Guid runId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Disabled<PetBrainRunDto>();

        var (run, child, error) = await LoadRunAsync(childId, runId, ct);
        if (error is not null)
            return error;

        var language = child!.LanguageCode;

        var template = ExperienceCatalog.Find(run!.TemplateKey);
        if (template is null)
            return ServiceResult<PetBrainRunDto>.NotFound(
                Localized.T(language, "Belə macəra yoxdur.", "There is no such adventure."));

        // Artıq tamamlanıbsa eyni yekun qaytarılır — təkrar basmaq xəta deyil.
        if (run.Status == PetBrainRunStatus.Completed)
            return ServiceResult<PetBrainRunDto>.Ok(await BuildCompletedDtoAsync(run, child, template, ct));

        if (run.Status == PetBrainRunStatus.Abandoned)
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language, "Bu macəra yarımçıq qalıb.", "This adventure was left unfinished."));

        // Yarımçıq run tamamlana bilməz.
        if (run.CurrentStage < template.StageCount)
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language, "Macəra hələ bitməyib.", "The adventure is not finished yet."));

        return await ApplyCompletionAsync(run, child, template, ct);
    }

    /// <summary>
    /// Mükafatı DƏQİQ BİR DƏFƏ tətbiq edir.
    ///
    /// <para>Zəmanət yaddaşdakı yoxlamada deyil, BAZADADIR: <c>ExecuteUpdateAsync</c>
    /// run-u yalnız hələ <c>Active</c> və mükafatsız olduğu halda "tutur". İki
    /// eyni vaxtlı sorğudan yalnız biri sətri dəyişə bilir; ikinci sorğu sıfır
    /// sətir görür və hazır yekunu oxuyur. Qalan yazılar (XP, bağ, kosmetik,
    /// xatirə, xassə) bir tranzaksiyada gedir.</para>
    /// </summary>
    private async Task<ServiceResult<PetBrainRunDto>> ApplyCompletionAsync(
        ExperienceRun run, ChildProfile child, ExperienceTemplate template, CancellationToken ct)
    {
        var language = child.LanguageCode;
        var now = _clock.GetUtcNow().UtcDateTime;
        var pet = child.Pet!;

        var score = ScoreFor(template, run);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var claimed = await _db.ExperienceRuns
            .Where(r => r.Id == run.Id
                        && r.ChildProfileId == child.Id
                        && r.Status == PetBrainRunStatus.Active
                        && !r.RewardApplied)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, PetBrainRunStatus.Completed)
                .SetProperty(r => r.RewardApplied, true)
                .SetProperty(r => r.CompletedAt, now)
                .SetProperty(r => r.ScorePercent, score), ct);

        if (claimed == 0)
        {
            // Başqa sorğu qabaqladı: heç nə yazmırıq, hazır yekunu qaytarırıq.
            await transaction.RollbackAsync(ct);

            var current = await _db.ExperienceRuns.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == run.Id && r.ChildProfileId == child.Id, ct);

            if (current is null)
                return ServiceResult<PetBrainRunDto>.NotFound(
                    Localized.T(language, "Macəra tapılmadı.", "Adventure not found."));

            return ServiceResult<PetBrainRunDto>.Ok(await BuildCompletedDtoAsync(current, child, template, ct));
        }

        // İzlənilən nüsxə də eyni vəziyyətə gətirilir ki, sonrakı yazma
        // köhnə dəyərləri geri qaytarmasın.
        run.Status = PetBrainRunStatus.Completed;
        run.RewardApplied = true;
        run.CompletedAt = now;
        run.ScorePercent = score;

        var isFirstCompletionOfTemplate = !await _db.ExperienceRuns
            .AnyAsync(r => r.ChildProfileId == child.Id
                           && r.TemplateKey == template.Key
                           && r.Status == PetBrainRunStatus.Completed
                           && r.Id != run.Id, ct);

        var isFirstAdventureEver = !await _db.ExperienceRuns
            .AnyAsync(r => r.ChildProfileId == child.Id
                           && r.Status == PetBrainRunStatus.Completed
                           && r.Id != run.Id, ct);

        // ---- Mükafat ----
        PetProgression.Refresh(pet, now);

        var xp = isFirstCompletionOfTemplate ? template.XpReward : template.XpReward / 2;
        var levelBefore = pet.Level;
        PetProgression.AddXp(pet, xp);

        var bondGranted = BondRules.Grant(
            pet,
            isFirstCompletionOfTemplate
                ? BondRules.ForCompletion(template, isFirstAdventureEver)
                : 1);

        var unlockedCode = string.Empty;
        if (isFirstCompletionOfTemplate && PetAccessories.GrantFromExperience(pet, template.RewardCode))
            unlockedCode = template.RewardCode;

        // ---- Xatirələr ----
        var memories = await RecordMemoriesAsync(run, child, template, unlockedCode, isFirstAdventureEver, now, ct);

        // ---- Profil ----
        await _tracker.TrackAsync(
            child.Id,
            PetBrainEventType.ExperienceCompleted,
            new PetBrainEventData(
                template.Key,
                $"score:{score}",
                ProfileLearningRules.ForCompletion(template, isFirstCompletionOfTemplate)),
            $"run-complete:{run.Id:N}",
            ct);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var dto = await ToDtoAsync(run, child, ct);
        dto.Summary = BuildSummary(
            template, language, pet, xp, bondGranted, unlockedCode, memories, pet.Level > levelBefore);

        // Recap TRANZAKSİYADAN SONRA açılır və heç nə gözlətmir: XP, bağ,
        // xatirə və kosmetik artıq verilib. Video gec gəlsə də (və ya heç
        // gəlməsə də) uşağın mükafatı toxunulmazdır.
        dto.Summary.Recap = await EnsureRecapAsync(run, ct);

        return ServiceResult<PetBrainRunDto>.Ok(dto);
    }

    /// <summary>
    /// Recap sətrini açır və uşağa göndəriləcək hissəni qurur.
    ///
    /// <para>Storyboard HƏMİŞƏ doludur — video hazır olmasa da uşaq öz
    /// seçimlərini üç kadrda görür və altyazılar serverin saxladığı HƏQİQİ
    /// seçimlərdən qurulur.</para>
    /// </summary>
    private async Task<PetBrainRecapDto> EnsureRecapAsync(ExperienceRun run, CancellationToken ct)
    {
        var spec = await _recapSpecs.BuildAsync(run.Id, ct);

        if (spec is null)
            return new PetBrainRecapDto();

        var row = await _recaps.EnsureAsync(spec, ct);

        if (row.Status is PetBrainRecapStatus.Pending or PetBrainRecapStatus.Generating)
            _recapQueue.Enqueue(spec);

        return ToRecapDto(spec, row);
    }

    /// <summary>Saxlanan vəziyyəti və deterministik storyboard-ı birləşdirir.</summary>
    private static PetBrainRecapDto ToRecapDto(AdventureRecapSpec spec, AdventureRecap row) => new()
    {
        Status = row.Status,

        // Ünvan yalnız hazır olanda verilir — provayderin URL-i heç vaxt.
        VideoUrl = row.Status == PetBrainRecapStatus.Ready
            ? $"/api/pet-brain/runs/{row.ExperienceRunId}/recap/video"
            : string.Empty,

        DurationSeconds = RecapStoryboard.TotalSeconds,
        Shots = [.. RecapStoryboard.Build(spec).Select(s => new PetBrainRecapShotDto
        {
            StartSeconds = s.StartSeconds,
            EndSeconds = s.EndSeconds,
            Caption = s.Caption,
            Icon = s.Icon
        })]
    };

    /// <summary>
    /// Uşağın ÖZ recap videosunun saxlanc açarı.
    ///
    /// <para>Yad run, hazır olmayan video və naməlum id üçün <c>null</c> —
    /// endpoint hamısına <c>404</c> verir.</para>
    /// </summary>
    public async Task<string?> GetRecapVideoKeyAsync(Guid childId, Guid runId, CancellationToken ct = default)
    {
        var row = await _db.AdventureRecaps
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ExperienceRunId == runId && r.ChildProfileId == childId, ct);

        return row is { Status: PetBrainRecapStatus.Ready } && !string.IsNullOrEmpty(row.AssetKey)
            ? row.AssetKey
            : null;
    }

    public async Task<ServiceResult<PetBrainRunDto>> AbandonRunAsync(
        Guid childId, Guid runId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Disabled<PetBrainRunDto>();

        var (run, child, error) = await LoadRunAsync(childId, runId, ct);
        if (error is not null)
            return error;

        var language = child!.LanguageCode;

        if (run!.Status != PetBrainRunStatus.Active)
            return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));

        var template = ExperienceCatalog.Find(run.TemplateKey);
        var now = _clock.GetUtcNow().UtcDateTime;

        run.Status = PetBrainRunStatus.Abandoned;
        run.CompletedAt = now;

        if (template is not null)
            await _tracker.TrackAsync(
                childId,
                PetBrainEventType.ExperienceAbandoned,
                new PetBrainEventData(
                    template.Key,
                    $"stage:{run.CurrentStage}",
                    // Yalnız MƏNALI yarımçıq qoyma marağı azaldır — giriş
                    // ekranından çıxmaq fikir bildirmək deyil.
                    ProfileLearningRules.ForAbandon(template, run.CurrentStage)),
                $"run-abandon:{run.Id:N}",
                ct);

        await _db.SaveChangesAsync(ct);
        return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));
    }

    // ==================== Ana ekran çipi ====================

    /// <summary>
    /// Ana ekran çipi.
    ///
    /// <para>Bu yol qəsdən <see cref="LoadChildAsync"/> işlətmir: ana ekran
    /// app-in ən çox açılan endpoint-idir və çipə nə XATİRƏLƏR (uşaq başına 40
    /// sətrə qədər), nə də tam bacarıq siyahısı lazımdır. Ona görə burada dar
    /// proyeksiyalar oxunur.</para>
    /// </summary>
    public async Task<PetBrainHomeChipDto?> GetHomeChipAsync(Guid childId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return null;

        var snapshot = await _db.ChildProfiles
            .AsNoTracking()
            .Where(c => c.Id == childId)
            .Select(c => new
            {
                c.Age,
                c.LanguageCode,
                IsHatched = c.Pet != null && c.Pet.HatchedAt != null
            })
            .FirstOrDefaultAsync(ct);

        // Yumurta macəraya çıxmır — çip də göstərilmir.
        if (snapshot is null || !snapshot.IsHatched)
            return null;

        var language = snapshot.LanguageCode;

        // Yarımçıq macəra varsa direktoru işlətməyə ehtiyac yoxdur.
        var activeTemplateKey = await _db.ExperienceRuns
            .AsNoTracking()
            .Where(r => r.ChildProfileId == childId && r.Status == PetBrainRunStatus.Active)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => r.TemplateKey)
            .FirstOrDefaultAsync(ct);

        if (activeTemplateKey is not null && ExperienceCatalog.Find(activeTemplateKey) is { } activeTemplate)
            return new PetBrainHomeChipDto
            {
                HasActiveRun = true,
                Title = activeTemplate.Title(language),
                Icon = activeTemplate.Icon,
                Reason = Localized.T(language, "Yarımçıq qalıb — davam et", "Unfinished — pick it up")
            };

        var traits = await _db.PlayerTraits
            .AsNoTracking()
            .Where(t => t.ChildProfileId == childId)
            .Select(t => new { t.Category, t.Key, t.Score })
            .ToListAsync(ct);

        var interests = traits
            .Where(t => t.Category == PetBrainTraitCategory.Interest)
            .ToDictionary(t => t.Key, t => TraitKeys.Clamp(t.Score), StringComparer.Ordinal);

        var playStyles = traits
            .Where(t => t.Category == PetBrainTraitCategory.PlayStyle)
            .ToDictionary(t => t.Key, t => TraitKeys.Clamp(t.Score), StringComparer.Ordinal);

        var history = await RecentRunsAsync(childId, ct);
        var last = LastPerformance(history);

        var difficulty = last is not null
            ? ExperienceDifficulty.Next(last, snapshot.Age)
            : ExperienceDifficulty.Initial(snapshot.Age, AdaptiveEngine.TargetDifficulty(
                await AverageMasteryAsync(childId, ct)));

        var context = new PetBrainDirectorContext(
            ChildId: childId,
            Age: snapshot.Age,
            Language: language,
            Interests: interests,
            PlayStyles: playStyles,
            RecentRuns: [.. history.Select(r => new RunHistoryEntry(r.TemplateKey, r.Theme, r.Status))],
            CompletedTemplates: history
                .Where(r => r.Status == PetBrainRunStatus.Completed)
                .Select(r => r.TemplateKey)
                .ToHashSet(StringComparer.Ordinal),
            Difficulty: difficulty,
            PetIsHatched: true);

        var decision = AdaptivePetDirector.Decide(context);

        if (decision is null)
            return null;

        return new PetBrainHomeChipDto
        {
            HasActiveRun = false,
            Title = decision.Template.Title(language),
            Icon = decision.Template.Icon,
            Reason = decision.Reasons.FirstOrDefault() ?? string.Empty
        };
    }

    /// <summary>Başlanğıc çətinlik üçün — tam bacarıq sətirlərini yükləməyə ehtiyac yoxdur.</summary>
    private async Task<int> AverageMasteryAsync(Guid childId, CancellationToken ct)
    {
        var ratings = await _db.SkillMasteries
            .AsNoTracking()
            .Where(m => m.ChildProfileId == childId)
            .Select(m => m.Rating)
            .ToListAsync(ct);

        return ratings.Count == 0 ? AdaptiveEngine.StartingRating : (int)ratings.Average();
    }

    // ==================== Valideyn müqayisəsi ====================

    public async Task<ServiceResult<PetBrainComparisonDto>> CompareChildrenAsync(
        Guid parentUserId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Disabled<PetBrainComparisonDto>();

        // Sahiblik: valideyn YALNIZ öz uşaqlarını görür.
        var children = await _db.ChildProfiles
            .Include(c => c.Pet)
            .Include(c => c.Traits)
            .Include(c => c.SkillMasteries)
            .Where(c => c.ParentUserId == parentUserId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        var result = new PetBrainComparisonDto();

        foreach (var child in children)
        {
            if (child.Pet is null)
                continue;

            var language = child.LanguageCode;
            var interests = ScoresOf(child, PetBrainTraitCategory.Interest);
            var playStyles = ScoresOf(child, PetBrainTraitCategory.PlayStyle);
            var personality = CompanionPersonality.Derive(interests, playStyles);

            var history = await RecentRunsAsync(child.Id, ct);
            var difficulty = DifficultyFor(child, LastPerformance(history));

            var context = await BuildContextAsync(child, interests, playStyles, difficulty, ct);
            var decision = AdaptivePetDirector.Decide(context);

            result.Children.Add(new PetBrainChildSnapshotDto
            {
                ChildId = child.Id,
                DisplayName = child.DisplayName,
                PetName = child.Pet.Name,
                Bond = BondRules.Clamp(child.Pet.Bond),
                PersonalityLabel = CompanionPersonality.Label(personality, language),
                TopInterests = TopTraits(interests, TraitKeys.Interests, language).Take(3).ToList(),
                RecommendedTemplateKey = decision?.Template.Key ?? string.Empty,
                RecommendedTitle = decision?.Template.Title(language) ?? string.Empty,
                Reasons = [.. decision?.Reasons ?? []]
            });
        }

        return ServiceResult<PetBrainComparisonDto>.Ok(result);
    }

    // ==================== Köməkçilər ====================

    private Task<ChildProfile?> LoadChildAsync(Guid childId, CancellationToken ct) =>
        _db.ChildProfiles
            .Include(c => c.Pet)
            .Include(c => c.Traits)
            .Include(c => c.Memories)
            // Başlanğıc çətinliyi mövcud adaptiv mühərrikin hədəfindən gəlir.
            .Include(c => c.SkillMasteries)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

    /// <summary>
    /// Run-u yükləyir və SAHİBLİYİ yoxlayır. Yad uşağın run-u <c>404</c> alır —
    /// <c>403</c> yox: mövcudluğu təsdiqləmək də məlumat sızmasıdır.
    /// </summary>
    private async Task<(ExperienceRun? Run, ChildProfile? Child, ServiceResult<PetBrainRunDto>? Error)>
        LoadRunAsync(Guid childId, Guid runId, CancellationToken ct)
    {
        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return (null, null, ServiceResult<PetBrainRunDto>.NotFound(
                Localized.T("Uşaq profili tapılmadı.", "Child profile not found.")));

        var run = await _db.ExperienceRuns
            .FirstOrDefaultAsync(r => r.Id == runId && r.ChildProfileId == childId, ct);

        if (run is null)
            return (null, null, ServiceResult<PetBrainRunDto>.NotFound(
                Localized.T(child.LanguageCode, "Macəra tapılmadı.", "Adventure not found.")));

        return (run, child, null);
    }

    private Task<ExperienceRun?> ActiveRunAsync(Guid childId, CancellationToken ct) =>
        _db.ExperienceRuns
            .Where(r => r.ChildProfileId == childId && r.Status == PetBrainRunStatus.Active)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);

    private async Task<List<ExperienceRun>> RecentRunsAsync(Guid childId, CancellationToken ct) =>
        await _db.ExperienceRuns
            .AsNoTracking()
            .Where(r => r.ChildProfileId == childId && r.Status != PetBrainRunStatus.Active)
            .OrderByDescending(r => r.CompletedAt)
            .ThenByDescending(r => r.StartedAt)
            .Take(RecentRunWindow)
            .ToListAsync(ct);

    /// <summary>Çətinlik yalnız SON TAMAMLANMIŞ run-dan asılıdır — bir pillə qaydası özü yaranır.</summary>
    private static RunPerformance? LastPerformance(IEnumerable<ExperienceRun> history)
    {
        var last = history.FirstOrDefault(r => r.Status == PetBrainRunStatus.Completed);

        return last is null
            ? null
            : new RunPerformance(last.Difficulty, last.ScorePercent, last.HintsUsed, last.Mistakes);
    }

    private PetBrainDifficulty DifficultyFor(ChildProfile child, RunPerformance? last)
    {
        if (last is not null)
            return ExperienceDifficulty.Next(last, child.Age);

        // İlk təcrübə: mövcud adaptiv mühərrikin hədəfi başlanğıc nöqtəsidir.
        var averageRating = child.SkillMasteries.Count > 0
            ? (int)child.SkillMasteries.Average(m => m.Rating)
            : AdaptiveEngine.StartingRating;

        return ExperienceDifficulty.Initial(child.Age, AdaptiveEngine.TargetDifficulty(averageRating));
    }

    private async Task<PetBrainDirectorContext> BuildContextAsync(
        ChildProfile child,
        IReadOnlyDictionary<string, int> interests,
        IReadOnlyDictionary<string, int> playStyles,
        PetBrainDifficulty difficulty,
        CancellationToken ct)
    {
        var runs = await _db.ExperienceRuns
            .AsNoTracking()
            .Where(r => r.ChildProfileId == child.Id && r.Status != PetBrainRunStatus.Active)
            .OrderByDescending(r => r.CompletedAt)
            .ThenByDescending(r => r.StartedAt)
            .Take(RecentRunWindow)
            .Select(r => new { r.TemplateKey, r.Theme, r.Status })
            .ToListAsync(ct);

        var completed = await _db.ExperienceRuns
            .AsNoTracking()
            .Where(r => r.ChildProfileId == child.Id && r.Status == PetBrainRunStatus.Completed)
            .Select(r => r.TemplateKey)
            .Distinct()
            .ToListAsync(ct);

        return new PetBrainDirectorContext(
            ChildId: child.Id,
            Age: child.Age,
            Language: child.LanguageCode,
            Interests: interests,
            PlayStyles: playStyles,
            RecentRuns: [.. runs.Select(r => new RunHistoryEntry(r.TemplateKey, r.Theme, r.Status))],
            CompletedTemplates: completed.ToHashSet(StringComparer.Ordinal),
            Difficulty: difficulty,
            PetIsHatched: child.Pet?.HatchedAt is not null);
    }

    private static Dictionary<string, int> ScoresOf(ChildProfile child, PetBrainTraitCategory category) =>
        child.Traits
            .Where(t => t.Category == category)
            .ToDictionary(t => t.Key, t => TraitKeys.Clamp(t.Score), StringComparer.Ordinal);

    /// <summary>
    /// Ekran üçün xassə barları. Bazada sətri olmayan açar da göstərilir —
    /// uşaq profilin bütün oxunu görür, yalnız artıq toxunduğu hissəni yox.
    /// </summary>
    private static List<PetBrainTraitDto> TopTraits(
        IReadOnlyDictionary<string, int> scores, IReadOnlyList<string> keys, string language) =>
        [.. keys
            .Select(key => TraitKeys.ToDto(key, scores.GetValueOrDefault(key, TraitKeys.StartingScore), language))
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.Key, StringComparer.Ordinal)];

    private static int ScoreFor(ExperienceTemplate template, ExperienceRun run)
    {
        // Yaradıcı təcrübədə doğru/səhv yoxdur: nəticə həmişə tamdır.
        if (template.Type == PetBrainExperienceType.Creative)
            return 100;

        return Math.Clamp(100 - (run.Mistakes * 20) - (run.HintsUsed * 10), 0, 100);
    }

    private async Task<List<PetMemory>> RecordMemoriesAsync(
        ExperienceRun run,
        ChildProfile child,
        ExperienceTemplate template,
        string unlockedCode,
        bool isFirstAdventureEver,
        DateTime now,
        CancellationToken ct)
    {
        List<PetMemory> created = [];

        // 1) Macəranın özü.
        Remember(
            isFirstAdventureEver ? PetBrainMemoryKind.FirstAdventure : PetBrainMemoryKind.ExperienceCompleted,
            template.Key,
            string.Empty,
            isFirstAdventureEver ? MemoryPolicy.FirstAdventureImportance : MemoryPolicy.CompletionImportance,
            [template.Theme, template.Type.ToString().ToLowerInvariant()]);

        // 2) Ən mənalı seçim — sonuncu, çünki o, macəranın nəticəsini müəyyən edir.
        var lastChoice = run.Choices.LastOrDefault(c => c is not ("continue" or "solved"));
        if (lastChoice is not null)
            Remember(PetBrainMemoryKind.ChoiceMade, template.Key, lastChoice,
                MemoryPolicy.ChoiceImportance, [template.Theme]);

        // 3) Kosmetik.
        if (!string.IsNullOrEmpty(unlockedCode))
            Remember(PetBrainMemoryKind.CosmeticUnlocked, unlockedCode, string.Empty,
                MemoryPolicy.CosmeticImportance, [template.Theme]);

        // 4) Təkrarlanan üstünlük — eyni mövzuda ikinci tamamlamadan sonra.
        var themeCompletions = await _db.ExperienceRuns
            .CountAsync(r => r.ChildProfileId == child.Id
                             && r.Theme == template.Theme
                             && r.Status == PetBrainRunStatus.Completed, ct);

        if (themeCompletions >= 2 && TraitKeys.CategoryOf(template.Theme) is not null)
            Remember(PetBrainMemoryKind.PreferenceObserved, template.Theme, string.Empty,
                MemoryPolicy.PreferenceImportance, [template.Theme]);

        return created;

        void Remember(PetBrainMemoryKind kind, string factKey, string valueKey, int importance, List<string> tags)
        {
            // Eyni fakt iki dəfə xatırlanmır — unikal indeks də bunu qoruyur.
            var existing = child.Memories.FirstOrDefault(m =>
                m.Kind == kind && m.FactKey == factKey && m.ValueKey == valueKey);

            if (existing is not null)
            {
                existing.Importance = Math.Max(existing.Importance, importance);
                created.Add(existing);
                return;
            }

            var memory = new PetMemory
            {
                ChildProfileId = child.Id,
                Kind = kind,
                FactKey = factKey,
                ValueKey = valueKey,
                Importance = importance,
                CreatedAt = now,
                Tags = tags
            };

            child.Memories.Add(memory);
            _db.PetMemories.Add(memory);
            created.Add(memory);
        }
    }

    private PetBrainSummaryDto BuildSummary(
        ExperienceTemplate template,
        string language,
        Pet pet,
        int xp,
        int bond,
        string unlockedCode,
        IReadOnlyList<PetMemory> memories,
        bool leveledUp)
    {
        var accessory = string.IsNullOrEmpty(unlockedCode) ? null : PetAccessories.Find(unlockedCode);

        return new PetBrainSummaryDto
        {
            Title = template.Title(language),
            PetLine = template.Celebration(language),
            XpEarned = xp,
            BondEarned = bond,
            BondTotal = BondRules.Clamp(pet.Bond),
            UnlockedAccessoryCode = unlockedCode,
            UnlockedAccessoryName = accessory is null
                ? string.Empty
                : Localized.T(language, accessory.NameAz, accessory.Name),
            UnlockedAccessoryIcon = accessory?.Icon ?? string.Empty,
            NewMemories = [.. memories.Select(m => MemoryPolicy.ToDto(m, language, pet.Name))],
            PetLeveledUp = leveledUp,
            PetLevel = pet.Level
        };
    }

    /// <summary>Artıq tamamlanmış run üçün yekun — mükafat TƏKRAR tətbiq edilmir.</summary>
    private async Task<PetBrainRunDto> BuildCompletedDtoAsync(
        ExperienceRun run, ChildProfile child, ExperienceTemplate template, CancellationToken ct)
    {
        var language = child.LanguageCode;
        var dto = await ToDtoAsync(run, child, ct);

        var memories = await _db.PetMemories
            .AsNoTracking()
            .Where(m => m.ChildProfileId == child.Id && m.FactKey == template.Key)
            .ToListAsync(ct);

        var unlocked = child.Pet!.UnlockedAccessories.Contains(template.RewardCode)
            ? template.RewardCode
            : string.Empty;

        dto.Summary = BuildSummary(
            template, language, child.Pet, 0, 0, unlocked, memories, leveledUp: false);

        // Təkrar açılışda "yeni əşya!" anı göstərilmir — o, bir dəfəlik andır.
        dto.Summary.UnlockedAccessoryCode = string.Empty;

        // Recap KEŞDƏN gəlir: eyni seçimlər eyni hash verir, deməli təkrar
        // baxış heç nə xərcləmir.
        dto.Summary.Recap = await EnsureRecapAsync(run, ct);

        return dto;
    }

    /// <summary>
    /// Run-un DTO-su. Cari mərhələ tapmacadırsa, tapmaca burada VERİLİR və
    /// saxlanılır — yəni yenilənmə eyni sualı qaytarır.
    /// </summary>
    private async Task<PetBrainRunDto> ToDtoAsync(
        ExperienceRun run,
        ChildProfile child,
        CancellationToken ct,
        bool showHint = false,
        bool puzzleMissed = false)
    {
        var language = child.LanguageCode;
        var template = ExperienceCatalog.Find(run.TemplateKey);

        var dto = new PetBrainRunDto
        {
            RunId = run.Id,
            TemplateKey = run.TemplateKey,
            ExperienceType = run.ExperienceType,
            Theme = run.Theme,
            SceneKey = template?.SceneKey ?? run.Theme,
            Difficulty = run.Difficulty,
            Status = run.Status,
            Title = template?.Title(language) ?? run.TemplateKey,
            CurrentStage = run.CurrentStage,
            StageCount = template?.StageCount ?? 0,
            Choices = [.. run.Choices],
            HintsUsed = run.HintsUsed,
            Mistakes = run.Mistakes
        };

        if (template is null || run.Status != PetBrainRunStatus.Active || run.CurrentStage >= template.StageCount)
            return dto;

        var stage = template.Stages[run.CurrentStage];

        dto.Stage = new PetBrainStageDto
        {
            Index = run.CurrentStage,
            Kind = stage.Kind,
            Prompt = stage.Prompt(language),
            PetLine = stage.PetLine(language)
        };

        if (stage.Kind != PetBrainStageKind.Puzzle)
        {
            dto.Stage.Options = [.. stage.Options.Select(o => ToOptionDto(o, language))];
            return dto;
        }

        var issued = await EnsurePuzzleAsync(run, template, child, ct);
        var puzzle = ReadPublic(issued);

        // Cəhd sayı yalnız göstərmək üçündür — ruhlandırıcı mesaj ondan asılıdır.
        puzzle.Attempts = issued.Attempts;

        // Səhnə HƏR OXUNUŞDA yenidən hesablanır, saxlanan məzmundan yox:
        // rəsm gec hazır olanda uşaq növbəti kadrda onu görməlidir. Həndəsə
        // dəyişmir — yalnız fon.
        await ApplySceneAsync(puzzle, issued, template, child, ct);

        // İpucu YALNIZ istənəndə (və ya dəstək rejimində) göndərilir; əks halda
        // sahə boş qalır və klientə heç nə sızmır.
        if (!showHint && issued.HintsUsed == 0 && !issued.Assisted)
            puzzle.Hint = string.Empty;

        dto.Stage.Puzzle = puzzle;
        dto.Stage.SupportsHint = puzzle.HintAvailable;
        dto.Stage.Hint = puzzle.Hint;
        dto.Stage.Prompt = puzzle.Instruction;

        if (puzzleMissed)
            dto.Stage.PetLine = Localized.T(language,
                "Yaxın idi! Bir də bax — mən yanındayam.",
                "So close! Look again — I am right here with you.");

        return dto;
    }

    // ==================== Tapmaca ====================

    /// <summary>
    /// Bu mərhələ üçün tapmacanı gətirir; yoxdursa YARADIR və saxlayır.
    ///
    /// <para>Saxlanması üç şey üçün vacibdir: yenilənmədən sonra EYNİ sual
    /// qayıtmalıdır, doğru həll yalnız serverdə qalmalıdır, təkrar isə barmaq
    /// izi ilə tanınmalıdır.</para>
    ///
    /// <para>İki eyni vaxtlı sorğu eyni anda yaratmağa çalışsa, bazadakı unikal
    /// indeks ikincisini dayandırır — o zaman mövcud sətir oxunur, yəni uşaq
    /// sualın dəyişdiyini görmür.</para>
    /// </summary>
    private async Task<IssuedPuzzle> EnsurePuzzleAsync(
        ExperienceRun run, ExperienceTemplate template, ChildProfile child, CancellationToken ct)
    {
        var existing = await _db.IssuedPuzzles
            .FirstOrDefaultAsync(p => p.ExperienceRunId == run.Id && p.StageIndex == run.CurrentStage, ct);

        if (existing is not null)
        {
            // Kataloq dəyişəndə (mexanika silinəndə, açar adlananda) köhnə
            // sətir OXUNMAZ qalır. Uşaq bunun günahkarı deyil: yarımçıq
            // macərası 500 xətası ilə bitməməlidir, ona görə oxunmayan sətir
            // atılır və mərhələ üçün yeni tapmaca verilir.
            //
            // Bu, «asan sual ovlamağa» qapı açmır: şərt uşağın nəzarətində
            // deyil — o, ancaq bizim buraxdığımız kataloq dəyişikliyi ilə
            // yaranır və hər mərhələ üçün bir dəfə işləyir.
            if (IsReadable(existing))
                return existing;

            _logger.LogWarning(
                "PetBrain: köhnə tapmaca atılır (run {RunId}, mərhələ {Stage}, şablon {Blueprint}).",
                run.Id, existing.StageIndex, existing.BlueprintKey);

            _db.IssuedPuzzles.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }

        var context = await BuildPuzzleContextAsync(run, template, child, ct);
        var generated = _puzzles.Generate(context);

        var now = _clock.GetUtcNow().UtcDateTime;

        // Səhnə tapmaca QURULANDAN SONRA açılır və ona heç nə əlavə etmir:
        // rəsm yalnız fondur. Model burada çağırılmır — sətir açılır, iş isə
        // arxa fona verilir, yəni uşaq gözləmir.
        var sceneSpec = SceneSpecFor(generated.Blueprint, template, child);

        if (await _illustrations.EnsureRowAsync(sceneSpec, ct) is { Status: PetBrainIllustrationStatus.Pending })
            _sceneQueue.Enqueue(sceneSpec);

        // Alt mətn NƏZARƏTLİ şablondandır — modelin çıxışından yox.
        generated.Public.Scene.AltText = sceneSpec.AltText();
        generated.Public.Scene.OverlayLayout = generated.Blueprint.Key;

        // Id ƏVVƏLCƏ verilir: uşağa gedən məzmun onu daşımalıdır, yəni
        // seriallaşdırmadan əvvəl məlum olmalıdır.
        var puzzleId = Guid.NewGuid();
        generated.Public.PuzzleId = puzzleId;

        var issued = new IssuedPuzzle
        {
            Id = puzzleId,
            ChildProfileId = child.Id,
            ExperienceRunId = run.Id,
            StageIndex = run.CurrentStage,
            BlueprintKey = generated.Blueprint.Key,
            BlueprintVersion = generated.Blueprint.Version,
            GeneratorVersion = PuzzleSeed.GeneratorVersion,
            Mechanic = generated.Blueprint.Mechanic,
            Seed = generated.SeedHex,
            PublicPayload = JsonSerializer.Serialize(generated.Public, PuzzleJson),
            PrivateSolution = JsonSerializer.Serialize(generated.Solution, PuzzleJson),
            ContentSignature = generated.Signature,
            SceneSpecHash = sceneSpec.Hash(),
            Difficulty = run.Difficulty,
            Assisted = run.Assisted,
            Status = PetBrainPuzzleStatus.Issued,
            IssuedAt = now
        };

        _db.IssuedPuzzles.Add(issued);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Başqa sorğu qabaqladı: öz namizədimizi ataraq mövcud tapmacanı oxuyuruq.
            _db.Entry(issued).State = EntityState.Detached;

            return await _db.IssuedPuzzles
                .FirstAsync(p => p.ExperienceRunId == run.Id && p.StageIndex == run.CurrentStage, ct);
        }

        return issued;
    }

    private async Task<PuzzleGenerationContext> BuildPuzzleContextAsync(
        ExperienceRun run, ExperienceTemplate template, ChildProfile child, CancellationToken ct)
    {
        // Son tapmacaların barmaq izləri — eyni sual dalbadal təkrarlanmasın.
        var recent = await _db.IssuedPuzzles
            .AsNoTracking()
            .Where(p => p.ChildProfileId == child.Id)
            .OrderByDescending(p => p.IssuedAt)
            .Take(RecentPuzzleWindow)
            .Select(p => p.ContentSignature)
            .ToListAsync(ct);

        var averageRating = child.SkillMasteries.Count > 0
            ? (int)child.SkillMasteries.Average(m => m.Rating)
            : AdaptiveEngine.StartingRating;

        return new PuzzleGenerationContext(
            ChildId: child.Id,
            RunId: run.Id,
            StageIndex: run.CurrentStage,
            Age: child.Age,
            Language: child.LanguageCode,
            ExperienceType: template.Type,
            Theme: template.Theme,
            Interests: ScoresOf(child, PetBrainTraitCategory.Interest),
            PlayStyles: ScoresOf(child, PetBrainTraitCategory.PlayStyle),
            Difficulty: run.Difficulty,
            MasteryTargetDifficulty: AdaptiveEngine.TargetDifficulty(averageRating),
            Assisted: run.Assisted,
            RecentSignatures: recent.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// Səhnənin CARİ vəziyyətini tapmacaya yazır.
    ///
    /// <para>Rəsm hazır deyilsə <c>AssetUrl</c> BOŞ qalır və klient
    /// deterministik SVG/CSS səhnəsini çəkir — eyni həndəsə ilə. Rəsm hazır
    /// olanda yalnız fon dəyişir, toxunuş hədəfləri tərpənmir.</para>
    ///
    /// <para>Sətir hələ <c>Pending</c>-dirsə növbəyə YENİDƏN qoyulur: proses
    /// yenidən başlasa növbə itir, amma bazadakı sətir qalır — uşaq tapmacanı
    /// açan kimi iş bərpa olunur.</para>
    /// </summary>
    public async Task<string?> GetIllustrationKeyAsync(
        Guid childId, Guid puzzleId, CancellationToken ct = default)
    {
        // Sahiblik SORĞUNUN İÇİNDƏDİR: yad uşağın tapmacası ümumiyyətlə
        // tapılmır, yəni "var, amma sənin deyil" fərqi görünmür.
        var hash = await _db.IssuedPuzzles
            .AsNoTracking()
            .Where(p => p.Id == puzzleId && p.ChildProfileId == childId)
            .Select(p => p.SceneSpecHash)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrEmpty(hash))
            return null;

        var row = await _db.PuzzleIllustrations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.SceneSpecHash == hash, ct);

        return row is { Status: PetBrainIllustrationStatus.Ready } && !string.IsNullOrEmpty(row.AssetKey)
            ? row.AssetKey
            : null;
    }

    private async Task ApplySceneAsync(
        PetBrainPuzzleDto puzzle,
        IssuedPuzzle issued,
        ExperienceTemplate template,
        ChildProfile child,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(issued.SceneSpecHash))
            return;

        var row = await _db.PuzzleIllustrations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.SceneSpecHash == issued.SceneSpecHash, ct);

        if (row is null)
            return;

        puzzle.Scene.IllustrationStatus = row.Status;

        puzzle.Scene.AssetUrl = row.Status == PetBrainIllustrationStatus.Ready
            ? $"/api/pet-brain/puzzles/{issued.Id}/illustration"
            : string.Empty;

        if (row.Status != PetBrainIllustrationStatus.Pending)
            return;

        // Növbə prosesdaxilidir və yenidən başlatmada itir; bazadakı sətir isə
        // qalır. Ona görə gözləyən səhnə hər açılışda yenidən növbəyə düşür —
        // növbə özü təkrarı süzür, iş isə ikinci dəfə başlamır (sətir artıq
        // Pending deyilsə işçi dərhal qayıdır).
        if (PuzzleBlueprintCatalog.Find(issued.BlueprintKey) is { } blueprint)
            _sceneQueue.Enqueue(SceneSpecFor(blueprint, template, child));
    }

    /// <summary>
    /// Səhnə təsviri — YALNIZ təsdiqlənmiş kataloq sahələrindən.
    ///
    /// <para>Pet-in adı QƏSDƏN yoxdur: onu uşaq yazır, deməli sərbəst mətndir
    /// və modelə getməməlidir. Növ isə qapalı siyahıdandır.</para>
    /// </summary>
    private static PuzzleSceneSpec SceneSpecFor(
        PuzzleBlueprint blueprint, ExperienceTemplate template, ChildProfile child) =>
        PuzzleSceneSpec.For(
            blueprint, child.LanguageCode, template, child.Pet?.Species ?? string.Empty);

    /// <summary>
    /// Saxlanan tapmaca hələ də bu buraxılışla oxunurmu: şablon kataloqdadır və
    /// JSON cari müqaviləyə uyğundur.
    /// </summary>
    private static bool IsReadable(IssuedPuzzle issued)
    {
        if (!PuzzleBlueprintCatalog.IsKnown(issued.BlueprintKey))
            return false;

        try
        {
            return JsonSerializer.Deserialize<PetBrainPuzzleDto>(issued.PublicPayload, PuzzleJson) is not null
                && JsonSerializer.Deserialize<PuzzleSolution>(issued.PrivateSolution, PuzzleJson) is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static PetBrainPuzzleDto ReadPublic(IssuedPuzzle issued) =>
        JsonSerializer.Deserialize<PetBrainPuzzleDto>(issued.PublicPayload, PuzzleJson)
        ?? new PetBrainPuzzleDto { PuzzleId = issued.Id };

    private static PuzzleSolution ReadSolution(IssuedPuzzle issued) =>
        JsonSerializer.Deserialize<PuzzleSolution>(issued.PrivateSolution, PuzzleJson)
        ?? new PuzzleSolution([], PetBrainAnswerKind.SelectIds);

    private static PetBrainOptionDto ToOptionDto(ExperienceOption option, string language) => new()
    {
        Key = option.Key,
        Label = option.Label(language),
        Icon = option.Icon,
        Detail = option.Detail(language)
    };

    private async Task<PetBrainRecommendationDto> ToDtoAsync(
        PetBrainRecommendation decision, ChildProfile child, CancellationToken ct)
    {
        var language = child.LanguageCode;
        var template = decision.Template;

        var narrative = await _narrative.DescribeAsync(new NarrativeContext(
            Language: language,
            AgeBand: AgeBand(child.Age),
            Template: template,
            Difficulty: decision.Difficulty,
            PetName: child.Pet?.Name ?? "Pet",
            MemoryKeys: [.. child.Memories.Select(m => m.FactKey).Distinct().Take(5)]), ct);

        return new PetBrainRecommendationDto
        {
            TemplateKey = template.Key,
            ExperienceType = template.Type,
            Theme = template.Theme,
            ActivityType = template.ActivityType,
            Difficulty = decision.Difficulty,
            TargetMinutes = template.TargetMinutes,
            Title = narrative.Title,
            Intro = narrative.Intro,
            Icon = template.Icon,
            RewardCode = template.RewardCode,
            AlreadyCompleted = decision.AlreadyCompleted,
            Reasons = [.. decision.Reasons],
            NarrativeSource = narrative.Source
        };
    }

    private async Task<PetBrainDebugDto> BuildDebugAsync(
        Guid childId,
        PetBrainRecommendation? decision,
        PetBrainDifficulty difficulty,
        RunPerformance? lastPerformance,
        string language,
        string narrativeSource,
        CancellationToken ct)
    {
        var events = await _db.BehaviorEvents
            .AsNoTracking()
            .Where(e => e.ChildProfileId == childId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(DebugEventLimit)
            .Select(e => new PetBrainEventDto
            {
                Type = e.Type,
                Source = e.Source,
                OccurredAt = e.OccurredAt
            })
            .ToListAsync(ct);

        return new PetBrainDebugDto
        {
            Difficulty = difficulty,
            DifficultySignal = ExperienceDifficulty.Signal(lastPerformance, language),
            RecentEvents = events,
            NarrativeSource = narrativeSource,
            Candidates = [.. (decision?.Candidates ?? []).Select(c => new PetBrainCandidateDto
            {
                TemplateKey = c.Template.Key,
                Theme = c.Template.Theme,
                FitScore = c.FitScore,
                NoveltyScore = c.NoveltyScore,
                SurpriseScore = c.SurpriseScore,
                TotalScore = (int)Math.Round(c.TotalScore, MidpointRounding.AwayFromZero),
                Selected = decision is not null && c.Template.Key == decision.Template.Key
            })]
        };
    }

    private static string AgeBand(int age) => age switch
    {
        <= 6 => "5-6",
        <= 8 => "7-8",
        _ => "9-10"
    };

    private static ServiceResult<T> Disabled<T>() =>
        ServiceResult<T>.NotFound(Localized.T("Bu bölmə hazırda bağlıdır.", "This section is currently closed."));
}
