using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Bir SƏHNƏ üçün rəsmin vəziyyəti.
///
/// <para>Açar tapmaca deyil, <see cref="SceneSpecHash"/>-dır və bu, qəsdəndir:
/// səhnə təsvirində uşağa aid heç nə yoxdur, ona görə eyni hekayə anını iki
/// uşaq paylaşa bilər. Nəticədə eyni səhnə üçün <b>ikinci pullu sorğu
/// getmir</b> — unikal indeks bunu bazada təmin edir, yaddaşdakı yoxlama ilə
/// yox.</para>
///
/// <para>Sətir HƏMİŞƏ əvvəlcə <c>Pending</c> kimi yazılır, model çağırışı isə
/// tranzaksiyadan KƏNARDA olur: baza bağlantısı model gözləyərkən tutulmur.</para>
/// </summary>
public class PuzzleIllustration
{
    public Guid Id { get; set; }

    /// <summary>Səhnə təsvirinin kanonik hash-ı — UNİKAL.</summary>
    public string SceneSpecHash { get; set; } = string.Empty;

    /// <summary>Hansı şablonun səhnəsidir — nümayiş paneli üçün.</summary>
    public string BlueprintKey { get; set; } = string.Empty;

    public PetBrainIllustrationStatus Status { get; set; } = PetBrainIllustrationStatus.Pending;

    /// <summary>Provayder və model adı — mənşə açıq qeyd olunur.</summary>
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    /// <summary>Prompt şablonunun versiyası — qaydalar dəyişəndə artır.</summary>
    public int PromptTemplateVersion { get; set; }

    /// <summary>Promptun barmaq izi. Promptun ÖZÜ saxlanmır — lazım deyil.</summary>
    public string PromptHash { get; set; } = string.Empty;

    /// <summary>App-in öz saxlancındakı açar. Provayderin URL-i SAXLANMIR.</summary>
    public string AssetKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Uğursuzluğun səbəbi — yalnız valideyn/nümayiş qatı üçün.</summary>
    public string FailureReason { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
