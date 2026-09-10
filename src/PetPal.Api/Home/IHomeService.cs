using PetPal.Api.Common;
using PetPal.Shared.Dtos.Home;

namespace PetPal.Api.Home;

public interface IHomeService
{
    Task<ServiceResult<HomeStateDto>> GetAsync(Guid childId, CancellationToken ct = default);
}
