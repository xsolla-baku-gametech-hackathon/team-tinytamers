using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Uşağın bacarıq sahəsi üzrə cari səviyyəsi. <see cref="Rating"/> Elo-tipli dəyərdir:
/// hər cavabdan sonra yenilənir və növbəti sualların çətinliyini müəyyən edir.
/// </summary>
public class SkillMastery
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public SkillArea Skill { get; set; }

    /// <summary>100–1000 aralığı. Başlanğıc 300 (≈ 3-cü çətinlik).</summary>
    public int Rating { get; set; } = 300;

    public int AnsweredCount { get; set; }
    public int CorrectCount { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
