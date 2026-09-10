using Microsoft.EntityFrameworkCore;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Valideynin yaddaş üzərində nəzarəti.
///
/// <para><b>Nə üçün lazımdır?</b> Pet uşaq haqqında bir şey «öyrənir» və onu
/// aylarla saxlayır. Valideyn bunu GÖRƏ və LƏĞV EDƏ bilməlidir — əks halda
/// yaddaş uşağın nəzarətindən kənar, davamlı bir profil olur.</para>
///
/// <para>İki ayrı əməliyyat var və fərq qəsdəndir: bir faktı unutmaq nadir,
/// hədəflənmiş hərəkətdir; hamısını sıfırlamaq isə «təmiz başlanğıc»dır.
/// İkisini bir düymədə birləşdirmək təsadüfi tam silinməyə yol açardı.</para>
///
/// <para>Silinən sətir GERİ QAYTARILMIR: bu, gizlilik əməliyyatıdır və
/// «arxivdə qalsın» davranışı onun mənasını pozardı.</para>
/// </summary>
public sealed class PetMemoryAdmin
{
    private readonly AppDbContext _db;

    public PetMemoryAdmin(AppDbContext db) => _db = db;

    /// <summary>
    /// Uşağın bütün xatirələri — valideyn üçün, cümlə şəklində.
    ///
    /// <para>Açar deyil, uşağın gördüyü CÜMLƏ göstərilir: valideyn pet-in nə
    /// dediyini oxumalıdır, daxili taksonomiyanı yox.</para>
    /// </summary>
    /// <returns>Yad uşaq üçün <c>null</c> — «var, amma sənin deyil» də sızmadır.</returns>
    public async Task<IReadOnlyList<PetBrainMemoryDto>?> ListAsync(
        Guid parentId, Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .AsNoTracking()
            .Include(c => c.Pet)
            .Include(c => c.Memories)
            .FirstOrDefaultAsync(c => c.Id == childId && c.ParentUserId == parentId, ct);

        if (child is null)
            return null;

        var language = child.LanguageCode;

        var petName = child.Pet?.Name ?? "Pet";

        return
        [
            .. child.Memories
                .OrderByDescending(m => m.Tier == PetBrainMemoryTier.Semantic)
                .ThenByDescending(m => m.Importance)
                .ThenByDescending(m => m.CreatedAt)
                .Select(m => MemoryPolicy.ToDto(m, language, petName) with { Id = m.Id })
        ];
    }

    /// <summary>
    /// BİR xatirəni silir. Yad uşağın sətri toxunulmaz qalır — sahiblik
    /// sorğunun içindədir.
    /// </summary>
    /// <returns>Silindisə <c>true</c>; tapılmadısa və ya yad uşaqdırsa <c>false</c>.</returns>
    public async Task<bool> ForgetAsync(
        Guid parentId, Guid childId, Guid memoryId, CancellationToken ct = default)
    {
        if (!await OwnsAsync(parentId, childId, ct))
            return false;

        var removed = await _db.PetMemories
            .Where(m => m.Id == memoryId && m.ChildProfileId == childId)
            .ExecuteDeleteAsync(ct);

        return removed > 0;
    }

    /// <summary>
    /// BÜTÜN xatirələri silir — «təmiz başlanğıc».
    ///
    /// <para>Xassələrə, mükafata, macəra tarixçəsinə TOXUNMUR: valideyn
    /// yaddaşı sıfırlayır, uşağın qazandıqlarını yox.</para>
    /// </summary>
    /// <returns>Neçə sətir silindi; yad uşaq üçün <c>null</c>.</returns>
    public async Task<int?> ResetAsync(Guid parentId, Guid childId, CancellationToken ct = default)
    {
        if (!await OwnsAsync(parentId, childId, ct))
            return null;

        return await _db.PetMemories
            .Where(m => m.ChildProfileId == childId)
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>Sahiblik — valideyn YALNIZ öz uşağının yaddaşına toxuna bilir.</summary>
    private Task<bool> OwnsAsync(Guid parentId, Guid childId, CancellationToken ct) =>
        _db.ChildProfiles.AnyAsync(c => c.Id == childId && c.ParentUserId == parentId, ct);

    /// <summary>
    /// Vaxtı keçmiş xatirələri təmizləyir.
    ///
    /// <para><see cref="Entities.PetMemory.ExpiresAt"/> müvəqqəti faktlar üçün
    /// nəzərdə tutulub; bu, onun HƏQİQƏTƏN tətbiq olunduğu yerdir.</para>
    /// </summary>
    public Task<int> PurgeExpiredAsync(Guid childId, DateTime now, CancellationToken ct = default) =>
        _db.PetMemories
            .Where(m => m.ChildProfileId == childId && m.ExpiresAt != null && m.ExpiresAt <= now)
            .ExecuteDeleteAsync(ct);
}
