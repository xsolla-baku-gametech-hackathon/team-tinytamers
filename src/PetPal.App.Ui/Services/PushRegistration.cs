namespace PetPal.App.Ui.Services;

/// <summary>
/// Cihazın push ünvanını serverdə qeyd edir.
///
/// <para>Qeydiyyat HƏR SESSİYADA bir dəfə edilir: token cihaza bağlıdır, amma
/// sahibi dəyişə bilər — eyni telefonda başqa uşaq profilinə keçmək köhnə
/// profilin bildiriş almasını dayandırmalıdır.</para>
///
/// <para>Token yoxdursa (Firebase qurulmayıb) heç nə etmir və heç bir xəta
/// göstərmir — bildirişlər könüllü qatdır.</para>
/// </summary>
public class PushRegistration
{
    private readonly IPushTokenProvider _tokens;
    private readonly GameApiClient _api;
    private readonly AppSession _session;

    /// <summary>Sonuncu qeyd edilən (token, profil) cütü — təkrar sorğu atılmasın.</summary>
    private (string Token, Guid? ChildId)? _registered;

    public PushRegistration(IPushTokenProvider tokens, GameApiClient api, AppSession session)
    {
        _tokens = tokens;
        _api = api;
        _session = session;
    }

    /// <summary>
    /// Aktiv profilə görə tokeni qeyd edir. Uğursuzluq udulur: bildirişin
    /// qeydə alınmaması app-in işini dayandırmamalıdır.
    /// </summary>
    public async Task SyncAsync()
    {
        var token = await _tokens.GetTokenAsync();

        if (string.IsNullOrWhiteSpace(token))
            return;

        var current = (token, _session.ActiveChildId);

        if (_registered == current)
            return;

        var result = await _api.RegisterDeviceAsync(token, _tokens.Platform);

        if (result.Succeeded)
            _registered = current;
    }
}
