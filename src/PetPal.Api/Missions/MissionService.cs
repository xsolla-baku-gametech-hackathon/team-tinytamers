using Microsoft.EntityFrameworkCore;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Rewards;
using PetPal.Shared.Dtos.Missions;
using PetPal.Shared.Enums;

namespace PetPal.Api.Missions;

public class MissionService : IMissionService, IMissionProgressTracker
{
    private readonly AppDbContext _db;
    private readonly IRewardService _rewards;
    private readonly TimeProvider _clock;

    public MissionService(AppDbContext db, IRewardService rewards, TimeProvider clock)
    {
        _db = db;
        _rewards = rewards;
        _clock = clock;
    }

    public async Task TrackAsync(Guid childId, MissionType type, SkillArea? skill, int amount, CancellationToken ct = default)
    {
        if (amount <= 0)
            return;

        foreach (var state in await LoadTrackableAsync(childId, type, ct))
        {
            // Bacarığa bağlı missiya yalnız həmin bacarıqdan gələn irəliləyişi sayır.
            if (state.Mission.Skill.HasValue && state.Mission.Skill != skill)
                continue;

            Advance(state.ChildMission, state.Mission, state.ChildMission.Progress + amount);
        }
    }

    public async Task SetProgressAsync(Guid childId, MissionType type, int value, CancellationToken ct = default)
    {
        foreach (var state in await LoadTrackableAsync(childId, type, ct))
            Advance(state.ChildMission, state.Mission, Math.Max(state.ChildMission.Progress, value));
    }

    public async Task<ServiceResult<WorldStateDto>> GetWorldAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles.FirstOrDefaultAsync(c => c.Id == childId, ct);
        if (child is null)
            return ServiceResult<WorldStateDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var zones = await _db.WorldZones
            .Include(z => z.Missions.Where(m => m.IsActive))
            .OrderBy(z => z.SortOrder)
            .ToListAsync(ct);

        var childMissions = await EnsureChildMissionsAsync(childId, zones.SelectMany(z => z.Missions), ct);

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var weekStart = today.AddDays(-6);
        var week = await _db.DailyGoals
            .AsNoTracking()
            .Where(g => g.ChildProfileId == childId && g.Date >= weekStart && g.Date <= today)
            .Select(g => new { g.Completed, g.Target })
            .ToListAsync(ct);

        var score = WorldStateCalculator.EngagementScore(
            week.Select(g => (g.Completed, g.Completed >= g.Target)));
        var weather = WorldStateCalculator.WeatherFor(score);

        var dto = new WorldStateDto
        {
            Weather = weather,
            EngagementScore = score,
            Headline = WorldStateCalculator.HeadlineFor(weather, child.LanguageCode),
            Zones = zones.Select(zone =>
            {
                var missions = zone.Missions
                    .OrderBy(m => m.SortOrder)
                    .Select(m => ToDto(m, childMissions[m.Id], zone.Code, child.LanguageCode))
                    .ToList();

                var claimed = missions.Count(m => m.Status == MissionStatus.Claimed);

                return new WorldZoneDto
                {
                    Code = zone.Code,
                    Name = Localized.Pick(child.LanguageCode, zone.Name, zone.NameAz),
                    Description = Localized.Pick(child.LanguageCode, zone.Description, zone.DescriptionAz),
                    IconKey = zone.IconKey,
                    RequiredStars = zone.RequiredStars,
                    IsUnlocked = child.Stars >= zone.RequiredStars,
                    RestoredPercent = missions.Count == 0 ? 0 : claimed * 100 / missions.Count,
                    Missions = missions
                };
            }).ToList()
        };

        await _db.SaveChangesAsync(ct);
        return ServiceResult<WorldStateDto>.Ok(dto);
    }

    public async Task<ServiceResult<ClaimMissionResultDto>> ClaimAsync(Guid childId, Guid missionId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles.FirstOrDefaultAsync(c => c.Id == childId, ct);
        if (child is null)
            return ServiceResult<ClaimMissionResultDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var mission = await _db.Missions
            .Include(m => m.WorldZone)
            .FirstOrDefaultAsync(m => m.Id == missionId && m.IsActive, ct);

        if (mission is null)
            return ServiceResult<ClaimMissionResultDto>.NotFound(Localized.T("Missiya tapılmadı.", "Mission not found."));

        if (child.Stars < mission.WorldZone.RequiredStars)
        {
            var zoneName = Localized.Pick(child.LanguageCode, mission.WorldZone.Name, mission.WorldZone.NameAz);
            return ServiceResult<ClaimMissionResultDto>.Forbidden(
                Localized.Normalize(child.LanguageCode) == Localized.Azerbaijani
                    ? $"{zoneName} hələ bağlıdır. Açmaq üçün {mission.WorldZone.RequiredStars} ulduz lazımdır."
                    : $"{zoneName} is still locked. Earn {mission.WorldZone.RequiredStars} stars to unlock it.");
        }

        var childMission = await _db.ChildMissions
            .FirstOrDefaultAsync(cm => cm.ChildProfileId == childId && cm.MissionId == missionId, ct);

        if (childMission is null || childMission.Status != MissionStatus.Completed)
            return ServiceResult<ClaimMissionResultDto>.Fail(Localized.T("Missiya hələ bitməyib.", "This mission is not finished yet."));

        childMission.Status = MissionStatus.Claimed;
        childMission.ClaimedAt = _clock.GetUtcNow().UtcDateTime;
        childMission.UpdatedAt = childMission.ClaimedAt.Value;

        await _rewards.GrantStarsAsync(child, mission.RewardStars, $"Mission: {mission.Title}", ct);
        await _rewards.GrantGemsAsync(child, mission.RewardGems, $"Mission: {mission.Title}", ct);
        await _db.SaveChangesAsync(ct);

        var newBadges = await _rewards.EvaluateBadgesAsync(childId, ct);

        return ServiceResult<ClaimMissionResultDto>.Ok(new ClaimMissionResultDto
        {
            Mission = ToDto(mission, childMission, mission.WorldZone.Code, child.LanguageCode),
            Wallet = await _rewards.GetWalletAsync(childId, ct),
            Message = Localized.Normalize(child.LanguageCode) == Localized.Azerbaijani
                ? $"Missiya tamamlandı! {Localized.Pick(child.LanguageCode, mission.WorldZone.Name, mission.WorldZone.NameAz)} artıq daha gözəl görünür."
                : $"Mission complete! {mission.WorldZone.Name} is looking better already.",
            NewBadges = newBadges
        });
    }

    public async Task<List<MissionDto>> GetFeaturedAsync(Guid childId, int count = 3, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == childId, ct);
        if (child is null)
            return [];

        var missions = await _db.Missions
            .Include(m => m.WorldZone)
            .Where(m => m.IsActive && m.WorldZone.RequiredStars <= child.Stars)
            .OrderBy(m => m.WorldZone.SortOrder)
            .ThenBy(m => m.SortOrder)
            .ToListAsync(ct);

        var childMissions = await EnsureChildMissionsAsync(childId, missions, ct);
        await _db.SaveChangesAsync(ct);

        return missions
            .Select(m => ToDto(m, childMissions[m.Id], m.WorldZone.Code, child.LanguageCode))
            .Where(m => m.Status != MissionStatus.Claimed)
            // Tamamlanmış (mükafatı gözləyən) missiyalar əvvəldə görünsün.
            .OrderByDescending(m => m.Status == MissionStatus.Completed)
            .ThenByDescending(m => m.Target == 0 ? 0 : m.Progress * 100 / m.Target)
            .Take(count)
            .ToList();
    }

    private async Task<List<(Mission Mission, ChildMission ChildMission)>> LoadTrackableAsync(
        Guid childId, MissionType type, CancellationToken ct)
    {
        var missions = await _db.Missions
            .Where(m => m.IsActive && m.Type == type)
            .ToListAsync(ct);

        var childMissions = await EnsureChildMissionsAsync(childId, missions, ct);

        return missions
            .Select(m => (m, childMissions[m.Id]))
            .Where(pair => pair.Item2.Status is MissionStatus.Active)
            .ToList();
    }

    /// <summary>
    /// Uşaq üçün missiya sətirləri ilk toxunuşda yaradılır — qeydiyyat zamanı
    /// bütün kataloqu kopyalamağa ehtiyac qalmır.
    /// </summary>
    private async Task<Dictionary<Guid, ChildMission>> EnsureChildMissionsAsync(
        Guid childId, IEnumerable<Mission> missions, CancellationToken ct)
    {
        var missionIds = missions.Select(m => m.Id).ToList();

        var existing = await _db.ChildMissions
            .Where(cm => cm.ChildProfileId == childId && missionIds.Contains(cm.MissionId))
            .ToListAsync(ct);

        var map = existing.ToDictionary(cm => cm.MissionId);

        foreach (var id in missionIds.Where(id => !map.ContainsKey(id)))
        {
            var created = new ChildMission
            {
                ChildProfileId = childId,
                MissionId = id,
                Status = MissionStatus.Active,
                UpdatedAt = _clock.GetUtcNow().UtcDateTime
            };

            _db.ChildMissions.Add(created);
            map[id] = created;
        }

        return map;
    }

    private void Advance(ChildMission childMission, Mission mission, int newProgress)
    {
        childMission.Progress = Math.Min(newProgress, mission.Target);
        childMission.UpdatedAt = _clock.GetUtcNow().UtcDateTime;

        if (childMission.Progress >= mission.Target && childMission.Status == MissionStatus.Active)
        {
            childMission.Status = MissionStatus.Completed;
            childMission.CompletedAt = childMission.UpdatedAt;
        }
    }

    private static MissionDto ToDto(Mission mission, ChildMission childMission, string zoneCode, string language) => new()
    {
        Id = mission.Id,
        Code = mission.Code,
        Title = Localized.Pick(language, mission.Title, mission.TitleAz),
        Description = Localized.Pick(language, mission.Description, mission.DescriptionAz),
        Type = mission.Type,
        Status = childMission.Status,
        Progress = childMission.Progress,
        Target = mission.Target,
        RewardStars = mission.RewardStars,
        RewardGems = mission.RewardGems,
        ZoneCode = zoneCode,
        Skill = mission.Skill
    };
}
