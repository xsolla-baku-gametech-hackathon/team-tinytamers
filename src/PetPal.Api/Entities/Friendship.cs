using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Qarşılıqlı dostluq — hər iki istiqamət üçün bir sətir yazılır ki,
/// sorğular sadə qalsın. Yalnız friend code ilə yaradılır.
///
/// <para><b>Kod yazmaq dostluq qurmur.</b> Hər iki sətir əvvəlcə
/// <see cref="FriendshipStatus.Pending"/> yaradılır və yalnız KODU VERƏN
/// tərəf özü qəbul edəndən sonra <see cref="FriendshipStatus.Active"/>
/// olur. Səbəb sadədir: kodu yazan uşaq öz seçimini artıq edib, razılığı
/// verməli olan isə qarşı tərəfdir.</para>
///
/// <para>İki sətir HƏMİŞƏ eyni vəziyyətdə olur — biri təsdiqlənib, digəri
/// gözləyən qala bilməz. Ona görə istənilən sorğu tək istiqaməti oxumaqla
/// dostluğun vəziyyətini bilir.</para>
/// </summary>
public class Friendship
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public Guid FriendChildProfileId { get; set; }
    public ChildProfile FriendChildProfile { get; set; } = null!;

    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

    /// <summary>
    /// Dost kodunu YAZAN uşaq. Təsdiq sorğusu bunun ƏKSİNƏ göstərilir:
    /// təsdiqi kodun sahibinin valideyni verir.
    /// </summary>
    public Guid RequestedByChildProfileId { get; set; }

    /// <summary>Sorğunu alan uşaq təsdiq və ya imtina edən an. Gözləyən sorğuda boşdur.</summary>
    public DateTime? RespondedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
