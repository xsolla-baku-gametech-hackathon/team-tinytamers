using PetPal.Api.Common;
using PetPal.Shared.Dtos.Learning;

namespace PetPal.Api.Learning;

public interface ILearningService
{
    Task<ServiceResult<LearningSessionDto>> StartSessionAsync(Guid childId, StartSessionRequest request, CancellationToken ct = default);

    Task<ServiceResult<AnswerResultDto>> SubmitAnswerAsync(Guid childId, SubmitAnswerRequest request, CancellationToken ct = default);

    Task<ServiceResult<SessionSummaryDto>> CompleteSessionAsync(Guid childId, Guid sessionId, CancellationToken ct = default);
}
