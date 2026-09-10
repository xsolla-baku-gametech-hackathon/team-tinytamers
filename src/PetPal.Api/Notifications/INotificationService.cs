using PetPal.Api.Common;
using PetPal.Shared.Dtos.Notifications;

namespace PetPal.Api.Notifications;

/// <summary>
/// Bildirişlərin MƏHSUL qatı: kimə, nə vaxt və hansı dildə göndəriləcəyini
/// bu servis qərar verir; necə göndərildiyi <see cref="INotificationSender"/>-dədir.
/// </summary>
public interface INotificationService
{
    /// <summary>Cihazın push ünvanını qeyd edir (təkrar çağırış yeniləyir).</summary>
    Task<ServiceResult<bool>> RegisterDeviceAsync(
        Guid? childId, Guid? parentUserId, RegisterDeviceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Cihazı siyahıdan çıxarır — çıxış edəndə çağırılır. Yalnız ÖZ ailəsinin
    /// cihazı silinə bilər: token bilmək icazə demək deyil.
    /// </summary>
    Task<ServiceResult<bool>> UnregisterDeviceAsync(
        Guid? childId, Guid? parentUserId, string token, CancellationToken ct = default);

    /// <summary>
    /// Uşağa dostluq sorğusu gəldi → onun VALİDEYNİNƏ bildiriş. Qərar valideynindir,
    /// ona görə yuxu rejimi bu bildirişi saxlamır.
    /// </summary>
    Task NotifyFriendRequestAsync(Guid recipientChildId, string requesterName, CancellationToken ct = default);

    /// <summary>
    /// Komanda missiyası tamamlandı → qoşulmuş uşaqlara. Yuxu rejimində
    /// saxlanılır: uşağı gecə oyatmaq app-in ekran vaxtı vədini pozardı.
    /// </summary>
    Task NotifyTeamMissionCompletedAsync(
        IEnumerable<Guid> childIds, string missionTitleEn, string? missionTitleAz, CancellationToken ct = default);
}
