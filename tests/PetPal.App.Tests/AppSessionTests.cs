using PetPal.App.Ui.Services;
using PetPal.Shared.Dtos.Auth;
using PetPal.Shared.Enums;

namespace PetPal.App.Tests;

/// <summary>
/// Sessiya idarəetməsi: uşaq sessiyası aktivdirsə sorğular onun tokeni ilə gedir,
/// uşaq sessiyası bitəndə valideyn tokeni qalır.
/// </summary>
public class AppSessionTests
{
    [Fact]
    public async Task SignInParent_ValideynSessiyasiniQurur()
    {
        var session = NewSession();

        await session.SignInParentAsync(ParentResponse());

        Assert.True(session.IsParentSignedIn);
        Assert.False(session.IsChildActive);
        Assert.Equal("parent-token", session.CurrentAccessToken);
    }

    [Fact]
    public async Task ActivateChild_UsaqTokeniUstunOlur()
    {
        var session = NewSession();
        await session.SignInParentAsync(ParentResponse());

        await session.ActivateChildAsync(ChildResponse());

        Assert.True(session.IsChildActive);
        Assert.Equal("child-token", session.CurrentAccessToken);
        Assert.Equal("parent-token", session.ParentAccessToken);
    }

    [Fact]
    public async Task ExitChild_ValideynSessiyasiniSaxlayir()
    {
        var session = NewSession();
        await session.SignInParentAsync(ParentResponse());
        await session.ActivateChildAsync(ChildResponse());

        session.ExitChild();

        Assert.False(session.IsChildActive);
        Assert.True(session.IsParentSignedIn);
        Assert.Equal("parent-token", session.CurrentAccessToken);
    }

    [Fact]
    public async Task SignOut_HerIkiSessiyaniTemizleyir()
    {
        var session = NewSession();
        await session.SignInParentAsync(ParentResponse());
        await session.ActivateChildAsync(ChildResponse());

        await session.SignOutAsync();

        Assert.False(session.IsParentSignedIn);
        Assert.False(session.IsChildActive);
        Assert.Null(session.CurrentAccessToken);
    }

    [Fact]
    public async Task ActivateChild_AktivUsagiTapir()
    {
        var session = NewSession();
        await session.SignInParentAsync(ParentResponse());
        await session.ActivateChildAsync(ChildResponse());

        Assert.Equal("Ava", session.ActiveChildName);
        Assert.NotNull(session.ActiveChildId);
    }

    [Fact]
    public async Task ApplyRefreshed_TokeniDogruSessiyayaYazir()
    {
        var session = NewSession();
        await session.SignInParentAsync(ParentResponse());
        await session.ActivateChildAsync(ChildResponse());

        await session.ApplyRefreshedAsync(new AuthResponse
        {
            AccessToken = "child-token-2",
            RefreshToken = "child-refresh-2",
            ProfileKind = ProfileKind.Child,
            ChildId = ChildId
        });

        Assert.Equal("child-token-2", session.CurrentAccessToken);
        Assert.Equal("parent-token", session.ParentAccessToken);
    }

    [Fact]
    public async Task SignInParent_RefreshTokeniAnbardaSaxlayir()
    {
        var store = new InMemoryTokenStore();
        var session = NewSession(store);

        await session.SignInParentAsync(ParentResponse());

        Assert.Equal("parent-refresh", await store.GetAsync(TokenStoreKeys.ParentRefreshToken));
    }

    private static readonly Guid ChildId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Server refresh tokeni ROTASİYA edir: köhnəsi ləğv olunur. Yenisi cihazda
    /// saxlanmasa, app növbəti açılışda ölü tokenlə gəlir və giriş yenidən
    /// soruşulur — məhz bu səhv üçün test var.
    /// </summary>
    [Fact]
    public async Task ApplyRefreshed_YeniValideynTokeniniCihazdaSaxlayir()
    {
        var store = new InMemoryTokenStore();
        var session = NewSession(store);

        await session.SignInParentAsync(ParentResponse(), "ana@example.com");

        await session.ApplyRefreshedAsync(new AuthResponse
        {
            AccessToken = "parent-token-2",
            RefreshToken = "parent-refresh-2",
            ProfileKind = ProfileKind.Parent
        });

        Assert.Equal("parent-refresh-2", await store.GetAsync(TokenStoreKeys.ParentRefreshToken));
    }

    /// <summary>E-poçt cihazda qalır ki, giriş forması onu özü doldursun.</summary>
    [Fact]
    public async Task SignInParent_EPoctuSaxlayir_CixisdaDaQalir()
    {
        var store = new InMemoryTokenStore();
        var session = NewSession(store);

        await session.SignInParentAsync(ParentResponse(), "  ana@example.com ");
        Assert.Equal("ana@example.com", await session.GetStoredEmailAsync());

        await session.SignOutAsync();

        Assert.Null(await store.GetAsync(TokenStoreKeys.ParentRefreshToken));
        Assert.Equal("ana@example.com", await session.GetStoredEmailAsync());
    }

    private static AppSession NewSession() => NewSession(new InMemoryTokenStore());

    /// <summary>Dil servisi sessiya ilə eyni anbarı paylaşır — real appdəki kimi.</summary>
    private static AppSession NewSession(ITokenStore store) => new(store, new Loc(store));

    private static AuthResponse ParentResponse() => new()
    {
        AccessToken = "parent-token",
        RefreshToken = "parent-refresh",
        ProfileKind = ProfileKind.Parent,
        ParentDisplayName = "Aysel",
        Children = [new ChildSummaryDto { Id = ChildId, DisplayName = "Ava" }]
    };

    private static AuthResponse ChildResponse() => new()
    {
        AccessToken = "child-token",
        RefreshToken = "child-refresh",
        ProfileKind = ProfileKind.Child,
        ChildId = ChildId,
        Children = [new ChildSummaryDto { Id = ChildId, DisplayName = "Ava" }]
    };
}
