using PetPal.Shared.Dtos.Auth;
using PetPal.Shared.Enums;

namespace PetPal.App.Ui.Services;

/// <summary>
/// Cari sessiya: hansı token istifadə olunur (valideyn yoxsa uşaq), aktiv uşaq profili kimdir.
/// App boyu tək nüsxə (singleton) saxlanılır və dəyişiklikdə UI xəbərdar edilir.
/// </summary>
public class AppSession
{
    private readonly ITokenStore _tokenStore;
    private readonly Loc _loc;

    public AppSession(ITokenStore tokenStore, Loc loc)
    {
        _tokenStore = tokenStore;
        _loc = loc;
    }

    public event Action? Changed;

    /// <summary>Valideyn sessiyasının tokeni — uşaq profilinə keçmək üçün saxlanılır.</summary>
    public AuthResponse? ParentSession { get; private set; }

    /// <summary>Aktiv uşaq sessiyası (oyun ekranları bu tokeni istifadə edir).</summary>
    public AuthResponse? ChildSession { get; private set; }

    public bool IsParentSignedIn => ParentSession is not null;
    public bool IsChildActive => ChildSession is not null;

    public Guid? ActiveChildId => ChildSession?.ChildId;
    public string ActiveChildName => ActiveChild?.DisplayName ?? string.Empty;

    public ChildSummaryDto? ActiveChild =>
        ChildSession?.Children.FirstOrDefault(c => c.Id == ChildSession.ChildId);

    public List<ChildSummaryDto> Children => ParentSession?.Children ?? [];

    /// <summary>Cari sorğu üçün istifadə olunacaq access token — uşaq sessiyası varsa, o üstündür.</summary>
    public string? CurrentAccessToken => ChildSession?.AccessToken ?? ParentSession?.AccessToken;

    public string? ParentAccessToken => ParentSession?.AccessToken;

    /// <summary>
    /// Valideyn sessiyasını qurur və cihazda saxlayır: refresh token ilə
    /// e-poçt. Beləliklə app növbəti açılışda özü daxil olur, çıxış edilibsə
    /// isə heç olmasa e-poçtu yazmaq lazım gəlmir.
    /// </summary>
    public async Task SignInParentAsync(AuthResponse response, string? email = null)
    {
        ParentSession = response;
        ChildSession = null;
        await _tokenStore.SetAsync(TokenStoreKeys.ParentRefreshToken, response.RefreshToken);

        if (!string.IsNullOrWhiteSpace(email))
            await _tokenStore.SetAsync(TokenStoreKeys.LastEmail, email.Trim());

        // Dil sessiya ilə birlikdə gəlir: valideyn ekranları da ailənin
        // dilində olmalıdır, yoxsa uşaq ingiliscə oynayır, valideyn isə
        // başqa dildə profil yaradır.
        await _loc.UseAsync(response.LanguageCode);

        Notify();
    }

    public async Task ActivateChildAsync(AuthResponse response)
    {
        ChildSession = response;
        if (response.ChildId.HasValue)
            await _tokenStore.SetAsync(TokenStoreKeys.LastChildId, response.ChildId.Value.ToString());

        await _loc.UseAsync(response.LanguageCode);
        Notify();
    }

    /// <summary>
    /// Valideyn uşağın dilini dəyişdi. İnterfeys dərhal keçir, sessiyadakı profil
    /// sətri də yenilənir — uşaq yenidən daxil olmadan yeni dildə davam edir.
    /// </summary>
    public async Task UseChildLanguageAsync(string languageCode)
    {
        if (ActiveChild is { } child)
            child.LanguageCode = languageCode;

        if (ChildSession is not null)
            ChildSession.LanguageCode = languageCode;

        await _loc.UseAsync(languageCode);
        Notify();
    }

    /// <summary>Uşaq sessiyasından çıxıb valideyn profil seçiminə qayıdır.</summary>
    public void ExitChild()
    {
        ChildSession = null;
        Notify();
    }

    public async Task SignOutAsync()
    {
        ParentSession = null;
        ChildSession = null;
        await _tokenStore.RemoveAsync(TokenStoreKeys.ParentRefreshToken);
        await _tokenStore.RemoveAsync(TokenStoreKeys.LastChildId);
        Notify();
    }

    public void UpdateParentChildren(List<ChildSummaryDto> children)
    {
        if (ParentSession is not null)
            ParentSession.Children = children;
        Notify();
    }

    /// <summary>
    /// Refresh nəticəsini cari sessiya tipinə görə yerləşdirir.
    ///
    /// Yeni refresh token MÜTLƏQ saxlanılmalıdır: server rotasiya edir, yəni
    /// köhnə token dərhal ləğv olunur. Saxlanmasa, cihazdakı token ilk
    /// yeniləmədən sonra ölür və app növbəti açılışda girişi yenidən istəyir.
    /// </summary>
    public async Task ApplyRefreshedAsync(AuthResponse response)
    {
        if (response.ProfileKind == ProfileKind.Child)
        {
            ChildSession = response;
        }
        else
        {
            ParentSession = response;
            await _tokenStore.SetAsync(TokenStoreKeys.ParentRefreshToken, response.RefreshToken);
        }

        await _loc.UseAsync(response.LanguageCode);
        Notify();
    }

    /// <summary>Cihazda saxlanmış e-poçt — giriş formasını doldurmaq üçün.</summary>
    public Task<string?> GetStoredEmailAsync() => _tokenStore.GetAsync(TokenStoreKeys.LastEmail);

    /// <summary>
    /// Saxlanmış sessiyanı silir, amma E-POÇTU SAXLAYIR: token ölübsə uşaq
    /// yenidən şifrə yazacaq, e-poçtu isə yenidən yazmağa ehtiyac yoxdur.
    /// </summary>
    public Task ClearStoredSessionAsync() => _tokenStore.RemoveAsync(TokenStoreKeys.ParentRefreshToken);

    public Task<string?> GetStoredParentRefreshTokenAsync() =>
        _tokenStore.GetAsync(TokenStoreKeys.ParentRefreshToken);

    private void Notify() => Changed?.Invoke();
}
