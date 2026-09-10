using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.Auth;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(ApplicationUser user, ProfileKind profileKind, Guid? childId);

    Task<string> IssueRefreshTokenAsync(Guid userId, Guid? childId, CancellationToken ct = default);

    Task<RefreshToken?> FindActiveRefreshTokenAsync(string rawToken, CancellationToken ct = default);

    Task RevokeAsync(RefreshToken token, CancellationToken ct = default);
}
