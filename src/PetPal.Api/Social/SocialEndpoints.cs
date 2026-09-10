using Microsoft.AspNetCore.Mvc;
using PetPal.Api.Common;
using PetPal.Api.Security;
using PetPal.Shared.Dtos.Social;

namespace PetPal.Api.Social;

public static class SocialEndpoints
{
    public static IEndpointRouteBuilder MapSocialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/social")
            .WithTags("Social")
            .RequireAuthorization(AuthorizationPolicies.Child)
            .RequireRateLimiting("social");

        group.MapGet("/friend-code", async (HttpContext http, ISocialService service, CancellationToken ct) =>
                (await service.GetFriendCodeAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Uşağın öz dost kodu.");

        group.MapGet("/friends", async (HttpContext http, ISocialService service, CancellationToken ct) =>
                Results.Ok(await service.GetFriendsViewAsync(http.User.ChildIdOrThrow(), ct)))
            .WithSummary("Dostlar (onlayn işarəsi ilə) və gözləyən sorğular.");

        group.MapPost("/friends", async (
                [FromBody] AddFriendRequest request,
                HttpContext http,
                ISocialService service,
                CancellationToken ct) =>
            (await service.AddFriendAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .WithSummary("Dost kodu ilə SORĞU göndərir — dostluq qarşı tərəf qəbul edəndə işləyir.");

        // Cavab UŞAĞIN özünündür. Əvvəl bu yalnız valideyn panelində idi;
        // razılıq qaydası qalır, sadəcə onu verən dəyişir — sorğu yenə də
        // göndərənin tək klikləməsi ilə dostluğa çevrilmir.
        group.MapPost("/friend-requests/{requesterChildId:guid}", async (
                Guid requesterChildId,
                [FromBody] FriendRequestDecision decision,
                HttpContext http,
                ISocialService service,
                CancellationToken ct) =>
            (await service.RespondToFriendRequestAsync(
                http.User.ChildIdOrThrow(), requesterChildId, decision.Approve, ct)).ToHttpResult())
            .WithSummary("Gələn dostluq sorğusunu qəbul edir və ya rədd edir.");

        group.MapDelete("/friends/{friendChildId:guid}", async (
                Guid friendChildId,
                HttpContext http,
                ISocialService service,
                CancellationToken ct) =>
            (await service.RemoveFriendAsync(http.User.ChildIdOrThrow(), friendChildId, ct)).ToHttpResult())
            .WithSummary("Dostu və ya göndərilmiş sorğunu silir.");

        group.MapGet("/team-missions", async (HttpContext http, ISocialService service, CancellationToken ct) =>
                Results.Ok(await service.GetTeamMissionsAsync(http.User.ChildIdOrThrow(), ct)))
            .WithSummary("İştirak edilən və dəvət alınan komanda missiyaları.");

        group.MapPost("/team-missions", async (
                [FromBody] List<Guid> friendIds,
                HttpContext http,
                ISocialService service,
                CancellationToken ct) =>
            (await service.StartTeamMissionAsync(http.User.ChildIdOrThrow(), friendIds, ct)).ToHttpResult())
            .WithSummary("Dostları birgə missiyaya dəvət edir.");

        group.MapPost("/team-missions/{missionId:guid}/respond", async (
                Guid missionId,
                [FromQuery] bool join,
                HttpContext http,
                ISocialService service,
                CancellationToken ct) =>
            (await service.RespondToTeamMissionAsync(http.User.ChildIdOrThrow(), missionId, join, ct)).ToHttpResult())
            .WithSummary("Komanda missiyası dəvətinə cavab: qoşul və ya imtina et.");

        return app;
    }
}
