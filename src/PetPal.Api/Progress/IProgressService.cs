using PetPal.Api.Common;
using PetPal.Shared.Dtos.Progress;

namespace PetPal.Api.Progress;

public interface IProgressService
{
    Task<ServiceResult<ProgressSummaryDto>> GetSummaryAsync(Guid childId, CancellationToken ct = default);

    Task<List<SkillProgressDto>> GetSkillsAsync(Guid childId, CancellationToken ct = default);

    Task<DailyGoalDto> GetTodayGoalAsync(Guid childId, CancellationToken ct = default);
}
