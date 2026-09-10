using System.ComponentModel.DataAnnotations;
using PetPal.Shared.Enums;

namespace PetPal.Shared.Dtos.PetBrain;

/// <summary>
/// Uşağın AÇIQ ayarları — təxmin yox, seçim.
///
/// <para>Hər sahənin yanında onu KİMİN yazdığı gəlir. Ekran bunu göstərir:
/// «bunu valideyn seçib» ilə «bunu mən təxmin etdim» arasındakı fərq uşaq
/// üçün də, valideyn üçün də vacibdir.</para>
/// </summary>
public class PetBrainSettingsDto
{
    /// <summary>Fərdiləşdirmənin ümumi açarı.</summary>
    public bool PersonalizationEnabled { get; set; } = true;

    /// <summary>Model mətn variasiyası — ayrıca söndürülə bilir.</summary>
    public bool AiNarrativeEnabled { get; set; } = true;

    public bool SurpriseEnabled { get; set; } = true;

    public PetBrainSessionLength SessionLength { get; set; } = PetBrainSessionLength.Medium;
    public PetBrainSettingSource SessionLengthSource { get; set; }

    public PetBrainPace Pace { get; set; } = PetBrainPace.Balanced;
    public PetBrainSettingSource PaceSource { get; set; }

    public PetBrainNoveltyTolerance NoveltyTolerance { get; set; } = PetBrainNoveltyTolerance.Balanced;
    public PetBrainSettingSource NoveltyToleranceSource { get; set; }

    public PetBrainHintStyle HintStyle { get; set; } = PetBrainHintStyle.Visual;
    public PetBrainSettingSource HintStyleSource { get; set; }

    public PetBrainHintTiming HintTiming { get; set; } = PetBrainHintTiming.OnRequest;
    public PetBrainSettingSource HintTimingSource { get; set; }

    public bool ReducedOptions { get; set; }
    public bool DemonstrationFirst { get; set; }
    public bool ExtraResponseTime { get; set; }

    public bool ReducedMotion { get; set; }
    public bool LargeText { get; set; }
    public bool HighContrast { get; set; }
    public bool IconWithText { get; set; }
    public bool Narration { get; set; }
    public bool Subtitles { get; set; } = true;

    public PetBrainReadingLevel ReadingLevel { get; set; } = PetBrainReadingLevel.Standard;
    public PetBrainSettingSource ReadingLevelSource { get; set; }

    public PetBrainRewardPreference RewardPreference { get; set; } = PetBrainRewardPreference.PetCosmetic;
    public PetBrainSettingSource RewardPreferenceSource { get; set; }

    /// <summary>Köməyin uşağın dilində adı — ekranda bir sətir kimi göstərilir.</summary>
    public string SupportLabel { get; set; } = string.Empty;

    /// <summary>Mükafat növünün uşağın dilində adı.</summary>
    public string RewardLabel { get; set; } = string.Empty;
}

/// <summary>
/// Uşağın öz ayarlarını dəyişməsi.
///
/// <para><b>Uşaq nələrə toxuna bilməz:</b> valideynin yazdığı ayar, oxu
/// səviyyəsi, fərdiləşdirmənin ümumi açarı və blok siyahısı. Bunlar
/// valideyn qərarıdır və endpoint səviyyəsində qorunur.</para>
///
/// <para>Bütün sahələr KÖNÜLLÜDÜR: yalnız göndərilənlər dəyişir, ona görə
/// ekranın bir hissəsi digərinin seçimini silmir.</para>
/// </summary>
public class UpdatePetBrainSettingsRequest
{
    public PetBrainSessionLength? SessionLength { get; set; }
    public PetBrainPace? Pace { get; set; }
    public PetBrainNoveltyTolerance? NoveltyTolerance { get; set; }
    public PetBrainHintStyle? HintStyle { get; set; }
    public PetBrainHintTiming? HintTiming { get; set; }
    public PetBrainRewardPreference? RewardPreference { get; set; }
    public bool? SurpriseEnabled { get; set; }
    public bool? ReducedMotion { get; set; }
    public bool? LargeText { get; set; }
    public bool? HighContrast { get; set; }
    public bool? IconWithText { get; set; }
    public bool? Narration { get; set; }
    public bool? Subtitles { get; set; }
    public bool? ReducedOptions { get; set; }
    public bool? DemonstrationFirst { get; set; }
    public bool? ExtraResponseTime { get; set; }
}

/// <summary>
/// İlk tanışlıq — SERVERİN verdiyi suallar və variantlar.
///
/// <para>Variantlar klientdə yazılmır: mövzu, fəaliyyət və ton açarları
/// serverin allowlist-indən gəlir, yəni uydurma açar profilə düşə bilmir.</para>
/// </summary>
public class PetBrainOnboardingDto
{
    /// <summary>Tanışlıq artıq tamamlanıb və ya keçilibmi.</summary>
    public bool Completed { get; set; }

    /// <summary>Seçiləcək mövzular — təsdiqlənmiş taksonomiyadan.</summary>
    public List<PetBrainTraitDto> Topics { get; set; } = new();

    /// <summary>Seçiləcək fəaliyyətlər (mexanikalar).</summary>
    public List<PetBrainTraitDto> Activities { get; set; } = new();

    /// <summary>Ən çox neçə mövzu seçilə bilər.</summary>
    public int MaxTopics { get; set; } = 3;

    /// <summary>Pet-in tonu üçün variantlar.</summary>
    public List<PetBrainOnboardingToneDto> Tones { get; set; } = new();
}

/// <summary>Pet-in tonu — uşağa nümunə cümlə ilə göstərilir.</summary>
public class PetBrainOnboardingToneDto
{
    public PetBrainPersonality Personality { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    /// <summary>Bu tonda pet-in bir cümləsi — uşaq seçməzdən əvvəl eşitsin.</summary>
    public string SampleLine { get; set; } = string.Empty;
}

/// <summary>
/// İlk tanışlığın cavabları.
///
/// <para><b>Cavablar PRIOR-dur, həqiqət deyil.</b> Onlar başlanğıc balı
/// yaradır və davranış sübutu ilə tədricən yenilənir — yəni uşağın bir
/// dəfəki seçimi aylarla dəyişməz bir etiketə çevrilmir.</para>
///
/// <para>Tanışlıq tamamilə KEÇİLƏ bilər: <see cref="Skipped"/> qoyulanda heç
/// bir prior yazılmır və müxtəlif təhlükəsiz standartlar işləyir.</para>
/// </summary>
public class PetBrainOnboardingRequest
{
    /// <summary>Uşaq tanışlığı keçdi — heç bir seçim yazılmır.</summary>
    public bool Skipped { get; set; }

    /// <summary>Seçilmiş mövzu açarları (ən çox üç).</summary>
    [MaxLength(3)]
    public List<string>? Topics { get; set; }

    /// <summary>Seçilmiş fəaliyyət (mexanika) açarları.</summary>
    [MaxLength(3)]
    public List<string>? Activities { get; set; }

    /// <summary>Pet-in tonu.</summary>
    public PetBrainPersonality? Tone { get; set; }

    public PetBrainSessionLength? SessionLength { get; set; }

    /// <summary>«Məni təəccübləndir» — kəşf payını artırır.</summary>
    public bool? SurpriseEnabled { get; set; }

    /// <summary>Əlçatanlıq seçimləri — tanışlıqda da təklif olunur.</summary>
    public bool? ReducedMotion { get; set; }
    public bool? LargeText { get; set; }
    public bool? HighContrast { get; set; }
    public bool? Narration { get; set; }
}

/// <summary>
/// Uşağın AÇIQ məzmun rəyi: «bəyənirəm» və ya «daha az göstər».
///
/// <para>Klient burada bal dəyişikliyi göndərmir — yalnız serverin tanıdığı
/// açar və rəyin növü. Blok göndərilə bilmir: o, valideyn qərarıdır.</para>
/// </summary>
public class PetBrainContentFeedbackRequest
{
    public PetBrainContentScope Scope { get; set; }

    /// <summary>Təsdiqlənmiş mövzu, macəra və ya mexanika açarı.</summary>
    [Required]
    [StringLength(60)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Yalnız <c>Liked</c> və <c>ShowLess</c> qəbul edilir.</summary>
    public PetBrainContentPreferenceKind Kind { get; set; }
}

// ==================== Valideyn görünüşü ====================

/// <summary>
/// Valideynin fərdiləşdirmə paneli — nə toplanır, nə təsir edir, necə silinir.
/// </summary>
public class ParentPersonalizationDto
{
    public Guid ChildId { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    public PetBrainSettingsDto Settings { get; set; } = new();

    /// <summary>Toplanan maraq balları — kateqoriya ilə birlikdə.</summary>
    public List<ParentProfileEntryDto> Interests { get; set; } = new();
    public List<ParentProfileEntryDto> PlayStyles { get; set; } = new();
    public List<ParentProfileEntryDto> Mechanics { get; set; } = new();

    /// <summary>Mexanika üzrə ustalıq — maraqdan AYRI göstərilir.</summary>
    public List<ParentMasteryEntryDto> Mastery { get; set; } = new();

    /// <summary>Açıq seçimlər: bəyənmə, «daha az göstər», valideyn bloku.</summary>
    public List<ParentContentPreferenceDto> ContentPreferences { get; set; } = new();

    /// <summary>Bloklana bilən mövzuların tam siyahısı — allowlist.</summary>
    public List<PetBrainTraitDto> BlockableThemes { get; set; } = new();

    /// <summary>Profilin ümumi inamı (0–100).</summary>
    public int ProfileConfidence { get; set; }
}

/// <summary>Bir profil sətri — bal və onun ARXASINDAKI sübut.</summary>
public class ParentProfileEntryDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    /// <summary>Köhnəlmə tətbiq olunmuş EFFEKTİV bal (0–100).</summary>
    public int Score { get; set; }

    /// <summary>Bu açara İNAM (0–100) — baldan ayrı.</summary>
    public int Confidence { get; set; }

    public int ObservationCount { get; set; }
    public int PositiveEvidence { get; set; }
    public int NegativeEvidence { get; set; }
    public int SkipEvidence { get; set; }
    public int ExposureCount { get; set; }

    /// <summary>Neçə FƏRQLİ mənbədən siqnal gəlib.</summary>
    public int SourceCount { get; set; }

    public DateTime? LastObservedAt { get; set; }

    public int ModelVersion { get; set; }
}

/// <summary>Bir mexanika üzrə ustalıq — «bacarır», «sevir» ilə qarışdırılmır.</summary>
public class ParentMasteryEntryDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    public int EstimatedLevel { get; set; }
    public int Confidence { get; set; }
    public int Attempts { get; set; }
    public int Successes { get; set; }
    public int AssistedSuccesses { get; set; }
    public int RecentTrend { get; set; }
    public DateTime? LastPracticedAt { get; set; }

    /// <summary>Tövsiyə olunan çətinlik pilləsi.</summary>
    public PetBrainDifficulty RecommendedBand { get; set; }
}

/// <summary>Bir açıq məzmun seçimi.</summary>
public class ParentContentPreferenceDto
{
    public Guid Id { get; set; }
    public PetBrainContentScope Scope { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public PetBrainContentPreferenceKind Kind { get; set; }
    public PetBrainSettingSource Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Valideynin ayar dəyişikliyi.
///
/// <para>Valideyn uşağın toxuna bilmədiklərinə də toxuna bilir: ümumi açar,
/// oxu səviyyəsi, model mətni. Yazılan hər sahənin mənbəyi
/// <c>Parent</c> olur və heç bir təxmin onu üstələmir.</para>
/// </summary>
public class UpdateParentPersonalizationRequest
{
    public bool? PersonalizationEnabled { get; set; }
    public bool? AiNarrativeEnabled { get; set; }
    public bool? SurpriseEnabled { get; set; }

    public PetBrainSessionLength? SessionLength { get; set; }
    public PetBrainPace? Pace { get; set; }
    public PetBrainNoveltyTolerance? NoveltyTolerance { get; set; }
    public PetBrainHintStyle? HintStyle { get; set; }
    public PetBrainHintTiming? HintTiming { get; set; }
    public PetBrainReadingLevel? ReadingLevel { get; set; }
    public PetBrainRewardPreference? RewardPreference { get; set; }

    public bool? ReducedOptions { get; set; }
    public bool? DemonstrationFirst { get; set; }
    public bool? ExtraResponseTime { get; set; }

    public bool? ReducedMotion { get; set; }
    public bool? LargeText { get; set; }
    public bool? HighContrast { get; set; }
    public bool? IconWithText { get; set; }
    public bool? Narration { get; set; }
    public bool? Subtitles { get; set; }
}

/// <summary>Valideynin blok/blok-ləğv əməliyyatı.</summary>
public class ParentBlockContentRequest
{
    public PetBrainContentScope Scope { get; set; }

    [Required]
    [StringLength(60)]
    public string Key { get; set; } = string.Empty;

    /// <summary><c>true</c> = blokla, <c>false</c> = bloku götür.</summary>
    public bool Blocked { get; set; } = true;
}

/// <summary>
/// Profilin tam ixracı — valideyn «nə toplanıb?» sualına bir faylla cavab
/// ala bilsin.
///
/// <para>İçində uşağın söhbət mətni, şəkli, dəqiq yaşı və ya cihaz məlumatı
/// YOXDUR: yalnız açarlar, ballar və sübut sayğacları.</para>
/// </summary>
public class PetBrainProfileExportDto
{
    public Guid ChildId { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Dəqiq yaş deyil — ZOLAQ.</summary>
    public string AgeBand { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }

    public PetBrainSettingsDto Settings { get; set; } = new();
    public List<ParentProfileEntryDto> Interests { get; set; } = new();
    public List<ParentProfileEntryDto> PlayStyles { get; set; } = new();
    public List<ParentProfileEntryDto> Mechanics { get; set; } = new();
    public List<ParentMasteryEntryDto> Mastery { get; set; } = new();
    public List<ParentContentPreferenceDto> ContentPreferences { get; set; } = new();
    public List<PetBrainMemoryDto> Memories { get; set; } = new();

    /// <summary>Son tövsiyə qərarları — PII-siz.</summary>
    public List<ParentDecisionEntryDto> RecentDecisions { get; set; } = new();
}

/// <summary>Bir tövsiyə qərarının valideyn görünüşü — sərbəst mətn yoxdur.</summary>
public class ParentDecisionEntryDto
{
    public Guid DecisionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public PetBrainRecommendationSlot Slot { get; set; }
    public PetBrainRecommendationFeedback Feedback { get; set; }
    public bool WasExploration { get; set; }
    public int PolicyVersion { get; set; }
    public int TotalScore { get; set; }
    public List<PetBrainWhyReason> WhyReasons { get; set; } = new();

    /// <summary>Süzülən namizədlər: <c>"açar=səbəb"</c>.</summary>
    public List<string> FilteredCandidates { get; set; } = new();
}

/// <summary>Sıfırlama əməliyyatının nəticəsi — nə silindi.</summary>
public class PetBrainResetResultDto
{
    public int TraitsRemoved { get; set; }
    public int MasteryRemoved { get; set; }
    public int PreferencesRemoved { get; set; }
    public int MemoriesRemoved { get; set; }

    /// <summary>Ayarlar SİLİNMİR — valideynin açıq seçimi sıfırlanmamalıdır.</summary>
    public bool SettingsKept { get; set; } = true;
}
