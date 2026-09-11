using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Uşağın paltar otağında YAZDIĞI bir arzu və onun şəkli.
///
/// <para><b>Hər cəhd saxlanılır</b>, filtrin saxladığı da daxil: söhbətdə
/// olduğu kimi valideyn uşağın nə yazdığını tam görməlidir. Uşaq dizaynı
/// silsə belə sətir qalır (<see cref="DeletedAt"/>), yalnız şəkil faylı
/// silinir.</para>
///
/// <para>Sətir həm də İŞDİR: <see cref="WardrobeDesignStatus.Pending"/> olan
/// dizaynı arxa fon işçisi çəkir. Proses yenidən başlasa da iş itmir — işçi
/// başlayanda gözləyən sətirləri növbəyə qaytarır.</para>
/// </summary>
public class WardrobeDesign
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    /// <summary>Uşağın yazdığı mətn — valideyn baxışı üçün olduğu kimi saxlanılır.</summary>
    public string Text { get; set; } = string.Empty;

    public WardrobeDesignStatus Status { get; set; }

    public WardrobeBlockReason Reason { get; set; }

    /// <summary>
    /// Pet-in dizayn anındakı növü və mərhələsi. Şəkil sonradan çəkilir, pet
    /// isə o arada böyüyə bilər — prompt uşağın GÖRDÜYÜ pet-dən qurulmalıdır.
    /// </summary>
    public string PetSpecies { get; set; } = "fox";
    public PetStage PetStage { get; set; }

    /// <summary>Neçə dəfə çəkilməyə cəhd edilib — sonsuz təkrar olmasın.</summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Sorğu pullu şəkil modelinə çatıbmı. Gündəlik ümumi hədd bunu sayır:
    /// filtrin və moderasiyanın saxladığı cəhd pul xərcləmir.
    /// </summary>
    public bool ReachedProvider { get; set; }

    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    public int PromptVersion { get; set; }

    /// <summary>Promptun barmaq izi. Promptun özü saxlanmır.</summary>
    public string PromptHash { get; set; } = string.Empty;

    /// <summary>App-in öz saxlancındakı açar. Provayderin URL-i SAXLANMIR.</summary>
    public string AssetKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Hazırda pet-in üstündədirmi — uşaqda ən çox bir dizayn.</summary>
    public bool IsEquipped { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Uşaq dizaynı silib — sətir audit üçün qalır.</summary>
    public DateTime? DeletedAt { get; set; }
}
