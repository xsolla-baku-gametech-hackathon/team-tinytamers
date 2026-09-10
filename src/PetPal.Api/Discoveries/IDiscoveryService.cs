using PetPal.Api.Common;
using PetPal.Shared.Dtos.Discovery;

namespace PetPal.Api.Discoveries;

public interface IDiscoveryService
{
    Task<ServiceResult<DiscoveryResultDto>> CreateAsync(Guid childId, DiscoveryRequest request, CancellationToken ct = default);

    Task<List<DiscoveryDto>> GetRecentAsync(Guid childId, int take = 20, CancellationToken ct = default);

    /// <summary>
    /// Kəşfin şəkli. Uşaq id-si parametrdir, tapıntı deyil: sorğu HƏMİŞƏ
    /// "bu uşağın bu kəşfi" kimi qurulur, yəni başqa uşağın şəklinin id-si
    /// bilinsə belə "tapılmadı" qayıdır.
    /// </summary>
    Task<ServiceResult<DiscoveryPhotoFile>> GetPhotoAsync(
        Guid childId, Guid discoveryId, CancellationToken ct = default);
}

/// <summary>
/// Şəklin cavaba veriləcək forması.
///
/// <para>Diskdə saxlanılanda YOL verilir, baytlar yox: 30 MB-lıq şəkli yaddaşa
/// oxumaq həmin problemi (bax <see cref="DiscoveryService"/>) oxu tərəfində
/// təkrarlayardı — <c>Results.File</c> onu birbaşa axına verir.</para>
///
/// <para>Obyekt saxlamada isə yerli fayl yoxdur, ona görə axın verilir.
/// İkisi eyni anda dolu olmur.</para>
/// </summary>
/// <param name="AbsolutePath">Tam yol — <c>Results.File</c> nisbi yol qəbul etmir.</param>
/// <param name="Content">Obyekt saxlamadan gələn axın.</param>
public record DiscoveryPhotoFile(string ContentType, string? AbsolutePath, Stream? Content);

