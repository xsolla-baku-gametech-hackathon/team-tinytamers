using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Puzzles;

namespace PetPal.Api.Wardrobe;

/// <summary>Saxlanmış dizayn faylı — endpoint onu axınla verir.</summary>
public sealed record WardrobeImageFile(string ContentType, string AbsolutePath);

/// <summary>
/// Dizayn şəkillərinin app-in ÖZ saxlancı.
///
/// <para>Provayderin cavabı bayt kimi gəlir və bizim tərəfdə saxlanılır:
/// uşağın cihazı heç vaxt naməlum domenə sorğu atmır, şəkil yalnız sahiblik
/// yoxlanan endpoint-dən paylanır.</para>
/// </summary>
public interface IWardrobeImageStore
{
    Task<string> SaveAsync(string key, ReadOnlyMemory<byte> bytes, CancellationToken ct = default);

    Task<WardrobeImageFile?> OpenAsync(string key, CancellationToken ct = default);

    Task<byte[]?> ReadAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Saxlanc açarları. Açar YALNIZ bizim qurduğumuz formadadır — uşağın
/// dizaynı <c>designs/{uşaq}/{dizayn}</c>, baza portreti isə
/// <c>base/{barmaq izi}</c>. Başqa hər şey rədd edilir.
/// </summary>
public static partial class WardrobeImageKeys
{
    public static string Design(Guid childId, Guid designId, string contentType) =>
        $"designs/{childId:N}/{designId:N}{Extension(contentType)}";

    public static string Base(string fingerprint, string contentType) =>
        $"base/{fingerprint}{Extension(contentType)}";

    public static bool IsSafe(string? key) => key is not null && SafeKey().IsMatch(key);

    public static string ContentTypeOf(string key) => Path.GetExtension(key) switch
    {
        ".webp" => PuzzleIllustrationValidator.Webp,
        ".jpg" => PuzzleIllustrationValidator.Jpeg,
        _ => PuzzleIllustrationValidator.Png
    };

    private static string Extension(string contentType) => contentType switch
    {
        PuzzleIllustrationValidator.Webp => ".webp",
        PuzzleIllustrationValidator.Jpeg => ".jpg",
        _ => ".png"
    };

    [GeneratedRegex(@"^(designs/[0-9a-f]{32}/[0-9a-f]{32}|base/[0-9a-f]{64})\.(png|jpg|webp)$")]
    private static partial Regex SafeKey();
}

/// <summary>
/// Diskə yazan implementasiya. Kök qovluq tapmaca rəsmləri ilə eynidir
/// (<see cref="PetBrainOptions.IllustrationStorageRoot"/>): konteynerin öz
/// diski hər deploy-da silinir, kalıcı volume isə bir yerə mount edilir.
/// </summary>
public sealed class LocalWardrobeImageStore : IWardrobeImageStore
{
    private const string Folder = "pet-wardrobe";

    private readonly string _root;

    public LocalWardrobeImageStore(IOptions<PetBrainOptions> options, IWebHostEnvironment environment)
    {
        var configured = options.Value.IllustrationStorageRoot;

        var baseRoot = string.IsNullOrWhiteSpace(configured)
            ? environment.ContentRootPath
            : configured;

        _root = Path.GetFullPath(Path.Combine(baseRoot, Folder));
    }

    public async Task<string> SaveAsync(string key, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
    {
        var path = PathOf(key) ?? throw new ArgumentException("Wardrobe: yolverilməz saxlanc açarı.", nameof(key));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, ct);

        return key;
    }

    public Task<WardrobeImageFile?> OpenAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(PathOf(key) is { } path && File.Exists(path)
            ? new WardrobeImageFile(WardrobeImageKeys.ContentTypeOf(key), path)
            : null);

    public async Task<byte[]?> ReadAsync(string key, CancellationToken ct = default) =>
        PathOf(key) is { } path && File.Exists(path)
            ? await File.ReadAllBytesAsync(path, ct)
            : null;

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        if (PathOf(key) is { } path && File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Açar təhlükəsizdirsə mütləq yol, deyilsə <c>null</c>. Açar onsuz da
    /// yalnız bizim qurduğumuz formadadır; kök yoxlaması isə gələcək üçündür —
    /// «../..» ilə app-in başqa faylı heç vaxt verilməməlidir.
    /// </summary>
    private string? PathOf(string key)
    {
        if (!WardrobeImageKeys.IsSafe(key))
            return null;

        var path = Path.GetFullPath(Path.Combine(_root, key));

        return path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? path : null;
    }
}
