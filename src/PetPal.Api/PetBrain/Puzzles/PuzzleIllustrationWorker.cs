using System.Collections.Concurrent;
using System.Threading.Channels;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// Gözləyən səhnələrin növbəsi.
///
/// <para>Broker əlavə edilmir — bu, prototip üçün qəsdən sadədir. Növbə
/// prosesdaxilidir, <b>həqiqi idempotentlik isə bazadadır</b>: sətir
/// <c>Pending</c> deyilsə işçi heç nə etmir.</para>
///
/// <para>Ona görə növbənin itməsi (yenidən başlatma) məlumat itkisi deyil:
/// sətir bazada <c>Pending</c> qalır və tapmaca növbəti dəfə açılanda yenidən
/// növbəyə düşür. Uşaq bu aralıqda heç nə gözləmir.</para>
/// </summary>
public sealed class PuzzleIllustrationQueue
{
    private readonly Channel<PuzzleSceneSpec> _channel =
        Channel.CreateBounded<PuzzleSceneSpec>(new BoundedChannelOptions(256)
        {
            // Növbə dolsa ƏN KÖHNƏ atılır: uşaq gözləmir, ekranda onsuz da
            // deterministik səhnə var.
            FullMode = BoundedChannelFullMode.DropOldest
        });

    /// <summary>Növbəyə eyni səhnə iki dəfə düşməsin.</summary>
    private readonly ConcurrentDictionary<string, byte> _queued = new(StringComparer.Ordinal);

    public void Enqueue(PuzzleSceneSpec spec)
    {
        var hash = spec.Hash();

        if (!_queued.TryAdd(hash, 0))
            return;

        if (!_channel.Writer.TryWrite(spec))
            _queued.TryRemove(hash, out _);
    }

    public IAsyncEnumerable<PuzzleSceneSpec> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);

    public void Release(string hash) => _queued.TryRemove(hash, out _);
}

/// <summary>
/// Səhnələri arxa fonda çəkdirən işçi.
///
/// <para>Uşağın sorğusu HEÇ VAXT modeli gözləmir: tapmaca dərhal
/// deterministik səhnə ilə açılır, rəsm hazır olanda isə növbəti yenilənmədə
/// eyni həndəsənin altına düşür — toxunuş hədəfləri tərpənmir.</para>
/// </summary>
public sealed class PuzzleIllustrationWorker : BackgroundService
{
    private readonly PuzzleIllustrationQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PuzzleIllustrationWorker> _logger;

    public PuzzleIllustrationWorker(
        PuzzleIllustrationQueue queue,
        IServiceScopeFactory scopes,
        ILogger<PuzzleIllustrationWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var spec in _queue.ReadAllAsync(stoppingToken))
        {
            var hash = spec.Hash();

            try
            {
                using var scope = _scopes.CreateScope();

                var coordinator = scope.ServiceProvider.GetRequiredService<PuzzleIllustrationCoordinator>();
                await coordinator.RenderAsync(hash, spec, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // İşçi bir səhnəyə görə DAYANMIR: qalan səhnələr çəkilməlidir.
                _logger.LogWarning(ex, "PetBrain: səhnə işçisi xəta ilə qarşılaşdı ({Hash}).", hash);
            }
            finally
            {
                _queue.Release(hash);
            }
        }
    }
}
