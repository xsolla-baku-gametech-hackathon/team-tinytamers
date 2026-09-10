using System.Security.Cryptography;

namespace PetPal.Api.Security;

/// <summary>
/// 6 simvolluq dost kodu. Qarışdırıla bilən simvollar (0/O, 1/I) çıxarılıb —
/// uşaq kodu səhv oxumasın deyə.
/// </summary>
public static class FriendCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Create()
    {
        Span<char> buffer = stackalloc char[6];
        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

        return new string(buffer);
    }
}
