using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Shared.Dtos;
using PetPal.Shared.Dtos.Auth;
using PetPal.Shared.Dtos.Home;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Dtos.Missions;
using PetPal.Shared.Dtos.Parent;
using PetPal.Shared.Dtos.Progress;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

public class LocalizedHelperTests
{
    [Theory]
    [InlineData("az", "az")]
    [InlineData("AZ", "az")]
    [InlineData("en", "en")]
    [InlineData("de", "az")]
    [InlineData("", "az")]
    [InlineData(null, "az")]
    public void Normalize_DestelenmeyenDiliDefaultaCevirir(string? input, string expected)
    {
        Assert.Equal(expected, Localized.Normalize(input));
    }

    [Fact]
    public void Pick_AzerbaycancaVariantYoxdursaIngilisceyeDuşur()
    {
        Assert.Equal("English text", Localized.Pick("az", "English text", null));
        Assert.Equal("English text", Localized.Pick("az", "English text", "   "));
        Assert.Equal("Azərbaycanca", Localized.Pick("az", "English text", "Azərbaycanca"));
        Assert.Equal("English text", Localized.Pick("en", "English text", "Azərbaycanca"));
    }
}

public class ContentLocalizationTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ContentLocalizationTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task SualBanki_HerIkiDildeSeedOlunub()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var language in Localized.Supported)
        {
            foreach (var skill in Enum.GetValues<SkillArea>())
            {
                var count = await db.Questions.CountAsync(q => q.LanguageCode == language && q.Skill == skill);
                Assert.True(count > 0, $"{language} / {skill} üçün sual yoxdur.");
            }
        }
    }

    [Fact]
    public async Task AzerbaycanDilliUsaq_AzerbaycancaSualAlir()
    {
        var client = await NewChildAsync("az");

        var session = await StartSessionAsync(client, SkillArea.Math);

        await AssertLanguageAsync(session, "az");
    }

    [Fact]
    public async Task IngilisDilliUsaq_IngilisceSualAlir()
    {
        var client = await NewChildAsync("en");

        var session = await StartSessionAsync(client, SkillArea.Math);

        await AssertLanguageAsync(session, "en");
    }

    [Fact]
    public async Task PetReplikasi_UsaginDilindeGelir()
    {
        var az = await NewChildAsync("az");
        var en = await NewChildAsync("en");

        var azHome = await GetHomeAsync(az);
        var enHome = await GetHomeAsync(en);

        Assert.NotEqual(azHome.PetMessage, enHome.PetMessage);
    }

    [Fact]
    public async Task MissiyaBasliqlari_UsaginDilindeGelir()
    {
        var az = await NewChildAsync("az");
        var en = await NewChildAsync("en");

        var azWorld = await GetWorldAsync(az);
        var enWorld = await GetWorldAsync(en);

        var azMission = azWorld.Zones.SelectMany(z => z.Missions).First(m => m.Code == "meadow-first-3");
        var enMission = enWorld.Zones.SelectMany(z => z.Missions).First(m => m.Code == "meadow-first-3");

        Assert.Equal("Çəməni oyat", azMission.Title);
        Assert.Equal("Wake the Meadow", enMission.Title);
    }

    [Fact]
    public async Task ZonaAdlari_UsaginDilindeGelir()
    {
        var az = await NewChildAsync("az");
        var world = await GetWorldAsync(az);

        Assert.Equal("Günəşli çəmən", world.Zones.First(z => z.Code == "meadow").Name);
    }

    [Fact]
    public async Task NisanAdlari_UsaginDilindeGelir()
    {
        var az = await NewChildAsync("az");

        var response = await az.Http.GetAsync("/api/progress");
        response.EnsureSuccessStatusCode();
        var progress = (await response.Content.ReadFromJsonAsync<ProgressSummaryDto>())!;

        Assert.Equal("İlk addımlar", progress.Badges.First(b => b.Code == "first-steps").Title);
    }

    [Fact]
    public async Task DestelenmeyenDil_DefaultaDuşur()
    {
        var http = _factory.CreateClient();
        var email = $"lang-{Guid.NewGuid():N}@petpal.test";

        var register = await http.PostAsJsonAsync("/api/auth/register", new RegisterParentRequest
        {
            Email = email,
            Password = "Passw0rd!",
            DisplayName = "Test"
        });
        register.EnsureSuccessStatusCode();
        var parent = (await register.Content.ReadFromJsonAsync<AuthResponse>())!;
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", parent.AccessToken);

        // "de" dəstəklənmir → validasiya rədd etməlidir.
        var create = await http.PostAsJsonAsync("/api/auth/children", new CreateChildRequest
        {
            DisplayName = "Ava",
            Age = 8,
            Pin = "1234",
            PetName = "Max",
            LanguageCode = "de"
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, create.StatusCode);
    }

    /// <summary>
    /// İnterfeysin dili sessiya cavabı ilə gəlir: app-in ekranları məhz bu
    /// dəyərə baxır. Olmasa, uşaq ingiliscə sual alır, amma düymələr
    /// Azərbaycanca qalırdı.
    /// </summary>
    [Fact]
    public async Task SessiyaCavabi_UsaginDiliniDasiyir()
    {
        var en = await NewChildAsync("en");
        var az = await NewChildAsync("az");

        Assert.Equal("en", en.Child.LanguageCode);
        Assert.Equal("az", az.Child.LanguageCode);

        // Profil siyahısı da dili daşıyır: valideyn ekranı hansı uşağın hansı
        // dildə oynadığını bilməlidir.
        Assert.Equal("en", en.Child.Children.Single().LanguageCode);
    }

    /// <summary>
    /// Uşaq profili tapılmayanda serverin dil mənbəyi yalnız Accept-Language
    /// başlığıdır — klient onu hər sorğuda göndərir.
    /// </summary>
    [Theory]
    [InlineData("en", "The email or password is wrong.")]
    [InlineData("az", "E-poçt və ya şifrə yanlışdır.")]
    public async Task XetaMesaji_SorgununDilindeGelir(string language, string expected)
    {
        var http = _factory.CreateClient();
        http.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));

        var response = await http.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "yoxdur@petpal.test",
            Password = "Passw0rd!"
        });

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.Equal(expected, error?.Message);
    }

    /// <summary>
    /// Forma qaydalarının mətni də dilə tabedir: atributlar sabit mətn qəbul
    /// etdiyi üçün mesajlar <c>ValidationMessages</c> sinfindən refleksiya ilə
    /// oxunur və dili <c>CurrentUICulture</c> həll edir.
    /// </summary>
    [Fact]
    public async Task FormaQaydasi_SorgununDilindeGelir()
    {
        var http = _factory.CreateClient();
        http.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));

        var response = await http.PostAsJsonAsync("/api/auth/register", new RegisterParentRequest
        {
            Email = "duzgun-deyil",
            Password = "123",
            DisplayName = "A"
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("The password must be at least 8 characters.", body);
        Assert.DoesNotContain("Şifrə ən azı 8 simvol olmalıdır.", body);
    }

    /// <summary>
    /// Dil profil yaradılanda seçilir, amma orada bitmir: valideyn onu sonra da
    /// dəyişə bilməlidir. Dəyişiklik həm interfeysə, həm də sual bankına keçir —
    /// uşağın yaşı, ulduzları və proqresi isə yerində qalır.
    /// </summary>
    [Fact]
    public async Task ValideynDiliDeyisir_ProqresItmir()
    {
        var client = await NewChildAsync("az");

        var before = await GetHomeAsync(client);

        // Dil valideyn bölməsindən dəyişir — uşaq tokeni ora düşmür.
        client.SwitchToParent();

        var change = await client.Http.PutAsJsonAsync(
            $"/api/parent/children/{client.ChildId}/language",
            new ChildLanguageRequest { LanguageCode = "en" });

        change.EnsureSuccessStatusCode();
        var summary = (await change.Content.ReadFromJsonAsync<ChildSummaryDto>())!;
        Assert.Equal("en", summary.LanguageCode);

        client.SwitchToChild();

        // Sual bankı yeni dilə keçir…
        var session = await StartSessionAsync(client, SkillArea.Math);
        await AssertLanguageAsync(session, "en");

        // …proqres isə qalır.
        var after = await GetHomeAsync(client);
        Assert.Equal(before.Pet.Level, after.Pet.Level);
        Assert.Equal(before.Wallet.Stars, after.Wallet.Stars);
    }

    /// <summary>Dəstəklənməyən dil rədd olunur — baza yalnız az/en bilir.</summary>
    [Fact]
    public async Task ValideynDiliDeyisir_DestelenmeyenDilRedEdilir()
    {
        var client = await NewChildAsync("az");
        client.SwitchToParent();

        var change = await client.Http.PutAsJsonAsync(
            $"/api/parent/children/{client.ChildId}/language",
            new ChildLanguageRequest { LanguageCode = "de" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, change.StatusCode);
    }

    // ---------- köməkçilər ----------

    private Task<ApiTestClient> NewChildAsync(string language) =>
        ApiTestClient.CreateAsync(_factory, $"loc-{Guid.NewGuid():N}@petpal.test", language: language);

    private static async Task<LearningSessionDto> StartSessionAsync(ApiTestClient client, SkillArea skill)
    {
        var response = await client.Http.PostAsJsonAsync("/api/learn/sessions",
            new StartSessionRequest { Skill = skill, QuestionCount = 3 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LearningSessionDto>())!;
    }

    /// <summary>
    /// Dil yoxlaması mətnə görə deyil, <c>LanguageCode</c>-a görə edilir.
    /// Əvvəl «What is» / «neçədir» axtarılırdı — banka məsələ mətnləri və
    /// müqayisə sualları düşən kimi test sındı, halbuki dil düzgün idi.
    /// </summary>
    private async Task AssertLanguageAsync(LearningSessionDto session, string language)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ids = session.Questions.Select(q => q.Id).ToList();
        var languages = await db.Questions.AsNoTracking()
            .Where(q => ids.Contains(q.Id))
            .Select(q => q.LanguageCode)
            .ToListAsync();

        Assert.NotEmpty(languages);
        Assert.All(languages, code => Assert.Equal(language, code));
    }

    private static async Task<HomeStateDto> GetHomeAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync("/api/home");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HomeStateDto>())!;
    }

    private static async Task<WorldStateDto> GetWorldAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync("/api/world");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorldStateDto>())!;
    }
}
