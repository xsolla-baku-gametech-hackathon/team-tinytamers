using Microsoft.Extensions.Options;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>Saxlanmış rəsm faylı — endpoint onu axınla verir.</summary>
public sealed record PuzzleIllustrationFile(string ContentType, string AbsolutePath);

/// <summary>
/// Hazır rəsmin app-in ÖZ saxlancı.
///
/// <para>Provayderin URL-i heç vaxt klientə verilmir: bayt bizim tərəfə
/// köçürülür və sahiblik yoxlanan endpoint-dən paylanır. Beləliklə uşağın
/// cihazı naməlum domenə sorğu atmır.</para>
/// </summary>
public interface IPuzzleIllustrationStore
{
    /// <summary>Baytları yazır və SABİT açar qaytarır (səhnə hash-ı əsasında).</summary>
    Task<string> SaveAsync(string sceneSpecHash, ReadOnlyMemory<byte> bytes, string contentType, CancellationToken ct = default);

    Task<PuzzleIllustrationFile?> OpenAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Diskə yazan implementasiya.
///
/// <para>Kök qovluq <see cref="PetBrainOptions.IllustrationStorageRoot"/> ilə
/// təyin olunur — kəşf şəkillərində olduğu kimi: konteynerin öz diski hər
/// deploy-da silinir, ona görə kalıcı volume ora mount edilir.</para>
/// </summary>
public sealed class LocalPuzzleIllustrationStore : IPuzzleIllustrationStore
{
    private const string Folder = "pet-brain-scenes";

    private readonly string _root;

    public LocalPuzzleIllustrationStore(IOptions<PetBrainOptions> options, IWebHostEnvironment environment)
    {
        var configured = options.Value.IllustrationStorageRoot;

        var baseRoot = string.IsNullOrWhiteSpace(configured)
            ? environment.ContentRootPath
            : configured;

        _root = Path.GetFullPath(Path.Combine(baseRoot, Folder));
    }

    public async Task<string> SaveAsync(
        string sceneSpecHash, ReadOnlyMemory<byte> bytes, string contentType, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_root);

        var key = $"{Safe(sceneSpecHash)}{Extension(contentType)}";

        // Eyni səhnə üçün fayl SABİTDİR: təkrar yazı da eyni ünvana düşür,
        // yəni yenilənmədən sonra uşaq eyni rəsmi görür.
        await File.WriteAllBytesAsync(Path.Combine(_root, key), bytes, ct);

        return key;
    }

    public Task<PuzzleIllustrationFile?> OpenAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Task.FromResult<PuzzleIllustrationFile?>(null);

        var absolute = Path.GetFullPath(Path.Combine(_root, key));

        // Açar bazadan gəlir, yəni hazırda təhlükəsizdir. Yoxlama gələcək
        // üçündür: oraya bir gün kənar dəyər düşsə, "../.." ilə app-in başqa
        // faylı verilməməlidir.
        if (!absolute.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(absolute))
            return Task.FromResult<PuzzleIllustrationFile?>(null);

        return Task.FromResult<PuzzleIllustrationFile?>(
            new PuzzleIllustrationFile(ContentTypeOf(absolute), absolute));
    }

    /// <summary>Yalnız hex simvolları — açar onsuz da SHA-256 hash-ıdır.</summary>
    private static string Safe(string hash) =>
        new([.. hash.Where(char.IsAsciiHexDigitLower).Take(64)]);

    private static string Extension(string contentType) => contentType switch
    {
        PuzzleIllustrationValidator.Png => ".png",
        PuzzleIllustrationValidator.Jpeg => ".jpg",
        PuzzleIllustrationValidator.Webp => ".webp",
        _ => ".bin"
    };

    private static string ContentTypeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => PuzzleIllustrationValidator.Png,
        ".jpg" or ".jpeg" => PuzzleIllustrationValidator.Jpeg,
        ".webp" => PuzzleIllustrationValidator.Webp,
        _ => "application/octet-stream"
    };
}
