namespace PetPal.Api.Discoveries;

/// <summary>
/// Şəkil növünün müəyyən edilməsi. Servisdən AYRILIB, çünki eyni qaydaya həm
/// yükləmə (uzantı seçimi), həm də saxlama tətbiqləri (MIME seçimi) baxır.
/// </summary>
public static class DiscoveryContentType
{
    /// <summary>
    /// Uzantı faylın ADINDAN deyil, MƏZMUNUNDAN seçilir: klient nə göndərdiyini
    /// düzgün deməyə bilər, kamera isə PNG/WebP də verə bilir. Hamısını ".jpg"
    /// adlandırmaq yüklənəndə problem yaratmırdı — şəkil geri VERİLDİYİ üçün
    /// yaradır: səhv Content-Type ilə brauzer şəkli göstərməyə bilər.
    /// Naməlum məzmun köhnə davranışda qalır.
    /// </summary>
    public static string ExtensionFor(ReadOnlySpan<byte> bytes) => bytes switch
    {
        [0xFF, 0xD8, 0xFF, ..] => ".jpg",
        [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, ..] => ".png",
        [(byte)'G', (byte)'I', (byte)'F', (byte)'8', ..] => ".gif",
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => ".webp",
        _ => ".jpg"
    };

    /// <summary>Uzantı → MIME. Siyahı <see cref="ExtensionFor"/> ilə eyni olmalıdır.</summary>
    public static string ForPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}
