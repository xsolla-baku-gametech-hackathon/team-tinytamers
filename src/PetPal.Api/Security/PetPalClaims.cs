using System.Security.Claims;
using PetPal.Shared.Enums;

namespace PetPal.Api.Security;

/// <summary>
/// Token daxilindəki xüsusi claim-lər. Uşaq sessiyası ilə valideyn sessiyası
/// eyni Identity istifadəçisinə aiddir, ona görə profil tipi tokendə açıq saxlanılır.
/// </summary>
public static class PetPalClaims
{
    public const string ProfileKindClaim = "petpal:profile";
    public const string ChildIdClaim = "petpal:child";

    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static ProfileKind GetProfileKind(this ClaimsPrincipal principal) =>
        Enum.TryParse<ProfileKind>(principal.FindFirstValue(ProfileKindClaim), out var kind)
            ? kind
            : ProfileKind.Parent;

    public static Guid? GetChildId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ChildIdClaim);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
