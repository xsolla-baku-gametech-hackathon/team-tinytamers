using Microsoft.EntityFrameworkCore;
using PetPal.Api.Data;
using PetPal.Shared.Enums;

namespace PetPal.Api.Learning.Arena;

/// <summary>Bir uşağın həftəlik liqa sətri (profil məlumatı olmadan).</summary>
public record ArenaStandingRow(
    Guid ChildProfileId,
    int Wins,
    int Draws,
    int Losses,
    int CorrectCount,
    int Milliseconds)
{
    public int Duels => Wins + Draws + Losses;
    public int Points => ArenaLeague.Points(Wins, Draws);
}

/// <summary>Uşağın cari həftədəki mövqeyi. <see cref="Rank"/> 0-dırsa, bu həftə duel oynamayıb.</summary>
public record ArenaStandingPosition(int Rank, int Points, int Duels);

/// <summary>
/// Həftəlik liqanın hesablanması. Ayrıca servisdir, çünki onu HƏM arena
/// (cədvəli göstərmək üçün), HƏM də mükafat servisi (liqa nişanı üçün)
/// oxuyur — birbaşa bir-birinə bağlansaydılar dairəvi asılılıq yaranardı.
/// </summary>
public interface IArenaStandings
{
    /// <summary>Cari həftənin cədvəli, sıralanmış: xal → doğru cavab → sürət.</summary>
    Task<List<ArenaStandingRow>> GetCurrentWeekAsync(CancellationToken ct = default);

    /// <summary>Uşağın cari həftədəki yeri.</summary>
    Task<ArenaStandingPosition> GetPositionAsync(Guid childId, CancellationToken ct = default);
}

public class ArenaStandings : IArenaStandings
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public ArenaStandings(AppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<List<ArenaStandingRow>> GetCurrentWeekAsync(CancellationToken ct = default)
    {
        var weekStart = ArenaLeague.WeekStart(_clock.GetUtcNow().UtcDateTime);

        // Yalnız BİTMİŞ və ƏSL duellər sayılır: məşq süni rəqiblədir, yarımçıq
        // duelin isə nəticəsi yoxdur.
        var rows = await _db.DuelEntries
            .AsNoTracking()
            .Where(e => !e.Duel.IsPractice
                        && e.Duel.Status == DuelStatus.Complete
                        && e.FinishedAt != null
                        && e.FinishedAt >= weekStart)
            .GroupBy(e => e.ChildProfileId)
            .Select(g => new ArenaStandingRow(
                g.Key,
                g.Count(e => e.Outcome == DuelOutcome.Win),
                g.Count(e => e.Outcome == DuelOutcome.Draw),
                g.Count(e => e.Outcome == DuelOutcome.Loss),
                g.Sum(e => e.CorrectCount),
                g.Sum(e => e.TotalMilliseconds)))
            .ToListAsync(ct);

        // Sıralama yaddaşdadır: cədvəl bir həftəlik və kiçikdir, bərabərlik
        // qaydası isə (doğru cavab, sonra sürət) SQL-də oxunaqsız olardı.
        return rows
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.CorrectCount)
            .ThenBy(r => r.Milliseconds)
            .ToList();
    }

    public async Task<ArenaStandingPosition> GetPositionAsync(Guid childId, CancellationToken ct = default)
    {
        var rows = await GetCurrentWeekAsync(ct);
        var index = rows.FindIndex(r => r.ChildProfileId == childId);

        return index < 0
            ? new ArenaStandingPosition(0, 0, 0)
            : new ArenaStandingPosition(index + 1, rows[index].Points, rows[index].Duels);
    }
}
