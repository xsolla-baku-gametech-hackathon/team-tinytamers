using Microsoft.Extensions.Options;
using PetPal.Api.PetBrain.Media;

namespace PetPal.Api.PetBrain.Recap;

/// <summary>Saxlanmış video — endpoint onu range dəstəyi ilə axınlayır.</summary>
public sealed record RecapVideoFile(string ContentType, string AbsolutePath);

/// <summary>
/// Hazır videonun app-in ÖZ saxlancı.
///
/// <para>Provayderin URL-i qısa ömürlüdür və klientə heç vaxt verilmir: bayt
/// bizim tərəfə köçürülür, uşağın cihazı isə yalnız bizim sahiblik yoxlanan
/// endpoint-imizə sorğu atır.</para>
/// </summary>
public interface IRecapVideoStore
{
    Task<string> SaveAsync(string recapSpecHash, ReadOnlyMemory<byte> bytes, CancellationToken ct = default);

    Task<RecapVideoFile?> OpenAsync(string key, CancellationToken ct = default);
}

public sealed class LocalRecapVideoStore : IRecapVideoStore
{
    private const string Folder = "pet-brain-recaps";

    private readonly string _root;

    public LocalRecapVideoStore(IOptions<PetBrainOptions> options, IWebHostEnvironment environment)
    {
        var configured = options.Value.IllustrationStorageRoot;

        var baseRoot = string.IsNullOrWhiteSpace(configured)
            ? environment.ContentRootPath
            : configured;

        _root = Path.GetFullPath(Path.Combine(baseRoot, Folder));
    }

    public async Task<string> SaveAsync(
        string recapSpecHash, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_root);

        // Fayl adı SABİTDİR: eyni seçimlər eyni videonu verir, deməli təkrar
        // yazı da eyni ünvana düşür.
        var key = $"{Safe(recapSpecHash)}.mp4";

        await File.WriteAllBytesAsync(Path.Combine(_root, key), bytes, ct);

        return key;
    }

    public Task<RecapVideoFile?> OpenAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Task.FromResult<RecapVideoFile?>(null);

        var absolute = Path.GetFullPath(Path.Combine(_root, key));

        // Açar bazadan gəlir, yəni hazırda təhlükəsizdir. Yoxlama gələcək
        // üçündür: oraya bir gün kənar dəyər düşsə, "../.." ilə app-in başqa
        // faylı verilməməlidir.
        if (!absolute.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            !File.Exists(absolute))
            return Task.FromResult<RecapVideoFile?>(null);

        return Task.FromResult<RecapVideoFile?>(new RecapVideoFile(RecapVideoValidator.Mp4, absolute));
    }

    /// <summary>Yalnız hex — açar onsuz da SHA-256 hash-ıdır.</summary>
    private static string Safe(string hash) =>
        new([.. hash.Where(char.IsAsciiHexDigitLower).Take(64)]);
}
