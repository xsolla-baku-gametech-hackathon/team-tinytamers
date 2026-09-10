using System.Net;
using System.Net.Http.Json;
using PetPal.Shared.Dtos.Parent;

namespace PetPal.Tests;

/// <summary>
/// Valideyn bölməsinin qapısı.
///
/// <para>Əvvəl qapı vurma sualı idi və YALNIZ KLİENTDƏ yoxlanılırdı — yəni
/// server üçün qapı ümumiyyətlə yox idi. Bu testlər qapının serverdə
/// olduğunu qoruyur: PIN qurulmayınca açılış verilmir, səhv PIN keçmir, və
/// PIN-i dəyişmək üçün kimlik təsdiqi tələb olunur.</para>
/// </summary>
public class ParentGateTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ParentGateTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task YeniHesabda_PinQurulmayib()
    {
        var client = await NewParentAsync();

        var status = await client.Http.GetFromJsonAsync<ParentGateStatusDto>("/api/parent/gate");

        Assert.NotNull(status);
        Assert.False(status!.HasPin);
    }

    /// <summary>
    /// PIN yoxdursa qapı BAĞLIDIR. Köhnə hesablar da təhlükəsiz vəziyyətdə
    /// qalmalıdır — «PIN yoxdursa buraxaq» qapını mənasız edərdi.
    /// </summary>
    [Fact]
    public async Task PinQurulmayibsa_AcilisVerilmir()
    {
        var client = await NewParentAsync();

        var response = await client.Http.PostAsJsonAsync("/api/parent/gate/unlock",
            new ParentGateUnlockRequest { Pin = "1234" });

        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ParolIle_PinQurulurVeAcilisVerilir()
    {
        var client = await NewParentAsync();

        var set = await client.Http.PutAsJsonAsync("/api/parent/gate/pin",
            new SetParentPinRequest { Password = "Passw0rd!", NewPin = "8317" });
        set.EnsureSuccessStatusCode();

        var unlock = await client.Http.PostAsJsonAsync("/api/parent/gate/unlock",
            new ParentGateUnlockRequest { Pin = "8317" });
        unlock.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SehvPin_Kecmir()
    {
        var client = await NewParentAsync();

        (await client.Http.PutAsJsonAsync("/api/parent/gate/pin",
            new SetParentPinRequest { Password = "Passw0rd!", NewPin = "8317" })).EnsureSuccessStatusCode();

        var response = await client.Http.PostAsJsonAsync("/api/parent/gate/unlock",
            new ParentGateUnlockRequest { Pin = "0000" });

        Assert.False(response.IsSuccessStatusCode);
    }

    /// <summary>
    /// Səhv parol PIN-i dəyişə bilməz. Bu olmasa, açıq qalmış valideyn
    /// sessiyasında PIN-i sadəcə üzərinə yazmaq olardı və qapı yox olardı.
    /// </summary>
    [Fact]
    public async Task SehvParolVeYaPin_DeyisikliyeIcazeVermir()
    {
        var client = await NewParentAsync();

        (await client.Http.PutAsJsonAsync("/api/parent/gate/pin",
            new SetParentPinRequest { Password = "Passw0rd!", NewPin = "8317" })).EnsureSuccessStatusCode();

        var wrongPin = await client.Http.PutAsJsonAsync("/api/parent/gate/pin",
            new SetParentPinRequest { CurrentPin = "0000", NewPin = "4242" });
        Assert.False(wrongPin.IsSuccessStatusCode);

        var noProof = await client.Http.PutAsJsonAsync("/api/parent/gate/pin",
            new SetParentPinRequest { NewPin = "4242" });
        Assert.False(noProof.IsSuccessStatusCode);

        // Köhnə PIN hələ də işləyir — dəyişiklik baş tutmayıb.
        (await client.Http.PostAsJsonAsync("/api/parent/gate/unlock",
            new ParentGateUnlockRequest { Pin = "8317" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CariPinIle_PinDeyisdirilir()
    {
        var client = await NewParentAsync();

        (await client.Http.PutAsJsonAsync("/api/parent/gate/pin",
            new SetParentPinRequest { Password = "Passw0rd!", NewPin = "8317" })).EnsureSuccessStatusCode();

        (await client.Http.PutAsJsonAsync("/api/parent/gate/pin",
            new SetParentPinRequest { CurrentPin = "8317", NewPin = "4242" })).EnsureSuccessStatusCode();

        (await client.Http.PostAsJsonAsync("/api/parent/gate/unlock",
            new ParentGateUnlockRequest { Pin = "4242" })).EnsureSuccessStatusCode();

        var old = await client.Http.PostAsJsonAsync("/api/parent/gate/unlock",
            new ParentGateUnlockRequest { Pin = "8317" });
        Assert.False(old.IsSuccessStatusCode);
    }

    /// <summary>Qapı valideyn sessiyasına aiddir — uşaq tokeni ona çatmamalıdır.</summary>
    [Fact]
    public async Task UsaqSessiyasi_QapiyaCatmir()
    {
        var client = await NewParentAsync();

        (await client.Http.PutAsJsonAsync("/api/parent/gate/pin",
            new SetParentPinRequest { Password = "Passw0rd!", NewPin = "8317" })).EnsureSuccessStatusCode();

        client.SwitchToChild();

        var response = await client.Http.PostAsJsonAsync("/api/parent/gate/unlock",
            new ParentGateUnlockRequest { Pin = "8317" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Valideyn sessiyası ilə başlayan klient — qapı endpoint-ləri onundur.</summary>
    private async Task<ApiTestClient> NewParentAsync()
    {
        var client = await ApiTestClient.CreateAsync(
            _factory, email: $"gate-{Guid.NewGuid():N}@petpal.test");

        client.SwitchToParent();
        return client;
    }
}

/// <summary>
/// Yumurta danışmır. Ev ekranı balonu onsuz da toxunulmaz edir, amma
/// <c>/chat</c> ünvanına birbaşa keçmək olur (veb hostda ünvan sətri var),
/// ona görə qayda SERVERDƏ də durmalıdır.
/// </summary>
public class EggChatTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public EggChatTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Yumurta_SohbetiRedd()
    {
        var client = await ApiTestClient.CreateAsync(_factory, $"eggchat-{Guid.NewGuid():N}@petpal.test");
        await EnableChatAsync(client);

        var response = await client.Http.PostAsJsonAsync("/api/pet/chat",
            new PetPal.Shared.Dtos.Pets.PetChatRequest { Message = "salam" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Ekran «bağlı» göstərsin deyə vəziyyət də qapalı qaytarılır.</summary>
    [Fact]
    public async Task Yumurta_SohbetVeziyyetiBagliGorunur()
    {
        var client = await ApiTestClient.CreateAsync(_factory, $"eggstate-{Guid.NewGuid():N}@petpal.test");
        await EnableChatAsync(client);

        var state = await client.Http.GetFromJsonAsync<PetPal.Shared.Dtos.Pets.PetChatStateDto>("/api/pet/chat");

        Assert.NotNull(state);
        Assert.False(state!.Enabled);
    }

    [Fact]
    public async Task CixandanSonra_SohbetAcilir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, $"hatchchat-{Guid.NewGuid():N}@petpal.test");
        await EnableChatAsync(client);
        await client.HatchAsync(_factory);

        var state = await client.Http.GetFromJsonAsync<PetPal.Shared.Dtos.Pets.PetChatStateDto>("/api/pet/chat");

        Assert.NotNull(state);
        Assert.True(state!.Enabled);
    }

    private static async Task EnableChatAsync(ApiTestClient client)
    {
        client.SwitchToParent();
        (await client.Http.PutAsJsonAsync($"/api/parent/children/{client.ChildId}/chat",
            new PetPal.Shared.Dtos.Parent.ChatSettingsRequest { Enabled = true })).EnsureSuccessStatusCode();
        client.SwitchToChild();
    }
}
