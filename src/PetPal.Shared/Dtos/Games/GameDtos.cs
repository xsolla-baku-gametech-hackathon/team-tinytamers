using System.ComponentModel.DataAnnotations;
using PetPal.Shared.Dtos.Pets;
using PetPal.Shared.Validation;

namespace PetPal.Shared.Dtos.Games;

public class GameCatalogItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;

    /// <summary>Oyunun məşq etdirdiyi bacarıq — valideyn üçün şəffaflıq.</summary>
    public string SkillHint { get; set; } = string.Empty;

    /// <summary>Açılış qiyməti ulduzla. 0 = əvvəldən açıqdır.</summary>
    public int UnlockStarCost { get; set; }

    public bool IsUnlocked { get; set; }
}

public class UnlockGameRequest
{
    [Required]
    [StringLength(40)]
    public string GameKey { get; set; } = string.Empty;
}

public class UnlockGameResultDto
{
    public string GameKey { get; set; } = string.Empty;
    public int StarsSpent { get; set; }
    public List<GameCatalogItemDto> Catalog { get; set; } = new();
}

public class SubmitGameResultRequest
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.GameKeyRequired))]
    [StringLength(40)]
    public string GameKey { get; set; } = string.Empty;

    /// <summary>0–100 arası normallaşdırılmış nəticə.</summary>
    [Range(0, 100, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.ScoreRange))]
    public int Score { get; set; }

    [Range(0, 1_800_000, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.DurationRange))]
    public int DurationMs { get; set; }
}

public class GameResultDto
{
    public string GameKey { get; set; } = string.Empty;
    public int Score { get; set; }

    public int StarsEarned { get; set; }
    public int XpEarned { get; set; }

    /// <summary>Gündəlik mükafat limiti dolubsa <c>true</c> — oyun oynanır, amma ulduz verilmir.</summary>
    public bool RewardCapped { get; set; }

    public string Message { get; set; } = string.Empty;
    public PetDto Pet { get; set; } = new();
}
