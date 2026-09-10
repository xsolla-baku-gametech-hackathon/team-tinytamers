using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PetPal.Api.Discoveries;
using PetPal.Shared.Dtos.Discovery;
using PetPal.Shared.Dtos.Parent;
using PetPal.Shared.Dtos.Social;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

public class DiscoveryTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public DiscoveryTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task IlkKesf_UlduzQazandirir()
    {
        var client = await NewChildAsync();

        var result = await DiscoverAsync(client, "Leaf");

        Assert.True(result.IsFirstOfKind);
        Assert.Equal(15, result.StarsEarned);
        Assert.Contains("Leaf", result.Message);
        Assert.Equal(1, result.TotalDiscoveries);
    }

    [Fact]
    public async Task TekrarKesf_DahaAzUlduzVerir()
    {
        var client = await NewChildAsync();

        await DiscoverAsync(client, "Stone");
        var second = await DiscoverAsync(client, "Stone");

        Assert.False(second.IsFirstOfKind);
        Assert.Equal(10, second.StarsEarned);
    }

    [Fact]
    public async Task GundelikLimitdenSonraUlduzVerilmir()
    {
        var client = await NewChildAsync();

        for (var i = 0; i < 5; i++)
            await DiscoverAsync(client, $"Item-{i}");

        var overLimit = await DiscoverAsync(client, "Item-over");

        Assert.Equal(0, overLimit.StarsEarned);
        Assert.Equal(6, overLimit.TotalDiscoveries);
    }

    [Fact]
    public async Task BosAdQebulEdilmir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.PostAsJsonAsync("/api/discoveries", new DiscoveryRequest { Label = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task XarabSekilQebulEdilmir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.PostAsJsonAsync("/api/discoveries", new DiscoveryRequest
        {
            Label = "Feather",
            PhotoBase64 = "bu-base64-deyil!!!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Telefon kamerası 10–20 MB-lıq şəkil çıxarır; köhnə 3 MB limiti ilə uşaq
    /// öz çəkdiyi şəkli göndərə bilmirdi. Limit 30 MB-dır — burada 5 MB-lıq
    /// şəkil yoxlanılır, çünki test məqsədi limitin KEÇDİYİNİ təsdiqləməkdir,
    /// 40 MB-lıq gövdəni yaddaşda saxlamaq deyil.
    /// </summary>
    [Fact]
    public async Task BoyukSekilQebulEdilir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.PostAsJsonAsync("/api/discoveries", new DiscoveryRequest
        {
            Label = "Pine cone",
            PhotoBase64 = Convert.ToBase64String(new byte[5 * 1024 * 1024])
        });

        response.EnsureSuccessStatusCode();
    }

    /// <summary>Limitdən böyük şəkil aydın mesajla rədd olunmalıdır — səssiz xəta yox.</summary>
    [Fact]
    public async Task LimitdenBoyukSekilRedEdilir()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DiscoveryOptions>>().Value;

        Assert.Equal(30 * 1024 * 1024, options.MaxPhotoBytes);

        var client = await NewChildAsync();

        var response = await client.Http.PostAsJsonAsync("/api/discoveries", new DiscoveryRequest
        {
            Label = "Too big",
            PhotoBase64 = Convert.ToBase64String(new byte[options.MaxPhotoBytes + 1024])
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Şəkil yükləmə app-in yeganə böyük gövdə qəbul edən yoludur (30 MB fayl
    /// JSON-da ~40 MB olur). Limitsiz qalsa, ard-arda gələn bir neçə sorğu kiçik
    /// instansiyanın yaddaşını doldura bilər.
    /// </summary>
    [Fact]
    public async Task ArdicilYuklemeler_LimitdenSonra429Alir()
    {
        var client = await NewChildAsync();

        for (var i = 0; i < 10; i++)
        {
            var allowed = await client.Http.PostAsJsonAsync("/api/discoveries",
                new DiscoveryRequest { Label = $"Limit-{i}" });

            allowed.EnsureSuccessStatusCode();
        }

        var blocked = await client.Http.PostAsJsonAsync("/api/discoveries",
            new DiscoveryRequest { Label = "Limit-over" });

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    /// <summary>
    /// Limit UŞAĞA görə bölünməlidir, IP-yə görə yox — əks halda eyni evdəki
    /// (və ya eyni NAT arxasındakı) ikinci uşaq birincinin limitindən əziyyət
    /// çəkərdi. Bu, yalnız limiter autentifikasiyadan sonra işləyəndə mümkündür.
    /// </summary>
    [Fact]
    public async Task BirUsaginLimiti_DigerUsagaTesirEtmir()
    {
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        for (var i = 0; i < 11; i++)
            await first.Http.PostAsJsonAsync("/api/discoveries", new DiscoveryRequest { Label = $"Doldur-{i}" });

        var otherChild = await second.Http.PostAsJsonAsync("/api/discoveries",
            new DiscoveryRequest { Label = "Başqa uşaq" });

        otherChild.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task KesflerSiyahidaGorunur()
    {
        var client = await NewChildAsync();
        await DiscoverAsync(client, "Acorn");

        var response = await client.Http.GetAsync("/api/discoveries");
        response.EnsureSuccessStatusCode();

        var list = (await response.Content.ReadFromJsonAsync<List<DiscoveryDto>>())!;

        Assert.Single(list);
        Assert.Equal("Acorn", list[0].Label);
        Assert.False(list[0].HasPhoto);
    }

    /// <summary>
    /// Şəkil bir vaxtlar YÜKLƏNİRDİ, amma heç bir yol onu geri vermirdi: fayl
    /// diskdə qalır, uşaq isə çəkdiyi şəkli bir daha görmürdü. Kolleksiyanın
    /// mənası budur — ona görə yol testlə qorunur.
    /// </summary>
    [Fact]
    public async Task YuklenenSekil_GeriQaytarilir()
    {
        var client = await NewChildAsync();
        var photo = FakeImage(0xFF, 0xD8, 0xFF);

        var discovery = await DiscoverAsync(client, "Maple leaf", photo);

        var list = (await client.Http.GetFromJsonAsync<List<DiscoveryDto>>("/api/discoveries"))!;
        Assert.True(list.Single(d => d.Id == discovery.Id).HasPhoto);

        var response = await client.Http.GetAsync($"/api/discoveries/{discovery.Id}/photo");
        response.EnsureSuccessStatusCode();

        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(photo, await response.Content.ReadAsByteArrayAsync());
    }

    /// <summary>
    /// Uzantı və Content-Type faylın MƏZMUNUNDAN seçilir. Hər şeyi ".jpg"
    /// adlandırmaq yükləmə vaxtı problem deyildi — şəkil geri veriləndə isə
    /// səhv Content-Type ilə brauzer onu göstərməyə bilər.
    /// </summary>
    [Fact]
    public async Task PngSekil_OzContentTypeIleQaytarilir()
    {
        var client = await NewChildAsync();
        var png = FakeImage(0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A);

        var discovery = await DiscoverAsync(client, "Snail shell", png);

        var response = await client.Http.GetAsync($"/api/discoveries/{discovery.Id}/photo");
        response.EnsureSuccessStatusCode();

        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SekilsizKesfinSekliIsteneNde_TapilmadiQayidir()
    {
        var client = await NewChildAsync();
        var discovery = await DiscoverAsync(client, "Cloud");

        var response = await client.Http.GetAsync($"/api/discoveries/{discovery.Id}/photo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Kəşfin id-si bilinsə belə, şəkil yalnız SAHİBİNƏ verilir — sorğu
    /// "bu uşağın bu kəşfi" kimi qurulur.
    /// </summary>
    [Fact]
    public async Task BasqaUsaqOzgeSekliniGoreBilmir()
    {
        var owner = await NewChildAsync();
        var stranger = await NewChildAsync();

        var discovery = await DiscoverAsync(owner, "Feather", FakeImage(0xFF, 0xD8, 0xFF));

        var response = await stranger.Http.GetAsync($"/api/discoveries/{discovery.Id}/photo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<ApiTestClient> NewChildAsync() =>
        ApiTestClient.CreateAsync(_factory, $"discovery-{Guid.NewGuid():N}@petpal.test");

    /// <summary>Format yalnız ilk baytlardan tanınır — qalanı doldurmadır.</summary>
    private static byte[] FakeImage(params byte[] magic) => [.. magic, .. new byte[64]];

    private static async Task<DiscoveryResultDto> DiscoverAsync(
        ApiTestClient client, string label, byte[]? photo = null)
    {
        var response = await client.Http.PostAsJsonAsync("/api/discoveries", new DiscoveryRequest
        {
            Label = label,
            PhotoBase64 = photo is null ? null : Convert.ToBase64String(photo)
        });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<DiscoveryResultDto>())!;
    }
}

public class SocialTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public SocialTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task FriendCode_HerUsaqUcunVerilir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.GetAsync("/api/social/friend-code");
        response.EnsureSuccessStatusCode();

        var code = (await response.Content.ReadFromJsonAsync<string>())!;

        Assert.Equal(6, code.Length);
    }

    [Fact]
    public async Task DostKoduYazmaq_DostluqQurmur_YalnizSorguYaradir()
    {
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        await SendRequestAsync(first, second);

        var firstView = await GetFriendsAsync(first);
        var secondView = await GetFriendsAsync(second);

        // Ən vacib qayda: təsdiqə qədər HEÇ KİM dost siyahısında görünmür.
        Assert.Empty(firstView.Friends);
        Assert.Empty(secondView.Friends);

        // Hər iki tərəf sorğunu görür, amma istiqamət fərqlidir.
        Assert.True(Assert.Single(firstView.Pending).IsOutgoing);
        Assert.False(Assert.Single(secondView.Pending).IsOutgoing);
    }

    [Fact]
    public async Task ValideynTesdiqleyendenSonra_DostluqQarsiliqliIsleyir()
    {
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        await SendRequestAsync(first, second);
        await RespondAsync(second, first.ChildId, approve: true);

        var firstFriends = (await GetFriendsAsync(first)).Friends;
        var secondFriends = (await GetFriendsAsync(second)).Friends;

        Assert.Equal(second.ChildId, Assert.Single(firstFriends).ChildId);
        Assert.Equal(first.ChildId, Assert.Single(secondFriends).ChildId);

        // Gözləyən sorğu qalmır — vəziyyət iki yerdə eyni anda olmamalıdır.
        Assert.Empty((await GetFriendsAsync(first)).Pending);
    }

    [Fact]
    public async Task ValideynImtinaEdirse_SorguSilinir()
    {
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        await SendRequestAsync(first, second);
        await RespondAsync(second, first.ChildId, approve: false);

        var firstView = await GetFriendsAsync(first);

        Assert.Empty(firstView.Friends);

        // Sətir qalsaydı uşaq eyni kodu bir daha yaza bilməzdi.
        Assert.Empty(firstView.Pending);
    }

    /// <summary>
    /// Gələn sorğu UŞAĞIN öz ekranında görünür (valideyn panelində yox) və
    /// «gələn» kimi işarələnir — uşaq cavab düymələrini yalnız orada görməlidir.
    /// </summary>
    [Fact]
    public async Task GelenSorgu_UsaginOzEkranindaGorunur()
    {
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        await SendRequestAsync(first, second);

        var view = await GetFriendsAsync(second);
        var pending = Assert.Single(view.Pending);

        Assert.Equal(first.ChildId, pending.ChildId);
        Assert.False(pending.IsOutgoing);
    }

    /// <summary>Valideyn API-sində dostluq sorğusu ÜMUMİYYƏTLƏ yoxdur.</summary>
    [Fact]
    public async Task ValideynApisinde_DostluqSorgusuYoxdur()
    {
        var child = await NewChildAsync();

        child.SwitchToParent();
        var response = await child.Http.GetAsync($"/api/parent/children/{child.ChildId}/friend-requests");
        child.SwitchToChild();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EyniSorguIkinciDefeGondermek_KonfliktVerir()
    {
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        await SendRequestAsync(first, second);

        var code = await GetFriendCodeAsync(second);
        var again = await first.Http.PostAsJsonAsync("/api/social/friends", new AddFriendRequest { FriendCode = code });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task DostSilinende_HerIkiIstiqametGedir()
    {
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        await SendRequestAsync(first, second);
        await RespondAsync(second, first.ChildId, approve: true);

        var removed = await first.Http.DeleteAsync($"/api/social/friends/{second.ChildId}");
        removed.EnsureSuccessStatusCode();

        Assert.Empty((await GetFriendsAsync(first)).Friends);
        Assert.Empty((await GetFriendsAsync(second)).Friends);
    }

    [Fact]
    public async Task GozleyenDostla_KomandaMissiyasiQurulmur()
    {
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        // Sorğu var, təsdiq yoxdur — hələ dost deyil.
        await SendRequestAsync(first, second);

        var response = await first.Http.PostAsJsonAsync("/api/social/team-missions", new List<Guid> { second.ChildId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task KomandaMissiyasininBasligi_UsaginDilindeGelir()
    {
        var azChild = await NewChildAsync();
        var friend = await NewChildAsync();

        await SendRequestAsync(azChild, friend);
        await RespondAsync(friend, azChild.ChildId, approve: true);

        var response = await azChild.Http.PostAsJsonAsync("/api/social/team-missions", new List<Guid> { friend.ChildId });
        response.EnsureSuccessStatusCode();

        var mission = (await response.Content.ReadFromJsonAsync<TeamMissionDto>())!;

        // Profil "az" dilindədir: başlıq ingiliscə sabit mətn OLMAMALIDIR.
        Assert.DoesNotContain("Solve", mission.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sual", mission.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OlmayanKodQebulEdilmir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.PostAsJsonAsync("/api/social/friends",
            new AddFriendRequest { FriendCode = "ZZZZZZ" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OzunuDostKimiElaveEtmekOlmur()
    {
        var client = await NewChildAsync();
        var ownCode = await GetFriendCodeAsync(client);

        var response = await client.Http.PostAsJsonAsync("/api/social/friends",
            new AddFriendRequest { FriendCode = ownCode });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DostOlmayanlaKomandaMissiyasiQurulmur()
    {
        var first = await NewChildAsync();
        var stranger = await NewChildAsync();

        var response = await first.Http.PostAsJsonAsync("/api/social/team-missions", new List<Guid> { stranger.ChildId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DostlaKomandaMissiyasiQurulur_DevetCavabGozleyir()
    {
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        await SendRequestAsync(first, second);
        await RespondAsync(second, first.ChildId, approve: true);

        var response = await first.Http.PostAsJsonAsync("/api/social/team-missions", new List<Guid> { second.ChildId });
        response.EnsureSuccessStatusCode();

        var mission = (await response.Content.ReadFromJsonAsync<TeamMissionDto>())!;

        Assert.Equal(2, mission.Members.Count);
        Assert.Equal(0, mission.Progress);
        Assert.True(mission.Target > 0);

        // Başladan qoşulub, dəvət olunan isə cavab gözləyir — missiya dostun
        // başına gəlməməlidir.
        Assert.Equal(TeamMemberStatus.Joined, mission.MyStatus);
        Assert.Equal(TeamMemberStatus.Invited,
            mission.Members.Single(m => m.ChildId == second.ChildId).Status);
    }

    [Fact]
    public async Task DeveteQosulmaq_UzvluyuAktivEdir()
    {
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        await SendRequestAsync(first, second);
        await RespondAsync(second, first.ChildId, approve: true);

        var created = await first.Http.PostAsJsonAsync("/api/social/team-missions", new List<Guid> { second.ChildId });
        created.EnsureSuccessStatusCode();
        var mission = (await created.Content.ReadFromJsonAsync<TeamMissionDto>())!;

        var joined = await second.Http.PostAsync($"/api/social/team-missions/{mission.Id}/respond?join=true", null);
        joined.EnsureSuccessStatusCode();

        var updated = (await joined.Content.ReadFromJsonAsync<TeamMissionDto>())!;

        Assert.Equal(TeamMemberStatus.Joined, updated.MyStatus);
    }

    [Fact]
    public async Task DevetdenImtinaEdilse_MissiyaSiyahidaGorunmur()
    {
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        await SendRequestAsync(first, second);
        await RespondAsync(second, first.ChildId, approve: true);

        var created = await first.Http.PostAsJsonAsync("/api/social/team-missions", new List<Guid> { second.ChildId });
        created.EnsureSuccessStatusCode();
        var mission = (await created.Content.ReadFromJsonAsync<TeamMissionDto>())!;

        (await second.Http.PostAsync($"/api/social/team-missions/{mission.Id}/respond?join=false", null))
            .EnsureSuccessStatusCode();

        var list = await second.Http.GetAsync("/api/social/team-missions");
        list.EnsureSuccessStatusCode();
        var missions = (await list.Content.ReadFromJsonAsync<List<TeamMissionDto>>())!;

        Assert.DoesNotContain(missions, m => m.Id == mission.Id);
    }

    private Task<ApiTestClient> NewChildAsync() =>
        ApiTestClient.CreateAsync(_factory, $"social-{Guid.NewGuid():N}@petpal.test");

    private static async Task<string> GetFriendCodeAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync("/api/social/friend-code");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<string>())!;
    }

    private static async Task<FriendsViewDto> GetFriendsAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync("/api/social/friends");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FriendsViewDto>())!;
    }

    /// <summary><paramref name="from"/> uşağı <paramref name="to"/> uşağının kodunu yazır.</summary>
    private static async Task SendRequestAsync(ApiTestClient from, ApiTestClient to)
    {
        var code = await GetFriendCodeAsync(to);
        var response = await from.Http.PostAsJsonAsync("/api/social/friends", new AddFriendRequest { FriendCode = code });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Cavabı KODUN SAHİBİ ÖZÜ verir — sorğunu yazan uşaq öz seçimini artıq
    /// edib, razılığı verməli olan qarşı tərəfdir. Valideyn sessiyasına keçid
    /// yoxdur: bu, uşaq endpoint-idir.
    /// </summary>
    private static async Task RespondAsync(ApiTestClient owner, Guid requesterChildId, bool approve)
    {
        var response = await owner.Http.PostAsJsonAsync(
            $"/api/social/friend-requests/{requesterChildId}",
            new FriendRequestDecision { Approve = approve });

        response.EnsureSuccessStatusCode();
    }
}
