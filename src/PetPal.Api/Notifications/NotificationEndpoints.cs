using Microsoft.AspNetCore.Mvc;
using PetPal.Api.Common;
using PetPal.Api.Security;
using PetPal.Shared.Dtos.Notifications;

namespace PetPal.Api.Notifications;

public static class NotificationEndpoints
{
    /// <summary>
    /// Cihaz qeydiyyatı. Marşrut HƏM uşaq, həm valideyn sessiyasına açıqdır —
    /// hansı token gəlirsə, sətir ona bağlanır: dostluq sorğusu valideynə,
    /// tamamlanan missiya isə uşağa gedir.
    /// </summary>
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization()
            .RequireRateLimiting("social");

        group.MapPost("/devices", async (
                [FromBody] RegisterDeviceRequest request,
                HttpContext http,
                INotificationService service,
                CancellationToken ct) =>
            (await service.RegisterDeviceAsync(
                http.User.GetChildId(),
                http.User.GetChildId() is null ? http.User.GetUserId() : null,
                request, ct)).ToHttpResult())
            .WithSummary("Cihazın push ünvanını qeyd edir.");

        group.MapDelete("/devices/{token}", async (
                string token,
                HttpContext http,
                INotificationService service,
                CancellationToken ct) =>
            (await service.UnregisterDeviceAsync(
                http.User.GetChildId(),
                http.User.GetChildId() is null ? http.User.GetUserId() : null,
                token, ct)).ToHttpResult())
            .WithSummary("Cihazı bildiriş siyahısından çıxarır.");

        return app;
    }
}
