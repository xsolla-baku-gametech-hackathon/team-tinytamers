using Microsoft.EntityFrameworkCore;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Gündəlik xassə tavanının DAVAMLI sayğacı.
///
/// <para>Tavanın mənası budur: bir uşaq bir açarı bir gündə
/// <see cref="ProfileLearningRules.DailyGainCapPerKey"/> baldan çox qaldıra
/// bilməz. Əvvəlki sayğac sorğu daxilində yaşayan lüğət idi, ona görə ayrı-ayrı
/// HTTP sorğuları limitdən xəbərsiz qalırdı — uşaq eyni macərəni dövrə vurub
/// profili şişirdə bilirdi.</para>
///
/// <para><b>Nə üçün müqayisə-və-yaz (CAS)?</b> «Oxu, hesabla, yaz» ardıcıllığı
/// paralel iki sorğuda hər ikisinə eyni «qalıq» göstərir və hər ikisi yazır —
/// nəticədə tavan iki dəfə xərclənir. Burada isə artım YALNIZ sayğac hələ də
/// oxuduğumuz dəyərdə olduqda tətbiq olunur; uduzan sorğu yenidən oxuyub qalan
/// payı hesablayır.</para>
///
/// <para>Sinif <c>SaveChangesAsync</c> ÇAĞIRMIR: <c>ExecuteUpdateAsync</c> və
/// upsert birbaşa bazaya gedir və çağıranın açıq tranzaksiyası varsa ONUN
/// içində icra olunur. Yəni tamamlama tranzaksiyası geri qayıdanda tavan da
/// geri qayıdır.</para>
/// </summary>
public sealed class TraitDailyLedger
{
    /// <summary>CAS uduzanda neçə dəfə yenidən cəhd edilir.</summary>
    private const int MaxAttempts = 4;

    private readonly AppDbContext _db;

    public TraitDailyLedger(AppDbContext db) => _db = db;

    /// <summary>
    /// Bu gün üçün ən çoxu <paramref name="requested"/> bal AYIRIR və həqiqətən
    /// ayrılan payı qaytarır (0 = gün üçün tavan artıq dolub).
    /// </summary>
    public async Task<int> ReserveAsync(
        Guid childId,
        PetBrainTraitCategory category,
        string traitKey,
        DateTime nowUtc,
        int requested,
        int cap,
        CancellationToken ct)
    {
        if (requested <= 0 || cap <= 0)
            return 0;

        var dayKey = TraitDailyGain.KeyFor(nowUtc);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var gained = await CurrentAsync(childId, category, traitKey, dayKey, ct);

            if (gained is null)
            {
                await InsertIfAbsentAsync(childId, category, traitKey, dayKey, nowUtc, ct);
                gained = 0;
            }

            var remaining = cap - gained.Value;
            if (remaining <= 0)
                return 0;

            var grant = Math.Min(requested, remaining);
            var expected = gained.Value;

            // Şərt sayğacın HƏLƏ DƏ oxuduğumuz dəyərdə olmasıdır: aradan başqa
            // sorğu keçibsə sıfır sətir dəyişir və biz yenidən oxuyuruq.
            var applied = await _db.TraitDailyGains
                .Where(g => g.ChildProfileId == childId
                            && g.Category == category
                            && g.TraitKey == traitKey
                            && g.DayKey == dayKey
                            && g.Gained == expected)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(g => g.Gained, expected + grant)
                    .SetProperty(g => g.UpdatedAt, nowUtc), ct);

            if (applied == 1)
                return grant;
        }

        // Bu qədər yarışdan sonra da yaza bilmiriksə, VERMƏMƏK doğru tərəfdir:
        // tavanın altında qalmaq onu keçməkdən yaxşıdır.
        return 0;
    }

    /// <summary>Bu günün sayğacı; sətir hələ yoxdursa <c>null</c>.</summary>
    public Task<int?> CurrentAsync(
        Guid childId, PetBrainTraitCategory category, string traitKey, int dayKey, CancellationToken ct) =>
        _db.TraitDailyGains
            .AsNoTracking()
            .Where(g => g.ChildProfileId == childId
                        && g.Category == category
                        && g.TraitKey == traitKey
                        && g.DayKey == dayKey)
            .Select(g => (int?)g.Gained)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Sıfır dəyərli sətri yaradır; başqa sorğu qabaqlayıbsa heç nə etmir.
    ///
    /// <para>Sətir dəyişiklik izləyicisi ilə əlavə edilə BİLMƏZ: onda yazma anı
    /// çağıranın <c>SaveChangesAsync</c>-inə qədər gecikərdi və aradakı CAS
    /// hələ mövcud olmayan sətri yeniləməyə çalışardı.</para>
    ///
    /// <para><c>ON CONFLICT DO NOTHING</c> həm PostgreSQL, həm də SQLite
    /// tərəfindən dəstəklənir — yəni produksiya və test eyni yolla gedir.</para>
    /// </summary>
    private Task InsertIfAbsentAsync(
        Guid childId,
        PetBrainTraitCategory category,
        string traitKey,
        int dayKey,
        DateTime nowUtc,
        CancellationToken ct) =>
        _db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "TraitDailyGains" ("Id", "ChildProfileId", "Category", "TraitKey", "DayKey", "Gained", "UpdatedAt")
             VALUES ({Guid.NewGuid()}, {childId}, {(int)category}, {traitKey}, {dayKey}, 0, {nowUtc})
             ON CONFLICT ("ChildProfileId", "Category", "TraitKey", "DayKey") DO NOTHING
             """, ct);
}
