using Microsoft.AspNetCore.SignalR;

namespace PetPal.Api.Realtime;

/// <summary>
/// App-in real vaxt xəbərləri. Servislər SignalR tiplərini birbaşa görməsin
/// deyə dar interfeys arxasındadır — məntiq nəqliyyatdan asılı olmamalıdır.
/// </summary>
public interface ILiveNotifier
{
    // ---------- Arena ----------

    /// <summary>Gözləyən duelə rəqib qoşuldu.</summary>
    Task DuelMatchedAsync(Guid childId, Guid duelId, CancellationToken ct = default);

    /// <summary>Rəqib növbəti sualı cavabladı.</summary>
    Task OpponentProgressAsync(Guid childId, Guid duelId, int answered, int total, CancellationToken ct = default);

    /// <summary>Duel bağlandı — nəticə hazırdır.</summary>
    Task DuelCompletedAsync(IEnumerable<Guid> childIds, Guid duelId, CancellationToken ct = default);

    /// <summary>Dost səni birbaşa yarışa çağırdı.</summary>
    Task DuelChallengedAsync(Guid childId, Guid duelId, string fromName, CancellationToken ct = default);

    /// <summary>Çağırış geri götürüldü və ya vaxtı bitdi.</summary>
    Task ChallengeCancelledAsync(Guid childId, Guid duelId, CancellationToken ct = default);

    // ---------- Dostlar ----------

    /// <summary>Dost siyahısı dəyişdi — sorğu təsdiqləndi, imtina edildi və ya dost silindi.</summary>
    Task FriendsChangedAsync(IEnumerable<Guid> childIds, CancellationToken ct = default);

    // ---------- Komanda missiyaları ----------

    /// <summary>Komanda missiyasına dəvət olundun.</summary>
    Task TeamInvitedAsync(Guid childId, Guid missionId, CancellationToken ct = default);

    /// <summary>Komanda missiyasının ümumi irəliləyişi dəyişdi.</summary>
    Task TeamProgressAsync(IEnumerable<Guid> childIds, Guid missionId, int progress, int target, CancellationToken ct = default);

    /// <summary>Komanda missiyası tamamlandı.</summary>
    Task TeamCompletedAsync(IEnumerable<Guid> childIds, Guid missionId, CancellationToken ct = default);
}

public class LiveNotifier : ILiveNotifier
{
    private readonly IHubContext<LiveHub> _hub;
    private readonly ILogger<LiveNotifier> _logger;

    public LiveNotifier(IHubContext<LiveHub> hub, ILogger<LiveNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public Task DuelMatchedAsync(Guid childId, Guid duelId, CancellationToken ct = default) =>
        SendAsync(childId, "DuelMatched", [duelId], ct);

    public Task OpponentProgressAsync(Guid childId, Guid duelId, int answered, int total, CancellationToken ct = default) =>
        SendAsync(childId, "OpponentProgress", [duelId, answered, total], ct);

    public Task DuelCompletedAsync(IEnumerable<Guid> childIds, Guid duelId, CancellationToken ct = default) =>
        SendManyAsync(childIds, "DuelCompleted", [duelId], ct);

    public Task DuelChallengedAsync(Guid childId, Guid duelId, string fromName, CancellationToken ct = default) =>
        SendAsync(childId, "DuelChallenged", [duelId, fromName], ct);

    public Task ChallengeCancelledAsync(Guid childId, Guid duelId, CancellationToken ct = default) =>
        SendAsync(childId, "ChallengeCancelled", [duelId], ct);

    public Task FriendsChangedAsync(IEnumerable<Guid> childIds, CancellationToken ct = default) =>
        SendManyAsync(childIds, "FriendsChanged", [], ct);

    public Task TeamInvitedAsync(Guid childId, Guid missionId, CancellationToken ct = default) =>
        SendAsync(childId, "TeamInvited", [missionId], ct);

    public Task TeamProgressAsync(IEnumerable<Guid> childIds, Guid missionId, int progress, int target, CancellationToken ct = default) =>
        SendManyAsync(childIds, "TeamProgress", [missionId, progress, target], ct);

    public Task TeamCompletedAsync(IEnumerable<Guid> childIds, Guid missionId, CancellationToken ct = default) =>
        SendManyAsync(childIds, "TeamCompleted", [missionId], ct);

    private async Task SendManyAsync(IEnumerable<Guid> childIds, string method, object?[] args, CancellationToken ct)
    {
        foreach (var childId in childIds)
            await SendAsync(childId, method, args, ct);
    }

    /// <summary>
    /// Xəbər göndərmək əməliyyatın özünü RİSKƏ ATMAMALIDIR: uşaq cavabını verib,
    /// nəticə bazada yazılıb — kanal işləmirsə bu, sorğunu uğursuz etməməlidir.
    /// Klient eyni məlumatı adi endpoint-dən onsuz da alır.
    /// </summary>
    private async Task SendAsync(Guid childId, string method, object?[] args, CancellationToken ct)
    {
        try
        {
            await _hub.Clients.Group(LiveHub.GroupFor(childId)).SendCoreAsync(method, args, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Real vaxt xəbəri göndərilmədi: {Method} → {ChildId}", method, childId);
        }
    }
}
