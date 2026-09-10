using Microsoft.AspNetCore.SignalR.Client;

namespace PetPal.App.Ui.Services;

/// <summary>
/// App-in TƏK real vaxt kanalı (SignalR): arena, dostların onlayn vəziyyəti
/// və komanda missiyaları eyni bağlantıdan gəlir.
///
/// <para>Bir kanal seçilib, iki yox: mobil cihazda hər websocket batareya ilə
/// ödənilir, server tərəfdə isə qrup onsuz da uşaq başınadır.</para>
///
/// <para><b>Qoşula bilməsə heç nə sınmır.</b> Xəta udulur və app real vaxt
/// yeniləmələri olmadan işləyir — bütün məlumat adi endpoint-lərdən də gəlir
/// (arena ekranı dueli özü yoxlayır, dostlar siyahısı yenidən oxunur).
/// Bu, qəsdəndir: uşaq ekranında şəbəkə xətası görünməməlidir.</para>
/// </summary>
public class LiveClient : IAsyncDisposable
{
    private readonly AppSession _session;
    private readonly Uri _apiBaseAddress;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private HubConnection? _connection;

    /// <summary>Kanal hazırda hansı uşaq üçün açıqdır — profil dəyişəndə yenidən qurulur.</summary>
    private Guid? _connectedChildId;

    public LiveClient(AppSession session, Uri apiBaseAddress)
    {
        _session = session;
        _apiBaseAddress = apiBaseAddress;
    }

    // ---------- Arena ----------

    /// <summary>Gözləyən duelə rəqib qoşuldu.</summary>
    public event Action<Guid>? DuelMatched;

    /// <summary>Rəqib növbəti sualı cavabladı: duel, cavablanan say, ümumi say.</summary>
    public event Action<Guid, int, int>? OpponentProgress;

    /// <summary>Duel bağlandı — nəticə hazırdır.</summary>
    public event Action<Guid>? DuelCompleted;

    /// <summary>Dost səni birbaşa çağırdı: duel və çağıranın adı.</summary>
    public event Action<Guid, string>? DuelChallenged;

    /// <summary>Çağırış geri götürüldü və ya vaxtı bitdi.</summary>
    public event Action<Guid>? ChallengeCancelled;

    // ---------- Dostlar ----------

    /// <summary>Dostun onlayn vəziyyəti dəyişdi: dostun profili və vəziyyət.</summary>
    public event Action<Guid, bool>? FriendPresence;

    /// <summary>Dost siyahısı dəyişdi — sorğu təsdiqləndi, imtina edildi və ya dost silindi.</summary>
    public event Action? FriendsChanged;

    // ---------- Komanda missiyaları ----------

    /// <summary>Komanda missiyasına dəvət olundun.</summary>
    public event Action<Guid>? TeamInvited;

    /// <summary>Komanda missiyasının irəliləyişi: missiya, irəliləyiş, hədəf.</summary>
    public event Action<Guid, int, int>? TeamProgress;

    /// <summary>Komanda missiyası tamamlandı.</summary>
    public event Action<Guid>? TeamCompleted;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Kanalı açır (artıq açıqdırsa heç nə etmir). Uşaq profili dəyişibsə
    /// köhnə bağlantı bağlanır — mesajlar yalnız aktiv uşağa aid olmalıdır.
    /// </summary>
    public async Task EnsureStartedAsync()
    {
        var childId = _session.ActiveChildId;
        if (childId is null)
            return;

        await _gate.WaitAsync();

        try
        {
            if (_connection is not null && _connectedChildId == childId)
            {
                if (_connection.State == HubConnectionState.Disconnected)
                    await TryStartAsync();

                return;
            }

            await DisposeConnectionAsync();

            _connection = new HubConnectionBuilder()
                .WithUrl(new Uri(_apiBaseAddress, "hubs/live"), options =>
                {
                    // Brauzerin WebSocket-i başlıq daşımır — token sorğu sətri ilə gedir.
                    options.AccessTokenProvider = () => Task.FromResult(_session.CurrentAccessToken);
                })
                .WithAutomaticReconnect()
                .Build();

            _connection.On<Guid>("DuelMatched", duelId => DuelMatched?.Invoke(duelId));
            _connection.On<Guid, int, int>("OpponentProgress",
                (duelId, answered, total) => OpponentProgress?.Invoke(duelId, answered, total));
            _connection.On<Guid>("DuelCompleted", duelId => DuelCompleted?.Invoke(duelId));
            _connection.On<Guid, string>("DuelChallenged",
                (duelId, fromName) => DuelChallenged?.Invoke(duelId, fromName));
            _connection.On<Guid>("ChallengeCancelled", duelId => ChallengeCancelled?.Invoke(duelId));

            _connection.On<Guid, bool>("FriendPresence",
                (friendId, online) => FriendPresence?.Invoke(friendId, online));
            _connection.On("FriendsChanged", () => FriendsChanged?.Invoke());

            _connection.On<Guid>("TeamInvited", missionId => TeamInvited?.Invoke(missionId));
            _connection.On<Guid, int, int>("TeamProgress",
                (missionId, progress, target) => TeamProgress?.Invoke(missionId, progress, target));
            _connection.On<Guid>("TeamCompleted", missionId => TeamCompleted?.Invoke(missionId));

            _connectedChildId = childId;
            await TryStartAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeConnectionAsync();
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task TryStartAsync()
    {
        try
        {
            await _connection!.StartAsync();
        }
        catch (Exception)
        {
            // Kanal açılmadı: app real vaxt yeniləmələri olmadan işləməyə davam edir.
        }
    }

    private async Task DisposeConnectionAsync()
    {
        if (_connection is null)
            return;

        try
        {
            await _connection.DisposeAsync();
        }
        catch (Exception)
        {
            // Bağlantı onsuz da qırılıb.
        }

        _connection = null;
        _connectedChildId = null;
    }
}
