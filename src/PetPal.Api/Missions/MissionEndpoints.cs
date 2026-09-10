using PetPal.Api.Common;
using PetPal.Api.Security;

namespace PetPal.Api.Missions;

public static class MissionEndpoints
{
    public static IEndpointRouteBuilder MapMissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/world")
            .WithTags("World")
            .RequireAuthorization(AuthorizationPolicies.Child);

        group.MapGet("/", async (HttpContext http, IMissionService service, CancellationToken ct) =>
                (await service.GetWorldAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Dünya xəritəsi: hava vəziyyəti, zonalar və missiyalar.");

        group.MapGet("/missions/featured", async (HttpContext http, IMissionService service, CancellationToken ct) =>
                Results.Ok(await service.GetFeaturedAsync(http.User.ChildIdOrThrow(), 3, ct)))
            .WithSummary("Ana ekranda göstərilən ən aktual missiyalar.");

        group.MapPost("/missions/{missionId:guid}/claim", async (
                Guid missionId,
                HttpContext http,
                IMissionService service,
                CancellationToken ct) =>
            (await service.ClaimAsync(http.User.ChildIdOrThrow(), missionId, ct)).ToHttpResult())
            .WithSummary("Tamamlanmış missiyanın mükafatını alır.");

        return app;
    }
}
