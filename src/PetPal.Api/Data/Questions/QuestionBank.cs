using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.Data.Questions;

/// <summary>
/// Sual banklarının ortaq köməkçisi.
///
/// Bank <c>DbInitializer</c>-dən çıxarılıb: dörd bacarıq × on çətinlik × iki dil
/// bir faylda saxlanılsaydı, seed məntiqi mətnin altında itərdi.
///
/// Bir qayda hər iki bankda eynidir: <b>düzgün cavabın yeri dəyişməlidir</b>.
/// Uşaq "həmişə birincisi" naxışını üç sessiyaya tapır və sualı oxumağı dayandırır.
/// Qayda testlə qorunur (<c>QuestionBankTests</c>).
/// </summary>
internal static class QuestionBank
{
    public static Question Choice(
        string language,
        SkillArea skill,
        int difficulty,
        string prompt,
        string[] options,
        int correctIndex,
        string explanation,
        string hint) => new()
        {
            Skill = skill,
            LanguageCode = language,
            Difficulty = difficulty,
            Prompt = prompt,
            Options = options.ToList(),
            CorrectIndex = correctIndex,
            Explanation = explanation,
            Hint = hint,

            // Çətinlik artdıqca sual yuxarı yaşa keçir: 1–2 → 5 yaş, 9–10 → 8 yaş.
            MinAge = Math.Clamp(4 + difficulty / 2, 5, 12),
            MaxAge = 12
        };
}
