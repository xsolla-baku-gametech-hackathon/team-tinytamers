namespace PetPal.App.Ui.Services;

/// <summary>
/// App açılanda cihazda saxlanmış valideyn sessiyasını bərpa edir.
///
/// Səbəb sadədir: giriş bir dəfə edilir. Refresh token cihazda saxlanılır
/// (mobil/masaüstündə SecureStorage, brauzerdə localStorage), açılışda isə
/// onunla yeni access token alınır — yəni istifadəçi çıxış etməyibsə, e-poçt və
/// şifrə bir daha soruşulmur.
///
/// Token ölübsə (müddəti bitib və ya başqa cihazdan çıxış edilib) yalnız token
/// silinir, e-poçt qalır: giriş formasında ünvanı yenidən yazmaq lazım gəlmir.
/// </summary>
public class SessionRestorer
{
    private readonly AuthApiClient _auth;
    private readonly AppSession _session;
    private readonly Loc _loc;

    private Task? _restore;

    public SessionRestorer(AuthApiClient auth, AppSession session, Loc loc)
    {
        _auth = auth;
        _session = session;
        _loc = loc;
    }

    /// <summary>Bərpa bir dəfə işləyir — eyni Task bütün çağırışlara qaytarılır.</summary>
    public Task RestoreAsync() => _restore ??= RunAsync();

    private async Task RunAsync()
    {
        // Dil tokendən ƏVVƏL bərpa olunur: token ölü çıxsa da, giriş ekranı
        // sonuncu dildə açılmalıdır.
        await _loc.RestoreAsync();

        var refreshToken = await _session.GetStoredParentRefreshTokenAsync();
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var result = await _auth.RefreshAsync(refreshToken);

        if (result.Succeeded && result.Value is not null)
        {
            await _session.SignInParentAsync(result.Value);
            return;
        }

        await _session.ClearStoredSessionAsync();
    }
}
