using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>Sual bankı. Adaptiv seçim <see cref="Difficulty"/> və uşağın reytinqinə görə edilir.</summary>
public class Question
{
    public Guid Id { get; set; }

    public SkillArea Skill { get; set; }

    /// <summary>Sualın dili ("az" / "en"). Uşağa yalnız öz dilindəki suallar verilir.</summary>
    public string LanguageCode { get; set; } = "az";

    /// <summary>1–10. Daxili reytinq qarşılığı: Difficulty * 100.</summary>
    public int Difficulty { get; set; } = 3;

    public string Prompt { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public int CorrectIndex { get; set; }

    public string Explanation { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;

    public int MinAge { get; set; } = 5;
    public int MaxAge { get; set; } = 12;

    public bool IsActive { get; set; } = true;
}
