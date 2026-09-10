using PetPal.Shared.Dtos.Rewards;
using PetPal.Shared.Enums;

namespace PetPal.Shared.Dtos.Progress;

public class SkillProgressDto
{
    public SkillArea Skill { get; set; }

    /// <summary>0–100. Adaptiv reytinqin normallaşdırılmış göstəricisi — UI-dakı "Focus Area" barları.</summary>
    public int MasteryPercent { get; set; }

    /// <summary>Daxili Elo-tipli reytinq (100–1000). Sual seçimi bu dəyərə görə edilir.</summary>
    public int Rating { get; set; }

    public int AnsweredCount { get; set; }
    public int CorrectCount { get; set; }
    public int AccuracyPercent { get; set; }

    /// <summary>Ən zəif 2 sahə "focus area" sayılır və gündəlik tapşırıqlarda daha tez-tez gəlir.</summary>
    public bool IsFocusArea { get; set; }
}

public class DailyGoalDto
{
    public DateOnly Date { get; set; }
    public int Target { get; set; }
    public int Completed { get; set; }
    public bool IsReached => Completed >= Target;
    public int StreakDays { get; set; }
}

public class ProgressSummaryDto
{
    public WalletDto Wallet { get; set; } = new();
    public DailyGoalDto DailyGoal { get; set; } = new();
    public List<SkillProgressDto> Skills { get; set; } = new();
    public int TotalAnswered { get; set; }
    public int AverageAccuracyPercent { get; set; }
    public List<BadgeDto> Badges { get; set; } = new();
}
