using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Ai;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Discoveries;
using PetPal.Api.Entities;
using PetPal.Api.Pets;
using PetPal.Api.Progress;
using PetPal.Api.Social;
using PetPal.Shared.Dtos.Auth;
using PetPal.Shared.Dtos.Discovery;
using PetPal.Shared.Dtos.Parent;
using PetPal.Shared.Dtos.Progress;
using PetPal.Shared.Enums;

namespace PetPal.Api.Parent;

/// <summary>
/// Parent View: proqres, bacarıq analitikası və ekran vaxtı balansı.
/// Bütün metodlar əvvəlcə uşağın həmin valideynə aid olduğunu yoxlayır.
/// </summary>
public class ParentService : IParentService
{
    private const int TrendWindowDays = 7;

    private readonly AppDbContext _db;
    private readonly IProgressService _progress;
    private readonly IDiscoveryService _discoveries;
    private readonly PetChatOptions _chat;
    private readonly TimeProvider _clock;

    // Parent → Social asılılığı QALMADI: dostluq sorğularına cavabı uşağın
    // özü verir, ona görə valideyn qatının Social slice-ından xəbəri olmamalıdır.
    public ParentService(
        AppDbContext db,
        IProgressService progress,
        IDiscoveryService discoveries,
        IOptions<PetChatOptions> chat,
        TimeProvider clock)
    {
        _db = db;
        _progress = progress;
        _discoveries = discoveries;
        _chat = chat.Value;
        _clock = clock;
    }

    public async Task<ServiceResult<ParentDashboardDto>> GetDashboardAsync(
        Guid parentId, Guid childId, CancellationToken ct = default)
    {
        var child = await LoadOwnedChildAsync(parentId, childId, ct);
        if (child is null)
            return ServiceResult<ParentDashboardDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var windowStart = today.AddDays(-(TrendWindowDays - 1));

        var goals = await _db.DailyGoals
            .AsNoTracking()
            .Where(g => g.ChildProfileId == childId && g.Date >= windowStart && g.Date <= today)
            .ToListAsync(ct);

        var byDate = goals.ToDictionary(g => g.Date);
        var trend = Enumerable.Range(0, TrendWindowDays)
            .Select(offset =>
            {
                var date = windowStart.AddDays(offset);
                byDate.TryGetValue(date, out var goal);

                return new DailyActivityPointDto
                {
                    Date = date,
                    TasksDone = goal?.Completed ?? 0,
                    Minutes = goal?.MinutesSpent ?? 0,
                    AccuracyPercent = goal is null || goal.AnsweredCount == 0
                        ? 0
                        : goal.CorrectCount * 100 / goal.AnsweredCount
                };
            })
            .ToList();

        var todayPoint = trend.Last();
        var skills = await _progress.GetSkillsAsync(childId, ct);

        // Nailiyyət kartı üçün pet və nişan sayı. Ayrıca sorğulardır, çünki
        // profil sətri onları daşımır — ikisi də kiçikdir.
        var pet = await _db.Pets.AsNoTracking().FirstOrDefaultAsync(p => p.ChildProfileId == childId, ct);
        var badgeCount = await _db.Set<ChildBadge>().CountAsync(b => b.ChildProfileId == childId, ct);

        return ServiceResult<ParentDashboardDto>.Ok(new ParentDashboardDto
        {
            ChildId = child.Id,
            ChildDisplayName = child.DisplayName,
            AvatarKey = child.AvatarKey,
            LanguageCode = Localized.Normalize(child.LanguageCode),
            TasksDoneToday = todayPoint.TasksDone,
            AccuracyPercentToday = todayPoint.AccuracyPercent,
            MinutesToday = todayPoint.Minutes,
            StreakDays = child.StreakDays,
            FriendCode = child.FriendCode,
            PetName = pet?.Name ?? string.Empty,
            PetLevel = pet?.Level ?? 1,
            Stars = child.Stars,
            BadgeCount = badgeCount,
            Skills = skills,
            Last7Days = trend,
            ChatEnabled = child.ChatEnabled,
            ArenaEnabled = child.ArenaEnabled,
            ArenaFriendsOnly = child.ArenaFriendsOnly,
            ScreenTime = new ScreenTimeSettingsDto
            {
                DailyGoalTarget = child.DailyGoalTarget,
                DailyMinutesLimit = child.DailyMinutesLimit,
                BedtimeStartHour = child.BedtimeStartHour,
                BedtimeEndHour = child.BedtimeEndHour
            },
            Insights = BuildInsights(child, trend, skills)
        });
    }

    public async Task<ServiceResult<ScreenTimeSettingsDto>> UpdateScreenTimeAsync(
        Guid parentId, Guid childId, ScreenTimeSettingsDto settings, CancellationToken ct = default)
    {
        var child = await LoadOwnedChildAsync(parentId, childId, ct);
        if (child is null)
            return ServiceResult<ScreenTimeSettingsDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        child.DailyGoalTarget = settings.DailyGoalTarget;
        child.DailyMinutesLimit = settings.DailyMinutesLimit;
        child.BedtimeStartHour = settings.BedtimeStartHour;
        child.BedtimeEndHour = settings.BedtimeEndHour;

        // Bugünkü hədəf sətri artıq varsa, yeni hədəf dərhal tətbiq olunsun.
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var goal = await _db.DailyGoals.FirstOrDefaultAsync(g => g.ChildProfileId == childId && g.Date == today, ct);
        if (goal is not null)
            goal.Target = settings.DailyGoalTarget;

        await _db.SaveChangesAsync(ct);
        return ServiceResult<ScreenTimeSettingsDto>.Ok(settings);
    }

    /// <summary>
    /// Dil dəyişəndə heç nə itmir: yaş, statlar, ulduzlar və açılmış əşyalar
    /// yerində qalır — yalnız mətnin dili dəyişir. Sual bankı hər iki dildə
    /// tam olduğuna görə uşaq elə həmin səviyyədən davam edir.
    /// </summary>
    public async Task<ServiceResult<ChildSummaryDto>> UpdateLanguageAsync(
        Guid parentId, Guid childId, ChildLanguageRequest request, CancellationToken ct = default)
    {
        var child = await LoadOwnedChildAsync(parentId, childId, ct);
        if (child is null)
            return ServiceResult<ChildSummaryDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        child.LanguageCode = Localized.Normalize(request.LanguageCode);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<ChildSummaryDto>.Ok(ToSummary(child));
    }

    public async Task<ServiceResult<ParentChatLogDto>> GetChatLogAsync(
        Guid parentId, Guid childId, int? take = null, CancellationToken ct = default)
    {
        var child = await LoadOwnedChildAsync(parentId, childId, ct);
        if (child is null)
            return ServiceResult<ParentChatLogDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var limit = Math.Clamp(take ?? _chat.ParentHistoryLimit, 1, _chat.ParentHistoryLimit);
        var dayStart = _clock.GetUtcNow().UtcDateTime.Date;

        var turns = await _db.ChatTurns
            .AsNoTracking()
            .Where(t => t.ChildProfileId == childId)
            .OrderByDescending(t => t.Sequence)
            .Take(limit)
            .Select(t => new ParentChatTurnDto
            {
                Id = t.Id,
                FromChild = t.FromChild,
                Text = t.Text,
                FromAi = t.FromAi,
                BlockedReason = t.BlockedReason,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(ct);

        var today = await _db.ChatTurns
            .Where(t => t.ChildProfileId == childId && t.FromChild && t.CreatedAt >= dayStart)
            .CountAsync(ct);

        return ServiceResult<ParentChatLogDto>.Ok(new ParentChatLogDto
        {
            Enabled = child.ChatEnabled,
            MessagesToday = today,
            MessagesPerDay = _chat.MessagesPerDay,
            Turns = turns
        });
    }

    public async Task<ServiceResult<ChatSettingsRequest>> UpdateChatSettingsAsync(
        Guid parentId, Guid childId, ChatSettingsRequest request, CancellationToken ct = default)
    {
        var child = await LoadOwnedChildAsync(parentId, childId, ct);
        if (child is null)
            return ServiceResult<ChatSettingsRequest>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        // Söhbət bağlananda tarixçə SİLİNMİR: valideyn sonradan da baxa bilməlidir,
        // uşaq isə otağa girə bilmir — bax PetChatService.SendAsync.
        child.ChatEnabled = request.Enabled;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<ChatSettingsRequest>.Ok(request);
    }

    public async Task<ServiceResult<ArenaSettingsRequest>> UpdateArenaSettingsAsync(
        Guid parentId, Guid childId, ArenaSettingsRequest request, CancellationToken ct = default)
    {
        var child = await LoadOwnedChildAsync(parentId, childId, ct);
        if (child is null)
            return ServiceResult<ArenaSettingsRequest>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        // Açar dəyişəndə BAŞLANMIŞ duellər ləğv olunmur: uşaq artıq oynayıb və
        // rəqib onun cavablarını gözləyir. Yeni şərt növbəti uyğunlaşdırmadan
        // qüvvəyə minir — açıq duelin şərti isə duelin üstündə dondurulub.
        child.ArenaEnabled = request.Enabled;
        child.ArenaFriendsOnly = request.FriendsOnly;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<ArenaSettingsRequest>.Ok(request);
    }

    public async Task<ServiceResult<List<ChildSummaryDto>>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
    {
        var children = await _db.ChildProfiles
            .AsNoTracking()
            .Include(c => c.Pet)
            .Where(c => c.ParentUserId == parentId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        return ServiceResult<List<ChildSummaryDto>>.Ok(children.Select(ToSummary).ToList());
    }

    /// <summary>
    /// Kolleksiyanı uşaq da, valideyn də görür — məzmun eynidir, yollar
    /// fərqlidir. Ona görə burada siyahı yenidən qurulmur: sahiblik yoxlanılır
    /// və eyni servis çağırılır. Əks halda iki sorğu vaxtla bir-birindən
    /// ayrılar və valideyn uşağın gördüyündən fərqli siyahıya baxardı.
    /// </summary>
    public async Task<ServiceResult<List<DiscoveryDto>>> GetDiscoveriesAsync(
        Guid parentId, Guid childId, int take = 20, CancellationToken ct = default)
    {
        if (!await OwnsChildAsync(parentId, childId, ct))
            return ServiceResult<List<DiscoveryDto>>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        return ServiceResult<List<DiscoveryDto>>.Ok(await _discoveries.GetRecentAsync(childId, take, ct));
    }

    public async Task<ServiceResult<DiscoveryPhotoFile>> GetDiscoveryPhotoAsync(
        Guid parentId, Guid childId, Guid discoveryId, CancellationToken ct = default)
    {
        if (!await OwnsChildAsync(parentId, childId, ct))
            return ServiceResult<DiscoveryPhotoFile>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        return await _discoveries.GetPhotoAsync(childId, discoveryId, ct);
    }

    /// <summary>
    /// Profil siyahısının sətri. Dil də daxildir: valideyn hansı uşağın hansı
    /// dildə oynadığını görməli və dəyişə bilməlidir.
    /// </summary>
    private static ChildSummaryDto ToSummary(ChildProfile child) => new()
    {
        Id = child.Id,
        DisplayName = child.DisplayName,
        AvatarKey = child.AvatarKey,
        Age = child.Age,
        Stars = child.Stars,
        Gems = child.Gems,
        Level = child.Pet?.Level ?? 1,
        PetName = child.Pet?.Name ?? string.Empty,
        PetStage = child.Pet is null ? PetStage.Egg : PetProgression.StageFor(child.Pet),
        LanguageCode = Localized.Normalize(child.LanguageCode)
    };

    /// <summary>Valideynə göstərilən qısa, konkret müşahidələr — quru rəqəmdən daha faydalıdır.</summary>
    private static List<string> BuildInsights(
        ChildProfile child, List<DailyActivityPointDto> trend, List<SkillProgressDto> skills)
    {
        var insights = new List<string>();

        var activeDays = trend.Count(t => t.TasksDone > 0);
        insights.Add($"{child.DisplayName} son 7 gündə {activeDays} gün məşğul olub, cəmi {trend.Sum(t => t.TasksDone)} tapşırıq həll edib.");

        var strongest = skills.MaxBy(s => s.Rating);
        var weakest = skills.MinBy(s => s.Rating);
        if (strongest is not null && weakest is not null && strongest.Skill != weakest.Skill)
            insights.Add($"Ən güclü sahə: {strongest.Skill} ({strongest.MasteryPercent}%). Daha çox məşq lazım olan sahə: {weakest.Skill} ({weakest.MasteryPercent}%).");

        var minutes = trend.Sum(t => t.Minutes);
        var averageMinutes = activeDays == 0 ? 0 : minutes / activeDays;
        insights.Add(averageMinutes > child.DailyMinutesLimit
            ? $"Gündəlik orta ekran vaxtı {averageMinutes} dəq — təyin etdiyiniz {child.DailyMinutesLimit} dəq limitindən yuxarıdır."
            : $"Gündəlik orta ekran vaxtı {averageMinutes} dəq — {child.DailyMinutesLimit} dəq limiti daxilindədir.");

        if (child.StreakDays >= 3)
            insights.Add($"{child.StreakDays} günlük ardıcıllıq davam edir — bu vərdişi qorumaq motivasiyanı yüksək saxlayır.");

        return insights;
    }

    private Task<ChildProfile?> LoadOwnedChildAsync(Guid parentId, Guid childId, CancellationToken ct) =>
        _db.ChildProfiles.FirstOrDefaultAsync(c => c.Id == childId && c.ParentUserId == parentId, ct);

    /// <summary>Profilin özü lazım olmayan yerlərdə sətri gətirmək mənasızdır.</summary>
    private Task<bool> OwnsChildAsync(Guid parentId, Guid childId, CancellationToken ct) =>
        _db.ChildProfiles.AnyAsync(c => c.Id == childId && c.ParentUserId == parentId, ct);
}
