using Microsoft.EntityFrameworkCore;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Learning.Arena;
using PetPal.Shared.Dtos.Rewards;
using PetPal.Shared.Enums;

namespace PetPal.Api.Rewards;

public class RewardService : IRewardService
{
    private readonly AppDbContext _db;
    private readonly IArenaStandings _standings;
    private readonly TimeProvider _clock;

    public RewardService(AppDbContext db, IArenaStandings standings, TimeProvider clock)
    {
        _db = db;
        _standings = standings;
        _clock = clock;
    }

    public Task GrantStarsAsync(ChildProfile child, int amount, string reason, CancellationToken ct = default)
    {
        if (amount <= 0)
            return Task.CompletedTask;

        child.Stars += amount;
        AddEntry(child.Id, RewardKind.Star, amount, reason);
        return Task.CompletedTask;
    }

    public Task GrantGemsAsync(ChildProfile child, int amount, string reason, CancellationToken ct = default)
    {
        if (amount <= 0)
            return Task.CompletedTask;

        child.Gems += amount;
        AddEntry(child.Id, RewardKind.Gem, amount, reason);
        return Task.CompletedTask;
    }

    public Task<bool> SpendStarsAsync(ChildProfile child, int amount, string reason, CancellationToken ct = default)
    {
        if (amount <= 0)
            return Task.FromResult(true);

        if (child.Stars < amount)
            return Task.FromResult(false);

        child.Stars -= amount;
        AddEntry(child.Id, RewardKind.Star, -amount, reason);
        return Task.FromResult(true);
    }

    public async Task<WalletDto> GetWalletAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        return new WalletDto { Stars = child?.Stars ?? 0, Gems = child?.Gems ?? 0 };
    }

    public async Task<List<RewardEntryDto>> GetLedgerAsync(Guid childId, int take = 50, CancellationToken ct = default)
    {
        return await _db.RewardEntries
            .AsNoTracking()
            .Where(r => r.ChildProfileId == childId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(r => new RewardEntryDto
            {
                Id = r.Id,
                Kind = r.Kind,
                Amount = r.Amount,
                Reason = r.Reason,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<List<BadgeDto>> GetBadgesAsync(Guid childId, CancellationToken ct = default)
    {
        var language = await GetLanguageAsync(childId, ct);
        var all = await _db.Badges.AsNoTracking().OrderBy(b => b.Tier).ThenBy(b => b.Title).ToListAsync(ct);
        var earned = await _db.ChildBadges
            .AsNoTracking()
            .Where(cb => cb.ChildProfileId == childId)
            .ToDictionaryAsync(cb => cb.BadgeId, cb => cb.EarnedAt, ct);

        return all.Select(b => ToDto(b, earned.TryGetValue(b.Id, out var at) ? at : null, language)).ToList();
    }

    private async Task<string> GetLanguageAsync(Guid childId, CancellationToken ct) =>
        await _db.ChildProfiles
            .AsNoTracking()
            .Where(c => c.Id == childId)
            .Select(c => c.LanguageCode)
            .FirstOrDefaultAsync(ct) ?? Localized.Azerbaijani;

    public async Task<List<BadgeDto>> EvaluateBadgesAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .Include(c => c.SkillMasteries)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child is null)
            return [];

        var alreadyEarned = await _db.ChildBadges
            .Where(cb => cb.ChildProfileId == childId)
            .Select(cb => cb.Badge.Code)
            .ToListAsync(ct);

        var stats = await LoadStatsAsync(child, ct);
        var deserved = BadgeRules.Evaluate(stats).Except(alreadyEarned).ToList();
        if (deserved.Count == 0)
            return [];

        var badges = await _db.Badges.Where(b => deserved.Contains(b.Code)).ToListAsync(ct);
        var now = _clock.GetUtcNow().UtcDateTime;

        foreach (var badge in badges)
            _db.ChildBadges.Add(new ChildBadge { ChildProfileId = childId, BadgeId = badge.Id, EarnedAt = now });

        await _db.SaveChangesAsync(ct);

        return badges.Select(b => ToDto(b, now, child.LanguageCode)).ToList();
    }

    private async Task<BadgeStats> LoadStatsAsync(ChildProfile child, CancellationToken ct)
    {
        var masteries = child.SkillMasteries.ToDictionary(m => m.Skill, m => m.CorrectCount);

        var perfectRounds = await _db.LearningSessions
            .CountAsync(s => s.ChildProfileId == child.Id
                             && s.CompletedAt != null
                             && s.TotalCount >= 3
                             && s.CorrectCount == s.TotalCount, ct);

        var answered = await _db.SessionAnswers
            .CountAsync(a => a.LearningSession.ChildProfileId == child.Id, ct);

        var discoveries = await _db.Discoveries.CountAsync(d => d.ChildProfileId == child.Id, ct);

        // Yalnız TAMAMLANMIŞ duellər sayılır — rəqib gözləyən duel hələ nəticə deyil.
        var arenaDuels = await _db.DuelEntries
            .CountAsync(e => e.ChildProfileId == child.Id
                             && e.Duel.Status == DuelStatus.Complete
                             && !e.Duel.IsPractice, ct);

        // Həftəlik liqa mövqeyi ayrıca servisdən oxunur — arena ilə mükafat
        // servisi bir-birindən bu dar interfeys vasitəsilə xəbər tutur.
        var league = await _standings.GetPositionAsync(child.Id, ct);

        var arenaWins = await _db.DuelEntries
            .CountAsync(e => e.ChildProfileId == child.Id
                             && e.Outcome == DuelOutcome.Win
                             && !e.Duel.IsPractice, ct);

        // Bir zona tam bərpa olunubsa (bütün missiyaları alınıbsa) "world-healer" verilir.
        // Zona/missiya sayı kiçik kataloqdur — yaddaşda hesablamaq sorğunu sadə saxlayır.
        var missionsByZone = await _db.Missions
            .AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => new { m.Id, m.WorldZoneId })
            .ToListAsync(ct);

        var claimedMissionIds = await _db.ChildMissions
            .AsNoTracking()
            .Where(cm => cm.ChildProfileId == child.Id && cm.Status == MissionStatus.Claimed)
            .Select(cm => cm.MissionId)
            .ToListAsync(ct);

        var claimed = claimedMissionIds.ToHashSet();
        var zonesRestored = missionsByZone
            .GroupBy(m => m.WorldZoneId)
            .Count(group => group.All(m => claimed.Contains(m.Id)));

        return new BadgeStats
        {
            AnsweredCount = answered,
            PerfectRounds = perfectRounds,
            MathCorrect = masteries.GetValueOrDefault(SkillArea.Math),
            VocabularyCorrect = masteries.GetValueOrDefault(SkillArea.Vocabulary),
            LogicCorrect = masteries.GetValueOrDefault(SkillArea.Logic),
            StreakDays = child.StreakDays,
            DiscoveryCount = discoveries,
            CareActionCount = child.CareActionCount,
            ZonesRestored = zonesRestored,
            ArenaDuels = arenaDuels,
            ArenaWins = arenaWins,
            ArenaLeagueRank = league.Rank,
            ArenaWeeklyDuels = league.Duels
        };
    }

    private void AddEntry(Guid childId, RewardKind kind, int amount, string reason) =>
        _db.RewardEntries.Add(new RewardEntry
        {
            ChildProfileId = childId,
            Kind = kind,
            Amount = amount,
            Reason = reason,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        });

    private static BadgeDto ToDto(Badge badge, DateTime? earnedAt, string language) => new()
    {
        Code = badge.Code,
        Title = Localized.Pick(language, badge.Title, badge.TitleAz),
        Description = Localized.Pick(language, badge.Description, badge.DescriptionAz),
        Tier = badge.Tier,
        IconKey = badge.IconKey,
        EarnedAt = earnedAt
    };
}
