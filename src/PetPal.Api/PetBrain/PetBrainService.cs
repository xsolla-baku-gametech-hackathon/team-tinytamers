using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Learning;
using PetPal.Api.PetBrain.BoundedAi;
using PetPal.Api.PetBrain.Intent;
using PetPal.Api.PetBrain.Mind;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Recap;
using PetPal.Api.PetBrain.Recommendation;
using PetPal.Api.PetBrain.Story;
using PetPal.Api.Pets;
using PetPal.Api.Progress;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Pet Brain-in əməliyyat qatı: qərarları toplayır, mərhələləri yoxlayır və
/// mükafatı dəqiq bir dəfə tətbiq edir.
///
/// <para>Qərarın ÖZÜ burada deyil — o, saf siniflərdədir
/// (<see cref="AdaptivePetDirector"/>, <see cref="ProfileLearningRules"/>,
/// <see cref="ExperienceDifficulty"/>, <see cref="MemoryPolicy"/>). Bu sinif
/// yalnız bazanı, saatı və avtorizasiyanı bağlayır.</para>
/// </summary>
public class PetBrainService : IPetBrainService
{
    /// <summary>Yeniliyin baxdığı run sayı.</summary>
    private const int RecentRunWindow = 8;

    /// <summary>Nümayiş panelində göstərilən son hadisə sayı.</summary>
    private const int DebugEventLimit = 8;

    /// <summary>Qərar jurnalında saxlanan ən çox namizəd açarı.</summary>
    private const int CandidateLogLimit = 8;

    /// <summary>Təkrar yoxlamasının baxdığı son tapmaca sayı.</summary>
    private const int RecentPuzzleWindow = 6;

    /// <summary>Mexanika üzrə pillə üçün baxılan son HƏLL OLUNMUŞ tapmaca sayı.</summary>
    private const int MechanicHistoryWindow = 24;

    /// <summary>Saxlanan məzmun sabit formatda olmalıdır — bax IssuedPuzzle.</summary>
    private static readonly JsonSerializerOptions PuzzleJson = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly IPetService _pets;
    private readonly IBehaviorTracker _tracker;
    private readonly IExperienceNarrativeProvider _narrative;
    private readonly IPersonalizedPuzzleGenerator _puzzles;
    private readonly IDailyGoalService _dailyGoals;
    private readonly PetMindContextBuilder _mind;
    private readonly PetIntentService _intents;
    private readonly StoryPlanCoordinator _plans;
    private readonly DeclinedRecommendations _declined;
    private readonly PetBrainTelemetry _telemetry;
    private readonly TimeProvider _clock;
    private readonly PetBrainOptions _options;
    private readonly PetBrainV2Options _v2;
    private readonly RecommendationPolicyOptions _policy;
    private readonly ScreenTimeOptions _screenTime;
    private readonly PuzzleIllustrationCoordinator _illustrations;
    private readonly PuzzleIllustrationQueue _sceneQueue;
    private readonly IRecapSpecFactory _recapSpecs;
    private readonly RecapCoordinator _recaps;
    private readonly RecapQueue _recapQueue;
    private readonly ILogger<PetBrainService> _logger;

    public PetBrainService(
        AppDbContext db,
        IPetService pets,
        IBehaviorTracker tracker,
        IExperienceNarrativeProvider narrative,
        IPersonalizedPuzzleGenerator puzzles,
        IDailyGoalService dailyGoals,
        PetMindContextBuilder mind,
        PetIntentService intents,
        StoryPlanCoordinator plans,
        DeclinedRecommendations declined,
        PetBrainTelemetry telemetry,
        TimeProvider clock,
        IOptions<PetBrainOptions> options,
        IOptions<PetBrainV2Options> v2,
        IOptions<RecommendationPolicyOptions> policy,
        IOptions<ScreenTimeOptions> screenTime,
        PuzzleIllustrationCoordinator illustrations,
        PuzzleIllustrationQueue sceneQueue,
        IRecapSpecFactory recapSpecs,
        RecapCoordinator recaps,
        RecapQueue recapQueue,
        ILogger<PetBrainService> logger)
    {
        _db = db;
        _pets = pets;
        _tracker = tracker;
        _narrative = narrative;
        _puzzles = puzzles;
        _dailyGoals = dailyGoals;
        _mind = mind;
        _intents = intents;
        _plans = plans;
        _declined = declined;
        _telemetry = telemetry;
        _clock = clock;
        _options = options.Value;
        _v2 = v2.Value;
        _policy = policy.Value;
        _screenTime = screenTime.Value;
        _illustrations = illustrations;
        _sceneQueue = sceneQueue;
        _recapSpecs = recapSpecs;
        _recaps = recaps;
        _recapQueue = recapQueue;
        _logger = logger;
    }

    // ==================== Giriş ekranı ====================

    public async Task<ServiceResult<PetBrainStateDto>> GetStateAsync(Guid childId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return ServiceResult<PetBrainStateDto>.Ok(new PetBrainStateDto { Enabled = false });

        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return ServiceResult<PetBrainStateDto>.NotFound(
                Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var now = _clock.GetUtcNow().UtcDateTime;
        var language = child.LanguageCode;
        var pet = child.Pet;

        PetProgression.Refresh(pet, now);

        var greeting = PetBrainGreeting.Build(
            pet, child.DisplayName, language, child.Memories, now, child.Personality);

        // Salamlamada işlənən xatirə qeyd olunur — pet hər açılışda eyni cümləni
        // təkrarlamamalıdır (bax MemoryPolicy.PickForGreeting).
        if (greeting.UsedMemory is not null)
            greeting.UsedMemory.LastUsedAt = now;

        var interests = ScoresOf(child, PetBrainTraitCategory.Interest);
        var playStyles = ScoresOf(child, PetBrainTraitCategory.PlayStyle);
        var personality = StablePersonality(child, interests, playStyles, now);

        var bondTier = BondTiers.Of(pet.Bond);

        var state = new PetBrainStateDto
        {
            Enabled = true,
            PetIsHatched = pet.HatchedAt is not null,
            PetName = pet.Name,
            Greeting = greeting.Text,
            Bond = BondRules.Clamp(pet.Bond),
            BondTier = bondTier,
            BondTierLabel = BondTiers.Label(bondTier, language),
            BondNextThreshold = BondTiers.NextThreshold(bondTier) ?? 0,
            BondEmote = PersonalityVoice.Emote(personality, bondTier),
            BondUnlockLine = BondTiers.UnlockLine(bondTier, language, pet.Name),
            BondPose = BondTiers.UnlockFor(bondTier).Pose,
            BondRoomDecor = BondTiers.UnlockFor(bondTier).RoomDecor,
            BondUnlocks = [.. BondTiers.Ladder(bondTier, language, pet.Name)],
            Personality = personality,
            PersonalityLabel = CompanionPersonality.Label(personality, language),
            Interests = TopTraits(interests, TraitKeys.Interests, language),
            PlayStyles = TopTraits(playStyles, TraitKeys.PlayStyles, language),
            Memories = [.. MemoryPolicy.Retrieve(child.Memories, now, intent: "greeting")
                .Select(m => MemoryPolicy.ToDto(m, language, pet.Name))],
            DemoMode = _options.DemoMode
        };

        // Ortaq kontekst: ana ekran, söhbət, qulluq və bu ekran EYNİ həqiqətə
        // baxmalıdır — bax PetMindContextBuilder.
        var mind = await _mind.BuildAsync(child, ct);

        state.ScreenTimeBlocked = mind.ActivityBlocked;
        state.ScreenTimeMessage = ScreenTimeGuard.MessageFor(
            ScreenTimeGuard.Evaluate(child, await _dailyGoals.GetOrCreateTodayAsync(child, ct), now,
                _screenTime.Enforced),
            language, child);

        // Pet-in ÖZ niyyəti — özəllik açarı bağlıdırsa heç nə yazılmır və
        // ekran onu ümumiyyətlə çəkmir.
        state.Intent = await _intents.AdvanceAsync(child, mind, ct);

        var activeRun = await ActiveRunAsync(childId, ct);
        if (activeRun is not null)
            state.ActiveRun = await ToDtoAsync(activeRun, child, ct);

        state.Mechanics = TopTraits(
            ScoresOf(child, PetBrainTraitCategory.Mechanic), MechanicKeys.All, language);
        state.Settings = PersonalizationMapper.ToDto(child.PersonalizationSettings, mind, language);
        state.OnboardingPending = mind.Personalization.OnboardingPending;
        state.ProfileConfidence = mind.ProfileConfidence;

        var set = DecideFor(mind);

        if (set.Primary is not null)
        {
            var opened = await OpenDecisionsAsync(child, mind, set, now, ordinal: 0, ct);

            state.Recommendation = await ToDtoAsync(opened[0].Card, opened[0].Record, child, mind, ct);
            foreach (var (card, record) in opened.Skip(1))
                state.Alternatives.Add(await ToDtoAsync(card, record, child, mind, ct));

            // "Tövsiyə göründü" hadisəsi gün ərzində BİR DƏFƏ sayılır — ekranı
            // yeniləmək profili şişirtməməlidir. Göstərilmə HEÇ BİR xassəni
            // artırmır: siyahı qəsdən boşdur.
            await _tracker.TrackAsync(
                childId,
                PetBrainEventType.RecommendationViewed,
                new PetBrainEventData(set.Primary.Candidate.Key, set.Primary.Candidate.Theme, []),
                $"rec-view:{set.Primary.Candidate.Key}:{now:yyyyMMdd}",
                ct);

            // Göstərilmə sayğacı BALA toxunmur: yalnız «uşaq bunu gördü»
            // faktını saxlayır ki, «seçmədi» ilə «heç görmədi» ayrıla bilsin.
            await RecordExposureAsync(child, opened.Select(o => o.Card.Candidate.Key), now, ct);

            _telemetry.Recommendation(
                PetBrainRecommendationFeedback.Shown, set.Primary.Candidate.Key, _policy.PolicyVersion);
        }

        if (_options.DemoMode)
            state.Debug = await BuildDebugAsync(childId, set, mind.Difficulty,
                LastPerformance(await RecentRunsAsync(childId, ct)), language,
                state.Recommendation?.NarrativeSource ?? ExperienceNarrative.TemplateSource, ct);

        await _db.SaveChangesAsync(ct);

        return ServiceResult<PetBrainStateDto>.Ok(state);
    }

    // ==================== Tövsiyə qərarı ====================

    /// <summary>
    /// İki mərhələli siyasəti ortaq kontekstlə işlədir.
    ///
    /// <para>Süzgəc, sıralama və kartların yığılması <see cref="RecommendationPolicy"/>
    /// daxilindədir — burada yalnız çağırış və determinizmin toxumu qalır.
    /// Toxum kontekst hash-ıdır: eyni vəziyyət eyni nəticəni verir, yəni
    /// «niyə bunu gördüm?» sualının cavabı prosesdən-prosesə dəyişmir.</para>
    /// </summary>
    private RecommendationSet DecideFor(PetMindContext mind) =>
        RecommendationPolicy.Decide(mind, _policy, ContextHash(mind));

    /// <summary>
    /// Qərarları AÇIR: serverin nə təklif etdiyini yazır və uşağa yalnız
    /// id-lərini verir.
    ///
    /// <para><b>Hər kartın ÖZ sətri var.</b> Yalnız əsas təklifi qeyd etsəydik,
    /// uşağın alternativi seçməsi ya heç yerdə görünməz, ya da «əsas təklif
    /// qəbul edildi» kimi yazılardı — yəni siyasətin səhvi öz uğuru kimi
    /// oxunardı. Ortaq <c>GroupId</c> onları bir baxışa bağlayır.</para>
    ///
    /// <para>Sətir PII saxlamır — nə ad, nə söhbət, nə yaddaş cümləsi. Kontekst
    /// yalnız hash kimi qalır, süzülmə səbəbləri isə enum adıdır.</para>
    /// </summary>
    private async Task<List<(RecommendationCard Card, RecommendationDecision Record)>> OpenDecisionsAsync(
        ChildProfile child,
        PetMindContext mind,
        RecommendationSet set,
        DateTime now,
        int ordinal,
        CancellationToken ct)
    {
        var groupId = Guid.NewGuid();
        var contextHash = ContextHash(mind);
        var candidateKeys = set.Ranked.Take(CandidateLogLimit).Select(c => c.Key).ToList();
        var filtered = set.Filtered.Take(CandidateLogLimit).Select(f => f.ToString()).ToList();

        List<(RecommendationCard, RecommendationDecision)> opened = [];

        foreach (var card in set.Cards)
        {
            var candidate = card.Candidate;

            var record = new RecommendationDecision
            {
                ChildProfileId = child.Id,
                GroupId = groupId,
                Slot = card.Slot,
                WasExploration = card.WasExploration,
                ProfileConfidence = set.ProfileConfidence,
                PolicyVersion = set.PolicyVersion,
                CreatedAt = now,
                ContextHash = contextHash,
                CandidateKeys = candidateKeys,
                FilteredCandidates = filtered,
                WhyReasons = [.. candidate.Why.Select(r => r.ToString())],
                SelectedTemplateKey = candidate.Key,
                FitScore = candidate.TopicFit,
                NoveltyScore = candidate.NoveltyValue,
                SurpriseScore = candidate.MechanicFit,
                TopicFit = candidate.TopicFit,
                MechanicFit = candidate.MechanicFit,
                MasteryChallengeFit = candidate.MasteryChallengeFit,
                SupportFit = candidate.SupportFit,
                PaceFit = candidate.PaceFit,
                ContinuityFit = candidate.ContinuityFit,
                RewardFit = candidate.RewardFit,
                RepetitionPenalty = candidate.RepetitionPenalty,
                TotalScore = (int)Math.Round(candidate.Total, MidpointRounding.AwayFromZero),
                Difficulty = set.Difficulty,
                Feedback = PetBrainRecommendationFeedback.Shown,
                Ordinal = ordinal
            };

            _db.RecommendationDecisions.Add(record);
            opened.Add((card, record));
        }

        await _db.SaveChangesAsync(ct);

        return opened;
    }

    /// <summary>
    /// «Uşağa göstərildi» qeydi — <b>bal DƏYİŞMİR</b>.
    ///
    /// <para>Göstərilmə üstünlük deyil. Amma «seçmədi» ilə «heç görmədi»
    /// arasındakı fərq real məlumatdır: birincisi zəif siqnaldır, ikincisi
    /// isə heç nə. Sayğac məhz bunu ayırır.</para>
    ///
    /// <para>Sətir yoxdursa yaradılmır: heç vaxt toxunulmamış açar üçün
    /// «göstərildi» yazmaq onu mövcud kimi göstərərdi.</para>
    /// </summary>
    private async Task RecordExposureAsync(
        ChildProfile child, IEnumerable<string> templateKeys, DateTime now, CancellationToken ct)
    {
        var themes = templateKeys
            .Select(ExperienceCatalog.Find)
            .Where(t => t is not null)
            .Select(t => t!.PrimaryInterest)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (themes.Count == 0)
            return;

        var traits = await _db.PlayerTraits
            .Where(t => t.ChildProfileId == child.Id
                        && t.Category == PetBrainTraitCategory.Interest
                        && themes.Contains(t.Key))
            .ToListAsync(ct);

        foreach (var trait in traits)
            trait.ExposureCount++;
    }

    /// <summary>Kontekstin barmaq izi — yalnız zolaq və açarlardan.</summary>
    private static string ContextHash(PetMindContext mind) =>
        StableHash.Of(string.Join('|',
            mind.AgeBand,
            mind.Language,
            mind.Personality,
            mind.BondTier,
            mind.Mood,
            mind.Difficulty,
            mind.DailyGoal,
            mind.Weather,
            mind.ScreenTime,
            mind.SessionBucket,
            string.Join(',', mind.Interests.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}:{p.Value}")),
            string.Join(',', mind.PlayStyles.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}:{p.Value}")))).ToString("x16");

    /// <summary>
    /// "Başqa fikir" və "sonra".
    ///
    /// <para>Hər ikisi serverin verdiyi qərara bağlanır: klient nə şablon, nə də
    /// bal göndərə bilmir. Yad və ya köhnəlmiş qərar rədd olunur.</para>
    /// </summary>
    public async Task<ServiceResult<PetBrainStateDto>> SubmitFeedbackAsync(
        Guid childId, PetBrainFeedbackRequest request, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Disabled<PetBrainStateDto>();

        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return ServiceResult<PetBrainStateDto>.NotFound(
                Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var language = child.LanguageCode;

        if (request.Feedback is not (PetBrainRecommendationFeedback.ShowAnother
            or PetBrainRecommendationFeedback.NotNow
            or PetBrainRecommendationFeedback.Liked
            or PetBrainRecommendationFeedback.ShowLess))
            return ServiceResult<PetBrainStateDto>.Fail(
                Localized.T(language, "Bu cavab qəbul edilmir.", "That answer is not accepted."));

        // Sahiblik sorğunun İÇİNDƏDİR: yad uşağın qərarı ümumiyyətlə tapılmır.
        var record = await _db.RecommendationDecisions
            .FirstOrDefaultAsync(d => d.Id == request.DecisionId && d.ChildProfileId == childId, ct);

        if (record is null)
            return ServiceResult<PetBrainStateDto>.NotFound(
                Localized.T(language, "Bu təklif artıq keçərli deyil.", "That suggestion is no longer valid."));

        if (record.Feedback != PetBrainRecommendationFeedback.Shown)
            return ServiceResult<PetBrainStateDto>.Conflict(
                Localized.T(language, "Bu təklifə artıq cavab verilib.", "You already answered this suggestion."));

        var now = _clock.GetUtcNow().UtcDateTime;

        record.Feedback = request.Feedback;
        record.FeedbackAt = now;

        switch (request.Feedback)
        {
            case PetBrainRecommendationFeedback.ShowAnother:
            case PetBrainRecommendationFeedback.NotNow:
                // Hər iki cavab şablonu bu SESSİYA üçün kənara qoyur. Nə biri,
                // nə digəri maraq balını AZALTMIR: bir dəfə "sonra" demək bir
                // mövzunu sevməmək deyil.
                _declined.Record(childId, record.SelectedTemplateKey, now);

                // Amma bu, EXPOSURE qeydidir: uşağa göstərildi, o isə seçmədi.
                // Bal toxunulmaz qalır, yalnız İNAM azalır — «bunu sevir»
                // iddiasına şübhə qatılır, «sevmir» deyilmir.
                await RecordExposureSkipAsync(child, record.SelectedTemplateKey, now, ct);
                break;

            case PetBrainRecommendationFeedback.Liked:
                // AÇIQ bəyənmə kartı kənara QOYMUR: uşaq bəyəndiyini oynaya
                // bilməlidir, yoxsa düymə onu cəzalandırardı.
                await ApplyContentFeedbackAsync(
                    child, PetBrainContentScope.Template, record.SelectedTemplateKey,
                    PetBrainContentPreferenceKind.Liked, PetBrainSettingSource.Child, now, ct);
                break;

            case PetBrainRecommendationFeedback.ShowLess:
                await ApplyContentFeedbackAsync(
                    child, PetBrainContentScope.Template, record.SelectedTemplateKey,
                    PetBrainContentPreferenceKind.ShowLess, PetBrainSettingSource.Child, now, ct);

                _declined.Record(childId, record.SelectedTemplateKey, now);
                break;
        }

        await _db.SaveChangesAsync(ct);

        _telemetry.Recommendation(request.Feedback, record.SelectedTemplateKey, _policy.PolicyVersion);

        return await GetStateAsync(childId, ct);
    }

    /// <summary>
    /// Açıq məzmun rəyini yazır: qeyd sətri + xassə düzəlişi.
    ///
    /// <para><b>İki iş birlikdə görülür.</b> Yalnız balı dəyişsəydik, uşaq
    /// dediyinin nəticəsini görməzdi (bal görünmür); yalnız qeyd saxlasaydıq,
    /// profil öyrənməzdi. Qeyd kartı görünən şəkildə geri çəkir, düzəliş isə
    /// gələcək sıralamaya təsir edir.</para>
    ///
    /// <para><b>Blok bura düşmür</b> — o, valideyn qərarıdır və uşağın zövqü
    /// kimi yazıla bilməz.</para>
    /// </summary>
    private async Task ApplyContentFeedbackAsync(
        ChildProfile child,
        PetBrainContentScope scope,
        string key,
        PetBrainContentPreferenceKind kind,
        PetBrainSettingSource source,
        DateTime now,
        CancellationToken ct)
    {
        var preference = await _db.ContentPreferences.FirstOrDefaultAsync(
            p => p.ChildProfileId == child.Id && p.Scope == scope && p.Key == key, ct);

        if (preference is null)
        {
            preference = new ContentPreference
            {
                ChildProfileId = child.Id,
                Scope = scope,
                Key = key,
                CreatedAt = now
            };

            _db.ContentPreferences.Add(preference);
        }
        else if (preference.Kind == kind && kind == PetBrainContentPreferenceKind.ShowLess)
        {
            // Təkrar «daha az göstər» müddəti uzadır — amma sonsuz deyil.
            preference.RepeatCount++;
        }

        preference.Kind = kind;
        preference.Source = source;
        preference.UpdatedAt = now;
        preference.ExpiresAt = kind == PetBrainContentPreferenceKind.ShowLess
            ? ContentPreferenceRules.ExpiryFor(preference.RepeatCount, now)
            : null;

        var adjustments = ProfileLearningRules.ForExplicitFeedback(scope, key, kind);

        if (adjustments.Count == 0)
            return;

        var eventType = kind == PetBrainContentPreferenceKind.Liked
            ? PetBrainEventType.ExplicitLiked
            : PetBrainEventType.ExplicitDisliked;

        // İdempotentlik açarı gün + açar üzrədir: uşaq düyməni dalbadal
        // basanda profil beş dəfə dəyişməməlidir.
        await _tracker.TrackAsync(
            child.Id,
            eventType,
            new PetBrainEventData(key, scope.ToString(), adjustments),
            $"explicit:{kind}:{scope}:{key}:{now:yyyyMMdd}",
            ct);
    }

    /// <summary>
    /// «Göstərildi, seçilmədi» qeydi.
    ///
    /// <para>Bu, özünü təsdiqləyən dövrəni qıran yerdir: sistem öz təklifini
    /// uşağın üstünlüyü kimi oxuya bilməz, amma uşağın ONA BAXIB keçdiyini də
    /// unutmamalıdır. Bal DƏYİŞMİR — yalnız inam azalır.</para>
    ///
    /// <para>Sətir yoxdursa yaradılmır: heç vaxt toxunulmamış açar üçün
    /// «skip» yazmaq onu mövcud kimi göstərərdi.</para>
    /// </summary>
    private async Task RecordExposureSkipAsync(
        ChildProfile child, string templateKey, DateTime now, CancellationToken ct)
    {
        if (ExperienceCatalog.Find(templateKey) is not { } template)
            return;

        var trait = await _db.PlayerTraits.FirstOrDefaultAsync(
            t => t.ChildProfileId == child.Id
                 && t.Category == PetBrainTraitCategory.Interest
                 && t.Key == template.PrimaryInterest, ct);

        if (trait is null)
            return;

        TraitEvidence.RecordSkip(trait, now);
        trait.UpdatedAt = now;
    }

    /// <summary>
    /// Uşaq alternativi seçdi — həmin baxışın ƏSAS kartı rədd edilmiş sayılır.
    ///
    /// <para>Bal cədvəlinə TOXUNMUR: bu, «bəyənmədim» deyil, «bu dəfə o birini
    /// istədim»dir. Yalnız qərar jurnalı düzəlir ki, «tövsiyəm qəbul olundumu»
    /// sualının cavabı dürüst qalsın.</para>
    /// </summary>
    private async Task MarkPrimaryDeclinedAsync(
        RecommendationDecision chosen, DateTime now, CancellationToken ct)
    {
        var primary = await _db.RecommendationDecisions.FirstOrDefaultAsync(
            d => d.GroupId == chosen.GroupId
                 && d.ChildProfileId == chosen.ChildProfileId
                 && d.Slot == PetBrainRecommendationSlot.Primary
                 && d.Feedback == PetBrainRecommendationFeedback.Shown, ct);

        if (primary is null)
            return;

        primary.Feedback = PetBrainRecommendationFeedback.NotNow;
        primary.FeedbackAt = now;
    }

    /// <summary>
    /// Tapmacanın nəticəsini MEXANİKA ustalığına yazır.
    ///
    /// <para><b>Maraq cədvəlinə toxunmur.</b> Bacarmaq və sevmək iki ayrı
    /// sualdır: tapmacada uğursuzluq mövzunu sevməmək demək deyil, ipucu
    /// istəmək isə nə birini, nə də digərini göstərir.</para>
    ///
    /// <para>Naməlum mexanika səssizcə buraxılır (fail closed): yeni tapmaca
    /// növü gələndə profil uydurma sütun almır.</para>
    /// </summary>
    private async Task ApplyMechanicMasteryAsync(
        Guid childId, IssuedPuzzle issued, bool solved, DateTime now, CancellationToken ct)
    {
        if (MechanicKeys.For(issued.Mechanic) is not { } key)
            return;

        var mastery = await _db.MechanicMasteries.FirstOrDefaultAsync(
            m => m.ChildProfileId == childId && m.Mechanic == key, ct);

        if (mastery is null)
        {
            mastery = MechanicMasteryRules.New(childId, key, now);
            _db.MechanicMasteries.Add(mastery);
        }

        MechanicMasteryRules.Apply(
            mastery,
            new MechanicAttempt(
                Solved: solved,
                UsedHint: issued.HintsUsed > 0,
                Mistakes: Math.Max(0, issued.Attempts - 1),
                Difficulty: issued.Difficulty),
            now);
    }

    /// <summary>Bu sessiyada neçə dəfə "başqa fikir" deyilib.</summary>
    private async Task<int> ShowAnotherCountAsync(Guid childId, DateTime now, CancellationToken ct) =>
        await _db.RecommendationDecisions
            .AsNoTracking()
            .CountAsync(d => d.ChildProfileId == childId
                             && d.Feedback == PetBrainRecommendationFeedback.ShowAnother
                             && d.FeedbackAt != null
                             && d.FeedbackAt > now - DeclinedRecommendations.Window, ct);

    // ==================== Run başlatmaq ====================

    public async Task<ServiceResult<PetBrainRunDto>> StartRunAsync(
        Guid childId, StartPetBrainRunRequest request, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Disabled<PetBrainRunDto>();

        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return ServiceResult<PetBrainRunDto>.NotFound(
                Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var language = child.LanguageCode;
        var now = _clock.GetUtcNow().UtcDateTime;

        PetProgression.Refresh(child.Pet, now);

        // Yumurta macəraya çıxmır — mövcud qulluq/söhbət qaydası ilə eyni.
        if (child.Pet.HatchedAt is null)
            return ServiceResult<PetBrainRunDto>.Fail(PetVoice.StillAnEgg(language));

        // Naməlum şablon dərhal rədd olunur — kataloqdan kənar heç nə başlamır.
        if (!string.IsNullOrWhiteSpace(request.TemplateKey) && !ExperienceCatalog.IsKnown(request.TemplateKey))
            return ServiceResult<PetBrainRunDto>.NotFound(
                Localized.T(language, "Belə macəra yoxdur.", "There is no such adventure."));

        // Ekran vaxtı YALNIZ yeni fəaliyyəti bloklayır.
        var goal = await _dailyGoals.GetOrCreateTodayAsync(child, ct);
        var screenTime = ScreenTimeGuard.Evaluate(child, goal, now, _screenTime.Enforced);

        if (screenTime != ScreenTimeState.Allowed)
        {
            await _db.SaveChangesAsync(ct);
            return ServiceResult<PetBrainRunDto>.Forbidden(
                ScreenTimeGuard.MessageFor(screenTime, language, child));
        }

        // Yarımçıq run varsa yenisi açılmır — uşaq iki macərada eyni anda olmamalıdır.
        var existing = await ActiveRunAsync(childId, ct);
        if (existing is not null)
            return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(existing, child, ct));

        var mind = await _mind.BuildAsync(child, ct);
        var difficulty = mind.Difficulty;

        var set = DecideFor(mind);
        var template = set.Primary?.Candidate.Template;

        // Qərar id-si verilibsə, o, uşağın ÖZ və HƏLƏ AÇIQ qərarı olmalıdır.
        //
        // Bu, təkcə səliqə deyil: klient sərbəst şablon və ya yad qərar
        // göndərə bilməməlidir. Qərar tapılanda serverin cari sıralaması
        // əvəzinə MƏHZ göstərilmiş kart başladılır.
        //
        // <b>Alternativ də qəbul edilir.</b> Yalnız «əsas» kartı başlatmaq
        // uşağı bizim siyasətimizə məcbur etmək olardı: ekranda üç kart
        // göstərib yalnız birini qəbul etmək seçim deyil, bəzəkdir.
        RecommendationDecision? opened = null;

        if (request.DecisionId is { } decisionId)
        {
            opened = await _db.RecommendationDecisions
                .FirstOrDefaultAsync(d => d.Id == decisionId && d.ChildProfileId == childId, ct);

            if (opened is null || opened.Feedback != PetBrainRecommendationFeedback.Shown)
                return ServiceResult<PetBrainRunDto>.Conflict(
                    Localized.T(language,
                        "Bu macəra artıq növbədə deyil — ekranı yenilə.",
                        "That adventure is no longer the current one — refresh the screen."));

            if (ExperienceCatalog.Find(opened.SelectedTemplateKey) is { } offered)
                template = offered;
        }

        if (template is null)
            return ServiceResult<PetBrainRunDto>.Fail(
                Localized.T(language, "Hazırda uyğun macəra yoxdur.", "No suitable adventure right now."));

        // Klient kataloqdan istədiyini seçə bilmir: göndərilən açar serverin
        // hazırkı qərarı ilə üst-üstə düşməlidir.
        if (!string.IsNullOrWhiteSpace(request.TemplateKey)
            && !string.Equals(request.TemplateKey, template.Key, StringComparison.Ordinal))
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language,
                    "Bu macəra artıq növbədə deyil — ekranı yenilə.",
                    "That adventure is no longer the current one — refresh the screen."));
        var graph = GraphFor(template.Key);

        // Modelə plan qurmaq İCAZƏSİ (bağlıdırsa heç nə olmur). Plan yalnız
        // BÜTÜN yoxlamalardan keçəndə deterministik tərifi əvəz edir; bir
        // yoxlama da uğursuz olsa, uşaq fərqi görmür.
        if (graph is not null)
            graph = await _plans.ResolveAsync(graph, mind, ct);

        var run = new ExperienceRun
        {
            ChildProfileId = childId,
            TemplateKey = template.Key,

            // Tərifin versiyası run-a YAZILIR: sonrakı deploy kataloqu
            // dəyişdirsə də, bu macəra başladığı qaydalarla oxunur.
            DefinitionVersion = graph?.Version ?? template.Version,
            CurrentNodeId = graph?.StartNodeId ?? string.Empty,
            StoryFlags = [.. graph is null ? [] : StoryRuntime.FlagsOf(graph.Start)],
            DecisionId = opened?.Id,
            ExperienceType = template.Type,
            Theme = template.Theme,
            Difficulty = difficulty,
            Status = PetBrainRunStatus.Active,
            CurrentStage = 0,
            StartedAt = now,

            // Toxum run-a bağlıdır: uşaq ekranı yeniləyib asan sual ovlaya bilmir.
            // Tapmacanın toxumu BURADA saxlanılmır: o, run-ın öz id-sindən və
            // şablon açarından SHA-256 ilə çıxarılır (bax PuzzleSeed) və
            // verilmiş tapmaca ilə birlikdə IssuedPuzzles cədvəlinə düşür.
            Assisted = NeedsAssist(mind)
        };

        _db.ExperienceRuns.Add(run);

        // Qərar "seçildi" olaraq bağlanır: göstərilmə ilə seçilmə artıq eyni
        // şey deyil və ölçmə ikisini ayırd edir.
        var choseAlternative = opened is not null && opened.Slot != PetBrainRecommendationSlot.Primary;

        if (opened is not null)
        {
            opened.Feedback = PetBrainRecommendationFeedback.Selected;
            opened.FeedbackAt = now;

            // Uşaq alternativi seçibsə, ƏSAS kart rədd edilib. Onu «Shown»
            // qoyub buraxmaq siyasətin səhvini görünməz edərdi: ölçmədə
            // «təklifim qəbul olundu» kimi oxunardı.
            if (choseAlternative)
                await MarkPrimaryDeclinedAsync(opened, now, ct);
        }

        // Yeni macəra başlayanda sessiyanın "sonra" siyahısı təmizlənir.
        _declined.Clear(childId);

        // Uşaq artıq bitirdiyi macəraya QAYIDIRSA, bu, ən güclü dolayı müsbət
        // siqnaldır — tövsiyənin qəbulundan fərqli olaraq sırf onun seçimidir.
        var isReplay = mind.CompletedTemplates.Contains(template.Key);

        await _tracker.TrackAsync(
            childId,
            choseAlternative
                ? PetBrainEventType.AlternativeSelected
                : PetBrainEventType.RecommendationSelected,
            new PetBrainEventData(template.Key, template.Theme, ProfileLearningRules.ForSelection(template)),
            null,
            ct);

        if (isReplay)
            await _tracker.TrackAsync(
                childId,
                PetBrainEventType.ContentReplayed,
                new PetBrainEventData(template.Key, template.Theme, ProfileLearningRules.ForReplay(template)),
                $"replay:{run.Id:N}",
                ct);

        await _tracker.TrackAsync(
            childId,
            PetBrainEventType.ExperienceStarted,
            new PetBrainEventData(template.Key, difficulty.ToString(), []),
            $"run-start:{run.Id:N}",
            ct);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Bazadakı qismən unikal indeks ikinci paralel sorğunu burada
            // dayandırır. Bu, uşağın səhvi deyil — iki toxunuş, zəif şəbəkə və
            // ya iki cihaz eyni anda "başla" göndərə bilər. Ona görə xəta yox,
            // BİRİNCİ sorğunun yaratdığı macəra qaytarılır: uşaq həmişə bir və
            // eyni macərada olur.
            //
            // Uduzan sorğunun BÜTÜN yeni sətirləri atılır — macəra, hadisə
            // jurnalı və hadisənin yaratdığı xassə sətirləri. Yalnız run-u
            // ayırmaq kifayət etmirdi: qalan sətirlər izləyicidə <c>Added</c>
            // qalır və növbəti yazmada (tapmacanın saxlanması) yenidən cəhd
            // edilirdi — orada isə qalibin artıq yazdığı eyni açar unikal
            // indeksi ikinci dəfə pozurdu və uşaq 500 alırdı.
            foreach (var entry in _db.ChangeTracker.Entries()
                         .Where(e => e.State == EntityState.Added)
                         .ToList())
                entry.State = EntityState.Detached;

            var winner = await ActiveRunAsync(childId, ct);

            if (winner is null)
                return ServiceResult<PetBrainRunDto>.Conflict(
                    Localized.T(language,
                        "Macəra başlamadı — bir daha yoxla.",
                        "The adventure did not start — please try again."));

            _logger.LogInformation(
                "PetBrain: paralel başlatma bloklandı, mövcud macəra qaytarıldı ({RunId}).", winner.Id);

            _telemetry.Conflict("start-run");

            return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(winner, child, ct));
        }

        _telemetry.Recommendation(
            PetBrainRecommendationFeedback.Selected, template.Key, _v2.PolicyVersion);
        _telemetry.RunStarted(template.Key, run.DefinitionVersion, difficulty, graph is not null);

        return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));
    }

    /// <summary>
    /// Bu macərada dəstək rejimi lazımdırmı — son TAMAMLANMIŞ nəticəyə görə.
    /// </summary>
    private static bool NeedsAssist(PetMindContext mind)
    {
        var last = mind.RecentOutcomes.FirstOrDefault(o => o.CountsForDifficulty);

        return last is not null && ExperienceDifficulty.NeedsAssist(
            new RunPerformance(mind.Difficulty, last.ScorePercent, last.HintsUsed, last.Mistakes));
    }

    /// <summary>
    /// Bu şablonun budaqlanan tərifi — özəllik açarı bağlıdırsa <c>null</c>.
    /// </summary>
    private ExperienceDefinition? GraphFor(string templateKey) =>
        _v2 is { Enabled: true, StoryGraphEnabled: true }
            ? StoryCatalog.Find(templateKey)
            : null;

    /// <summary>
    /// YARIMÇIQ run-ın tərifi.
    ///
    /// <para>Burada özəllik açarına BAXILMIR və bu, qəsdəndir: qraf run-u
    /// başlayandan sonra açar bağlansa, run yenə də başladığı modellə
    /// bitirilməlidir. Əks halda uşağın açıq macərası deploy zamanı ekranda
    /// boş qalardı.</para>
    /// </summary>
    private ExperienceDefinition? GraphOf(ExperienceRun run)
    {
        if (string.IsNullOrEmpty(run.CurrentNodeId) && string.IsNullOrEmpty(run.EndingKey))
            return null;

        var (definition, exact) = StoryCatalog.Resolve(run.TemplateKey, run.DefinitionVersion);

        if (definition is not null && !exact)
            _logger.LogWarning(
                "PetBrain: qraf run {RunId} {Version} versiyası ilə başlayıb, kataloqda {Current} var.",
                run.Id, run.DefinitionVersion, definition.Version);

        return definition;
    }

    // ==================== Run oxumaq ====================

    public async Task<ServiceResult<PetBrainRunDto>> GetRunAsync(
        Guid childId, Guid runId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Disabled<PetBrainRunDto>();

        var (run, child, error) = await LoadRunAsync(childId, runId, ct);
        if (error is not null)
            return error;

        // Tamamlanmış run YEKUNU ilə qayıdır.
        //
        // Bu, təkcə səliqə deyil: recap videosu arxa fonda hazır olur və uşaq
        // ekranı yeniləyəndə onu GÖRMƏLİDİR. Yekun yalnız tamamlama cavabında
        // olsaydı, video heç vaxt ekrana çıxmazdı — yenilənmə isə xülasəni
        // tamamilə itirərdi.
        if (run!.Status == PetBrainRunStatus.Completed &&
            TemplateOf(run) is { } completed)
            return ServiceResult<PetBrainRunDto>.Ok(
                await BuildCompletedDtoAsync(run, child!, completed, ct));

        return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child!, ct));
    }

    // ==================== Mərhələ cavabı ====================

    public async Task<ServiceResult<PetBrainRunDto>> SubmitChoiceAsync(
        Guid childId, Guid runId, PetBrainChoiceRequest request, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Disabled<PetBrainRunDto>();

        var (run, child, error) = await LoadRunAsync(childId, runId, ct);
        if (error is not null)
            return error;

        var language = child!.LanguageCode;

        if (run!.Status != PetBrainRunStatus.Active)
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language, "Bu macəra artıq bitib.", "This adventure is already finished."));

        var template = TemplateOf(run);
        if (template is null)
            return ServiceResult<PetBrainRunDto>.NotFound(
                Localized.T(language, "Belə macəra yoxdur.", "There is no such adventure."));

        // Budaqlanan macəra AYRI yolla gedir: orada "mərhələ indeksi" deyil,
        // düyün açarı və hekayə bayraqları qərar verir.
        if (GraphOf(run) is { } graph)
            return await SubmitGraphStepAsync(run, child, template, graph, request, ct);

        if (run.CurrentStage >= template.StageCount)
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language, "Bütün mərhələlər bitib.", "Every stage is already done."));

        // Mərhələ atlamaq və iki dəfə basmaq eyni yolla bağlanır: klientin
        // gözlədiyi mərhələ serverinki ilə üst-üstə düşməlidir.
        if (request.StageIndex != run.CurrentStage)
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language, "Bu mərhələ artıq keçilib.", "That stage has already been played."));

        var stage = template.Stages[run.CurrentStage];
        var now = _clock.GetUtcNow().UtcDateTime;

        // ---- İpucu istəyi: mərhələ İRƏLİLƏMİR ----
        if (request.RequestHint)
        {
            if (stage.Kind != PetBrainStageKind.Puzzle)
                return ServiceResult<PetBrainRunDto>.Fail(
                    Localized.T(language, "Bu mərhələdə ipucu yoxdur.", "There is no hint on this stage."));

            run.HintsUsed++;

            // İpucu sayğacı tapmacanın özündə də saxlanılır: yenilənmədən sonra
            // ipucu ekranda QALMALIDIR, yoxsa uşaq onu itirmiş olur.
            var hinted = await EnsurePuzzleAsync(run, template, child, run.CurrentStage, ct);
            hinted.HintsUsed++;

            await _tracker.TrackAsync(
                childId,
                PetBrainEventType.HintRequested,
                // İpucu MARAĞI AZALTMIR — bax ProfileLearningRules.
                new PetBrainEventData(template.Key, $"stage:{run.CurrentStage}", []),
                $"hint:{run.Id:N}:{run.CurrentStage}:{run.HintsUsed}",
                ct);

            await _db.SaveChangesAsync(ct);
            return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct, showHint: true));
        }

        // Tapmaca cavabı id SİYAHISIDIR, seçim mərhələsi isə tək açar — ona görə
        // iki mərhələ növü ayrı sahələr işlədir və biri digərinin yerinə keçmir.
        if (stage.Kind == PetBrainStageKind.Puzzle)
            return await SubmitPuzzleAsync(run, child, template, request.SelectedIds, now, ct);

        if (string.IsNullOrWhiteSpace(request.OptionKey))
            return ServiceResult<PetBrainRunDto>.Fail(
                Localized.T(language, "Bir variant seçilməlidir.", "Please choose an option."));

        return stage.Kind switch
        {
            PetBrainStageKind.Choice => await SubmitChoiceStageAsync(run, child, template, stage, request.OptionKey!, ct),
            _ => await AdvanceIntroAsync(run, child, template, ct)
        };
    }

    // ==================== Budaqlanan hekayə ====================

    /// <summary>
    /// Qrafda BİR addım.
    ///
    /// <para>Ardıcıllıq həmişə eynidir: ekran uyğunluğu → giriş yığılır →
    /// nəticə sətri yazılır → növbəti düyün hesablanır → düyünün effektləri
    /// tətbiq olunur. Nəticə sətrinin (run, düyün) cütü unikaldır, ona görə
    /// təkrar göndərilən sorğu addımı İKİNCİ DƏFƏ tətbiq edə bilmir.</para>
    /// </summary>
    private async Task<ServiceResult<PetBrainRunDto>> SubmitGraphStepAsync(
        ExperienceRun run,
        ChildProfile child,
        ExperienceTemplate template,
        ExperienceDefinition graph,
        PetBrainChoiceRequest request,
        CancellationToken ct)
    {
        var language = child.LanguageCode;
        var now = _clock.GetUtcNow().UtcDateTime;

        var node = graph.Find(run.CurrentNodeId);

        // Düyün tapılmırsa tərif dəyişib. Uşağın macərasını 500 ilə itirmək
        // əvəzinə onu başlanğıc düyünə TƏHLÜKƏSİZ qaytarırıq.
        if (node is null)
        {
            _logger.LogWarning(
                "PetBrain: run {RunId} üçün «{Node}» düyünü tapılmadı — başlanğıca qaytarılır.",
                run.Id, run.CurrentNodeId);

            _telemetry.Fallback("missing-node");

            run.CurrentNodeId = graph.StartNodeId;
            await _db.SaveChangesAsync(ct);

            return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));
        }

        // Ekran uyğunluğu: klientin gördüyü düyün serverinki ilə üst-üstə
        // düşməlidir. Açar göndərilməyibsə köhnə mərhələ indeksi qəbul edilir.
        if (!string.IsNullOrWhiteSpace(request.NodeId))
        {
            if (!string.Equals(request.NodeId, node.Id, StringComparison.Ordinal))
                return ServiceResult<PetBrainRunDto>.Conflict(
                    Localized.T(language, "Bu addım artıq keçilib.", "That step has already been played."));
        }
        else if (request.StageIndex != run.CurrentStage)
        {
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language, "Bu addım artıq keçilib.", "That step has already been played."));
        }

        // ---- İpucu: addım İRƏLİLƏMİR ----
        if (request.RequestHint)
        {
            if (node.Kind != PetBrainStageKind.Puzzle)
                return ServiceResult<PetBrainRunDto>.Fail(
                    Localized.T(language, "Bu addımda ipucu yoxdur.", "There is no hint on this step."));

            run.HintsUsed++;

            var hinted = await EnsurePuzzleAsync(run, template, child, run.CurrentStage, ct, node.PuzzleFamily);
            hinted.HintsUsed++;

            await _tracker.TrackAsync(
                child.Id,
                PetBrainEventType.HintRequested,
                new PetBrainEventData(template.Key, $"node:{node.Id}", []),
                $"hint:{run.Id:N}:{node.Id}:{run.HintsUsed}",
                ct);

            await _db.SaveChangesAsync(ct);

            _telemetry.Puzzle("hint", hinted.BlueprintKey, run.Difficulty);

            return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct, showHint: true));
        }

        var input = await ReadInputAsync(run, child, template, node, request, now, ct);

        if (input.Error is not null)
            return input.Error;

        // Tapmaca həll olunmayıbsa addım İRƏLİLƏMİR — macəra bitmir, uşaq
        // yenidən cəhd edir.
        if (input.Retry)
            return ServiceResult<PetBrainRunDto>.Ok(
                await ToDtoAsync(run, child, ct, puzzleMissed: true));

        var next = StoryRuntime.Next(graph, node, input.Story!);

        if (next is null)
        {
            // Validator bunu buraxmır; yenə də uşaq boş ekranla qalmamalıdır.
            _logger.LogError(
                "PetBrain: «{Node}» düyünündən keçid tapılmadı ({Template}).", node.Id, template.Key);

            _telemetry.Fallback("no-transition");

            return ServiceResult<PetBrainRunDto>.Fail(
                Localized.T(language, "Hekayə burada dayandı.", "The story paused here."));
        }

        await RecordOutcomeAsync(run, child, node, input, now, ct);

        run.CurrentNodeId = next.Id;
        run.CurrentStage++;

        // Effektlər düyünə DAXİL OLARKƏN tətbiq olunur və ideal-potentdir:
        // bayraq dəsti çoxluqdur, təkrar giriş onu ikiqat qoymur.
        foreach (var flag in StoryRuntime.FlagsOf(next))
        {
            if (!run.StoryFlags.Contains(flag, StringComparer.Ordinal))
                run.StoryFlags.Add(flag);
        }

        if (next.IsEnding)
            run.EndingKey = next.EndingKey;

        _telemetry.NodeCompleted(template.Key, node.Kind, input.Story!.OptionKey);

        await _db.SaveChangesAsync(ct);

        return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));
    }

    /// <summary>Bir addımın nəticəsi — həll olunmuş giriş və ya səhv/təkrar siqnalı.</summary>
    private sealed record GraphInput(
        StoryInput? Story,
        PetBrainStageResult Result,
        IReadOnlyList<string> SelectedKeys,
        int Attempts,
        int Hints,
        string BlueprintKey,
        bool Retry,
        ServiceResult<PetBrainRunDto>? Error);

    /// <summary>
    /// Uşağın cavabını YOXLAYIR və qrafın anlayacağı girişə çevirir.
    ///
    /// <para>Tapmacanın doğruluğu YALNIZ burada, saxlanmış həllə qarşı
    /// müəyyən edilir — klientin gövdəsində belə sahə ümumiyyətlə yoxdur.</para>
    /// </summary>
    private async Task<GraphInput> ReadInputAsync(
        ExperienceRun run,
        ChildProfile child,
        ExperienceTemplate template,
        ExperienceNode node,
        PetBrainChoiceRequest request,
        DateTime now,
        CancellationToken ct)
    {
        var language = child.LanguageCode;
        var flags = run.StoryFlags.ToHashSet(StringComparer.Ordinal);

        GraphInput Fail(string az, string en) => new(
            null, PetBrainStageResult.None, [], 0, 0, string.Empty, false,
            ServiceResult<PetBrainRunDto>.Fail(Localized.T(language, az, en)));

        switch (node.Kind)
        {
            case PetBrainStageKind.Choice:
            {
                if (string.IsNullOrWhiteSpace(request.OptionKey))
                    return Fail("Bir variant seçilməlidir.", "Please choose an option.");

                var option = node.Options.FirstOrDefault(o =>
                    string.Equals(o.Key, request.OptionKey, StringComparison.Ordinal));

                if (option is null)
                    return Fail("Belə variant yoxdur.", "There is no such option.");

                run.Choices.Add(option.Key);

                await _tracker.TrackAsync(
                    child.Id,
                    PetBrainEventType.StageChoiceMade,
                    new PetBrainEventData(template.Key, option.Key, ProfileLearningRules.ForChoice(option)),
                    $"choice:{run.Id:N}:{node.Id}",
                    ct);

                return new GraphInput(
                    new StoryInput(option.Key, PetBrainStageResult.None, flags),
                    PetBrainStageResult.None, [option.Key], 0, 0, string.Empty, false, null);
            }

            case PetBrainStageKind.Puzzle:
            {
                var issued = await EnsurePuzzleAsync(
                    run, template, child, run.CurrentStage, ct, node.PuzzleFamily);

                var blueprint = PuzzleBlueprintCatalog.Find(issued.BlueprintKey);

                if (blueprint is null)
                    return Fail("Bu tapmaca artıq keçərli deyil.", "This puzzle is no longer valid.");

                if (issued.Status != PetBrainPuzzleStatus.Issued)
                    return new GraphInput(
                        new StoryInput(string.Empty, PetBrainStageResult.Solved, flags),
                        PetBrainStageResult.Solved, [], issued.Attempts, issued.HintsUsed,
                        issued.BlueprintKey, false, null);

                var evaluated = PuzzleAnswerEvaluator.Evaluate(
                    blueprint, ReadPublic(issued), ReadSolution(issued), request.SelectedIds);

                // Formaya uyğun olmayan cavab NƏ səhv sayılır, NƏ də cəhd —
                // bu, uşağın səhvi deyil, sorğunun səhvidir.
                if (evaluated.Rejected)
                    return Fail("Bu cavab uyğun deyil.", "That answer does not fit.");

                issued.Attempts++;

                if (!evaluated.IsCorrect)
                {
                    run.Mistakes++;

                    await _tracker.TrackAsync(
                        child.Id,
                        PetBrainEventType.PuzzleFailed,
                        // Səhv cavab HEÇ BİR marağı azaltmır.
                        new PetBrainEventData(template.Key, $"node:{node.Id}", []),
                        $"puzzle-miss:{run.Id:N}:{node.Id}:{run.Mistakes}",
                        ct);

                    await _db.SaveChangesAsync(ct);

                    await ApplyMechanicMasteryAsync(child.Id, issued, solved: false, now, ct);

                    _telemetry.Puzzle("miss", issued.BlueprintKey, run.Difficulty);

                    return new GraphInput(
                        null, PetBrainStageResult.None, [], issued.Attempts, issued.HintsUsed,
                        issued.BlueprintKey, true, null);
                }

                issued.Status = PetBrainPuzzleStatus.Solved;
                issued.SolvedAt = now;

                var result = blueprint.LowPressure
                    ? PetBrainStageResult.Accepted
                    : PetBrainStageResult.Solved;

                // İlk cəhdə, ipucusuz həll AYRI hekayə düyününə apara bilər:
                // hekayə uşağın necə çatdığını da danışmalıdır.
                if (result == PetBrainStageResult.Solved && issued.Attempts == 1 && issued.HintsUsed == 0)
                    flags.Add(MoonCrystalHunt.CleanSolveFlag);

                var selected = blueprint.LowPressure && request.SelectedIds is not null
                    ? [.. request.SelectedIds]
                    : new List<string>();

                if (selected.Count > 0)
                    run.Choices.AddRange(selected);
                else
                    run.Choices.Add("solved");

                await _tracker.TrackAsync(
                    child.Id,
                    PetBrainEventType.PuzzleSolved,
                    new PetBrainEventData(
                        template.Key, $"node:{node.Id}:{issued.BlueprintKey}",
                        ProfileLearningRules.ForPuzzleSolved(issued.Mechanic)),
                    $"puzzle-solved:{run.Id:N}:{node.Id}",
                    ct);

                await ApplyMechanicMasteryAsync(child.Id, issued, solved: true, now, ct);

                _telemetry.Puzzle("solved", issued.BlueprintKey, run.Difficulty);

                return new GraphInput(
                    new StoryInput(string.Empty, result, flags),
                    result, selected, issued.Attempts, issued.HintsUsed, issued.BlueprintKey, false, null);
            }

            default:
            {
                // Giriş və nəticə ekranları: uşaq yalnız "davam" deyir.
                run.Choices.Add("continue");

                return new GraphInput(
                    new StoryInput(string.Empty, PetBrainStageResult.None, flags),
                    PetBrainStageResult.None, [], 0, 0, string.Empty, false, null);
            }
        }
    }

    /// <summary>
    /// Addımın REAL qeydi.
    ///
    /// <para>(run, düyün) cütü unikaldır: təkrar göndərilən sorğu ikinci sətir
    /// yaratmır və nəticə iki dəfə sayılmır.</para>
    /// </summary>
    private async Task RecordOutcomeAsync(
        ExperienceRun run,
        ChildProfile child,
        ExperienceNode node,
        GraphInput input,
        DateTime now,
        CancellationToken ct)
    {
        var existing = await _db.RunStageOutcomes
            .FirstOrDefaultAsync(o => o.ExperienceRunId == run.Id && o.NodeId == node.Id, ct);

        if (existing is not null)
            return;

        _db.RunStageOutcomes.Add(new RunStageOutcome
        {
            ExperienceRunId = run.Id,
            ChildProfileId = child.Id,
            NodeId = node.Id,
            StageOrdinal = run.CurrentStage,
            Kind = node.Kind,
            SelectedOptionKeys = [.. input.SelectedKeys],
            Result = input.Result,
            Attempts = input.Attempts,
            HintsUsed = input.Hints,
            EffectKeys = [.. StoryRuntime.FlagsOf(node)],
            CompletedAt = now
        });
    }

    private async Task<ServiceResult<PetBrainRunDto>> AdvanceIntroAsync(
        ExperienceRun run, ChildProfile child, ExperienceTemplate template, CancellationToken ct)
    {
        run.Choices.Add("continue");
        run.CurrentStage++;

        await _db.SaveChangesAsync(ct);
        return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));
    }

    private async Task<ServiceResult<PetBrainRunDto>> SubmitChoiceStageAsync(
        ExperienceRun run,
        ChildProfile child,
        ExperienceTemplate template,
        ExperienceStage stage,
        string optionKey,
        CancellationToken ct)
    {
        var language = child.LanguageCode;

        var option = stage.Options.FirstOrDefault(o => string.Equals(o.Key, optionKey, StringComparison.Ordinal));
        if (option is null)
            return ServiceResult<PetBrainRunDto>.Fail(
                Localized.T(language, "Belə variant yoxdur.", "There is no such option."));

        var stageIndex = run.CurrentStage;

        run.Choices.Add(option.Key);
        run.CurrentStage++;

        await _tracker.TrackAsync(
            child.Id,
            PetBrainEventType.StageChoiceMade,
            new PetBrainEventData(template.Key, option.Key, ProfileLearningRules.ForChoice(option)),
            $"choice:{run.Id:N}:{stageIndex}",
            ct);

        await _db.SaveChangesAsync(ct);
        return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));
    }

    /// <summary>
    /// Tapmaca cavabı.
    ///
    /// <para>Doğruluq YALNIZ burada, SAXLANMIŞ həllə qarşı müəyyən edilir.
    /// Klientin gövdəsində nə <c>IsCorrect</c>, nə <c>Score</c>, nə də
    /// <c>Reward</c> sahəsi var — belə sahələr DTO-da ümumiyyətlə mövcud
    /// deyil, ona görə saxta doğru cavab göndərmək mümkün deyil.</para>
    /// </summary>
    private async Task<ServiceResult<PetBrainRunDto>> SubmitPuzzleAsync(
        ExperienceRun run,
        ChildProfile child,
        ExperienceTemplate template,
        IReadOnlyList<string>? selectedIds,
        DateTime now,
        CancellationToken ct)
    {
        var language = child.LanguageCode;
        var stageIndex = run.CurrentStage;

        var issued = await EnsurePuzzleAsync(run, template, child, run.CurrentStage, ct);

        // Naməlum mexanika heç yerdə qəbul edilmir (fail closed).
        var blueprint = PuzzleBlueprintCatalog.Find(issued.BlueprintKey);
        if (blueprint is null)
            return ServiceResult<PetBrainRunDto>.Fail(
                Localized.T(language, "Bu tapmaca artıq keçərli deyil.", "This puzzle is no longer valid."));

        if (issued.Status != PetBrainPuzzleStatus.Issued)
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language, "Bu tapmaca artıq həll olunub.", "This puzzle is already solved."));

        var result = PuzzleAnswerEvaluator.Evaluate(
            blueprint, ReadPublic(issued), ReadSolution(issued), selectedIds);

        // Formaya uyğun olmayan cavab (naməlum id, təkrar, yanlış say) NƏ səhv
        // sayılır, NƏ də cəhd — bu, uşağın səhvi deyil, sorğunun səhvidir.
        if (result.Rejected)
            return ServiceResult<PetBrainRunDto>.Fail(
                Localized.T(language, "Bu cavab uyğun deyil.", "That answer does not fit."));

        issued.Attempts++;

        if (!result.IsCorrect)
        {
            run.Mistakes++;

            await _tracker.TrackAsync(
                child.Id,
                PetBrainEventType.PuzzleFailed,
                // Səhv cavab HEÇ BİR marağı azaltmır — bacarmamaq sevməmək deyil.
                new PetBrainEventData(template.Key, $"stage:{stageIndex}", []),
                $"puzzle-miss:{run.Id:N}:{stageIndex}:{run.Mistakes}",
                ct);

            await ApplyMechanicMasteryAsync(child.Id, issued, solved: false, now, ct);

            await _db.SaveChangesAsync(ct);

            // Mərhələ İRƏLİLƏMİR: uşaq yenidən cəhd edir, macəra bitmir.
            return ServiceResult<PetBrainRunDto>.Ok(
                await ToDtoAsync(run, child, ct, puzzleMissed: true));
        }

        issued.Status = PetBrainPuzzleStatus.Solved;
        issued.SolvedAt = now;

        // Yaradıcı yığımda seçilmiş parçalar SƏHNƏYƏ düşür — uşaq seçdiyini
        // qanadın üstündə görməlidir. Məntiq tapmacalarında isə yalnız
        // "həll olundu" qeyd edilir.
        if (blueprint.LowPressure && selectedIds is not null)
            run.Choices.AddRange(selectedIds);
        else
            run.Choices.Add("solved");

        run.CurrentStage++;

        await _tracker.TrackAsync(
            child.Id,
            PetBrainEventType.PuzzleSolved,
            new PetBrainEventData(
                template.Key,
                $"stage:{stageIndex}:{issued.BlueprintKey}",
                ProfileLearningRules.ForPuzzleSolved(issued.Mechanic)),
            $"puzzle-solved:{run.Id:N}:{stageIndex}",
            ct);

        await ApplyMechanicMasteryAsync(child.Id, issued, solved: true, now, ct);

        await _db.SaveChangesAsync(ct);
        return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));
    }

    // ==================== Tamamlama ====================

    public async Task<ServiceResult<PetBrainRunDto>> CompleteRunAsync(
        Guid childId, Guid runId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Disabled<PetBrainRunDto>();

        var (run, child, error) = await LoadRunAsync(childId, runId, ct);
        if (error is not null)
            return error;

        var language = child!.LanguageCode;

        var template = TemplateOf(run!);
        if (template is null)
            return ServiceResult<PetBrainRunDto>.NotFound(
                Localized.T(language, "Belə macəra yoxdur.", "There is no such adventure."));

        // Artıq tamamlanıbsa eyni yekun qaytarılır — təkrar basmaq xəta deyil.
        if (run!.Status == PetBrainRunStatus.Completed)
            return ServiceResult<PetBrainRunDto>.Ok(await BuildCompletedDtoAsync(run, child, template, ct));

        if (run.Status == PetBrainRunStatus.Abandoned)
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language, "Bu macəra yarımçıq qalıb.", "This adventure was left unfinished."));

        // Yarımçıq run tamamlana bilməz.
        //
        // Budaqlanan macərada "bütün mərhələlər keçildi" şərti mənasızdır —
        // yollar müxtəlif uzunluqdadır. Orada şərt SONLUQ düyününə çatmaqdır.
        var finished = GraphOf(run) is { } graph
            ? graph.Find(run.CurrentNodeId) is { IsEnding: true }
            : run.CurrentStage >= template.StageCount;

        if (!finished)
            return ServiceResult<PetBrainRunDto>.Conflict(
                Localized.T(language, "Macəra hələ bitməyib.", "The adventure is not finished yet."));

        return await ApplyCompletionAsync(run, child, template, ct);
    }

    /// <summary>
    /// Mükafatı DƏQİQ BİR DƏFƏ tətbiq edir.
    ///
    /// <para>Zəmanət yaddaşdakı yoxlamada deyil, BAZADADIR: <c>ExecuteUpdateAsync</c>
    /// run-u yalnız hələ <c>Active</c> və mükafatsız olduğu halda "tutur". İki
    /// eyni vaxtlı sorğudan yalnız biri sətri dəyişə bilir; ikinci sorğu sıfır
    /// sətir görür və hazır yekunu oxuyur. Qalan yazılar (XP, bağ, kosmetik,
    /// xatirə, xassə) bir tranzaksiyada gedir.</para>
    /// </summary>
    private async Task<ServiceResult<PetBrainRunDto>> ApplyCompletionAsync(
        ExperienceRun run, ChildProfile child, ExperienceTemplate template, CancellationToken ct)
    {
        var language = child.LanguageCode;
        var now = _clock.GetUtcNow().UtcDateTime;
        var pet = child.Pet!;

        var score = ScoreFor(template, run);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var claimed = await _db.ExperienceRuns
            .Where(r => r.Id == run.Id
                        && r.ChildProfileId == child.Id
                        && r.Status == PetBrainRunStatus.Active
                        && !r.RewardApplied)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, PetBrainRunStatus.Completed)
                .SetProperty(r => r.RewardApplied, true)
                .SetProperty(r => r.CompletedAt, now)
                .SetProperty(r => r.ScorePercent, score), ct);

        if (claimed == 0)
        {
            // Başqa sorğu qabaqladı: heç nə yazmırıq, hazır yekunu qaytarırıq.
            await transaction.RollbackAsync(ct);

            var current = await _db.ExperienceRuns.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == run.Id && r.ChildProfileId == child.Id, ct);

            if (current is null)
                return ServiceResult<PetBrainRunDto>.NotFound(
                    Localized.T(language, "Macəra tapılmadı.", "Adventure not found."));

            return ServiceResult<PetBrainRunDto>.Ok(await BuildCompletedDtoAsync(current, child, template, ct));
        }

        // İzlənilən nüsxə də eyni vəziyyətə gətirilir ki, sonrakı yazma
        // köhnə dəyərləri geri qaytarmasın.
        run.Status = PetBrainRunStatus.Completed;
        run.RewardApplied = true;
        run.CompletedAt = now;
        run.ScorePercent = score;

        var isFirstCompletionOfTemplate = !await _db.ExperienceRuns
            .AnyAsync(r => r.ChildProfileId == child.Id
                           && r.TemplateKey == template.Key
                           && r.Status == PetBrainRunStatus.Completed
                           && r.Id != run.Id, ct);

        var isFirstAdventureEver = !await _db.ExperienceRuns
            .AnyAsync(r => r.ChildProfileId == child.Id
                           && r.Status == PetBrainRunStatus.Completed
                           && r.Id != run.Id, ct);

        // ---- Mükafat ----
        PetProgression.Refresh(pet, now);

        var xp = isFirstCompletionOfTemplate ? template.XpReward : template.XpReward / 2;
        var levelBefore = pet.Level;
        PetProgression.AddXp(pet, xp);

        var bondGranted = BondRules.Grant(
            pet,
            isFirstCompletionOfTemplate
                ? BondRules.ForCompletion(template, isFirstAdventureEver)
                : 1);

        var unlockedCode = string.Empty;
        if (isFirstCompletionOfTemplate && PetAccessories.GrantFromExperience(pet, template.RewardCode))
            unlockedCode = template.RewardCode;

        // ---- Xatirələr ----
        var memories = await RecordMemoriesAsync(run, child, template, unlockedCode, isFirstAdventureEver, now, ct);

        // ---- Profil ----
        await _tracker.TrackAsync(
            child.Id,
            PetBrainEventType.ExperienceCompleted,
            new PetBrainEventData(
                template.Key,
                $"score:{score}",
                ProfileLearningRules.ForCompletion(template, isFirstCompletionOfTemplate)),
            $"run-complete:{run.Id:N}",
            ct);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var dto = await ToDtoAsync(run, child, ct);
        dto.Summary = BuildSummary(
            template, language, pet, xp, bondGranted, unlockedCode, memories, pet.Level > levelBefore);

        // Recap TRANZAKSİYADAN SONRA açılır və heç nə gözlətmir: XP, bağ,
        // xatirə və kosmetik artıq verilib. Video gec gəlsə də (və ya heç
        // gəlməsə də) uşağın mükafatı toxunulmazdır.
        dto.Summary.Recap = await EnsureRecapAsync(run, ct);

        _telemetry.RunCompleted(template.Key, run.EndingKey, run.CurrentStage);

        return ServiceResult<PetBrainRunDto>.Ok(dto);
    }

    /// <summary>
    /// Recap sətrini açır və uşağa göndəriləcək hissəni qurur.
    ///
    /// <para>Storyboard HƏMİŞƏ doludur — video hazır olmasa da uşaq öz
    /// seçimlərini üç kadrda görür və altyazılar serverin saxladığı HƏQİQİ
    /// seçimlərdən qurulur.</para>
    /// </summary>
    private async Task<PetBrainRecapDto> EnsureRecapAsync(ExperienceRun run, CancellationToken ct)
    {
        var spec = await _recapSpecs.BuildAsync(run.Id, ct);

        if (spec is null)
            return new PetBrainRecapDto();

        var row = await _recaps.EnsureAsync(spec, ct);

        if (row.Status is PetBrainRecapStatus.Pending or PetBrainRecapStatus.Generating)
            _recapQueue.Enqueue(spec);

        return ToRecapDto(spec, row);
    }

    /// <summary>Saxlanan vəziyyəti və deterministik storyboard-ı birləşdirir.</summary>
    private static PetBrainRecapDto ToRecapDto(AdventureRecapSpec spec, AdventureRecap row) => new()
    {
        Status = row.Status,

        // Ünvan yalnız hazır olanda verilir — provayderin URL-i heç vaxt.
        VideoUrl = row.Status == PetBrainRecapStatus.Ready
            ? $"/api/pet-brain/runs/{row.ExperienceRunId}/recap/video"
            : string.Empty,

        DurationSeconds = RecapStoryboard.TotalSeconds,
        Shots = [.. RecapStoryboard.Build(spec).Select(s => new PetBrainRecapShotDto
        {
            StartSeconds = s.StartSeconds,
            EndSeconds = s.EndSeconds,
            Caption = s.Caption,
            Icon = s.Icon
        })]
    };

    /// <summary>
    /// Uşağın ÖZ recap videosunun saxlanc açarı.
    ///
    /// <para>Yad run, hazır olmayan video və naməlum id üçün <c>null</c> —
    /// endpoint hamısına <c>404</c> verir.</para>
    /// </summary>
    public async Task<string?> GetRecapVideoKeyAsync(Guid childId, Guid runId, CancellationToken ct = default)
    {
        var row = await _db.AdventureRecaps
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ExperienceRunId == runId && r.ChildProfileId == childId, ct);

        return row is { Status: PetBrainRecapStatus.Ready } && !string.IsNullOrEmpty(row.AssetKey)
            ? row.AssetKey
            : null;
    }

    public async Task<ServiceResult<PetBrainRunDto>> AbandonRunAsync(
        Guid childId, Guid runId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Disabled<PetBrainRunDto>();

        var (run, child, error) = await LoadRunAsync(childId, runId, ct);
        if (error is not null)
            return error;

        var language = child!.LanguageCode;

        if (run!.Status != PetBrainRunStatus.Active)
            return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));

        var template = TemplateOf(run);
        var now = _clock.GetUtcNow().UtcDateTime;

        run.Status = PetBrainRunStatus.Abandoned;
        run.CompletedAt = now;

        if (template is not null)
            await _tracker.TrackAsync(
                childId,
                PetBrainEventType.ExperienceAbandoned,
                new PetBrainEventData(
                    template.Key,
                    $"stage:{run.CurrentStage}",
                    // Yalnız MƏNALI yarımçıq qoyma marağı azaldır — giriş
                    // ekranından çıxmaq fikir bildirmək deyil.
                    ProfileLearningRules.ForAbandon(template, run.CurrentStage)),
                $"run-abandon:{run.Id:N}",
                ct);

        await _db.SaveChangesAsync(ct);
        return ServiceResult<PetBrainRunDto>.Ok(await ToDtoAsync(run, child, ct));
    }

    // ==================== Ana ekran çipi ====================

    /// <summary>
    /// Ana ekran çipi.
    ///
    /// <para>Bu yol qəsdən <see cref="LoadChildAsync"/> işlətmir: ana ekran
    /// app-in ən çox açılan endpoint-idir və çipə nə XATİRƏLƏR (uşaq başına 40
    /// sətrə qədər), nə də tam bacarıq siyahısı lazımdır. Ona görə burada dar
    /// proyeksiyalar oxunur.</para>
    /// </summary>
    public async Task<PetBrainHomeChipDto?> GetHomeChipAsync(Guid childId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return null;

        var child = await LoadChildAsync(childId, ct);

        // Yumurta macəraya çıxmır — çip də göstərilmir.
        if (child?.Pet is null || child.Pet.HatchedAt is null)
            return null;

        var language = child.LanguageCode;

        // Ana ekran, söhbət və Pet Brain EYNİ kontekstdən oxuyur: pet bir
        // ekranda "yorğunam", digərində "macərəya çıxaq" deməməlidir.
        var mind = await _mind.BuildAsync(child, ct);

        // Yarımçıq macəra varsa direktoru işlətməyə ehtiyac yoxdur.
        if (mind.UnfinishedTemplateKey is { } unfinished
            && ExperienceCatalog.Find(unfinished) is { } activeTemplate)
            return new PetBrainHomeChipDto
            {
                HasActiveRun = true,
                Title = activeTemplate.Title(language),
                Icon = activeTemplate.Icon,
                Reason = Localized.T(language, "Yarımçıq qalıb — davam et", "Unfinished — pick it up")
            };

        var set = DecideFor(mind);

        if (set.Primary is not { } primary)
            return null;

        var template = primary.Candidate.Template;

        return new PetBrainHomeChipDto
        {
            HasActiveRun = false,
            Title = template.Title(language),
            Icon = template.Icon,

            // Səbəb XARAKTERİN səsi ilə gəlir: ana ekranda da pet özü kimi
            // danışmalıdır, ümumi bir cümlə ilə yox.
            //
            // İzah Pet Brain ekranındakı ilə EYNİ səbəb kodundan qurulur: iki
            // ekran eyni qərarı iki cür izah etsəydi, uşaq hansına inanacağını
            // bilməzdi.
            Reason = mind.NeedsCare
                ? Localized.T(language, "Əvvəlcə mənə bir baxaq?", "Shall we take care of me first?")
                : ChipReason(primary.Candidate, language)
        };
    }

    // ==================== Valideyn müqayisəsi ====================

    public async Task<ServiceResult<PetBrainComparisonDto>> CompareChildrenAsync(
        Guid parentUserId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Disabled<PetBrainComparisonDto>();

        // Sahiblik: valideyn YALNIZ öz uşaqlarını görür.
        var children = await _db.ChildProfiles
            .Include(c => c.Pet)
            .Include(c => c.Traits)
            .Include(c => c.SkillMasteries)
            .Where(c => c.ParentUserId == parentUserId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        var result = new PetBrainComparisonDto();

        foreach (var child in children)
        {
            if (child.Pet is null)
                continue;

            var language = child.LanguageCode;
            var interests = ScoresOf(child, PetBrainTraitCategory.Interest);
            var playStyles = ScoresOf(child, PetBrainTraitCategory.PlayStyle);

            // Valideyn görünüşü uşağın GÖRDÜYÜ etiketi göstərməlidir, ona görə
            // saxlanan xarakter başlanğıc nöqtəsidir. Burada yazma yoxdur:
            // xarakteri yalnız uşağın öz ekranı dəyişdirir.
            var personality = CompanionPersonality.Derive(interests, playStyles, child.Personality);

            var history = await RecentRunsAsync(child.Id, ct);
            var difficulty = DifficultyFor(child, LastPerformance(history));

            var context = await BuildContextAsync(child, interests, playStyles, difficulty, ct);
            var decision = AdaptivePetDirector.Decide(context);

            result.Children.Add(new PetBrainChildSnapshotDto
            {
                ChildId = child.Id,
                DisplayName = child.DisplayName,
                PetName = child.Pet.Name,
                Bond = BondRules.Clamp(child.Pet.Bond),
                PersonalityLabel = CompanionPersonality.Label(personality, language),
                TopInterests = TopTraits(interests, TraitKeys.Interests, language).Take(3).ToList(),
                RecommendedTemplateKey = decision?.Template.Key ?? string.Empty,
                RecommendedTitle = decision?.Template.Title(language) ?? string.Empty,
                Reasons = [.. decision?.Reasons ?? []]
            });
        }

        return ServiceResult<PetBrainComparisonDto>.Ok(result);
    }

    // ==================== Köməkçilər ====================

    /// <summary>Ana ekran çipinin bir sətirlik səbəbi — Pet Brain ilə eyni koddan.</summary>
    private static string ChipReason(CandidateScore candidate, string language)
    {
        var reason = candidate.Why.FirstOrDefault();
        var template = candidate.Template;

        return RecommendationVoice.Reason(
            reason,
            language,
            TraitKeys.Label(template.PrimaryInterest, language),
            MechanicKeys.Label(template.PrimaryMechanic ?? string.Empty, language),
            template.TargetMinutes);
    }

    private Task<ChildProfile?> LoadChildAsync(Guid childId, CancellationToken ct) =>
        _db.ChildProfiles
            .Include(c => c.Pet)
            .Include(c => c.Traits)
            .Include(c => c.Memories)
            // Başlanğıc çətinliyi mövcud adaptiv mühərrikin hədəfindən gəlir.
            .Include(c => c.SkillMasteries)
            // Fərdiləşdirmənin AÇIQ qatı: ayarlar, mexanika ustalığı və açıq
            // məzmun seçimləri. Ayrı sorğularla gətirmək ekranın hər açılışında
            // dörd əlavə gediş demək olardı.
            .Include(c => c.PersonalizationSettings)
            .Include(c => c.MechanicMasteries)
            .Include(c => c.ContentPreferences)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

    /// <summary>
    /// Run-u yükləyir və SAHİBLİYİ yoxlayır. Yad uşağın run-u <c>404</c> alır —
    /// <c>403</c> yox: mövcudluğu təsdiqləmək də məlumat sızmasıdır.
    /// </summary>
    private async Task<(ExperienceRun? Run, ChildProfile? Child, ServiceResult<PetBrainRunDto>? Error)>
        LoadRunAsync(Guid childId, Guid runId, CancellationToken ct)
    {
        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return (null, null, ServiceResult<PetBrainRunDto>.NotFound(
                Localized.T("Uşaq profili tapılmadı.", "Child profile not found.")));

        var run = await _db.ExperienceRuns
            .FirstOrDefaultAsync(r => r.Id == runId && r.ChildProfileId == childId, ct);

        if (run is null)
            return (null, null, ServiceResult<PetBrainRunDto>.NotFound(
                Localized.T(child.LanguageCode, "Macəra tapılmadı.", "Adventure not found.")));

        return (run, child, null);
    }

    /// <summary>
    /// Run-un tərifi — BAŞLADIĞI versiya nəzərə alınmaqla.
    ///
    /// <para>Kataloq deploy ilə dəyişə bilər. Versiya uyğun gəlmirsə tərif yenə
    /// oxunur (uşağın yarımçıq macərası itməməlidir), amma bu, jurnala düşür:
    /// yarımçıq run-un başqa qaydalarla bitməsi görünməz qalmamalıdır.</para>
    /// </summary>
    private ExperienceTemplate? TemplateOf(ExperienceRun run)
    {
        var lookup = ExperienceCatalog.Resolve(run.TemplateKey, run.DefinitionVersion);

        if (lookup.Template is not null && !lookup.Exact)
            _logger.LogWarning(
                "PetBrain: run {RunId} {Version} versiyası ilə başlayıb, kataloqda isə {Current} var.",
                run.Id, run.DefinitionVersion, lookup.Template.Version);

        return lookup.Template;
    }

    private Task<ExperienceRun?> ActiveRunAsync(Guid childId, CancellationToken ct) =>
        _db.ExperienceRuns
            .Where(r => r.ChildProfileId == childId && r.Status == PetBrainRunStatus.Active)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);

    private async Task<List<ExperienceRun>> RecentRunsAsync(Guid childId, CancellationToken ct) =>
        await _db.ExperienceRuns
            .AsNoTracking()
            .Where(r => r.ChildProfileId == childId && r.Status != PetBrainRunStatus.Active)
            .OrderByDescending(r => r.CompletedAt)
            .ThenByDescending(r => r.StartedAt)
            .Take(RecentRunWindow)
            .ToListAsync(ct);

    /// <summary>
    /// Çətinlik yalnız SON TAMAMLANMIŞ run-dan asılıdır — bir pillə qaydası
    /// özü yaranır.
    ///
    /// <para><b>Yaradıcı macəralar sayılmır.</b> Orada doğru/səhv yoxdur, ona
    /// görə nəticə həmişə 100-dür — və bu, "mükəmməl performans" kimi oxunub
    /// çətinliyi haqsız yerə qaldırırdı. Yaradıcı işin tamamlanması dəyərlidir,
    /// amma o, məharət ölçüsü DEYİL.</para>
    /// </summary>
    private static RunPerformance? LastPerformance(IEnumerable<ExperienceRun> history)
    {
        var last = history.FirstOrDefault(r =>
            r.Status == PetBrainRunStatus.Completed
            && r.ExperienceType != PetBrainExperienceType.Creative);

        return last is null
            ? null
            : new RunPerformance(last.Difficulty, last.ScorePercent, last.HintsUsed, last.Mistakes);
    }

    private PetBrainDifficulty DifficultyFor(ChildProfile child, RunPerformance? last)
    {
        if (last is not null)
            return ExperienceDifficulty.Next(last, child.Age);

        // İlk təcrübə: mövcud adaptiv mühərrikin hədəfi başlanğıc nöqtəsidir.
        var averageRating = child.SkillMasteries.Count > 0
            ? (int)child.SkillMasteries.Average(m => m.Rating)
            : AdaptiveEngine.StartingRating;

        return ExperienceDifficulty.Initial(child.Age, AdaptiveEngine.TargetDifficulty(averageRating));
    }

    private async Task<PetBrainDirectorContext> BuildContextAsync(
        ChildProfile child,
        IReadOnlyDictionary<string, int> interests,
        IReadOnlyDictionary<string, int> playStyles,
        PetBrainDifficulty difficulty,
        CancellationToken ct)
    {
        var runs = await _db.ExperienceRuns
            .AsNoTracking()
            .Where(r => r.ChildProfileId == child.Id && r.Status != PetBrainRunStatus.Active)
            .OrderByDescending(r => r.CompletedAt)
            .ThenByDescending(r => r.StartedAt)
            .Take(RecentRunWindow)
            .Select(r => new { r.TemplateKey, r.Theme, r.Status })
            .ToListAsync(ct);

        var completed = await _db.ExperienceRuns
            .AsNoTracking()
            .Where(r => r.ChildProfileId == child.Id && r.Status == PetBrainRunStatus.Completed)
            .Select(r => r.TemplateKey)
            .Distinct()
            .ToListAsync(ct);

        return new PetBrainDirectorContext(
            ChildId: child.Id,
            Age: child.Age,
            Language: child.LanguageCode,
            Interests: interests,
            PlayStyles: playStyles,
            RecentRuns: [.. runs.Select(r => new RunHistoryEntry(r.TemplateKey, r.Theme, r.Status))],
            CompletedTemplates: completed.ToHashSet(StringComparer.Ordinal),
            Difficulty: difficulty,
            PetIsHatched: child.Pet?.HatchedAt is not null);
    }

    /// <summary>
    /// Xarakteri SAXLANAN etiketin üstündə hesablayır və dəyişəni yazır.
    ///
    /// <para>Histerezis (<see cref="CompanionPersonality.SwitchMargin"/>) yalnız
    /// əvvəlki etiket məlum olanda işləyir. Əvvəllər servis onu ötürmürdü, yəni
    /// hər sorğu <c>Balanced</c>-dan başlayırdı və qoruyucu faktiki olaraq
    /// söndürülmüşdü — iki yaxın bal arasında pet hər açılışda "fikrini
    /// dəyişirdi".</para>
    ///
    /// <para>Yazma burada <c>SaveChanges</c> çağırmır: çağıranın onsuz da
    /// sonda bir yazması var.</para>
    /// </summary>
    private static PetBrainPersonality StablePersonality(
        ChildProfile child,
        IReadOnlyDictionary<string, int> interests,
        IReadOnlyDictionary<string, int> playStyles,
        DateTime now)
    {
        var personality = CompanionPersonality.Derive(interests, playStyles, child.Personality);

        if (personality == child.Personality)
            return personality;

        child.Personality = personality;
        child.PersonalityChangedAt = now;

        return personality;
    }

    private static Dictionary<string, int> ScoresOf(ChildProfile child, PetBrainTraitCategory category) =>
        child.Traits
            .Where(t => t.Category == category)
            .ToDictionary(t => t.Key, t => TraitKeys.Clamp(t.Score), StringComparer.Ordinal);

    /// <summary>
    /// Ekran üçün xassə barları. Bazada sətri olmayan açar da göstərilir —
    /// uşaq profilin bütün oxunu görür, yalnız artıq toxunduğu hissəni yox.
    /// </summary>
    private static List<PetBrainTraitDto> TopTraits(
        IReadOnlyDictionary<string, int> scores, IReadOnlyList<string> keys, string language) =>
        [.. keys
            .Select(key => TraitKeys.ToDto(key, scores.GetValueOrDefault(key, TraitKeys.StartingScore), language))
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.Key, StringComparer.Ordinal)];

    private static int ScoreFor(ExperienceTemplate template, ExperienceRun run)
    {
        // Yaradıcı təcrübədə doğru/səhv yoxdur: nəticə həmişə tamdır.
        if (template.Type == PetBrainExperienceType.Creative)
            return 100;

        return Math.Clamp(100 - (run.Mistakes * 20) - (run.HintsUsed * 10), 0, 100);
    }

    private async Task<List<PetMemory>> RecordMemoriesAsync(
        ExperienceRun run,
        ChildProfile child,
        ExperienceTemplate template,
        string unlockedCode,
        bool isFirstAdventureEver,
        DateTime now,
        CancellationToken ct)
    {
        List<PetMemory> created = [];

        // 1) Macəranın özü.
        Remember(
            isFirstAdventureEver ? PetBrainMemoryKind.FirstAdventure : PetBrainMemoryKind.ExperienceCompleted,
            template.Key,
            string.Empty,
            isFirstAdventureEver ? MemoryPolicy.FirstAdventureImportance : MemoryPolicy.CompletionImportance,
            [template.Theme, template.Type.ToString().ToLowerInvariant()]);

        // 2) Ən mənalı seçim.
        //
        // Budaqlanan macərada bu, REAL nəticə sətirlərindən oxunur: orada
        // hansı seçimin hansı addımda edildiyi dəqiq bilinir. Düz `Choices`
        // siyahısında isə "continue" və "solved" açarları seçimlərlə qarışır
        // və sonuncu element həmişə mənalı seçim olmur.
        var lastChoice = await LastMeaningfulChoiceAsync(run, ct);

        if (lastChoice is not null)
            Remember(PetBrainMemoryKind.ChoiceMade, template.Key, lastChoice,
                MemoryPolicy.ChoiceImportance, [template.Theme]);

        // 2b) Budaqlanan macəranın SONLUĞU — "hansı yolu seçdik" faktı.
        if (!string.IsNullOrEmpty(run.EndingKey))
            Remember(PetBrainMemoryKind.ChoiceMade, template.Key, run.EndingKey,
                MemoryPolicy.ChoiceImportance + 5, [template.Theme, "ending"]);

        // 3) Kosmetik.
        if (!string.IsNullOrEmpty(unlockedCode))
            Remember(PetBrainMemoryKind.CosmeticUnlocked, unlockedCode, string.Empty,
                MemoryPolicy.CosmeticImportance, [template.Theme]);

        // 4) Təkrarlanan üstünlük — eyni mövzuda ikinci tamamlamadan sonra.
        var themeCompletions = await _db.ExperienceRuns
            .CountAsync(r => r.ChildProfileId == child.Id
                             && r.Theme == template.Theme
                             && r.Status == PetBrainRunStatus.Completed, ct);

        if (themeCompletions >= 2 && TraitKeys.CategoryOf(template.Theme) is not null)
            Remember(PetBrainMemoryKind.PreferenceObserved, template.Theme, string.Empty,
                MemoryPolicy.PreferenceImportance, [template.Theme]);

        // 5) SEMANTİK nəticələr — bir neçə epizoddan çıxarılan naxış.
        await LearnPatternsAsync();

        Prune();

        return created;

        // Naxış BİR epizoddan çıxarılmır: şərt ən azı iki müstəqil
        // müşahidədir. «Bir dəfə seçdi, deməli sevir» məhz qaçmalı olduğumuz
        // səhvdir (bax SemanticMemory).
        async Task LearnPatternsAsync()
        {
            var choices = await _db.RunStageOutcomes
                .AsNoTracking()
                .Where(o => o.ChildProfileId == child.Id && o.Kind == PetBrainStageKind.Choice)
                .Select(o => new { o.ExperienceRunId, o.SelectedOptionKeys })
                .ToListAsync(ct);

            // Seçim açarları kataloqun XASSƏ təsirinə çevrilir: hansı naxışı
            // dəstəklədiyini bilən yeganə yer oradır.
            List<string> traitKeys = [];

            foreach (var outcome in choices)
            foreach (var key in outcome.SelectedOptionKeys)
            {
                var option = ExperienceCatalog.Templates
                    .SelectMany(t => t.Stages)
                    .SelectMany(s => s.Options)
                    .Concat(Story.StoryCatalog.Definitions
                        .SelectMany(d => d.Nodes)
                        .SelectMany(n => n.Options))
                    .FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.Ordinal));

                if (option is null)
                    continue;

                traitKeys.AddRange(option.Traits.Select(t => t.Key));
            }

            foreach (var candidate in SemanticMemory.FromChoices(traitKeys))
                RememberPattern(candidate);

            // Dəstəyin faydalı olduğu naxış: ipucu istəyib SONRA həll etmək.
            var supported = await _db.IssuedPuzzles
                .AsNoTracking()
                .CountAsync(p => p.ChildProfileId == child.Id
                                 && p.Status == PetBrainPuzzleStatus.Solved
                                 && p.HintsUsed > 0, ct);

            if (SemanticMemory.FromSupport(supported) is { } support)
                RememberPattern(support);
        }

        void RememberPattern(SemanticCandidate candidate)
        {
            var existing = child.Memories.FirstOrDefault(m =>
                m.Kind == PetBrainMemoryKind.PatternLearned && m.FactKey == candidate.FactKey);

            if (existing is not null)
            {
                // Naxış təkrar təsdiqləndi: dəstək artır, cümlə eyni qalır.
                existing.SupportCount = Math.Max(existing.SupportCount, candidate.Support);
                existing.Importance = Math.Max(existing.Importance, candidate.Importance);
                return;
            }

            var memory = new PetMemory
            {
                ChildProfileId = child.Id,
                Kind = PetBrainMemoryKind.PatternLearned,
                Tier = PetBrainMemoryTier.Semantic,
                FactKey = candidate.FactKey,
                ValueKey = candidate.ValueKey,
                Importance = candidate.Importance,
                SupportCount = candidate.Support,
                CreatedAt = now,
                Tags = [template.Theme]
            };

            child.Memories.Add(memory);
            _db.PetMemories.Add(memory);
        }

        // Saxlama limiti YAZI YOLUNDA tətbiq olunur.
        //
        // Qayda əvvəldən yazılmışdı, amma heç yerdən çağırılmırdı: yaddaş
        // sonsuz böyüyür, seçim isə həmişə eyni bir neçə "vacib" xatirəni
        // qaytarırdı — yəni pet zamanla yalnız ilk günlərini xatırlayan olurdu.
        void Prune()
        {
            var stale = MemoryPolicy.Prune(child.Memories);
            if (stale.Count == 0)
                return;

            foreach (var memory in stale)
            {
                // Bu run-da yaranan xatirə silinmir: uşaq onu yekun ekranında
                // GÖRÜR, deməli onun dərhal itməsi yalan olardı.
                if (created.Contains(memory))
                    continue;

                child.Memories.Remove(memory);
                _db.PetMemories.Remove(memory);
            }
        }

        void Remember(PetBrainMemoryKind kind, string factKey, string valueKey, int importance, List<string> tags)
        {
            // Eyni fakt iki dəfə xatırlanmır — unikal indeks də bunu qoruyur.
            var existing = child.Memories.FirstOrDefault(m =>
                m.Kind == kind && m.FactKey == factKey && m.ValueKey == valueKey);

            if (existing is not null)
            {
                existing.Importance = Math.Max(existing.Importance, importance);
                created.Add(existing);
                return;
            }

            var memory = new PetMemory
            {
                ChildProfileId = child.Id,
                Kind = kind,
                FactKey = factKey,
                ValueKey = valueKey,
                Importance = importance,
                CreatedAt = now,
                Tags = tags
            };

            child.Memories.Add(memory);
            _db.PetMemories.Add(memory);
            created.Add(memory);
        }
    }

    /// <summary>
    /// Uşağın bu macərada etdiyi SON mənalı seçim.
    ///
    /// <para>Budaqlanan run-da nəticə sətirlərindən; xətti run-da isə köhnə
    /// siyahıdan — miqrasiya olunmamış şablonlar da işləməyə davam etməlidir.</para>
    /// </summary>
    private async Task<string?> LastMeaningfulChoiceAsync(ExperienceRun run, CancellationToken ct)
    {
        var recorded = await _db.RunStageOutcomes
            .AsNoTracking()
            .Where(o => o.ExperienceRunId == run.Id && o.Kind == PetBrainStageKind.Choice)
            .OrderByDescending(o => o.StageOrdinal)
            .Select(o => o.SelectedOptionKeys)
            .FirstOrDefaultAsync(ct);

        if (recorded is { Count: > 0 })
            return recorded[0];

        return run.Choices.LastOrDefault(c => c is not ("continue" or "solved"));
    }

    private PetBrainSummaryDto BuildSummary(
        ExperienceTemplate template,
        string language,
        Pet pet,
        int xp,
        int bond,
        string unlockedCode,
        IReadOnlyList<PetMemory> memories,
        bool leveledUp)
    {
        var accessory = string.IsNullOrEmpty(unlockedCode) ? null : PetAccessories.Find(unlockedCode);

        return new PetBrainSummaryDto
        {
            Title = template.Title(language),
            PetLine = template.Celebration(language),

            // Reaksiya bağ PİLLƏSİNDƏNDİR: uşaq qazandığı yaxınlığı məhz
            // burada, birlikdə bitirdikləri anda eşidir.
            BondReaction = BondTiers.AdventureReactionLine(BondTiers.Of(pet.Bond), language),
            XpEarned = xp,
            BondEarned = bond,
            BondTotal = BondRules.Clamp(pet.Bond),
            UnlockedAccessoryCode = unlockedCode,
            UnlockedAccessoryName = accessory is null
                ? string.Empty
                : Localized.T(language, accessory.NameAz, accessory.Name),
            UnlockedAccessoryIcon = accessory?.Icon ?? string.Empty,
            NewMemories = [.. memories.Select(m => MemoryPolicy.ToDto(m, language, pet.Name))],
            PetLeveledUp = leveledUp,
            PetLevel = pet.Level
        };
    }

    /// <summary>Artıq tamamlanmış run üçün yekun — mükafat TƏKRAR tətbiq edilmir.</summary>
    private async Task<PetBrainRunDto> BuildCompletedDtoAsync(
        ExperienceRun run, ChildProfile child, ExperienceTemplate template, CancellationToken ct)
    {
        var language = child.LanguageCode;
        var dto = await ToDtoAsync(run, child, ct);

        var memories = await _db.PetMemories
            .AsNoTracking()
            .Where(m => m.ChildProfileId == child.Id && m.FactKey == template.Key)
            .ToListAsync(ct);

        var unlocked = child.Pet!.UnlockedAccessories.Contains(template.RewardCode)
            ? template.RewardCode
            : string.Empty;

        dto.Summary = BuildSummary(
            template, language, child.Pet, 0, 0, unlocked, memories, leveledUp: false);

        // Təkrar açılışda "yeni əşya!" anı göstərilmir — o, bir dəfəlik andır.
        dto.Summary.UnlockedAccessoryCode = string.Empty;

        // Recap KEŞDƏN gəlir: eyni seçimlər eyni hash verir, deməli təkrar
        // baxış heç nə xərcləmir.
        dto.Summary.Recap = await EnsureRecapAsync(run, ct);

        return dto;
    }

    /// <summary>
    /// Run-un DTO-su. Cari mərhələ tapmacadırsa, tapmaca burada VERİLİR və
    /// saxlanılır — yəni yenilənmə eyni sualı qaytarır.
    ///
    /// <para>Cari mərhələ tapmaca deyilsə, QARŞIDAKI tapmaca da burada verilir
    /// və rəsmi arxa fon növbəsinə düşür. Macəranın start cavabı da bu
    /// metoddan keçir, ona görə AI rəsmi uşaq giriş və seçim mərhələlərini
    /// oynayarkən çəkilir, tapmaca isə açılan kimi oynanır.</para>
    /// </summary>
    private async Task<PetBrainRunDto> ToDtoAsync(
        ExperienceRun run,
        ChildProfile child,
        CancellationToken ct,
        bool showHint = false,
        bool puzzleMissed = false)
    {
        var language = child.LanguageCode;
        var template = TemplateOf(run);

        var dto = new PetBrainRunDto
        {
            RunId = run.Id,
            TemplateKey = run.TemplateKey,
            ExperienceType = run.ExperienceType,
            Theme = run.Theme,
            SceneKey = template?.SceneKey ?? run.Theme,
            Difficulty = run.Difficulty,
            Status = run.Status,
            Title = template?.Title(language) ?? run.TemplateKey,
            CurrentStage = run.CurrentStage,
            StageCount = template?.StageCount ?? 0,
            Choices = [.. run.Choices],
            HintsUsed = run.HintsUsed,
            Mistakes = run.Mistakes,
            StepsTaken = run.CurrentStage,
            EndingKey = run.EndingKey
        };

        if (template is null)
            return dto;

        // Budaqlanan macəra AYRI qurulur: mərhələ siyahısı yox, düyün və yol.
        if (GraphOf(run) is { } graph)
            return await ToGraphDtoAsync(dto, run, child, template, graph, ct, showHint, puzzleMissed);

        if (run.Status != PetBrainRunStatus.Active || run.CurrentStage >= template.StageCount)
            return dto;

        dto.EstimatedSteps = template.StageCount;

        var stage = template.Stages[run.CurrentStage];

        dto.Stage = new PetBrainStageDto
        {
            Index = run.CurrentStage,
            Kind = stage.Kind,
            Prompt = stage.Prompt(language),
            PetLine = stage.PetLine(language)
        };

        if (stage.Kind != PetBrainStageKind.Puzzle)
        {
            dto.Stage.Options = [.. stage.Options.Select(o => ToOptionDto(o, language))];
            dto.UpcomingScene = await UpcomingSceneAsync(run, template, child, ct);
            return dto;
        }

        var issued = await EnsurePuzzleAsync(run, template, child, run.CurrentStage, ct);
        var puzzle = ReadPublic(issued);

        // Cəhd sayı yalnız göstərmək üçündür — ruhlandırıcı mesaj ondan asılıdır.
        puzzle.Attempts = issued.Attempts;

        // Səhnə HƏR OXUNUŞDA yenidən hesablanır, saxlanan məzmundan yox:
        // rəsm gec hazır olanda uşaq növbəti kadrda onu görməlidir. Həndəsə
        // dəyişmir — yalnız fon.
        await ApplySceneAsync(puzzle, issued, template, child, ct);

        // İpucu YALNIZ istənəndə (və ya dəstək rejimində) göndərilir; əks halda
        // sahə boş qalır və klientə heç nə sızmır.
        if (!showHint && issued.HintsUsed == 0 && !issued.Assisted)
            puzzle.Hint = string.Empty;

        dto.Stage.Puzzle = puzzle;
        dto.Stage.SupportsHint = puzzle.HintAvailable;
        dto.Stage.Hint = puzzle.Hint;
        dto.Stage.Prompt = puzzle.Instruction;

        if (puzzleMissed)
            dto.Stage.PetLine = Localized.T(language,
                "Yaxın idi! Bir də bax — mən yanındayam.",
                "So close! Look again — I am right here with you.");

        return dto;
    }

    /// <summary>
    /// Budaqlanan macəranın ekranı.
    ///
    /// <para>Xətti versiyadan üç fərqi var: cari düyün açar ilə göstərilir
    /// (klient onu geri qaytarır), səhnə variantı seçimlərə görə dəyişir və
    /// yol göstəricisi REAL qeydlərdən qurulur — indeks təxminindən yox.</para>
    /// </summary>
    private async Task<PetBrainRunDto> ToGraphDtoAsync(
        PetBrainRunDto dto,
        ExperienceRun run,
        ChildProfile child,
        ExperienceTemplate template,
        ExperienceDefinition graph,
        CancellationToken ct,
        bool showHint,
        bool puzzleMissed)
    {
        var language = child.LanguageCode;

        dto.EstimatedSteps = StoryRuntime.EstimatedTotalSteps(graph);
        dto.StageCount = dto.EstimatedSteps;
        dto.Path = await PathAsync(run, graph, language, ct);

        if (run.Status != PetBrainRunStatus.Active)
            return dto;

        var node = graph.Find(run.CurrentNodeId);

        if (node is null)
            return dto;

        dto.Stage = new PetBrainStageDto
        {
            Index = run.CurrentStage,
            NodeId = node.Id,
            SceneVariant = StoryRuntime.SceneVariantOf(node),
            Kind = node.Kind,
            Prompt = node.Prompt(language),
            PetLine = node.PetLine(language),
            MemoryCallback = MemoryCallbackFor(node, child, template.Theme, language)
        };

        // Nəticə ekranında pet ÖZ səsi ilə cavab verir: hekayənin replikası
        // hamı üçün eynidir, reaksiya isə xarakterə görə dəyişir.
        if (node.Kind == PetBrainStageKind.Consequence)
            dto.Stage.PetReaction = PersonalityVoice.ChoiceReaction(child.Personality, language);

        if (node.Kind != PetBrainStageKind.Puzzle)
        {
            dto.Stage.Options = [.. node.Options.Select(o => ToOptionDto(o, language))];
            dto.UpcomingScene = await UpcomingGraphSceneAsync(run, template, child, graph, node, ct);

            return dto;
        }

        var issued = await EnsurePuzzleAsync(run, template, child, run.CurrentStage, ct, node.PuzzleFamily);
        var puzzle = ReadPublic(issued);

        puzzle.Attempts = issued.Attempts;

        await ApplySceneAsync(puzzle, issued, template, child, ct);

        if (!showHint && issued.HintsUsed == 0 && !issued.Assisted)
            puzzle.Hint = string.Empty;

        dto.Stage.Puzzle = puzzle;
        dto.Stage.SupportsHint = puzzle.HintAvailable;
        dto.Stage.Hint = puzzle.Hint;
        dto.Stage.Prompt = puzzle.Instruction;

        // İpucu TƏKLİFİ də xarakterin səsindədir — kömək istəmək heç bir
        // variantda zəiflik kimi verilmir.
        if (puzzle.HintAvailable)
            dto.Stage.HintOffer = PersonalityVoice.HintOffer(child.Personality, language);

        // Ruhlandırma XARAKTERƏ uyğundur — cəza dili heç bir variantda yoxdur.
        if (puzzleMissed)
            dto.Stage.PetLine = PersonalityVoice.Encouragement(child.Personality, language);

        return dto;
    }

    /// <summary>
    /// Uşağın keçdiyi yol — REAL nəticə sətirlərindən.
    ///
    /// <para>Düz <c>Choices</c> siyahısından qurmaq budaqlanan hekayədə səhv
    /// olardı: orada "continue" və "solved" açarları seçimlərlə qarışır və
    /// hansı elementin hansı addıma aid olduğu bilinmir.</para>
    /// </summary>
    private async Task<List<PetBrainPathStepDto>> PathAsync(
        ExperienceRun run, ExperienceDefinition graph, string language, CancellationToken ct)
    {
        var outcomes = await _db.RunStageOutcomes
            .AsNoTracking()
            .Where(o => o.ExperienceRunId == run.Id)
            .OrderBy(o => o.StageOrdinal)
            .ToListAsync(ct);

        List<PetBrainPathStepDto> path = [];

        foreach (var outcome in outcomes)
        {
            var node = graph.Find(outcome.NodeId);

            if (node is null)
                continue;

            var option = outcome.SelectedOptionKeys.Count > 0
                ? node.Options.FirstOrDefault(o =>
                    string.Equals(o.Key, outcome.SelectedOptionKeys[0], StringComparison.Ordinal))
                : null;

            path.Add(new PetBrainPathStepDto
            {
                Ordinal = path.Count + 1,
                Icon = option?.Icon ?? IconFor(node.Kind),
                Label = option?.Label(language) ?? node.Prompt(language),
                WasChoice = option is not null
            });
        }

        // Sonluğun öz nəticə sətri yoxdur (ondan sonra addım atılmır), amma
        // uşaq yolunun sonunu GÖRMƏLİDİR.
        if (graph.Find(run.CurrentNodeId) is { IsEnding: true } ending)
            path.Add(new PetBrainPathStepDto
            {
                Ordinal = path.Count + 1,
                Icon = IconFor(PetBrainStageKind.Ending),
                Label = ending.Prompt(language),
                WasChoice = false
            });

        return path;
    }

    private static string IconFor(PetBrainStageKind kind) => kind switch
    {
        PetBrainStageKind.Puzzle => "🧩",
        PetBrainStageKind.Consequence => "✨",
        PetBrainStageKind.Ending => "🏁",
        _ => "•"
    };

    /// <summary>
    /// Düyünün istədiyi yaddaş çağırışı — YALNIZ təsdiqlənmiş fakt.
    ///
    /// <para>Pet burada sərbəst cümlə qurmur: xatirə strukturludur və mətn
    /// <see cref="MemoryPolicy.Render"/> ilə uşağın dilində yazılır. Uyğun
    /// xatirə yoxdursa cümlə də yoxdur — pet uydurmur.</para>
    /// </summary>
    private string MemoryCallbackFor(
        ExperienceNode node, ChildProfile child, string theme, string language)
    {
        if (string.IsNullOrEmpty(node.MemoryCallbackKey) || child.Pet is null)
            return string.Empty;

        // Seçim anında SEÇİM xatirəsi işə yarayır: bal mövzu uyğunluğunu,
        // niyyəti, yeniliyi, inamı və təkrar cəzasını birlikdə çəkir.
        var memory = MemoryPolicy
            .Retrieve(child.Memories, _clock.GetUtcNow().UtcDateTime, theme, "choice", limit: 1)
            .FirstOrDefault();

        if (memory is null)
            return string.Empty;

        _telemetry.MemoryCallback("shown", memory.Kind);

        return MemoryPolicy.Render(memory, language, child.Pet.Name);
    }

    /// <summary>
    /// Qrafda QARŞIDAKI tapmacanın səhnəsi.
    ///
    /// <para>Yalnız növbəti düyün BİRMƏNALI olanda hazırlanır: budaqda hansı
    /// tapmacanın gələcəyi hələ məlum deyil, boş yerə pullu rəsm sorğusu
    /// göndərmək isə yolverilməzdir.</para>
    /// </summary>
    private async Task<PetBrainUpcomingSceneDto?> UpcomingGraphSceneAsync(
        ExperienceRun run,
        ExperienceTemplate template,
        ChildProfile child,
        ExperienceDefinition graph,
        ExperienceNode node,
        CancellationToken ct)
    {
        if (StoryRuntime.UpcomingPuzzle(graph, node) is not { } upcoming)
            return null;

        var (next, depth) = upcoming;

        var issued = await EnsurePuzzleAsync(
            run, template, child, run.CurrentStage + depth, ct, next.PuzzleFamily);

        var scene = await SceneOfAsync(issued, template, child, ct);

        return new PetBrainUpcomingSceneDto
        {
            PuzzleId = issued.Id,
            IllustrationStatus = scene?.Status ?? PetBrainIllustrationStatus.Fallback
        };
    }

    // ==================== Tapmaca ====================

    /// <summary>
    /// Qarşıdakı tapmacanı indidən verir və səhnəsinin vəziyyətini qaytarır;
    /// qarşıda tapmaca yoxdursa <c>null</c>.
    ///
    /// <para>Model burada gözlənilmir: <see cref="EnsurePuzzleAsync"/> yalnız
    /// deterministik tapmacanı saxlayır və rəsm işini növbəyə verir. Sual
    /// verildiyi an sabitlənir, ona görə uşaq tapmacaya çatanda EYNİ sualı və
    /// artıq çəkilmiş (və ya çəkilməkdə olan) EYNİ səhnəni görür.</para>
    /// </summary>
    private async Task<PetBrainUpcomingSceneDto?> UpcomingSceneAsync(
        ExperienceRun run, ExperienceTemplate template, ChildProfile child, CancellationToken ct)
    {
        if (NextPuzzleStage(template, run.CurrentStage) is not { } stageIndex)
            return null;

        var issued = await EnsurePuzzleAsync(run, template, child, stageIndex, ct);
        var scene = await SceneOfAsync(issued, template, child, ct);

        return new PetBrainUpcomingSceneDto
        {
            PuzzleId = issued.Id,
            IllustrationStatus = scene?.Status ?? PetBrainIllustrationStatus.Fallback
        };
    }

    private static int? NextPuzzleStage(ExperienceTemplate template, int fromStage)
    {
        for (var stageIndex = fromStage; stageIndex < template.StageCount; stageIndex++)
        {
            if (template.Stages[stageIndex].Kind == PetBrainStageKind.Puzzle)
                return stageIndex;
        }

        return null;
    }

    /// <summary>
    /// Verilən mərhələ üçün tapmacanı gətirir; yoxdursa YARADIR və saxlayır.
    ///
    /// <para>Saxlanması üç şey üçün vacibdir: yenilənmədən sonra EYNİ sual
    /// qayıtmalıdır, doğru həll yalnız serverdə qalmalıdır, təkrar isə barmaq
    /// izi ilə tanınmalıdır.</para>
    ///
    /// <para>İki eyni vaxtlı sorğu eyni anda yaratmağa çalışsa, bazadakı unikal
    /// indeks ikincisini dayandırır — o zaman mövcud sətir oxunur, yəni uşaq
    /// sualın dəyişdiyini görmür.</para>
    /// </summary>
    private async Task<IssuedPuzzle> EnsurePuzzleAsync(
        ExperienceRun run,
        ExperienceTemplate template,
        ChildProfile child,
        int stageIndex,
        CancellationToken ct,
        string preferredFamily = "")
    {
        var existing = await _db.IssuedPuzzles
            .FirstOrDefaultAsync(p => p.ExperienceRunId == run.Id && p.StageIndex == stageIndex, ct);

        if (existing is not null)
        {
            // Kataloq dəyişəndə (mexanika silinəndə, açar adlananda) köhnə
            // sətir OXUNMAZ qalır. Uşaq bunun günahkarı deyil: yarımçıq
            // macərası 500 xətası ilə bitməməlidir, ona görə oxunmayan sətir
            // atılır və mərhələ üçün yeni tapmaca verilir.
            //
            // Bu, «asan sual ovlamağa» qapı açmır: şərt uşağın nəzarətində
            // deyil — o, ancaq bizim buraxdığımız kataloq dəyişikliyi ilə
            // yaranır və hər mərhələ üçün bir dəfə işləyir.
            if (IsReadable(existing))
                return existing;

            _logger.LogWarning(
                "PetBrain: köhnə tapmaca atılır (run {RunId}, mərhələ {Stage}, şablon {Blueprint}).",
                run.Id, existing.StageIndex, existing.BlueprintKey);

            _db.IssuedPuzzles.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }

        var context = await BuildPuzzleContextAsync(run, template, child, stageIndex, preferredFamily, ct);
        var generated = _puzzles.Generate(context);

        var now = _clock.GetUtcNow().UtcDateTime;

        // Səhnə tapmaca QURULANDAN SONRA açılır və ona heç nə əlavə etmir:
        // rəsm yalnız fondur. Model burada çağırılmır — sətir açılır, iş isə
        // arxa fona verilir, yəni uşaq gözləmir.
        var sceneSpec = SceneSpecFor(generated.Blueprint, template, child);

        if (await _illustrations.EnsureRowAsync(sceneSpec, ct) is { Status: PetBrainIllustrationStatus.Pending })
            _sceneQueue.Enqueue(sceneSpec);

        // Alt mətn NƏZARƏTLİ şablondandır — modelin çıxışından yox.
        generated.Public.Scene.AltText = sceneSpec.AltText();
        generated.Public.Scene.OverlayLayout = generated.Blueprint.Key;

        // Id ƏVVƏLCƏ verilir: uşağa gedən məzmun onu daşımalıdır, yəni
        // seriallaşdırmadan əvvəl məlum olmalıdır.
        var puzzleId = Guid.NewGuid();
        generated.Public.PuzzleId = puzzleId;

        var issued = new IssuedPuzzle
        {
            Id = puzzleId,
            ChildProfileId = child.Id,
            ExperienceRunId = run.Id,
            StageIndex = stageIndex,
            BlueprintKey = generated.Blueprint.Key,
            BlueprintVersion = generated.Blueprint.Version,
            GeneratorVersion = PuzzleSeed.GeneratorVersion,
            Mechanic = generated.Blueprint.Mechanic,
            Seed = generated.SeedHex,
            PublicPayload = JsonSerializer.Serialize(generated.Public, PuzzleJson),
            PrivateSolution = JsonSerializer.Serialize(generated.Solution, PuzzleJson),
            ContentSignature = generated.Signature,
            SceneSpecHash = sceneSpec.Hash(),
            Difficulty = run.Difficulty,
            Assisted = run.Assisted,
            Status = PetBrainPuzzleStatus.Issued,
            IssuedAt = now
        };

        _db.IssuedPuzzles.Add(issued);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Başqa sorğu qabaqladı: öz namizədimizi ataraq mövcud tapmacanı oxuyuruq.
            _db.Entry(issued).State = EntityState.Detached;

            var winner = await _db.IssuedPuzzles
                .FirstOrDefaultAsync(p => p.ExperienceRunId == run.Id && p.StageIndex == stageIndex, ct);

            // Sətir yoxdursa, xəta bu tapmacadan DEYİL, izləyicidəki başqa bir
            // yazılmamış sətirdən gəlib. Onu udmaq səhvi gizlədərdi.
            if (winner is null)
                throw;

            return winner;
        }

        return issued;
    }

    private async Task<PuzzleGenerationContext> BuildPuzzleContextAsync(
        ExperienceRun run,
        ExperienceTemplate template,
        ChildProfile child,
        int stageIndex,
        string preferredFamily,
        CancellationToken ct)
    {
        // Son tapmacaların barmaq izləri — eyni sual dalbadal təkrarlanmasın.
        var recent = await _db.IssuedPuzzles
            .AsNoTracking()
            .Where(p => p.ChildProfileId == child.Id)
            .OrderByDescending(p => p.IssuedAt)
            .Take(RecentPuzzleWindow)
            .Select(p => p.ContentSignature)
            .ToListAsync(ct);

        var averageRating = child.SkillMasteries.Count > 0
            ? (int)child.SkillMasteries.Average(m => m.Rating)
            : AdaptiveEngine.StartingRating;

        return new PuzzleGenerationContext(
            ChildId: child.Id,
            RunId: run.Id,
            StageIndex: stageIndex,
            Age: child.Age,
            Language: child.LanguageCode,
            TemplateKey: template.Key,
            ExperienceType: template.Type,
            Theme: template.Theme,
            Interests: ScoresOf(child, PetBrainTraitCategory.Interest),
            PlayStyles: ScoresOf(child, PetBrainTraitCategory.PlayStyle),
            Difficulty: run.Difficulty,
            MasteryTargetDifficulty: AdaptiveEngine.TargetDifficulty(averageRating),
            MechanicTiers: await MechanicTiersAsync(child, ct),
            Assisted: run.Assisted,
            RecentSignatures: recent.ToHashSet(StringComparer.Ordinal),
            PreferredBlueprintKey: preferredFamily);
    }

    /// <summary>
    /// Mexanika üzrə pillə — hər ailənin ÖZ tarixçəsindən.
    ///
    /// <para>Qlobal pillə son tamamlanmış macəradan gəlir və hekayənin ritmini
    /// seçir. Tapmacanın özü isə başqa sualdır: uşaq marşrutda üç dəfə ipucusuz
    /// keçib, sıralamanı isə heç görməyib. Tək rəqəm bunu gizlədirdi — biri
    /// darıxdırıcı, digəri həddindən çətin olurdu.</para>
    ///
    /// <para>Ayrıca cədvəl yaradılmır: verilmiş tapmacaların özü onsuz da
    /// mexanikanı, pilləni, cəhdi və ipucunu saxlayır.</para>
    /// </summary>
    private async Task<Dictionary<string, PetBrainDifficulty>> MechanicTiersAsync(
        ChildProfile child, CancellationToken ct)
    {
        var recent = await _db.IssuedPuzzles
            .AsNoTracking()
            .Where(p => p.ChildProfileId == child.Id && p.Status == PetBrainPuzzleStatus.Solved)
            .OrderByDescending(p => p.SolvedAt)
            .Take(MechanicHistoryWindow)
            .Select(p => new { p.BlueprintKey, p.Difficulty, p.Attempts, p.HintsUsed, p.SolvedAt })
            .ToListAsync(ct);

        Dictionary<string, PetBrainDifficulty> tiers = new(StringComparer.Ordinal);

        foreach (var group in recent.GroupBy(p => p.BlueprintKey, StringComparer.Ordinal))
        {
            var last = group.OrderByDescending(p => p.SolvedAt).First();

            // Nəticə TAPMACANIN öz ölçüsündən qurulur: neçə cəhd, neçə ipucu.
            // Macəranın balı burada yoxdur — o, başqa sualın cavabıdır.
            var score = Math.Clamp(100 - ((last.Attempts - 1) * 25) - (last.HintsUsed * 15), 0, 100);

            tiers[group.Key] = ExperienceDifficulty.Next(
                new RunPerformance(last.Difficulty, score, last.HintsUsed, Math.Max(0, last.Attempts - 1)),
                child.Age);
        }

        return tiers;
    }

    /// <summary>
    /// Uşağın ÖZ tapmacasının rəsm vəziyyəti.
    ///
    /// <para>Sahiblik SORĞUNUN İÇİNDƏDİR: yad uşağın tapmacası ümumiyyətlə
    /// tapılmır, yəni "var, amma sənin deyil" fərqi görünmür. Səhnəsi olmayan
    /// tapmaca və itmiş sətir <c>Fallback</c> sayılır — rəsm heç vaxt
    /// gəlməyəcək.</para>
    /// </summary>
    public async Task<PuzzleIllustrationLookup?> GetIllustrationAsync(
        Guid childId, Guid puzzleId, CancellationToken ct = default)
    {
        var puzzle = await _db.IssuedPuzzles
            .AsNoTracking()
            .Where(p => p.Id == puzzleId && p.ChildProfileId == childId)
            .Select(p => new { p.SceneSpecHash })
            .FirstOrDefaultAsync(ct);

        if (puzzle is null)
            return null;

        var row = string.IsNullOrEmpty(puzzle.SceneSpecHash)
            ? null
            : await _db.PuzzleIllustrations
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.SceneSpecHash == puzzle.SceneSpecHash, ct);

        if (row is null)
            return new PuzzleIllustrationLookup(PetBrainIllustrationStatus.Fallback, null);

        return new PuzzleIllustrationLookup(
            row.Status,
            row.Status == PetBrainIllustrationStatus.Ready && !string.IsNullOrEmpty(row.AssetKey)
                ? row.AssetKey
                : null);
    }

    private async Task ApplySceneAsync(
        PetBrainPuzzleDto puzzle,
        IssuedPuzzle issued,
        ExperienceTemplate template,
        ChildProfile child,
        CancellationToken ct)
    {
        if (await SceneOfAsync(issued, template, child, ct) is not { } row)
            return;

        puzzle.Scene.IllustrationStatus = row.Status;

        puzzle.Scene.AssetUrl = row.Status == PetBrainIllustrationStatus.Ready
            ? $"/api/pet-brain/puzzles/{issued.Id}/illustration"
            : string.Empty;
    }

    /// <summary>
    /// Tapmacanın səhnə sətri. Sətir hələ <c>Pending</c>-dirsə növbəyə YENİDƏN
    /// qoyulur.
    ///
    /// <para>Növbə prosesdaxilidir və yenidən başlatmada itir; bazadakı sətir
    /// isə qalır. Ona görə gözləyən səhnə hər oxunuşda yenidən növbəyə düşür —
    /// növbə özü təkrarı süzür, iş isə ikinci dəfə başlamır (sətir artıq
    /// <c>Pending</c> deyilsə işçi dərhal qayıdır).</para>
    /// </summary>
    private async Task<PuzzleIllustration?> SceneOfAsync(
        IssuedPuzzle issued, ExperienceTemplate template, ChildProfile child, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(issued.SceneSpecHash))
            return null;

        var row = await _db.PuzzleIllustrations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.SceneSpecHash == issued.SceneSpecHash, ct);

        if (row is { Status: PetBrainIllustrationStatus.Pending } &&
            PuzzleBlueprintCatalog.Find(issued.BlueprintKey) is { } blueprint)
            _sceneQueue.Enqueue(SceneSpecFor(blueprint, template, child));

        return row;
    }

    /// <summary>
    /// Səhnə təsviri — YALNIZ təsdiqlənmiş kataloq sahələrindən.
    ///
    /// <para>Pet-in adı QƏSDƏN yoxdur: onu uşaq yazır, deməli sərbəst mətndir
    /// və modelə getməməlidir. Növ isə qapalı siyahıdandır.</para>
    /// </summary>
    private static PuzzleSceneSpec SceneSpecFor(
        PuzzleBlueprint blueprint, ExperienceTemplate template, ChildProfile child) =>
        PuzzleSceneSpec.For(
            blueprint, child.LanguageCode, template, child.Pet?.Species ?? string.Empty);

    /// <summary>
    /// Saxlanan tapmaca hələ də bu buraxılışla oxunurmu: şablon kataloqdadır və
    /// JSON cari müqaviləyə uyğundur.
    /// </summary>
    private static bool IsReadable(IssuedPuzzle issued)
    {
        var blueprint = PuzzleBlueprintCatalog.Find(issued.BlueprintKey);

        if (blueprint is null)
            return false;

        // Versiya uyğunsuzluğu = qaydalar dəyişib.
        //
        // Saxlanan tapmacanı YENİ qaydalarla qiymətləndirmək ən pis variantdır:
        // uşaq köhnə lövhəyə baxır, server isə başqa şərtlə yoxlayır — doğru
        // cavab səhv sayıla bilər. Ona görə sətir oxunmaz sayılır və mərhələ
        // üçün cari qaydalarla YENİ tapmaca verilir.
        if (blueprint.Version != issued.BlueprintVersion)
            return false;

        try
        {
            return JsonSerializer.Deserialize<PetBrainPuzzleDto>(issued.PublicPayload, PuzzleJson) is not null
                && JsonSerializer.Deserialize<PuzzleSolution>(issued.PrivateSolution, PuzzleJson) is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static PetBrainPuzzleDto ReadPublic(IssuedPuzzle issued) =>
        JsonSerializer.Deserialize<PetBrainPuzzleDto>(issued.PublicPayload, PuzzleJson)
        ?? new PetBrainPuzzleDto { PuzzleId = issued.Id };

    private static PuzzleSolution ReadSolution(IssuedPuzzle issued) =>
        JsonSerializer.Deserialize<PuzzleSolution>(issued.PrivateSolution, PuzzleJson)
        ?? new PuzzleSolution([], PetBrainAnswerKind.SelectIds);

    private static PetBrainOptionDto ToOptionDto(ExperienceOption option, string language) => new()
    {
        Key = option.Key,
        Label = option.Label(language),
        Icon = option.Icon,
        Detail = option.Detail(language)
    };

    /// <summary>
    /// Bir kartı uşağın gördüyü tövsiyəyə çevirir.
    ///
    /// <para>Səbəb KODLARI burada cümləyə çevrilir: jurnalda kod qalır, ekrana
    /// isə uşağın dilində mətn gedir. Bal parçalanması valideyn/münsif üçün
    /// ayrıca daşınır — uşaq ekranında rəqəm göstərilmir.</para>
    /// </summary>
    private async Task<PetBrainRecommendationDto> ToDtoAsync(
        RecommendationCard card,
        RecommendationDecision record,
        ChildProfile child,
        PetMindContext mind,
        CancellationToken ct)
    {
        var language = child.LanguageCode;
        var candidate = card.Candidate;
        var template = candidate.Template;

        // Model mətni valideyn tərəfindən AYRICA söndürülə bilir; söndürüləndə
        // deterministik şablon mətni qalır və ekran tam işləyir.
        var narrative = mind.Personalization.AiNarrativeEnabled
            ? await _narrative.DescribeAsync(new NarrativeContext(
                ChildId: child.Id,
                Language: language,
                AgeBand: AgeBand(child.Age),
                Template: template,
                Difficulty: mind.Difficulty,
                PetName: child.Pet?.Name ?? "Pet",
                MemoryKeys: [.. child.Memories.Select(m => m.FactKey).Distinct().Take(5)]), ct)
            : new ExperienceNarrative(
                template.Title(language), template.Intro(language), ExperienceNarrative.TemplateSource);

        var topicLabel = TraitKeys.Label(template.PrimaryInterest, language);
        var mechanicLabel = MechanicKeys.Label(template.PrimaryMechanic ?? string.Empty, language);

        var dto = new PetBrainRecommendationDto
        {
            DecisionId = record.Id,
            TemplateKey = template.Key,
            ExperienceType = template.Type,
            Theme = template.Theme,
            ActivityType = template.ActivityType,
            Difficulty = mind.Difficulty,
            TargetMinutes = template.TargetMinutes,
            Title = narrative.Title,
            Intro = narrative.Intro,
            Icon = template.Icon,
            RewardCode = template.RewardCode,
            AlreadyCompleted = mind.CompletedTemplates.Contains(template.Key),
            NarrativeSource = narrative.Source,
            PetLine = PersonalityVoice.RecommendationLine(mind.Personality, language, narrative.Title),
            Slot = card.Slot,
            SlotLabel = RecommendationVoice.SlotLabel(card.Slot, language),
            ReasonCodes = [.. candidate.Why],
            Reasons =
            [
                .. candidate.Why.Select(reason => RecommendationVoice.Reason(
                    reason, language, topicLabel, mechanicLabel, template.TargetMinutes))
            ],
            PersonalizationFactors = PersonalizationMapper.FactorsOf(candidate, _policy, language),
            ChallengeLabel = RecommendationVoice.ChallengeLabel(mind.Difficulty, language),
            SupportAvailable = template.HasPuzzle,
            SupportLabel = RecommendationVoice.SupportLabel(mind.Personalization.Support.Style, language),
            RewardFlavor = template.RewardFlavor,
            RewardLabel = RecommendationVoice.RewardLabel(template.RewardFlavor, language),
            WasExploration = card.WasExploration,
            PolicyVersion = record.PolicyVersion,
            CanGiveFeedback = mind.Personalization.Enabled
        };

        var used = await ShowAnotherCountAsync(child.Id, record.CreatedAt, ct);
        dto.CanShowAnother = card.Slot == PetBrainRecommendationSlot.Primary
                             && used < _v2.MaxShowAnotherPerSession;

        return dto;
    }

    private async Task<PetBrainDebugDto> BuildDebugAsync(
        Guid childId,
        RecommendationSet? set,
        PetBrainDifficulty difficulty,
        RunPerformance? lastPerformance,
        string language,
        string narrativeSource,
        CancellationToken ct)
    {
        var events = await _db.BehaviorEvents
            .AsNoTracking()
            .Where(e => e.ChildProfileId == childId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(DebugEventLimit)
            .Select(e => new PetBrainEventDto
            {
                Type = e.Type,
                Source = e.Source,
                OccurredAt = e.OccurredAt
            })
            .ToListAsync(ct);

        return new PetBrainDebugDto
        {
            Difficulty = difficulty,
            DifficultySignal = ExperienceDifficulty.Signal(lastPerformance, language),
            RecentEvents = events,
            NarrativeSource = narrativeSource,
            Candidates =
            [
                .. (set?.Ranked ?? []).Select(c => new PetBrainCandidateDto
                {
                    TemplateKey = c.Key,
                    Theme = c.Theme,
                    FitScore = c.TopicFit,
                    NoveltyScore = c.NoveltyValue,
                    SurpriseScore = c.MechanicFit,
                    TotalScore = (int)Math.Round(c.Total, MidpointRounding.AwayFromZero),
                    Selected = set?.Primary is not null && c.Key == set.Primary.Candidate.Key
                })
            ],
            FilteredCandidates =
            [
                .. (set?.Filtered ?? []).Select(f => new PetBrainFilteredCandidateDto
                {
                    TemplateKey = f.TemplateKey,
                    Reason = f.Reason
                })
            ]
        };
    }

    private static string AgeBand(int age) => age switch
    {
        <= 6 => "5-6",
        <= 8 => "7-8",
        _ => "9-10"
    };

    private static ServiceResult<T> Disabled<T>() =>
        ServiceResult<T>.NotFound(Localized.T("Bu bölmə hazırda bağlıdır.", "This section is currently closed."));
}
