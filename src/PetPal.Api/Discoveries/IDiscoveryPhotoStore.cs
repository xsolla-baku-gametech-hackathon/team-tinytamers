namespace PetPal.Api.Discoveries;

/// <summary>
/// Kəşf şəkillərinin saxlandığı yer.
///
/// <para>Abstraksiya ona görə var ki, konteyner diski MÜVƏQQƏTİDİR: qeyd bazada
/// qalır, fayl isə hər deploy-da itirdi və uşaq çəkdiyi şəkli bir daha görmürdü.
/// İki tətbiq var — biri diskə (mount edilmiş volume-a) yazır, digəri
/// S3-uyğun obyekt saxlamaya.</para>
/// </summary>
public interface IDiscoveryPhotoStore
{
    /// <summary>Şəkli saxlayır və bazada saxlanılacaq açarı qaytarır.</summary>
    Task<string> SaveAsync(Guid childId, ReadOnlyMemory<byte> bytes, string extension, CancellationToken ct = default);

    /// <summary>
    /// Şəkli oxumaq üçün açır. Fayl yoxdursa <c>null</c> — köhnə qeydlərin
    /// faylı itmiş ola bilər və bu, xəta deyil, «tapılmadı»dır.
    /// </summary>
    Task<DiscoveryPhotoFile?> OpenAsync(string key, CancellationToken ct = default);
}
