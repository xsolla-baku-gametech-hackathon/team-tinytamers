using PetPal.Shared.Dtos.Rewards;
using PetPal.Shared.Enums;

namespace PetPal.Shared.Dtos.Missions;

public class MissionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public MissionType Type { get; set; }
    public MissionStatus Status { get; set; }

    public int Progress { get; set; }
    public int Target { get; set; }

    public int RewardStars { get; set; }
    public int RewardGems { get; set; }

    public string ZoneCode { get; set; } = string.Empty;
    public SkillArea? Skill { get; set; }
}

public class WorldZoneDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;

    public int RequiredStars { get; set; }
    public bool IsUnlocked { get; set; }

    /// <summary>0–100: zonanın "bərpa" faizi — tamamlanmış missiyalardan hesablanır.</summary>
    public int RestoredPercent { get; set; }

    public List<MissionDto> Missions { get; set; } = new();
}

/// <summary>
/// "World changes based on engagement" — dünyanın hava/vizual vəziyyəti
/// son 7 günün iştirak balına görə hesablanır.
/// </summary>
public class WorldStateDto
{
    public WorldWeather Weather { get; set; }
    public int EngagementScore { get; set; }
    public string Headline { get; set; } = string.Empty;
    public List<WorldZoneDto> Zones { get; set; } = new();
}

public class ClaimMissionResultDto
{
    public MissionDto Mission { get; set; } = new();
    public WalletDto Wallet { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public List<BadgeDto> NewBadges { get; set; } = new();
}
