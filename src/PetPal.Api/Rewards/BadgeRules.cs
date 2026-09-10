namespace PetPal.Api.Rewards;

/// <summary>Nişan qaydalarının saf (I/O-suz) hissəsi — birbaşa unit test olunur.</summary>
public class BadgeStats
{
    public int AnsweredCount { get; init; }
    public int PerfectRounds { get; init; }
    public int MathCorrect { get; init; }
    public int VocabularyCorrect { get; init; }
    public int LogicCorrect { get; init; }
    public int StreakDays { get; init; }
    public int DiscoveryCount { get; init; }
    public int CareActionCount { get; init; }
    public int ZonesRestored { get; init; }

    /// <summary>Tamamlanmış arena duelləri (nəticəsi hazır olanlar).</summary>
    public int ArenaDuels { get; init; }

    public int ArenaWins { get; init; }

    /// <summary>Cari həftəlik liqadakı yer; 0 = bu həftə duel oynanmayıb.</summary>
    public int ArenaLeagueRank { get; init; }

    /// <summary>Cari həftədə oynanmış duel sayı.</summary>
    public int ArenaWeeklyDuels { get; init; }
}

public static class BadgeRules
{
    /// <summary>Liqa nişanı üçün həftəlik minimum duel sayı (<c>ArenaLeague.BadgeMinimumDuels</c> ilə eynidir).</summary>
    public const int ArenaLeagueBadgeMinimumDuels = 3;

    public static IReadOnlyList<string> Evaluate(BadgeStats stats)
    {
        var earned = new List<string>();

        if (stats.AnsweredCount >= 1) earned.Add("first-steps");
        if (stats.PerfectRounds >= 1) earned.Add("perfect-round");
        if (stats.MathCorrect >= 25) earned.Add("math-starter");
        if (stats.VocabularyCorrect >= 25) earned.Add("word-wizard");
        if (stats.LogicCorrect >= 25) earned.Add("logic-hero");
        if (stats.StreakDays >= 3) earned.Add("streak-3");
        if (stats.StreakDays >= 7) earned.Add("streak-7");
        if (stats.DiscoveryCount >= 5) earned.Add("explorer");
        if (stats.CareActionCount >= 20) earned.Add("pet-friend");
        if (stats.ZonesRestored >= 1) earned.Add("world-healer");
        if (stats.ArenaDuels >= 1) earned.Add("arena-first");
        if (stats.ArenaDuels >= 10) earned.Add("arena-veteran");
        if (stats.ArenaWins >= 5) earned.Add("arena-champion");

        // Liqa nişanı bir duelli "çempion" yaratmasın deyə həftədə ən azı üç duel tələb olunur.
        if (stats.ArenaWeeklyDuels >= ArenaLeagueBadgeMinimumDuels
            && stats.ArenaLeagueRank is >= 1 and <= 3)
            earned.Add("arena-league-top3");

        return earned;
    }
}
