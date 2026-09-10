using Microsoft.EntityFrameworkCore;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Missions;
using PetPal.Api.Notifications;
using PetPal.Api.Realtime;
using PetPal.Api.Rewards;
using PetPal.Shared.Dtos.Social;
using PetPal.Shared.Enums;

namespace PetPal.Api.Social;

/// <summary>
/// Uşaqlar üçün qapalı sosial dövrə: sərbəst axtarış və mətn yazışması yoxdur,
/// yalnız dost kodu ilə əlaqə və birgə missiyalar.
///
/// <para><b>İki razılıq qaydası bu servisin əsasıdır:</b></para>
/// <list type="number">
///   <item>Dost kodu yazmaq dostluq QURMUR — kodun sahibi özü qəbul edir.</item>
///   <item>Komanda missiyası dostun başına GƏLMİR — o, dəvəti özü qəbul edir.</item>
/// </list>
/// <para>Hər ikisi eyni səbəbdəndir: uşağın sosial əlaqəsi başqasının tək
/// klikləməsi ilə yaranmamalıdır.</para>
///
/// <para>Razılığı əvvəl VALİDEYN verirdi. Artıq uşağın özü verir: qapalı
/// dövrədə (kodsuz tapmaq olmur, sərbəst mətn yoxdur, hədd 20 dostdur) qərar
/// uşağın öz səlahiyyətindədir. Qaydanın özü qalır — dəyişən yalnız kimin
/// təsdiqləməsidir.</para>
/// </summary>
public class SocialService : ISocialService, ITeamMissionTracker
{
    private const int MaxFriends = 20;
    private const int TeamMissionTarget = 20;
    private const int TeamMissionRewardStars = 50;

    /// <summary>
    /// Missiya başlıqları İKİ DİLDƏ saxlanılır: missiyanı bir uşaq yaradır,
    /// amma onu fərqli dildəki dostlar da görür — mətn yaradanın dilində
    /// dondurula bilməz. Sıra sabitdir, seçim təsadüfidir.
    /// </summary>
    private static readonly (string En, string Az)[] TeamMissionTitles =
    [
        ("Rescue the Whisper Forest", "Pıçıltı meşəsini xilas et"),
        ("Light Up Crystal Lake", "Büllur gölü işıqlandır"),
        ("Rebuild the Star Bridge", "Ulduz körpüsünü yenidən qur"),
        ("Chase Away the Storm", "Fırtınanı qov")
    ];

    private readonly AppDbContext _db;
    private readonly IRewardService _rewards;
    private readonly IMissionProgressTracker _missions;
    private readonly IPresenceTracker _presence;
    private readonly ILiveNotifier _live;
    private readonly INotificationService _notifications;
    private readonly TimeProvider _clock;

    public SocialService(
        AppDbContext db,
        IRewardService rewards,
        IMissionProgressTracker missions,
        IPresenceTracker presence,
        ILiveNotifier live,
        INotificationService notifications,
        TimeProvider clock)
    {
        _db = db;
        _rewards = rewards;
        _missions = missions;
        _presence = presence;
        _live = live;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<ServiceResult<string>> GetFriendCodeAsync(Guid childId, CancellationToken ct = default)
    {
        var code = await _db.ChildProfiles
            .Where(c => c.Id == childId)
            .Select(c => c.FriendCode)
            .FirstOrDefaultAsync(ct);

        return code is null
            ? ServiceResult<string>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."))
            : ServiceResult<string>.Ok(code);
    }

    public async Task<FriendsViewDto> GetFriendsViewAsync(Guid childId, CancellationToken ct = default)
    {
        var rows = await _db.Friendships
            .AsNoTracking()
            .Where(f => f.ChildProfileId == childId)
            .OrderBy(f => f.CreatedAt)
            .Select(f => new
            {
                f.Status,
                f.RequestedByChildProfileId,
                f.CreatedAt,
                FriendId = f.FriendChildProfile.Id,
                f.FriendChildProfile.DisplayName,
                f.FriendChildProfile.AvatarKey,
                PetName = f.FriendChildProfile.Pet != null ? f.FriendChildProfile.Pet.Name : string.Empty,
                Level = f.FriendChildProfile.Pet != null ? f.FriendChildProfile.Pet.Level : 1
            })
            .ToListAsync(ct);

        var active = rows.Where(r => r.Status == FriendshipStatus.Active).ToList();

        // Presence yaddaşdadır — bütün dostlar üçün tək keçidə işarələnir.
        var online = _presence.OnlineAmong(active.Select(r => r.FriendId));

        return new FriendsViewDto
        {
            Friends = active
                .Select(r => new FriendDto
                {
                    ChildId = r.FriendId,
                    DisplayName = r.DisplayName,
                    AvatarKey = r.AvatarKey,
                    PetName = r.PetName,
                    Level = r.Level,
                    IsOnline = online.Contains(r.FriendId)
                })
                .ToList(),

            Pending = rows
                .Where(r => r.Status == FriendshipStatus.Pending)
                .Select(r => new PendingFriendDto
                {
                    ChildId = r.FriendId,
                    DisplayName = r.DisplayName,
                    AvatarKey = r.AvatarKey,
                    IsOutgoing = r.RequestedByChildProfileId == childId,
                    RequestedAt = r.CreatedAt
                })
                .ToList()
        };
    }

    public async Task<ServiceResult<PendingFriendDto>> AddFriendAsync(
        Guid childId, AddFriendRequest request, CancellationToken ct = default)
    {
        var code = request.FriendCode.Trim().ToUpperInvariant();

        var friend = await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.FriendCode == code, ct);

        if (friend is null)
            return ServiceResult<PendingFriendDto>.NotFound(Localized.T("Bu kodla profil tapılmadı.", "No profile was found with that code."));

        if (friend.Id == childId)
            return ServiceResult<PendingFriendDto>.Fail(Localized.T("Özünüzü dost kimi əlavə edə bilməzsiniz.", "You cannot add yourself as a friend."));

        var existing = await _db.Friendships
            .FirstOrDefaultAsync(f => f.ChildProfileId == childId && f.FriendChildProfileId == friend.Id, ct);

        if (existing is not null)
        {
            return existing.Status == FriendshipStatus.Active
                ? ServiceResult<PendingFriendDto>.Conflict(Localized.T("Bu profil artıq dostlar siyahısındadır.", "This profile is already in your friends list."))
                : ServiceResult<PendingFriendDto>.Conflict(Localized.T("Bu sorğu artıq göndərilib — cavab gözlənilir.", "That request is already sent — it's waiting for an answer."));
        }

        // Hədd yalnız TƏSDİQLƏNMİŞ dostlara aiddir: gözləyən sorğular hələ dostluq
        // deyil və uşağın siyahısını doldurmamalıdır.
        var friendCount = await _db.Friendships
            .CountAsync(f => f.ChildProfileId == childId && f.Status == FriendshipStatus.Active, ct);

        if (friendCount >= MaxFriends)
            return ServiceResult<PendingFriendDto>.Conflict(
                Localized.T($"Ən çoxu {MaxFriends} dost əlavə edilə bilər.", $"You can have at most {MaxFriends} friends."));

        var now = _clock.GetUtcNow().UtcDateTime;

        // Dostluq həmişə qarşılıqlıdır — hər iki istiqamət eyni vəziyyətdə yazılır.
        _db.Friendships.Add(new Friendship
        {
            ChildProfileId = childId,
            FriendChildProfileId = friend.Id,
            Status = FriendshipStatus.Pending,
            RequestedByChildProfileId = childId,
            CreatedAt = now
        });
        _db.Friendships.Add(new Friendship
        {
            ChildProfileId = friend.Id,
            FriendChildProfileId = childId,
            Status = FriendshipStatus.Pending,
            RequestedByChildProfileId = childId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        // Sorğunu görməli olan UŞAĞIN ÖZÜDÜR — qərar onundur. Bildiriş olmasa
        // sorğu günlərlə gözləyə bilər, çünki uşaq Dostlar ekranını hər gün açmır.
        var requesterName = await _db.ChildProfiles
            .Where(c => c.Id == childId)
            .Select(c => c.DisplayName)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        await _notifications.NotifyFriendRequestAsync(friend.Id, requesterName, ct);

        return ServiceResult<PendingFriendDto>.Ok(new PendingFriendDto
        {
            ChildId = friend.Id,
            DisplayName = friend.DisplayName,
            AvatarKey = friend.AvatarKey,
            IsOutgoing = true,
            RequestedAt = now
        });
    }

    public async Task<ServiceResult<bool>> RemoveFriendAsync(
        Guid childId, Guid friendChildId, CancellationToken ct = default)
    {
        var rows = await _db.Friendships
            .Where(f => (f.ChildProfileId == childId && f.FriendChildProfileId == friendChildId)
                        || (f.ChildProfileId == friendChildId && f.FriendChildProfileId == childId))
            .ToListAsync(ct);

        if (rows.Count == 0)
            return ServiceResult<bool>.NotFound(Localized.T("Bu profil dostlar siyahısında deyil.", "That profile isn't in your friends list."));

        _db.Friendships.RemoveRange(rows);
        await _db.SaveChangesAsync(ct);

        await _live.FriendsChangedAsync([childId, friendChildId], ct);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> RespondToFriendRequestAsync(
        Guid childId, Guid requesterChildId, bool approve, CancellationToken ct = default)
    {
        var rows = await _db.Friendships
            .Where(f => f.Status == FriendshipStatus.Pending
                        && f.RequestedByChildProfileId == requesterChildId
                        && ((f.ChildProfileId == childId && f.FriendChildProfileId == requesterChildId)
                            || (f.ChildProfileId == requesterChildId && f.FriendChildProfileId == childId)))
            .ToListAsync(ct);

        if (rows.Count == 0)
            return ServiceResult<bool>.NotFound(Localized.T("Gözləyən sorğu tapılmadı.", "No pending request was found."));

        var now = _clock.GetUtcNow().UtcDateTime;

        if (approve)
        {
            // Hədd HƏR İKİ tərəf üçün təsdiq anında yoxlanılır.
            //
            // Yalnız təsdiq edəni yoxlamaq kifayət deyildi: sorğu göndərən uşağın
            // Active sayı göndərmə anında həmişə 0 olur (bütün sorğular gözləyir),
            // yəni o, istənilən qədər sorğu göndərib sonra hamısını təsdiqlədə
            // bilirdi — hədd tamamilə keçilirdi.
            var counts = await _db.Friendships
                .Where(f => (f.ChildProfileId == childId || f.ChildProfileId == requesterChildId)
                            && f.Status == FriendshipStatus.Active)
                .GroupBy(f => f.ChildProfileId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(ct);

            if (counts.Any(c => c.Count >= MaxFriends))
                return ServiceResult<bool>.Conflict(
                    Localized.T($"Dost siyahısı doludur ({MaxFriends}).", $"The friends list is full ({MaxFriends})."));

            foreach (var row in rows)
            {
                row.Status = FriendshipStatus.Active;
                row.RespondedAt = now;
            }
        }
        else
        {
            // İmtina sətirləri SAXLAMIR: qalsaydı, uşaq eyni kodu bir daha yaza
            // bilməzdi və səbəbini heç kim izah edə bilməzdi.
            _db.Friendships.RemoveRange(rows);
        }

        await _db.SaveChangesAsync(ct);
        await _live.FriendsChangedAsync([childId, requesterChildId], ct);

        return ServiceResult<bool>.Ok(approve);
    }

    public async Task<List<TeamMissionDto>> GetTeamMissionsAsync(Guid childId, CancellationToken ct = default)
    {
        var language = await LanguageOfAsync(childId, ct);

        var missions = await _db.TeamMissions
            .AsNoTracking()
            .Include(m => m.Members).ThenInclude(m => m.ChildProfile)
            .Where(m => m.Members.Any(member => member.ChildProfileId == childId
                                                && member.Status != TeamMemberStatus.Declined))
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        return missions.Select(m => ToDto(m, childId, language)).ToList();
    }

    public async Task<ServiceResult<TeamMissionDto>> StartTeamMissionAsync(
        Guid childId, List<Guid> friendIds, CancellationToken ct = default)
    {
        if (friendIds.Count == 0)
            return ServiceResult<TeamMissionDto>.Fail(Localized.T("Ən azı bir dost seçilməlidir.", "Pick at least one friend."));

        // Yalnız TƏSDİQLƏNMİŞ dostlar: gözləyən sorğu ilə komanda qurula bilməz.
        var confirmedFriendIds = await _db.Friendships
            .Where(f => f.ChildProfileId == childId
                        && f.Status == FriendshipStatus.Active
                        && friendIds.Contains(f.FriendChildProfileId))
            .Select(f => f.FriendChildProfileId)
            .ToListAsync(ct);

        if (confirmedFriendIds.Count != friendIds.Distinct().Count())
            return ServiceResult<TeamMissionDto>.Forbidden(Localized.T("Yalnız dostlar siyahısındakı profillərlə komanda qurula bilər.", "You can only team up with profiles from your friends list."));

        var language = await LanguageOfAsync(childId, ct);
        var now = _clock.GetUtcNow().UtcDateTime;
        var title = TeamMissionTitles[Random.Shared.Next(TeamMissionTitles.Length)];

        var mission = new TeamMission
        {
            Title = title.En,
            TitleAz = title.Az,
            Description = $"Solve {TeamMissionTarget} questions together with your friends.",
            DescriptionAz = $"Dostlarınla birlikdə {TeamMissionTarget} sual həll et.",
            Target = TeamMissionTarget,
            RewardStars = TeamMissionRewardStars,
            CreatedAt = now
        };

        // Missiyanı başladan onu artıq seçib — ondan bir daha soruşmaq mənasızdır.
        mission.Members.Add(new TeamMissionMember
        {
            ChildProfileId = childId,
            Status = TeamMemberStatus.Joined,
            JoinedAt = now
        });

        foreach (var friendId in confirmedFriendIds)
        {
            mission.Members.Add(new TeamMissionMember
            {
                ChildProfileId = friendId,
                Status = TeamMemberStatus.Invited,
                JoinedAt = now
            });
        }

        _db.TeamMissions.Add(mission);
        await _db.SaveChangesAsync(ct);

        foreach (var friendId in confirmedFriendIds)
            await _live.TeamInvitedAsync(friendId, mission.Id, ct);

        var saved = await _db.TeamMissions
            .Include(m => m.Members).ThenInclude(m => m.ChildProfile)
            .FirstAsync(m => m.Id == mission.Id, ct);

        return ServiceResult<TeamMissionDto>.Ok(ToDto(saved, childId, language));
    }

    public async Task<ServiceResult<TeamMissionDto>> RespondToTeamMissionAsync(
        Guid childId, Guid missionId, bool join, CancellationToken ct = default)
    {
        var mission = await _db.TeamMissions
            .Include(m => m.Members).ThenInclude(m => m.ChildProfile)
            .FirstOrDefaultAsync(m => m.Id == missionId, ct);

        var member = mission?.Members.FirstOrDefault(m => m.ChildProfileId == childId);

        if (mission is null || member is null)
            return ServiceResult<TeamMissionDto>.NotFound(Localized.T("Missiya tapılmadı.", "Mission not found."));

        if (member.Status != TeamMemberStatus.Invited)
            return ServiceResult<TeamMissionDto>.Conflict(Localized.T("Bu dəvətə artıq cavab verilib.", "You already answered this invitation."));

        member.Status = join ? TeamMemberStatus.Joined : TeamMemberStatus.Declined;
        member.JoinedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        var language = await LanguageOfAsync(childId, ct);
        return ServiceResult<TeamMissionDto>.Ok(ToDto(mission, childId, language));
    }

    public async Task TrackCorrectAnswerAsync(Guid childId, CancellationToken ct = default)
    {
        var missions = await _db.TeamMissions
            .Include(m => m.Members)
            .Where(m => m.Status == MissionStatus.Active
                        && m.Members.Any(member => member.ChildProfileId == childId
                                                   && member.Status == TeamMemberStatus.Joined))
            .ToListAsync(ct);

        if (missions.Count == 0)
            return;

        var now = _clock.GetUtcNow().UtcDateTime;

        foreach (var mission in missions)
        {
            var member = mission.Members.First(m => m.ChildProfileId == childId);
            member.Contribution++;
            mission.Progress = Math.Min(mission.Progress + 1, mission.Target);

            // Mükafat və xəbər yalnız QOŞULANLARA aiddir — dəvəti cavabsız
            // qoyan uşaq nə töhfə verir, nə pay alır.
            var joinedIds = mission.Members
                .Where(m => m.Status == TeamMemberStatus.Joined)
                .Select(m => m.ChildProfileId)
                .ToList();

            if (mission.Progress < mission.Target)
            {
                await _live.TeamProgressAsync(joinedIds, mission.Id, mission.Progress, mission.Target, ct);
                continue;
            }

            mission.Status = MissionStatus.Completed;
            mission.CompletedAt = now;

            var children = await _db.ChildProfiles.Where(c => joinedIds.Contains(c.Id)).ToListAsync(ct);

            foreach (var child in children)
            {
                await _rewards.GrantStarsAsync(child, mission.RewardStars, $"Team mission: {mission.Title}", ct);
                await _missions.TrackAsync(child.Id, MissionType.TeamChallenge, null, 1, ct);
            }

            await _live.TeamCompletedAsync(joinedIds, mission.Id, ct);

            // Komanda missiyası saatlarla davam edir: onu bitirən uşaq app-də
            // olur, digərləri isə çox vaxt olmur — mükafatı onlara push çatdırır.
            // Yuxu rejimində bildiriş saxlanılır (bax NotificationService).
            await _notifications.NotifyTeamMissionCompletedAsync(
                joinedIds, mission.Title, mission.TitleAz, ct);
        }
    }

    private Task<string> LanguageOfAsync(Guid childId, CancellationToken ct) =>
        _db.ChildProfiles
            .Where(c => c.Id == childId)
            .Select(c => c.LanguageCode)
            .FirstOrDefaultAsync(ct)!;

    private static TeamMissionDto ToDto(TeamMission mission, Guid childId, string language)
    {
        var mine = mission.Members.FirstOrDefault(m => m.ChildProfileId == childId);
        var starter = mission.Members.OrderBy(m => m.JoinedAt).FirstOrDefault();

        return new TeamMissionDto
        {
            Id = mission.Id,
            Title = Localized.Pick(language, mission.Title, mission.TitleAz),
            Description = Localized.Pick(language, mission.Description, mission.DescriptionAz),
            Target = mission.Target,
            Progress = mission.Progress,
            RewardStars = mission.RewardStars,
            Status = mission.Status,
            MyStatus = mine?.Status ?? TeamMemberStatus.Invited,
            StartedBy = starter?.ChildProfile?.DisplayName ?? string.Empty,
            Members = mission.Members
                .Where(m => m.Status != TeamMemberStatus.Declined)
                .OrderByDescending(m => m.Contribution)
                .Select(m => new TeamMemberDto
                {
                    ChildId = m.ChildProfileId,
                    DisplayName = m.ChildProfile?.DisplayName ?? string.Empty,
                    AvatarKey = m.ChildProfile?.AvatarKey ?? string.Empty,
                    Contribution = m.Contribution,
                    Status = m.Status
                })
                .ToList()
        };
    }
}
