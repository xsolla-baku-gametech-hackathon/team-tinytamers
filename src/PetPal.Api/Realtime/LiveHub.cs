using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PetPal.Api.Data;
using PetPal.Api.Security;
using PetPal.Shared.Enums;

namespace PetPal.Api.Realtime;

/// <summary>
/// App-in TƏK real vaxt kanalı — arena, dostlar və komanda missiyaları.
///
/// <para>Bir kanal seçilib, iki yox: qrup onsuz da uşaq başınadır və mobil
/// klientdə ikinci websocket saxlamaq batareya ilə ödənilir. Əvvəllər bu tip
/// <c>ArenaHub</c> adlanırdı; ad yalnız arena üçün dar gəldi.</para>
///
/// <para><b>Kanal itsə heç nə sınmır.</b> Bütün məlumat adi HTTP endpoint-lərindən
/// də alınır: arena ekranı dueli özü yoxlayır, dostlar siyahısı yenilənəndə
/// presence yenidən oxunur. Kanal sadəcə həmin məlumatı TEZ çatdırır.</para>
/// </summary>
[Authorize(Policy = AuthorizationPolicies.Child)]
public class LiveHub : Hub
{
    private readonly AppDbContext _db;
    private readonly IPresenceTracker _presence;

    public LiveHub(AppDbContext db, IPresenceTracker presence)
    {
        _db = db;
        _presence = presence;
    }

    /// <summary>Hər uşaq öz qrupundadır — bir uşağın bir neçə cihazı ola bilər.</summary>
    public static string GroupFor(Guid childId) => $"live-{childId}";

    public override async Task OnConnectedAsync()
    {
        var childId = Context.User?.GetChildId();

        // Token uşaq sessiyası deyilsə kanal açılmır: mesajlar uşağa aiddir.
        if (childId is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(childId.Value));

        // Yalnız BİRİNCİ cihaz qoşulanda dostlara xəbər gedir — uşaq telefonu və
        // planşeti eyni anda açanda dost siyahısı iki dəfə yanıb-sönməməlidir.
        if (_presence.Connect(childId.Value, Context.ConnectionId))
            await NotifyFriendsAsync(childId.Value, online: true);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var childId = Context.User?.GetChildId();

        if (childId is not null && _presence.Disconnect(childId.Value, Context.ConnectionId))
            await NotifyFriendsAsync(childId.Value, online: false);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Dostlara «filankəs onlayn oldu/çıxdı» xəbəri. Yalnız TƏSDİQLƏNMİŞ
    /// dostlara gedir: gözləyən sorğu hələ dostluq deyil, presence isə
    /// şəxsi məlumatdır.
    /// </summary>
    private async Task NotifyFriendsAsync(Guid childId, bool online)
    {
        var friendIds = await _db.Friendships
            .AsNoTracking()
            .Where(f => f.FriendChildProfileId == childId && f.Status == FriendshipStatus.Active)
            .Select(f => f.ChildProfileId)
            .ToListAsync();

        foreach (var friendId in friendIds)
            await Clients.Group(GroupFor(friendId)).SendAsync("FriendPresence", childId, online);
    }
}
