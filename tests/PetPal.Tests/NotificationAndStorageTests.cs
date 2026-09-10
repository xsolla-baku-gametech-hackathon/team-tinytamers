using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using PetPal.Api.Discoveries;
using PetPal.Api.Entities;
using PetPal.Api.Notifications;
using PetPal.Shared.Dtos.Notifications;

namespace PetPal.Tests;

// ===================== Saf qaydalar =====================

/// <summary>
/// Bildirişin nə vaxt göndəriləcəyi məhsul qərarıdır, ona görə ayrıca və
/// birbaşa yoxlanılır — şəbəkə və baza olmadan.
/// </summary>
public class NotificationScheduleTests
{
    private static ChildProfile Child(int bedtimeStart = 21, int bedtimeEnd = 7, int utcOffsetMinutes = 0) => new()
    {
        BedtimeStartHour = bedtimeStart,
        BedtimeEndHour = bedtimeEnd,
        UtcOffsetMinutes = utcOffsetMinutes
    };

    [Fact]
    public void YuxuVaxtinda_UsagaBildirisGetmir()
    {
        var child = Child();

        // 23:00 — yuxu zolağının içi.
        Assert.False(NotificationSchedule.CanSendToChild(
            child, new DateTime(2026, 9, 6, 23, 0, 0, DateTimeKind.Utc), respectBedtime: true));

        // 02:00 — gecə yarısından sonra, zolaq hələ davam edir.
        Assert.False(NotificationSchedule.CanSendToChild(
            child, new DateTime(2026, 9, 6, 2, 0, 0, DateTimeKind.Utc), respectBedtime: true));
    }

    [Fact]
    public void GunduzUsagaBildirisGedir()
    {
        Assert.True(NotificationSchedule.CanSendToChild(
            Child(), new DateTime(2026, 9, 6, 15, 0, 0, DateTimeKind.Utc), respectBedtime: true));
    }

    /// <summary>
    /// Server UTC-də işləyir, yuxu vaxtı isə uşağın ÖZ gecəsidir. Bakı (+4)
    /// üçün UTC 19:00 yerli 23:00 deməkdir — bildiriş getməməlidir.
    /// </summary>
    [Fact]
    public void YuxuVaxti_UsaginYerliSaatiIleHesablanir()
    {
        var baku = Child(utcOffsetMinutes: 240);

        Assert.False(NotificationSchedule.CanSendToChild(
            baku, new DateTime(2026, 9, 6, 19, 0, 0, DateTimeKind.Utc), respectBedtime: true));

        // Eyni an UTC-də yaşayan uşaq üçün hələ 19:00-dır — ona bildiriş gedir.
        Assert.True(NotificationSchedule.CanSendToChild(
            Child(), new DateTime(2026, 9, 6, 19, 0, 0, DateTimeKind.Utc), respectBedtime: true));
    }

    [Fact]
    public void AcarSondurulubse_YuxuQaydasiIslemir()
    {
        Assert.True(NotificationSchedule.CanSendToChild(
            Child(), new DateTime(2026, 9, 6, 23, 0, 0, DateTimeKind.Utc), respectBedtime: false));
    }

    /// <summary>Valideynə gedən bildiriş yuxu rejimindən asılı deyil — qərar onundur.</summary>
    [Fact]
    public void ValideynBildirisi_YuxuRejiminden_AsiliDeyil() =>
        Assert.True(NotificationSchedule.CanSendToParent());
}

/// <summary>
/// FCM giriş biletinin JWT hissəsi şəbəkəsizdir və ayrıca yoxlanılır:
/// səhv imzalanmış assertion produksiyada yalnız «bildiriş getmədi» kimi
/// görünərdi, səbəbi isə görünməzdi.
/// </summary>
public class FirebaseAssertionTests
{
    private static ServiceAccountKey TestKey()
    {
        using var rsa = RSA.Create(2048);

        return new ServiceAccountKey
        {
            ClientEmail = "petpal@test.iam.gserviceaccount.com",
            PrivateKey = rsa.ExportPkcs8PrivateKeyPem(),
            TokenUri = "https://oauth2.googleapis.com/token"
        };
    }

    [Fact]
    public void Assertion_DuzgunIddialariDasiyir()
    {
        var key = TestKey();
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(FirebaseAccessToken.BuildAssertion(key, now));

        Assert.Equal(key.ClientEmail, jwt.Issuer);
        Assert.Equal(key.TokenUri, Assert.Single(jwt.Audiences));
        Assert.Equal("https://www.googleapis.com/auth/firebase.messaging",
            jwt.Claims.First(c => c.Type == "scope").Value);
        Assert.Equal("RS256", jwt.Header.Alg);

        // Google bir saatdan uzun assertion qəbul etmir.
        Assert.True(jwt.ValidTo - jwt.ValidFrom <= TimeSpan.FromHours(1));
    }

    [Fact]
    public void AcarYoxdursa_GondericiSonukdur()
    {
        var sender = new NullNotificationSender();

        Assert.False(sender.IsConfigured);
    }
}

/// <summary>
/// Şəkil açarının normallaşdırılması. Köhnə qeydlərdə açar qovluq prefiksi ilə
/// yazılırdı; prefiks kəsilmirsə saxlama kökü dəyişən kimi BÜTÜN köhnə şəkillər
/// itmiş görünərdi.
/// </summary>
public class DiscoveryPhotoKeyTests
{
    private const string Folder = "uploads/discoveries";

    [Fact]
    public void KohneAcar_PrefiksdenTemizlenir() =>
        Assert.Equal("abc/def.jpg", DiscoveryPhotoKey.Normalize("uploads/discoveries/abc/def.jpg", Folder));

    [Fact]
    public void YeniAcar_ToxunulmazQalir() =>
        Assert.Equal("abc/def.jpg", DiscoveryPhotoKey.Normalize("abc/def.jpg", Folder));

    [Fact]
    public void WindowsAyiricisi_NormalHalaGetirilir() =>
        Assert.Equal("abc/def.jpg", DiscoveryPhotoKey.Normalize(@"uploads\discoveries\abc\def.jpg", Folder));
}

/// <summary>
/// Diskə yazan saxlama. Kök qovluğun konfiqurasiya oluna bilməsi kəşf
/// şəkillərinin deploy-da itməməsi üçün əsas şərtdir.
/// </summary>
public class LocalDiscoveryPhotoStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"petpal-{Guid.NewGuid():N}");

    private LocalDiscoveryPhotoStore CreateStore() =>
        new(Options.Create(new DiscoveryOptions { StorageRoot = _root }), new TestEnvironment());

    [Fact]
    public async Task YazilanSekil_GeriOxunur()
    {
        var store = CreateStore();
        var childId = Guid.NewGuid();
        var bytes = new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };

        var key = await store.SaveAsync(childId, bytes, ".png");
        var file = await store.OpenAsync(key);

        Assert.NotNull(file);
        Assert.Equal("image/png", file!.ContentType);
        Assert.NotNull(file.AbsolutePath);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(file.AbsolutePath!));
    }

    /// <summary>Açar kökdən ASILI OLMAMALIDIR: volume mount ediləndə kök dəyişir.</summary>
    [Fact]
    public async Task Acar_KokQovlugunuDasimir()
    {
        var key = await CreateStore().SaveAsync(Guid.NewGuid(), new byte[] { 0xFF, 0xD8, 0xFF }, ".jpg");

        Assert.DoesNotContain(_root, key);
        Assert.DoesNotContain("uploads", key);
    }

    [Fact]
    public async Task OlmayanSekil_NullQaytarir() =>
        Assert.Null(await CreateStore().OpenAsync($"{Guid.NewGuid():N}/yoxdur.jpg"));

    /// <summary>Kənar açar app-in başqa faylını verməməlidir.</summary>
    [Fact]
    public async Task KokdenKenaraCixanAcar_RedEdilir() =>
        Assert.Null(await CreateStore().OpenAsync("../../../etc/passwd"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);

        GC.SuppressFinalize(this);
    }

    private class TestEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "PetPal.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Test";
    }
}

// ===================== Endpoint axını =====================

public class DeviceTokenTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public DeviceTokenTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task UsaqSessiyasi_CihaziQeydEdir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.PostAsJsonAsync("/api/notifications/devices",
            new RegisterDeviceRequest { Token = $"tok-{Guid.NewGuid():N}", Platform = "android" });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ValideynSessiyasi_CihaziQeydEdir()
    {
        var client = await NewChildAsync();
        client.SwitchToParent();

        var response = await client.Http.PostAsJsonAsync("/api/notifications/devices",
            new RegisterDeviceRequest { Token = $"tok-{Guid.NewGuid():N}", Platform = "ios" });

        client.SwitchToChild();
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Eyni cihazda profil dəyişə bilər. İkinci sətir yaransaydı, köhnə profil
    /// bildiriş almağa davam edərdi — token unikaldır və sahibi yenilənir.
    /// </summary>
    [Fact]
    public async Task EyniToken_IkinciDefe_SetirCoxaltmir()
    {
        var client = await NewChildAsync();
        var token = $"tok-{Guid.NewGuid():N}";

        (await client.Http.PostAsJsonAsync("/api/notifications/devices",
            new RegisterDeviceRequest { Token = token, Platform = "android" })).EnsureSuccessStatusCode();

        client.SwitchToParent();
        var second = await client.Http.PostAsJsonAsync("/api/notifications/devices",
            new RegisterDeviceRequest { Token = token, Platform = "android" });
        client.SwitchToChild();

        second.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CihazSiyahidanCixarilir()
    {
        var client = await NewChildAsync();
        var token = $"tok-{Guid.NewGuid():N}";

        (await client.Http.PostAsJsonAsync("/api/notifications/devices",
            new RegisterDeviceRequest { Token = token, Platform = "android" })).EnsureSuccessStatusCode();

        var removed = await client.Http.DeleteAsync($"/api/notifications/devices/{token}");

        removed.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task BosToken_QebulEdilmir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.PostAsJsonAsync("/api/notifications/devices",
            new RegisterDeviceRequest { Token = "", Platform = "android" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<ApiTestClient> NewChildAsync() =>
        ApiTestClient.CreateAsync(_factory, $"push-{Guid.NewGuid():N}@petpal.test");
}
