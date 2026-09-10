using System.Security.Claims;
using PetPal.Shared.Enums;

namespace PetPal.Api.Security;

public static class AuthorizationPolicies
{
    /// <summary>Uşaq sessiyası — oyun/öyrənmə endpoint-ləri.</summary>
    public const string Child = "child-session";

    /// <summary>Valideyn sessiyası — panel, tənzimləmələr, profil idarəetməsi.</summary>
    public const string Parent = "parent-session";

    /// <summary>Policy sayəsində claim mütləq mövcuddur; yenə də müdafiəli oxunur.</summary>
    public static Guid ChildIdOrThrow(this ClaimsPrincipal principal) =>
        principal.GetChildId() ?? throw new InvalidOperationException("Uşaq sessiyası tələb olunur.");

    public static Guid UserIdOrThrow(this ClaimsPrincipal principal) =>
        principal.GetUserId() ?? throw new InvalidOperationException("Autentifikasiya tələb olunur.");

    public static string ProfileKindValue(ProfileKind kind) => kind.ToString();
}
