using Microsoft.JSInterop;

namespace PetPal.App.Ui.Services;

/// <summary>
/// Dəvət linkindəki dost kodunu oxuyur (<c>?friend=AB12CD</c>).
///
/// <para>Kod yalnız formanı DOLDURUR — dostluq yenə də adi yolla gedir:
/// sorğu yaranır və qarşı tərəf özü qəbul edir. Link bir kliklə
/// dostluq qurmur; belə olsaydı, linki tapan hər kəs uşağın dostuna çevrilərdi.</para>
/// </summary>
public class InviteCodeReader
{
    private readonly IJSRuntime _js;
    private readonly PendingInvite _pending;

    public InviteCodeReader(IJSRuntime js, PendingInvite pending)
    {
        _js = js;
        _pending = pending;
    }

    /// <summary>
    /// Kod iki yoldan gələ bilər: native deep link (<c>petpal://friend/AB12CD</c>)
    /// və ya veb ünvanı (<c>?friend=AB12CD</c>). Native əvvəl yoxlanılır —
    /// app məhz onunla açılıbsa, ünvan sətrində heç nə olmur.
    /// </summary>
    public async Task<string?> ReadAsync()
    {
        if (_pending.Take() is { } fromDeepLink)
            return fromDeepLink;

        try
        {
            return await _js.InvokeAsync<string?>("petpalShare.readFriendCode");
        }
        catch (Exception)
        {
            return null;
        }
    }
}
