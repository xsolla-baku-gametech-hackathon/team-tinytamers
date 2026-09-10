using Microsoft.AspNetCore.Mvc;
using PetPal.Api.Common;
using PetPal.Api.Security;
using PetPal.Shared.Dtos.Auth;

namespace PetPal.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (
                [FromBody] RegisterParentRequest request,
                IAuthService service,
                CancellationToken ct) =>
            (await service.RegisterParentAsync(request, ct)).ToHttpResult())
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithSummary("Valideyn hesabı yaradır.");

        group.MapPost("/login", async (
                [FromBody] LoginRequest request,
                IAuthService service,
                CancellationToken ct) =>
            (await service.LoginAsync(request, ct)).ToHttpResult())
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithSummary("Valideyn sessiyası açır.");

        group.MapPost("/refresh", async (
                [FromBody] RefreshRequest request,
                IAuthService service,
                CancellationToken ct) =>
            (await service.RefreshAsync(request, ct)).ToHttpResult())
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithSummary("Token cütünü yeniləyir (rotasiya ilə).");

        group.MapPost("/logout", async (
                [FromBody] RefreshRequest request,
                IAuthService service,
                CancellationToken ct) =>
            (await service.LogoutAsync(request, ct)).ToHttpResult())
            .AllowAnonymous()
            .WithSummary("Refresh token-i ləğv edir.");

        group.MapGet("/children", async (
                HttpContext http,
                IAuthService service,
                CancellationToken ct) =>
            (await service.GetChildrenAsync(http.User.UserIdOrThrow(), ct)).ToHttpResult())
            .RequireAuthorization(AuthorizationPolicies.Parent)
            .WithSummary("Valideynə bağlı uşaq profillərini qaytarır.");

        group.MapPost("/children", async (
                [FromBody] CreateChildRequest request,
                HttpContext http,
                IAuthService service,
                CancellationToken ct) =>
            (await service.CreateChildAsync(http.User.UserIdOrThrow(), request, ct)).ToHttpResult())
            .RequireAuthorization(AuthorizationPolicies.Parent)
            .WithSummary("Yeni uşaq profili və onun pet-ini yaradır.");

        group.MapPost("/children/login", async (
                [FromBody] ChildLoginRequest request,
                HttpContext http,
                IAuthService service,
                CancellationToken ct) =>
            (await service.ChildLoginAsync(http.User.UserIdOrThrow(), request, ct)).ToHttpResult())
            .RequireAuthorization(AuthorizationPolicies.Parent)
            .RequireRateLimiting("auth")
            .WithSummary("PIN ilə uşaq sessiyasına keçir.");

        return app;
    }
}
