using PetPal.Api.Learning;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>Bir təcrübənin bitmiş nəticəsi — çətinlik qərarının yeganə girişi.</summary>
public sealed record RunPerformance(
    PetBrainDifficulty Difficulty,
    int ScorePercent,
    int Hints,
    int Mistakes);

/// <summary>
/// Təcrübənin çətinliyi — saf qaydalar, I/O yoxdur.
///
/// <para><b>Bu, məktəb reytinqi DEYİL.</b> <see cref="SkillMastery"/> Elo-dur və
/// adaptiv sual seçimini idarə edir; macəranın ritmi isə ayrıdır. İkisini
/// qarışdırmaq həm arena/oyun davranışını dərs seçiminə sızdırardı, həm də
/// tərsinə. Ona görə burada AYRI, kiçik və yavaş bir pillə sistemi var.</para>
///
/// <para>Təsdiqlənmiş TAPMACA isə istisnadır: onun çətinliyi mövcud
/// <see cref="AdaptiveEngine.TargetDifficulty"/> dəyərindən gəlir — ikinci
/// akademik Elo qurmaq düzgün olmazdı.</para>
/// </summary>
public static class ExperienceDifficulty
{
    /// <summary>Bu nəticədən yuxarı "rahat bacardı" sayılır.</summary>
    public const int StrongScorePercent = 85;

    /// <summary>Bu qədər səhv "çətin gəldi" siqnalıdır.</summary>
    public const int StrugglingMistakes = 3;

    /// <summary>Bu qədər ipucu da eyni siqnaldır — amma cəza deyil, KÖMƏKdir.</summary>
    public const int StrugglingHints = 2;

    /// <summary>
    /// İlk təcrübənin pilləsi. Yaş TƏHLÜKƏSİZLİK həddidir: kiçik uşaq Hard
    /// almır. Bu, onun bacarığı haqqında iddia deyil.
    /// </summary>
    public static PetBrainDifficulty Initial(int age, int masteryTargetDifficulty)
    {
        var ceiling = Ceiling(age);

        // Mövcud adaptiv mühərrikin hədəf çətinliyi (1–10) pilləyə oturdulur.
        var tier = masteryTargetDifficulty switch
        {
            <= 2 => PetBrainDifficulty.Easy,
            <= 5 => PetBrainDifficulty.Medium,
            _ => PetBrainDifficulty.Hard
        };

        return Min(tier, ceiling);
    }

    /// <summary>
    /// NÖVBƏTİ təcrübənin pilləsi. Cari run heç vaxt dəyişmir — uşaq başladığı
    /// macərənın ortasında qəfil divara çırpılmamalıdır.
    ///
    /// <para>Bir tamamlamada ən çox BİR pillə hərəkət olur.</para>
    /// </summary>
    public static PetBrainDifficulty Next(RunPerformance previous, int age)
    {
        var ceiling = Ceiling(age);

        var struggled = previous.Mistakes >= StrugglingMistakes || previous.Hints >= StrugglingHints;

        // Çətinlik siqnalı üstündür: uşaq həm çox səhv edib, həm də yüksək bal
        // ala bilməz, amma qarışıq hallarda köməyə üstünlük verilir.
        if (struggled)
            return Min(Step(previous.Difficulty, -1), ceiling);

        var strong = previous.ScorePercent >= StrongScorePercent
                     && previous.Hints == 0
                     && previous.Mistakes == 0;

        return strong
            ? Min(Step(previous.Difficulty, +1), ceiling)
            : Min(previous.Difficulty, ceiling);
    }

    /// <summary>
    /// Əlavə dəstək lazımdırmı — ipucu düyməsi əvvəldən görünsün, tapmacada
    /// variant sayı azalsın. Bu, çətinliyi endirməyin YUMŞAQ variantıdır.
    /// </summary>
    public static bool NeedsAssist(RunPerformance previous) =>
        previous.Mistakes >= StrugglingMistakes || previous.Hints >= StrugglingHints;

    /// <summary>Nümayiş panelində göstərilən qısa siqnal mətni (dilə görə).</summary>
    public static string Signal(RunPerformance? previous, string language)
    {
        if (previous is null)
            return Localized(language, "Hələ tamamlanmış təcrübə yoxdur — başlanğıc pilləsi yaşa görədir.",
                "No completed experience yet — the starting tier comes from age.");

        if (NeedsAssist(previous))
            return Localized(language,
                $"Son təcrübədə {previous.Mistakes} səhv, {previous.Hints} ipucu — növbəti dəfə bir pillə asan.",
                $"Last run had {previous.Mistakes} mistakes and {previous.Hints} hints — one tier easier next time.");

        if (previous.ScorePercent >= StrongScorePercent && previous.Hints == 0 && previous.Mistakes == 0)
            return Localized(language,
                $"Son təcrübə {previous.ScorePercent}% — ipucusuz və səhvsiz, növbəti dəfə bir pillə çətin.",
                $"Last run was {previous.ScorePercent}% with no hints or mistakes — one tier harder next time.");

        return Localized(language,
            $"Son təcrübə {previous.ScorePercent}% — pillə olduğu kimi qalır.",
            $"Last run was {previous.ScorePercent}% — the tier stays the same.");
    }

    /// <summary>Yaşın icazə verdiyi ən yüksək pillə.</summary>
    public static PetBrainDifficulty Ceiling(int age) => age switch
    {
        <= 6 => PetBrainDifficulty.Easy,
        <= 8 => PetBrainDifficulty.Medium,
        _ => PetBrainDifficulty.Hard
    };

    private static PetBrainDifficulty Step(PetBrainDifficulty tier, int direction) =>
        (PetBrainDifficulty)Math.Clamp((int)tier + direction, (int)PetBrainDifficulty.Easy, (int)PetBrainDifficulty.Hard);

    private static PetBrainDifficulty Min(PetBrainDifficulty a, PetBrainDifficulty b) =>
        (PetBrainDifficulty)Math.Min((int)a, (int)b);

    private static string Localized(string language, string az, string en) =>
        Common.Localized.T(language, az, en);
}
