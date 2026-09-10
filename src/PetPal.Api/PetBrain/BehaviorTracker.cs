using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Davranış hadisələrini yazır və profili TƏDRİCƏN yeniləyir.
///
/// <para>Üç qoruyucu var və hər biri ölçülmüş bir riskə cavabdır:</para>
/// <list type="number">
///   <item><b>İdempotentlik.</b> Eyni açarlı hadisə iki dəfə sayılmır. Yaddaqalan
///   yoxlama ilk müdafiədir; əsl zəmanət isə bazadakı unikal indeksdir — iki
///   eyni vaxtlı sorğuda ikincisi yazma anında dayanır.</item>
///   <item><b>Gündəlik tavan.</b> Bir açar bir gündə
///   <see cref="ProfileLearningRules.DailyGainCapPerKey"/> baldan çox qazana
///   bilmir — uşaq eyni macərəni dövrə vurub profili şişirdə bilməsin. Sayğac
///   BAZADADIR (<see cref="TraitDailyLedger"/>), yəni tavan ayrı sorğularda və
///   paralel sorğularda da eyni tavandır.</item>
///   <item><b>Qapalı taksonomiya.</b> Naməlum açar səssizcə buraxılır; uydurma
///   xassə heç vaxt yaranmır.</item>
/// </list>
/// </summary>
public class BehaviorTracker : IBehaviorTracker
{
    private const int MaxSourceLength = 60;
    private const int MaxDetailLength = 200;
    private const int MaxIdempotencyKeyLength = 120;

    private readonly AppDbContext _db;
    private readonly TraitDailyLedger _ledger;
    private readonly TimeProvider _clock;
    private readonly PetBrainOptions _options;

    public BehaviorTracker(
        AppDbContext db, TraitDailyLedger ledger, TimeProvider clock, IOptions<PetBrainOptions> options)
    {
        _db = db;
        _ledger = ledger;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<bool> TrackAsync(
        Guid childId,
        PetBrainEventType type,
        PetBrainEventData data,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return false;

        var now = _clock.GetUtcNow().UtcDateTime;
        var key = Truncate(idempotencyKey, MaxIdempotencyKeyLength);

        if (key is not null && await AlreadyTrackedAsync(childId, key, ct))
            return false;

        _db.BehaviorEvents.Add(new BehaviorEvent
        {
            ChildProfileId = childId,
            Type = type,
            Source = Truncate(data.Source, MaxSourceLength) ?? string.Empty,
            Detail = Truncate(data.Detail, MaxDetailLength) ?? string.Empty,
            OccurredAt = now,
            IdempotencyKey = key
        });

        if (data.Adjustments.Count > 0)
            await ApplyAdjustmentsAsync(childId, data.Adjustments, now, ct);

        return true;
    }

    /// <summary>
    /// Xassələri yeniləyir. Sətir yoxdursa yaradılır — bütün açarları
    /// qeydiyyat anında doldurmaq lazım gəlmir.
    /// </summary>
    private async Task ApplyAdjustmentsAsync(
        Guid childId,
        IReadOnlyList<TraitAdjustment> adjustments,
        DateTime now,
        CancellationToken ct)
    {
        var keys = adjustments.Select(a => a.Key).Distinct().ToList();

        // Dəyişiklik gözlədiyimiz sətirlər izlənilməlidir (AsNoTracking YOX).
        var existing = await _db.PlayerTraits
            .Where(t => t.ChildProfileId == childId && keys.Contains(t.Key))
            .ToListAsync(ct);

        // Bir SORĞUDA bir neçə hadisə izlənə bilər (tamamlama + seçim + tapmaca)
        // və hamısı eyni `SaveChanges`-i gözləyir. Yuxarıdakı sorğu yalnız
        // BAZADAKI sətirləri görür, ona görə əvvəlki hadisənin ƏLAVƏ ETDİYİ,
        // amma hələ yazılmamış sətir görünməz qalırdı — nəticədə eyni açar üçün
        // ikinci sətir yaradılır və unikal indeks yazma anında pozulurdu.
        //
        // Ona görə dəyişiklik izləyicisindəki yerli sətirlər də birləşdirilir.
        foreach (var local in _db.PlayerTraits.Local)
        {
            if (local.ChildProfileId == childId
                && keys.Contains(local.Key)
                && !existing.Any(t => ReferenceEquals(t, local)))
                existing.Add(local);
        }

        foreach (var adjustment in adjustments)
        {
            if (!TraitKeys.IsKnown(adjustment.Category, adjustment.Key))
                continue;

            var trait = existing.FirstOrDefault(t =>
                t.Key == adjustment.Key && t.Category == adjustment.Category);

            if (trait is null)
            {
                trait = new PlayerTrait
                {
                    ChildProfileId = childId,
                    Category = adjustment.Category,
                    Key = adjustment.Key,
                    Score = TraitKeys.StartingScore,
                    UpdatedAt = now
                };

                _db.PlayerTraits.Add(trait);
                existing.Add(trait);
            }

            var delta = adjustment.Delta;

            // Gündəlik tavan yalnız ARTIMA aiddir: azalma nadir hadisədir
            // (mənalı yarımçıq qoyma) və onu məhdudlaşdırmağa ehtiyac yoxdur.
            if (delta > 0)
            {
                delta = await _ledger.ReserveAsync(
                    childId,
                    adjustment.Category,
                    adjustment.Key,
                    now,
                    delta,
                    ProfileLearningRules.DailyGainCapPerKey,
                    ct);

                // Tavan dolub: bal ARTMIR, amma müşahidənin özü itmir —
                // uşaq bunu yenə seçdi və bu, sübutdur.
                if (delta == 0)
                {
                    TraitEvidence.Record(trait, adjustment.Source, 0, now);
                    trait.UpdatedAt = now;
                    continue;
                }
            }

            trait.Score = TraitKeys.Clamp(trait.Score + delta);

            // Balın yanında SÜBUT da yazılır: neçə müşahidə, uşağın öz seçimi
            // idimi, hansı mənbədən. Bal «nə qədər», sübut «nə dərəcədə
            // əminik» sualına cavab verir (bax TraitEvidence).
            TraitEvidence.Record(trait, adjustment.Source, delta, now);

            trait.UpdatedAt = now;
        }
    }

    private Task<bool> AlreadyTrackedAsync(Guid childId, string key, CancellationToken ct) =>
        _db.BehaviorEvents.AnyAsync(
            e => e.ChildProfileId == childId && e.IdempotencyKey == key, ct);

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= max ? value : value[..max];
}
