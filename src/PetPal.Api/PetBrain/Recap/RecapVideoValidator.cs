using System.Buffers.Binary;

namespace PetPal.Api.PetBrain.Recap;

/// <summary>
/// Video baytlarının MÜSTƏQİL yoxlayıcısı.
///
/// <para>Başlıqlara İNANILMIR: konteyner, ölçü və müddət faylın ÖZÜNDƏN
/// oxunur. Səbəb: bu fayl uşağın ekranında oynadılacaq, provayderin
/// <c>Content-Type</c> dediyi isə sübut deyil.</para>
///
/// <para>MP4 qutuları (<c>ftyp</c>, <c>moov</c> → <c>mvhd</c>, <c>tkhd</c>)
/// oxunur — tam dekoder deyil, amma «oxuna bilməyən fayl rədd olunur» qaydasını
/// təmin etmək üçün kifayətdir.</para>
/// </summary>
public static class RecapVideoValidator
{
    public const string Mp4 = "video/mp4";

    /// <summary>10 saniyəlik portret klip üçün kifayət qədər səxavətli hədd.</summary>
    public const int MaxBytes = 20 * 1024 * 1024;

    /// <summary>Ən kiçik qəbul edilən kadr — bulanıq video uşağa göstərilmir.</summary>
    public const int MinWidth = 360;
    public const int MinHeight = 640;

    /// <summary>Portret tələbi — kətan 390×690-dır.</summary>
    public const double MaxAspectRatio = 0.95;

    /// <summary>Hədəf müddət və icazə verilən sapma.</summary>
    public const double TargetSeconds = 10.0;
    public const double ToleranceSeconds = 0.5;

    public sealed record Check(
        bool IsValid, string ContentType, int Width, int Height, double Seconds, string Reason)
    {
        public static Check Invalid(string reason) => new(false, string.Empty, 0, 0, 0, reason);
    }

    public static Check Validate(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
            return Check.Invalid("empty");

        if (bytes.Length > MaxBytes)
            return Check.Invalid("too-large");

        // `ftyp` qutusu MP4-ün imzasıdır və 4-cü baytdan başlayır.
        if (bytes.Length < 12 ||
            bytes[4] != (byte)'f' || bytes[5] != (byte)'t' || bytes[6] != (byte)'y' || bytes[7] != (byte)'p')
            return Check.Invalid("unsupported-container");

        var duration = ReadDuration(bytes);
        if (duration is not { } seconds)
            return Check.Invalid("undecodable-duration");

        var size = ReadDimensions(bytes);
        if (size is not { } decoded)
            return Check.Invalid("undecodable-dimensions");

        var (width, height) = decoded;

        if (width < MinWidth || height < MinHeight)
            return Check.Invalid("too-small");

        if ((double)width / height > MaxAspectRatio)
            return Check.Invalid("not-portrait");

        // Müddət DAR aralıqdadır: qısa klipi dövrə salmaq və ya sürətləndirmək
        // uşağın gördüyü hərəkəti təhrif edərdi.
        if (Math.Abs(seconds - TargetSeconds) > ToleranceSeconds)
            return Check.Invalid("duration-out-of-contract");

        return new Check(true, Mp4, width, height, seconds, string.Empty);
    }

    /// <summary><c>moov/mvhd</c> qutusundan müddət (saniyə).</summary>
    private static double? ReadDuration(byte[] bytes)
    {
        var mvhd = FindBox(bytes, "mvhd");
        if (mvhd < 0)
            return null;

        // mvhd: version(1) flags(3) [created modified timescale duration]
        var version = bytes[mvhd];

        // Versiya 1-də vaxt sahələri 64 bitdir.
        var offset = version == 1 ? mvhd + 4 + 8 + 8 : mvhd + 4 + 4 + 4;

        if (version == 1)
        {
            if (offset + 12 > bytes.Length)
                return null;

            var timescale64 = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            var duration64 = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(offset + 4, 8));

            return timescale64 == 0 ? null : (double)duration64 / timescale64;
        }

        if (offset + 8 > bytes.Length)
            return null;

        var timescale = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
        var duration = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 4, 4));

        return timescale == 0 ? null : (double)duration / timescale;
    }

    /// <summary><c>tkhd</c> qutusundan kadr ölçüsü (16.16 sabit nöqtə).</summary>
    private static (int Width, int Height)? ReadDimensions(byte[] bytes)
    {
        var tkhd = FindBox(bytes, "tkhd");
        if (tkhd < 0)
            return null;

        var version = bytes[tkhd];

        // tkhd gövdəsi (ISO/IEC 14496-12):
        //   version+flags 4
        //   created / modified / trackId / reserved / duration
        //     — versiya 0-da 4+4+4+4+4 = 20, versiya 1-də 8+8+4+4+8 = 32
        //   reserved[2] 8 · layer+group+volume+reserved 8 · matrix 36
        //   sonra width(4) və height(4) — 16.16 sabit nöqtə.
        var times = version == 1 ? 32 : 20;
        var afterMatrix = tkhd + 4 + times + 8 + 8 + 36;

        if (afterMatrix + 8 > bytes.Length)
            return null;

        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(afterMatrix, 4)) >> 16;
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(afterMatrix + 4, 4)) >> 16;

        return ((int)width, (int)height);
    }

    /// <summary>
    /// Qutunun GÖVDƏSİNİN başlanğıcını tapır.
    ///
    /// <para>Sadə xətti axtarışdır: qutu ağacını tam gəzmək əvəzinə tip
    /// imzasını axtarır. Bu, dekoder deyil — məqsəd faylın oxuna bilməsini
    /// yoxlamaqdır, onu oynatmaq deyil.</para>
    /// </summary>
    private static int FindBox(byte[] bytes, string type)
    {
        var needle = new[] { (byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3] };

        for (var i = 4; i + 4 <= bytes.Length; i++)
        {
            if (bytes[i] == needle[0] && bytes[i + 1] == needle[1] &&
                bytes[i + 2] == needle[2] && bytes[i + 3] == needle[3])
                return i + 4;
        }

        return -1;
    }
}
