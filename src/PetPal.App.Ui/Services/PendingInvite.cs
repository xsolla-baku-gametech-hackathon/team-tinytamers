namespace PetPal.App.Ui.Services;

/// <summary>
/// Deep link ilə gələn dost kodunu app açılana qədər saxlayır.
///
/// <para>Native tərəf (Android intent, iOS URL scheme) linki UI qurulmazdan
/// ƏVVƏL alır, dostlar ekranı isə xeyli sonra açılır — kod arada bir yerdə
/// gözləməlidir.</para>
///
/// <para>Kod bir dəfə oxunur və silinir: uşaq ekranlar arasında gəzəndə forma
/// təkrar-təkrar dolmamalıdır. Kod yalnız FORMANI doldurur — dostluq yenə də
/// sorğu + qarşı tərəfin qəbulu yolundan keçir.</para>
/// </summary>
public class PendingInvite
{
    private string? _code;

    /// <summary>Native tərəfdən çağırılır. Yararsız kod səssizcə atılır.</summary>
    public void Set(string? rawCode)
    {
        var clean = new string((rawCode ?? string.Empty)
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .Take(6)
            .ToArray());

        _code = clean.Length == 6 ? clean : null;
    }

    /// <summary>Kodu qaytarır və yaddaşdan silir.</summary>
    public string? Take()
    {
        var code = _code;
        _code = null;
        return code;
    }
}
