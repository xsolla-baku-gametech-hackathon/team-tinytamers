using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// Tapmacanın TOXUM MÜQAVİLƏSİ — determinizmin bütün yükü buradadır.
///
/// <para>Toxum kanonik bayt təsvirindən SHA-256 ilə çıxarılır. Kodlaşdırma
/// AÇIQ yazılıb ki, illər sonra da eyni nəticə alınsın:</para>
///
/// <list type="bullet">
///   <item><b>Domen etiketi</b> — <c>petpal-puzzle-v1</c>, UTF-8.</item>
///   <item><b>Guid</b> — <c>ToByteArray(bigEndian: true)</c>, 16 bayt. Platformadan
///   asılı sahə sırası qəsdən kənarlaşdırılıb.</item>
///   <item><b>Mətn</b> — UTF-8, uzunluq prefiksi yoxdur (ayırıcı var).</item>
///   <item><b>Tam ədəd</b> — 4 bayt, <b>little-endian</b>.</item>
///   <item><b>Ayırıcı</b> — hər sahədən sonra <c>0x1F</c> (unit separator), yəni
///   qonşu sahələr bir-birinə "yapışa" bilmir.</item>
/// </list>
///
/// <para><b>İşlədilməyənlər və səbəbi:</b> <c>string.GetHashCode()</c> hər
/// prosesdə başqa nəticə verir (hash-flooding müdafiəsi);
/// <c>Random.Shared</c> paylaşılan və toxumlanmayan vəziyyətdir;
/// divar saatı isə eyni run-un bərpasını qeyri-mümkün edərdi. Üçü də burada
/// QADAĞANDIR — testlə qorunur.</para>
/// </summary>
public static class PuzzleSeed
{
    /// <summary>
    /// Generatorun versiyası. Toxum müqaviləsi və ya generasiya qaydaları
    /// dəyişəndə artırılır; verilmiş tapmacalar öz versiyası ilə saxlanılır.
    /// </summary>
    public const int GeneratorVersion = 1;

    private const string Domain = "petpal-puzzle-v1";
    private const byte Separator = 0x1F;

    /// <summary>Kanonik bayt təsviri — toxumun yeganə girişi.</summary>
    public static byte[] Canonical(
        Guid childId,
        Guid runId,
        string blueprintKey,
        int blueprintVersion,
        int targetDifficulty,
        int attempt)
    {
        var buffer = new List<byte>(96);

        AppendText(buffer, Domain);
        AppendGuid(buffer, childId);
        AppendGuid(buffer, runId);
        AppendText(buffer, blueprintKey);
        AppendInt(buffer, blueprintVersion);
        AppendInt(buffer, targetDifficulty);

        // Cəhd sayğacı: namizəd təkrar çıxsa və ya yoxlamadan keçməsə,
        // generasiya BAŞQA toxumla, amma yenə DETERMİNİST təkrarlanır.
        AppendInt(buffer, attempt);

        return [.. buffer];
    }

    /// <summary>Toxumun saxlanan hex təsviri (64 simvol).</summary>
    public static string Hex(
        Guid childId,
        Guid runId,
        string blueprintKey,
        int blueprintVersion,
        int targetDifficulty,
        int attempt) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Canonical(childId, runId, blueprintKey, blueprintVersion, targetDifficulty, attempt)));

    /// <summary>Hex toxumdan generator vəziyyəti — ilk 8 bayt, little-endian.</summary>
    public static ulong StateFrom(string seedHex)
    {
        var bytes = Convert.FromHexString(seedHex);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(0, 8));
    }

    /// <summary>Məzmunun barmaq izi — təkrarı tanımaq üçün (toxumdan AYRIDIR).</summary>
    public static string Signature(string mechanic, IEnumerable<string> parts) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{mechanic}|{string.Join('|', parts)}")))[..32];

    private static void AppendText(List<byte> buffer, string value)
    {
        buffer.AddRange(Encoding.UTF8.GetBytes(value));
        buffer.Add(Separator);
    }

    private static void AppendGuid(List<byte> buffer, Guid value)
    {
        buffer.AddRange(value.ToByteArray(bigEndian: true));
        buffer.Add(Separator);
    }

    private static void AppendInt(List<byte> buffer, int value)
    {
        Span<byte> four = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(four, value);

        buffer.AddRange(four);
        buffer.Add(Separator);
    }
}

/// <summary>
/// SplitMix64 — kiçik, sabit və sürətli determinist generator.
///
/// <para><c>System.Random</c> qəsdən işlədilmir: onun daxili alqoritmi .NET
/// versiyaları arasında dəyişib və gələcəkdə də dəyişə bilər. Tapmaca isə
/// illər sonra eyni qalmalıdır, ona görə alqoritm burada, öz kodumuzda durur.</para>
/// </summary>
public sealed class PuzzleRandom
{
    private ulong _state;

    public PuzzleRandom(ulong seed) => _state = seed;

    public PuzzleRandom(string seedHex) : this(PuzzleSeed.StateFrom(seedHex)) { }

    /// <summary>[minInclusive, maxExclusive) aralığında determinist tam ədəd.</summary>
    public int Next(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            return minInclusive;

        var span = (ulong)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextUInt64() % span);
    }

    /// <summary>Siyahını determinist qarışdırır (Fisher–Yates).</summary>
    public void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = Next(0, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>Siyahıdan determinist <paramref name="count"/> element seçir.</summary>
    public List<T> Take<T>(IReadOnlyList<T> source, int count)
    {
        var copy = source.ToList();
        Shuffle(copy);

        return [.. copy.Take(Math.Clamp(count, 0, copy.Count))];
    }

    private ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15;

        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;

        return z ^ (z >> 31);
    }
}
