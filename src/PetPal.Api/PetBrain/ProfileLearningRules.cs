using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Bir hadisənin profilə təsiri: hansı açar, neçə bal, HARADAN.
///
/// <para>Mənbə sübut modelinə düşür (bax <see cref="TraitEvidence"/>): eyni
/// bal müxtəlif mənbələrdən gələndə daha etibarlıdır, çünki bir mövzunu həm
/// macərada, həm mini oyunda, həm də dərsdə seçmək təsadüf deyil.</para>
/// </summary>
public sealed record TraitAdjustment(
    PetBrainTraitCategory Category,
    string Key,
    int Delta,
    PetBrainEvidenceSource Source = PetBrainEvidenceSource.Unknown);

/// <summary>
/// Profilin TƏDRİCƏN öyrənmə qaydaları — saf funksiyalar, I/O yoxdur.
///
/// <para>Cədvəl bir yerdədir ki, balans dəyişikliyi endpoint-lərə səpələnməsin.
/// Dörd qayda bütün dizaynı müəyyən edir:</para>
///
/// <list type="number">
///   <item>Bir hərəkət beş baldan çox dəyişmir (<see cref="TraitKeys.MaxDeltaPerAction"/>).</item>
///   <item>Səhv cavab MARAĞI azaltmır — bacarmamaq sevməmək deyil.</item>
///   <item>İpucu istəmək heç nəyi azaltmır — kömək istəmək cəzalandırılmır.</item>
///   <item>Yalnız MƏNALI yarımçıq qoyma (ən azı bir mərhələ keçilib) marağı azaldır.</item>
/// </list>
/// </summary>
public static class ProfileLearningRules
{
    /// <summary>Təcrübə seçiləndə əsas maraq bu qədər artır.</summary>
    public const int SelectedInterest = 1;

    /// <summary>Tamamlanmış təcrübə əsas marağı bu qədər qaldırır.</summary>
    public const int CompletedPrimaryInterest = 3;

    /// <summary>Tamamlanmış təcrübə uyğun oyun üslubunu bu qədər qaldırır.</summary>
    public const int CompletedPlayStyle = 2;

    /// <summary>İkinci dərəcəli maraqlar (kataloqdakı sıradan sonrakılar).</summary>
    public const int CompletedSecondaryInterest = 1;

    /// <summary>Mənalı yarımçıq qoyma əsas marağı bu qədər azaldır.</summary>
    public const int AbandonedPrimaryInterest = -2;

    /// <summary>Təsdiqlənmiş tapmaca həlli.</summary>
    public const int PuzzleSolvedPuzzles = 2;
    public const int PuzzleSolvedProblemSolver = 1;

    /// <summary>Mini oyun və dərs kimi mövcud axınlardan gələn zəif siqnallar.</summary>
    public const int WeakSignal = 1;

    /// <summary>
    /// Eyni şablonun təkrar tamamlanması bu qədər verir — ilk dəfə ilə eyni
    /// olsaydı, uşaq bir macərəni dövrə vurub profili şişirdə bilərdi.
    /// </summary>
    public const int RepeatCompletionInterest = 1;

    /// <summary>Bir gündə bir açarın qazana biləcəyi ƏN BÖYÜK cəm — təkrar becərməyə qarşı.</summary>
    public const int DailyGainCapPerKey = 6;

    /// <summary>
    /// Uşaq macərəni BAŞLATDI.
    ///
    /// <para>Bu, kartın GÖRÜNMƏSİ deyil — düyməyə basmaqdır, yəni uşağın öz
    /// hərəkəti. Göstərilmə (<c>RecommendationViewed</c>) heç bir xassəni
    /// artırmır və onun düzəliş siyahısı qəsdən boşdur.</para>
    /// </summary>
    public static IReadOnlyList<TraitAdjustment> ForSelection(ExperienceTemplate template) =>
        [Interest(template.PrimaryInterest, SelectedInterest, PetBrainEvidenceSource.Adventure)];

    /// <summary>
    /// Təcrübə tamamlandı. Əsas maraq +3, uyğun üslublar +2, ikinci maraqlar +1.
    /// Təkrar tamamlamada rəqəmlər kiçilir.
    /// </summary>
    public static IReadOnlyList<TraitAdjustment> ForCompletion(ExperienceTemplate template, bool isFirstCompletion)
    {
        List<TraitAdjustment> adjustments = [];

        var primary = isFirstCompletion ? CompletedPrimaryInterest : RepeatCompletionInterest;
        adjustments.Add(Interest(template.PrimaryInterest, primary, PetBrainEvidenceSource.Adventure));

        if (isFirstCompletion)
        {
            foreach (var interest in template.InterestAffinity.Skip(1))
                adjustments.Add(Interest(interest, CompletedSecondaryInterest, PetBrainEvidenceSource.Adventure));

            foreach (var style in template.PlayStyleAffinity)
                adjustments.Add(PlayStyle(style, CompletedPlayStyle, PetBrainEvidenceSource.Adventure));
        }
        else
        {
            foreach (var style in template.PlayStyleAffinity.Take(1))
                adjustments.Add(PlayStyle(style, RepeatCompletionInterest, PetBrainEvidenceSource.Adventure));
        }

        return adjustments;
    }

    /// <summary>
    /// Mənalı yarımçıq qoyma. "Mənalı" = uşaq ən azı bir mərhələ keçib: giriş
    /// ekranından dərhal çıxmaq fikir bildirmək deyil, sadəcə səhv toxunuşdur.
    /// </summary>
    public static IReadOnlyList<TraitAdjustment> ForAbandon(ExperienceTemplate template, int completedStages) =>
        completedStages >= 1
            ? [Interest(template.PrimaryInterest, AbandonedPrimaryInterest, PetBrainEvidenceSource.Adventure)]
            : [];

    /// <summary>Kataloqda yazılmış seçim təsiri — açarlar orada təsdiqlənib.</summary>
    public static IReadOnlyList<TraitAdjustment> ForChoice(ExperienceOption option)
    {
        List<TraitAdjustment> adjustments = [];

        foreach (var delta in option.Traits)
        {
            var category = TraitKeys.CategoryOf(delta.Key);
            if (category is null)
                continue;

            adjustments.Add(new TraitAdjustment(
                category.Value, delta.Key, Cap(delta.Delta), PetBrainEvidenceSource.Choice));
        }

        return adjustments;
    }

    /// <summary>
    /// Təsdiqlənmiş tapmaca həlli. Səhv cavab üçün QARŞILIĞI YOXDUR: bir səhv
    /// nə marağı, nə də üslubu azaltmır.
    /// </summary>
    public static IReadOnlyList<TraitAdjustment> ForPuzzleSolved() =>
    [
        Interest(TraitKeys.Puzzles, PuzzleSolvedPuzzles, PetBrainEvidenceSource.Puzzle),
        PlayStyle(TraitKeys.ProblemSolver, PuzzleSolvedProblemSolver, PetBrainEvidenceSource.Puzzle)
    ];

    /// <summary>
    /// Mövcud axınlardan gələn zəif siqnallar (dərs cavabı, mini oyun, qulluq).
    /// Bunlar Pet Brain-in öz hadisələrindən qəsdən zəifdir: onlar birbaşa
    /// seçimdir, bunlar isə dolayı işarədir.
    /// </summary>
    public static IReadOnlyList<TraitAdjustment> ForSkillAnswer(SkillArea skill, bool isCorrect)
    {
        // Səhv cavab heç nə dəyişmir — bacarmamaq sevməmək deyil.
        if (!isCorrect)
            return [];

        return skill switch
        {
            SkillArea.Science => [Interest(TraitKeys.Science, WeakSignal, PetBrainEvidenceSource.Learning)],
            SkillArea.Logic => [Interest(TraitKeys.Puzzles, WeakSignal, PetBrainEvidenceSource.Learning),
                PlayStyle(TraitKeys.ProblemSolver, WeakSignal, PetBrainEvidenceSource.Learning)],
            SkillArea.Math => [PlayStyle(TraitKeys.ProblemSolver, WeakSignal, PetBrainEvidenceSource.Learning)],
            SkillArea.Reading => [Interest(TraitKeys.Stories, WeakSignal, PetBrainEvidenceSource.Learning)],
            SkillArea.Vocabulary => [Interest(TraitKeys.Stories, WeakSignal, PetBrainEvidenceSource.Learning)],
            _ => []
        };
    }

    /// <summary>Pet-ə qulluq — qayğıkeşlik üslubunun yeganə mənbəyi.</summary>
    public static IReadOnlyList<TraitAdjustment> ForCare() =>
        [PlayStyle(TraitKeys.Caring, WeakSignal, PetBrainEvidenceSource.Care)];

    /// <summary>Şkafda görünüş qurmaq yaradıcı üslubun zəif işarəsidir.</summary>
    public static IReadOnlyList<TraitAdjustment> ForAccessoryEquipped() =>
        [PlayStyle(TraitKeys.Creative, WeakSignal, PetBrainEvidenceSource.Cosmetic)];

    /// <summary>
    /// Mini oyun. Şənlik üslubu HƏMİŞƏ artır, amma oyunun AİLƏSİ də sayılır.
    ///
    /// <para>Əvvəl hər oyun eyni «playful +1» verirdi, ona görə yaddaş oyununu
    /// yenidən-yenidən seçən uşaq ilə reflekslə oynayan uşaq eyni görünürdü.
    /// İndi ailə ikinci, DAHA ZƏİF siqnal əlavə edir: yaddaş və sıra oyunları
    /// tapmaca marağına, ritm və rəng oyunları yaradıcılığa, hərəkət oyunları
    /// kəşfiyyatçılığa toxunur.</para>
    ///
    /// <para>Siqnal qəsdən zəifdir: mini oyun MÖVZU seçimi deyil — uşaq orada
    /// «kosmos» yox, «əylən» seçir.</para>
    /// </summary>
    public static IReadOnlyList<TraitAdjustment> ForMiniGame(string? gameKey = null)
    {
        List<TraitAdjustment> adjustments =
            [PlayStyle(TraitKeys.Playful, WeakSignal, PetBrainEvidenceSource.MiniGame)];

        var family = MiniGameFamilies.FamilyOf(gameKey);

        if (family is { } second)
            adjustments.Add(new TraitAdjustment(
                second.Category, second.Key, WeakSignal, PetBrainEvidenceSource.MiniGame));

        return adjustments;
    }

    private static TraitAdjustment Interest(
        string key, int delta, PetBrainEvidenceSource source = PetBrainEvidenceSource.Unknown) =>
        new(PetBrainTraitCategory.Interest, key, Cap(delta), source);

    private static TraitAdjustment PlayStyle(
        string key, int delta, PetBrainEvidenceSource source = PetBrainEvidenceSource.Unknown) =>
        new(PetBrainTraitCategory.PlayStyle, key, Cap(delta), source);

    /// <summary>Cədvəldə səhvən böyük rəqəm yazılsa belə profil sıçramır.</summary>
    private static int Cap(int delta) =>
        Math.Clamp(delta, -TraitKeys.MaxDeltaPerAction, TraitKeys.MaxDeltaPerAction);
}
