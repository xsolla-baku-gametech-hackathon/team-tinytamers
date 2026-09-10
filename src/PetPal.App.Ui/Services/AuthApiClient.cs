using PetPal.Shared.Dtos.Auth;

namespace PetPal.App.Ui.Services;

public class AuthApiClient : ApiClientBase
{
    public AuthApiClient(HttpClient http, AppSession session, Loc loc) : base(http, session, loc) { }

    protected override bool UseParentToken => true;

    public Task<ApiResult<AuthResponse>> RegisterAsync(RegisterParentRequest request, CancellationToken ct = default) =>
        PostAsync<RegisterParentRequest, AuthResponse>("api/auth/register", request, ct);

    public Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
        PostAsync<LoginRequest, AuthResponse>("api/auth/login", request, ct);

    public Task<ApiResult<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default) =>
        PostAsync<RefreshRequest, AuthResponse>("api/auth/refresh", new RefreshRequest { RefreshToken = refreshToken }, ct);

    public Task<ApiResult<bool>> LogoutAsync(string refreshToken, CancellationToken ct = default) =>
        PostAsync<RefreshRequest, bool>("api/auth/logout", new RefreshRequest { RefreshToken = refreshToken }, ct);

    public Task<ApiResult<List<ChildSummaryDto>>> GetChildrenAsync(CancellationToken ct = default) =>
        GetAsync<List<ChildSummaryDto>>("api/auth/children", ct);

    public Task<ApiResult<ChildSummaryDto>> CreateChildAsync(CreateChildRequest request, CancellationToken ct = default) =>
        PostAsync<CreateChildRequest, ChildSummaryDto>("api/auth/children", request, ct);

    public Task<ApiResult<AuthResponse>> ChildLoginAsync(ChildLoginRequest request, CancellationToken ct = default) =>
        PostAsync<ChildLoginRequest, AuthResponse>("api/auth/children/login", request, ct);
}
