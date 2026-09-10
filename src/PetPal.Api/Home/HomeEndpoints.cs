using PetPal.Api.Common;
using PetPal.Api.Security;

namespace PetPal.Api.Home;

public static class HomeEndpoints
{
    public static IEndpointRouteBuilder MapHomeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/home", async (HttpContext http, IHomeService service, CancellationToken ct) =>
                (await service.GetAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .RequireAuthorization(AuthorizationPolicies.Child)
            .WithTags("Home")
            .WithSummary("Ana ekranın tam vəziyyəti (pet, cüzdan, gündəlik hədəf, missiyalar).");

        return app;
    }
}
