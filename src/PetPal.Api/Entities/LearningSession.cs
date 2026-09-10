using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

public class LearningSession
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public SkillArea Skill { get; set; }

    /// <summary>Hərəkətsizlikdən sonra təklif olunan qısa dəst — bonus mükafatı var.</summary>
    public bool IsSprint { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public int TotalCount { get; set; }
    public int CorrectCount { get; set; }
    public int StarsEarned { get; set; }
    public int XpEarned { get; set; }

    public ICollection<SessionAnswer> Answers { get; set; } = new List<SessionAnswer>();
}
