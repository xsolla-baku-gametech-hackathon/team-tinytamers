using PetPal.Api.PetBrain.Scenery;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// Rəsm cəhdinin nəticəsi.
///
/// <para>Uğursuzluğun səbəbi <b>qeyd olunur, amma uşağa göstərilmir</b>:
/// ekranda onsuz da tam oynanan deterministik səhnə var, ona görə uşaq üçün
/// "xəta" hadisəsi baş vermir.</para>
/// </summary>
public sealed record PuzzleIllustrationResult(
    bool Succeeded,
    byte[]? Bytes,
    string ContentType,
    string Provider,
    string Model,
    string Reason)
{
    public static PuzzleIllustrationResult Ok(byte[] bytes, string contentType, string provider, string model) =>
        new(true, bytes, contentType, provider, model, string.Empty);

    public static PuzzleIllustrationResult Failed(string reason, string provider = "", string model = "") =>
        new(false, null, string.Empty, provider, model, reason);
}

/// <summary>
/// Hekayə rəsmini hazırlayan qat.
///
/// <para>Tapmaca generasiyasından TAM AYRIDIR və bu, qəsdəndir: rəsm gec
/// gəlsə, pozuq gəlsə, moderasiyadan keçməsə və ya heç gəlməsə də tapmaca
/// dəyişmir. Model nə mexanikanı, nə rəqəmləri, nə variantları, nə həlli, nə
/// çətinliyi, nə də mükafatı seçə bilir.</para>
/// </summary>
public interface IPuzzleIllustrationProvider
{
    /// <summary>AI konfiqurasiya olunubmu — yoxdursa heç bir sorğu getmir.</summary>
    bool IsEnabled { get; }

    Task<PuzzleIllustrationResult> RenderAsync(
        IStoryScene scene, string prompt, CancellationToken ct = default);
}

/// <summary>
/// Standart implementasiya: rəsm YOXDUR.
///
/// <para>Bu, "ehtiyat variant" deyil — ƏSAS variantdır. AI heç vaxt
/// qurulmasa da tapmaca tam oynanır, çünki bütün qaydalar deterministik
/// SVG/CSS səhnəsindədir.</para>
/// </summary>
public sealed class DisabledPuzzleIllustrationProvider : IPuzzleIllustrationProvider
{
    public bool IsEnabled => false;

    public Task<PuzzleIllustrationResult> RenderAsync(
        IStoryScene scene, string prompt, CancellationToken ct = default) =>
        Task.FromResult(PuzzleIllustrationResult.Failed("disabled"));
}
