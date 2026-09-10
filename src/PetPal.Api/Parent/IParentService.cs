using PetPal.Api.Common;
using PetPal.Api.Discoveries;
using PetPal.Shared.Dtos.Auth;
using PetPal.Shared.Dtos.Discovery;
using PetPal.Shared.Dtos.Parent;

namespace PetPal.Api.Parent;

public interface IParentService
{
    Task<ServiceResult<ParentDashboardDto>> GetDashboardAsync(Guid parentId, Guid childId, CancellationToken ct = default);

    Task<ServiceResult<ScreenTimeSettingsDto>> UpdateScreenTimeAsync(
        Guid parentId, Guid childId, ScreenTimeSettingsDto settings, CancellationToken ct = default);

    /// <summary>Uşağın dilini dəyişir — app-in bütün mətni bununla dəyişir.</summary>
    Task<ServiceResult<ChildSummaryDto>> UpdateLanguageAsync(
        Guid parentId, Guid childId, ChildLanguageRequest request, CancellationToken ct = default);

    Task<ServiceResult<List<ChildSummaryDto>>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);

    /// <summary>
    /// Uşağın "Real Life Connect" kolleksiyası. Valideyn ekrandan kənarda nə
    /// tapıldığını görməlidir — panelin qalan hissəsi yalnız rəqəmlərdir.
    /// </summary>
    Task<ServiceResult<List<DiscoveryDto>>> GetDiscoveriesAsync(
        Guid parentId, Guid childId, int take = 20, CancellationToken ct = default);


    /// <summary>
    /// Uşağın pet ilə söhbət tarixçəsi. Valideyn uşağın NƏ yazdığını görməlidir —
    /// bu, söhbət özəlliyini valideyn üçün qəbul edilən edən əsas şərtdir.
    /// </summary>
    Task<ServiceResult<ParentChatLogDto>> GetChatLogAsync(
        Guid parentId, Guid childId, int? take = null, CancellationToken ct = default);

    /// <summary>Söhbəti açır və ya bağlayır.</summary>
    Task<ServiceResult<ChatSettingsRequest>> UpdateChatSettingsAsync(
        Guid parentId, Guid childId, ChatSettingsRequest request, CancellationToken ct = default);

    /// <summary>
    /// Arenanı açır/bağlayır və rəqib hovuzunu idarə edir. Hovuz standart olaraq
    /// hamıdır; "yalnız dostlar" valideynin könüllü daralmasıdır.
    /// </summary>
    Task<ServiceResult<ArenaSettingsRequest>> UpdateArenaSettingsAsync(
        Guid parentId, Guid childId, ArenaSettingsRequest request, CancellationToken ct = default);

    Task<ServiceResult<DiscoveryPhotoFile>> GetDiscoveryPhotoAsync(
        Guid parentId, Guid childId, Guid discoveryId, CancellationToken ct = default);

}
