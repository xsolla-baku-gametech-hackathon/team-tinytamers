using PetPal.Shared.Enums;

namespace PetPal.Shared.Dtos.Wardrobe;

/// <summary>Dizayn otağının sabit hədləri — server və ekran eyni rəqəmə baxır.</summary>
public static class WardrobeLimits
{
    /// <summary>
    /// Bir-iki cümlə. Uşağın paltar arzusu üçün kifayətdir, prompt isə qısa
    /// və yoxlanıla bilən qalır.
    /// </summary>
    public const int MaxTextLength = 80;
}

/// <summary>
/// Paltar otağındakı dizayn studiyasının vəziyyəti.
///
/// <para><see cref="Enabled"/> üç şərtin birgə nəticəsidir: valideyn açıb,
/// xidmət qurulub və pet yumurtadan çıxıb. Ekran hansı şərtin çatmadığını
/// ayrıca göstərə bilsin deyə ilk ikisi də ayrıca gəlir.</para>
/// </summary>
public class WardrobeStateDto
{
    public bool Enabled { get; set; }

    /// <summary>Valideyn dizayn studiyasını açıbmı (standart: bağlı).</summary>
    public bool ParentAllowed { get; set; }

    /// <summary>Şəkil xidməti qurulubmu.</summary>
    public bool ServiceReady { get; set; }

    /// <summary>Uşaq başına gündəlik dizayn həddi. 0 — hədd yoxdur (standart).</summary>
    public int DesignsPerDay { get; set; }

    /// <summary>Bu gün qalan dizayn sayı — yalnız <see cref="DesignsPerDay"/> müsbət olanda mənalıdır.</summary>
    public int DesignsLeftToday { get; set; }

    public int MaxTextLength { get; set; } = WardrobeLimits.MaxTextLength;

    /// <summary>Uşağa ilham verən hazır fikirlər — uşağın dilində.</summary>
    public List<string> Examples { get; set; } = new();

    /// <summary>Hazırda geyindirilmiş dizayn; adi görünüşdədirsə <c>null</c>.</summary>
    public Guid? EquippedDesignId { get; set; }

    /// <summary>Son dizaynlar — ən yenisi əvvəl.</summary>
    public List<WardrobeDesignDto> Designs { get; set; } = new();
}

public class WardrobeDesignDto
{
    public Guid Id { get; set; }

    /// <summary>Uşağın özünün yazdığı arzu.</summary>
    public string Text { get; set; } = string.Empty;

    public WardrobeDesignStatus Status { get; set; }

    public WardrobeBlockReason Reason { get; set; }

    /// <summary>Uşağa göstərilən mesaj — nəzarətli şablondan, modeldən yox.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Şəklin ünvanı — YALNIZ app-in öz, sahiblik yoxlanan endpoint-i. Dizayn
    /// hələ tikilərkən də doludur (endpoint o vaxt <c>404</c> qaytarır);
    /// saxlanılmış və ya alınmamış dizaynda boşdur.
    /// </summary>
    public string ImageUrl { get; set; } = string.Empty;

    public bool IsEquipped { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CreateWardrobeDesignRequest
{
    /// <summary>Uşağın paltar arzusu — uzunluq və məzmun serverdə yoxlanılır.</summary>
    public string Text { get; set; } = string.Empty;
}

public class EquipWardrobeDesignRequest
{
    /// <summary>Geyindiriləcək dizayn; <c>null</c> — adi görünüşə qayıt.</summary>
    public Guid? DesignId { get; set; }
}

/// <summary>
/// Dizayn studiyasının valideyn açarı. Standart olaraq BAĞLIDIR: uşağın
/// yazdığı mətn xarici şəkil modelinə gedir.
/// </summary>
public class WardrobeSettingsRequest
{
    public bool Enabled { get; set; }
}

/// <summary>Valideyn baxışı: uşağın yazdığı HƏR arzu, saxlanılanlar da daxil.</summary>
public class ParentWardrobeLogDto
{
    public bool Enabled { get; set; }
    public bool ServiceReady { get; set; }
    public int DesignsToday { get; set; }

    /// <summary>Uşaq başına gündəlik dizayn həddi. 0 və ya mənfi — hədd yoxdur.</summary>
    public int DesignsPerDay { get; set; }

    /// <summary>Ən yenidən ən köhnəyə.</summary>
    public List<ParentWardrobeEntryDto> Entries { get; set; } = new();
}

public class ParentWardrobeEntryDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public WardrobeDesignStatus Status { get; set; }
    public WardrobeBlockReason Reason { get; set; }

    /// <summary>Şəkil valideynə göstərilə bilərmi.</summary>
    public bool HasImage { get; set; }

    /// <summary>Uşaq dizaynı silib — mətn audit üçün qalır, şəkil yoxdur.</summary>
    public bool DeletedByChild { get; set; }

    public DateTime CreatedAt { get; set; }
}
