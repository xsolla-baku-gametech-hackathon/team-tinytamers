using Microsoft.AspNetCore.Mvc;
using PetPal.Api.Common;
using PetPal.Api.Security;
using PetPal.Shared.Dtos.Games;

namespace PetPal.Api.Games;

public static class GameEndpoints
{
    public static IEndpointRouteBuilder MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games")
            .WithTags("Games")
            .RequireAuthorization(AuthorizationPolicies.Child);

        group.MapGet("/", async (HttpContext http, IGameService service, CancellationToken ct) =>
                (await service.GetCatalogAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Mövcud mini oyunlar (uşağın dilində).");

        group.MapPost("/unlock", async (
                [FromBody] UnlockGameRequest request,
                HttpContext http,
                IGameService service,
                CancellationToken ct) =>
            (await service.UnlockAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .WithSummary("Kilidli oyunu ulduzla açır.");

        group.MapPost("/results", async (
                [FromBody] SubmitGameResultRequest request,
                HttpContext http,
                IGameService service,
                CancellationToken ct) =>
            (await service.SubmitResultAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .WithSummary("Oyun nəticəsini qeyd edir və mükafatı hesablayır.");

        return app;
    }
}
