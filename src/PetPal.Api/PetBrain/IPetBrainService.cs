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
    /// «Başqa fikir» və «sonra».
    ///
    /// <para>Cavab serverin verdiyi <c>DecisionId</c>-yə bağlanır: klient nə
    /// şablon, nə də xassə dəyişikliyi göndərə bilir. Heç bir cavab marağı
    /// AZALTMIR — hər ikisi yalnız cari sessiyada kartı kənara qoyur.</para>
    /// </summary>
    Task<ServiceResult<PetBrainStateDto>> SubmitFeedbackAsync(
        Guid childId, PetBrainFeedbackRequest request, CancellationToken ct = default);

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
    /// Macərəni dayandırır — vəziyyət qalır, mükafat hüququ itmir.
    ///
    /// <para><see cref="AbandonRunAsync"/>-dan fərqi əsaslıdır: yarımçıq qoymaq
    /// imtina deyil. Uzun macərada uşaq nahara çağırıla bilər.</para>
    /// </summary>
    Task<ServiceResult<PetBrainResumeDto>> PauseRunAsync(
        Guid childId, Guid runId, PetBrainPauseRequest request, CancellationToken ct = default);

    /// <summary>Dayandırılmış macərəni ən son checkpoint-dən davam etdirir.</summary>
    Task<ServiceResult<PetBrainRunDto>> ResumeRunAsync(Guid childId, Guid runId, CancellationToken ct = default);

    /// <summary>Davam edilə bilən macəranın kartı — ana ekran və hub üçün.</summary>
    Task<PetBrainResumeDto?> GetResumeCardAsync(Guid childId, CancellationToken ct = default);

    /// <summary>Fəsilli macəralar — uşağın öz irəliləməsi ilə.</summary>
    Task<ServiceResult<List<PetBrainAdventureSummaryDto>>> GetAdventuresAsync(
        Guid childId, CancellationToken ct = default);

    /// <summary>Bir macəranın ön baxışı.</summary>
    Task<ServiceResult<PetBrainAdventurePreviewDto>> GetAdventureAsync(
        Guid childId, string key, CancellationToken ct = default);

    /// <summary>Mərkəzdən başlamaq, davam etmək və ya təkrar oynamaq.</summary>
    Task<ServiceResult<PetBrainRunDto>> StartAdventureAsync(
        Guid childId, string key, CancellationToken ct = default);

    /// <summary>Açıq run-ın vəziyyətinin bir hissəsi — məqsəd, çanta, jurnal, xəritə.</summary>
    Task<ServiceResult<T>> GetAdventurePartAsync<T>(
        Guid childId, Guid runId, Func<PetBrainAdventureStateDto, T> select, CancellationToken ct = default);

    /// <summary>Tapmaca cavabı — cari addımın tapmacasının id-si ilə.</summary>
    Task<ServiceResult<PetBrainRunDto>> SubmitPuzzleAnswerAsync(
        Guid childId, Guid runId, Guid puzzleId, PetBrainChoiceRequest request, CancellationToken ct = default);

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
