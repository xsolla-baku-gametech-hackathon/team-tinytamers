using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

public class Badge
{
    public Guid Id { get; set; }

    /// <summary>Kod sabitdir və qaydalar mühərriki ona görə işləyir (məs. "first-steps").</summary>
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string? TitleAz { get; set; }
    public string? DescriptionAz { get; set; }

    public BadgeTier Tier { get; set; }
    public string IconKey { get; set; } = string.Empty;

    public ICollection<ChildBadge> ChildBadges { get; set; } = new List<ChildBadge>();
}

public class ChildBadge
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public Guid BadgeId { get; set; }
    public Badge Badge { get; set; } = null!;

    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
}
