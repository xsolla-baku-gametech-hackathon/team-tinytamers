using PetPal.Api.Common;
using PetPal.Shared.Dtos.Missions;

namespace PetPal.Api.Missions;

public interface IMissionService
{
    Task<ServiceResult<WorldStateDto>> GetWorldAsync(Guid childId, CancellationToken ct = default);

    Task<ServiceResult<ClaimMissionResultDto>> ClaimAsync(Guid childId, Guid missionId, CancellationToken ct = default);

    /// <summary>Ana ekranda göstərilən ən aktual missiyalar.</summary>
    Task<List<MissionDto>> GetFeaturedAsync(Guid childId, int count = 3, CancellationToken ct = default);
}
