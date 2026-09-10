using Microsoft.AspNetCore.Mvc;
using PetPal.Api.Ai;
using PetPal.Api.Common;
using PetPal.Api.Security;
using PetPal.Shared.Dtos.Pets;

namespace PetPal.Api.Pets;

public static class PetEndpoints
{
    public static IEndpointRouteBuilder MapPetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pet")
            .WithTags("Pet")
            .RequireAuthorization(AuthorizationPolicies.Child);

        group.MapGet("/", async (HttpContext http, IPetService service, CancellationToken ct) =>
                (await service.GetAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Pet-in cari vəziyyəti (statlar vaxta görə azaldılmış şəkildə).");

        group.MapPost("/care", async (
                [FromBody] CarePetRequest request,
                HttpContext http,
                IPetService service,
                CancellationToken ct) =>
            (await service.CareAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .WithSummary("Feed / Play / Clean / Sleep əməliyyatı.");

        group.MapGet("/foods", async (HttpContext http, IPetService service, CancellationToken ct) =>
                (await service.GetFoodsAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Yemək kataloqu (qiymət və təsirlərlə, uşağın dilində).");

        group.MapPost("/hatch", async (HttpContext http, IPetService service, CancellationToken ct) =>
                (await service.HatchAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Yumurtanı ulduzla açır.");

        group.MapPut("/accessories", async (
                [FromBody] EquipAccessoriesRequest request,
                HttpContext http,
                IPetService service,
                CancellationToken ct) =>
            (await service.EquipAccessoriesAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .WithSummary("Pet-in görünüşü: taxılacaq əşyaların tam siyahısı.");

        group.MapPut("/species", async (
                [FromBody] ChangeSpeciesRequest request,
                HttpContext http,
                IPetService service,
                CancellationToken ct) =>
            (await service.ChangeSpeciesAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .WithSummary("Pet-in növünü dəyişir (yalnız görünüş).");

        group.MapPut("/name", async (
                [FromBody] RenamePetRequest request,
                HttpContext http,
                IPetService service,
                CancellationToken ct) =>
            (await service.RenameAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .WithSummary("Pet-in adını dəyişir.");

        // ---------- Söhbət ----------

        group.MapGet("/chat", async (HttpContext http, IPetChatService chat, CancellationToken ct) =>
                (await chat.GetStateAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Söhbət vəziyyəti: açıqdırmı, bu gün neçə mesaj qalıb, son replikalar.");

        // Ayrıca limit: hər mesaj model çağırışıdır və pulsuz kvota dəqiqəlik
        // tokenlə ölçülür. Bölgü uşaq başınadır — ailədəki ikinci uşaq
        // birincinin limitindən əziyyət çəkməməlidir.
        group.MapPost("/chat", async (
                [FromBody] PetChatRequest request,
                HttpContext http,
                IPetChatService chat,
                CancellationToken ct) =>
            (await chat.SendAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .RequireRateLimiting("chat")
            .WithSummary("Uşağın mesajı — pet cavab verir.");

        return app;
    }
}
