namespace PetPal.Api.Entities;

/// <summary>"Real Life Connect" — ekrandan kənar kəşflər.</summary>
public class Discovery
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public string Label { get; set; } = string.Empty;
    public string CategoryKey { get; set; } = "nature";

    /// <summary>Fayl sistemindəki nisbi yol; foto könüllüdür.</summary>
    public string? PhotoPath { get; set; }

    public int StarsEarned { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
