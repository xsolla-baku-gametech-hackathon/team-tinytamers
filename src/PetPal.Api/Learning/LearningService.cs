using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Missions;
using PetPal.Api.Pets;
using PetPal.Api.Progress;
using PetPal.Api.Rewards;
using PetPal.Api.Social;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Enums;

namespace PetPal.Api.Learning;

/// <summary>
/// Əsas oyun dövrəsi: pet tapşırıq verir → uşaq həll edir → ulduz/XP qazanır →
/// pet inkişaf edir, missiyalar irəliləyir, dünya dəyişir.
/// </summary>
public class LearningService : ILearningService
{
    private const int PerfectRoundGemBonus = 1;

    /// <summary>Sprint qısa olmalıdır — məqsəd uşağı 1–2 dəqiqəyə cəlb etməkdir.</summary>
    private const int SprintQuestionCount = 3;

    /// <summary>Sprint tam doğru tamamlananda verilən bonus.</summary>
    private const int SprintBonusStars = 15;

    private readonly AppDbContext _db;
    private readonly IQuestionSelector _selector;
    private readonly IRewardService _rewards;
    private readonly IMissionProgressTracker _missions;
    private readonly IDailyGoalService _dailyGoals;
    private readonly ITeamMissionTracker _teamMissions;
    private readonly TimeProvider _clock;
    private readonly ScreenTimeOptions _screenTime;

    public LearningService(
        AppDbContext db,
        IQuestionSelector selector,
        IRewardService rewards,
        IMissionProgressTracker missions,
        IDailyGoalService dailyGoals,
        ITeamMissionTracker teamMissions,
        TimeProvider clock,
        IOptions<ScreenTimeOptions> screenTime)
    {
        _db = db;
        _selector = selector;
        _rewards = rewards;
        _missions = missions;
        _dailyGoals = dailyGoals;
        _teamMissions = teamMissions;
        _clock = clock;
        _screenTime = screenTime.Value;
    }

    public async Task<ServiceResult<LearningSessionDto>> StartSessionAsync(
        Guid childId, StartSessionRequest request, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .Include(c => c.SkillMasteries)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child is null)
            return ServiceResult<LearningSessionDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        // Yuxu rejimi və gündəlik limit YENİ sessiyanı bloklayır.
        var now = _clock.GetUtcNow().UtcDateTime;
        var todayGoal = await _dailyGoals.GetOrCreateTodayAsync(child, ct);
        var screenTime = ScreenTimeGuard.Evaluate(child, todayGoal, now, _screenTime.Enforced);
        if (screenTime != ScreenTimeState.Allowed)
        {
            await _db.SaveChangesAsync(ct);
            return ServiceResult<LearningSessionDto>.Forbidden(
                ScreenTimeGuard.MessageFor(screenTime, child.LanguageCode, child));
        }

        EnsureMasteries(child);

        var skill = request.Skill ?? PickFocusSkill(child);
        var mastery = child.SkillMasteries.First(m => m.Skill == skill);

        // Sprint qısadır — hərəkətsizlikdən sonrakı təklif uzun olmamalıdır.
        var questionCount = request.IsSprint ? SprintQuestionCount : request.QuestionCount;

        var questions = await _selector.SelectAsync(
            childId, skill, child.LanguageCode, mastery.Rating, child.Age, questionCount, ct);

        if (questions.Count == 0)
            return ServiceResult<LearningSessionDto>.NotFound(Localized.T("Bu bacarıq üçün uyğun sual tapılmadı.", "No suitable question was found for this skill."));

        var session = new LearningSession
        {
            ChildProfileId = childId,
            Skill = skill,
            IsSprint = request.IsSprint,
            StartedAt = now,
            TotalCount = questions.Count
        };

        for (var i = 0; i < questions.Count; i++)
            session.Answers.Add(new SessionAnswer { QuestionId = questions[i].Id, Order = i });

        _db.LearningSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<LearningSessionDto>.Ok(new LearningSessionDto
        {
            SessionId = session.Id,
            Skill = skill,
            IsSprint = session.IsSprint,
            Questions = questions.Select(ToDto).ToList()
        });
    }

    public async Task<ServiceResult<AnswerResultDto>> SubmitAnswerAsync(
        Guid childId, SubmitAnswerRequest request, CancellationToken ct = default)
    {
        var session = await _db.LearningSessions
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, ct);

        if (session is null || session.ChildProfileId != childId)
            return ServiceResult<AnswerResultDto>.NotFound(Localized.T("Sessiya tapılmadı.", "Session not found."));

        if (session.CompletedAt is not null)
            return ServiceResult<AnswerResultDto>.Fail(Localized.T("Bu sessiya artıq tamamlanıb.", "This session is already finished."));

        var slot = session.Answers.FirstOrDefault(a => a.QuestionId == request.QuestionId);
        if (slot is null)
            return ServiceResult<AnswerResultDto>.NotFound(Localized.T("Bu sual sessiyaya aid deyil.", "This question does not belong to the session."));

        if (slot.IsAnswered)
            return ServiceResult<AnswerResultDto>.Conflict(Localized.T("Bu sual artıq cavablandırılıb.", "This question has already been answered."));

        var question = await _db.Questions.AsNoTracking().FirstAsync(q => q.Id == request.QuestionId, ct);
        if (request.ChosenIndex < 0 || request.ChosenIndex >= question.Options.Count)
            return ServiceResult<AnswerResultDto>.Fail(Localized.T("Cavab variantı düzgün deyil.", "That answer choice is not valid."));

        var child = await _db.ChildProfiles
            .Include(c => c.SkillMasteries)
            .Include(c => c.Pet)
            .FirstAsync(c => c.Id == childId, ct);

        EnsureMasteries(child);

        var now = _clock.GetUtcNow().UtcDateTime;
        var isCorrect = request.ChosenIndex == question.CorrectIndex;

        slot.ChosenIndex = request.ChosenIndex;
        slot.IsCorrect = isCorrect;
        slot.ElapsedMs = request.ElapsedMs;
        slot.AnsweredAt = now;

        // 1) Adaptiv reytinq
        var mastery = child.SkillMasteries.First(m => m.Skill == question.Skill);
        mastery.Rating = AdaptiveEngine.UpdateRating(mastery.Rating, question.Difficulty, isCorrect);
        mastery.AnsweredCount++;
        if (isCorrect)
            mastery.CorrectCount++;
        mastery.UpdatedAt = now;

        // 2) Mükafat və pet inkişafı
        var stars = AdaptiveEngine.StarsFor(question.Difficulty, isCorrect);
        var xp = AdaptiveEngine.XpFor(question.Difficulty, isCorrect);

        session.CorrectCount += isCorrect ? 1 : 0;
        session.StarsEarned += stars;
        session.XpEarned += xp;

        if (stars > 0)
            await _rewards.GrantStarsAsync(child, stars, $"{question.Skill} question", ct);

        if (child.Pet is not null)
        {
            PetProgression.Refresh(child.Pet, now);
            PetProgression.AddXp(child.Pet, xp);

            // Doğru cavab pet-i sevindirir — geri əlaqə dərhal görünür.
            if (isCorrect)
                child.Pet.Happiness = PetProgression.Clamp(child.Pet.Happiness + 3);
        }

        // 3) Gündəlik hədəf və missiyalar yalnız doğru cavabda irəliləyir
        var goal = await _dailyGoals.GetOrCreateTodayAsync(child, ct);
        goal.AnsweredCount++;

        if (isCorrect)
        {
            goal.CorrectCount++;
            goal.Completed++;
            await _missions.TrackAsync(childId, MissionType.SolveQuestions, question.Skill, 1, ct);
            await _teamMissions.TrackCorrectAnswerAsync(childId, ct);
        }

        await _db.SaveChangesAsync(ct);

        if (isCorrect)
            await _dailyGoals.SettleIfReachedAsync(child, goal, ct);

        return ServiceResult<AnswerResultDto>.Ok(new AnswerResultDto
        {
            IsCorrect = isCorrect,
            CorrectIndex = question.CorrectIndex,
            Explanation = question.Explanation,
            StarsEarned = stars,
            XpEarned = xp,
            PetReaction = PetVoice.AnswerReaction(
                child.LanguageCode, isCorrect, session.CorrectCount, question.Skill, child.Pet?.Name ?? "Pet"),
            DailyGoalDone = goal.Completed,
            DailyGoalTarget = goal.Target
        });
    }

    public async Task<ServiceResult<SessionSummaryDto>> CompleteSessionAsync(
        Guid childId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await _db.LearningSessions
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null || session.ChildProfileId != childId)
            return ServiceResult<SessionSummaryDto>.NotFound(Localized.T("Sessiya tapılmadı.", "Session not found."));

        var child = await _db.ChildProfiles
            .Include(c => c.Pet)
            .FirstAsync(c => c.Id == childId, ct);

        var levelBefore = child.Pet?.Level ?? 1;
        var answered = session.Answers.Count(a => a.IsAnswered);
        var sprintBonus = 0;

        if (session.CompletedAt is null)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            session.CompletedAt = now;

            // Cavabsız qalan suallar ümumi saydan çıxılır ki, dəqiqlik ədalətli hesablansın.
            session.TotalCount = answered;

            var minutes = Math.Clamp((int)Math.Round((now - session.StartedAt).TotalMinutes), 0, 60);
            var goal = await _dailyGoals.GetOrCreateTodayAsync(child, ct);
            goal.MinutesSpent += minutes;

            var isPerfect = answered >= 3 && session.CorrectCount == answered;

            if (isPerfect)
                await _rewards.GrantGemsAsync(child, PerfectRoundGemBonus, "Perfect round", ct);

            if (session.IsSprint && isPerfect)
            {
                sprintBonus = SprintBonusStars;
                session.StarsEarned += sprintBonus;
                await _rewards.GrantStarsAsync(child, sprintBonus, "Knowledge sprint", ct);
            }

            await _db.SaveChangesAsync(ct);
        }

        var newBadges = await _rewards.EvaluateBadgesAsync(childId, ct);
        var levelAfter = child.Pet?.Level ?? 1;

        return ServiceResult<SessionSummaryDto>.Ok(new SessionSummaryDto
        {
            SessionId = session.Id,
            Skill = session.Skill,
            CorrectCount = session.CorrectCount,
            TotalCount = session.TotalCount,
            AccuracyPercent = session.TotalCount == 0 ? 0 : session.CorrectCount * 100 / session.TotalCount,
            StarsEarned = session.StarsEarned,
            XpEarned = session.XpEarned,
            PetLevel = levelAfter,
            PetLeveledUp = levelAfter > levelBefore,
            PetStage = PetProgression.StageFor(levelAfter),
            NewBadges = newBadges,
            SprintBonusStars = sprintBonus,
            PetMessage = PetVoice.SessionSummary(
                child.LanguageCode, session.CorrectCount, session.TotalCount, child.Pet?.Name ?? "Pet")
        });
    }

    /// <summary>Ən zəif bacarıq sahəsi seçilir — uşaq özü seçməyəndə fokus avtomatik qurulur.</summary>
    private static SkillArea PickFocusSkill(ChildProfile child) =>
        child.SkillMasteries.OrderBy(m => m.Rating).ThenBy(m => m.AnsweredCount).First().Skill;

    private static void EnsureMasteries(ChildProfile child)
    {
        foreach (var skill in Enum.GetValues<SkillArea>())
        {
            if (child.SkillMasteries.All(m => m.Skill != skill))
                child.SkillMasteries.Add(new SkillMastery { Skill = skill, Rating = AdaptiveEngine.StartingRating });
        }
    }

    private static QuestionDto ToDto(Question question) => new()
    {
        Id = question.Id,
        Skill = question.Skill,
        Difficulty = question.Difficulty,
        Prompt = question.Prompt,
        Options = question.Options.ToList(),
        Hint = question.Hint
    };
}
