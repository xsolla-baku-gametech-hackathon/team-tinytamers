using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Shared.Dtos.Auth;

namespace PetPal.Tests;

/// <summary>
/// Testlərin təkrarlanan qurulma addımları: valideyn qeydiyyatı → uşaq profili →
/// uşaq sessiyası. Hər test faylı bu axını yenidən yazmasın deyə burada saxlanılır.
/// </summary>
public class ApiTestClient
{
    private ApiTestClient(HttpClient http, AuthResponse parent, AuthResponse child)
    {
        Http = http;
        Parent = parent;
        Child = child;
    }

    public HttpClient Http { get; }
    public AuthResponse Parent { get; }
    public AuthResponse Child { get; }
    public Guid ChildId => Child.ChildId!.Value;

    public static async Task<ApiTestClient> CreateAsync(
        TestWebAppFactory factory,
        string email = "parent@petpal.test",
        string childName = "Ava",
        string pin = "1234",
        string language = "az")
    {
        var http = factory.CreateClient();

        var register = await http.PostAsJsonAsync("/api/auth/register", new RegisterParentRequest
        {
            Email = email,
            Password = "Passw0rd!",
            DisplayName = "Test Parent"
        });
        register.EnsureSuccessStatusCode();

        var parent = (await register.Content.ReadFromJsonAsync<AuthResponse>())!;
        UseToken(http, parent.AccessToken);

        var createChild = await http.PostAsJsonAsync("/api/auth/children", new CreateChildRequest
        {
            DisplayName = childName,
            Age = 8,
            AvatarKey = "avatar-fox",
            Pin = pin,
            PetName = "Max",
            PetSpecies = "fox",
            LanguageCode = language
        });
        createChild.EnsureSuccessStatusCode();

        var summary = (await createChild.Content.ReadFromJsonAsync<ChildSummaryDto>())!;

        var childLogin = await http.PostAsJsonAsync("/api/auth/children/login", new ChildLoginRequest
        {
            ChildId = summary.Id,
            Pin = pin
        });
        childLogin.EnsureSuccessStatusCode();

        var child = (await childLogin.Content.ReadFromJsonAsync<AuthResponse>())!;
        UseToken(http, child.AccessToken);

        return new ApiTestClient(http, parent, child);
    }

    /// <summary>
    /// Pet-i yumurtadan çıxarır (birbaşa bazada).
    ///
    /// <para>Yeni profildə pet YUMURTADIR, qulluq və söhbət isə yalnız pet
    /// çıxandan sonra açılır. Ulduz toplayıb `/api/pet/hatch` çağırmaq hər
    /// testdə on sətir olardı, ona görə quraşdırma addımı burada saxlanılır —
    /// testin mövzusu bu deyil.</para>
    /// </summary>
    public async Task HatchAsync(TestWebAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pet = await db.Pets.FirstAsync(p => p.ChildProfileId == ChildId);
        pet.HatchedAt = factory.Clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync();
    }

    /// <summary>Valideyn endpoint-lərini yoxlamaq üçün token-i geri çevirir.</summary>
    public void SwitchToParent() => UseToken(Http, Parent.AccessToken);

    public void SwitchToChild() => UseToken(Http, Child.AccessToken);

    private static void UseToken(HttpClient http, string token) =>
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
