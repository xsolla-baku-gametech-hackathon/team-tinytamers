using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>Missiya kataloqu (bütün uşaqlar üçün ümumi tərif).</summary>
public class Mission
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string? TitleAz { get; set; }
    public string? DescriptionAz { get; set; }

    public MissionType Type { get; set; }

    /// <summary>Yalnız <see cref="MissionType.SolveQuestions"/> üçün: konkret bacarıq tələbi.</summary>
    public SkillArea? Skill { get; set; }

    public int Target { get; set; } = 3;
    public int RewardStars { get; set; } = 20;
    public int RewardGems { get; set; }

    public Guid WorldZoneId { get; set; }
    public WorldZone WorldZone { get; set; } = null!;

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Uşağın konkret missiya üzrə irəliləyişi.</summary>
public class ChildMission
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public Guid MissionId { get; set; }
    public Mission Mission { get; set; } = null!;

    public int Progress { get; set; }
    public MissionStatus Status { get; set; } = MissionStatus.Active;

    public DateTime? CompletedAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
