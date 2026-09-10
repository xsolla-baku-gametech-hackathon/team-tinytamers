using System.ComponentModel.DataAnnotations;
using PetPal.Shared.Dtos.Rewards;
using PetPal.Shared.Enums;
using PetPal.Shared.Validation;

namespace PetPal.Shared.Dtos.Learning;

public class StartSessionRequest
{
    /// <summary>Boş buraxılsa, adaptiv mühərrik ən zəif bacarıq sahəsini özü seçir.</summary>
    public SkillArea? Skill { get; set; }

    [Range(1, 10, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.QuestionCountRange))]
    public int QuestionCount { get; set; } = 5;

    /// <summary>
    /// "Knowledge Sprint" — uşaq app-də hərəkətsiz qaldıqda təklif olunan
    /// qısa (3 sual) dəst. Tamamlananda bonus ulduz verilir.
    /// </summary>
    public bool IsSprint { get; set; }
}

public class LearningSessionDto
{
    public Guid SessionId { get; set; }
    public SkillArea Skill { get; set; }
    public bool IsSprint { get; set; }
    public List<QuestionDto> Questions { get; set; } = new();
}

public class QuestionDto
{
    public Guid Id { get; set; }
    public SkillArea Skill { get; set; }

    /// <summary>1–10 çətinlik. Adaptiv seçim uşağın cari reytinqinə yaxın sualları gətirir.</summary>
    public int Difficulty { get; set; }

    public string Prompt { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public string Hint { get; set; } = string.Empty;
}

public class SubmitAnswerRequest
{
    [Required]
    public Guid SessionId { get; set; }

    [Required]
    public Guid QuestionId { get; set; }

    [Range(0, 5, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.AnswerChoiceRange))]
    public int ChosenIndex { get; set; }

    [Range(0, 600000)]
    public int ElapsedMs { get; set; }
}

public class AnswerResultDto
{
    public bool IsCorrect { get; set; }
    public int CorrectIndex { get; set; }
    public string Explanation { get; set; } = string.Empty;

    public int StarsEarned { get; set; }
    public int XpEarned { get; set; }

    /// <summary>Pet-in cavabdan sonrakı replikası — "Great job! You solved 3 math questions!" tipli.</summary>
    public string PetReaction { get; set; } = string.Empty;

    public int DailyGoalDone { get; set; }
    public int DailyGoalTarget { get; set; }
}

public class SessionSummaryDto
{
    public Guid SessionId { get; set; }
    public SkillArea Skill { get; set; }
    public int CorrectCount { get; set; }
    public int TotalCount { get; set; }
    public int AccuracyPercent { get; set; }

    public int StarsEarned { get; set; }
    public int XpEarned { get; set; }

    public int PetLevel { get; set; }
    public bool PetLeveledUp { get; set; }
    public PetStage PetStage { get; set; }

    public List<BadgeDto> NewBadges { get; set; } = new();
    public string PetMessage { get; set; } = string.Empty;

    /// <summary>Sprint tam doğru tamamlananda verilən əlavə ulduz.</summary>
    public int SprintBonusStars { get; set; }
}
