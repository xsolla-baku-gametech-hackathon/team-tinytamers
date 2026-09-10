using Microsoft.AspNetCore.Mvc;
using PetPal.Api.Common;
using PetPal.Api.Security;
using PetPal.Shared.Dtos.Discovery;

namespace PetPal.Api.Discoveries;

public static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/discoveries")
            .WithTags("Discovery")
            .RequireAuthorization(AuthorizationPolicies.Child);

        group.MapPost("/", async (
                [FromBody] DiscoveryRequest request,
                HttpContext http,
                IDiscoveryService service,
                CancellationToken ct) =>
            (await service.CreateAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            // Bu, app-in yeganə BÖYÜK gövdə qəbul edən endpoint-idir (30 MB şəkil).
            // Limit olmasa, ard-arda gələn bir neçə yükləmə kiçik instansiyanın
            // yaddaşını doldura bilər. Siyahı endpoint-i limitsiz qalır.
            .RequireRateLimiting("upload")
            .WithSummary("Real dünyada tapılan bir şeyi qeyd edir və ulduz qazandırır.");

        group.MapGet("/", async (HttpContext http, IDiscoveryService service, int? take, CancellationToken ct) =>
                Results.Ok(await service.GetRecentAsync(http.User.ChildIdOrThrow(), take ?? 20, ct)))
            .WithSummary("Son kəşflərin siyahısı.");

        group.MapGet("/{discoveryId:guid}/photo", async (
                Guid discoveryId,
                HttpContext http,
                IDiscoveryService service,
                CancellationToken ct) =>
            (await service.GetPhotoAsync(http.User.ChildIdOrThrow(), discoveryId, ct)).ToPhotoResult(http))
            .WithSummary("Kəşfin şəkli — uşaq öz kolleksiyasına baxanda.");

        return app;
    }
}

public static class DiscoveryPhotoResults
{
    /// <summary>
    /// Uğurda faylın özü, xətada isə app-in standart JSON xətası qayıdır.
    ///
    /// Cavab "private" ilə keşlənir: şəkil dəyişməzdir (hər kəşfin öz faylı var),
    /// amma ünvan token tələb etdiyi üçün ARADAKI keşlərə düşməməlidir.
    /// </summary>
    public static IResult ToPhotoResult(this ServiceResult<DiscoveryPhotoFile> result, HttpContext http)
    {
        if (!result.Succeeded || result.Value is null)
            return result.ToHttpResult();

        http.Response.Headers.CacheControl = "private, max-age=86400";

        // Diskdəki fayl birbaşa yolla verilir (ASP.NET onu yaddaşa oxumur);
        // obyekt saxlamadan gələn şəkil isə axın kimi ötürülür.
        return result.Value.AbsolutePath is { } path
            ? Results.File(path, result.Value.ContentType)
            : Results.Stream(result.Value.Content!, result.Value.ContentType);
    }
}

