using System.Net;
using System.Text;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Shared.Dtos.Pets;
using PetPal.Shared.Dtos.Rewards;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

public class PetCareTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetCareTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task GetPet_BaslangicVeziyyetiniQaytarir()
    {
        var client = await NewChildAsync(hatched: false);

        var pet = await GetPetAsync(client);

        Assert.Equal("Max", pet.Name);
        Assert.Equal(1, pet.Level);
        Assert.Equal(PetStage.Egg, pet.Stage);
        Assert.False(pet.IsHatched);
        Assert.InRange(pet.Happiness, 0, 100);
    }

    // ---------- Yumurta ----------

    [Fact]
    public async Task Care_YumurtaRejimindeImtinaEdilir()
    {
        var client = await NewChildAsync(hatched: false);

        var response = await client.Http.PostAsJsonAsync("/api/pet/care", new CarePetRequest { Action = CareAction.Play });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Hatch_UlduzCatismayandaImtinaEdir()
    {
        var client = await NewChildAsync(hatched: false);

        var response = await client.Http.PostAsync("/api/pet/hatch", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Hatch_UlduzKifayetdirseYumurtaniAcirVeBalansdanCixir()
    {
        var client = await NewChildAsync(hatched: false);
        await GrantStarsAsync(client, 60);

        var before = await GetWalletAsync(client);
        var response = await client.Http.PostAsync("/api/pet/hatch", null);
        response.EnsureSuccessStatusCode();

        var result = (await response.Content.ReadFromJsonAsync<HatchPetResultDto>())!;
        var after = await GetWalletAsync(client);

        Assert.True(result.Pet.IsHatched);
        Assert.NotEqual(PetStage.Egg, result.Pet.Stage);
        Assert.Equal(50, result.StarsSpent);
        Assert.Equal(before.Stars - 50, after.Stars);
    }

    [Fact]
    public async Task Hatch_IkinciDefeIslemir()
    {
        var client = await NewChildAsync(hatched: false);
        await GrantStarsAsync(client, 200);

        (await client.Http.PostAsync("/api/pet/hatch", null)).EnsureSuccessStatusCode();
        var second = await client.Http.PostAsync("/api/pet/hatch", null);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Hatch_AcildiqdanSonraQulluqIsleyir()
    {
        var client = await NewChildAsync(hatched: false);
        await GrantStarsAsync(client, 60);
        (await client.Http.PostAsync("/api/pet/hatch", null)).EnsureSuccessStatusCode();

        var result = await CareAsync(client, CareAction.Play);

        Assert.True(result.Pet.Happiness > 0);
    }

    [Fact]
    public async Task Play_SevinciArtirir_EnerjiniAzaldir()
    {
        var client = await NewChildAsync();
        var before = await GetPetAsync(client);

        var result = await CareAsync(client, CareAction.Play);

        Assert.True(result.Pet.Happiness > before.Happiness);
        Assert.True(result.Pet.Energy < before.Energy);
        Assert.Equal(0, result.StarsSpent);
    }

    [Fact]
    public async Task Feed_UlduzCatismayandaImtinaEdir()
    {
        var client = await NewChildAsync();

        // Yeni profilin balansı sıfırdır, yemək isə 5 ulduza başa gəlir.
        await SetPetStatAsync(client, fullness: 30);
        var response = await client.Http.PostAsJsonAsync("/api/pet/care", new CarePetRequest { Action = CareAction.Feed });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Feed_UlduzVarsaToxluguArtirirVeBalansdanCixir()
    {
        var client = await NewChildAsync();
        await SetPetStatAsync(client, fullness: 30);
        await GrantStarsAsync(client, 50);

        var before = await GetWalletAsync(client);
        var result = await CareAsync(client, CareAction.Feed);
        var after = await GetWalletAsync(client);

        Assert.Equal(5, result.StarsSpent);
        Assert.Equal(before.Stars - 5, after.Stars);
        Assert.True(result.Pet.Fullness > 30);
    }

    [Fact]
    public async Task Foods_KataloqQiymetVeTesirIleQayidir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.GetAsync("/api/pet/foods");
        response.EnsureSuccessStatusCode();

        var foods = (await response.Content.ReadFromJsonAsync<List<PetFoodDto>>())!;

        Assert.True(foods.Count > 1, "Yem qabında bir neçə seçim olmalıdır.");
        Assert.Equal(foods.Select(f => f.Code).Distinct().Count(), foods.Count);
        Assert.All(foods, food =>
        {
            Assert.True(food.StarCost > 0);
            Assert.False(string.IsNullOrWhiteSpace(food.Name));
            Assert.False(string.IsNullOrWhiteSpace(food.Icon));
            Assert.False(string.IsNullOrWhiteSpace(food.Effect));
        });
    }

    [Fact]
    public async Task Feed_UcuzYemekAzUlduzaBasaGelir()
    {
        var client = await NewChildAsync();
        await SetPetStatAsync(client, fullness: 30);
        await GrantStarsAsync(client, 50);

        var before = await GetWalletAsync(client);
        var result = await CareAsync(client, CareAction.Feed, "carrot");
        var after = await GetWalletAsync(client);

        Assert.Equal(2, result.StarsSpent);
        Assert.Equal(before.Stars - 2, after.Stars);
        Assert.Equal(42, result.Pet.Fullness);
    }

    /// <summary>
    /// Şirniyyat çox sevindirir, amma pet bulaşır — seçimin görünən nəticəsi var,
    /// yəni sonra çimizdirmə lazım gəlir.
    /// </summary>
    [Fact]
    public async Task Feed_SirniyyatSevinciArtirirVeTemizliyiAzaldir()
    {
        var client = await NewChildAsync();
        await SetPetStatAsync(client, happiness: 50, fullness: 30, cleanliness: 80);
        await GrantStarsAsync(client, 50);

        var result = await CareAsync(client, CareAction.Feed, "cake");

        Assert.Equal(9, result.StarsSpent);
        Assert.Equal(70, result.Pet.Happiness);
        Assert.Equal(72, result.Pet.Cleanliness);
    }

    [Fact]
    public async Task Feed_NamelumYemekKoduImtinaEdilir()
    {
        var client = await NewChildAsync();
        await SetPetStatAsync(client, fullness: 30);
        await GrantStarsAsync(client, 50);

        var response = await client.Http.PostAsJsonAsync(
            "/api/pet/care", new CarePetRequest { Action = CareAction.Feed, Food = "pizza" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Naməlum kod heç nəyə toxunmamalıdır — nə ulduz gedir, nə də toxluq artır.
        var wallet = await GetWalletAsync(client);
        Assert.Equal(50, wallet.Stars);
    }

    [Fact]
    public async Task Feed_UlduzYalnizSecilenYemeyeCatmayandaImtinaEdilir()
    {
        var client = await NewChildAsync();
        await SetPetStatAsync(client, fullness: 30);
        await GrantStarsAsync(client, 3);

        // 5 ulduzluq ət alınmır, 2 ulduzluq kök isə alınır.
        var expensive = await client.Http.PostAsJsonAsync(
            "/api/pet/care", new CarePetRequest { Action = CareAction.Feed, Food = "meat" });
        var cheap = await CareAsync(client, CareAction.Feed, "carrot");

        Assert.Equal(HttpStatusCode.BadRequest, expensive.StatusCode);
        Assert.Equal(2, cheap.StarsSpent);
    }

    [Fact]
    public async Task Clean_ArtiqTemizPetiTekrarTemizlemir()
    {
        var client = await NewChildAsync();
        await SetPetStatAsync(client, cleanliness: 100);

        var response = await client.Http.PostAsJsonAsync("/api/pet/care", new CarePetRequest { Action = CareAction.Clean });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sleep_EnerjiniBerpaEdir()
    {
        var client = await NewChildAsync();
        await SetPetStatAsync(client, energy: 20);

        var result = await CareAsync(client, CareAction.Sleep);

        Assert.True(result.Pet.Energy > 20);
        Assert.NotEqual(PetMood.Sleepy, result.Pet.Mood);
    }

    [Fact]
    public async Task Play_EnerjiCoxAzOlandaImtinaEdir()
    {
        var client = await NewChildAsync();
        await SetPetStatAsync(client, energy: 5);

        var response = await client.Http.PostAsJsonAsync("/api/pet/care", new CarePetRequest { Action = CareAction.Play });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Görünüş ----------

    [Fact]
    public async Task Accessories_TaxilibCixarilaBilir()
    {
        var client = await NewChildAsync();
        await SetPetStatAsync(client, happiness: 95);
        await UnlockAccessoriesAsync(client, level: 3);

        var pet = await GetPetAsync(client);
        Assert.Contains("bow-red", pet.EquippedAccessories);

        // Çıxarmaq
        var bare = await EquipAsync(client, []);
        Assert.Empty(bare.EquippedAccessories);
        Assert.Contains("bow-red", bare.UnlockedAccessories);
        Assert.All(bare.Accessories, a => Assert.False(a.IsEquipped));

        // Yenidən taxmaq
        var dressed = await EquipAsync(client, ["bow-red"]);
        Assert.Equal(["bow-red"], dressed.EquippedAccessories);
        Assert.True(dressed.Accessories.Single(a => a.Code == "bow-red").IsEquipped);
    }

    [Fact]
    public async Task Accessories_KilidliEsyaTaxilmir()
    {
        var client = await NewChildAsync();
        await UnlockAccessoriesAsync(client, level: 2);

        var response = await client.Http.PutAsJsonAsync(
            "/api/pet/accessories", new EquipAccessoriesRequest { Codes = ["crown-gold"] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var pet = await GetPetAsync(client);
        Assert.DoesNotContain("crown-gold", pet.EquippedAccessories);
    }

    [Fact]
    public async Task Accessories_YumurtaRejimindeImtinaEdilir()
    {
        var client = await NewChildAsync(hatched: false);

        var response = await client.Http.PutAsJsonAsync(
            "/api/pet/accessories", new EquipAccessoriesRequest { Codes = [] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RenamePet_AdiDeyisir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.PutAsJsonAsync("/api/pet/name", new RenamePetRequest { Name = "Luna" });
        response.EnsureSuccessStatusCode();

        var pet = await GetPetAsync(client);
        Assert.Equal("Luna", pet.Name);
    }

    [Fact]
    public async Task Care_MissiyaIrelileyisiniArtirir()
    {
        var client = await NewChildAsync();

        await CareAsync(client, CareAction.Play);
        await CareAsync(client, CareAction.Sleep);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var progress = await db.ChildMissions
            .Where(cm => cm.ChildProfileId == client.ChildId && cm.Mission.Type == MissionType.CareForPet)
            .Select(cm => cm.Progress)
            .ToListAsync();

        Assert.Contains(progress, p => p >= 2);
    }

    /// <summary>
    /// Pozuq JSON serverin nasazlığı deyil, sorğunun nasazlığıdır — 500 həm səhv
    /// siqnal verir, həm də monitorinqdə yalançı həyəcan yaradır.
    /// </summary>
    [Fact]
    public async Task PozuqJson_400QaytarirVe500Yox()
    {
        var client = await NewChildAsync();

        var content = new StringContent("""{"action": }""", Encoding.UTF8, "application/json");
        var response = await client.Http.PostAsync("/api/pet/care", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- köməkçilər ----------

    /// <summary>
    /// Qulluq testlərinin əksəriyyəti açılmış pet tələb edir — yumurta rejimində
    /// bütün care əməliyyatları imtina edilir. Yumurta davranışı ayrıca yoxlanılır.
    /// </summary>
    private async Task<ApiTestClient> NewChildAsync(bool hatched = true)
    {
        var client = await ApiTestClient.CreateAsync(_factory, $"care-{Guid.NewGuid():N}@petpal.test");

        if (hatched)
            await HatchInDbAsync(client);

        return client;
    }

    private async Task HatchInDbAsync(ApiTestClient client)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pet = await db.Pets.FirstAsync(p => p.ChildProfileId == client.ChildId);
        pet.HatchedAt = _factory.Clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync();
    }

    private static async Task<PetDto> GetPetAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync("/api/pet");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PetDto>())!;
    }

    private static async Task<WalletDto> GetWalletAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync("/api/rewards/wallet");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WalletDto>())!;
    }

    private static async Task<CarePetResultDto> CareAsync(ApiTestClient client, CareAction action, string? food = null)
    {
        var response = await client.Http.PostAsJsonAsync(
            "/api/pet/care", new CarePetRequest { Action = action, Food = food });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CarePetResultDto>())!;
    }

    /// <summary>Statları birbaşa bazada qurmaq testi saatla oynamaqdan azad edir.</summary>
    private async Task SetPetStatAsync(
        ApiTestClient client, int? happiness = null, int? energy = null, int? fullness = null, int? cleanliness = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pet = await db.Pets.FirstAsync(p => p.ChildProfileId == client.ChildId);
        pet.Happiness = happiness ?? pet.Happiness;
        pet.Energy = energy ?? pet.Energy;
        pet.Fullness = fullness ?? pet.Fullness;
        pet.Cleanliness = cleanliness ?? pet.Cleanliness;
        pet.LastDecayAt = _factory.Clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync();
    }

    private static async Task<PetDto> EquipAsync(ApiTestClient client, List<string> codes)
    {
        var response = await client.Http.PutAsJsonAsync(
            "/api/pet/accessories", new EquipAccessoriesRequest { Codes = codes });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PetDto>())!;
    }

    /// <summary>
    /// Səviyyəni bazada qaldırıb açılış qaydasını işə salır — əşya qazanmaq üçün
    /// onlarla sual həll etmək testin işi deyil.
    /// </summary>
    private async Task UnlockAccessoriesAsync(ApiTestClient client, int level)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pet = await db.Pets.FirstAsync(p => p.ChildProfileId == client.ChildId);
        pet.Level = level;
        PetPal.Api.Pets.PetAccessories.UnlockEarned(pet);

        await db.SaveChangesAsync();
    }

    private async Task GrantStarsAsync(ApiTestClient client, int stars)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var child = await db.ChildProfiles.FirstAsync(c => c.Id == client.ChildId);
        child.Stars += stars;

        await db.SaveChangesAsync();
    }
}
