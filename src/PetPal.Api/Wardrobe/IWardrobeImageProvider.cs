namespace PetPal.Api.Wardrobe;

/// <summary>
/// Şəkil cəhdinin nəticəsi.
///
/// <para><see cref="Refused"/> ayrıca saxlanılır: modelin öz təhlükəsizlik
/// sisteminin rəddi texniki xəta DEYİL — uşağa «başqa paltar fikirləşək»
/// deyilir və cəhd təkrarlanmır. Texniki xəta isə uşağın həddindən sayılmır.</para>
/// </summary>
public sealed record WardrobeImageResult(bool Succeeded, bool Refused, byte[]? Bytes, string Reason)
{
    public static WardrobeImageResult Ok(byte[] bytes) => new(true, false, bytes, string.Empty);

    public static WardrobeImageResult Refusal() => new(false, true, null, "moderation_blocked");

    public static WardrobeImageResult Failed(string reason) => new(false, false, null, reason);
}

/// <summary>
/// Pet-i uşağın arzuladığı paltarda çəkən qat.
///
/// <para>İki yol var: sıfırdan çəkmək (<see cref="GenerateAsync"/>) və baza
/// portretini redaktə etmək (<see cref="EditAsync"/>). İkincisi pet-i hər
/// dizaynda EYNİ saxlayır — uşaq üçün «bu mənim pet-imdir» hissi məhz budur.</para>
/// </summary>
public interface IWardrobeImageProvider
{
    /// <summary>Xidmət qurulubmu — deyilsə heç bir sorğu getmir.</summary>
    bool IsEnabled { get; }

    string Name { get; }

    string Model { get; }

    Task<WardrobeImageResult> GenerateAsync(string prompt, CancellationToken ct = default);

    Task<WardrobeImageResult> EditAsync(
        string prompt, byte[] reference, string referenceContentType, CancellationToken ct = default);
}

/// <summary>
/// Standart implementasiya: şəkil YOXDUR. Paltar otağı adi əşyalarla tam
/// işləyir, dizayn studiyası isə «dərzi hələ gəlməyib» deyir.
/// </summary>
public sealed class DisabledWardrobeImageProvider : IWardrobeImageProvider
{
    public bool IsEnabled => false;

    public string Name => "none";

    public string Model => string.Empty;

    public Task<WardrobeImageResult> GenerateAsync(string prompt, CancellationToken ct = default) =>
        Task.FromResult(WardrobeImageResult.Failed("disabled"));

    public Task<WardrobeImageResult> EditAsync(
        string prompt, byte[] reference, string referenceContentType, CancellationToken ct = default) =>
        Task.FromResult(WardrobeImageResult.Failed("disabled"));
}
