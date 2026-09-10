using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Ulduz/gem hərəkətlərinin dəftəri. Balans birbaşa dəyişdirilmir —
/// hər dəyişiklik burada iz buraxır ki, valideyn panelində izah edilə bilsin.
/// </summary>
public class RewardEntry
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public RewardKind Kind { get; set; }

    /// <summary>Müsbət = qazanc, mənfi = xərc (məs. pet üçün yemək almaq).</summary>
    public int Amount { get; set; }

    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
