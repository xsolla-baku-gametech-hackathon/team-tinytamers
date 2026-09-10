using Microsoft.Extensions.Options;

namespace PetPal.Api.Discoveries;

/// <summary>
/// Şəkilləri diskə yazır.
///
/// <para><b>Kök qovluq konfiqurasiya oluna bilir</b> (<c>Discovery:StorageRoot</c>).
/// Səbəb budur: konteynerin öz diski müvəqqətidir və hər deploy-da silinir —
/// qeyd bazada qalır, fayl isə yox olurdu. Railway/Fly kimi platformalarda
/// kalıcı volume mount edilib bu dəyərə yazılır. Boş buraxılsa, köhnə davranış
/// qalır (app-in öz qovluğu) — yəni lokal iş üçün heç nə dəyişmir.</para>
/// </summary>
public class LocalDiscoveryPhotoStore : IDiscoveryPhotoStore
{
    private readonly DiscoveryOptions _options;
    private readonly string _root;

    public LocalDiscoveryPhotoStore(IOptions<DiscoveryOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;

        var baseRoot = string.IsNullOrWhiteSpace(_options.StorageRoot)
            ? environment.ContentRootPath
            : _options.StorageRoot;

        // Qovluq adı normallaşdırılır: sondakı "/" kökü fərqli hesablatdırır və
        // yazılan şəkillərin heç biri geri tapılmır.
        _root = Path.GetFullPath(Path.Combine(baseRoot, DiscoveryPhotoKey.Folder(_options.StorageFolder)));
    }

    public async Task<string> SaveAsync(
        Guid childId, ReadOnlyMemory<byte> bytes, string extension, CancellationToken ct = default)
    {
        var folder = Path.Combine(_root, childId.ToString("N"));
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        await File.WriteAllBytesAsync(Path.Combine(folder, fileName), bytes, ct);

        // Açar KÖKDƏN ASILI DEYİL: kök dəyişsə (volume mount edilsə) bazadakı
        // köhnə açarlar yenə oxunmalıdır.
        return $"{childId:N}/{fileName}";
    }

    public Task<DiscoveryPhotoFile?> OpenAsync(string key, CancellationToken ct = default)
    {
        var absolute = Path.GetFullPath(Path.Combine(_root, DiscoveryPhotoKey.Normalize(key, _options.StorageFolder)));

        // Açar bazadan gəlir, yəni hazırda təhlükəsizdir. Yoxlama gələcək üçündür:
        // oraya bir gün kənar dəyər düşsə, "../.." ilə app-in başqa faylı
        // verilməməlidir.
        if (!absolute.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(absolute))
            return Task.FromResult<DiscoveryPhotoFile?>(null);

        return Task.FromResult<DiscoveryPhotoFile?>(
            new DiscoveryPhotoFile(DiscoveryContentType.ForPath(absolute), absolute, null));
    }
}
