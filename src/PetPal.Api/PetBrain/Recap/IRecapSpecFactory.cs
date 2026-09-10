using Microsoft.EntityFrameworkCore;
using PetPal.Api.Data;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Recap;

/// <summary>
/// Tamamlanmış run-dan recap təsvirini QURAN qat.
///
/// <para>Ayrıca müqavilədir, çünki iki yerdən lazımdır: tamamlama axını və
/// açılış süpürgəsi (yenidən başlatmadan sonra). Təsvir bazada saxlanmır —
/// orada uşağa aid sahələr olardı; onun əvəzinə hər dəfə serverin öz
/// vəziyyətindən yenidən qurulur və hash-ı eyni çıxır.</para>
/// </summary>
public interface IRecapSpecFactory
{
    Task<AdventureRecapSpec?> BuildAsync(Guid runId, CancellationToken ct = default);
}

public sealed class RecapSpecFactory : IRecapSpecFactory
{
    private readonly AppDbContext _db;

    public RecapSpecFactory(AppDbContext db) => _db = db;

    public async Task<AdventureRecapSpec?> BuildAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _db.ExperienceRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, ct);

        if (run is null || ExperienceCatalog.Find(run.TemplateKey) is not { } template)
            return null;

        var child = await _db.ChildProfiles
            .AsNoTracking()
            .Include(c => c.Pet)
            .FirstOrDefaultAsync(c => c.Id == run.ChildProfileId, ct);

        if (child?.Pet is null)
            return null;

        // Tapmaca səhnəsi videonun ilk kadrıdır — hash onu bağlayır.
        var puzzle = await _db.IssuedPuzzles
            .AsNoTracking()
            .Where(p => p.ExperienceRunId == runId && p.Status == PetBrainPuzzleStatus.Solved)
            .OrderBy(p => p.StageIndex)
            .Select(p => new { p.SceneSpecHash, p.Mechanic })
            .FirstOrDefaultAsync(ct);

        // Budaqlanan macərada xülasə REAL nəticə sətirlərindən qurulur; xətti
        // macərada isə köhnə (indeks əsaslı) yol saxlanılır, çünki miqrasiya
        // olunmamış şablonlar da işləməyə davam etməlidir.
        var outcomes = await _db.RunStageOutcomes
            .AsNoTracking()
            .Where(o => o.ExperienceRunId == runId)
            .OrderBy(o => o.StageOrdinal)
            .ToListAsync(ct);

        if (outcomes.Count > 0)
            return AdventureRecapSpec.ForGraph(
                run,
                template,
                child.Pet,
                child.LanguageCode,
                puzzle?.SceneSpecHash ?? string.Empty,
                puzzle is null ? "none" : puzzle.Mechanic.ToString(),
                outcomes);

        return AdventureRecapSpec.For(
            run,
            template,
            child.Pet,
            child.LanguageCode,
            puzzle?.SceneSpecHash ?? string.Empty,
            puzzle is null ? "none" : puzzle.Mechanic.ToString());
    }
}
