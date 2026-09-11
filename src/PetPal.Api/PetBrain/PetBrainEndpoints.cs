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

        group.MapGet("/onboarding", async (
                HttpContext http,
                PetBrainPersonalizationAdmin admin,
                CancellationToken ct) =>
            await admin.GetOnboardingAsync(http.User.ChildIdOrThrow(), ct) is { } dto
                ? Results.Ok(dto)
                : Results.NotFound())
            .WithSummary("İlk tanışlığın sualları və təsdiqlənmiş variantları.");

        group.MapPost("/onboarding", async (
                [FromBody] PetBrainOnboardingRequest request,
                HttpContext http,
                PetBrainPersonalizationAdmin admin,
                CancellationToken ct) =>
            await admin.SubmitOnboardingAsync(http.User.ChildIdOrThrow(), request, ct)
                ? Results.NoContent()
                : Results.NotFound())
            .WithSummary("Tanışlığın cavabları — PRIOR kimi yazılır, keçilə də bilər.");

        group.MapPut("/settings", async (
                [FromBody] UpdatePetBrainSettingsRequest request,
                HttpContext http,
                PetBrainPersonalizationAdmin admin,
                CancellationToken ct) =>
            await admin.UpdateSettingsAsync(http.User.ChildIdOrThrow(), request, ct) is { } dto
                ? Results.Ok(dto)
                : Results.NotFound())
            .WithSummary("Uşağın öz ayarları: sessiya, temp, kömək, əlçatanlıq.");

        group.MapPost("/content-feedback", async (
                [FromBody] PetBrainContentFeedbackRequest request,
                HttpContext http,
                PetBrainPersonalizationAdmin admin,
                CancellationToken ct) =>
            await admin.SubmitContentFeedbackAsync(http.User.ChildIdOrThrow(), request, ct)
                ? Results.NoContent()
                : Results.BadRequest())
            .WithSummary("«Bunu bəyənirəm» / «bunu daha az göstər».");

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

        group.MapPost("/runs/{runId:guid}/pause", async (
                Guid runId,
                [FromBody] PetBrainPauseRequest? request,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.PauseRunAsync(
                http.User.ChildIdOrThrow(), runId, request ?? new PetBrainPauseRequest(), ct)).ToHttpResult())
            .WithSummary("Macərəni dayandırır — bütün vəziyyət saxlanılır.");

        group.MapPost("/runs/{runId:guid}/resume", async (
                Guid runId,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.ResumeRunAsync(http.User.ChildIdOrThrow(), runId, ct)).ToHttpResult())
            .WithSummary("Dayandırılmış macərəni son checkpoint-dən davam etdirir.");

        group.MapGet("/resume", async (
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            await service.GetResumeCardAsync(http.User.ChildIdOrThrow(), ct) is { } card
                ? Results.Ok(card)
                : Results.NoContent())
            .WithSummary("Davam edilə bilən macəranın kartı; yoxdursa boş cavab.");

        group.MapGet("/adventures", async (
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.GetAdventuresAsync(http.User.ChildIdOrThrow(), ct)).ToHttpResult())
            .WithSummary("Fəsilli macəralar — irəliləmə, müddət və tapılan sonluqlarla.");

        group.MapGet("/adventures/{key}", async (
                string key,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.GetAdventureAsync(http.User.ChildIdOrThrow(), key, ct)).ToHttpResult())
            .WithSummary("Macəranın ön baxışı: fəsillər, sonluqlar, əlçatanlıq.");

        group.MapPost("/adventures/{key}/start", async (
                string key,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.StartAdventureAsync(http.User.ChildIdOrThrow(), key, ct)).ToHttpResult())
            .WithSummary("Başlat, davam et və ya təkrar oyna — direktorun təklifi və təhlükəsizlik süzgəci ilə.");

        group.MapGet("/runs/{runId:guid}/objectives", async (
                Guid runId,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.GetAdventurePartAsync(http.User.ChildIdOrThrow(), runId, s => s.Objectives, ct))
            .ToHttpResult())
            .WithSummary("Cari 1–3 məqsəd.");

        group.MapGet("/runs/{runId:guid}/inventory", async (
                Guid runId,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.GetAdventurePartAsync(http.User.ChildIdOrThrow(), runId, s => s.Inventory, ct))
            .ToHttpResult())
            .WithSummary("Çantadakı əşyalar.");

        group.MapGet("/runs/{runId:guid}/clues", async (
                Guid runId,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.GetAdventurePartAsync(http.User.ChildIdOrThrow(), runId, s => s.Clues, ct))
            .ToHttpResult())
            .WithSummary("İpucu jurnalı — ən vacibi əvvəl.");

        group.MapGet("/runs/{runId:guid}/map", async (
                Guid runId,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.GetAdventurePartAsync(http.User.ChildIdOrThrow(), runId, s => s.Chapters, ct))
            .ToHttpResult())
            .WithSummary("Fəsil xəritəsi — tamamlanan, cari və bağlı fəsillər.");

        group.MapPost("/runs/{runId:guid}/actions", async (
                Guid runId,
                [FromBody] PetBrainChoiceRequest request,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.SubmitChoiceAsync(http.User.ChildIdOrThrow(), runId, request, ct)).ToHttpResult())
            .WithSummary("Ümumi addım: seçim, qarşılıqlı təsir və ya davam — /choices ilə eyni yol.");

        group.MapPost("/runs/{runId:guid}/hints", async (
                Guid runId,
                [FromBody] PetBrainChoiceRequest request,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
        {
            request.RequestHint = true;

            return (await service.SubmitChoiceAsync(http.User.ChildIdOrThrow(), runId, request, ct)).ToHttpResult();
        })
            .WithSummary("Növbəti ipucu pilləsi — addım irəliləmir.");

        group.MapPost("/runs/{runId:guid}/puzzles/{puzzleId:guid}/submit", async (
                Guid runId,
                Guid puzzleId,
                [FromBody] PetBrainChoiceRequest request,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
            (await service.SubmitPuzzleAnswerAsync(http.User.ChildIdOrThrow(), runId, puzzleId, request, ct))
            .ToHttpResult())
            .WithSummary("Tapmaca cavabı — yalnız cari addımın tapmacasına.");

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

        group.MapGet("/runs/{runId:guid}/recap", async (
                Guid runId,
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
                await service.GetRecapAsync(http.User.ChildIdOrThrow(), runId, ct) is { } recap
                    ? Results.Ok(recap)
                    : Results.NotFound())
            .WithSummary("Uşağın ÖZ tamamlanmış macərasının recap vəziyyəti — video hazırlanırsa ekran bunu seyrək soruşur.");

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

        group.MapGet("/recaps", async (
                HttpContext http,
                IPetBrainService service,
                CancellationToken ct) =>
                await service.ListRecapsAsync(http.User.ChildIdOrThrow(), ct) is { } shelf
                    ? Results.Ok(shelf)
                    : Results.NotFound())
            .WithSummary("Uşağın «Macəra videoları» rəfi — bitmiş macəralar, storyboard və hazır videolar. Yeni pullu iş başlatmır.");

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

        // ---- Yaddaş üzərində valideyn nəzarəti ----
        //
        // Pet uşaq haqqında bir şey «öyrənir» və onu aylarla saxlayır.
        // Valideyn bunu GÖRƏ və LƏĞV EDƏ bilməlidir, yoxsa yaddaş nəzarətdən
        // kənar, davamlı bir profilə çevrilir.
        //
        // Sahiblik HƏR ÜÇ marşrutda yoxlanılır: yad uşağın yaddaşı nə
        // oxunur, nə silinir.
        var memory = app.MapGroup("/api/parent/pet-brain/children/{childId:guid}/memories")
            .RequireAuthorization(AuthorizationPolicies.Parent)
            .WithTags("PetBrain");

        memory.MapGet("/", async (
                Guid childId,
                HttpContext http,
                PetMemoryAdmin admin,
                CancellationToken ct) =>
            await admin.ListAsync(http.User.UserIdOrThrow(), childId, ct) is { } list
                ? Results.Ok(list)
                : Results.NotFound())
            .WithSummary("Uşağın bütün xatirələri — valideyn üçün, cümlə şəklində.");

        memory.MapDelete("/{memoryId:guid}", async (
                Guid childId,
                Guid memoryId,
                HttpContext http,
                PetMemoryAdmin admin,
                CancellationToken ct) =>
            await admin.ForgetAsync(http.User.UserIdOrThrow(), childId, memoryId, ct)
                ? Results.NoContent()
                : Results.NotFound())
            .WithSummary("BİR xatirəni unutdurur. Geri qaytarılmır.");

        memory.MapDelete("/", async (
                Guid childId,
                HttpContext http,
                PetMemoryAdmin admin,
                CancellationToken ct) =>
            await admin.ResetAsync(http.User.UserIdOrThrow(), childId, ct) is { } removed
                ? Results.Ok(new { removed })
                : Results.NotFound())
            .WithSummary("Yaddaşı tam sıfırlayır — xassələrə və mükafata TOXUNMUR.");

        var personalization = app.MapGroup("/api/parent/pet-brain/children/{childId:guid}/personalization")
            .RequireAuthorization(AuthorizationPolicies.Parent)
            .WithTags("PetBrain");

        personalization.MapGet("/", async (
                Guid childId,
                HttpContext http,
                PetBrainPersonalizationAdmin admin,
                CancellationToken ct) =>
            await admin.GetAsync(http.User.UserIdOrThrow(), childId, ct) is { } dto
                ? Results.Ok(dto)
                : Results.NotFound())
            .WithSummary("Toplanan profil, ustalıq, açıq seçimlər və ayarlar.");

        personalization.MapPut("/", async (
                Guid childId,
                [FromBody] UpdateParentPersonalizationRequest request,
                HttpContext http,
                PetBrainPersonalizationAdmin admin,
                CancellationToken ct) =>
            await admin.UpdateAsync(http.User.UserIdOrThrow(), childId, request, ct) is { } dto
                ? Results.Ok(dto)
                : Results.NotFound())
            .WithSummary("Valideynin ayarları — heç bir təxmin bunları üstələmir.");

        personalization.MapPost("/blocks", async (
                Guid childId,
                [FromBody] ParentBlockContentRequest request,
                HttpContext http,
                PetBrainPersonalizationAdmin admin,
                CancellationToken ct) =>
            await admin.BlockAsync(http.User.UserIdOrThrow(), childId, request, ct)
                ? Results.NoContent()
                : Results.NotFound())
            .WithSummary("Mövzu və ya macərəni bloklayır / blokunu götürür.");

        personalization.MapDelete("/", async (
                Guid childId,
                bool? includeMemories,
                HttpContext http,
                PetBrainPersonalizationAdmin admin,
                CancellationToken ct) =>
            await admin.ResetInferredAsync(
                    http.User.UserIdOrThrow(), childId, includeMemories ?? false, ct) is { } result
                ? Results.Ok(result)
                : Results.NotFound())
            .WithSummary("Öyrənilmiş profili sıfırlayır — AYARLAR və valideyn blokları qalır.");

        personalization.MapGet("/export", async (
                Guid childId,
                HttpContext http,
                PetBrainPersonalizationAdmin admin,
                CancellationToken ct) =>
            await admin.ExportAsync(http.User.UserIdOrThrow(), childId, ct) is { } dto
                ? Results.Ok(dto)
                : Results.NotFound())
            .WithSummary("Profilin tam ixracı — PII-siz.");

        personalization.MapGet("/decisions", async (
                Guid childId,
                HttpContext http,
                PetBrainPersonalizationAdmin admin,
                CancellationToken ct) =>
            await admin.DecisionsAsync(http.User.UserIdOrThrow(), childId, ct) is { } list
                ? Results.Ok(list)
                : Results.NotFound())
            .WithSummary("Son tövsiyə qərarları — «niyə bu macəra?» sualının izi.");

        return app;
    }
}
