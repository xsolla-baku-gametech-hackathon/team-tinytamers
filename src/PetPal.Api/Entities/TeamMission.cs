using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Dostlarla birgə tamamlanan missiya — "TEAM MISSION COMPLETE!" axını.
///
/// <para>Başlıq və izah missiya kataloqundakı kimi İKİ DİLDƏ saxlanılır
/// (<see cref="Title"/> ingiliscə baza, <see cref="TitleAz"/> azərbaycanca):
/// missiyanı bir uşaq yaradır, amma onu fərqli dildəki dostlar da görür,
/// ona görə mətn yaradanın dilində DONDURULA bilməz.</para>
/// </summary>
public class TeamMission
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string? TitleAz { get; set; }
    public string? DescriptionAz { get; set; }

    public int Target { get; set; } = 20;
    public int Progress { get; set; }
    public int RewardStars { get; set; } = 50;

    public MissionStatus Status { get; set; } = MissionStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public ICollection<TeamMissionMember> Members { get; set; } = new List<TeamMissionMember>();
}

public class TeamMissionMember
{
    public Guid Id { get; set; }

    public Guid TeamMissionId { get; set; }
    public TeamMission TeamMission { get; set; } = null!;

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    /// <summary>
    /// Dəvət qəbul edilməyibsə töhfə sayılmır və mükafat verilmir — missiya
    /// dostun başına gəlməməlidir, o özü qoşulmalıdır.
    /// </summary>
    public TeamMemberStatus Status { get; set; } = TeamMemberStatus.Invited;

    public int Contribution { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
