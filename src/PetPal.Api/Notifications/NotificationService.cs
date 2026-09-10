using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Progress;
using PetPal.Shared.Dtos.Notifications;

namespace PetPal.Api.Notifications;

/// <summary>
/// Bildirişlərin məhsul qaydaları.
///
/// <para>İki qayda bu servisin əsasıdır:</para>
/// <list type="number">
///   <item><b>Uşağa gedən bildiriş yuxu rejimində saxlanılır.</b> App gecə
///   ekran vaxtını bloklayır — telefon bildirişi ilə həmin uşağı oyatmaq
///   eyni vədin əksi olardı.</item>
///   <item><b>Dost çağırışı üçün push YOXDUR.</b> Çağırış 60 saniyəlikdir və
///   yalnız app açıq olanda mənalıdır; onu real vaxt kanalı daşıyır. Bağlı
///   app-ə göndərilən «indi oyna» bildirişi uşağı ekrana çağırmaq deməkdir.</item>
/// </list>
/// </summary>
public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly INotificationSender _sender;
    private readonly NotificationOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AppDbContext db,
        INotificationSender sender,
        IOptions<NotificationOptions> options,
        TimeProvider clock,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _sender = sender;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ServiceResult<bool>> RegisterDeviceAsync(
        Guid? childId, Guid? parentUserId, RegisterDeviceRequest request, CancellationToken ct = default)
    {
        if (childId is null && parentUserId is null)
            return ServiceResult<bool>.Fail(Localized.T("Sessiya tapılmadı.", "No session was found."));

        var token = request.Token.Trim();
        var now = _clock.GetUtcNow().UtcDateTime;

        // Cihaz AİLƏYƏ aiddir, tək profilə yox: app tokeni uşaq sessiyasından
        // göndərir, dostluq sorğusu bildirişi isə valideynə ünvanlanır. Yalnız
        // ChildProfileId yazsaydıq (əvvəl belə idi), valideynə gedən bildiriş
        // HEÇ VAXT heç bir cihaz tapmazdı — özəllik səssizcə işləməzdi.
        var owningParentId = parentUserId ?? await _db.ChildProfiles
            .Where(c => c.Id == childId)
            .Select(c => (Guid?)c.ParentUserId)
            .FirstOrDefaultAsync(ct);

        var existing = await _db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == token, ct);

        if (existing is null)
        {
            _db.DeviceTokens.Add(new DeviceToken
            {
                Token = token,
                Platform = request.Platform,
                ChildProfileId = childId,
                ParentUserId = owningParentId,
                CreatedAt = now,
                LastSeenAt = now
            });
        }
        else
        {
            // Eyni cihazda profil dəyişə bilər: token sahibini YENİLƏYİRİK,
            // ikinci sətir yaratmırıq — yoxsa köhnə profil bildiriş almağa
            // davam edərdi.
            existing.ChildProfileId = childId;
            existing.ParentUserId = owningParentId;
            existing.Platform = request.Platform;
            existing.LastSeenAt = now;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // İki sorğu eyni tokeni eyni anda yazsa unikal indeks pozulur.
            // Nəticə onsuz da eynidir (sətir mövcuddur), ona görə qeydiyyat
            // uğurlu sayılır — istifadəçiyə 500 qaytarmaq mənasızdır.
            _db.ChangeTracker.Clear();
        }

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> UnregisterDeviceAsync(
        Guid? childId, Guid? parentUserId, string token, CancellationToken ct = default)
    {
        // Silmək YALNIZ öz cihazına aiddir. Əvvəl token bilən hər kəs istənilən
        // ailənin cihazını bildiriş siyahısından çıxara bilirdi.
        var owningParentId = parentUserId ?? await _db.ChildProfiles
            .Where(c => c.Id == childId)
            .Select(c => (Guid?)c.ParentUserId)
            .FirstOrDefaultAsync(ct);

        var rows = await _db.DeviceTokens
            .Where(d => d.Token == token
                        && (d.ParentUserId == owningParentId
                            || (childId != null && d.ChildProfileId == childId)))
            .ToListAsync(ct);

        if (rows.Count == 0)
            return ServiceResult<bool>.Ok(false);

        _db.DeviceTokens.RemoveRange(rows);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task NotifyFriendRequestAsync(
        Guid recipientChildId, string requesterName, CancellationToken ct = default)
    {
        if (!_sender.IsConfigured)
            return;

        var child = await _db.ChildProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == recipientChildId, ct);

        if (child is null)
            return;

        // Bu bildiriş artıq UŞAĞA gedir (qərar onundur), ona görə uşaq
        // bildirişlərinin qaydalarına tabedir:
        //
        // 1) YUXU REJİMİ. Əvvəl bu, valideyn bildirişi idi və qəsdən yuxu
        //    qaydasından azad idi (bax NotificationSchedule.CanSendToParent).
        //    Ünvan dəyişəndən sonra həmin azadlıq gecə saat 2-də uşağı oyadan
        //    dostluq sorğusuna çevrilirdi — app-in öz ekran vaxtı vədinin əksi.
        //
        // 2) ÜNVAN. Token sorğusu `ParentUserId`-yə görə gedəndə ailənin BÜTÜN
        //    cihazlarına düşürdü: iki uşaqlı ailədə bacı-qardaşın cihazı
        //    "səninlə dost olmaq istəyir" yazısını alırdı — həm yalan, həm də
        //    qonşu uşağın adının sızması. İndi yalnız həmin uşağın cihazları.
        if (!NotificationSchedule.CanSendToChild(child, _clock.GetUtcNow().UtcDateTime, _options.RespectBedtime))
            return;

        var tokens = await _db.DeviceTokens
            .Where(d => d.ChildProfileId == child.Id)
            .Select(d => d.Token)
            .ToListAsync(ct);

        var message = new PushMessage(
            Localized.T(child.LanguageCode, "Dostluq sorğusu", "Friend request"),
            Localized.T(child.LanguageCode,
                $"{requesterName} səninlə dost olmaq istəyir.",
                $"{requesterName} wants to be your friend."),
            Route: "/friends");

        await SendAsync(tokens, message, ct);
    }

    public async Task NotifyTeamMissionCompletedAsync(
        IEnumerable<Guid> childIds, string missionTitleEn, string? missionTitleAz, CancellationToken ct = default)
    {
        if (!_sender.IsConfigured)
            return;

        var ids = childIds.ToList();
        if (ids.Count == 0)
            return;

        var now = _clock.GetUtcNow().UtcDateTime;

        var children = await _db.ChildProfiles
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(ct);

        foreach (var child in children)
        {
            // Yuxu rejimi bildirişi SAXLAYIR — uşağı gecə oyatmaq olmaz.
            if (!NotificationSchedule.CanSendToChild(child, now, _options.RespectBedtime))
                continue;

            var tokens = await _db.DeviceTokens
                .Where(d => d.ChildProfileId == child.Id)
                .Select(d => d.Token)
                .ToListAsync(ct);

            var title = Localized.Pick(child.LanguageCode, missionTitleEn, missionTitleAz);

            var message = new PushMessage(
                Localized.T(child.LanguageCode, "Komanda missiyası tamamlandı! 🎉", "Team mission complete! 🎉"),
                Localized.T(child.LanguageCode,
                    $"«{title}» bitdi — mükafatın səni gözləyir.",
                    $"\"{title}\" is done — your reward is waiting."),
                Route: "/friends");

            await SendAsync(tokens, message, ct);
        }
    }

    /// <summary>
    /// Göndərir və işləməyən tokenləri təmizləyir. Xəta çağıran əməliyyatı
    /// RİSKƏ ATMIR: bildiriş getməsə də dostluq sorğusu yaranıb, missiya bitib.
    /// </summary>
    private async Task SendAsync(List<string> tokens, PushMessage message, CancellationToken ct)
    {
        if (tokens.Count == 0)
            return;

        try
        {
            var stale = await _sender.SendAsync(tokens, message, ct);

            if (stale.Count == 0)
                return;

            var dead = await _db.DeviceTokens.Where(d => stale.Contains(d.Token)).ToListAsync(ct);
            _db.DeviceTokens.RemoveRange(dead);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Bildiriş göndərilmədi.");
        }
    }
}
