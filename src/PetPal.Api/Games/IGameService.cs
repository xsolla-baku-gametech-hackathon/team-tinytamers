using PetPal.Api.Common;
using PetPal.Shared.Dtos.Games;

namespace PetPal.Api.Games;

public interface IGameService
{
    Task<ServiceResult<List<GameCatalogItemDto>>> GetCatalogAsync(Guid childId, CancellationToken ct = default);

    /// <summary>Kilidli oyunu ulduzla açır.</summary>
    Task<ServiceResult<UnlockGameResultDto>> UnlockAsync(
        Guid childId, UnlockGameRequest request, CancellationToken ct = default);

    Task<ServiceResult<GameResultDto>> SubmitResultAsync(
        Guid childId, SubmitGameResultRequest request, CancellationToken ct = default);
}
