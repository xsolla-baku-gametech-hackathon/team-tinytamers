using PetPal.Api.Common;
using PetPal.Api.Entities;
using PetPal.Shared.Dtos.Pets;

namespace PetPal.Api.Pets;

public interface IPetService
{
    Task<ServiceResult<PetDto>> GetAsync(Guid childId, CancellationToken ct = default);

    Task<ServiceResult<CarePetResultDto>> CareAsync(Guid childId, CarePetRequest request, CancellationToken ct = default);

    /// <summary>Yumurtanı ulduzla açır — qulluq və kosmetik əşyalar bundan sonra işə düşür.</summary>
    Task<ServiceResult<HatchPetResultDto>> HatchAsync(Guid childId, CancellationToken ct = default);

    /// <summary>Uşağın dilində yemək kataloqu — qulluq ekranındakı yem qabını doldurur.</summary>
    Task<ServiceResult<List<PetFoodDto>>> GetFoodsAsync(Guid childId, CancellationToken ct = default);

    /// <summary>Pet-in üstündəki əşyaları uşağın seçiminə görə yeniləyir.</summary>
    Task<ServiceResult<PetDto>> EquipAccessoriesAsync(
        Guid childId, EquipAccessoriesRequest request, CancellationToken ct = default);

    /// <summary>Pet-in növünü dəyişir — yalnız görünüş, yaş və statlar qalır.</summary>
    Task<ServiceResult<PetDto>> ChangeSpeciesAsync(
        Guid childId, ChangeSpeciesRequest request, CancellationToken ct = default);

    Task<ServiceResult<PetDto>> RenameAsync(Guid childId, RenamePetRequest request, CancellationToken ct = default);

    /// <summary>Decay tətbiq edilmiş pet obyektini qaytarır (digər servislər üçün daxili giriş nöqtəsi).</summary>
    Task<Pet?> LoadCurrentAsync(Guid childId, CancellationToken ct = default);

    /// <param name="message">
    /// Hazır replika (məsələn AI tərəfindən yaradılmış). Boş buraxılarsa qayda
    /// əsaslı <see cref="PetVoice.Idle"/> istifadə olunur.
    /// </param>
    PetDto ToDto(Pet pet, string childName, string languageCode, string? message = null);
}
