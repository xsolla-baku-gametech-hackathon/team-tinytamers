using Microsoft.AspNetCore.Mvc;
using PetPal.Api.Common;
using PetPal.Api.Security;
using PetPal.Shared.Dtos.Wardrobe;
using PetPal.Shared.Enums;

namespace PetPal.Api.Wardrobe;

public static class WardrobeEndpoints
{
    public static IEndpointRouteBuilder MapWardrobeEndpoints(this IEndpointRouteBuilder app)
    {
        var child = app.MapGroup("/api/pet/wardrobe")
            .WithTags("Wardrobe")
            .RequireAuthorization(AuthorizationPolicies.Child);

        child.MapGet("/", async (HttpContext http, IWardrobeService service, CancellationToken ct) =>
                (await service.GetStateAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Dizayn studiyasının vəziyyəti: açıqdırmı, bu gün neçə dizayn qalıb, son dizaynlar.");

        child.MapPost("/designs", async (
                [FromBody] CreateWardrobeDesignRequest request,
                HttpContext http,
                IWardrobeService service,
                CancellationToken ct) =>
            (await service.CreateAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .RequireRateLimiting(RateLimitPolicy)
            .WithSummary("Uşağın paltar arzusu — dizayn arxa fonda çəkilir.");

        child.MapGet("/designs/{designId:guid}/image", async (
                Guid designId,
                HttpContext http,
                IWardrobeService service,
                IWardrobeImageStore store,
                CancellationToken ct) =>
            await ImageAsync(await service.GetImageAsync(http.User.ChildIdOrThrow(), designId, ct), store, http, ct))
            .WithSummary("Uşağın ÖZ dizaynının şəkli: hazırdırsa fayl, tikilirsə 404, gəlməyəcəksə 410.");

        child.MapPut("/equipped", async (
                [FromBody] EquipWardrobeDesignRequest request,
                HttpContext http,
                IWardrobeService service,
                CancellationToken ct) =>
            (await service.EquipAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .WithSummary("Dizaynı pet-ə geyindirir; boş id adi görünüşə qaytarır.");

        child.MapDelete("/designs/{designId:guid}", async (
                Guid designId,
                HttpContext http,
                IWardrobeService service,
                CancellationToken ct) =>
            (await service.DeleteAsync(http.User.ChildIdOrThrow(), designId, ct)).ToHttpResult())
            .WithSummary("Dizaynın şəklini silir — mətn valideyn baxışı üçün qalır.");

        var parent = app.MapGroup("/api/parent/children/{childId:guid}/wardrobe")
            .WithTags("Parent")
            .RequireAuthorization(AuthorizationPolicies.Parent);

        parent.MapGet("/", async (
                Guid childId,
                int? take,
                HttpContext http,
                IWardrobeService service,
                CancellationToken ct) =>
            (await service.GetParentLogAsync(http.User.UserIdOrThrow(), childId, take, ct)).ToHttpResult())
            .WithSummary("Uşağın yazdığı bütün paltar arzuları — saxlanılanlar da daxil.");

        parent.MapPut("/", async (
                Guid childId,
                [FromBody] WardrobeSettingsRequest request,
                HttpContext http,
                IWardrobeService service,
                CancellationToken ct) =>
            (await service.UpdateSettingsAsync(http.User.UserIdOrThrow(), childId, request, ct)).ToHttpResult())
            .WithSummary("Dizayn studiyasını açır və ya bağlayır.");

        parent.MapGet("/designs/{designId:guid}/image", async (
                Guid childId,
                Guid designId,
                HttpContext http,
                IWardrobeService service,
                IWardrobeImageStore store,
                CancellationToken ct) =>
            await ImageAsync(
                await service.GetParentImageAsync(http.User.UserIdOrThrow(), childId, designId, ct), store, http, ct))
            .WithSummary("Uşağın dizaynının şəkli — valideyn baxışı.");

        return app;
    }

    /// <summary>Uşaq başına dar limit: hər qəbul edilən arzu pullu şəkil ola bilər.</summary>
    public const string RateLimitPolicy = "wardrobe";

    /// <summary>
    /// Yad və tikilən dizayn <c>404</c>, gəlməyəcək dizayn <c>410</c>. Fayl
    /// dəyişməzdir (açar dizayn id-sidir), ona görə uzun şəxsi keş təhlükəsizdir.
    /// </summary>
    private static async Task<IResult> ImageAsync(
        WardrobeImageLookup? lookup, IWardrobeImageStore store, HttpContext http, CancellationToken ct)
    {
        if (lookup is null || lookup.Status == WardrobeDesignStatus.Pending)
            return Results.NotFound();

        if (lookup.AssetKey is null || await store.OpenAsync(lookup.AssetKey, ct) is not { } file)
            return Results.StatusCode(StatusCodes.Status410Gone);

        http.Response.Headers.CacheControl = "private, max-age=86400, immutable";

        return Results.File(file.AbsolutePath, file.ContentType);
    }
}
