namespace PetPal.Api.Entities;

/// <summary>
/// Sessiyanın sual sətri. Sessiya başlayanda cavabsız (<see cref="AnsweredAt"/> = null)
/// yaradılır — beləliklə sessiyanın sual dəsti ayrıca cədvəl olmadan qeyd olunur
/// və uşaq sessiyada olmayan sualı cavablaya bilmir.
/// </summary>
public class SessionAnswer
{
    public Guid Id { get; set; }

    public Guid LearningSessionId { get; set; }
    public LearningSession LearningSession { get; set; } = null!;

    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    public int Order { get; set; }

    public int ChosenIndex { get; set; } = -1;
    public bool IsCorrect { get; set; }
    public int ElapsedMs { get; set; }

    public DateTime? AnsweredAt { get; set; }

    public bool IsAnswered => AnsweredAt.HasValue;
}
