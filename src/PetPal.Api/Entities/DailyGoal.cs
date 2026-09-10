namespace PetPal.Api.Entities;

/// <summary>Gündəlik hədəf (ana ekranda "Daily Goal 3/5" barı) və ekran vaxtı ölçüsü.</summary>
public class DailyGoal
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public DateOnly Date { get; set; }

    public int Target { get; set; } = 5;
    public int Completed { get; set; }

    public int CorrectCount { get; set; }
    public int AnsweredCount { get; set; }

    /// <summary>Valideyn panelindəki "screen time balance" göstəricisi.</summary>
    public int MinutesSpent { get; set; }

    public bool RewardGranted { get; set; }
}
