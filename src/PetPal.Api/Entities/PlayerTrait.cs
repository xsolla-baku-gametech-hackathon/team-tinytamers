using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Uşağın bir xassəsi — maraq ("kosmos") və ya oyun üslubu ("kəşfiyyatçı").
///
/// <para>Sətir modeli qəsdən seçilib: onlarla nullable sütun əvəzinə hər xassə
/// bir sətirdir, yəni yeni açar əlavə etmək miqrasiya tələb etmir. Açarlar isə
/// SƏRBƏST DEYİL — <see cref="PetBrain.TraitKeys"/> siyahısındadır və klientdən
/// heç vaxt qəbul edilmir.</para>
///
/// <para>Bu, <see cref="SkillMastery"/> ilə qarışdırılmamalıdır: ora məktəb
/// mənimsəməsidir (Elo), bura isə maraqdır. Arena, yaradıcı seçim və mini oyun
/// davranışı reytinqə toxunmamalıdır.</para>
/// </summary>
public class PlayerTrait
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public PetBrainTraitCategory Category { get; set; }

    /// <summary>Təsdiqlənmiş açar: space, science, puzzles, creative, explorer…</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 0–100 aralığında. Bir hərəkət bunu beş baldan çox dəyişə bilməz.
    ///
    /// <para>Bu sahə SAXLANILIR və direktorun oxuduğu ölçü olaraq qalır —
    /// aşağıdakı sübut sahələri onu ƏVƏZ ETMİR, ona MƏNA verir. Köhnə
    /// sətirlərdə sübut sahələri boşdur və hər şey əvvəlki kimi işləyir.</para>
    /// </summary>
    public int Score { get; set; }

    // ---------- Sübut modeli ----------
    //
    // Tək bal bir sualı cavablandıra bilmir: "72" nə deməkdir? Bir dəfə güclü
    // siqnalmı, yoxsa iyirmi zəif toxunuşmu? Uşaq bunu SEÇDİ, yoxsa ona
    // GÖSTƏRİLDİ və başqa yolu yox idi? Aşağıdakı sahələr məhz bunu ayırır.

    /// <summary>Bu açar üzrə ümumi müşahidə sayı — nə qədər çox, o qədər inam.</summary>
    public int ObservationCount { get; set; }

    /// <summary>
    /// Uşağın AKTİV seçimi ilə gələn müşahidələr: seçim etdi, tamamladı,
    /// təkrar-təkrar qayıtdı.
    /// </summary>
    public int PositiveEvidence { get; set; }

    /// <summary>
    /// Kənara qoyma siqnalları: «başqa fikir», «sonra», mənalı yarımçıq qoyma.
    ///
    /// <para><b>Bir skip bəyənməmək DEYİL.</b> Ona görə bu sahə balı birbaşa
    /// azaltmır — yalnız inamı aşağı salır (bax <see cref="PetBrain.TraitEvidence"/>).</para>
    /// </summary>
    public int SkipEvidence { get; set; }

    /// <summary>
    /// Siqnalın neçə FƏRQLİ mənbədən gəldiyi — bit maskası
    /// (<see cref="PetBrainEvidenceSource"/> üzrə).
    ///
    /// <para>Yalnız macəradan gələn 80 bal ilə macəra + mini oyun + dərsdən
    /// gələn 80 bal eyni şey deyil; ikincisi daha etibarlıdır.</para>
    /// </summary>
    public int SourceMask { get; set; }

    /// <summary>Sonuncu dəfə nə vaxt müşahidə olundu — köhnəlmə bundan hesablanır.</summary>
    public DateTime? LastObservedAt { get; set; }

    /// <summary>Sonuncu GÜCLÜ siqnal (tamamlama, təkrar seçim) nə vaxt oldu.</summary>
    public DateTime? LastStrongEvidenceAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
