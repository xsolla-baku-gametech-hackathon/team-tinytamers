using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Notifications;

namespace PetPal.Tests;

/// <summary>
/// Dostluq sorğusunun push bildirişi.
///
/// <para>Bu bildiriş əvvəl VALİDEYNƏ gedirdi və iki nəticəsi vardı: alıcı
/// sorğusu <c>ParentUserId</c>-yə görə idi (yəni ailənin bütün cihazlarına
/// düşürdü) və yuxu qaydasından qəsdən azad idi (qərar verən böyükdür).</para>
///
/// <para>Qərar uşağa keçəndən sonra hər ikisi səhvə çevrildi: iki uşaqlı ailədə
/// bacı-qardaşın cihazı «səninlə dost olmaq istəyir» yazısını alırdı — həm yalan,
/// həm də qonşu uşağın adının sızması; və gecə saat 2-də gələn sorğu uşağı
/// oyada bilirdi, halbuki app həmin saatda ekranı bloklayır.</para>
///
/// <para>Kod yolu adi testlərdə İŞLƏMİR, çünki göndərici konfiqurasiya
/// olunmayanda servis dərhal qayıdır. Ona görə burada konfiqurasiya edilmiş
/// saxta göndərici işlədilir.</para>
/// </summary>
public class FriendRequestNotificationTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public FriendRequestNotificationTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Bildiriş yalnız sorğunu ALAN uşağın cihazına düşməlidir.</summary>
    [Fact]
    public async Task Bildiris_YalnizAlanUsaginCihazinaGedir()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (recipient, sibling, recipientToken, siblingToken) = await TwoSiblingsAsync(db, hour: 12);

        var sender = new RecordingSender();
        var service = NewService(db, sender, respectBedtime: true);

        await service.NotifyFriendRequestAsync(recipient.Id, "Ali");

        var sent = Assert.Single(sender.Batches);
        Assert.Equal([recipientToken], sent.Tokens);
        Assert.DoesNotContain(siblingToken, sent.Tokens);

        // Bacı-qardaşın cihazı heç nə almamalıdır — nə mətn, nə ad.
        Assert.DoesNotContain(sibling.DisplayName, sent.Message.Body, StringComparison.Ordinal);
        Assert.Contains("Ali", sent.Message.Body, StringComparison.Ordinal);
        Assert.Equal("/friends", sent.Message.Route);
    }

    /// <summary>
    /// Yuxu rejimində bildiriş GETMİR. App gecə ekranı bloklayır — telefonu
    /// zəngləndirmək eyni vədin əksi olardı.
    /// </summary>
    [Fact]
    public async Task YuxuRejiminde_BildirisGetmir()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Uşağın yerli saatı 02:00 — yuxu aralığındadır (21:00–07:00).
        var (recipient, _, _, _) = await TwoSiblingsAsync(db, hour: 2);

        var sender = new RecordingSender();
        var service = NewService(db, sender, respectBedtime: true);

        await service.NotifyFriendRequestAsync(recipient.Id, "Ali");

        Assert.Empty(sender.Batches);
    }

    /// <summary>Qayda söndürüləndə bildiriş yenə gedir — açar həqiqətən açardır.</summary>
    [Fact]
    public async Task YuxuQaydasiSondurulubse_BildirisGedir()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (recipient, _, _, _) = await TwoSiblingsAsync(db, hour: 2);

        var sender = new RecordingSender();
        var service = NewService(db, sender, respectBedtime: false);

        await service.NotifyFriendRequestAsync(recipient.Id, "Ali");

        Assert.Single(sender.Batches);
    }

    // ---------- Köməkçilər ----------

    /// <summary>
    /// Bir valideyn, iki uşaq, hər birinin öz cihazı. <paramref name="hour"/>
    /// alıcı uşağın YERLİ saatıdır — saat fərqi profilə yazılır, çünki server
    /// UTC-də işləyir.
    /// </summary>
    private async Task<(ChildProfile Recipient, ChildProfile Sibling, string RecipientToken, string SiblingToken)> TwoSiblingsAsync(
        AppDbContext db, int hour)
    {
        var parent = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"push-{Guid.NewGuid():N}@petpal.test",
            Email = $"push-{Guid.NewGuid():N}@petpal.test",
            DisplayName = "Push Parent"
        };
        db.Users.Add(parent);

        var utcHour = _factory.Clock.GetUtcNow().UtcDateTime.Hour;
        var offset = (hour - utcHour) * 60;

        var recipient = NewChild(parent.Id, "Alici", offset);
        var sibling = NewChild(parent.Id, "Bacisi", offset);
        db.ChildProfiles.AddRange(recipient, sibling);

        // Tokenlər testlər arasında UNİKAL olmalıdır: baza sinif boyu paylaşılır
        // və `DeviceTokens.Token` unikal indeksdədir.
        var recipientToken = $"dev-recipient-{Guid.NewGuid():N}";
        var siblingToken = $"dev-sibling-{Guid.NewGuid():N}";

        db.DeviceTokens.AddRange(
            NewDevice(recipientToken, parent.Id, recipient.Id),
            NewDevice(siblingToken, parent.Id, sibling.Id));

        await db.SaveChangesAsync();
        return (recipient, sibling, recipientToken, siblingToken);
    }

    private static ChildProfile NewChild(Guid parentId, string name, int utcOffsetMinutes) => new()
    {
        Id = Guid.NewGuid(),
        ParentUserId = parentId,
        DisplayName = name,
        AvatarKey = "avatar-fox",
        PinHash = "x",
        FriendCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
        LanguageCode = "az",
        UtcOffsetMinutes = utcOffsetMinutes,
        BedtimeStartHour = 21,
        BedtimeEndHour = 7
    };

    private static DeviceToken NewDevice(string token, Guid parentId, Guid childId) => new()
    {
        Id = Guid.NewGuid(),
        Token = token,
        Platform = "android",
        ParentUserId = parentId,
        ChildProfileId = childId
    };

    private NotificationService NewService(AppDbContext db, INotificationSender sender, bool respectBedtime) =>
        new(db,
            sender,
            Options.Create(new NotificationOptions { Enabled = true, RespectBedtime = respectBedtime }),
            _factory.Clock,
            NullLogger<NotificationService>.Instance);

    /// <summary>Konfiqurasiya olunmuş, amma heç yerə getməyən göndərici.</summary>
    private sealed class RecordingSender : INotificationSender
    {
        public List<(IReadOnlyCollection<string> Tokens, PushMessage Message)> Batches { get; } = [];

        public bool IsConfigured => true;

        public Task<IReadOnlyCollection<string>> SendAsync(
            IReadOnlyCollection<string> tokens, PushMessage message, CancellationToken ct = default)
        {
            Batches.Add((tokens, message));
            return Task.FromResult<IReadOnlyCollection<string>>([]);
        }
    }
}
