namespace PetPal.Api.Entities;

public class Pet
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public string Name { get; set; } = "Max";
    public string Species { get; set; } = "fox";

    public int Level { get; set; } = 1;
    public int Xp { get; set; }

    // Care statları 0–100
    public int Happiness { get; set; } = 70;
    public int Energy { get; set; } = 80;
    public int Fullness { get; set; } = 70;
    public int Cleanliness { get; set; } = 90;

    /// <summary>Statların vaxta görə azalması bu tarixdən hesablanır (lazy decay).</summary>
    public DateTime LastDecayAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastSleptAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Yumurtadan çıxma anı. <c>null</c> olduqda pet hələ yumurtadadır: qulluq
    /// əməliyyatları və kosmetik əşyalar bağlıdır. Uşaq ulduz toplayıb yumurtanı
    /// açır — bu, ilk əsl "mən qazandım" anıdır.
    /// </summary>
    public DateTime? HatchedAt { get; set; }

    /// <summary>
    /// Bağ (0–100) — uşaqla pet arasındakı UZUNMÜDDƏTLİ münasibət.
    ///
    /// <para>Xoşbəxtlikdən qəsdən ayrıdır: <see cref="Happiness"/> qulluq statıdır
    /// və vaxta görə azalır, bağ isə yalnız birlikdə yaşanan anlarla artır və
    /// HEÇ VAXT azalmır — səhv cavab, uğursuz tapmaca, buraxılan gün onu
    /// aşağı salmır. Uşağı geri qaytarmaq üçün itki hissi işlədilmir.</para>
    /// </summary>
    public int Bond { get; set; } = 10;

    /// <summary>Açılmış kosmetik əşyalar (JSON siyahı kimi saxlanılır).</summary>
    public List<string> UnlockedAccessories { get; set; } = new();

    /// <summary>
    /// Hazırda pet-in üstündə olan əşyalar. Açılmış ≠ taxılmış: uşaq görünüşü
    /// özü qurur, əşyanı istədiyi vaxt çıxarıb yenidən taxa bilər.
    /// Yeni açılan əşya avtomatik taxılır — mükafat dərhal görünməlidir.
    /// </summary>
    public List<string> EquippedAccessories { get; set; } = new();
}
