using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Security;
using PetPal.Shared.Enums;

namespace PetPal.Api.Auth;

public class TokenService : ITokenService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _options;

    public TokenService(AppDbContext db, IOptions<JwtOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAt) CreateAccessToken(ApplicationUser user, ProfileKind profileKind, Guid? childId)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(PetPalClaims.ProfileKindClaim, profileKind.ToString())
        };

        if (childId.HasValue)
            claims.Add(new Claim(PetPalClaims.ChildIdClaim, childId.Value.ToString()));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public async Task<string> IssueRefreshTokenAsync(Guid userId, Guid? childId, CancellationToken ct = default)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            ChildProfileId = childId,
            TokenHash = HashToken(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays)
        });

        await _db.SaveChangesAsync(ct);
        return raw;
    }

    public async Task<RefreshToken?> FindActiveRefreshTokenAsync(string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        var hash = HashToken(rawToken);
        var token = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        return token is not null && token.IsActive ? token : null;
    }

    public async Task RevokeAsync(RefreshToken token, CancellationToken ct = default)
    {
        token.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static string HashToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
