namespace PetPal.Api.Learning.Arena;

/// <summary>
/// Uyğunlaşdırmanın saf hissəsi — I/O yoxdur, birbaşa test olunur.
///
/// <para>Zolaq açıq duelin YAŞINA görə genişlənir: nə qədər çox gözləyibsə,
/// bir o qədər uzaq rəqibi qəbul edir.</para>
///
/// <para>Ölçü vahidi SANİYƏDİR, dəqiqə yox. Yarış sinxrondur — uşaq axtarış
/// ekranında canlı gözləyir və orada bir dəqiqə uzun müddətdir. Reytinqi bir az
/// uzaq rəqiblə qarşılaşdırmaq uşağı boş ekranda saxlamaqdan yaxşıdır.</para>
/// </summary>
public static class ArenaMatchmaker
{
    /// <summary>Zolaq yoxdur — hovuzda kim varsa uyğundur.</summary>
    public const int NoBand = int.MaxValue;

    /// <summary>Açıq duelin yaşına uyğun reytinq zolağı (±).</summary>
    public static int RatingBandFor(int waitingSeconds, ArenaOptions options)
    {
        if (waitingSeconds < options.WidenAfterSeconds)
            return options.RatingBand;

        return waitingSeconds < options.WidenAfterSeconds * 2
            ? options.WidenedRatingBand
            : NoBand;
    }

    /// <summary>
    /// Çətinlik dözümü (±). Dəst duel yaradılanda snapshot edildiyinə görə
    /// "iki uşağın ortası" qoşulan anda hesablana bilmir — əvəzinə qoşulan
    /// yalnız ÖZ hədəf çətinliyinə yaxın dueli qəbul edir. Nəticə eynidir:
    /// oynanan çətinlik hər ikisinin hədəfindən ən çox 1 addım uzaqdır.
    /// </summary>
    public static int DifficultyToleranceFor(int waitingSeconds, ArenaOptions options)
    {
        if (waitingSeconds < options.WidenAfterSeconds)
            return 1;

        return waitingSeconds < options.WidenAfterSeconds * 2 ? 2 : NoBand;
    }

    /// <summary>Namizəd duel bu uşağa uyğundurmu.</summary>
    public static bool Matches(
        int waitingSeconds,
        int creatorRating,
        int childRating,
        int duelDifficulty,
        int childTargetDifficulty,
        ArenaOptions options)
    {
        var band = RatingBandFor(waitingSeconds, options);
        if (band != NoBand && Math.Abs(creatorRating - childRating) > band)
            return false;

        var tolerance = DifficultyToleranceFor(waitingSeconds, options);
        return tolerance == NoBand || Math.Abs(duelDifficulty - childTargetDifficulty) <= tolerance;
    }
}
