using System.Text;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Prosesdən-prosesə DƏYİŞMƏYƏN hash.
///
/// <para><c>string.GetHashCode()</c> .NET-də hər proses üçün təsadüfi toxumla
/// işləyir (hash-flooding müdafiəsi). Yəni onunla seçilən "sürpriz" app hər
/// yenidən başlayanda başqa olardı və nümayiş təkrarlana bilməzdi. FNV-1a
/// isə sabitdir: eyni giriş həmişə eyni rəqəmi verir.</para>
/// </summary>
public static class StableHash
{
    private const ulong FnvOffsetBasis = 14695981039346656037;
    private const ulong FnvPrime = 1099511628211;

    public static ulong Of(string value)
    {
        var hash = FnvOffsetBasis;

        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        return hash;
    }

    /// <summary>0.0–1.0 aralığında determinist kəsr — sürpriz payı üçün.</summary>
    public static double Unit(string value) => (Of(value) % 10_000) / 10_000.0;
}
