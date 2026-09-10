using Microsoft.AspNetCore.Mvc;
using PetPal.Api.Common;
using PetPal.Api.Discoveries;
using PetPal.Api.Security;
using PetPal.Shared.Dtos.Parent;

namespace PetPal.Api.Parent;

public static class ParentEndpoints
{
    public static IEndpointRouteBuilder MapParentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/parent")
            .WithTags("Parent")
            .RequireAuthorization(AuthorizationPolicies.Parent);

        group.MapGet("/children", async (HttpContext http, IParentService service, CancellationToken ct) =>
                (await service.GetChildrenAsync(http.User.UserIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Valideynin uşaq profilləri.");

        group.MapGet("/children/{childId:guid}/dashboard", async (
                Guid childId,
                HttpContext http,
                IParentService service,
                CancellationToken ct) =>
            (await service.GetDashboardAsync(http.User.UserIdOrThrow(), childId, ct)).ToHttpResult())
            .WithSummary("Proqres, bacarıq analitikası və ekran vaxtı balansı.");

        group.MapPut("/children/{childId:guid}/screen-time", async (
                Guid childId,
                [FromBody] ScreenTimeSettingsDto settings,
                HttpContext http,
                IParentService service,
                CancellationToken ct) =>
            (await service.UpdateScreenTimeAsync(http.User.UserIdOrThrow(), childId, settings, ct)).ToHttpResult())
            .WithSummary("Gündəlik hədəf və ekran vaxtı limitlərini yeniləyir.");

        group.MapPut("/children/{childId:guid}/language", async (
                Guid childId,
                [FromBody] ChildLanguageRequest request,
                HttpContext http,
                IParentService service,
                CancellationToken ct) =>
            (await service.UpdateLanguageAsync(http.User.UserIdOrThrow(), childId, request, ct)).ToHttpResult())
            .WithSummary("Uşağın dilini dəyişir — app-in bütün mətni bu dilə keçir.");

        group.MapGet("/children/{childId:guid}/discoveries", async (
                Guid childId,
                HttpContext http,
                IParentService service,
                int? take,
                CancellationToken ct) =>
            (await service.GetDiscoveriesAsync(http.User.UserIdOrThrow(), childId, take ?? 20, ct)).ToHttpResult())
            .WithSummary("Uşağın kəşf kolleksiyası — valideyn baxışı.");

        group.MapGet("/children/{childId:guid}/discoveries/{discoveryId:guid}/photo", async (
                Guid childId,
                Guid discoveryId,
                HttpContext http,
                IParentService service,
                CancellationToken ct) =>
            (await service.GetDiscoveryPhotoAsync(http.User.UserIdOrThrow(), childId, discoveryId, ct))
                .ToPhotoResult(http))
            .WithSummary("Kəşfin şəkli — valideyn baxışı.");

        group.MapGet("/children/{childId:guid}/chat", async (
                Guid childId,
                HttpContext http,
                IParentService service,
                int? take,
                CancellationToken ct) =>
            (await service.GetChatLogAsync(http.User.UserIdOrThrow(), childId, take, ct)).ToHttpResult())
            .WithSummary("Uşağın pet ilə söhbət tarixçəsi — valideyn baxışı.");

        group.MapPut("/children/{childId:guid}/chat", async (
                Guid childId,
                [FromBody] ChatSettingsRequest request,
                HttpContext http,
                IParentService service,
                CancellationToken ct) =>
            (await service.UpdateChatSettingsAsync(http.User.UserIdOrThrow(), childId, request, ct)).ToHttpResult())
            .WithSummary("Söhbəti açır və ya bağlayır.");

        group.MapPut("/children/{childId:guid}/arena", async (
                Guid childId,
                [FromBody] ArenaSettingsRequest request,
                HttpContext http,
                IParentService service,
                CancellationToken ct) =>
            (await service.UpdateArenaSettingsAsync(http.User.UserIdOrThrow(), childId, request, ct)).ToHttpResult())
            .WithSummary("Arenanı açır/bağlayır və rəqib hovuzunu (hamı / yalnız dostlar) seçir.");

        // Dostluq sorğuları burada DEYİL: qərar uşağın özünündür və
        // `/api/social/friend-requests/{id}`-dədir. Valideyn paneli uşağın
        // sosial qərarına qarışmır.

        // ---------- Qapı ----------
        // Bunlar valideyn sessiyası ilə çağırılır (uşaq oynayarkən də token
        // yaddaşdadır), ona görə əsl müdafiə SÜRƏT LİMİTİdir: PIN dörd
        // rəqəmdir və limitsiz cəhd onu mənasız edərdi.
        group.MapGet("/gate", async (HttpContext http, IParentGateService service, CancellationToken ct) =>
                (await service.GetStatusAsync(http.User.UserIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Valideyn PIN-i qurulubmu.");

        group.MapPut("/gate/pin", async (
                [FromBody] SetParentPinRequest request,
                HttpContext http,
                IParentGateService service,
                CancellationToken ct) =>
            (await service.SetPinAsync(http.User.UserIdOrThrow(), request, ct)).ToHttpResult())
            .RequireRateLimiting("parent-gate")
            .WithSummary("Valideyn PIN-ini qurur və ya dəyişir.");

        group.MapPost("/gate/unlock", async (
                [FromBody] ParentGateUnlockRequest request,
                HttpContext http,
                IParentGateService service,
                CancellationToken ct) =>
            (await service.UnlockAsync(http.User.UserIdOrThrow(), request, ct)).ToHttpResult())
            .RequireRateLimiting("parent-gate")
            .WithSummary("PIN-i yoxlayır və valideyn bölməsini açır.");

        return app;
    }
}
