using PetPal.Api.Common;
using PetPal.Api.Security;

namespace PetPal.Api.Progress;

public static class ProgressEndpoints
{
    public static IEndpointRouteBuilder MapProgressEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/progress")
            .WithTags("Progress")
            .RequireAuthorization(AuthorizationPolicies.Child);

        group.MapGet("/", async (HttpContext http, IProgressService service, CancellationToken ct) =>
                (await service.GetSummaryAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Cüzdan, gündəlik hədəf, bacarıq mənimsəmə və nişanlar.");

        group.MapGet("/skills", async (HttpContext http, IProgressService service, CancellationToken ct) =>
                Results.Ok(await service.GetSkillsAsync(http.User.ChildIdOrThrow(), ct)))
            .WithSummary("Bacarıq sahələri üzrə mənimsəmə və focus area işarələri.");

        return app;
    }
}
