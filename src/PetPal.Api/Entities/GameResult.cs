namespace PetPal.Api.Entities;

/// <summary>
/// Mini oyun nəticəsi. Ayrıca saxlanılır ki, gündəlik mükafat limiti
/// tətbiq olunsun və valideyn panelində "oyun / öyrənmə" balansı görünsün.
/// </summary>
public class GameResult
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    /// <summary>Oyun kataloqundakı sabit açar (məs. "memory-match").</summary>
    public string GameKey { get; set; } = string.Empty;

    public int Score { get; set; }
    public int DurationMs { get; set; }

    public int StarsEarned { get; set; }
    public int XpEarned { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
