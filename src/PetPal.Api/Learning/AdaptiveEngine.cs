namespace PetPal.Api.Learning;

/// <summary>
/// Adaptiv öyrənmə mühərriki — saf riyaziyyat, I/O yoxdur.
///
/// Model Elo-ya bənzəyir: uşağın hər bacarıq üzrə reytinqi var (100–1000),
/// sualın çətinliyi isə <c>Difficulty × 100</c> reytinqinə uyğun gəlir.
/// Doğru cavab reytinqi qaldırır, səhv cavab endirir; addımın böyüklüyü
/// gözlənilən nəticədən nə qədər fərqləndiyindən asılıdır.
/// Nəticədə uşaq həmişə "bir az çətin, amma bacarılan" zonada qalır.
/// </summary>
public static class AdaptiveEngine
{
    public const int MinRating = 100;
    public const int MaxRating = 1000;
    public const int StartingRating = 300;

    /// <summary>Reytinq dəyişiminin maksimum addımı.</summary>
    private const int KFactor = 40;

    public static int TargetDifficulty(int rating) =>
        Math.Clamp((int)Math.Round(rating / 100.0), 1, 10);

    public static int QuestionRating(int difficulty) => Math.Clamp(difficulty, 1, 10) * 100;

    /// <summary>Uşağın bu sualı doğru cavablama ehtimalı (0–1).</summary>
    public static double ExpectedScore(int childRating, int difficulty)
    {
        var questionRating = QuestionRating(difficulty);
        return 1.0 / (1.0 + Math.Pow(10, (questionRating - childRating) / 200.0));
    }

    public static int UpdateRating(int currentRating, int difficulty, bool isCorrect)
    {
        var expected = ExpectedScore(currentRating, difficulty);
        var actual = isCorrect ? 1.0 : 0.0;
        var updated = currentRating + (int)Math.Round(KFactor * (actual - expected));
        return Math.Clamp(updated, MinRating, MaxRating);
    }

    /// <summary>UI-dakı "Focus Area" barları üçün 0–100 dəyər.</summary>
    public static int MasteryPercent(int rating) =>
        Math.Clamp((rating - MinRating) * 100 / (MaxRating - MinRating), 0, 100);

    /// <summary>Doğru cavabın ulduz dəyəri — çətin sual daha çox ulduz gətirir.</summary>
    public static int StarsFor(int difficulty, bool isCorrect) =>
        isCorrect ? 10 + Math.Clamp(difficulty, 1, 10) : 0;

    /// <summary>
    /// XP səhv cavabda da verilir (az miqdarda) — cəhd etmək cəzalandırılmır,
    /// uşaq davam etməyə həvəsləndirilir.
    /// </summary>
    public static int XpFor(int difficulty, bool isCorrect) =>
        isCorrect ? 5 + Math.Clamp(difficulty, 1, 10) : 2;
}
