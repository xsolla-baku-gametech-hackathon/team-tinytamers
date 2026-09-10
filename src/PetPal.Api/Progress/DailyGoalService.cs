using Microsoft.EntityFrameworkCore;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Missions;
using PetPal.Api.Rewards;
using PetPal.Shared.Enums;

namespace PetPal.Api.Progress;

public class DailyGoalService : IDailyGoalService
{
    private const int GoalBonusStars = 25;
    private const int GoalBonusGems = 1;

    private readonly AppDbContext _db;
    private readonly IRewardService _rewards;
    private readonly IMissionProgressTracker _missions;
    private readonly TimeProvider _clock;

    public DailyGoalService(AppDbContext db, IRewardService rewards, IMissionProgressTracker missions, TimeProvider clock)
    {
        _db = db;
        _rewards = rewards;
        _missions = missions;
        _clock = clock;
    }

    public async Task<DailyGoal> GetOrCreateTodayAsync(ChildProfile child, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        var goal = await _db.DailyGoals
            .FirstOrDefaultAsync(g => g.ChildProfileId == child.Id && g.Date == today, ct);

        if (goal is not null)
            return goal;

        goal = new DailyGoal
        {
            ChildProfileId = child.Id,
            Date = today,
            Target = child.DailyGoalTarget
        };

        _db.DailyGoals.Add(goal);
        return goal;
    }

    public async Task<bool> SettleIfReachedAsync(ChildProfile child, DailyGoal goal, CancellationToken ct = default)
    {
        if (goal.RewardGranted || goal.Completed < goal.Target)
            return false;

        goal.RewardGranted = true;

        var yesterday = goal.Date.AddDays(-1);
        child.StreakDays = child.LastActiveOn == yesterday ? child.StreakDays + 1 : 1;
        child.LastActiveOn = goal.Date;

        await _rewards.GrantStarsAsync(child, GoalBonusStars, "Daily goal complete", ct);
        await _rewards.GrantGemsAsync(child, GoalBonusGems, "Daily goal complete", ct);
        await _missions.SetProgressAsync(child.Id, MissionType.KeepStreak, child.StreakDays, ct);

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
