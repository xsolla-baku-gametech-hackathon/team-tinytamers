using PetPal.Shared.Enums;

namespace PetPal.Api.Learning.Arena;

/// <summary>
/// Arena reytinqinin saf riyaziyyatı — I/O yoxdur, birbaşa test olunur.
///
/// <para>Model <see cref="AdaptiveEngine"/> ilə eyni Elo ailəsindəndir, amma
/// AYRI dəyər üzərində işləyir: burada rəqib başqa uşaqdır, orada isə sualın
/// çətinliyi. İki reytinq iki fərqli işə xidmət edir və bir-birinə toxunmur.</para>
/// </summary>
public static class ArenaRatingEngine
{
    public const int MinRating = 100;
    public const int MaxRating = 1000;
    public const int StartingRating = 300;

    /// <summary>Öyrənmə reytinqindən kiçik addım: yarış nəticəsi bir gündə uşağı çox atmamalıdır.</summary>
    private const int KFactor = 24;

    /// <summary>Nəticə: doğru cavab sayı, bərabərlikdə isə SÜRƏT həll edir.</summary>
    public static DuelOutcome Decide(int myCorrect, int myMilliseconds, int opponentCorrect, int opponentMilliseconds)
    {
        if (myCorrect != opponentCorrect)
            return myCorrect > opponentCorrect ? DuelOutcome.Win : DuelOutcome.Loss;

        if (myMilliseconds == opponentMilliseconds)
            return DuelOutcome.Draw;

        return myMilliseconds < opponentMilliseconds ? DuelOutcome.Win : DuelOutcome.Loss;
    }

    public static DuelOutcome Opposite(DuelOutcome outcome) => outcome switch
    {
        DuelOutcome.Win => DuelOutcome.Loss,
        DuelOutcome.Loss => DuelOutcome.Win,
        _ => outcome
    };

    public static int UpdateRating(int currentRating, int opponentRating, DuelOutcome outcome)
    {
        var expected = 1.0 / (1.0 + Math.Pow(10, (opponentRating - currentRating) / 200.0));
        var actual = outcome switch
        {
            DuelOutcome.Win => 1.0,
            DuelOutcome.Draw => 0.5,
            _ => 0.0
        };

        var updated = currentRating + (int)Math.Round(KFactor * (actual - expected));
        return Math.Clamp(updated, MinRating, MaxRating);
    }

    /// <summary>Mükafat qaydası: uduzmaq da ulduz gətirir, heç bir halda mənfi olmur.</summary>
    public static int StarsFor(DuelOutcome outcome, ArenaOptions options) => outcome switch
    {
        DuelOutcome.Win => options.WinStars,
        DuelOutcome.Draw => options.DrawStars,
        DuelOutcome.Loss => options.LossStars,
        _ => 0
    };

    /// <summary>
    /// Sürət bonusu: rəqibi nə qədər TEZ udursansa, bir o qədər çox ulduz.
    ///
    /// <para>Bonus mütləq vaxta yox, rəqiblə FƏRQƏ baxır — çətin dəst hamıya
    /// eyni dərəcədə uzun gəlir, ona görə "5 saniyə" bir dəstdə əla, başqasında
    /// isə adi nəticədir. Pay = (rəqibin vaxtı − sənin vaxtın) / rəqibin vaxtı,
    /// yəni iki dəfə tez cavablamaq bonusun yarısını, dörd dəfə tez cavablamaq
    /// dörddə üçünü verir.</para>
    ///
    /// <para>Yalnız QALİBDƏ işləyir: uduzan uşağın ulduzu onsuz da azalmır, amma
    /// sürətinə görə qalibi keçməməlidir.</para>
    /// </summary>
    public static int SpeedBonusFor(
        DuelOutcome outcome, int myMilliseconds, int opponentMilliseconds, ArenaOptions options)
    {
        if (outcome != DuelOutcome.Win || options.SpeedBonusStars <= 0)
            return 0;

        // Rəqibin vaxtı yoxdursa (məşq, yarımçıq qeyd) müqayisə üçün baza yoxdur.
        if (opponentMilliseconds <= 0 || myMilliseconds >= opponentMilliseconds)
            return 0;

        var share = (double)(opponentMilliseconds - myMilliseconds) / opponentMilliseconds;
        return (int)Math.Round(options.SpeedBonusStars * Math.Clamp(share, 0, 1), MidpointRounding.AwayFromZero);
    }
}
