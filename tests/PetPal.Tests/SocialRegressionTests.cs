using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PetPal.Api.Data;
using PetPal.Api.Discoveries;
using PetPal.Api.Entities;
using PetPal.Api.Notifications;
using PetPal.Shared.Dtos.Auth;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Dtos.Notifications;
using PetPal.Shared.Dtos.Parent;
using PetPal.Shared.Dtos.Social;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// 2026-09-06 auditində tapılmış SƏKKİZ səhvin reqressiya testləri.
///
/// <para>Hər biri əvvəlcə səhv davranışı təsdiqləyən testlə sübut edilib,
/// sonra düzəldilib. Bu fayl düzəlişlərin geri qayıtmamasını qoruyur —
/// adlar səhvin özünü deyil, DOĞRU davranışı təsvir edir.</para>
/// </summary>
public class ChallengeRegressionTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ChallengeRegressionTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>
    /// Öz axtarışı gedən uşaq dostun çağırışını qəbul edə bilməlidir.
    /// Əvvəl «yarımçıq duel» yoxlaması gözləyən dueli də oyun sayırdı və
    /// qəbul uşağın ÖZ duelini qaytarırdı — çağıran isə əbədi gözləyirdi.
    /// </summary>
    [Fact]
    public async Task OzAxtarisiGedenUsaq_DostunCagirisiniQebulEdeBilir()
    {
        var (challenger, friend) = await FriendPairAsync();

        var challenge = await ChallengeAsync(challenger, friend.ChildId);

        var ownSearch = await friend.Http.PostAsync("/api/learn/arena/duels", null);
        ownSearch.EnsureSuccessStatusCode();
        var ownDuel = (await ownSearch.Content.ReadFromJsonAsync<ArenaDuelDto>())!;

        var accepted = await AcceptAsync(friend, challenge.DuelId);

        Assert.Equal(challenge.DuelId, accepted.DuelId);
        Assert.NotEqual(ownDuel.DuelId, accepted.DuelId);
    }

    /// <summary>
    /// İkinci dosta çağırış YENİ duel yaratmalıdır. Əvvəl funksiya birinci
    /// çağırışın özünü qaytarırdı — ikinci dost heç nə görmürdü.
    /// </summary>
    [Fact]
    public async Task IkinciDostaCagiris_YeniDuelYaradir()
    {
        var (challenger, first) = await FriendPairAsync();
        var second = await NewChildAsync();
        await BefriendAsync(challenger, second);

        var toFirst = await ChallengeAsync(challenger, first.ChildId);
        var toSecond = await ChallengeAsync(challenger, second.ChildId);

        Assert.NotEqual(toFirst.DuelId, toSecond.DuelId);
        Assert.Contains((await StatusAsync(second)).IncomingChallenges, c => c.DuelId == toSecond.DuelId);

        // Köhnə çağırış söndürülür — birinci dost artıq mövcud olmayan dueli
        // qəbul etməyə çalışmamalıdır.
        Assert.DoesNotContain((await StatusAsync(first)).IncomingChallenges, c => c.DuelId == toFirst.DuelId);
    }

    /// <summary>
    /// Çağırışda rəqib duelə qoşulmamışdan əvvəl də məlumdur (ünvan var), ona
    /// görə ekran «axtarılır» yox, «filankəs gözlənilir» deyə bilir.
    /// </summary>
    [Fact]
    public async Task Cagiris_RaqibinAdiniDasiyir()
    {
        var (challenger, friend) = await FriendPairAsync();

        var challenge = await ChallengeAsync(challenger, friend.ChildId);

        Assert.NotNull(challenge.Opponent);
        Assert.False(string.IsNullOrWhiteSpace(challenge.Opponent!.DisplayName));

        // Duel hələ BAŞLAMAYIB: ad görünsə də geri sayım başlamamalıdır.
        Assert.Equal(DuelStatus.WaitingOpponent, challenge.Status);
    }

    /// <summary>
    /// Çağırılan uşaq rəqib kimi ÇAĞIRANI görməlidir, özünü yox.
    ///
    /// <para>Rəqib artıq duelin ünvanından da oxuna bilir, ona görə DTO-nu
    /// quran kod «ünvan mənəmsə, rəqib deyiləm» şərtini saxlamalıdır.</para>
    /// </summary>
    [Fact]
    public async Task CagirilanUsaq_RaqibKimiCagiraniGorur()
    {
        var (challenger, friend) = await FriendPairAsync();
        var challenge = await ChallengeAsync(challenger, friend.ChildId);

        // Çağırılan uşaq duelə yalnız QƏBUL EDƏNDƏN sonra baxa bilir.
        var accepted = await AcceptAsync(friend, challenge.DuelId);

        Assert.NotNull(accepted.Opponent);

        // Dost olduqlarına görə əsl ad görünür — və bu ad ÇAĞIRANINDIR.
        var challengerName = (await challenger.Http.GetFromJsonAsync<ArenaDuelDto>(
            $"/api/learn/arena/duels/{challenge.DuelId}"))!.Opponent!.DisplayName;

        Assert.NotEqual(accepted.Opponent!.DisplayName, challengerName);
    }

    /// <summary>
    /// Valideyni arenanı bağlamış dosta çağırış GÖNDƏRİLMİR. Əvvəl çağırış
    /// yaranırdı, dostun ekranında kart görünürdü, qəbul isə mümkün deyildi —
    /// uşaq üçün izahsız nasazlıq.
    /// </summary>
    [Fact]
    public async Task ArenasiBagliDosta_CagirisGonderilmir()
    {
        var (challenger, friend) = await FriendPairAsync();
        await SetArenaAsync(friend, enabled: false);

        var response = await challenger.Http.PostAsync($"/api/learn/arena/challenge/{friend.ChildId}", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty((await StatusAsync(friend)).IncomingChallenges);
    }

    /// <summary>
    /// Dostluq pozulandan sonra çağırış qəbul edilə bilməz. Əvvəl dostluq
    /// yalnız çağırış YARADILARKƏN yoxlanılırdı.
    /// </summary>
    [Fact]
    public async Task DostlugPozulandanSonra_CagirisQebulEdilmir()
    {
        var (challenger, friend) = await FriendPairAsync();
        var challenge = await ChallengeAsync(challenger, friend.ChildId);

        (await challenger.Http.DeleteAsync($"/api/social/friends/{friend.ChildId}")).EnsureSuccessStatusCode();

        var accept = await friend.Http.PostAsync($"/api/learn/arena/challenge/{challenge.DuelId}/accept", null);

        Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
    }

    /// <summary>
    /// Dost həddi SORĞU GÖNDƏRƏN tərəfdə də işləməlidir.
    ///
    /// <para>Əvvəl hədd yalnız təsdiq edən uşaq üçün yoxlanılırdı. Göndərənin
    /// Active sayı isə göndərmə anında həmişə 0-dır (bütün sorğular gözləyir),
    /// yəni bir uşaq istənilən qədər sorğu göndərib hamısını təsdiqlədə bilirdi.</para>
    /// </summary>
    [Fact]
    public async Task DostHeddi_SorguGonderenTerefdeDe_Islenir()
    {
        const int maxFriends = 20;

        var requester = await NewChildAsync();
        var owners = new List<(HttpClient Http, Guid ChildId, string Code)>();

        HttpClient? parent = null;
        var perParent = 0;

        // Bir hesaba ən çoxu 6 uşaq düşür, ona görə valideynlər növbə ilə yaradılır.
        for (var i = 0; i <= maxFriends; i++)
        {
            if (parent is null || perParent == 6)
            {
                parent = await NewParentAsync();
                perParent = 0;
            }

            owners.Add(await NewChildOfAsync(parent));
            perParent++;
        }

        foreach (var owner in owners)
        {
            var sent = await requester.Http.PostAsJsonAsync("/api/social/friends",
                new AddFriendRequest { FriendCode = owner.Code });
            sent.EnsureSuccessStatusCode();
        }

        var approved = 0;
        var rejected = 0;

        foreach (var owner in owners)
        {
            var response = await owner.Http.PostAsJsonAsync(
                $"/api/social/friend-requests/{requester.ChildId}",
                new FriendRequestDecision { Approve = true });

            if (response.IsSuccessStatusCode)
                approved++;
            else
                rejected++;
        }

        var friends = (await requester.Http.GetFromJsonAsync<FriendsViewDto>("/api/social/friends"))!.Friends;

        Assert.Equal(maxFriends, friends.Count);
        Assert.Equal(maxFriends, approved);
        Assert.Equal(1, rejected);
    }

    // ---------- Köməkçilər ----------

    private Task<ApiTestClient> NewChildAsync(string childName = "Ava") =>
        ApiTestClient.CreateAsync(_factory, $"reg-{Guid.NewGuid():N}@petpal.test", childName);

    private async Task<HttpClient> NewParentAsync()
    {
        var http = _factory.CreateClient();

        var register = await http.PostAsJsonAsync("/api/auth/register", new RegisterParentRequest
        {
            Email = $"reg-{Guid.NewGuid():N}@petpal.test",
            Password = "Passw0rd!",
            DisplayName = "Regression Parent"
        });
        register.EnsureSuccessStatusCode();

        var parent = (await register.Content.ReadFromJsonAsync<AuthResponse>())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parent.AccessToken);

        return http;
    }

    /// <summary>Valideyn sessiyasında yeni uşaq yaradır və dost kodunu paneldən oxuyur.</summary>
    /// <summary>
    /// Uşağı valideyn sessiyasında yaradır, sonra ONUN ÖZ sessiyasını açır.
    ///
    /// <para>Qaytarılan klient uşaq tokeni daşıyır, valideynin yox: dostluq
    /// sorğusuna cavabı artıq uşaq özü verir (<c>/api/social/friend-requests</c>)
    /// və valideyn tokeni ora buraxılmır. Hər uşaq ayrıca klient alır — bir
    /// HttpClient yalnız bir token daşıya bilər.</para>
    /// </summary>
    private async Task<(HttpClient Http, Guid ChildId, string Code)> NewChildOfAsync(HttpClient parent)
    {
        var created = await parent.PostAsJsonAsync("/api/auth/children", new CreateChildRequest
        {
            DisplayName = "Dost",
            Age = 8,
            AvatarKey = "avatar-fox",
            Pin = "1234",
            PetName = "Max",
            PetSpecies = "fox",
            LanguageCode = "az"
        });
        created.EnsureSuccessStatusCode();

        var summary = (await created.Content.ReadFromJsonAsync<ChildSummaryDto>())!;
        var dashboard = (await parent.GetFromJsonAsync<ParentDashboardDto>(
            $"/api/parent/children/{summary.Id}/dashboard"))!;

        // Uşaq girişi VALİDEYN sessiyası tələb edir, ona görə token əvvəlcə
        // ondan köçürülür, sonra uşağınkı ilə əvəz olunur.
        var childHttp = _factory.CreateClient();
        childHttp.DefaultRequestHeaders.Authorization = parent.DefaultRequestHeaders.Authorization;

        var login = await childHttp.PostAsJsonAsync("/api/auth/children/login",
            new ChildLoginRequest { ChildId = summary.Id, Pin = "1234" });
        login.EnsureSuccessStatusCode();

        var child = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        childHttp.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", child.AccessToken);

        return (childHttp, summary.Id, dashboard.FriendCode);
    }

    /// <summary>Adlar QƏSDƏN fərqlidir: eyni ad testin nəyi yoxladığını gizlədir.</summary>
    private async Task<(ApiTestClient First, ApiTestClient Second)> FriendPairAsync()
    {
        var first = await NewChildAsync("Cagiran");
        var second = await NewChildAsync("Raqib");
        await BefriendAsync(first, second);
        return (first, second);
    }

    private static async Task BefriendAsync(ApiTestClient requester, ApiTestClient owner)
    {
        var code = (await owner.Http.GetFromJsonAsync<string>("/api/social/friend-code"))!;

        (await requester.Http.PostAsJsonAsync("/api/social/friends",
            new AddFriendRequest { FriendCode = code })).EnsureSuccessStatusCode();

        (await owner.Http.PostAsJsonAsync($"/api/social/friend-requests/{requester.ChildId}",
            new FriendRequestDecision { Approve = true })).EnsureSuccessStatusCode();
    }

    private static async Task SetArenaAsync(ApiTestClient client, bool enabled)
    {
        client.SwitchToParent();
        (await client.Http.PutAsJsonAsync($"/api/parent/children/{client.ChildId}/arena",
            new ArenaSettingsRequest { Enabled = enabled, FriendsOnly = false })).EnsureSuccessStatusCode();
        client.SwitchToChild();
    }

    private static async Task<ArenaDuelDto> ChallengeAsync(ApiTestClient client, Guid friendChildId)
    {
        var response = await client.Http.PostAsync($"/api/learn/arena/challenge/{friendChildId}", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ArenaDuelDto>())!;
    }

    private static async Task<ArenaDuelDto> AcceptAsync(ApiTestClient client, Guid duelId)
    {
        var response = await client.Http.PostAsync($"/api/learn/arena/challenge/{duelId}/accept", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ArenaDuelDto>())!;
    }

    private static async Task<ArenaStatusDto> StatusAsync(ApiTestClient client) =>
        (await client.Http.GetFromJsonAsync<ArenaStatusDto>("/api/learn/arena/status"))!;
}

/// <summary>Bildiriş və şəkil saxlama qatındakı üç səhvin reqressiya testləri.</summary>
public class NotificationRegressionTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public NotificationRegressionTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>Göndərilən tokenləri yazan sınaq göndəricisi.</summary>
    private sealed class RecordingSender : INotificationSender
    {
        public List<string> Tokens { get; } = [];
        public bool IsConfigured => true;

        public Task<IReadOnlyCollection<string>> SendAsync(
            IReadOnlyCollection<string> tokens, PushMessage message, CancellationToken ct = default)
        {
            Tokens.AddRange(tokens);
            return Task.FromResult<IReadOnlyCollection<string>>([]);
        }
    }

    /// <summary>
    /// Valideyn bildirişi app-in ÖZ qeydiyyatı ilə çatmalıdır.
    ///
    /// <para>App tokeni uşaq sessiyasından göndərir. Əvvəl sətirdə yalnız
    /// <c>ChildProfileId</c> dolurdu, dostluq sorğusu bildirişi isə
    /// <c>ParentUserId</c>-yə baxırdı — yəni bildiriş heç vaxt heç bir cihaza
    /// getmirdi və özəllik səssizcə işləmirdi.</para>
    /// </summary>
    [Fact]
    public async Task ValideynBildirisi_AppinQeydiyyatiIle_Catir()
    {
        var client = await NewFamilyAsync();
        var token = $"tok-{Guid.NewGuid():N}";

        (await client.Http.PostAsJsonAsync("/api/notifications/devices",
            new RegisterDeviceRequest { Token = token, Platform = "android" })).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recorder = new RecordingSender();

        await Service(db, recorder).NotifyFriendRequestAsync(client.ChildId, "Dost");

        Assert.Contains(token, recorder.Tokens);
    }

    /// <summary>Cihaz sətri həm uşağa, həm valideynə bağlanır — cihaz ailəyə aiddir.</summary>
    [Fact]
    public async Task CihazSetri_HerIkiSahibiDasiyir()
    {
        var client = await NewFamilyAsync();
        var token = $"tok-{Guid.NewGuid():N}";

        (await client.Http.PostAsJsonAsync("/api/notifications/devices",
            new RegisterDeviceRequest { Token = token, Platform = "android" })).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var row = await db.DeviceTokens.SingleAsync(d => d.Token == token);

        Assert.Equal(client.ChildId, row.ChildProfileId);
        Assert.NotNull(row.ParentUserId);
    }

    /// <summary>
    /// Yad sessiya başqa ailənin cihazını silə bilməz. Əvvəl silmə yalnız
    /// tokenə baxırdı — tokeni bilən hər kəs istənilən cihazı bildiriş
    /// siyahısından çıxara bilirdi.
    /// </summary>
    [Fact]
    public async Task YadSessiya_OzgeCihaziniSileBilmir()
    {
        var owner = await NewFamilyAsync();
        var stranger = await NewFamilyAsync();
        var token = $"tok-{Guid.NewGuid():N}";

        (await owner.Http.PostAsJsonAsync("/api/notifications/devices",
            new RegisterDeviceRequest { Token = token, Platform = "android" })).EnsureSuccessStatusCode();

        await stranger.Http.DeleteAsync($"/api/notifications/devices/{token}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(await db.DeviceTokens.AnyAsync(d => d.Token == token));
    }

    /// <summary>Öz cihazını silmək isə işləməlidir.</summary>
    [Fact]
    public async Task OzCihazi_SilinirmiIsleyir()
    {
        var owner = await NewFamilyAsync();
        var token = $"tok-{Guid.NewGuid():N}";

        (await owner.Http.PostAsJsonAsync("/api/notifications/devices",
            new RegisterDeviceRequest { Token = token, Platform = "android" })).EnsureSuccessStatusCode();

        (await owner.Http.DeleteAsync($"/api/notifications/devices/{token}")).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(await db.DeviceTokens.AnyAsync(d => d.Token == token));
    }

    /// <summary>
    /// Saxlama qovluğunun sonundakı «/» şəkilləri itirməməlidir.
    ///
    /// <para>Əvvəl belə konfiqurasiyada fayl diskə DÜZGÜN yazılırdı, oxu tərəfi
    /// isə kökü fərqli hesabladığına görə heç birini tapa bilmirdi — tək bir
    /// simvol bütün kolleksiyanı görünməz edirdi.</para>
    /// </summary>
    [Fact]
    public async Task SaxlamaQovlugununSonundakiSlash_SekilleriItirmir()
    {
        var root = Path.Combine(Path.GetTempPath(), $"petpal-reg-{Guid.NewGuid():N}");

        try
        {
            var store = new LocalDiscoveryPhotoStore(
                Options.Create(new DiscoveryOptions
                {
                    StorageRoot = root,
                    StorageFolder = "uploads/discoveries/"
                }),
                new RegressionEnvironment());

            var key = await store.SaveAsync(Guid.NewGuid(), new byte[] { 0xFF, 0xD8, 0xFF }, ".jpg");

            Assert.NotNull(await store.OpenAsync(key));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private NotificationService Service(AppDbContext db, INotificationSender sender) =>
        new(db, sender, Options.Create(new NotificationOptions()), _factory.Clock,
            NullLogger<NotificationService>.Instance);

    private Task<ApiTestClient> NewFamilyAsync() =>
        ApiTestClient.CreateAsync(_factory, $"reg-{Guid.NewGuid():N}@petpal.test");

    private sealed class RegressionEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "PetPal.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Test";
    }
}
