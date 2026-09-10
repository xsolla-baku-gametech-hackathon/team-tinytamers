using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Uşağın AÇIQ fərdiləşdirmə ayarları — təxmin yox, seçim.
///
/// <para><b>Nə üçün ayrı cədvəl?</b> <see cref="PlayerTrait"/> öyrənilən,
/// köhnələn və inamı olan bir ehtimaldır. Bunlar isə heç köhnəlmir və heç bir
/// davranış siqnalı ilə üstələnmir: valideyn «hərəkət azaldılsın» deyəndə
/// sistem sabah öz təxmini ilə onu geri qaytara bilməməlidir. İki fərqli
/// ömürlü məlumatı bir cədvəldə saxlamaq məhz bu səhvə aparırdı.</para>
///
/// <para><b>Hər sahənin bir MƏNBƏYİ var.</b> Mənbə həm də üstünlük sırasıdır
/// (<see cref="PetBrainSettingSource"/>): sistemin təxmini valideynin
/// seçimini heç vaxt əvəz etmir. Ona görə hər dəyərin yanında onu kimin
/// yazdığı saxlanılır.</para>
///
/// <para><b>Standartlar yüksək məxfiliklidir:</b> fərdiləşdirmə açıqdır (o,
/// oyunun özüdür), amma sürpriz təklifi, gecikmiş ipucu və oxu səviyyəsinin
/// dəyişməsi kimi hər şey ehtiyatlı başlanğıcdadır.</para>
/// </summary>
public class ChildPersonalizationSettings
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    /// <summary>
    /// Fərdiləşdirmənin ÜMUMİ açarı.
    ///
    /// <para>Bağlandıqda profil SİLİNMİR, sadəcə oxunmur: tövsiyə neytral
    /// sıraya, ipucu standart formaya, mükafat isə kataloq sırasına qayıdır.
    /// Valideyn fikrini dəyişəndə uşağın tarixçəsi yerində qalır.</para>
    /// </summary>
    public bool PersonalizationEnabled { get; set; } = true;

    /// <summary>
    /// Model mətn variasiyası (<c>PetBrain:UseAiNarrative</c>) bu uşaq üçün
    /// AYRICA söndürülə bilir — ümumi fərdiləşdirməni söndürmədən.
    /// </summary>
    public bool AiNarrativeEnabled { get; set; } = true;

    /// <summary>«Məni təəccübləndir» və təhlükəsiz kəşf kartı göstərilsinmi.</summary>
    public bool SurpriseEnabled { get; set; } = true;

    // ---------- Temp və sessiya forması ----------

    public PetBrainSessionLength SessionLength { get; set; } = PetBrainSessionLength.Medium;
    public PetBrainSettingSource SessionLengthSource { get; set; } = PetBrainSettingSource.Default;

    public PetBrainPace Pace { get; set; } = PetBrainPace.Balanced;
    public PetBrainSettingSource PaceSource { get; set; } = PetBrainSettingSource.Default;

    public PetBrainNoveltyTolerance NoveltyTolerance { get; set; } = PetBrainNoveltyTolerance.Balanced;
    public PetBrainSettingSource NoveltyToleranceSource { get; set; } = PetBrainSettingSource.Default;

    // ---------- Dəstək (scaffolding) profili ----------

    public PetBrainHintStyle HintStyle { get; set; } = PetBrainHintStyle.Visual;
    public PetBrainSettingSource HintStyleSource { get; set; } = PetBrainSettingSource.Default;

    public PetBrainHintTiming HintTiming { get; set; } = PetBrainHintTiming.OnRequest;
    public PetBrainSettingSource HintTimingSource { get; set; } = PetBrainSettingSource.Default;

    /// <summary>Eyni anda göstərilən variant sayı azaldılsın.</summary>
    public bool ReducedOptions { get; set; }

    /// <summary>Cavab üçün əlavə vaxt — heç bir yerdə saat işləməsə də müqavilədir.</summary>
    public bool ExtraResponseTime { get; set; }

    /// <summary>Tapmacadan əvvəl bir nümunə addım göstərilsin.</summary>
    public bool DemonstrationFirst { get; set; }

    // ---------- Əlçatanlıq ----------

    public bool ReducedMotion { get; set; }
    public bool LargeText { get; set; }
    public bool HighContrast { get; set; }

    /// <summary>Hər işarənin yanında mətn — rəng və ikon TƏK daşıyıcı olmasın.</summary>
    public bool IconWithText { get; set; }

    /// <summary>Mətn səsləndirilsin (cihaz TTS-i).</summary>
    public bool Narration { get; set; }

    /// <summary>Səsli məzmun üçün altyazı.</summary>
    public bool Subtitles { get; set; } = true;

    /// <summary>
    /// Oxu səviyyəsi. YALNIZ açıq ayarla dəyişir — bir zəif cavaba görə
    /// avtomatik aşağı salınmır.
    /// </summary>
    public PetBrainReadingLevel ReadingLevel { get; set; } = PetBrainReadingLevel.Standard;
    public PetBrainSettingSource ReadingLevelSource { get; set; } = PetBrainSettingSource.Default;

    // ---------- Mükafat ----------

    public PetBrainRewardPreference RewardPreference { get; set; } = PetBrainRewardPreference.PetCosmetic;
    public PetBrainSettingSource RewardPreferenceSource { get; set; } = PetBrainSettingSource.Default;

    // ---------- İlk tanışlıq ----------

    /// <summary>Tanışlıq tamamlanıb; <c>null</c> = hələ göstərilməyib.</summary>
    public DateTime? OnboardingCompletedAt { get; set; }

    /// <summary>Tanışlıq KEÇİLİB — təkrar sual verilmir, standartlar işləyir.</summary>
    public DateTime? OnboardingSkippedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Tanışlıq nə tamamlanıb, nə keçilib — ekran onu bir dəfə təklif edir.</summary>
    public bool OnboardingPending => OnboardingCompletedAt is null && OnboardingSkippedAt is null;
}
