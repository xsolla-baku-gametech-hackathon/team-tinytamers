using PetPal.Api.Security;

namespace PetPal.Api.Rewards;

public static class RewardEndpoints
{
    public static IEndpointRouteBuilder MapRewardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rewards")
            .WithTags("Rewards")
            .RequireAuthorization(AuthorizationPolicies.Child);

        group.MapGet("/wallet", async (HttpContext http, IRewardService service, CancellationToken ct) =>
                Results.Ok(await service.GetWalletAsync(http.User.ChildIdOrThrow(), ct)))
            .WithSummary("Ulduz və gem balansı.");

        group.MapGet("/ledger", async (HttpContext http, IRewardService service, int? take, CancellationToken ct) =>
                Results.Ok(await service.GetLedgerAsync(http.User.ChildIdOrThrow(), take ?? 50, ct)))
            .WithSummary("Mükafat hərəkətlərinin tarixçəsi.");

        group.MapGet("/badges", async (HttpContext http, IRewardService service, CancellationToken ct) =>
                Results.Ok(await service.GetBadgesAsync(http.User.ChildIdOrThrow(), ct)))
            .WithSummary("Bütün nişanlar (qazanılmış və qazanılmamış).");

        return app;
    }
}
