using PetPal.Shared.Dtos.Auth;
using PetPal.Shared.Dtos.Discovery;
using PetPal.Shared.Dtos.Parent;
using PetPal.Shared.Dtos.PetBrain;

namespace PetPal.App.Ui.Services;

public class ParentApiClient : ApiClientBase
{
    public ParentApiClient(HttpClient http, AppSession session, Loc loc) : base(http, session, loc) { }

    protected override bool UseParentToken => true;

    public Task<ApiResult<List<ChildSummaryDto>>> GetChildrenAsync(CancellationToken ct = default) =>
        GetAsync<List<ChildSummaryDto>>("api/parent/children", ct);

    public Task<ApiResult<ParentDashboardDto>> GetDashboardAsync(Guid childId, CancellationToken ct = default) =>
        GetAsync<ParentDashboardDto>($"api/parent/children/{childId}/dashboard", ct);

    public Task<ApiResult<ParentPersonalizationDto>> GetPersonalizationAsync(
        Guid childId, CancellationToken ct = default) =>
        GetAsync<ParentPersonalizationDto>(
            $"api/parent/pet-brain/children/{childId}/personalization", ct);

    public Task<ApiResult<PetBrainSettingsDto>> UpdatePersonalizationAsync(
        Guid childId, UpdateParentPersonalizationRequest request, CancellationToken ct = default) =>
        PutAsync<UpdateParentPersonalizationRequest, PetBrainSettingsDto>(
            $"api/parent/pet-brain/children/{childId}/personalization", request, ct);

    public Task<ApiResult<bool>> SetContentBlockAsync(
        Guid childId, ParentBlockContentRequest request, CancellationToken ct = default) =>
        PostAsync<ParentBlockContentRequest, bool>(
            $"api/parent/pet-brain/children/{childId}/personalization/blocks", request, ct);

    public Task<ApiResult<PetBrainResetResultDto>> ResetPersonalizationAsync(
        Guid childId, bool includeMemories, CancellationToken ct = default) =>
        DeleteAsync<PetBrainResetResultDto>(
            $"api/parent/pet-brain/children/{childId}/personalization?includeMemories={(includeMemories ? "true" : "false")}", ct);

    public Task<ApiResult<PetBrainProfileExportDto>> ExportPersonalizationAsync(
        Guid childId, CancellationToken ct = default) =>
        GetAsync<PetBrainProfileExportDto>(
            $"api/parent/pet-brain/children/{childId}/personalization/export", ct);

    public Task<ApiResult<List<ParentDecisionEntryDto>>> GetPersonalizationDecisionsAsync(
        Guid childId, CancellationToken ct = default) =>
        GetAsync<List<ParentDecisionEntryDto>>(
            $"api/parent/pet-brain/children/{childId}/personalization/decisions", ct);

    /// <summary>Uşağın dilini dəyişir — app-in bütün mətni bu dilə keçir.</summary>
    public Task<ApiResult<ChildSummaryDto>> UpdateLanguageAsync(
        Guid childId, string languageCode, CancellationToken ct = default) =>
        PutAsync<ChildLanguageRequest, ChildSummaryDto>(
            $"api/parent/children/{childId}/language",
            new ChildLanguageRequest { LanguageCode = languageCode }, ct);

    public Task<ApiResult<ScreenTimeSettingsDto>> UpdateScreenTimeAsync(
        Guid childId, ScreenTimeSettingsDto settings, CancellationToken ct = default) =>
        PutAsync<ScreenTimeSettingsDto, ScreenTimeSettingsDto>(
            $"api/parent/children/{childId}/screen-time", settings, ct);

    /// <summary>Uşağın kəşf kolleksiyası — uşağın öz ekranındakı ilə eyni siyahı.</summary>
    public Task<ApiResult<List<DiscoveryDto>>> GetDiscoveriesAsync(Guid childId, CancellationToken ct = default) =>
        GetAsync<List<DiscoveryDto>>($"api/parent/children/{childId}/discoveries", ct);


    // ---------- Söhbət ----------

    /// <summary>
    /// Uşağın pet ilə söhbət tarixçəsi. Valideyn uşağın NƏ yazdığını görməlidir —
    /// söhbət açarını məsuliyyətlə idarə etməyin şərti budur.
    /// </summary>
    public Task<ApiResult<ParentChatLogDto>> GetChatLogAsync(Guid childId, CancellationToken ct = default) =>
        GetAsync<ParentChatLogDto>($"api/parent/children/{childId}/chat", ct);

    public Task<ApiResult<ChatSettingsRequest>> UpdateChatSettingsAsync(
        Guid childId, bool enabled, CancellationToken ct = default) =>
        PutAsync<ChatSettingsRequest, ChatSettingsRequest>(
            $"api/parent/children/{childId}/chat", new ChatSettingsRequest { Enabled = enabled }, ct);

    // ---------- Bilik Arenası ----------

    /// <summary>
    /// Arena açarları. <paramref name="friendsOnly"/> standart olaraq BAĞLIDIR:
    /// rəqib hovuzu hamıdır və bu, valideynin könüllü daralmasıdır.
    /// </summary>
    public Task<ApiResult<ArenaSettingsRequest>> UpdateArenaSettingsAsync(
        Guid childId, bool enabled, bool friendsOnly, CancellationToken ct = default) =>
        PutAsync<ArenaSettingsRequest, ArenaSettingsRequest>(
            $"api/parent/children/{childId}/arena",
            new ArenaSettingsRequest { Enabled = enabled, FriendsOnly = friendsOnly }, ct);

    public Task<ApiResult<string>> GetDiscoveryPhotoAsync(
        Guid childId, Guid discoveryId, CancellationToken ct = default) =>
        GetDataUrlAsync($"api/parent/children/{childId}/discoveries/{discoveryId}/photo", ct);

    // Dostluq sorğuları burada YOXDUR: cavabı uşaq özü verir, ona görə çağırış
    // GameApiClient-dədir (RespondToFriendRequestAsync).

    // ---------- Qapı ----------

    public Task<ApiResult<ParentGateStatusDto>> GetGateStatusAsync(CancellationToken ct = default) =>
        GetAsync<ParentGateStatusDto>("api/parent/gate", ct);

    public Task<ApiResult<ParentGateStatusDto>> SetGatePinAsync(
        string newPin, string? password = null, string? currentPin = null, CancellationToken ct = default) =>
        PutAsync<SetParentPinRequest, ParentGateStatusDto>(
            "api/parent/gate/pin",
            new SetParentPinRequest { NewPin = newPin, Password = password, CurrentPin = currentPin }, ct);

    /// <summary>
    /// PIN-i SERVERDƏ yoxlayır. Qapının bütün mənası buradadır: cavab kodda
    /// deyil, uşaq nə görə, nə də təxmin edə bilir.
    /// </summary>
    public Task<ApiResult<bool>> UnlockGateAsync(string pin, CancellationToken ct = default) =>
        PostAsync<ParentGateUnlockRequest, bool>(
            "api/parent/gate/unlock", new ParentGateUnlockRequest { Pin = pin }, ct);
}
