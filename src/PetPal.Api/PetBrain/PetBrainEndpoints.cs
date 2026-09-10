using Microsoft.AspNetCore.Mvc;
using PetPal.Api.Common;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Recap;
using PetPal.Api.Security;
using PetPal.Shared.Dtos.PetBrain;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Pet Brain HTTP səthi — qəsdən kiçik və tipli.
///
/// <para>Üç qayda bütün marşrutlara aiddir:</para>
/// <list type="number">
///   <item>Uşaq id-si HƏMİŞƏ claim-dən gəlir; sorğu gövdəsi uşağı təyin edə bilmir.</item>
///   <item>Klient nə hadisə adı, nə mükafat, nə xassə dəyişikliyi, nə də növbəti
///   mərhələni göndərə bilir — hamısı serverin qərarıdır.</item>
///   <item>Valideyn marşrutu ayrı policy-dədir və yalnız ÖZ uşaqlarını görür.</item>
/// </list>
/// </summary>
public static class PetBrainEndpoints
{
    public static IEndpointRouteBuilder MapPetBrainEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pet-brain")
            .WithTags("PetBrain")
            .RequireAuthorization(AuthorizationPolicies.Child);

        group.MapGet("/", async (HttpContext http, IPetBrainService service, CancellationToken ct) =>
                (await service.GetStateAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Profil, yaddaş, bağ, xarakter və növbəti macəra tövsiyəsi.");

        // ---- Tövsiyəyə cavab ----
        // "Başqa fikir" və "sonra". Klient burada NƏ şablon, NƏ də bal
        // dəyişikliyi göndərmir — yalnız serverin verdiyi qərar id-sini.
        group.MapPost("/recommendation/feedback", async (
                [FromBody] PetBrainFeedbackRequest request,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.SubmitFeedbackAsync(http.User.ChildIdOrThrow(), request, ct)).ToHttpResult())
            .WithSummary("«Başqa fikir» və ya «sonra» — yeni vəziyyəti qaytarır.");

        group.MapPost("/runs", async (
                [FromBody] StartPetBrainRunRequest? request,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.StartRunAsync(
                http.User.ChildIdOrThrow(), request ?? new StartPetBrainRunRequest(), ct)).ToHttpResult())
            .WithSummary("Tövsiyə olunan macərəni başladır (ekran vaxtı yoxlanılır).");

        group.MapGet("/runs/{runId:guid}", async (
                Guid runId,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.GetRunAsync(http.User.ChildIdOrThrow(), runId, ct)).ToHttpResult())
            .WithSummary("Uşağın ÖZ macərasını bərpa edir.");

        group.MapPost("/runs/{runId:guid}/choices", async (
                Guid runId,
                [FromBody] PetBrainChoiceRequest request,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.SubmitChoiceAsync(http.User.ChildIdOrThrow(), runId, request, ct)).ToHttpResult())
            .WithSummary("Bir mərhələnin seçimi, tapmaca cavabı və ya ipucu istəyi.");

        group.MapPost("/runs/{runId:guid}/complete", async (
                Guid runId,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.CompleteRunAsync(http.User.ChildIdOrThrow(), runId, ct)).ToHttpResult())
            .WithSummary("Macərəni bitirir və mükafatı DƏQİQ BİR DƏFƏ verir.");

        group.MapPost("/runs/{runId:guid}/abandon", async (
                Guid runId,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.AbandonRunAsync(http.User.ChildIdOrThrow(), runId, ct)).ToHttpResult())
            .WithSummary("Yarımçıq qoyur — tamamlama mükafatı verilmir.");

        // ---- Hekayə rəsmi ----
        // Rəsm app-in ÖZ saxlancındandır: provayderin URL-i nə saxlanılır, nə
        // də klientə verilir. Yad uşaq 404 alır — mövcudluğu təsdiqləmək də
        // məlumat sızmasıdır.
        group.MapGet("/puzzles/{puzzleId:guid}/illustration", async (
                Guid puzzleId,
                HttpContext http,
                IPetBrainService service,
                IPuzzleIllustrationStore store,
                CancellationToken ct) =>
            {
                var illustration = await service.GetIllustrationAsync(http.User.ChildIdOrThrow(), puzzleId, ct);

                if (illustration is null || illustration.StillDrawing)
                    return Results.NotFound();

                if (illustration.AssetKey is null || await store.OpenAsync(illustration.AssetKey, ct) is not { } file)
                    return Results.StatusCode(StatusCodes.Status410Gone);

                // Fayl dəyişməzdir (açar səhnə hash-ıdır), ona görə uzun keş
                // təhlükəsizdir; "private" isə paylaşılan proxy-ni kənarda saxlayır.
                http.Response.Headers.CacheControl = "private, max-age=86400, immutable";

                return Results.File(file.AbsolutePath, file.ContentType, enableRangeProcessing: true);
            })
            .WithSummary("Uşağın ÖZ tapmacasının hekayə rəsmi: hazırdırsa fayl, hələ çəkilirsə 404, gəlməyəcəksə 410.");

        // ---- Recap videosu ----
        // Range dəstəyi AÇIQDIR: video oynadıcısı fayla hissə-hissə müraciət
        // edir. Provayderin URL-i nə saxlanılır, nə də proxy edilir — fayl
        // app-in öz saxlancındandır.
        group.MapGet("/runs/{runId:guid}/recap/video", async (
                Guid runId,
                HttpContext http,
                IPetBrainService service,
                IRecapVideoStore store,
                CancellationToken ct) =>
            {
                var key = await service.GetRecapVideoKeyAsync(http.User.ChildIdOrThrow(), runId, ct);

                if (key is null || await store.OpenAsync(key, ct) is not { } file)
                    return Results.NotFound();

                // Məzmun dəyişməzdir (açar seçimlərin hash-ıdır).
                http.Response.Headers.CacheControl = "private, max-age=86400, immutable";

                return Results.File(file.AbsolutePath, file.ContentType, enableRangeProcessing: true);
            })
            .WithSummary("Uşağın ÖZ macərasının 10 saniyəlik recap videosu (hazırdırsa).");

        // ---- Valideyn müqayisəsi ----
        // Uşaq sessiyası bura DÜŞMÜR: bu, ailənin bütün uşaqlarını göstərir.
        app.MapGet("/api/parent/pet-brain/comparison", async (
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.CompareChildrenAsync(http.User.UserIdOrThrow(), ct)).ToHttpResult())
            .RequireAuthorization(AuthorizationPolicies.Parent)
            .WithTags("PetBrain")
            .WithSummary("Valideynin ÖZ uşaqlarının Pet Brain müqayisəsi.");

        return app;
    }
}
