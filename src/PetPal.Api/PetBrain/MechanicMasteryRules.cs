using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>Bir cəhdin nəticəsi — ustalıq yeniləməsinin YEGANƏ girişi.</summary>
/// <param name="Solved">Tapmaca həll olundumu.</param>
/// <param name="UsedHint">İpucu istənildimi.</param>
/// <param name="Mistakes">Neçə səhv cəhd oldu.</param>
/// <param name="Difficulty">Cəhdin verildiyi çətinlik pilləsi.</param>
public sealed record MechanicAttempt(bool Solved, bool UsedHint, int Mistakes, PetBrainDifficulty Difficulty);

/// <summary>
/// Mexanika ustalığının qaydaları — <b>saf funksiyalar</b>, I/O yoxdur.
///
/// <para><b>Model qəsdən məhdud Elo-dur.</b> Dərin öyrənmə modeli burada nə
/// lazımdır, nə də auditə açıqdır: valideyn «niyə çətinləşdi?» soruşanda cavab
/// bir cümlə olmalıdır — «marşrutu üç dəfə köməksiz həll etdi». Elo bunu verir,
/// determinist qalır və testdə eyni giriş eyni nəticəni yaradır.</para>
///
/// <para><b>Ustalıq maraqdan AYRIDIR.</b> Bu qaydalar heç bir yerdə
/// <see cref="PlayerTrait"/>-ə toxunmur: tapmacada uğursuzluq mövzunu
/// sevməmək demək deyil, ipucu istəmək isə nə birini, nə digərini
/// göstərir.</para>
///
/// <para><b>Kömək cəzalandırılmır.</b> İpucu ilə gələn uğur həqiqi uğurdur və
/// səviyyəni QALDIRIR — sadəcə müstəqil uğurdan az. Bunları qarışdırmaq
/// uşağın çətinliyini haqsız yerə qaldırırdı.</para>
/// </summary>
public static class MechanicMasteryRules
{
    /// <summary>Çətinlik pillələrinin səviyyə qarşılığı — gözlənilən nəticə bundan çıxır.</summary>
    public const int EasyTarget = 25;
    public const int MediumTarget = 50;
    public const int HardTarget = 75;

    /// <summary>Elo əyrisinin eni: bu qədər fərq təxminən 10:1 üstünlük deməkdir.</summary>
    public const double RatingSpread = 25.0;

    /// <summary>Müstəqil həll.</summary>
    public const double SolvedOutcome = 1.0;

    /// <summary>
    /// Köməklə həll. Sıfırla bir arasındadır və qəsdən yuxarı yarıdadır:
    /// uşaq tapmacanı BİTİRDİ, sadəcə tək başına deyil.
    /// </summary>
    public const double AssistedOutcome = 0.6;

    /// <summary>Həll olunmadı. Cəza deyil — sadəcə «hələ yox» siqnalı.</summary>
    public const double UnsolvedOutcome = 0.0;

    /// <summary>Az cəhd olanda model tez kalibrlənir…</summary>
    public const int EarlyStep = 12;
    public const int MidStep = 8;

    /// <summary>…çox cəhddən sonra isə bir nəticə profili sarsıtmır.</summary>
    public const int SettledStep = 5;

    public const int EarlyAttempts = 5;
    public const int MidAttempts = 15;

    /// <summary>Bir cəhdin dəyişə biləcəyi ƏN BÖYÜK səviyyə — profil sıçramasın.</summary>
    public const int MaxStepPerAttempt = 6;

    /// <summary>Bu saydan sonra cəhdlər inamı artırmır.</summary>
    public const int ConfidenceSaturation = 8;

    /// <summary>Bu qədər gündən sonra ustalıq inamı yumşalır — bacarıq unudulur.</summary>
    public const int FreshnessGraceDays = 21;

    /// <summary>
    /// Aşağı səviyyədə bu qədər cəhddən sonra bir pillə YUXARI sınanır.
    ///
    /// <para>Qoruyucu qəsdəndir: yalnız «uyğun çətinlik» qaydası ilə getsək,
    /// zəif başlayan uşaq eyni asan tapşırığın sonsuz təkrarına düşərdi —
    /// bu, adaptiv sistem deyil, tələdir.</para>
    /// </summary>
    public const int StretchAfterAttempts = 6;

    /// <summary>Çətinliyin gözlənilən səviyyə qarşılığı.</summary>
    public static int TargetFor(PetBrainDifficulty difficulty) => difficulty switch
    {
        PetBrainDifficulty.Easy => EasyTarget,
        PetBrainDifficulty.Hard => HardTarget,
        _ => MediumTarget
    };

    /// <summary>Uşağın bu çətinliyi keçmə ehtimalı (0–1).</summary>
    public static double Expected(int level, PetBrainDifficulty difficulty) =>
        1.0 / (1.0 + Math.Pow(10, (TargetFor(difficulty) - level) / RatingSpread));

    /// <summary>Cəhdin nəticə dəyəri (0–1).</summary>
    public static double OutcomeOf(MechanicAttempt attempt)
    {
        if (!attempt.Solved)
            return UnsolvedOutcome;

        return attempt.UsedHint || attempt.Mistakes > 0 ? AssistedOutcome : SolvedOutcome;
    }

    /// <summary>Cəhd sayına görə addım əmsalı.</summary>
    public static int StepFor(int attempts) => attempts switch
    {
        < EarlyAttempts => EarlyStep,
        < MidAttempts => MidStep,
        _ => SettledStep
    };

    /// <summary>
    /// Bir cəhdi sətirə tətbiq edir. Sətir YERİNDƏ dəyişdirilir; çağıran
    /// yalnız saxlayır.
    /// </summary>
    public static void Apply(MechanicMastery mastery, MechanicAttempt attempt, DateTime now)
    {
        var outcome = OutcomeOf(attempt);
        var expected = Expected(mastery.EstimatedLevel, attempt.Difficulty);

        var raw = StepFor(mastery.Attempts) * (outcome - expected);
        var delta = Math.Clamp(
            (int)Math.Round(raw, MidpointRounding.AwayFromZero),
            -MaxStepPerAttempt,
            MaxStepPerAttempt);

        // Köməklə də olsa BİTİRMƏK səviyyəni aşağı salmır.
        //
        // Saf Elo bunu edərdi: asan tapmacada ipucu istəmək «gözləntidən aşağı»
        // sayılır. Amma nəticə uşaq üçün tələ olardı — kömək istədikcə səviyyə
        // düşər, səviyyə düşdükcə tapmaca asanlaşar və o, heç vaxt irəli
        // getməzdi. Uğursuzluq hələ də səviyyəni endirir; BİTİRMƏK yox.
        if (attempt.Solved && delta < 0)
            delta = 0;

        mastery.EstimatedLevel = Math.Clamp(
            mastery.EstimatedLevel + delta, MechanicMastery.MinLevel, MechanicMastery.MaxLevel);

        mastery.Attempts++;

        if (attempt.Solved)
        {
            if (attempt.UsedHint || attempt.Mistakes > 0)
                mastery.AssistedSuccesses++;
            else
                mastery.Successes++;
        }

        mastery.RecentTrend = Math.Clamp(
            mastery.RecentTrend + TrendStep(attempt),
            MechanicMastery.MinTrend,
            MechanicMastery.MaxTrend);

        mastery.LastPracticedAt = now;
        mastery.UpdatedAt = now;
    }

    /// <summary>
    /// Trendin bir addımı. Köməklə gələn uğur trendi AŞAĞI salmır — kömək
    /// istəmək geriləmə deyil.
    /// </summary>
    private static int TrendStep(MechanicAttempt attempt)
    {
        if (!attempt.Solved)
            return -1;

        return attempt.UsedHint || attempt.Mistakes > 0 ? 0 : 1;
    }

    /// <summary>
    /// Bu mexanika üçün tövsiyə olunan çətinlik.
    ///
    /// <para><b>Sonsuz çətinlik yoxdur:</b> yuxarı hədd <c>Hard</c>-dır.
    /// <b>Sonsuz asanlıq da yoxdur:</b> uzun müddət aşağı pillədə qalan uşağa
    /// determinist şəkildə bir pillə yuxarı təklif olunur (bax
    /// <see cref="ShouldStretch"/>).</para>
    /// </summary>
    public static PetBrainDifficulty BandFor(MechanicMastery? mastery)
    {
        if (mastery is null)
            return PetBrainDifficulty.Easy;

        var band = mastery.EstimatedLevel switch
        {
            < 40 => PetBrainDifficulty.Easy,
            < 70 => PetBrainDifficulty.Medium,
            _ => PetBrainDifficulty.Hard
        };

        if (band != PetBrainDifficulty.Hard && ShouldStretch(mastery))
            band = band == PetBrainDifficulty.Easy ? PetBrainDifficulty.Medium : PetBrainDifficulty.Hard;

        return band;
    }

    /// <summary>
    /// Uşaq eyni pillədə çox qalıbmı və bir addım yuxarı sınanmalıdırmı.
    ///
    /// <para>Determinist: cəhd sayından hesablanır, təsadüf yoxdur — yəni test
    /// onu sabit şəkildə yoxlaya bilir. Trend MƏNFİ olanda sınanmır: uşaq
    /// artıq çətinlik çəkirsə, üstünə qoymaq kömək deyil.</para>
    /// </summary>
    public static bool ShouldStretch(MechanicMastery mastery) =>
        mastery.Attempts >= StretchAfterAttempts
        && mastery.RecentTrend >= 0
        && mastery.Attempts % StretchAfterAttempts == 0;

    /// <summary>
    /// Ustalıq təxmininə İNAM (0–100): nə qədər cəhd, nə qədər təzə.
    ///
    /// <para>Səviyyədən AYRIDIR: «50 səviyyə, bir cəhd» ilə «50 səviyyə, on
    /// cəhd» eyni şey deyil və birincisinə əsasən çətinliyi qaldırmaq
    /// təxmini fakt kimi işlətmək olardı.</para>
    /// </summary>
    public static int Confidence(MechanicMastery? mastery, DateTime now)
    {
        if (mastery is null || mastery.Attempts <= 0)
            return 0;

        var volume = Math.Min(1.0, mastery.Attempts / (double)ConfidenceSaturation);

        var practiced = mastery.LastPracticedAt ?? mastery.UpdatedAt;
        var idleDays = practiced == default
            ? 0
            : Math.Max(0, (now - practiced).TotalDays - FreshnessGraceDays);

        var freshness = Math.Max(0.4, 1.0 - (idleDays / 120.0));

        return Math.Clamp((int)Math.Round(100 * volume * freshness, MidpointRounding.AwayFromZero), 0, 100);
    }

    /// <summary>Yeni, hələ heç nə bilinməyən sətir.</summary>
    public static MechanicMastery New(Guid childId, string mechanic, DateTime now) => new()
    {
        ChildProfileId = childId,
        Mechanic = mechanic,
        EstimatedLevel = MechanicMastery.StartingLevel,
        UpdatedAt = now
    };
}
