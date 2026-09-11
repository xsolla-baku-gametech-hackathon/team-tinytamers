using PetPal.Api.Common;
using PetPal.Shared.Dtos.Wardrobe;
using PetPal.Shared.Enums;

namespace PetPal.Api.Wardrobe;

/// <summary>Dizaynın şəkil vəziyyəti; <see cref="AssetKey"/> yalnız hazır şəkildə doludur.</summary>
public sealed record WardrobeImageLookup(WardrobeDesignStatus Status, string? AssetKey)
{
    public bool StillDrawing => Status == WardrobeDesignStatus.Pending;
}

/// <summary>
/// Paltar otağının dizayn studiyası. Uşaq metodları uşaq id-sini CLAIM-dən,
/// valideyn metodları valideyn id-sini alır — sorğu gövdəsi heç kimi təyin
/// edə bilmir.
/// </summary>
public interface IWardrobeService
{
    Task<ServiceResult<WardrobeStateDto>> GetStateAsync(Guid childId, CancellationToken ct = default);

    /// <summary>
    /// Uşağın arzusunu qəbul edir. Filtrin saxladığı mətn də SAXLANILIR (valideyn
    /// görür) və dərhal yumşaq mesajla qayıdır; qəbul edilən dizayn isə
    /// arxa fonda çəkilir.
    /// </summary>
    Task<ServiceResult<WardrobeDesignDto>> CreateAsync(
        Guid childId, CreateWardrobeDesignRequest request, CancellationToken ct = default);

    /// <summary>Yad və naməlum dizayn üçün <c>null</c> — «var, amma sənin deyil» də sızmadır.</summary>
    Task<WardrobeImageLookup?> GetImageAsync(Guid childId, Guid designId, CancellationToken ct = default);

    Task<ServiceResult<WardrobeStateDto>> EquipAsync(
        Guid childId, EquipWardrobeDesignRequest request, CancellationToken ct = default);

    /// <summary>Şəkli silir; mətn valideyn baxışı üçün qalır.</summary>
    Task<ServiceResult<WardrobeStateDto>> DeleteAsync(Guid childId, Guid designId, CancellationToken ct = default);

    Task<ServiceResult<ParentWardrobeLogDto>> GetParentLogAsync(
        Guid parentId, Guid childId, int? take, CancellationToken ct = default);

    Task<ServiceResult<WardrobeSettingsRequest>> UpdateSettingsAsync(
        Guid parentId, Guid childId, WardrobeSettingsRequest request, CancellationToken ct = default);

    Task<WardrobeImageLookup?> GetParentImageAsync(
        Guid parentId, Guid childId, Guid designId, CancellationToken ct = default);
}
