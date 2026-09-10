using PetPal.Api.Common;
using PetPal.Shared.Dtos.PetBrain;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Pet Brain-in əməliyyat qatı. Bütün metodlar uşaq id-sini CLAIM-dən alır —
/// heç bir sorğu gövdəsi uşağı təyin edə bilmir.
/// </summary>
public interface IPetBrainService
{
    /// <summary>Giriş ekranı: profil, yaddaş, bağ, xarakter, tövsiyə və aktiv run.</summary>
    Task<ServiceResult<PetBrainStateDto>> GetStateAsync(Guid childId, CancellationToken ct = default);

    /// <summary>
    /// Tövsiyə olunan təcrübəni başladır. Ekran vaxtı bloku YENİ run-a maneədir.
    /// </summary>
    Task<ServiceResult<PetBrainRunDto>> StartRunAsync(
        Guid childId, StartPetBrainRunRequest request, CancellationToken ct = default);

    /// <summary>Uşağın ÖZ run-unu qaytarır — yad run <c>404</c> alır.</summary>
    Task<ServiceResult<PetBrainRunDto>> GetRunAsync(Guid childId, Guid runId, CancellationToken ct = default);

    /// <summary>Bir mərhələnin cavabı və ya ipucu istəyi.</summary>
    Task<ServiceResult<PetBrainRunDto>> SubmitChoiceAsync(
        Guid childId, Guid runId, PetBrainChoiceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Yalnız BÜTÜN mərhələləri keçilmiş run-u tamamlayır və mükafatı DƏQİQ BİR
    /// DƏFƏ tətbiq edir. Təkrar çağırış eyni yekunu qaytarır.
    /// </summary>
    Task<ServiceResult<PetBrainRunDto>> CompleteRunAsync(Guid childId, Guid runId, CancellationToken ct = default);

    /// <summary>Yarımçıq qoyur — tamamlama mükafatı verilmir.</summary>
    Task<ServiceResult<PetBrainRunDto>> AbandonRunAsync(Guid childId, Guid runId, CancellationToken ct = default);

    /// <summary>
    /// Uşağın ÖZ tapmacasının rəsm vəziyyəti və hazırdırsa saxlanc açarı.
    ///
    /// <para>Yad və naməlum tapmaca üçün <c>null</c> qaytarır — endpoint ona
    /// <c>404</c> cavab verir, çünki "var, amma sənin deyil" cavabı da məlumat
    /// sızmasıdır. Sahibinə isə üç hal ayrılır: hazır rəsm, hələ çəkilən rəsm
    /// və heç vaxt gəlməyəcək rəsm — sonuncu klientə gözləməni dayandırmağa
    /// imkan verir.</para>
    /// </summary>
    Task<PuzzleIllustrationLookup?> GetIllustrationAsync(Guid childId, Guid puzzleId, CancellationToken ct = default);

    /// <summary>
    /// Uşağın ÖZ macərasının recap videosunun saxlanc açarı.
    ///
    /// <para>Yad run, hazır olmayan video və naməlum id üçün <c>null</c>.
    /// Provayderin URL-i heç vaxt qaytarılmır.</para>
    /// </summary>
    Task<string?> GetRecapVideoKeyAsync(Guid childId, Guid runId, CancellationToken ct = default);

    /// <summary>
    /// Ana ekranın aqreqatı üçün kompakt təklif. Ayrıca HTTP sorğusu OLMASIN
    /// deyə <c>HomeService</c> bunu birbaşa çağırır.
    /// </summary>
    Task<PetBrainHomeChipDto?> GetHomeChipAsync(Guid childId, CancellationToken ct = default);

    /// <summary>
    /// Valideynin ÖZ uşaqlarının müqayisəsi — nümayişin "eyni oyun, fərqli uşaq"
    /// hissəsi. Sahiblik yoxlanılır.
    /// </summary>
    Task<ServiceResult<PetBrainComparisonDto>> CompareChildrenAsync(
        Guid parentUserId, CancellationToken ct = default);
}
