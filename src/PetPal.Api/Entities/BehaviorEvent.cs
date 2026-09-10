using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Mənalı bir davranış hadisəsi. Hər barmaq hərəkəti yox, yalnız domenin
/// təsdiqlədiyi nəticələr yazılır — siyahı <see cref="PetBrainEventType"/>-dadır.
///
/// <para><b>Gizlilik:</b> burada sərbəst mətn, söhbət replikası, ad, ünvan və ya
/// model düşüncəsi SAXLANILMIR. <see cref="Source"/> təsdiqlənmiş açardır
/// (şablon kodu, oyun açarı, bacarıq adı), <see cref="Detail"/> isə qısa
/// strukturlu əlavədir (seçimin açarı, nəticə faizi).</para>
/// </summary>
public class BehaviorEvent
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public PetBrainEventType Type { get; set; }

    /// <summary>Hadisənin mənbəyi: şablon açarı, oyun açarı, bacarıq adı.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Qısa strukturlu əlavə — seçim açarı və ya nəticə. Sərbəst mətn deyil.</summary>
    public string Detail { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// Eyni hadisənin iki dəfə sayılmasının qarşısını alan açar. Unikal indeks
    /// bazanın özündədir: iki eyni vaxtlı sorğu da yalnız bir sətir yaza bilir,
    /// yəni xassələr ikiqat yenilənmir.
    ///
    /// <para><c>null</c> buraxıla bilər — təkrarı mümkün olmayan hadisələr üçün
    /// (bazada NULL-lar unikal indeksə düşmür).</para>
    /// </summary>
    public string? IdempotencyKey { get; set; }
}
