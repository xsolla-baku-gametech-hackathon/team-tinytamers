using System.Net;
using System.Net.Http.Json;
using PetPal.Shared.Dtos.Auth;
using PetPal.Shared.Dtos.Parent;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

public class AuthTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public AuthTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_ValideynSessiyasiQaytarir()
    {
        var http = _factory.CreateClient();

        var response = await http.PostAsJsonAsync("/api/auth/register", new RegisterParentRequest
        {
            Email = $"register-{Guid.NewGuid():N}@petpal.test",
            Password = "Passw0rd!",
            DisplayName = "Aysel"
        });

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(auth);
        Assert.Equal(ProfileKind.Parent, auth.ProfileKind);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.Null(auth.ChildId);
    }

    [Fact]
    public async Task Register_TekrarEPoctda409Qaytarir()
    {
        var http = _factory.CreateClient();
        var email = $"dupe-{Guid.NewGuid():N}@petpal.test";
        var request = new RegisterParentRequest { Email = email, Password = "Passw0rd!", DisplayName = "Aysel" };

        (await http.PostAsJsonAsync("/api/auth/register", request)).EnsureSuccessStatusCode();
        var second = await http.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_YanlisSifreQebulEtmir()
    {
        var http = _factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@petpal.test";

        (await http.PostAsJsonAsync("/api/auth/register", new RegisterParentRequest
        {
            Email = email,
            Password = "Passw0rd!",
            DisplayName = "Aysel"
        })).EnsureSuccessStatusCode();

        var response = await http.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "SehvSifre1!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChildLogin_DuzgunPinIleUsaqSessiyasiVerir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, $"child-{Guid.NewGuid():N}@petpal.test");

        Assert.Equal(ProfileKind.Child, client.Child.ProfileKind);
        Assert.NotNull(client.Child.ChildId);
        Assert.Single(client.Child.Children);
    }

    [Fact]
    public async Task ChildLogin_YanlisPinQebulEtmir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, $"pin-{Guid.NewGuid():N}@petpal.test");
        client.SwitchToParent();

        var response = await client.Http.PostAsJsonAsync("/api/auth/children/login", new ChildLoginRequest
        {
            ChildId = client.ChildId,
            Pin = "9999"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UsaqTokeni_ValideynEndpointineDaxilOlaBilmir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, $"guard1-{Guid.NewGuid():N}@petpal.test");

        var response = await client.Http.GetAsync($"/api/parent/children/{client.ChildId}/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ValideynTokeni_UsaqEndpointineDaxilOlaBilmir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, $"guard2-{Guid.NewGuid():N}@petpal.test");
        client.SwitchToParent();

        var response = await client.Http.GetAsync("/api/home");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TokensuzSorgu401Qaytarir()
    {
        var http = _factory.CreateClient();

        var response = await http.GetAsync("/api/home");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_KohneTokeniLegvEdir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, $"refresh-{Guid.NewGuid():N}@petpal.test");
        var http = _factory.CreateClient();

        var first = await http.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest { RefreshToken = client.Child.RefreshToken });
        first.EnsureSuccessStatusCode();

        var refreshed = await first.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(ProfileKind.Child, refreshed!.ProfileKind);

        // Rotasiya: eyni token ikinci dəfə işləməməlidir.
        var second = await http.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest { RefreshToken = client.Child.RefreshToken });

        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
    }

    [Fact]
    public async Task Valideyn_BasqaValideyninUsaginaBaxaBilmir()
    {
        var first = await ApiTestClient.CreateAsync(_factory, $"iso-a-{Guid.NewGuid():N}@petpal.test", "Ava");
        var second = await ApiTestClient.CreateAsync(_factory, $"iso-b-{Guid.NewGuid():N}@petpal.test", "Nils");

        second.SwitchToParent();
        var response = await second.Http.GetAsync($"/api/parent/children/{first.ChildId}/dashboard");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Valideyn_BasqaValideyninUsaginaAyarDeyiseBilmir()
    {
        var first = await ApiTestClient.CreateAsync(_factory, $"iso-c-{Guid.NewGuid():N}@petpal.test", "Ava");
        var second = await ApiTestClient.CreateAsync(_factory, $"iso-d-{Guid.NewGuid():N}@petpal.test", "Nils");

        second.SwitchToParent();
        var response = await second.Http.PutAsJsonAsync(
            $"/api/parent/children/{first.ChildId}/screen-time",
            new ScreenTimeSettingsDto { DailyGoalTarget = 50, DailyMinutesLimit = 240 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
