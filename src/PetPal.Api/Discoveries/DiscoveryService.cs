using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Missions;
using PetPal.Api.Rewards;
using PetPal.Shared.Dtos.Discovery;
using PetPal.Shared.Enums;

namespace PetPal.Api.Discoveries;

/// <summary>
/// "Real Life Connect" axını: uşaq ekrandan kənarda nəsə tapır, app-də qeyd edir,
/// pet reaksiya verir və ulduz düşür.
/// </summary>
public class DiscoveryService : IDiscoveryService
{
    private readonly AppDbContext _db;
    private readonly IRewardService _rewards;
    private readonly IMissionProgressTracker _missions;
    private readonly DiscoveryOptions _options;
    private readonly IDiscoveryPhotoStore _photos;
    private readonly TimeProvider _clock;

    public DiscoveryService(
        AppDbContext db,
        IRewardService rewards,
        IMissionProgressTracker missions,
        IOptions<DiscoveryOptions> options,
        IDiscoveryPhotoStore photos,
        TimeProvider clock)
    {
        _db = db;
        _rewards = rewards;
        _missions = missions;
        _options = options.Value;
        _photos = photos;
        _clock = clock;
    }

    public async Task<ServiceResult<DiscoveryResultDto>> CreateAsync(
        Guid childId, DiscoveryRequest request, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles.FirstOrDefaultAsync(c => c.Id == childId, ct);
        if (child is null)
            return ServiceResult<DiscoveryResultDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var label = request.Label.Trim();
        var now = _clock.GetUtcNow().UtcDateTime;

        var isFirstOfKind = !await _db.Discoveries
            .AnyAsync(d => d.ChildProfileId == childId && d.Label.ToLower() == label.ToLower(), ct);

        var rewardedToday = await _db.Discoveries
            .CountAsync(d => d.ChildProfileId == childId && d.CreatedAt >= now.Date && d.StarsEarned > 0, ct);

        var stars = rewardedToday >= _options.MaxRewardedPerDay
            ? 0
            : isFirstOfKind ? _options.FirstOfKindStars : _options.BaseStars;

        string? photoPath = null;
        if (!string.IsNullOrWhiteSpace(request.PhotoBase64))
        {
            var saved = await TrySavePhotoAsync(childId, request.PhotoBase64, ct);
            if (!saved.Succeeded)
                return ServiceResult<DiscoveryResultDto>.Fail(saved.Error);

            photoPath = saved.Value;
        }

        var discovery = new Entities.Discovery
        {
            ChildProfileId = childId,
            Label = label,
            CategoryKey = string.IsNullOrWhiteSpace(request.CategoryKey) ? "nature" : request.CategoryKey,
            PhotoPath = photoPath,
            StarsEarned = stars,
            CreatedAt = now
        };

        _db.Discoveries.Add(discovery);

        if (stars > 0)
            await _rewards.GrantStarsAsync(child, stars, $"Discovery: {label}", ct);

        await _missions.TrackAsync(childId, MissionType.DiscoverRealWorld, null, 1, ct);
        await _db.SaveChangesAsync(ct);
        await _rewards.EvaluateBadgesAsync(childId, ct);

        var total = await _db.Discoveries.CountAsync(d => d.ChildProfileId == childId, ct);

        return ServiceResult<DiscoveryResultDto>.Ok(new DiscoveryResultDto
        {
            Id = discovery.Id,
            Label = label,
            StarsEarned = stars,
            IsFirstOfKind = isFirstOfKind,
            TotalDiscoveries = total,
            Message = BuildMessage(child.LanguageCode, label, stars, isFirstOfKind)
        });
    }

    public async Task<List<DiscoveryDto>> GetRecentAsync(Guid childId, int take = 20, CancellationToken ct = default) =>
        await _db.Discoveries
            .AsNoTracking()
            .Where(d => d.ChildProfileId == childId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .Select(d => new DiscoveryDto
            {
                Id = d.Id,
                Label = d.Label,
                CategoryKey = d.CategoryKey,
                HasPhoto = d.PhotoPath != null,
                StarsEarned = d.StarsEarned,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync(ct);

    public async Task<ServiceResult<DiscoveryPhotoFile>> GetPhotoAsync(
        Guid childId, Guid discoveryId, CancellationToken ct = default)
    {
        // Sahiblik sorğunun ÖZÜNDƏDİR: kəşfi tapıb sonra uşağı yoxlamaq eyni
        // nəticəni verir, amma bir gün şərtin biri düşsə, başqa uşağın şəkli
        // görünərdi. Bu formada belə bir səhv mümkün deyil.
        var photoPath = await _db.Discoveries
            .AsNoTracking()
            .Where(d => d.Id == discoveryId && d.ChildProfileId == childId)
            .Select(d => d.PhotoPath)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(photoPath))
            return ServiceResult<DiscoveryPhotoFile>.NotFound(
                Localized.T("Bu kəşfin şəkli yoxdur.", "This discovery has no photo."));

        // Faylın olub-olmaması saxlama qatının işidir: qeyd bazada qalıb, fayl
        // isə köhnə deploy-larda itmiş ola bilər — bu, xəta deyil, «tapılmadı»dır.
        var file = await _photos.OpenAsync(photoPath, ct);

        return file is null
            ? ServiceResult<DiscoveryPhotoFile>.NotFound(Localized.T("Şəkil tapılmadı.", "The photo was not found."))
            : ServiceResult<DiscoveryPhotoFile>.Ok(file);
    }

    private static string BuildMessage(string language, string label, int stars, bool isFirstOfKind)
    {
        var az = Localized.Normalize(language) == Localized.Azerbaijani;

        if (stars == 0)
            return az
                ? "Gözəl tapıntı! Bugünkü ulduz bonusu bitib — amma kəşf etməyə davam et."
                : "Nice find! You've already collected today's star bonus — keep exploring anyway.";

        if (isFirstOfKind)
            return az ? $"Əla! Yeni bir şey kəşf etdin: {label}!" : $"Nice! You discovered a new {label}!";

        return az ? $"Yenə {label}! Kolleksiyan böyüyür." : $"Another {label}! Your collection is growing.";
    }

    /// <summary>
    /// Şəkil JSON gövdəsində base64 mətn kimi gəlir, yəni 30 MB fayl serverdə
    /// 40 MB mətnə (UTF-16-da 80 MB) çevrilir. Ona görə burada hər artıq nüsxə
    /// baha başa gəlir və iki qayda var:
    ///
    ///   1. ÖLÇÜ ƏVVƏLCƏ MƏTNDƏN yoxlanılır — dekod etməmişdən. Əvvəl əvvəlcə
    ///      dekod olunur, sonra ölçüyə baxılırdı: limitdən böyük şəkil rədd
    ///      edilməmişdən öncə onlarla meqabayt yaddaş ayırırdı.
    ///   2. Prefiks (`data:image/jpeg;base64,`) SPAN ilə kəsilir — substring
    ///      bütün mətnin ikinci nüsxəsini yaradırdı.
    /// </summary>
    private async Task<ServiceResult<string>> TrySavePhotoAsync(Guid childId, string base64, CancellationToken ct)
    {
        // "data:image/jpeg;base64,...." formatı da qəbul olunur — nüsxə çıxarmadan kəsilir.
        var comma = base64.IndexOf(',');
        var payload = comma >= 0 ? base64.AsSpan(comma + 1) : base64.AsSpan();

        // Base64 hər 4 simvolda 3 bayt daşıyır; doldurma nəzərə alınmadan üst hədd budur.
        if ((long)payload.Length / 4 * 3 > _options.MaxPhotoBytes)
            return ServiceResult<string>.Fail($"Şəkil {_options.MaxPhotoBytes / (1024 * 1024)} MB-dan böyük ola bilməz.");

        var buffer = new byte[payload.Length / 4 * 3 + 3];

        if (!Convert.TryFromBase64Chars(payload, buffer, out var written))
            return ServiceResult<string>.Fail(Localized.T("Şəkil formatı düzgün deyil.", "That image format is not valid."));

        var bytes = buffer.AsMemory(0, written);

        if (written > _options.MaxPhotoBytes)
            return ServiceResult<string>.Fail($"Şəkil {_options.MaxPhotoBytes / (1024 * 1024)} MB-dan böyük ola bilməz.");

        var key = await _photos.SaveAsync(childId, bytes, DiscoveryContentType.ExtensionFor(bytes.Span), ct);

        return ServiceResult<string>.Ok(key);
    }

}
