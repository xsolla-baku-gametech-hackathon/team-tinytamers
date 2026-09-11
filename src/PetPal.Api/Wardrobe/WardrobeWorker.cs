using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using PetPal.Api.Data;
using PetPal.Shared.Enums;

namespace PetPal.Api.Wardrobe;

/// <summary>
/// Gözləyən dizaynların növbəsi.
///
/// <para>Prosesdaxilidir və qəsdən sadədir — HƏQİQİ iş bazadakı sətirdir.
/// Növbə itsə (yenidən başlatma) sətir <c>Pending</c> qalır və işçi başlayanda
/// onu yenidən növbəyə qoyur.</para>
/// </summary>
public sealed class WardrobeQueue
{
    private readonly Channel<Guid> _channel =
        Channel.CreateBounded<Guid>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private readonly ConcurrentDictionary<Guid, byte> _queued = new();

    public void Enqueue(Guid designId)
    {
        if (!_queued.TryAdd(designId, 0))
            return;

        if (!_channel.Writer.TryWrite(designId))
            _queued.TryRemove(designId, out _);
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);

    public void Release(Guid designId) => _queued.TryRemove(designId, out _);
}

/// <summary>
/// Dizaynları arxa fonda çəkdirən işçi.
///
/// <para>Uşağın sorğusu modeli HEÇ VAXT gözləmir: dizayn dərhal
/// <c>Pending</c> qayıdır, ekran isə vəziyyəti seyrək soruşur. İşçi dizaynları
/// bir-bir çəkir — eyni anda iki pullu sorğu getmir.</para>
/// </summary>
public sealed class WardrobeWorker : BackgroundService
{
    private readonly WardrobeQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<WardrobeWorker> _logger;

    public WardrobeWorker(WardrobeQueue queue, IServiceScopeFactory scopes, ILogger<WardrobeWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingAsync(stoppingToken);

        await foreach (var designId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();

                await scope.ServiceProvider
                    .GetRequiredService<WardrobeRenderer>()
                    .RenderAsync(designId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Wardrobe: dizayn işçisi xəta ilə qarşılaşdı ({DesignId}).", designId);
            }
            finally
            {
                _queue.Release(designId);
            }
        }
    }

    /// <summary>Yenidən başlatmadan sonra yarımçıq qalmış dizaynları qaytarır.</summary>
    private async Task RequeuePendingAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pending = await db.WardrobeDesigns
                .AsNoTracking()
                .Where(d => d.Status == WardrobeDesignStatus.Pending)
                .OrderBy(d => d.CreatedAt)
                .Select(d => d.Id)
                .Take(100)
                .ToListAsync(ct);

            foreach (var designId in pending)
                _queue.Enqueue(designId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Wardrobe: gözləyən dizaynlar növbəyə qaytarılmadı.");
        }
    }
}
