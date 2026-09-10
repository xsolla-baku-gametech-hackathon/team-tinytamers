using PetPal.Api.PetBrain.Mind;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Recommendation;

/// <summary>
/// Tövsiyə siyasəti — <b>iki mərhələli, saf və determinist</b>.
///
/// <list type="number">
///   <item><b>Sərt şərtlər.</b> Yaş, valideyn bloku, ekran vaxtı, soyuma.
///   Bunlar bal məsələsi deyil: keçməyən namizəd sıralamaya heç
///   girmir.</item>
///   <item><b>Sıralama.</b> Hər namizəd üçün İZAH EDİLƏ BİLƏN bal kartı —
///   komponentlər ayrı qalır ki, «niyə bu?» sualının cavabı hesablanmış
///   olsun, sonradan uydurulmasın.</item>
/// </list>
///
/// <para><b>Nəticə bir məcburi macəra deyil.</b> Siyahı fərqli ROLLAR
/// daşıyır: ən uyğun, davam, yaxın kəşf, təhlükəsiz sürpriz. Üç «ən uyğun»
/// kart filter bubble-dır və uşağı öz keçmişinə kilidləyir.</para>
///
/// <para><b>Sinif safdır:</b> I/O yoxdur, <c>DateTime.UtcNow</c> yoxdur,
/// təsadüfi generator yoxdur. Eyni kontekst + eyni seed + eyni
/// <c>PolicyVersion</c> həmişə eyni nəticəni verir — testlər buna görə
/// stabildir.</para>
/// </summary>
public static class RecommendationPolicy
{
    /// <summary>Ustalıq məlumatı olmayanda neytral-müsbət dəyər.</summary>
    public const int UnknownMasteryFit = 70;

    /// <summary>Dəstək ehtiyacı bilinməyəndə neytral dəyər.</summary>
    public const int NeutralSupportFit = 70;

    /// <summary>Bir pillə fərqin çətinlik uyğunluğuna dəyəri.</summary>
    public const int ChallengeStepPenalty = 35;

    /// <summary>Bir addım fərqin temp uyğunluğuna dəyəri.</summary>
    public const int PaceStepPenalty = 18;

    /// <summary>Mövzu balının qabarıqlığı — güclü maraq keçər maraqdan ayrılsın.</summary>
    public const double TopicCurve = 100.0;

    /// <summary>Yenilik cəzasının pəncərəsi (son run sayı).</summary>
    public const int NoveltyWindow = 5;

    private static readonly int[] TemplateRepeatPenalty = [60, 40, 25, 12, 6];
    private static readonly int[] ThemeRepeatPenalty = [10, 6, 3];

    /// <summary>
    /// Bir baxışın tam nəticəsi.
    /// </summary>
    /// <param name="mind">Ortaq kontekst — bütün ekranların oxuduğu həqiqət.</param>
    /// <param name="options">Çəkilər və kəşf payı; versiyalanır.</param>
    /// <param name="seed">
    /// Determinizmin toxumu. Eyni kontekst üçün eyni qalmalıdır — kontekst
    /// hash-ı bu işi görür, ona görə nəticə saatdan və proses ömründən asılı
    /// olmur.
    /// </param>
    public static RecommendationSet Decide(
        PetMindContext mind, RecommendationPolicyOptions options, string seed)
    {
        List<FilteredCandidate> filtered = [];
        var eligible = Filter(mind, options, filtered, relaxed: false);

        // Hər şey süzülübsə süzgəc YUMŞALDILIR. Uşağa «sənə heç nə təklif
        // etmirəm» ekranı göstərmək ən pis nəticədir; təhlükəsizlik şərtləri
        // (yaş, valideyn bloku) isə yumşalmada da qalır.
        if (eligible.Count == 0)
        {
            filtered.Clear();
            eligible = Filter(mind, options, filtered, relaxed: true);
        }

        if (eligible.Count == 0)
            return new RecommendationSet(
                [], [], filtered, mind.Difficulty, options.PolicyVersion, mind.ProfileConfidence);

        var ranked = eligible
            .Select(t => Score(t, mind, options))
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .ToList();

        var cards = Compose(ranked, mind, options, seed);

        return new RecommendationSet(
            cards, ranked, filtered, mind.Difficulty, options.PolicyVersion, mind.ProfileConfidence);
    }

    // ==================== Mərhələ A: sərt şərtlər ====================

    /// <summary>
    /// Sərt şərtlər. <paramref name="relaxed"/> yalnız YUMŞAQ şərtləri
    /// buraxır — yaş həddi və valideyn bloku heç vaxt yumşalmır.
    /// </summary>
    private static List<ExperienceTemplate> Filter(
        PetMindContext mind,
        RecommendationPolicyOptions options,
        List<FilteredCandidate> filtered,
        bool relaxed)
    {
        List<ExperienceTemplate> eligible = [];

        // Yumurta macəraya çıxmır: pet hələ danışmır, seçim də etmir.
        if (!mind.PetIsHatched)
        {
            foreach (var template in ExperienceCatalog.Templates)
                filtered.Add(new FilteredCandidate(template.Key, PetBrainFilterReason.PetNotHatched));

            return eligible;
        }

        foreach (var template in ExperienceCatalog.Templates)
        {
            var reason = ReasonToSkip(template, mind, options, relaxed);

            if (reason == PetBrainFilterReason.None)
                eligible.Add(template);
            else
                filtered.Add(new FilteredCandidate(template.Key, reason));
        }

        return eligible;
    }

    private static PetBrainFilterReason ReasonToSkip(
        ExperienceTemplate template,
        PetMindContext mind,
        RecommendationPolicyOptions options,
        bool relaxed)
    {
        // ---- Yumşalmayan şərtlər ----

        if (mind.AgeForSafetyLimits < template.MinAge)
            return PetBrainFilterReason.AgeGate;

        if (mind.BlockedTemplates.Contains(template.Key) || mind.BlockedThemes.Contains(template.Theme))
            return PetBrainFilterReason.ParentBlocked;

        if (relaxed)
            return PetBrainFilterReason.None;

        // ---- Yumşalan şərtlər ----

        if (mind.ShowLessTemplates.Contains(template.Key) || mind.ShowLessThemes.Contains(template.Theme))
            return PetBrainFilterReason.ShowLessCooldown;

        if (mind.DeclinedTemplates.Contains(template.Key))
            return PetBrainFilterReason.DeclinedThisSession;

        // Bu yaxınlarda oynanmış macəra SÜZÜLMÜR, yalnız yenilik balında ağır
        // cəza alır (bax NoveltyValue: birinci mövqe üçün 60 bal).
        //
        // Sərt süzgəc cazibədar görünürdü, amma öz qaydamızı pozurdu: təkrar
        // oynamaq ən güclü müsbət siqnaldır və uşağa sevdiyi macəraya
        // qayıtmağı qadağan etsək, o siqnalı heç vaxt görə bilmərik.

        if (mind.ScreenTime == PetBrainScreenTimeBand.Ending
            && template.TargetMinutes > options.ShortSessionMaxMinutes)
            return PetBrainFilterReason.TooLongForRemainingTime;

        return PetBrainFilterReason.None;
    }

    // ==================== Mərhələ B: sıralama ====================

    /// <summary>Bir namizədin izah edilə bilən bal kartı.</summary>
    public static CandidateScore Score(
        ExperienceTemplate template, PetMindContext mind, RecommendationPolicyOptions options)
    {
        var topic = TopicFit(template, mind);
        var mechanic = MechanicFit(template, mind);
        var mastery = MasteryChallengeFit(template, mind);
        var style = StyleFit(template, mind);
        var support = SupportFit(template, mind);
        var pace = PaceFit(template, mind);
        var continuity = ContinuityFit(template, mind);
        var reward = RewardFit(template, mind);
        var novelty = NoveltyValue(template, mind);
        var repetition = 100 - novelty;
        var explicitAdjustment = ExplicitAdjustment(template, mind);

        var weighted =
            (options.TopicFit * topic)
            + (options.MechanicFit * mechanic)
            + (options.MasteryChallengeFit * mastery)
            + (options.StyleFit * style)
            + (options.SupportFit * support)
            + (options.PaceFit * pace)
            + (options.ContinuityFit * continuity)
            + (options.RewardFit * reward)
            + (options.NoveltyValue * novelty);

        var total = Math.Clamp(weighted + explicitAdjustment, 0, 100);

        return new CandidateScore(
            template, topic, mechanic, mastery, style, support, pace, continuity, reward,
            novelty, repetition, explicitAdjustment, total,
            WhyFor(template, mind, topic, mechanic, novelty, continuity, support));
    }

    /// <summary>
    /// Mövzu uyğunluğu. Əyri QABARIQDIR: xətti modeldə 85 ilə 60 arasındakı
    /// fərq itirdi və yenilik payı aydın üstünlüyü aşıra bilirdi.
    /// </summary>
    public static int TopicFit(ExperienceTemplate template, PetMindContext mind)
    {
        var primary = Interest(mind, template.PrimaryInterest);

        var secondary = template.InterestAffinity.Skip(1).ToList();
        var secondaryAverage = secondary.Count == 0
            ? primary
            : secondary.Average(key => (double)Interest(mind, key));

        var raw = (0.65 * primary) + (0.35 * secondaryAverage);

        return (int)Math.Round(raw * raw / TopicCurve, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Mexanika uyğunluğu — mövzudan AYRI.
    ///
    /// <para>Kataloqda mexanika elan edilməyibsə neytral qaytarılır: heç bir
    /// namizəd məlumatsızlıq üzündən irəli çıxmamalıdır.</para>
    /// </summary>
    public static int MechanicFit(ExperienceTemplate template, PetMindContext mind)
    {
        if (template.MechanicAffinity.Count == 0)
            return TraitKeys.StartingScore;

        return (int)Math.Round(
            template.MechanicAffinity.Average(key => (double)Mechanic(mind, key)),
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Çətinliyin uşağın USTALIĞINA uyğunluğu.
    ///
    /// <para>Ustalıq maraqdan ayrıdır: uşaq marşrutu sevə, amma hələ bacarmaya
    /// bilər. Ölçü «bu macəra bu uşağı DOĞRU pillədə çətinləşdirirmi» sualına
    /// cavab verir — nə darıxdırıcı, nə də əlçatmaz.</para>
    /// </summary>
    public static int MasteryChallengeFit(ExperienceTemplate template, PetMindContext mind)
    {
        if (template.MechanicAffinity.Count == 0)
            return UnknownMasteryFit;

        List<int> fits = [];

        foreach (var key in template.MechanicAffinity)
        {
            if (!mind.MechanicChallengeBand.TryGetValue(key, out var band))
            {
                fits.Add(UnknownMasteryFit);
                continue;
            }

            var distance = Math.Abs((int)band - (int)mind.Difficulty);
            fits.Add(Math.Clamp(100 - (distance * ChallengeStepPenalty), 0, 100));
        }

        return (int)Math.Round(fits.Average(), MidpointRounding.AwayFromZero);
    }

    public static int StyleFit(ExperienceTemplate template, PetMindContext mind) =>
        template.PlayStyleAffinity.Count == 0
            ? TraitKeys.StartingScore
            : (int)Math.Round(
                template.PlayStyleAffinity.Average(key => (double)PlayStyle(mind, key)),
                MidpointRounding.AwayFromZero);

    /// <summary>
    /// Uşağın ehtiyac duyduğu DƏSTƏK bu macərada varmı.
    ///
    /// <para>Dəstək ehtiyacı olmayanda ölçü neytraldır — yəni heç kim
    /// «kömək lazım deyil» deyə cəzalandırılmır.</para>
    /// </summary>
    public static int SupportFit(ExperienceTemplate template, PetMindContext mind)
    {
        var support = mind.Personalization.Support;

        var needsSupport = support.Timing != PetBrainHintTiming.OnRequest
                           || support.ReducedOptions
                           || support.DemonstrationFirst
                           || support.ExtraResponseTime;

        if (!needsSupport)
            return NeutralSupportFit;

        // Yaradıcı macərada doğru/səhv yoxdur — dəstəyə ehtiyacı olan uşaq
        // üçün ən təzyiqsiz yoldur.
        if (template.Type == PetBrainExperienceType.Creative)
            return 100;

        // Tapmacası olan macərada ipucu MÖVCUDDUR; olmayanda dəstək yeri azdır.
        return template.HasPuzzle ? 85 : 75;
    }

    /// <summary>Sessiya uzunluğu ilə macəranın addım sayının uyğunluğu.</summary>
    public static int PaceFit(ExperienceTemplate template, PetMindContext mind)
    {
        var budget = mind.Personalization.PreferredStepBudget;
        var distance = Math.Abs(template.StageCount - budget);

        return Math.Clamp(100 - (distance * PaceStepPenalty), 0, 100);
    }

    /// <summary>
    /// Bu macəra uşağın SON etdiyi işin davamı kimi görünürmü.
    ///
    /// <para>Yarımçıq run ayrıca ekranda göstərilir, ona görə burada söhbət
    /// «keçən dəfəki dünyaya qayıtmaq»dan gedir: eyni mövzu, ya da eyni
    /// mexanika.</para>
    /// </summary>
    public static int ContinuityFit(ExperienceTemplate template, PetMindContext mind)
    {
        if (string.Equals(mind.UnfinishedTemplateKey, template.Key, StringComparison.Ordinal))
            return 100;

        var last = mind.RecentOutcomes.FirstOrDefault();

        if (last is null)
            return TraitKeys.StartingScore;

        if (string.Equals(last.Theme, template.Theme, StringComparison.Ordinal))
            return 80;

        var previous = ExperienceCatalog.Find(last.TemplateKey);

        if (previous is not null && previous.MechanicAffinity.Intersect(
                template.MechanicAffinity, StringComparer.Ordinal).Any())
            return 65;

        return 25;
    }

    /// <summary>Macəranın mükafat forması uşağın seçdiyi ilə üst-üstə düşürmü.</summary>
    public static int RewardFit(ExperienceTemplate template, PetMindContext mind) =>
        template.RewardFlavor == mind.Personalization.RewardPreference ? 100 : 45;

    /// <summary>Yenilik (0–100). Heç oynanmamış və mövzusu təzə olan şablon 100 alır.</summary>
    public static int NoveltyValue(ExperienceTemplate template, PetMindContext mind)
    {
        var penalty = 0;
        var position = 0;

        foreach (var run in mind.RecentOutcomes.Take(NoveltyWindow))
        {
            if (string.Equals(run.TemplateKey, template.Key, StringComparison.Ordinal))
                penalty = Math.Max(penalty, At(TemplateRepeatPenalty, position));
            else if (string.Equals(run.Theme, template.Theme, StringComparison.Ordinal))
                penalty = Math.Max(penalty, At(ThemeRepeatPenalty, position));

            position++;
        }

        return Math.Clamp(100 - penalty, 0, 100);
    }

    /// <summary>
    /// AÇIQ seçimin düzəlişi. Bəyənmə irəli çəkir, «daha az göstər» geri —
    /// ikincisi yalnız süzgəc yumşalanda bura çatır.
    /// </summary>
    public static int ExplicitAdjustment(ExperienceTemplate template, PetMindContext mind)
    {
        var adjustment = 0;

        if (mind.LikedTemplates.Contains(template.Key) || mind.LikedThemes.Contains(template.Theme))
            adjustment += ContentPreferenceRules.LikeRankBonus;

        if (mind.ShowLessTemplates.Contains(template.Key) || mind.ShowLessThemes.Contains(template.Theme))
            adjustment -= ContentPreferenceRules.ShowLessRankPenalty;

        return adjustment;
    }

    // ==================== Mərhələ C: kartların yığılması ====================

    /// <summary>
    /// Kartların yığılması: bir əsas, bir davam, bir kəşf.
    ///
    /// <para><b>Kəşf determinist seçilir</b>, təsadüfi deyil: eyni kontekst
    /// eyni nəticəni verməlidir, yoxsa nə test stabil olar, nə də «niyə bunu
    /// gördüm?» sualının cavabı.</para>
    ///
    /// <para><b>Müxtəliflik qorunur:</b> mümkün olduqda eyni mövzudan iki kart
    /// göstərilmir — üç kosmos kartı seçim deyil, təkrardır.</para>
    /// </summary>
    private static List<RecommendationCard> Compose(
        IReadOnlyList<CandidateScore> ranked,
        PetMindContext mind,
        RecommendationPolicyOptions options,
        string seed)
    {
        List<RecommendationCard> cards = [];
        HashSet<string> used = new(StringComparer.Ordinal);
        HashSet<string> usedThemes = new(StringComparer.Ordinal);

        var exploring = ShouldExplore(mind, options, seed);

        var primary = exploring
            ? PickExploration(ranked, mind) ?? ranked[0]
            : ranked[0];

        Add(primary, PetBrainRecommendationSlot.Primary, exploring && primary != ranked[0]);

        if (cards.Count < options.CardCount && PickContinuity(ranked, mind, used) is { } continuity)
            Add(continuity, PetBrainRecommendationSlot.Continuity, wasExploration: false);

        if (cards.Count < options.CardCount && PickNearby(ranked, mind, used, usedThemes) is { } nearby)
            Add(nearby, PetBrainRecommendationSlot.NearbyDiscovery, wasExploration: true);

        if (cards.Count < options.CardCount
            && mind.Personalization.SurpriseEnabled
            && PickSurprise(ranked, used, usedThemes) is { } surprise)
            Add(surprise, PetBrainRecommendationSlot.SafeExploration, wasExploration: true);

        // Boşluq qalıbsa bal sırası ilə doldurulur — uşaq həmişə seçim görməlidir.
        foreach (var candidate in ranked)
        {
            if (cards.Count >= options.CardCount)
                break;

            if (used.Contains(candidate.Key))
                continue;

            Add(candidate, PetBrainRecommendationSlot.NearbyDiscovery, wasExploration: false);
        }

        return cards;

        void Add(CandidateScore candidate, PetBrainRecommendationSlot slot, bool wasExploration)
        {
            cards.Add(new RecommendationCard(candidate, slot, wasExploration));
            used.Add(candidate.Key);
            usedThemes.Add(candidate.Theme);
        }
    }

    /// <summary>
    /// Bu baxışda kəşf payı işə düşürmü.
    ///
    /// <para>Determinist: uşaq, siyasət versiyası və kontekst hash-ından
    /// hesablanır. Aşağı inamda pay ARTIR — sistem az şey biləndə daha çox
    /// soruşmalıdır, daha inadkar olmamalıdır.</para>
    /// </summary>
    public static bool ShouldExplore(
        PetMindContext mind, RecommendationPolicyOptions options, string seed)
    {
        var share = mind.Personalization.ExplorationShare;

        // Profil zəif tanınırsa kəşf payı artır — amma heç vaxt yarıdan çox olmur.
        if (mind.ProfileConfidence < 30)
            share = Math.Min(0.5, share + 0.15);

        var roll = StableHash.Unit($"petbrain-explore:{mind.ChildId:N}:{options.PolicyVersion}:{seed}");

        return roll < share;
    }

    private static CandidateScore? PickExploration(IReadOnlyList<CandidateScore> ranked, PetMindContext mind)
    {
        if (!mind.Personalization.SurpriseEnabled)
            return null;

        // Ən yüksək yenilik, amma sıranın ən altından deyil: uşağa "sürpriz"
        // adı ilə ən uyğunsuz macərəni vermək kəşf deyil, cəzadır.
        var pool = ranked.Take(Math.Max(2, ranked.Count - 1)).ToList();

        return pool
            .OrderByDescending(c => c.NoveltyValue)
            .ThenByDescending(c => c.Total)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static CandidateScore? PickContinuity(
        IReadOnlyList<CandidateScore> ranked, PetMindContext mind, IReadOnlySet<string> used) =>
        ranked
            .Where(c => !used.Contains(c.Key) && c.ContinuityFit >= 65)
            .OrderByDescending(c => c.ContinuityFit)
            .ThenByDescending(c => c.Total)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// Yaxın qonşu: tanış MEXANİKA, amma başqa mövzu — «sevdiyin qurma
    /// oyununun yeni növü».
    /// </summary>
    private static CandidateScore? PickNearby(
        IReadOnlyList<CandidateScore> ranked,
        PetMindContext mind,
        IReadOnlySet<string> used,
        IReadOnlySet<string> usedThemes) =>
        ranked
            .Where(c => !used.Contains(c.Key) && !usedThemes.Contains(c.Theme))
            .OrderByDescending(c => c.MechanicFit)
            .ThenByDescending(c => c.NoveltyValue)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>Təhlükəsiz sürpriz: ən təzə mövzu, yaş və blok şərtlərindən keçmiş.</summary>
    private static CandidateScore? PickSurprise(
        IReadOnlyList<CandidateScore> ranked,
        IReadOnlySet<string> used,
        IReadOnlySet<string> usedThemes) =>
        ranked
            .Where(c => !used.Contains(c.Key) && !usedThemes.Contains(c.Theme))
            .OrderByDescending(c => c.NoveltyValue)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .FirstOrDefault();

    // ==================== İzah ====================

    /// <summary>
    /// «Niyə bunu göstərirəm?» — SƏBƏB KODLARI.
    ///
    /// <para>Cümlə deyil, kod qaytarılır: uşağın dilində mətn ondan qurulur,
    /// jurnalda isə sərbəst mətn qalmır və tərcümə köhnəlmir.</para>
    /// </summary>
    public static IReadOnlyList<PetBrainWhyReason> WhyFor(
        ExperienceTemplate template,
        PetMindContext mind,
        int topic,
        int mechanic,
        int novelty,
        int continuity,
        int support)
    {
        List<PetBrainWhyReason> reasons = [];

        if (mind.LikedTemplates.Contains(template.Key) || mind.LikedThemes.Contains(template.Theme))
            reasons.Add(PetBrainWhyReason.StrongTopic);
        else if (topic >= 60)
            reasons.Add(PetBrainWhyReason.StrongTopic);

        if (mechanic >= 55)
        {
            var freshTopic = topic < 45 || novelty >= 90;
            reasons.Add(freshTopic
                ? PetBrainWhyReason.LovedMechanicNewTopic
                : PetBrainWhyReason.LovedMechanic);
        }

        if (continuity >= 80)
            reasons.Add(PetBrainWhyReason.ContinueStory);

        if (mind.Personalization.SessionLength != PetBrainSessionLength.Medium)
            reasons.Add(PetBrainWhyReason.MatchesSessionLength);

        if (support >= 85)
            reasons.Add(PetBrainWhyReason.SupportReady);

        if (novelty >= 100 && !mind.CompletedTemplates.Contains(template.Key))
            reasons.Add(PetBrainWhyReason.SomethingNew);

        if (template.RewardFlavor == mind.Personalization.RewardPreference)
            reasons.Add(PetBrainWhyReason.RewardMatch);

        // Dürüstlük: sistem az şey biləndə bunu gizlətmir.
        if (mind.ProfileConfidence < 30)
            reasons.Add(PetBrainWhyReason.StillLearning);

        if (reasons.Count == 0)
            reasons.Add(PetBrainWhyReason.MatchesChallenge);

        return [.. reasons.Distinct().Take(4)];
    }

    private static int Interest(PetMindContext mind, string key) =>
        mind.Interests.TryGetValue(key, out var value) ? TraitKeys.Clamp(value) : TraitKeys.StartingScore;

    private static int PlayStyle(PetMindContext mind, string key) =>
        mind.PlayStyles.TryGetValue(key, out var value) ? TraitKeys.Clamp(value) : TraitKeys.StartingScore;

    private static int Mechanic(PetMindContext mind, string key) =>
        mind.Mechanics.TryGetValue(key, out var value) ? TraitKeys.Clamp(value) : TraitKeys.StartingScore;

    private static int At(int[] table, int index) => index < table.Length ? table[index] : 0;
}
