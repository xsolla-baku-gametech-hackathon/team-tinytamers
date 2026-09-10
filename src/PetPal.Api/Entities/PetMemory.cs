using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Pet-in uzunmüddətli yaddaşı — yalnız MƏNALI, davamlı faktlar.
///
/// <para>Söhbət jurnalı (<see cref="ChatTurn"/>) bura düşmür: o, audit qeydidir və
/// uşağın sərbəst mətnini daşıyır. Yaddaş isə strukturludur — açarlardan ibarətdir
/// və cümlə render zamanı uşağın dilində qurulur, ona görə dil dəyişəndə köhnə
/// xatirələr də tərcümə olunur.</para>
/// </summary>
public class PetMemory
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public PetBrainMemoryKind Kind { get; set; }

    /// <summary>
    /// Yaddaşın SƏVİYYƏSİ.
    ///
    /// <para>Episodik fakt bir hadisəni saxlayır və çox olur; semantik nəticə
    /// bir naxışı saxlayır, az olur və təmizlənərkən daha güclü qorunur —
    /// «kosmosu sevir» faktını itirmək «üçüncü Ay macərasını bitirdi»
    /// faktını itirməkdən qat-qat bahalıdır.</para>
    ///
    /// <para>Köhnə sətirlərdə <c>0</c> (episodik) qalır — miqrasiya heç nəyi
    /// yenidən təsnif etmir.</para>
    /// </summary>
    public PetBrainMemoryTier Tier { get; set; } = PetBrainMemoryTier.Episodic;

    /// <summary>
    /// Bu faktı neçə müstəqil müşahidə dəstəkləyir.
    ///
    /// <para>Semantik nəticə üçün şərtdir: bir epizoddan naxış çıxarmaq
    /// «bir dəfə seçdi, deməli sevir» səhvidir.</para>
    /// </summary>
    public int SupportCount { get; set; } = 1;

    /// <summary>Faktın mövzusu — şablon açarı, mövzu adı və ya kosmetik kodu.</summary>
    public string FactKey { get; set; } = string.Empty;

    /// <summary>Faktın dəyəri — seçimin açarı (məsələn <c>solar-panel</c>). Boş ola bilər.</summary>
    public string ValueKey { get; set; } = string.Empty;

    /// <summary>0–100. Seçim limiti olduğuna görə vacib xatirə əvvəl gəlir.</summary>
    public int Importance { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Salamlamada işlədilən xatirə qeyd olunur — eyni cümlə hər dəfə təkrarlanmasın.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Müvəqqəti xatirələr üçün son istifadə tarixi; daimi faktlarda boş qalır.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Təsdiqlənmiş taksonomiyadan etiketlər (mövzu, janr) — seçim üçün.</summary>
    public List<string> Tags { get; set; } = new();
}
