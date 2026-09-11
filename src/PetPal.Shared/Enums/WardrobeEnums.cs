namespace PetPal.Shared.Enums;

/// <summary>Uşağın paltar dizaynının vəziyyəti.</summary>
public enum WardrobeDesignStatus
{
    /// <summary>Qəbul edildi — dərzi (şəkil modeli) hələ tikir.</summary>
    Pending = 0,

    /// <summary>Şəkil hazırdır və geyindirilə bilər.</summary>
    Ready = 1,

    /// <summary>Təhlükəsizlik qatlarından biri saxladı — şəkil çəkilmir.</summary>
    Blocked = 2,

    /// <summary>Texniki səbəbdən alınmadı. Uşağın günlük həddindən SAYILMIR.</summary>
    Failed = 3
}

/// <summary>
/// Dizaynın niyə çəkilmədiyi. Uşağa göstərilən mesaj bundan seçilir, valideyn
/// isə səbəbi olduğu kimi görür.
/// </summary>
public enum WardrobeBlockReason
{
    None = 0,

    /// <summary>Boş və ya hərfsiz mətn.</summary>
    Empty = 1,

    /// <summary>Həddindən uzun.</summary>
    TooLong = 2,

    /// <summary>Modelin qaydalarını dəyişməyə cəhd.</summary>
    Injection = 3,

    /// <summary>Telefon, e-poçt, link — uşaq şəxsi məlumat paylaşır.</summary>
    ContactInfo = 4,

    /// <summary>Eyni simvolun yığını — klaviaturada oynayır.</summary>
    Repetition = 5,

    /// <summary>Determinist siyahıdakı təhlükəli mövzu: silah, qan, çılpaqlıq, siqaret…</summary>
    UnsafeTheme = 6,

    /// <summary>Moderasiya modeli mətni işarələdi.</summary>
    Moderation = 7,

    /// <summary>Şəkil modelinin öz təhlükəsizlik sistemi rədd etdi.</summary>
    ProviderRefused = 8,

    /// <summary>Xidmət cavab vermədi. Yoxlama aparıla bilmirsə şəkil də çəkilmir.</summary>
    Unavailable = 9
}
