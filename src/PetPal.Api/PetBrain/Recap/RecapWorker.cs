using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using PetPal.Api.Data;
using PetPal.Api.PetBrain.Media;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Recap;

/// <summary>
/// Gözləyən recap işlərinin növbəsi.
///
/// <para>Broker əlavə edilmir — prototip üçün qəsdən sadədir. Növbə
/// prosesdaxilidir, <b>həqiqi vəziyyət isə bazadadır</b>: sətir <c>Pending</c>
/// və ya <c>Generating</c> deyilsə işçi heç nə etmir.</para>
/// </summary>
public sealed class RecapQueue
{
    private readonly Channel<AdventureRecapSpec> _channel =
        Channel.CreateBounded<AdventureRecapSpec>(new BoundedChannelOptions(128)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private readonly ConcurrentDictionary<string, byte> _queued = new(StringComparer.Ordinal);

    public void Enqueue(AdventureRecapSpec spec)
    {
        var hash = spec.Hash();

        if (!_queued.TryAdd(hash, 0))
            return;

        if (!_channel.Writer.TryWrite(spec))
            _queued.TryRemove(hash, out _);
    }

    public IAsyncEnumerable<AdventureRecapSpec> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);

    public void Release(string hash) => _queued.TryRemove(hash, out _);
}

/// <summary>
/// Recap videolarını arxa fonda hazırlayan işçi.
///
/// <para><b>Yenidən başlatma işi itirmir.</b> Açılışda baza süpürülür: yarımçıq
/// (<c>Pending</c>/<c>Generating</c>) sətirlər növbəyə qayıdır və
/// <c>Generating</c> olanlar SAXLANMIŞ tapşırıq id-si ilə davam edir — yəni
/// provayderə ikinci dəfə pul verilmir.</para>
///
/// <para>Uşaq bu qatı heç vaxt gözləmir: mükafat artıq verilib, ekranda isə
/// tam 10 saniyəlik deterministik storyboard oynayır.</para>
/// </summary>
public sealed class RecapWorker : BackgroundService
{
    private readonly RecapQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeSpan _pollDelay;
    private readonly ILogger<RecapWorker> _logger;

    public RecapWorker(
        RecapQueue queue,
        IServiceScopeFactory scopes,
        IOptions<PetBrainMediaOptions> options,
        ILogger<RecapWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;

        // Aşağı hədd sıx dövrənin qarşısını alır.
        _pollDelay = TimeSpan.FromMilliseconds(Math.Max(50, options.Value.RecapPollMilliseconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ResumeAsync(stoppingToken);

        await foreach (var spec in _queue.ReadAllAsync(stoppingToken))
        {
            var hash = spec.Hash();

            try
            {
                await DriveAsync(spec, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // İşçi bir recap-a görə DAYANMIR.
                _logger.LogWarning(ex, "PetBrain recap: işçi xəta ilə qarşılaşdı ({Hash}).", hash);
            }
            finally
            {
                _queue.Release(hash);
            }
        }
    }

    /// <summary>
    /// İşi sona qədər aparır: başlat → izlə → bitir.
    ///
    /// <para>Hər addım AYRICA scope-dadır, yəni baza bağlantısı model
    /// gözləyərkən tutulmur.</para>
    /// </summary>
    private async Task DriveAsync(AdventureRecapSpec spec, CancellationToken ct)
    {
        var hash = spec.Hash();

        while (!ct.IsCancellationRequested)
        {
            using var scope = _scopes.CreateScope();

            var coordinator = scope.ServiceProvider.GetRequiredService<RecapCoordinator>();
            await coordinator.AdvanceAsync(spec, ct);

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var state = await db.AdventureRecaps
                .AsNoTracking()
                .Where(r => r.RecapSpecHash == hash)
                .Select(r => new { r.Status, r.FailureReason })
                .FirstOrDefaultAsync(ct);

            // Yekunlaşıbsa dövrədən çıxırıq.
            if (state?.Status is not (PetBrainRecapStatus.Pending or PetBrainRecapStatus.Generating))
                return;

            if (state is { Status: PetBrainRecapStatus.Pending, FailureReason: RecapCoordinator.AwaitingScene })
            {
                _ = RequeueLaterAsync(spec, ct);
                return;
            }

            await Task.Delay(_pollDelay, ct);
        }
    }

    /// <summary>
    /// İlk kadrını gözləyən recap-ı bir azdan növbəyə QAYTARIR.
    ///
    /// <para>İşçi növbəni bir-bir emal edir. Gözləyən recap dövrədə qalsaydı,
    /// rəsm çəkilənə qədər başqa uşaqların videoları da gözləyərdi.</para>
    /// </summary>
    private async Task RequeueLaterAsync(AdventureRecapSpec spec, CancellationToken ct)
    {
        try
        {
            await Task.Delay(_pollDelay, ct);
            _queue.Enqueue(spec);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Açılış süpürgəsi — prosesin yenidən başlaması işi itirmir.
    ///
    /// <para>Təsvir bazada saxlanılmır (orada uşağa aid sahələr olardı), ona
    /// görə yarımçıq sətirlər <b>run-dan yenidən qurulur</b>. Tapşırıq id-si
    /// isə sətirdədir, yəni <c>Generating</c> iş davam etdirilir, yenidən
    /// başladılmır.</para>
    /// </summary>
    private async Task ResumeAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pending = await db.AdventureRecaps
                .AsNoTracking()
                .Where(r => r.Status == PetBrainRecapStatus.Pending
                            || r.Status == PetBrainRecapStatus.Generating)
                .Select(r => r.ExperienceRunId)
                .Take(32)
                .ToListAsync(ct);

            if (pending.Count == 0)
                return;

            var builder = scope.ServiceProvider.GetRequiredService<IRecapSpecFactory>();

            foreach (var runId in pending)
            {
                if (await builder.BuildAsync(runId, ct) is { } spec)
                    _queue.Enqueue(spec);
            }

            _logger.LogInformation("PetBrain recap: {Count} yarımçıq iş bərpa edildi.", pending.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Süpürgə uğursuz olsa da app qalxmalıdır: recap onsuz da
            // deterministik ehtiyatla işləyir.
            _logger.LogWarning(ex, "PetBrain recap: açılış süpürgəsi alınmadı.");
        }
    }
}
