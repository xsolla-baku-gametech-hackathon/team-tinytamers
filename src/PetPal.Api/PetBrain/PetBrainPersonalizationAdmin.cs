using Microsoft.EntityFrameworkCore;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain.Mind;
using PetPal.Api.PetBrain.Recommendation;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Fərdiləşdirmə profilinin İDARƏSİ: ilk tanışlıq, uşağın öz ayarları və
/// valideyn nəzarəti.
///
/// <para><b>Nə üçün ayrı sinif?</b> <see cref="PetBrainService"/> oyun
/// axınıdır: macəra başlayır, tapmaca yoxlanılır, mükafat verilir. Profilin
/// idarəsi isə fərqli bir səthdir — fərqli avtorizasiya (valideyn), fərqli
/// ömür (heç vaxt köhnəlmir) və fərqli risk (silmə geri qaytarılmır). İkisini
/// bir sinifdə saxlamaq həm faylı, həm də səhv etmə ehtimalını
/// böyüdürdü.</para>
///
/// <para><b>Üstünlük sırası pozulmur:</b> valideynin yazdığı sahə
/// <see cref="PetBrainSettingSource.Parent"/> mənbəsi alır və uşaq onu geri
/// dəyişə bilmir. Sistemin təxmini isə hər ikisinin altındadır.</para>
/// </summary>
public sealed class PetBrainPersonalizationAdmin
{
    /// <summary>Valideyn panelində göstərilən son qərar sayı.</summary>
    private const int DecisionHistoryLimit = 20;

    private readonly AppDbContext _db;
    private readonly PetMindContextBuilder _mind;
    private readonly IBehaviorTracker _tracker;
    private readonly TimeProvider _clock;

    public PetBrainPersonalizationAdmin(
        AppDbContext db, PetMindContextBuilder mind, IBehaviorTracker tracker, TimeProvider clock)
    {
        _db = db;
        _mind = mind;
        _tracker = tracker;
        _clock = clock;
    }

    // ==================== İlk tanışlıq ====================

    /// <summary>
    /// Tanışlığın sualları — variantlar SERVERİN allowlist-indən.
    ///
    /// <para>Klient öz siyahısını yazmır: uydurma açar profilə düşə bilməz və
    /// yeni mövzu əlavə etmək üçün klienti yeniləmək lazım gəlmir.</para>
    /// </summary>
    public async Task<PetBrainOnboardingDto?> GetOnboardingAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .AsNoTracking()
            .Include(c => c.PersonalizationSettings)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child is null)
            return null;

        var language = child.LanguageCode;

        return new PetBrainOnboardingDto
        {
            Completed = child.PersonalizationSettings is { OnboardingPending: false },
            MaxTopics = OnboardingRules.MaxTopics,
            Topics =
            [
                .. TraitKeys.Interests.Select(key =>
                    TraitKeys.ToDto(key, TraitKeys.StartingScore, language))
            ],
            Activities =
            [
                .. OnboardingRules.OfferedMechanics.Select(key =>
                    MechanicKeys.ToDto(key, TraitKeys.StartingScore, language))
            ],
            Tones =
            [
                .. OnboardingRules.OfferedTones.Select(tone => new PetBrainOnboardingToneDto
                {
                    Personality = tone,
                    Label = CompanionPersonality.Label(tone, language),
                    Icon = PersonalityVoice.Emote(tone, PetBrainBondTier.NewFriend),
                    SampleLine = PersonalityVoice.RecommendationLine(
                        tone, language, OnboardingRules.SampleTitle(language))
                })
            ]
        };
    }

    /// <summary>
    /// Tanışlığın cavabları.
    ///
    /// <para><b>Cavablar PRIOR-dur.</b> Onlar kiçik başlanğıc balı yaradır və
    /// davranış sübutu ilə tədricən yenilənir — uşağın bir dəfəki seçimi
    /// aylarla dəyişməz bir etiketə çevrilmir.</para>
    ///
    /// <para><b>Keçmək tam hüquqdur.</b> Keçiləndə heç bir prior yazılmır və
    /// təhlükəsiz standartlar işləyir; sual bir daha soruşulmur.</para>
    /// </summary>
    public async Task<bool> SubmitOnboardingAsync(
        Guid childId, PetBrainOnboardingRequest request, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .Include(c => c.PersonalizationSettings)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child is null)
            return false;

        var now = _clock.GetUtcNow().UtcDateTime;
        var settings = await EnsureSettingsAsync(child, now, ct);

        if (request.Skipped)
        {
            settings.OnboardingSkippedAt = now;
            settings.UpdatedAt = now;

            await _db.SaveChangesAsync(ct);
            return true;
        }

        var topics = OnboardingRules.CleanTopics(request.Topics);
        var mechanics = OnboardingRules.CleanMechanics(request.Activities);

        // Ayarlar tanışlıqdan gəlir: valideyn seçimindən ZƏİF, təxmindən güclü.
        if (request.SessionLength is { } length)
        {
            settings.SessionLength = length;
            settings.SessionLengthSource = Weaker(settings.SessionLengthSource, PetBrainSettingSource.Onboarding);
        }

        if (request.SurpriseEnabled is { } surprise)
        {
            settings.SurpriseEnabled = surprise;

            // «Məni təəccübləndir» yalnız bir düymə deyil: yenilik dözümü də
            // bir pillə qalxır, yoxsa vəd verilib yerinə yetirilməzdi.
            if (surprise)
                settings.NoveltyTolerance = PetBrainNoveltyTolerance.High;
        }

        // Əlçatanlıq HƏMİŞƏ açıq seçimdir — heç vaxt təxmin edilmir.
        if (request.ReducedMotion is { } reducedMotion) settings.ReducedMotion = reducedMotion;
        if (request.LargeText is { } largeText) settings.LargeText = largeText;
        if (request.HighContrast is { } highContrast) settings.HighContrast = highContrast;
        if (request.Narration is { } narration) settings.Narration = narration;

        if (request.Tone is { } tone && OnboardingRules.OfferedTones.Contains(tone))
            child.Personality = tone;

        settings.OnboardingCompletedAt = now;
        settings.UpdatedAt = now;

        var adjustments = ProfileLearningRules.ForOnboarding(topics, mechanics);

        if (adjustments.Count > 0)
            await _tracker.TrackAsync(
                childId,
                PetBrainEventType.OnboardingAnswered,
                new PetBrainEventData("onboarding", string.Join(',', topics), adjustments),
                $"onboarding:{childId:N}",
                ct);

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ==================== Uşağın öz ayarları ====================

    /// <summary>
    /// Uşağın öz ayarları.
    ///
    /// <para><b>Uşaq valideynin yazdığına toxuna bilmir.</b> Sahənin mənbəsi
    /// <see cref="PetBrainSettingSource.Parent"/>-dırsa, dəyişiklik səssizcə
    /// buraxılır — xəta qaytarmaq uşağa «valideynin nəyi kilidlədiyini»
    /// açardı və bu, ailə söhbətidir, ekranın işi deyil.</para>
    ///
    /// <para>Oxu səviyyəsi və fərdiləşdirmənin ümumi açarı burada
    /// ÜMUMİYYƏTLƏ yoxdur: müqavilədə olmayan sahəni səhvən yazmaq mümkün
    /// deyil.</para>
    /// </summary>
    public async Task<PetBrainSettingsDto?> UpdateSettingsAsync(
        Guid childId, UpdatePetBrainSettingsRequest request, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .Include(c => c.Pet)
            .Include(c => c.Traits)
            .Include(c => c.Memories)
            .Include(c => c.SkillMasteries)
            .Include(c => c.PersonalizationSettings)
            .Include(c => c.MechanicMasteries)
            .Include(c => c.ContentPreferences)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child is null)
            return null;

        var now = _clock.GetUtcNow().UtcDateTime;
        var settings = await EnsureSettingsAsync(child, now, ct);

        Apply(settings, request, PetBrainSettingSource.Child, now);

        await _tracker.TrackAsync(
            childId,
            PetBrainEventType.SettingChanged,
            new PetBrainEventData("settings", PetBrainSettingSource.Child.ToString(), []),
            null,
            ct);

        await _db.SaveChangesAsync(ct);

        var mind = await _mind.BuildAsync(child, ct);

        return PersonalizationMapper.ToDto(settings, mind, child.LanguageCode);
    }

    /// <summary>
    /// Uşağın AÇIQ məzmun rəyi: «bəyənirəm» / «daha az göstər».
    ///
    /// <para>Blok qəbul edilmir — o, valideyn qərarıdır və uşaq öz dünyasını
    /// təsadüfən bağlamamalıdır.</para>
    /// </summary>
    public async Task<bool> SubmitContentFeedbackAsync(
        Guid childId, PetBrainContentFeedbackRequest request, CancellationToken ct = default)
    {
        if (request.Kind == PetBrainContentPreferenceKind.Blocked)
            return false;

        if (!IsKnownKey(request.Scope, request.Key))
            return false;

        var child = await _db.ChildProfiles.FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child is null)
            return false;

        var now = _clock.GetUtcNow().UtcDateTime;

        await UpsertPreferenceAsync(
            childId, request.Scope, request.Key, request.Kind, PetBrainSettingSource.Child, now, ct);

        var adjustments = ProfileLearningRules.ForExplicitFeedback(request.Scope, request.Key, request.Kind);

        if (adjustments.Count > 0)
            await _tracker.TrackAsync(
                childId,
                request.Kind == PetBrainContentPreferenceKind.Liked
                    ? PetBrainEventType.ExplicitLiked
                    : PetBrainEventType.ExplicitDisliked,
                new PetBrainEventData(request.Key, request.Scope.ToString(), adjustments),
                $"explicit:{request.Kind}:{request.Scope}:{request.Key}:{now:yyyyMMdd}",
                ct);

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ==================== Valideyn nəzarəti ====================

    /// <summary>
    /// Valideyn paneli: nə toplanıb, nə təsir edir.
    ///
    /// <para>Bal TƏK göstərilmir — yanında sübut sayğacları da gedir. «72»
    /// rəqəmi valideynə heç nə demir; «üç mənbədən on müşahidə, sonuncusu
    /// keçən həftə» isə deyir.</para>
    /// </summary>
    /// <returns>Yad uşaq üçün <c>null</c> — mövcudluğu təsdiqləmək də sızmadır.</returns>
    public async Task<ParentPersonalizationDto?> GetAsync(
        Guid parentId, Guid childId, CancellationToken ct = default)
    {
        var child = await LoadOwnedAsync(parentId, childId, ct);

        if (child is null)
            return null;

        var now = _clock.GetUtcNow().UtcDateTime;
        var language = child.LanguageCode;
        var mind = await _mind.BuildAsync(child, ct);

        return new ParentPersonalizationDto
        {
            ChildId = child.Id,
            DisplayName = child.DisplayName,
            Settings = PersonalizationMapper.ToDto(child.PersonalizationSettings, mind, language),
            Interests = EntriesOf(child, PetBrainTraitCategory.Interest, now, language),
            PlayStyles = EntriesOf(child, PetBrainTraitCategory.PlayStyle, now, language),
            Mechanics = EntriesOf(child, PetBrainTraitCategory.Mechanic, now, language),
            Mastery =
            [
                .. child.MechanicMasteries
                    .OrderByDescending(m => m.Attempts)
                    .ThenBy(m => m.Mechanic, StringComparer.Ordinal)
                    .Select(m => PersonalizationMapper.ToEntry(m, now, language))
            ],
            ContentPreferences =
            [
                .. child.ContentPreferences
                    .OrderByDescending(p => p.UpdatedAt)
                    .Select(p => PersonalizationMapper.ToEntry(p, language))
            ],
            BlockableThemes =
            [
                .. TraitKeys.AllowedThemes.Select(theme =>
                    TraitKeys.ToDto(theme, TraitKeys.StartingScore, language))
            ],
            ProfileConfidence = mind.ProfileConfidence
        };
    }

    /// <summary>
    /// Valideynin ayar dəyişikliyi — mənbə <c>Parent</c> olur və heç bir
    /// təxmin onu üstələmir.
    /// </summary>
    public async Task<PetBrainSettingsDto?> UpdateAsync(
        Guid parentId, Guid childId, UpdateParentPersonalizationRequest request,
        CancellationToken ct = default)
    {
        var child = await LoadOwnedAsync(parentId, childId, ct);

        if (child is null)
            return null;

        var now = _clock.GetUtcNow().UtcDateTime;
        var settings = await EnsureSettingsAsync(child, now, ct);

        if (request.PersonalizationEnabled is { } enabled) settings.PersonalizationEnabled = enabled;
        if (request.AiNarrativeEnabled is { } narrative) settings.AiNarrativeEnabled = narrative;
        if (request.SurpriseEnabled is { } surprise) settings.SurpriseEnabled = surprise;

        if (request.ReadingLevel is { } readingLevel)
        {
            settings.ReadingLevel = readingLevel;
            settings.ReadingLevelSource = PetBrainSettingSource.Parent;
        }

        Apply(settings, new UpdatePetBrainSettingsRequest
        {
            SessionLength = request.SessionLength,
            Pace = request.Pace,
            NoveltyTolerance = request.NoveltyTolerance,
            HintStyle = request.HintStyle,
            HintTiming = request.HintTiming,
            RewardPreference = request.RewardPreference,
            SurpriseEnabled = request.SurpriseEnabled,
            ReducedMotion = request.ReducedMotion,
            LargeText = request.LargeText,
            HighContrast = request.HighContrast,
            IconWithText = request.IconWithText,
            Narration = request.Narration,
            Subtitles = request.Subtitles,
            ReducedOptions = request.ReducedOptions,
            DemonstrationFirst = request.DemonstrationFirst,
            ExtraResponseTime = request.ExtraResponseTime
        }, PetBrainSettingSource.Parent, now);

        await _tracker.TrackAsync(
            childId,
            PetBrainEventType.ParentOverrideApplied,
            new PetBrainEventData("settings", PetBrainSettingSource.Parent.ToString(), []),
            null,
            ct);

        await _db.SaveChangesAsync(ct);

        var mind = await _mind.BuildAsync(child, ct);

        return PersonalizationMapper.ToDto(settings, mind, child.LanguageCode);
    }

    /// <summary>
    /// Mövzu və ya macəranı bloklayır / blokunu götürür.
    ///
    /// <para>Blok SƏRT şərtdir: namizəd hovuzundan tamamilə çıxır və heç bir
    /// bal onu geri gətirə bilmir.</para>
    /// </summary>
    public async Task<bool> BlockAsync(
        Guid parentId, Guid childId, ParentBlockContentRequest request, CancellationToken ct = default)
    {
        if (!IsKnownKey(request.Scope, request.Key))
            return false;

        if (!await OwnsAsync(parentId, childId, ct))
            return false;

        var now = _clock.GetUtcNow().UtcDateTime;

        if (!request.Blocked)
        {
            await _db.ContentPreferences
                .Where(p => p.ChildProfileId == childId
                            && p.Scope == request.Scope
                            && p.Key == request.Key
                            && p.Kind == PetBrainContentPreferenceKind.Blocked)
                .ExecuteDeleteAsync(ct);

            return true;
        }

        await UpsertPreferenceAsync(
            childId, request.Scope, request.Key, PetBrainContentPreferenceKind.Blocked,
            PetBrainSettingSource.Parent, now, ct);

        await _tracker.TrackAsync(
            childId,
            PetBrainEventType.ParentOverrideApplied,
            new PetBrainEventData(request.Key, request.Scope.ToString(), []),
            null,
            ct);

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// TƏXMİN EDİLMİŞ profili sıfırlayır.
    ///
    /// <para><b>Ayarlar SİLİNMİR.</b> Valideyn «öyrəndiklərini unut» deyəndə
    /// öz açıq seçimlərini (hərəkət azaldılsın, model mətni bağlı) də itirmək
    /// istəmir — bu iki fərqli əməliyyatdır və birləşdirilməsi ən bahalı
    /// səhvlərdən olardı.</para>
    ///
    /// <para>Macəra tarixçəsi, mükafat və ulduzlara toxunulmur: uşağın
    /// qazandıqları gizlilik əməliyyatının qurbanı olmamalıdır.</para>
    /// </summary>
    public async Task<PetBrainResetResultDto?> ResetInferredAsync(
        Guid parentId, Guid childId, bool includeMemories, CancellationToken ct = default)
    {
        if (!await OwnsAsync(parentId, childId, ct))
            return null;

        var traits = await _db.PlayerTraits
            .Where(t => t.ChildProfileId == childId)
            .ExecuteDeleteAsync(ct);

        var mastery = await _db.MechanicMasteries
            .Where(m => m.ChildProfileId == childId)
            .ExecuteDeleteAsync(ct);

        // Uşağın ÖZ açıq seçimləri (bəyəndim / daha az göstər) də təxminlə
        // birlikdə gedir: «hər şeyi unut» yarımçıq olmamalıdır. Valideyn
        // BLOKLARI isə qalır — onlar profil deyil, qaydadır.
        var preferences = await _db.ContentPreferences
            .Where(p => p.ChildProfileId == childId && p.Kind != PetBrainContentPreferenceKind.Blocked)
            .ExecuteDeleteAsync(ct);

        var memories = includeMemories
            ? await _db.PetMemories.Where(m => m.ChildProfileId == childId).ExecuteDeleteAsync(ct)
            : 0;

        await _tracker.TrackAsync(
            childId,
            PetBrainEventType.ParentOverrideApplied,
            new PetBrainEventData("reset", includeMemories ? "with-memories" : "profile-only", []),
            null,
            ct);

        await _db.SaveChangesAsync(ct);

        return new PetBrainResetResultDto
        {
            TraitsRemoved = traits,
            MasteryRemoved = mastery,
            PreferencesRemoved = preferences,
            MemoriesRemoved = memories
        };
    }

    /// <summary>
    /// Profilin tam ixracı — «nə toplanıb?» sualına bir cavab.
    ///
    /// <para>İçində uşağın söhbət mətni, şəkli, dəqiq yaşı və ya cihaz
    /// məlumatı YOXDUR: yalnız açarlar, ballar və sübut sayğacları.</para>
    /// </summary>
    public async Task<PetBrainProfileExportDto?> ExportAsync(
        Guid parentId, Guid childId, CancellationToken ct = default)
    {
        var child = await LoadOwnedAsync(parentId, childId, ct);

        if (child is null)
            return null;

        var now = _clock.GetUtcNow().UtcDateTime;
        var language = child.LanguageCode;
        var mind = await _mind.BuildAsync(child, ct);
        var petName = child.Pet?.Name ?? "Pet";

        var decisions = await _db.RecommendationDecisions
            .AsNoTracking()
            .Where(d => d.ChildProfileId == childId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(DecisionHistoryLimit)
            .ToListAsync(ct);

        return new PetBrainProfileExportDto
        {
            ChildId = child.Id,
            DisplayName = child.DisplayName,
            AgeBand = AgeBands.Of(child.Age),
            Language = language,
            GeneratedAt = now,
            Settings = PersonalizationMapper.ToDto(child.PersonalizationSettings, mind, language),
            Interests = EntriesOf(child, PetBrainTraitCategory.Interest, now, language),
            PlayStyles = EntriesOf(child, PetBrainTraitCategory.PlayStyle, now, language),
            Mechanics = EntriesOf(child, PetBrainTraitCategory.Mechanic, now, language),
            Mastery =
            [
                .. child.MechanicMasteries.Select(m => PersonalizationMapper.ToEntry(m, now, language))
            ],
            ContentPreferences =
            [
                .. child.ContentPreferences.Select(p => PersonalizationMapper.ToEntry(p, language))
            ],
            Memories =
            [
                .. child.Memories
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => MemoryPolicy.ToDto(m, language, petName) with { Id = m.Id })
            ],
            RecentDecisions = [.. decisions.Select(d => ToEntry(d, language))]
        };
    }

    /// <summary>Son tövsiyə qərarları — «niyə bu macəra?» sualının izi.</summary>
    public async Task<IReadOnlyList<ParentDecisionEntryDto>?> DecisionsAsync(
        Guid parentId, Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == childId && c.ParentUserId == parentId, ct);

        if (child is null)
            return null;

        var decisions = await _db.RecommendationDecisions
            .AsNoTracking()
            .Where(d => d.ChildProfileId == childId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(DecisionHistoryLimit)
            .ToListAsync(ct);

        return [.. decisions.Select(d => ToEntry(d, child.LanguageCode))];
    }

    // ==================== Köməkçilər ====================

    private static ParentDecisionEntryDto ToEntry(RecommendationDecision decision, string language) => new()
    {
        DecisionId = decision.Id,
        CreatedAt = decision.CreatedAt,
        TemplateKey = decision.SelectedTemplateKey,
        Title = ExperienceCatalog.Find(decision.SelectedTemplateKey)?.Title(language)
                ?? decision.SelectedTemplateKey,
        Slot = decision.Slot,
        Feedback = decision.Feedback,
        WasExploration = decision.WasExploration,
        PolicyVersion = decision.PolicyVersion,
        TotalScore = decision.TotalScore,
        WhyReasons =
        [
            .. decision.WhyReasons
                .Select(r => Enum.TryParse<PetBrainWhyReason>(r, out var parsed) ? parsed : (PetBrainWhyReason?)null)
                .Where(r => r is not null)
                .Select(r => r!.Value)
        ],
        FilteredCandidates = [.. decision.FilteredCandidates]
    };

    private static List<ParentProfileEntryDto> EntriesOf(
        ChildProfile child, PetBrainTraitCategory category, DateTime now, string language) =>
    [
        .. child.Traits
            .Where(t => t.Category == category)
            .OrderByDescending(t => TraitEvidence.EffectiveScore(t, now))
            .ThenBy(t => t.Key, StringComparer.Ordinal)
            .Select(t => PersonalizationMapper.ToEntry(t, now, language))
    ];

    /// <summary>
    /// Ayar dəyişikliyini tətbiq edir.
    ///
    /// <para><paramref name="source"/> yalnız DAHA GÜCLÜ və ya bərabər mənbə
    /// olanda yazır: uşaq valideynin kilidlədiyi sahəyə toxuna bilmir.</para>
    /// </summary>
    private static void Apply(
        ChildPersonalizationSettings settings,
        UpdatePetBrainSettingsRequest request,
        PetBrainSettingSource source,
        DateTime now)
    {
        if (request.SessionLength is { } length && CanWrite(settings.SessionLengthSource, source))
        {
            settings.SessionLength = length;
            settings.SessionLengthSource = source;
        }

        if (request.Pace is { } pace && CanWrite(settings.PaceSource, source))
        {
            settings.Pace = pace;
            settings.PaceSource = source;
        }

        if (request.NoveltyTolerance is { } novelty && CanWrite(settings.NoveltyToleranceSource, source))
        {
            settings.NoveltyTolerance = novelty;
            settings.NoveltyToleranceSource = source;
        }

        if (request.HintStyle is { } hintStyle && CanWrite(settings.HintStyleSource, source))
        {
            settings.HintStyle = hintStyle;
            settings.HintStyleSource = source;
        }

        if (request.HintTiming is { } hintTiming && CanWrite(settings.HintTimingSource, source))
        {
            settings.HintTiming = hintTiming;
            settings.HintTimingSource = source;
        }

        if (request.RewardPreference is { } reward && CanWrite(settings.RewardPreferenceSource, source))
        {
            settings.RewardPreference = reward;
            settings.RewardPreferenceSource = source;
        }

        if (request.SurpriseEnabled is { } surprise) settings.SurpriseEnabled = surprise;

        // Əlçatanlıq açarları mənbə müqayisəsi ilə kilidlənmir: onları hər iki
        // tərəf açıq şəkildə dəyişir və heç biri təxmin deyil.
        if (request.ReducedMotion is { } reducedMotion) settings.ReducedMotion = reducedMotion;
        if (request.LargeText is { } largeText) settings.LargeText = largeText;
        if (request.HighContrast is { } highContrast) settings.HighContrast = highContrast;
        if (request.IconWithText is { } iconWithText) settings.IconWithText = iconWithText;
        if (request.Narration is { } narration) settings.Narration = narration;
        if (request.Subtitles is { } subtitles) settings.Subtitles = subtitles;
        if (request.ReducedOptions is { } reducedOptions) settings.ReducedOptions = reducedOptions;
        if (request.DemonstrationFirst is { } demonstration) settings.DemonstrationFirst = demonstration;
        if (request.ExtraResponseTime is { } extraTime) settings.ExtraResponseTime = extraTime;

        settings.UpdatedAt = now;
    }

    /// <summary>Valideynin yazdığını uşaq geri dəyişə bilmir.</summary>
    private static bool CanWrite(PetBrainSettingSource existing, PetBrainSettingSource incoming) =>
        incoming >= existing || existing != PetBrainSettingSource.Parent;

    private static PetBrainSettingSource Weaker(
        PetBrainSettingSource existing, PetBrainSettingSource incoming) =>
        existing == PetBrainSettingSource.Parent ? existing : incoming;

    private async Task<ChildPersonalizationSettings> EnsureSettingsAsync(
        ChildProfile child, DateTime now, CancellationToken ct)
    {
        if (child.PersonalizationSettings is { } existing)
            return existing;

        var settings = await _db.PersonalizationSettings
            .FirstOrDefaultAsync(s => s.ChildProfileId == child.Id, ct);

        if (settings is not null)
        {
            child.PersonalizationSettings = settings;
            return settings;
        }

        settings = PersonalizationProfileFactory.Defaults(child.Id, now);

        _db.PersonalizationSettings.Add(settings);
        child.PersonalizationSettings = settings;

        return settings;
    }

    private async Task UpsertPreferenceAsync(
        Guid childId,
        PetBrainContentScope scope,
        string key,
        PetBrainContentPreferenceKind kind,
        PetBrainSettingSource source,
        DateTime now,
        CancellationToken ct)
    {
        var preference = await _db.ContentPreferences.FirstOrDefaultAsync(
            p => p.ChildProfileId == childId && p.Scope == scope && p.Key == key, ct);

        if (preference is null)
        {
            preference = new ContentPreference
            {
                ChildProfileId = childId,
                Scope = scope,
                Key = key,
                CreatedAt = now
            };

            _db.ContentPreferences.Add(preference);
        }
        else if (preference.Kind == kind && kind == PetBrainContentPreferenceKind.ShowLess)
        {
            preference.RepeatCount++;
        }

        preference.Kind = kind;
        preference.Source = source;
        preference.UpdatedAt = now;
        preference.ExpiresAt = kind == PetBrainContentPreferenceKind.ShowLess
            ? ContentPreferenceRules.ExpiryFor(preference.RepeatCount, now)
            : null;
    }

    /// <summary>Açar SERVERİN taksonomiyasındandırmı — uydurma ad qəbul edilmir.</summary>
    private static bool IsKnownKey(PetBrainContentScope scope, string key) => scope switch
    {
        PetBrainContentScope.Theme => TraitKeys.AllowedThemes.Contains(key, StringComparer.Ordinal),
        PetBrainContentScope.Template => ExperienceCatalog.IsKnown(key),
        PetBrainContentScope.Mechanic => MechanicKeys.IsKnown(key),
        _ => false
    };

    private Task<ChildProfile?> LoadOwnedAsync(Guid parentId, Guid childId, CancellationToken ct) =>
        _db.ChildProfiles
            .Include(c => c.Pet)
            .Include(c => c.Traits)
            .Include(c => c.Memories)
            .Include(c => c.SkillMasteries)
            .Include(c => c.PersonalizationSettings)
            .Include(c => c.MechanicMasteries)
            .Include(c => c.ContentPreferences)
            .FirstOrDefaultAsync(c => c.Id == childId && c.ParentUserId == parentId, ct);

    private Task<bool> OwnsAsync(Guid parentId, Guid childId, CancellationToken ct) =>
        _db.ChildProfiles.AnyAsync(c => c.Id == childId && c.ParentUserId == parentId, ct);
}

/// <summary>
/// İlk tanışlığın QAPALI variant siyahıları və təmizləmə qaydaları.
///
/// <para>Ayrı sinifdədir ki, həm endpoint, həm də test eyni siyahını görsün:
/// klientin göstərdiyi variantla serverin qəbul etdiyi variant fərqlənsə,
/// uşaq «seçdim, amma nəsə dəyişmədi» halına düşərdi.</para>
/// </summary>
public static class OnboardingRules
{
    /// <summary>Ən çox neçə mövzu seçilə bilər — üç seçim 30 saniyəyə sığır.</summary>
    public const int MaxTopics = 3;

    /// <summary>Ən çox neçə fəaliyyət seçilə bilər.</summary>
    public const int MaxMechanics = 3;

    /// <summary>
    /// Tanışlıqda TƏKLİF OLUNAN fəaliyyətlər.
    ///
    /// <para>Bütün mexanikalar deyil: uşağa on bir variant göstərmək seçim
    /// deyil, siyahıdır. Buradakılar ən aydın fərqlənən altı işdir.</para>
    /// </summary>
    public static readonly IReadOnlyList<string> OfferedMechanics =
    [
        MechanicKeys.Building,
        MechanicKeys.Route,
        MechanicKeys.StoryChoice,
        MechanicKeys.Exploration,
        MechanicKeys.Decorating,
        MechanicKeys.Nurturing
    ];

    /// <summary>Tanışlıqda təklif olunan pet tonları.</summary>
    public static readonly IReadOnlyList<PetBrainPersonality> OfferedTones =
    [
        PetBrainPersonality.CaringCompanion,
        PetBrainPersonality.CreativeCompanion,
        PetBrainPersonality.CuriousScientist,
        PetBrainPersonality.Balanced
    ];

    /// <summary>Ton nümunəsində işlənən neytral başlıq.</summary>
    public static string SampleTitle(string language) =>
        Localized.T(language, "kiçik bir macəra", "a little adventure");

    /// <summary>Naməlum və təkrar açarları atır, sayı həddə salır.</summary>
    public static IReadOnlyList<string> CleanTopics(IEnumerable<string>? topics) =>
    [
        .. (topics ?? [])
            .Where(t => TraitKeys.Interests.Contains(t, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxTopics)
    ];

    public static IReadOnlyList<string> CleanMechanics(IEnumerable<string>? mechanics) =>
    [
        .. (mechanics ?? [])
            .Where(m => OfferedMechanics.Contains(m, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxMechanics)
    ];
}
