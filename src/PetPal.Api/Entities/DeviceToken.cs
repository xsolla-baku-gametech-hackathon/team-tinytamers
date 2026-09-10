namespace PetPal.Api.Entities;

/// <summary>
/// Bir cihazın push ünvanı (FCM registration token).
///
/// <para>Sətir ya UŞAĞA, ya da VALİDEYNƏ bağlıdır — ikisinə birdən yox.
/// Səbəb budur ki, bildirişlərin ünvanı fərqlidir: dostluq sorğusu valideynə
/// gedir (qərar onundur), tamamlanan komanda missiyası isə uşağa.</para>
///
/// <para>Eyni cihazda bir neçə uşaq profili ola bilər, ona görə açar
/// <see cref="Token"/> + sahibdir: profil dəyişəndə köhnə sətir silinir.</para>
/// </summary>
public class DeviceToken
{
    public Guid Id { get; set; }

    /// <summary>FCM registration token — cihaz onu özü yaradır.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>"android" · "ios" · "web" — yalnız diaqnostika üçün.</summary>
    public string Platform { get; set; } = string.Empty;

    public Guid? ChildProfileId { get; set; }
    public ChildProfile? ChildProfile { get; set; }

    public Guid? ParentUserId { get; set; }
    public ApplicationUser? ParentUser { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Hər qeydiyyatda yenilənir — köhnəlmiş tokenləri təmizləmək üçün.</summary>
    public DateTime LastSeenAt { get; set; }
}
