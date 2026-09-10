using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace PetPal.Api.Discoveries;

/// <summary>
/// Şəkilləri S3-uyğun obyekt saxlamada saxlayır (AWS S3, Cloudflare R2,
/// Backblaze B2, MinIO — hamısı eyni protokoldur).
///
/// <para>Bu, kəşf şəkillərinin deploy-da itməsi probleminin əsl həllidir:
/// konteyner diski müvəqqətidir, obyekt saxlama isə app-dən kənardadır.
/// Konfiqurasiya yoxdursa bu tətbiq HEÇ QEYDİYYATDAN KEÇMİR — app diskə
/// yazmağa davam edir (bax <see cref="LocalDiscoveryPhotoStore"/>).</para>
///
/// <para><b>Şəkil BİRBAŞA verilmir.</b> Obyektlər gizli qalır və bayt API-dən
/// keçir: ünvan token tələb edir, uşağın şəkli isə açıq linkdə olmamalıdır.
/// İmzalı URL də seçilmədi — belə link paylaşıla bilər və müddəti bitənə qədər
/// hər kəsdə işləyər.</para>
/// </summary>
public class S3DiscoveryPhotoStore : IDiscoveryPhotoStore
{
    private readonly IAmazonS3 _s3;
    private readonly DiscoveryOptions _options;
    private readonly ILogger<S3DiscoveryPhotoStore> _logger;

    public S3DiscoveryPhotoStore(
        IAmazonS3 s3, IOptions<DiscoveryOptions> options, ILogger<S3DiscoveryPhotoStore> logger)
    {
        _s3 = s3;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> SaveAsync(
        Guid childId, ReadOnlyMemory<byte> bytes, string extension, CancellationToken ct = default)
    {
        var key = $"{childId:N}/{Guid.NewGuid():N}{extension}";

        using var stream = new MemoryStream(bytes.ToArray(), writable: false);

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.S3.Bucket,
            Key = ObjectKey(key),
            InputStream = stream,
            ContentType = DiscoveryContentType.ForPath(extension),
            DisablePayloadSigning = _options.S3.DisablePayloadSigning
        }, ct);

        return key;
    }

    public async Task<DiscoveryPhotoFile?> OpenAsync(string key, CancellationToken ct = default)
    {
        var normalized = DiscoveryPhotoKey.Normalize(key, _options.StorageFolder);

        try
        {
            var response = await _s3.GetObjectAsync(_options.S3.Bucket, ObjectKey(normalized), ct);

            // Bayt yaddaşa köçürülür: cavab axını sorğu bitəndən sonra bağlanır,
            // ASP.NET isə faylı sonra yazır. Şəkil onsuz da limitlidir.
            var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            return new DiscoveryPhotoFile(DiscoveryContentType.ForPath(normalized), null, buffer);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Köhnə qeydin faylı köçürülməmiş ola bilər — bu, xəta deyil.
            return null;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(ex, "Kəşf şəkli obyekt saxlamadan alınmadı: {Key}", normalized);
            return null;
        }
    }

    /// <summary>Könüllü prefiks — bir bucket bir neçə mühitlə paylaşıla bilir.</summary>
    private string ObjectKey(string key) =>
        string.IsNullOrWhiteSpace(_options.S3.Prefix)
            ? key
            : $"{_options.S3.Prefix.Trim('/')}/{key}";
}
