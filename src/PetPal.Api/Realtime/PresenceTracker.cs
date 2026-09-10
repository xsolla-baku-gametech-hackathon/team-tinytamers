using System.Collections.Concurrent;

namespace PetPal.Api.Realtime;

/// <summary>
/// <see cref="IPresenceTracker"/>-in yaddaş tətbiqi. Singleton qeydiyyatdan keçir.
///
/// <para>Uşaq başına qoşulma İD-ləri saxlanılır, sadəcə sayğac yox: şəbəkə
/// qopanda SignalR bəzən qopma xəbərini gec verir və eyni cihaz yenidən
/// qoşulur — sayğac belə hallarda sürüşüb uşağı əbədi «onlayn» saxlayardı.</para>
/// </summary>
public class PresenceTracker : IPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();

    public bool Connect(Guid childId, string connectionId)
    {
        var set = _connections.GetOrAdd(childId, _ => new HashSet<string>());

        lock (set)
        {
            var wasEmpty = set.Count == 0;
            set.Add(connectionId);
            return wasEmpty;
        }
    }

    public bool Disconnect(Guid childId, string connectionId)
    {
        if (!_connections.TryGetValue(childId, out var set))
            return false;

        lock (set)
        {
            set.Remove(connectionId);

            if (set.Count > 0)
                return false;

            // Boş dəsti lüğətdə saxlamaq mənasızdır — uşaq profilləri çoxdur.
            _connections.TryRemove(childId, out _);
            return true;
        }
    }

    public bool IsOnline(Guid childId)
    {
        if (!_connections.TryGetValue(childId, out var set))
            return false;

        lock (set)
            return set.Count > 0;
    }

    public IReadOnlySet<Guid> OnlineAmong(IEnumerable<Guid> childIds)
    {
        var online = new HashSet<Guid>();

        foreach (var id in childIds)
        {
            if (IsOnline(id))
                online.Add(id);
        }

        return online;
    }
}
