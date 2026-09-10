using PetPal.Api.Common;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>Keçmiş bir təcrübə — yeniliyi hesablamaq üçün. Sıra: ƏN YENİ birincidir.</summary>
public sealed record RunHistoryEntry(string TemplateKey, string Theme, PetBrainRunStatus Status);

/// <summary>
/// Direktorun bütün girişləri. Serverdə yığılır — klient buraya heç nə əlavə edə bilmir.
/// </summary>
public sealed record PetBrainDirectorContext(
    Guid ChildId,
    int Age,
    string Language,
    IReadOnlyDictionary<string, int> Interests,
    IReadOnlyDictionary<string, int> PlayStyles,

    /// <summary>Ən yenidən köhnəyə doğru sıralanmış son runlar.</summary>
    IReadOnlyList<RunHistoryEntry> RecentRuns,

    /// <summary>Tamamlanmış şablonların açarları — kosmetik təkrar açılmasın deyə.</summary>
    IReadOnlySet<string> CompletedTemplates,

    PetBrainDifficulty Difficulty,
    bool PetIsHatched);

/// <summary>Bir namizədin bal kartı — nümayiş panelində olduğu kimi göstərilir.</summary>
public sealed record DirectorCandidate(
    ExperienceTemplate Template,
    int FitScore,
    int NoveltyScore,
    int SurpriseScore,
    double TotalScore);

/// <summary>
/// Adaptive Pet Director — növbəti təcrübəni seçən DETERMİNİST qərar qatı.
///
/// <para>Bal üç hissədən yığılır:</para>
/// <list type="bullet">
///   <item><b>~70% uyğunluq</b> — uşağın maraq və üslub balları.</item>
///   <item><b>~20% yenilik</b> — son vaxt oynanan şablon və mövzu cəzalanır.</item>
///   <item><b>~10% məhdud sürpriz</b> — determinist, ±4 bal enində.</item>
/// </list>
///
/// <para>Sürprizin enliliyi qəsdən kiçikdir: uşağın AYDIN üstünlüyü təsadüfi
/// bir rəqəmlə pozulmamalıdır. O, yalnız yaxın balları olan namizədlər
/// arasında növbələşmə yaradır — yəni eyni profil hər dəfə eyni macərəni
/// almır, amma kosmosu sevən uşağa da birdən rəqs dərsi təklif olunmur.</para>
///
/// <para>Sinif SAFDIR: I/O yoxdur, <c>DateTime.UtcNow</c> yoxdur, təsadüfi
/// generator yoxdur. Eyni kontekst həmişə eyni qərarı verir.</para>
/// </summary>
public static class AdaptivePetDirector
{
    public const double FitWeight = 0.70;
    public const double NoveltyWeight = 0.20;

    /// <summary>Sürpriz payının yarım eni — yekun bala ən çox ±4 təsir edir.</summary>
    public const double SurpriseHalfBand = 4.0;

    /// <summary>Eyni ŞABLONUN təkrarına cəza (mövqeyə görə azalır).</summary>
    private static readonly int[] TemplatePenalty = [60, 40, 25, 12, 6];

    /// <summary>
    /// Eyni MÖVZUNUN təkrarına cəza. Şablon cəzasından qəsdən çox kiçikdir:
    /// "bu yaxınlarda Ay macərası oldu" fikri Ayı geri itələməlidir, kosmosu
    /// bütövlükdə yox — uşağın əsas marağı bir oyunla söndürülmür.
    /// </summary>
    private static readonly int[] ThemePenalty = [10, 6, 3];

    /// <summary>Yeniliyin baxdığı pəncərə — bundan köhnə runlar cəza vermir.</summary>
    private const int RecencyWindow = 5;

    /// <summary>Tövsiyə üçün namizəd qalmayanda <c>null</c> qayıdır (məsələn yumurta halı).</summary>
    public static PetBrainRecommendation? Decide(PetBrainDirectorContext context)
    {
        var candidates = Rank(context);
        if (candidates.Count == 0)
            return null;

        var winner = candidates[0];

        return new PetBrainRecommendation(
            Template: winner.Template,
            Difficulty: context.Difficulty,
            Reasons: BuildReasons(winner, context),
            Candidates: candidates,
            AlreadyCompleted: context.CompletedTemplates.Contains(winner.Template.Key));
    }

    /// <summary>
    /// Bütün namizədləri bala görə sıralayır. Bərabərlikdə açarın əlifba sırası
    /// həll edir — yəni nəticə prosesdən-prosesə eynidir.
    /// </summary>
    public static IReadOnlyList<DirectorCandidate> Rank(PetBrainDirectorContext context)
    {
        // Yumurta macəraya çıxmır: pet hələ danışmır, seçim də etmir.
        if (!context.PetIsHatched)
            return [];

        List<DirectorCandidate> candidates = [];

        foreach (var template in ExperienceCatalog.Templates)
        {
            // Yaş həddi TƏHLÜKƏSİZLİK sərhədidir — keçilmir.
            if (context.Age < template.MinAge)
                continue;

            var fit = FitScore(template, context);
            var novelty = NoveltyScore(template, context.RecentRuns);
            var surprise = SurpriseScore(context.ChildId, template.Key);

            var total = (FitWeight * fit)
                        + (NoveltyWeight * novelty)
                        + (((surprise / 100.0) * 2 - 1) * SurpriseHalfBand);

            candidates.Add(new DirectorCandidate(template, fit, novelty, surprise, total));
        }

        return [.. candidates
            .OrderByDescending(c => c.TotalScore)
            .ThenBy(c => c.Template.Key, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Uyğunluq balı (0–100).
    ///
    /// <para>Əyri QABARIQDIR (kvadrat): 85 balla 60 bal arasındakı fərq xətti
    /// modeldə çox kiçik qalırdı və yenilik payı aydın üstünlüyü aşıra bilirdi.
    /// Qabarıq əyri "güclü maraq" ilə "keçər maraq" arasını açır.</para>
    /// </summary>
    public static int FitScore(ExperienceTemplate template, PetBrainDirectorContext context)
    {
        var primary = Score(context.Interests, template.PrimaryInterest);

        var secondary = template.InterestAffinity.Skip(1).ToList();
        var secondaryAverage = secondary.Count == 0
            ? primary
            : secondary.Average(key => Score(context.Interests, key));

        var interestFit = (0.65 * primary) + (0.35 * secondaryAverage);

        var styleFit = template.PlayStyleAffinity.Count == 0
            ? interestFit
            : template.PlayStyleAffinity.Average(key => (double)Score(context.PlayStyles, key));

        var raw = (0.72 * interestFit) + (0.28 * styleFit);

        return (int)Math.Round(raw * raw / 100.0, MidpointRounding.AwayFromZero);
    }

    /// <summary>Yenilik balı (0–100). Heç oynanmamış və mövzusu təzə olan şablon 100 alır.</summary>
    public static int NoveltyScore(ExperienceTemplate template, IReadOnlyList<RunHistoryEntry> recentRuns)
    {
        var penalty = 0;
        var position = 0;

        foreach (var run in recentRuns.Take(RecencyWindow))
        {
            if (string.Equals(run.TemplateKey, template.Key, StringComparison.Ordinal))
                penalty = Math.Max(penalty, At(TemplatePenalty, position));
            else if (string.Equals(run.Theme, template.Theme, StringComparison.Ordinal))
                penalty = Math.Max(penalty, At(ThemePenalty, position));

            position++;
        }

        return Math.Clamp(100 - penalty, 0, 100);
    }

    /// <summary>
    /// Məhdud sürpriz (0–100). Yalnız uşaq və şablondan asılıdır — TARİXDƏN YOX:
    /// nümayiş hansı gün göstərilməsindən asılı olmamalıdır.
    /// </summary>
    public static int SurpriseScore(Guid childId, string templateKey) =>
        (int)(StableHash.Unit($"petbrain-surprise:{childId:N}:{templateKey}") * 100);

    /// <summary>
    /// İki–dörd qısa izah. Bunlar hesablanmış faktlardır: bal cədvəlindən və
    /// run tarixçəsindən çıxır, modelin "düşüncəsi" deyil.
    /// </summary>
    private static List<string> BuildReasons(DirectorCandidate winner, PetBrainDirectorContext context)
    {
        var language = context.Language;
        List<string> reasons = [];

        var template = winner.Template;

        // 1) Ən güclü maraq.
        var primaryScore = Score(context.Interests, template.PrimaryInterest);
        var primaryLabel = TraitKeys.Label(template.PrimaryInterest, language);

        if (primaryScore >= 60)
            reasons.Add(Localized.T(language,
                $"{primaryLabel} sənin güclü maraqlarındandır",
                $"{primaryLabel} is one of your strongest interests"));
        else
            reasons.Add(Localized.T(language,
                $"{primaryLabel} mövzusunu birlikdə kəşf edək",
                $"Let us explore {primaryLabel.ToLowerInvariant()} together"));

        // 2) Ən güclü uyğun oyun üslubu.
        var topStyle = template.PlayStyleAffinity
            .OrderByDescending(key => Score(context.PlayStyles, key))
            .FirstOrDefault();

        if (topStyle is not null && Score(context.PlayStyles, topStyle) >= 55)
        {
            var styleLabel = TraitKeys.Label(topStyle, language);
            reasons.Add(Localized.T(language,
                $"Sən burada güclüsən: {styleLabel}",
                $"This is your strength: {styleLabel}"));
        }

        // 3) Yenilik — nə üçün eyni macərə deyil.
        var repeatedTheme = context.RecentRuns
            .Take(RecencyWindow)
            .FirstOrDefault(r => string.Equals(r.Theme, template.Theme, StringComparison.Ordinal)
                                 && !string.Equals(r.TemplateKey, template.Key, StringComparison.Ordinal));

        if (repeatedTheme is not null)
        {
            var previous = ExperienceCatalog.Find(repeatedTheme.TemplateKey);
            if (previous is not null)
                reasons.Add(Localized.T(language,
                    $"«{previous.Title(language)}» bu yaxınlarda oldu — bu dəfə yeni yer",
                    $"You played «{previous.Title(language)}» recently — somewhere new this time"));
        }
        else if (winner.NoveltyScore >= 100)
        {
            reasons.Add(Localized.T(language,
                "Bu, hələ görmədiyin bir macəradır",
                "This is an adventure you have not tried yet"));
        }

        // 4) Çətinlik — uşağa yox, münsifə/valideynə aydınlıq gətirir.
        reasons.Add(DifficultyReason(context.Difficulty, language));

        return [.. reasons.Take(4)];
    }

    private static string DifficultyReason(PetBrainDifficulty difficulty, string language) => difficulty switch
    {
        PetBrainDifficulty.Easy => Localized.T(language,
            "Bu dəfə daha rahat gedək", "Let us take it gently this time"),
        PetBrainDifficulty.Hard => Localized.T(language,
            "Sən hazırsan — bir az çətinləşdirdim", "You are ready — I made it a little harder"),
        _ => Localized.T(language,
            "Çətinlik sənin son nəticənə uyğundur", "The challenge matches how you did last time")
    };

    private static int Score(IReadOnlyDictionary<string, int> scores, string key) =>
        scores.TryGetValue(key, out var value) ? TraitKeys.Clamp(value) : TraitKeys.StartingScore;

    private static int At(int[] table, int index) =>
        index < table.Length ? table[index] : 0;
}

/// <summary>Direktorun qərarı — servis qatı bunu DTO-ya çevirir.</summary>
public sealed record PetBrainRecommendation(
    ExperienceTemplate Template,
    PetBrainDifficulty Difficulty,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<DirectorCandidate> Candidates,
    bool AlreadyCompleted);
