using Microsoft.AspNetCore.Mvc;
using PetPal.Api.Common;
using PetPal.Api.Learning.Arena;
using PetPal.Api.Security;
using PetPal.Shared.Dtos.Learning;

namespace PetPal.Api.Learning;

public static class LearningEndpoints
{
    public static IEndpointRouteBuilder MapLearningEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/learn")
            .WithTags("Learning")
            .RequireAuthorization(AuthorizationPolicies.Child);

        group.MapPost("/sessions", async (
                [FromBody] StartSessionRequest request,
                HttpContext http,
                ILearningService service,
                CancellationToken ct) =>
            (await service.StartSessionAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .WithSummary("Adaptiv sual dəsti ilə yeni sessiya başladır.");

        group.MapPost("/answers", async (
                [FromBody] SubmitAnswerRequest request,
                HttpContext http,
                ILearningService service,
                CancellationToken ct) =>
            (await service.SubmitAnswerAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .WithSummary("Cavabı qeyd edir, mükafat və pet reaksiyasını qaytarır.");

        group.MapPost("/sessions/{sessionId:guid}/complete", async (
                Guid sessionId,
                HttpContext http,
                ILearningService service,
                CancellationToken ct) =>
            (await service.CompleteSessionAsync(http.User.ChildIdOrThrow(), sessionId, ct)).ToHttpResult())
            .WithSummary("Sessiyanı bağlayır və yekun ekranının məlumatlarını qaytarır.");

        // Bilik Arenası Öyrən bölməsinin bir hissəsidir — marşrutu da onun altındadır.
        app.MapArenaEndpoints();

        return app;
    }
}
