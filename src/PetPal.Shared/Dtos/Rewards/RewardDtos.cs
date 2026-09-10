using PetPal.Shared.Enums;

namespace PetPal.Shared.Dtos.Rewards;

public class WalletDto
{
    public int Stars { get; set; }
    public int Gems { get; set; }
}

public class RewardEntryDto
{
    public Guid Id { get; set; }
    public RewardKind Kind { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class BadgeDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BadgeTier Tier { get; set; }
    public string IconKey { get; set; } = string.Empty;
    public DateTime? EarnedAt { get; set; }
    public bool IsEarned => EarnedAt.HasValue;
}
