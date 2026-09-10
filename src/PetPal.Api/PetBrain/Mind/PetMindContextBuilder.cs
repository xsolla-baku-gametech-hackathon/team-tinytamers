using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Learning;
using PetPal.Api.Pets;
using PetPal.Api.Progress;
using PetPal.Api.Missions;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Mind;

/// <summary>
/// Bu sessiyada uşağın "indi yox" dediyi şablonlar.
///
/// <para>Sorğular arası yaşamalıdır (uşaq kartı bir neçə dəfə dəyişə bilər),
/// amma BAZAYA yazılmamalıdır: bu, uşağın üstünlüyü deyil, ötəri qərarıdır və
/// sabah onu izləməməlidir. Ona görə yaddaşda, uşaq başına və qısa ömürlü
/// saxlanılır.</para>
/// </summary>
public sealed class DeclinedRecommendations
{
    /// <summary>Bir sessiyanın praktiki uzunluğu.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(45);

    /// <summary>Bir uşaq üçün yadda saxlanan ən çox rədd — sonsuz siyahı yaranmasın.</summary>
    public const int MaxPerChild = 12;

    private readonly Dictionary<Guid, List<(string TemplateKey, DateTime At)>> _declines = [];
    private readonly Lock _gate = new();

    public void Record(Guid childId, string templateKey, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
            return;

        lock (_gate)
        {
            if (!_declines.TryGetValue(childId, out var list))
                _declines[childId] = list = [];

            list.RemoveAll(entry => now - entry.At > Window);
            list.RemoveAll(entry => string.Equals(entry.TemplateKey, templateKey, StringComparison.Ordinal));
            list.Add((templateKey, now));

            if (list.Count > MaxPerChild)
                list.RemoveRange(0, list.Count - MaxPerChild);
        }
    }

    public IReadOnlySet<string> For(Guid childId, DateTime now)
    {
        lock (_gate)
        {
            if (!_declines.TryGetValue(childId, out var list))
                return new HashSet<string>(StringComparer.Ordinal);

            list.RemoveAll(entry => now - entry.At > Window);

            return list.Select(entry => entry.TemplateKey).ToHashSet(StringComparer.Ordinal);
        }
    }

    /// <summary>Macəra başlayanda sessiya təmizlənir — yeni dövr başlayır.</summary>
    public void Clear(Guid childId)
    {
        lock (_gate)
            _declines.Remove(childId);
    }
}

/// <summary>
/// <see cref="PetMindContext"/>-i quran YEGANƏ yer.
///
/// <para>Əvvəllər hər ekran öz kontekstini özü yığırdı: Pet Brain xassələrə və
/// run tarixçəsinə baxırdı, ana ekran ayrıca sorğu edirdi, söhbət isə yalnız
/// qulluq statlarını bilirdi. Nəticədə pet bir ekranda ac, digərində macərəya
/// hazır görünürdü. İndi hamısı buradan oxuyur.</para>
/// </summary>
public sealed class PetMindContextBuilder
{
    /// <summary>Direktorun baxdığı son run sayı.</summary>
    private const int RecentRunWindow = 8;

    /// <summary>Qərar üçün gətirilən ən çox xatirə.</summary>
    private const int MemoryWindow = 12;

    /// <summary>Ekran vaxtının "az qalıb" həddi (dəqiqə).</summary>
    private const int ScreenTimeEndingMinutes = 5;

    /// <summary>Ümumi inam hesablanarkən baxılan ƏN GÜCLÜ açar sayı.</summary>
    private const int ConfidenceSampleSize = 5;

    private readonly AppDbContext _db;
    private readonly IDailyGoalService _dailyGoals;
    private readonly DeclinedRecommendations _declined;
    private readonly TimeProvider _clock;
    private readonly ScreenTimeOptions _screenTime;

    public PetMindContextBuilder(
        AppDbContext db,
        IDailyGoalService dailyGoals,
        DeclinedRecommendations declined,
        TimeProvider clock,
        IOptions<ScreenTimeOptions> screenTime)
    {
        _db = db;
        _dailyGoals = dailyGoals;
        _declined = declined;
        _clock = clock;
        _screenTime = screenTime.Value;
    }

    /// <summary>
    /// Tam kontekst. <paramref name="child"/> <c>Pet</c>, <c>Traits</c> və
    /// <c>Memories</c> ilə yüklənmiş olmalıdır.
    /// </summary>
    public async Task<PetMindContext> BuildAsync(ChildProfile child, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var pet = child.Pet;

        var goal = await _dailyGoals.GetOrCreateTodayAsync(child, ct);
        var screenTimeState = ScreenTimeGuard.Evaluate(child, goal, now, _screenTime.Enforced);

        var outcomes = await RecentOutcomesAsync(child.Id, ct);

        var completed = await _db.ExperienceRuns
            .AsNoTracking()
            .Where(r => r.ChildProfileId == child.Id && r.Status == PetBrainRunStatus.Completed)
            .Select(r => r.TemplateKey)
            .Distinct()
            .ToListAsync(ct);

        var unfinished = await _db.ExperienceRuns
            .AsNoTracking()
            .Where(r => r.ChildProfileId == child.Id
                        && (r.Status == PetBrainRunStatus.Active || r.Status == PetBrainRunStatus.Paused))
            .OrderByDescending(r => r.StartedAt)
            .Select(r => r.TemplateKey)
            .FirstOrDefaultAsync(ct);

        var missions = await _db.ChildMissions
            .AsNoTracking()
            .Where(m => m.ChildProfileId == child.Id && m.Status == MissionStatus.Active)
            .Select(m => m.Mission.Code)
            .Take(5)
            .ToListAsync(ct);

        var interests = ScoresOf(child, PetBrainTraitCategory.Interest, now);
        var playStyles = ScoresOf(child, PetBrainTraitCategory.PlayStyle, now);
        var mechanics = ScoresOf(child, PetBrainTraitCategory.Mechanic, now);

        var settings = child.PersonalizationSettings
                       ?? await _db.PersonalizationSettings
                           .AsNoTracking()
                           .FirstOrDefaultAsync(s => s.ChildProfileId == child.Id, ct);

        var masteries = child.MechanicMasteries.Count > 0
            ? child.MechanicMasteries.ToList()
            : await _db.MechanicMasteries
                .AsNoTracking()
                .Where(m => m.ChildProfileId == child.Id)
                .ToListAsync(ct);

        var preferences = child.ContentPreferences.Count > 0
            ? child.ContentPreferences.ToList()
            : await _db.ContentPreferences
                .AsNoTracking()
                .Where(p => p.ChildProfileId == child.Id)
                .ToListAsync(ct);

        var personalization = PersonalizationProfileFactory.Build(settings, outcomes);

        return new PetMindContext(
            ChildId: child.Id,
            AgeBand: AgeBands.Of(child.Age),
            Language: child.LanguageCode,
            AgeForSafetyLimits: child.Age,
            PetIsHatched: pet?.HatchedAt is not null,
            PetSpecies: pet?.Species ?? string.Empty,
            PetStage: pet is null ? PetStage.Egg : PetProgression.StageFor(pet),
            Mood: pet is null ? PetMood.Neutral : PetProgression.MoodFor(pet),
            Happiness: Band(pet?.Happiness),
            Energy: Band(pet?.Energy),
            Fullness: Band(pet?.Fullness),
            Cleanliness: Band(pet?.Cleanliness),
            Bond: BondRules.Clamp(pet?.Bond ?? 0),
            BondTier: BondTiers.Of(pet?.Bond ?? 0),
            Personality: child.Personality,
            Interests: interests,
            PlayStyles: playStyles,
            InterestConfidence: ConfidenceOf(child, PetBrainTraitCategory.Interest, now),
            PlayStyleConfidence: ConfidenceOf(child, PetBrainTraitCategory.PlayStyle, now),
            Mechanics: mechanics,
            MechanicConfidence: ConfidenceOf(child, PetBrainTraitCategory.Mechanic, now),
            MechanicMastery: masteries.ToDictionary(
                m => m.Mechanic, m => m.EstimatedLevel, StringComparer.Ordinal),
            MechanicChallengeBand: masteries.ToDictionary(
                m => m.Mechanic, MechanicMasteryRules.BandFor, StringComparer.Ordinal),
            RecentOutcomes: outcomes,
            CompletedTemplates: completed.ToHashSet(StringComparer.Ordinal),
            Memories: await MemoriesAsync(child, now, ct),
            ActiveMissionKeys: missions,
            DailyGoal: GoalBand(goal),
            Weather: await WeatherAsync(child.Id, now, ct),
            ScreenTime: ScreenTimeBand(screenTimeState, child, goal),
            SessionBucket: BucketOf(now, child),
            SinceLastInteraction: await SinceLastInteractionAsync(child.Id, now, ct),
            UnfinishedTemplateKey: unfinished,
            DeclinedTemplates: _declined.For(child.Id, now),
            Difficulty: DifficultyFor(child, outcomes),
            Personalization: personalization,
            BlockedThemes: ContentPreferenceRules.BlockedKeys(
                preferences, PetBrainContentScope.Theme, now),
            BlockedTemplates: ContentPreferenceRules.BlockedKeys(
                preferences, PetBrainContentScope.Template, now),
            ShowLessThemes: ContentPreferenceRules.ShowLessKeys(
                preferences, PetBrainContentScope.Theme, now),
            ShowLessTemplates: ContentPreferenceRules.ShowLessKeys(
                preferences, PetBrainContentScope.Template, now),
            LikedThemes: ContentPreferenceRules.LikedKeys(
                preferences, PetBrainContentScope.Theme, now),
            LikedTemplates: ContentPreferenceRules.LikedKeys(
                preferences, PetBrainContentScope.Template, now),
            ProfileConfidence: OverallConfidence(child, now));
    }

    /// <summary>
    /// Profilin ÜMUMİ inamı (0–100).
    ///
    /// <para>Açarların ortalaması DEYİL: uşaq haqqında nə qədər şey bildiyimiz
    /// ən güclü bir neçə siqnalla ölçülür. Otuz açarın ortalaması yeni profildə
    /// həmişə sıfıra yaxın qalar və sistem heç vaxt «artıq tanıyıram» deyə
    /// bilməzdi.</para>
    /// </summary>
    private static int OverallConfidence(ChildProfile child, DateTime now)
    {
        var scores = child.Traits
            .Select(t => TraitEvidence.Confidence(t, now))
            .OrderByDescending(value => value)
            .Take(ConfidenceSampleSize)
            .ToList();

        if (scores.Count == 0)
            return 0;

        return Math.Clamp((int)Math.Round(scores.Sum() / (double)ConfidenceSampleSize), 0, 100);
    }

    /// <summary>
    /// Uşağın xassələri — <b>köhnəlmə tətbiq olunmuş</b> effektiv bal.
    ///
    /// <para>Saxlanan bal monotondur: hər müsbət hadisə onu qaldırır, heç nə
    /// endirmir. Ona görə direktora verilən dəyər burada hesablanır — köhnə,
    /// təkrarlanmayan siqnal öz çəkisini tədricən itirir və profillər zamanla
    /// bir-birinə oxşamır (bax <see cref="TraitEvidence.EffectiveScore"/>).</para>
    ///
    /// <para>Köhnəlmə bazaya YAZILMIR: uşağın tarixçəsi toxunulmazdır və fon
    /// işçisi lazım gəlmir — dəyər hər oxunuşda saatdan hesablanır.</para>
    /// </summary>
    private static Dictionary<string, int> ScoresOf(
        ChildProfile child, PetBrainTraitCategory category, DateTime now) =>
        child.Traits
            .Where(t => t.Category == category)
            .ToDictionary(t => t.Key, t => TraitEvidence.EffectiveScore(t, now), StringComparer.Ordinal);

    /// <summary>Açar üzrə inam — nümayiş panelində və gələcək planlayıcıda.</summary>
    private static Dictionary<string, int> ConfidenceOf(
        ChildProfile child, PetBrainTraitCategory category, DateTime now) =>
        child.Traits
            .Where(t => t.Category == category)
            .ToDictionary(t => t.Key, t => TraitEvidence.Confidence(t, now), StringComparer.Ordinal);

    private async Task<IReadOnlyList<MindRunOutcome>> RecentOutcomesAsync(Guid childId, CancellationToken ct)
    {
        var runs = await _db.ExperienceRuns
            .AsNoTracking()
            .Where(r => r.ChildProfileId == childId && r.Status != PetBrainRunStatus.Active)
            .OrderByDescending(r => r.CompletedAt)
            .ThenByDescending(r => r.StartedAt)
            .Take(RecentRunWindow)
            .Select(r => new MindRunOutcome(
                r.TemplateKey, r.Theme, r.ExperienceType, r.Status, r.ScorePercent, r.HintsUsed, r.Mistakes,
                r.EndingKey))
            .ToListAsync(ct);

        return runs;
    }

    /// <summary>
    /// Qərar üçün namizəd xatirələr.
    ///
    /// <para>Vaxtı keçmişlər süzülür; qalanlar vaciblik və yeniliyə görə
    /// sıralanır. Cümlə BURADA qurulmur — yalnız açarlar daşınır.</para>
    /// </summary>
    private async Task<IReadOnlyList<MindMemory>> MemoriesAsync(
        ChildProfile child, DateTime now, CancellationToken ct)
    {
        var memories = child.Memories.Count > 0
            ? child.Memories.ToList()
            : await _db.PetMemories.AsNoTracking()
                .Where(m => m.ChildProfileId == child.Id)
                .ToListAsync(ct);

        return
        [
            .. memories
                .Where(m => m.ExpiresAt is null || m.ExpiresAt > now)
                .OrderByDescending(m => m.Importance)
                .ThenByDescending(m => m.CreatedAt)
                .ThenBy(m => m.Id)
                .Take(MemoryWindow)
                .Select(m => new MindMemory(
                    m.Id, m.Kind, m.FactKey, m.ValueKey, m.Importance, m.CreatedAt, m.LastUsedAt, [.. m.Tags]))
        ];
    }

    private async Task<WorldWeather> WeatherAsync(Guid childId, DateTime now, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(now);
        var weekStart = today.AddDays(-6);

        var week = await _db.DailyGoals
            .AsNoTracking()
            .Where(g => g.ChildProfileId == childId && g.Date >= weekStart && g.Date <= today)
            .Select(g => new { g.Completed, g.Target })
            .ToListAsync(ct);

        return WorldStateCalculator.WeatherFor(
            WorldStateCalculator.EngagementScore(week.Select(g => (g.Completed, g.Completed >= g.Target))));
    }

    /// <summary>
    /// Son MƏNALI qarşılıqlı təsir — tamamlanmış/yarımçıq macəra və ya qulluq.
    ///
    /// <para>Söhbət sətirləri QƏSDƏN sayılmır: onlar ayrı audit jurnalıdır və
    /// Pet Brain-in qərar qatına girməməlidir.</para>
    /// </summary>
    private async Task<TimeSpan?> SinceLastInteractionAsync(Guid childId, DateTime now, CancellationToken ct)
    {
        var last = await _db.BehaviorEvents
            .AsNoTracking()
            .Where(e => e.ChildProfileId == childId)
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => (DateTime?)e.OccurredAt)
            .FirstOrDefaultAsync(ct);

        return last is null ? null : now - last.Value;
    }

    private PetBrainDifficulty DifficultyFor(ChildProfile child, IReadOnlyList<MindRunOutcome> outcomes)
    {
        var last = outcomes.FirstOrDefault(o => o.CountsForDifficulty);

        if (last is not null)
            return ExperienceDifficulty.Next(
                new RunPerformance(child.Pet is null ? PetBrainDifficulty.Easy : LastDifficulty(child, last),
                    last.ScorePercent, last.HintsUsed, last.Mistakes),
                child.Age);

        var averageRating = child.SkillMasteries.Count > 0
            ? (int)child.SkillMasteries.Average(m => m.Rating)
            : AdaptiveEngine.StartingRating;

        return ExperienceDifficulty.Initial(child.Age, AdaptiveEngine.TargetDifficulty(averageRating));
    }

    /// <summary>
    /// Son tamamlanmış run-ın çətinliyi. Nəticə sətri onu daşımır, ona görə
    /// izlənilən uşaq obyektindəki run-lardan oxunur; yoxdursa orta pillə.
    /// </summary>
    private static PetBrainDifficulty LastDifficulty(ChildProfile child, MindRunOutcome last) =>
        child.ExperienceRuns
            .Where(r => r.TemplateKey == last.TemplateKey && r.Status == PetBrainRunStatus.Completed)
            .OrderByDescending(r => r.CompletedAt)
            .Select(r => r.Difficulty)
            .FirstOrDefault();

    private static PetBrainCareBand Band(int? value) => value switch
    {
        null => PetBrainCareBand.Fine,
        < 25 => PetBrainCareBand.Urgent,
        < 50 => PetBrainCareBand.Low,
        < 80 => PetBrainCareBand.Fine,
        _ => PetBrainCareBand.Great
    };

    private static PetBrainDailyGoalBand GoalBand(DailyGoal goal)
    {
        if (goal.Target <= 0 || goal.Completed >= goal.Target)
            return PetBrainDailyGoalBand.Reached;

        if (goal.Completed == 0)
            return PetBrainDailyGoalBand.NotStarted;

        return goal.Completed >= goal.Target - 1
            ? PetBrainDailyGoalBand.AlmostDone
            : PetBrainDailyGoalBand.InProgress;
    }

    private static PetBrainScreenTimeBand ScreenTimeBand(
        ScreenTimeState state, ChildProfile child, DailyGoal goal)
    {
        if (state != ScreenTimeState.Allowed)
            return PetBrainScreenTimeBand.Blocked;

        if (child.DailyMinutesLimit <= 0)
            return PetBrainScreenTimeBand.Plenty;

        return child.DailyMinutesLimit - goal.MinutesSpent <= ScreenTimeEndingMinutes
            ? PetBrainScreenTimeBand.Ending
            : PetBrainScreenTimeBand.Plenty;
    }

    /// <summary>Uşağın ÖZ saatına görə gün hissəsi — server UTC-də işləyir.</summary>
    private static PetBrainSessionBucket BucketOf(DateTime utcNow, ChildProfile child) =>
        utcNow.AddMinutes(child.UtcOffsetMinutes).Hour switch
        {
            >= 5 and < 12 => PetBrainSessionBucket.Morning,
            >= 12 and < 17 => PetBrainSessionBucket.Afternoon,
            >= 17 and < 21 => PetBrainSessionBucket.Evening,
            _ => PetBrainSessionBucket.Night
        };
}

/// <summary>Yaş ZOLAQLARI — dəqiq yaş qərar qatına və modelə heç vaxt düşmür.</summary>
public static class AgeBands
{
    public static string Of(int age) => age switch
    {
        <= 6 => "5-6",
        <= 8 => "7-8",
        _ => "9-10"
    };
}
