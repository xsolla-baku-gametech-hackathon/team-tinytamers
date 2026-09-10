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

        // Sətir dəyişiklik izləyicisi ilə ƏLAVƏ EDİLƏ BİLMƏZ.
        //
        // Əlavə etmək yazma anını çağıranın `SaveChangesAsync`-inə qədər
        // gecikdirir; iki paralel sorğu isə aradan həmin pəncərədə keçir və hər
        // ikisi eyni (uşaq, gün) sətrini yazmağa çalışır. İkincisi unikal
        // indeksdə dayanır və çağıranın BÜTÜN yazması — macəra, hadisə, mükafat —
        // idarə olunmayan xəta ilə çökür. Uşaq üçün bu, "başla" düyməsinə iki
        // dəfə toxunmaqla eyni idi.
        //
        // Ona görə sətir öz atomik addımı ilə yaradılır: uduzan tərəf səssizcə
        // mövcud sətri oxuyur. `ON CONFLICT DO NOTHING` həm PostgreSQL, həm də
        // testlərdəki SQLite tərəfindən dəstəklənir.
        await _db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "DailyGoals" ("Id", "ChildProfileId", "Date", "Target", "Completed", "CorrectCount", "AnsweredCount", "MinutesSpent", "RewardGranted")
             VALUES ({Guid.NewGuid()}, {child.Id}, {today}, {child.DailyGoalTarget}, 0, 0, 0, 0, {false})
             ON CONFLICT ("ChildProfileId", "Date") DO NOTHING
             """, ct);

        return await _db.DailyGoals
            .FirstAsync(g => g.ChildProfileId == child.Id && g.Date == today, ct);
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
