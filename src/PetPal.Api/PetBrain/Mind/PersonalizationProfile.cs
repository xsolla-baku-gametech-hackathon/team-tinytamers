using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Mind;

/// <summary>
/// Uşağın gördüyü DƏSTƏYİN yekun forması — açıq ayar və müşahidə birləşəndən
/// sonra.
///
/// <para><b>Bu, uşaq haqqında iddia deyil.</b> Nə əlillik, nə diaqnoz, nə də
/// tibbi vəziyyət haqqında nəticə çıxarılır — sahələr yalnız «hansı izah bu
/// tapmacada işlədi» sualına cavab verir və hər biri bir açıq ayarla ləğv
/// oluna bilir.</para>
/// </summary>
/// <param name="Style">İpucunun forması.</param>
/// <param name="Timing">İpucunun nə vaxt təklif olunduğu.</param>
/// <param name="ReducedOptions">Eyni anda daha az variant.</param>
/// <param name="DemonstrationFirst">Tapmacadan əvvəl bir nümunə addım.</param>
/// <param name="ExtraResponseTime">Cavab üçün əlavə vaxt.</param>
/// <param name="Source">Bu planın ƏN GÜCLÜ mənbəyi — izahda göstərilir.</param>
public sealed record SupportPlan(
    PetBrainHintStyle Style,
    PetBrainHintTiming Timing,
    bool ReducedOptions,
    bool DemonstrationFirst,
    bool ExtraResponseTime,
    PetBrainSettingSource Source);

/// <summary>
/// Ekranın forması — rəng, hərəkət, şrift və səs.
///
/// <para>Hamısı YALNIZ açıq seçimlə dəyişir. Heç bir davranış siqnalı buraya
/// yazmır: «uşaq yavaş cavab verdi, deməli böyük şrift lazımdır» kimi nəticə
/// çıxarmaq uşaq haqqında tibbi iddia olardı.</para>
/// </summary>
public sealed record AccessibilityPlan(
    bool ReducedMotion,
    bool LargeText,
    bool HighContrast,
    bool IconWithText,
    bool Narration,
    bool Subtitles,
    PetBrainReadingLevel ReadingLevel);

/// <summary>
/// Fərdiləşdirmənin AÇIQ qatı — bir yerdə həll edilmiş ayarlar.
///
/// <para><see cref="PetMindContext"/>-in içində gəzir və bütün qərar qatları
/// onu eyni cür oxuyur. Ayarların özündən fərqli olaraq bu, artıq HƏLL
/// EDİLMİŞ nəticədir: standart, tanışlıq, uşaq, valideyn və təxmin arasındakı
/// üstünlük burada bir dəfə tətbiq olunur.</para>
/// </summary>
public sealed record PersonalizationProfile(
    bool Enabled,
    bool AiNarrativeEnabled,
    bool SurpriseEnabled,
    PetBrainSessionLength SessionLength,
    PetBrainPace Pace,
    PetBrainNoveltyTolerance NoveltyTolerance,
    PetBrainRewardPreference RewardPreference,
    SupportPlan Support,
    AccessibilityPlan Accessibility,
    bool OnboardingPending)
{
    /// <summary>
    /// Fərdiləşdirmə söndürüləndə işləyən NEYTRAL profil.
    ///
    /// <para>Diqqət: əlçatanlıq ayarları burada da qorunur — «fərdiləşdirməni
    /// söndür» düyməsi hərəkət həssaslığını geri qaytarmamalıdır. Bu, ayrı bir
    /// vəddir və bir açarla pozula bilməz.</para>
    /// </summary>
    public static PersonalizationProfile Neutral(AccessibilityPlan accessibility) => new(
        Enabled: false,
        AiNarrativeEnabled: false,
        SurpriseEnabled: false,
        SessionLength: PetBrainSessionLength.Medium,
        Pace: PetBrainPace.Balanced,
        NoveltyTolerance: PetBrainNoveltyTolerance.Balanced,
        RewardPreference: PetBrainRewardPreference.PetCosmetic,
        Support: new SupportPlan(
            PetBrainHintStyle.Visual, PetBrainHintTiming.OnRequest,
            ReducedOptions: false, DemonstrationFirst: false, ExtraResponseTime: false,
            PetBrainSettingSource.Default),
        Accessibility: accessibility,
        OnboardingPending: false);

    /// <summary>
    /// Macəranın hədəflədiyi ADDIM sayı — sessiya uzunluğundan.
    ///
    /// <para>Rəqəmlər kataloqun REAL aralığına kalibrlənib (4–5 mərhələ).
    /// «3 və 7» kimi nəzəri büdcə bütün namizədləri eyni məsafəyə salır və
    /// ölçünü səssizcə ölü sütuna çevirirdi.</para>
    /// </summary>
    public int PreferredStepBudget => SessionLength switch
    {
        PetBrainSessionLength.Short => 4,
        PetBrainSessionLength.Long => 6,
        _ => 5
    };

    /// <summary>Hədəflənən dəqiqə — kataloqun real aralığına (3–4) uyğun.</summary>
    public int PreferredMinutes => SessionLength switch
    {
        PetBrainSessionLength.Short => 3,
        PetBrainSessionLength.Long => 5,
        _ => 4
    };

    /// <summary>
    /// Tövsiyə siyahısındakı KƏŞF payı (0–1).
    ///
    /// <para>Ən aşağı pillədə də sıfır deyil: filter bubble qəsdən mümkün
    /// deyil. Sürpriz söndürüləndə pay azalır, amma yaxın qonşu mövzu qalır —
    /// uşaq öz dünyasına kilidlənməməlidir.</para>
    /// </summary>
    public double ExplorationShare => (NoveltyTolerance, SurpriseEnabled) switch
    {
        (PetBrainNoveltyTolerance.Low, false) => 0.10,
        (PetBrainNoveltyTolerance.Low, true) => 0.15,
        (PetBrainNoveltyTolerance.High, false) => 0.25,
        (PetBrainNoveltyTolerance.High, true) => 0.35,
        (_, false) => 0.15,
        _ => 0.25
    };
}

/// <summary>
/// Ayar sətrini HƏLL EDİLMİŞ profilə çevirən yeganə yer.
///
/// <para>Saf funksiyalar: baza yoxdur, saat yalnız parametr kimi gəlir. Ona
/// görə eyni sətir + eyni müşahidə həmişə eyni profili verir və test onu
/// sabit şəkildə yoxlaya bilir.</para>
/// </summary>
public static class PersonalizationProfileFactory
{
    /// <summary>
    /// Dəstək təxmininin işə düşməsi üçün lazım olan ƏN AZ müşahidə.
    ///
    /// <para>Bir çətin tapmacadan sonra pet-in davranışını dəyişmək uşaq
    /// haqqında tələsik nəticə olardı.</para>
    /// </summary>
    public const int MinSupportObservations = 3;

    /// <summary>Bu qədər səhvdən sonra kömək GÖZLƏNİLMƏDƏN təklif olunur.</summary>
    public const int StrugglingMistakeThreshold = 2;

    /// <summary>Ayar sətri yoxdursa bu standartlar işləyir.</summary>
    public static ChildPersonalizationSettings Defaults(Guid childId, DateTime now) => new()
    {
        ChildProfileId = childId,
        CreatedAt = now,
        UpdatedAt = now
    };

    public static AccessibilityPlan AccessibilityOf(ChildPersonalizationSettings? settings) =>
        settings is null
            ? new AccessibilityPlan(false, false, false, false, false, true, PetBrainReadingLevel.Standard)
            : new AccessibilityPlan(
                settings.ReducedMotion,
                settings.LargeText,
                settings.HighContrast,
                settings.IconWithText,
                settings.Narration,
                settings.Subtitles,
                settings.ReadingLevel);

    /// <summary>
    /// Tam profil.
    /// </summary>
    /// <param name="settings">Uşağın açıq ayarları; <c>null</c> = heç kim toxunmayıb.</param>
    /// <param name="recentOutcomes">Son macəra nəticələri — YALNIZ vaxtlama təxmini üçün.</param>
    public static PersonalizationProfile Build(
        ChildPersonalizationSettings? settings,
        IReadOnlyList<MindRunOutcome> recentOutcomes)
    {
        var accessibility = AccessibilityOf(settings);

        if (settings is not null && !settings.PersonalizationEnabled)
            return PersonalizationProfile.Neutral(accessibility);

        var support = SupportOf(settings, recentOutcomes);

        return new PersonalizationProfile(
            Enabled: true,
            AiNarrativeEnabled: settings?.AiNarrativeEnabled ?? true,
            SurpriseEnabled: settings?.SurpriseEnabled ?? true,
            SessionLength: settings?.SessionLength ?? PetBrainSessionLength.Medium,
            Pace: settings?.Pace ?? PetBrainPace.Balanced,
            NoveltyTolerance: settings?.NoveltyTolerance ?? PetBrainNoveltyTolerance.Balanced,
            RewardPreference: settings?.RewardPreference ?? PetBrainRewardPreference.PetCosmetic,
            Support: support,
            Accessibility: accessibility,
            OnboardingPending: settings?.OnboardingPending ?? true);
    }

    /// <summary>
    /// Dəstək planı.
    ///
    /// <para><b>Üstünlük sırası pozulmur:</b> valideyn və uşaq seçimi
    /// təxminin ÜSTÜNDƏDİR. Sistem yalnız heç kim toxunmayanda (mənbə
    /// <see cref="PetBrainSettingSource.Default"/>) və yalnız VAXTLAMANI
    /// təxmin edir.</para>
    ///
    /// <para><b>Forma heç vaxt təxmin edilmir.</b> «Bu uşağa vizual izah
    /// lazımdır» qənaəti davranışdan çıxarıla bilməz — o, yalnız seçilə
    /// bilər.</para>
    /// </summary>
    public static SupportPlan SupportOf(
        ChildPersonalizationSettings? settings,
        IReadOnlyList<MindRunOutcome> recentOutcomes)
    {
        var style = settings?.HintStyle ?? PetBrainHintStyle.Visual;
        var styleSource = settings?.HintStyleSource ?? PetBrainSettingSource.Default;

        var timing = settings?.HintTiming ?? PetBrainHintTiming.OnRequest;
        var timingSource = settings?.HintTimingSource ?? PetBrainSettingSource.Default;

        if (timingSource == PetBrainSettingSource.Default
            && timing == PetBrainHintTiming.OnRequest
            && NeedsEarlierHelp(recentOutcomes))
        {
            timing = PetBrainHintTiming.Delayed;
            timingSource = PetBrainSettingSource.Inferred;
        }

        return new SupportPlan(
            Style: style,
            Timing: timing,
            ReducedOptions: settings?.ReducedOptions ?? false,
            DemonstrationFirst: settings?.DemonstrationFirst ?? false,
            ExtraResponseTime: settings?.ExtraResponseTime ?? false,
            Source: styleSource >= timingSource ? styleSource : timingSource);
    }

    /// <summary>
    /// Uşaq kömək istəməzdən əvvəl təkrar-təkrar səhv edirmi.
    ///
    /// <para>Bu, bacarıq haqqında iddia DEYİL və heç bir yerdə maraq balına
    /// toxunmur: yeganə nəticəsi pet-in köməyi bir az əvvəl təklif etməsidir —
    /// uşaq isə onu yenə də rədd edə bilir.</para>
    /// </summary>
    public static bool NeedsEarlierHelp(IReadOnlyList<MindRunOutcome> recentOutcomes)
    {
        var considered = recentOutcomes
            .Where(o => o.CountsForDifficulty)
            .Take(MinSupportObservations)
            .ToList();

        if (considered.Count < MinSupportObservations)
            return false;

        // Kömək İSTƏMƏDƏN çox səhv edən uşaq — pet bir az əvvəl əl uzatsın.
        return considered.All(o => o.Mistakes >= StrugglingMistakeThreshold && o.HintsUsed == 0);
    }
}
