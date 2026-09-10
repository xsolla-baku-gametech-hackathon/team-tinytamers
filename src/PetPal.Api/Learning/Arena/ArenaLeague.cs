namespace PetPal.Api.Learning.Arena;

/// <summary>
/// Həftəlik liqanın saf qaydaları — I/O yoxdur, birbaşa test olunur.
///
/// <para>Liqa AYRICA cədvəldə saxlanılmır: hər həftə duel qeydlərindən yenidən
/// hesablanır. Səbəb sadədir — "həftə sonu sıfırlanma" belə heç bir iş tələb
/// etmir, keçmiş həftələr isə itmir (istənilən həftə eyni sorğu ilə açılır).</para>
/// </summary>
public static class ArenaLeague
{
    public const int WinPoints = 3;
    public const int DrawPoints = 1;
    public const int LossPoints = 0;

    /// <summary>Liqa cədvəlində göstərilən sətir sayı.</summary>
    public const int TopCount = 20;

    /// <summary>Nişan üçün minimum duel — bir duelli "çempion" olmasın deyə.</summary>
    public const int BadgeMinimumDuels = 3;

    /// <summary>
    /// Həftənin başlanğıcı: BAZAR ERTƏSİ 00:00 UTC.
    ///
    /// <para>Uşağın yerli saatı deyil, UTC işlədilir — liqa qlobaldır və bütün
    /// iştirakçılar üçün eyni anda sıfırlanmalıdır. Fərqli vaxt qurşaqlarında
    /// yerli həftə sərhədi işlətsək, iki uşağın "bu həftəsi" fərqli olardı.</para>
    /// </summary>
    public static DateTime WeekStart(DateTime utcNow)
    {
        var daysSinceMonday = ((int)utcNow.DayOfWeek + 6) % 7;
        return utcNow.Date.AddDays(-daysSinceMonday);
    }

    public static DateTime WeekEnd(DateTime utcNow) => WeekStart(utcNow).AddDays(7);

    public static int Points(int wins, int draws) => wins * WinPoints + draws * DrawPoints;
}
