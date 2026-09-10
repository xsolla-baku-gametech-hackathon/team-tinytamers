namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// Modeldən gələn baytların MÜSTƏQİL yoxlayıcısı.
///
/// <para>Başlıqlara (<c>Content-Type</c>) İNANILMIR: format baytların özündən
/// oxunur. Səbəb sadədir — provayder "image/png" desə də içəri başqa şey
/// gələ bilər, biz isə onu uşağın ekranına qoyuruq.</para>
///
/// <para>Ölçülər dekod olunmuş başlıqdan alınır, yəni "1×1 piksel" və ya
/// yataylıq kimi halların hamısı burada dayanır.</para>
/// </summary>
public static class PuzzleIllustrationValidator
{
    /// <summary>Ən böyük qəbul edilən fayl — mobil şəbəkə üçün.</summary>
    public const int MaxBytes = 3 * 1024 * 1024;

    /// <summary>Kiçik rəsm 390 piksellik kətanda bulanıq görünür.</summary>
    public const int MinWidth = 512;

    public const int MinHeight = 640;

    /// <summary>Portret tələbi — kətan 390×690-dır.</summary>
    public const double MaxAspectRatio = 0.95;

    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";
    public const string Webp = "image/webp";

    /// <summary>Yoxlama nəticəsi: səbəb yalnız loga düşür.</summary>
    public sealed record Check(bool IsValid, string ContentType, int Width, int Height, string Reason)
    {
        public static Check Invalid(string reason) => new(false, string.Empty, 0, 0, reason);
    }

    public static Check Validate(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
            return Check.Invalid("empty");

        if (bytes.Length > MaxBytes)
            return Check.Invalid("too-large");

        var format = Sniff(bytes);
        if (format is null)
            return Check.Invalid("unsupported-format");

        var size = format switch
        {
            Png => PngSize(bytes),
            Jpeg => JpegSize(bytes),
            Webp => WebpSize(bytes),
            _ => null
        };

        if (size is not { } decoded)
            return Check.Invalid("undecodable");

        var (width, height) = decoded;

        if (width <= 0 || height <= 0)
            return Check.Invalid("undecodable");

        if (width < MinWidth || height < MinHeight)
            return Check.Invalid("too-small");

        if ((double)width / height > MaxAspectRatio)
            return Check.Invalid("not-portrait");

        return new Check(true, format, width, height, string.Empty);
    }

    /// <summary>Formatı SEHRLİ BAYTLARDAN tanıyır — başlıqdan yox.</summary>
    private static string? Sniff(byte[] b)
    {
        if (b.Length >= 8 &&
            b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
            b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return Png;

        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return Jpeg;

        if (b.Length >= 12 &&
            b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F' &&
            b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P')
            return Webp;

        return null;
    }

    private static (int Width, int Height)? PngSize(byte[] b)
    {
        // IHDR həmişə ilk chunk-dır: 8 bayt imza + 4 uzunluq + 4 tip.
        if (b.Length < 24)
            return null;

        return (ReadBigEndian(b, 16), ReadBigEndian(b, 20));
    }

    private static (int Width, int Height)? JpegSize(byte[] b)
    {
        var i = 2;

        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF)
            {
                i++;
                continue;
            }

            var marker = b[i + 1];

            // SOF0–SOF15 (DHT/DAC/RST istisna) ölçüləri daşıyır.
            if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
                return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);

            var length = (b[i + 2] << 8) | b[i + 3];
            if (length < 2)
                return null;

            i += 2 + length;
        }

        return null;
    }

    private static (int Width, int Height)? WebpSize(byte[] b)
    {
        if (b.Length < 30)
            return null;

        // VP8X (genişləndirilmiş) formatı: ölçülər 24-cü baytdan, 24-bit, -1.
        if (b[12] == (byte)'V' && b[13] == (byte)'P' && b[14] == (byte)'8' && b[15] == (byte)'X')
        {
            var width = 1 + (b[24] | (b[25] << 8) | (b[26] << 16));
            var height = 1 + (b[27] | (b[28] << 8) | (b[29] << 16));

            return (width, height);
        }

        // VP8L (itkisiz): 14 bitlik en və hündürlük, hər ikisi -1.
        if (b[12] == (byte)'V' && b[13] == (byte)'P' && b[14] == (byte)'8' && b[15] == (byte)'L')
        {
            var bits = b[21] | (b[22] << 8) | (b[23] << 16) | (b[24] << 24);

            return (1 + (bits & 0x3FFF), 1 + ((bits >> 14) & 0x3FFF));
        }

        // VP8 (itkili): ölçülər açar kadrın başlığındadır.
        if (b[12] == (byte)'V' && b[13] == (byte)'P' && b[14] == (byte)'8' && b[15] == (byte)' ')
        {
            var width = ((b[27] << 8) | b[26]) & 0x3FFF;
            var height = ((b[29] << 8) | b[28]) & 0x3FFF;

            return (width, height);
        }

        return null;
    }

    private static int ReadBigEndian(byte[] b, int offset) =>
        (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];
}
