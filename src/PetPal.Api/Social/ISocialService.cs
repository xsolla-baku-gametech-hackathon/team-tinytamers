using PetPal.Api.Common;
using PetPal.Shared.Dtos.Social;

namespace PetPal.Api.Social;

public interface ISocialService
{
    Task<ServiceResult<string>> GetFriendCodeAsync(Guid childId, CancellationToken ct = default);

    /// <summary>
    /// Dostlar ekranının mənzərəsi: təsdiqlənmiş dostlar (onlayn işarəsi ilə) və
    /// gözləyən sorğular. İki siyahı bir sorğudadır — ekran açılanda mobil klient
    /// şəbəkəyə iki dəfə çıxmamalıdır.
    /// </summary>
    Task<FriendsViewDto> GetFriendsViewAsync(Guid childId, CancellationToken ct = default);

    /// <summary>
    /// Dost kodu ilə SORĞU yaradır. Dostluq burada qurulmur — kodun sahibi
    /// qəbul edənə qədər hər iki sətir gözləyir.
    /// </summary>
    Task<ServiceResult<PendingFriendDto>> AddFriendAsync(Guid childId, AddFriendRequest request, CancellationToken ct = default);

    /// <summary>Dostu (və ya göndərilmiş sorğunu) silir — hər iki istiqamət birlikdə gedir.</summary>
    Task<ServiceResult<bool>> RemoveFriendAsync(Guid childId, Guid friendChildId, CancellationToken ct = default);

    /// <summary>
    /// Sorğunu alan uşağın cavabı. Təsdiq hər iki sətri işə salır, imtina isə
    /// ikisini də silir — «yarı dostluq» vəziyyəti mövcud olmamalıdır.
    /// </summary>
    Task<ServiceResult<bool>> RespondToFriendRequestAsync(
        Guid childId, Guid requesterChildId, bool approve, CancellationToken ct = default);

    Task<List<TeamMissionDto>> GetTeamMissionsAsync(Guid childId, CancellationToken ct = default);

    /// <summary>
    /// Dostları missiyaya DƏVƏT edir. Dəvət olunanlar qəbul edənə qədər
    /// <c>Invited</c> qalır və töhfələri sayılmır.
    /// </summary>
    Task<ServiceResult<TeamMissionDto>> StartTeamMissionAsync(Guid childId, List<Guid> friendIds, CancellationToken ct = default);

    /// <summary>Dəvətə cavab: qoşul və ya imtina et.</summary>
    Task<ServiceResult<TeamMissionDto>> RespondToTeamMissionAsync(
        Guid childId, Guid missionId, bool join, CancellationToken ct = default);
}

/// <summary>
/// Komanda missiyaları öyrənmə axını ilə irəliləyir. Learning slice-ının
/// Social slice-ının bütün səthindən asılı olmaması üçün ayrıca interfeys.
/// </summary>
public interface ITeamMissionTracker
{
    Task TrackCorrectAnswerAsync(Guid childId, CancellationToken ct = default);
}
