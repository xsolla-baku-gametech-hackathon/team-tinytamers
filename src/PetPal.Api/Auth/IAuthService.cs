using PetPal.Api.Common;
using PetPal.Shared.Dtos.Auth;

namespace PetPal.Api.Auth;

public interface IAuthService
{
    Task<ServiceResult<AuthResponse>> RegisterParentAsync(RegisterParentRequest request, CancellationToken ct = default);

    Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);

    Task<ServiceResult<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct = default);

    Task<ServiceResult<bool>> LogoutAsync(RefreshRequest request, CancellationToken ct = default);

    Task<ServiceResult<ChildSummaryDto>> CreateChildAsync(Guid parentId, CreateChildRequest request, CancellationToken ct = default);

    Task<ServiceResult<List<ChildSummaryDto>>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);

    /// <summary>PIN doğrulandıqdan sonra uşaq sessiyası üçün yeni token cütü verir.</summary>
    Task<ServiceResult<AuthResponse>> ChildLoginAsync(Guid parentId, ChildLoginRequest request, CancellationToken ct = default);
}
