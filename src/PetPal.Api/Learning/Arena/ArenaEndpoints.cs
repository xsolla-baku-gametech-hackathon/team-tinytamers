using Microsoft.AspNetCore.Mvc;
using PetPal.Api.Common;
using PetPal.Api.Security;
using PetPal.Shared.Dtos.Learning;

namespace PetPal.Api.Learning.Arena;

public static class ArenaEndpoints
{
    /// <summary>
    /// Bilik Arenası — Öyrən bölməsinin altındadır, ona görə marşrut da
    /// <c>/api/learn/arena</c> altındadır. Öz rate limit siyasəti var: duel
    /// uyğunlaşdırması bir neçə cədvələ toxunur və ard-arda basılmamalıdır.
    /// </summary>
    public static IEndpointRouteBuilder MapArenaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/learn/arena")
            .WithTags("Arena")
            .RequireAuthorization(AuthorizationPolicies.Child)
            .RequireRateLimiting("arena");

        group.MapGet("/status", async (HttpContext http, IArenaService service, CancellationToken ct) =>
                (await service.GetStatusAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Arena vəziyyəti: reytinq, qalan duel sayı, hazır nəticələr.");

        group.MapPost("/duels", async (HttpContext http, IArenaService service, CancellationToken ct) =>
                (await service.StartDuelAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Uyğun rəqib axtarır, tapılmasa gözləyən duel yaradır (dəst hələ başlamır).");

        group.MapPost("/duels/{duelId:guid}/cancel", async (
                Guid duelId,
                HttpContext http,
                IArenaService service,
                CancellationToken ct) =>
            (await service.CancelSearchAsync(http.User.ChildIdOrThrow(), duelId, ct)).ToHttpResult())
            .WithSummary("Rəqib axtarışını dayandırır; başlamış duelə toxunmur.");

        group.MapGet("/league", async (HttpContext http, IArenaService service, CancellationToken ct) =>
                (await service.GetLeagueAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Həftəlik liqa cədvəli; ümumi siyahıda yalnız pet adı görünür.");

        group.MapPost("/practice", async (HttpContext http, IArenaService service, CancellationToken ct) =>
                (await service.StartPracticeAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Məşq dueli — rəqib süni və açıq işarələnmişdir; reytinq və ulduz dəyişmir.");

        group.MapPost("/challenge/{friendChildId:guid}", async (
                Guid friendChildId,
                HttpContext http,
                IArenaService service,
                CancellationToken ct) =>
            (await service.ChallengeFriendAsync(http.User.ChildIdOrThrow(), friendChildId, ct)).ToHttpResult())
            .WithSummary("Dostu birbaşa yarışa çağırır; duel hovuza düşmür.");

        group.MapPost("/challenge/{duelId:guid}/accept", async (
                Guid duelId,
                HttpContext http,
                IArenaService service,
                CancellationToken ct) =>
            (await service.AcceptChallengeAsync(http.User.ChildIdOrThrow(), duelId, ct)).ToHttpResult())
            .WithSummary("Dostun çağırışını qəbul edir.");

        group.MapPost("/challenge/{duelId:guid}/decline", async (
                Guid duelId,
                HttpContext http,
                IArenaService service,
                CancellationToken ct) =>
            (await service.DeclineChallengeAsync(http.User.ChildIdOrThrow(), duelId, ct)).ToHttpResult())
            .WithSummary("Çağırışdan imtina edir.");

        group.MapGet("/duels/{duelId:guid}", async (
                Guid duelId,
                HttpContext http,
                IArenaService service,
                CancellationToken ct) =>
            (await service.GetDuelAsync(http.User.ChildIdOrThrow(), duelId, ct)).ToHttpResult())
            .WithSummary("Duelin sual dəsti və rəqibin görünən məlumatları.");

        group.MapPost("/answers", async (
                [FromBody] SubmitDuelAnswerRequest request,
                HttpContext http,
                IArenaService service,
                CancellationToken ct) =>
            (await service.SubmitAnswerAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .WithSummary("Duel cavabını qeyd edir; müddət serverdə ölçülür.");

        group.MapGet("/duels/{duelId:guid}/result", async (
                Guid duelId,
                HttpContext http,
                IArenaService service,
                CancellationToken ct) =>
            (await service.GetResultAsync(http.User.ChildIdOrThrow(), duelId, ct)).ToHttpResult())
            .WithSummary("Sual-sual müqayisəli nəticə ekranı.");

        return app;
    }
}
