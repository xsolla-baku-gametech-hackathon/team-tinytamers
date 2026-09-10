using Microsoft.EntityFrameworkCore;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Learning;
using PetPal.Api.Rewards;
using PetPal.Shared.Dtos.Progress;
using PetPal.Shared.Enums;

namespace PetPal.Api.Progress;

public class ProgressService : IProgressService
{
    /// <summary>Ən zəif bu qədər sahə "focus area" kimi işarələnir.</summary>
    private const int FocusAreaCount = 2;

    private readonly AppDbContext _db;
    private readonly IRewardService _rewards;
    private readonly TimeProvider _clock;

    public ProgressService(AppDbContext db, IRewardService rewards, TimeProvider clock)
    {
        _db = db;
        _rewards = rewards;
        _clock = clock;
    }

    public async Task<ServiceResult<ProgressSummaryDto>> GetSummaryAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == childId, ct);
        if (child is null)
            return ServiceResult<ProgressSummaryDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var skills = await GetSkillsAsync(childId, ct);
        var answered = skills.Sum(s => s.AnsweredCount);
        var correct = skills.Sum(s => s.CorrectCount);

        return ServiceResult<ProgressSummaryDto>.Ok(new ProgressSummaryDto
        {
            Wallet = await _rewards.GetWalletAsync(childId, ct),
            DailyGoal = await GetTodayGoalAsync(childId, ct),
            Skills = skills,
            TotalAnswered = answered,
            AverageAccuracyPercent = answered == 0 ? 0 : correct * 100 / answered,
            Badges = await _rewards.GetBadgesAsync(childId, ct)
        });
    }

    public async Task<List<SkillProgressDto>> GetSkillsAsync(Guid childId, CancellationToken ct = default)
    {
        var masteries = await _db.SkillMasteries
            .AsNoTracking()
            .Where(m => m.ChildProfileId == childId)
            .ToListAsync(ct);

        var bySkill = masteries.ToDictionary(m => m.Skill);

        var result = Enum.GetValues<SkillArea>().Select(skill =>
        {
            bySkill.TryGetValue(skill, out var mastery);
            var rating = mastery?.Rating ?? AdaptiveEngine.StartingRating;
            var answered = mastery?.AnsweredCount ?? 0;
            var correct = mastery?.CorrectCount ?? 0;

            return new SkillProgressDto
            {
                Skill = skill,
                Rating = rating,
                MasteryPercent = AdaptiveEngine.MasteryPercent(rating),
                AnsweredCount = answered,
                CorrectCount = correct,
                AccuracyPercent = answered == 0 ? 0 : correct * 100 / answered
            };
        }).ToList();

        foreach (var focus in result.OrderBy(s => s.Rating).Take(FocusAreaCount))
            focus.IsFocusArea = true;

        return result;
    }

    public async Task<DailyGoalDto> GetTodayGoalAsync(Guid childId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        var child = await _db.ChildProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == childId, ct);
        var goal = await _db.DailyGoals
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.ChildProfileId == childId && g.Date == today, ct);

        return new DailyGoalDto
        {
            Date = today,
            Target = goal?.Target ?? child?.DailyGoalTarget ?? 5,
            Completed = goal?.Completed ?? 0,
            StreakDays = child?.StreakDays ?? 0
        };
    }
}
