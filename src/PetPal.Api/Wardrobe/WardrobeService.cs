using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain.Scenery;
using PetPal.Api.Pets;
using PetPal.Shared.Dtos.Wardrobe;
using PetPal.Shared.Enums;

namespace PetPal.Api.Wardrobe;

/// <summary>
/// Dizayn studiyası — uşaq pet-ə geyindirmək istədiyini YAZIR, şəkil modeli
/// çəkir.
///
/// <para>Söhbət kimi, uşağın mətni xarici modelə gedir, ona görə axın eyni
/// qatlardan keçir:</para>
/// <list type="number">
///   <item><b>Valideyn açarı</b> — <see cref="ChildProfile.WardrobeAiEnabled"/>, standart bağlı.</item>
///   <item><b>Günlük hədd</b> — uşaq başına və bütün uşaqlar üzrə.</item>
///   <item><b>Determinist filtr</b> — <see cref="WardrobeRequestGuard"/>, həmişə işləyir.</item>
///   <item><b>Moderasiya</b> — işçidə, şəkildən əvvəl; işləmirsə şəkil də çəkilmir.</item>
///   <item><b>Modelin öz təhlükəsizliyi</b> və <b>bayt yoxlaması</b>.</item>
/// </list>
///
/// <para>Hər cəhd valideynə görünür, saxlanılanlar da daxil.</para>
/// </summary>
public sealed class WardrobeService : IWardrobeService
{
    private const int StoredTextLength = 200;
    private const int DefaultParentTake = 50;
    private const int MaxParentTake = 200;

    private readonly AppDbContext _db;
    private readonly IWardrobeImageProvider _provider;
    private readonly IWardrobeImageStore _store;
    private readonly WardrobeQueue _queue;
    private readonly WardrobeOptions _options;
    private readonly TimeProvider _clock;

    public WardrobeService(
        AppDbContext db,
        IWardrobeImageProvider provider,
        IWardrobeImageStore store,
        WardrobeQueue queue,
        IOptions<WardrobeOptions> options,
        TimeProvider clock)
    {
        _db = db;
        _provider = provider;
        _store = store;
        _queue = queue;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<ServiceResult<WardrobeStateDto>> GetStateAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await LoadChildAsync(childId, ct);

        return child is null
            ? ServiceResult<WardrobeStateDto>.NotFound(ChildNotFound)
            : ServiceResult<WardrobeStateDto>.Ok(await BuildStateAsync(child, ct));
    }

    public async Task<ServiceResult<WardrobeDesignDto>> CreateAsync(
        Guid childId, CreateWardrobeDesignRequest request, CancellationToken ct = default)
    {
        var child = await LoadChildAsync(childId, ct);

        if (child is null)
            return ServiceResult<WardrobeDesignDto>.NotFound(ChildNotFound);

        var language = child.LanguageCode;

        if (child.Pet is not { HatchedAt: not null } pet)
            return ServiceResult<WardrobeDesignDto>.Conflict(Localized.T(language,
                "Əvvəlcə yumurtanı açmaq lazımdır.", "The egg needs to hatch first."));

        if (!child.WardrobeAiEnabled)
            return ServiceResult<WardrobeDesignDto>.Forbidden(Localized.T(language,
                "Dizayn studiyasını valideynin açmalıdır.", "A parent needs to turn on the design studio."));

        if (!_provider.IsEnabled)
            return ServiceResult<WardrobeDesignDto>.Conflict(Localized.T(language,
                "Dərzi hələ gəlməyib — bir az sonra yoxla.", "The tailor has not arrived yet — try again later."));

        var now = _clock.GetUtcNow().UtcDateTime;
        var text = request.Text ?? string.Empty;
        var reason = WardrobeRequestGuard.Inspect(text);

        if (reason != WardrobeBlockReason.None)
        {
            var blocked = NewDesign(child.Id, pet, text, now);
            blocked.Status = WardrobeDesignStatus.Blocked;
            blocked.Reason = reason;
            blocked.CompletedAt = now;

            _db.WardrobeDesigns.Add(blocked);
            await _db.SaveChangesAsync(ct);

            return ServiceResult<WardrobeDesignDto>.Ok(ToDto(blocked, language));
        }

        if (await UsedTodayAsync(child.Id, now, ct) >= Math.Max(0, _options.DesignsPerChildPerDay))
            return ServiceResult<WardrobeDesignDto>.Conflict(Localized.T(language,
                "Bu gün dərzi çox işlədi — sabah yeni paltarlar tikək!",
                "The tailor worked hard today — let us sew new outfits tomorrow!"));

        if (await PaidTodayAsync(now, ct) >= Math.Max(0, _options.MaxPaidImagesPerDay))
            return ServiceResult<WardrobeDesignDto>.Conflict(Localized.T(language,
                "Dərzi bu gün çox məşğuldur — sabah yenə yoxla.",
                "The tailor is very busy today — try again tomorrow."));

        var design = NewDesign(child.Id, pet, text, now);
        design.Status = WardrobeDesignStatus.Pending;

        _db.WardrobeDesigns.Add(design);
        await _db.SaveChangesAsync(ct);

        _queue.Enqueue(design.Id);

        return ServiceResult<WardrobeDesignDto>.Ok(ToDto(design, language));
    }

    public async Task<WardrobeImageLookup?> GetImageAsync(Guid childId, Guid designId, CancellationToken ct = default)
    {
        var design = await _db.WardrobeDesigns
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == designId && d.ChildProfileId == childId && d.DeletedAt == null, ct);

        return design is null ? null : Lookup(design);
    }

    public async Task<ServiceResult<WardrobeStateDto>> EquipAsync(
        Guid childId, EquipWardrobeDesignRequest request, CancellationToken ct = default)
    {
        var child = await LoadChildAsync(childId, ct);

        if (child is null)
            return ServiceResult<WardrobeStateDto>.NotFound(ChildNotFound);

        var language = child.LanguageCode;

        var touched = await _db.WardrobeDesigns
            .Where(d => d.ChildProfileId == childId && (d.IsEquipped || d.Id == request.DesignId))
            .ToListAsync(ct);

        if (request.DesignId is { } designId)
        {
            var target = touched.FirstOrDefault(d => d.Id == designId);

            if (target is null || target.DeletedAt is not null)
                return ServiceResult<WardrobeStateDto>.NotFound(Localized.T(language,
                    "Dizayn tapılmadı.", "Design not found."));

            if (target.Status != WardrobeDesignStatus.Ready)
                return ServiceResult<WardrobeStateDto>.Conflict(Localized.T(language,
                    "Bu dizayn hələ hazır deyil.", "This design is not ready yet."));
        }

        foreach (var design in touched)
            design.IsEquipped = design.Id == request.DesignId;

        await _db.SaveChangesAsync(ct);

        return ServiceResult<WardrobeStateDto>.Ok(await BuildStateAsync(child, ct));
    }

    public async Task<ServiceResult<WardrobeStateDto>> DeleteAsync(
        Guid childId, Guid designId, CancellationToken ct = default)
    {
        var child = await LoadChildAsync(childId, ct);

        if (child is null)
            return ServiceResult<WardrobeStateDto>.NotFound(ChildNotFound);

        var language = child.LanguageCode;

        var design = await _db.WardrobeDesigns
            .FirstOrDefaultAsync(d => d.Id == designId && d.ChildProfileId == childId && d.DeletedAt == null, ct);

        if (design is null)
            return ServiceResult<WardrobeStateDto>.NotFound(Localized.T(language,
                "Dizayn tapılmadı.", "Design not found."));

        if (design.Status == WardrobeDesignStatus.Pending)
            return ServiceResult<WardrobeStateDto>.Conflict(Localized.T(language,
                "Dərzi hələ tikir — bitəndən sonra silə bilərsən.",
                "The tailor is still sewing — you can remove it when it is done."));

        if (design.AssetKey.Length > 0)
            await _store.DeleteAsync(design.AssetKey, ct);

        design.AssetKey = string.Empty;
        design.IsEquipped = false;
        design.DeletedAt = _clock.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);

        return ServiceResult<WardrobeStateDto>.Ok(await BuildStateAsync(child, ct));
    }

    public async Task<ServiceResult<ParentWardrobeLogDto>> GetParentLogAsync(
        Guid parentId, Guid childId, int? take, CancellationToken ct = default)
    {
        var child = await LoadOwnedChildAsync(parentId, childId, ct);

        if (child is null)
            return ServiceResult<ParentWardrobeLogDto>.NotFound(ChildNotFound);

        var limit = Math.Clamp(take ?? DefaultParentTake, 1, MaxParentTake);

        var entries = await _db.WardrobeDesigns
            .AsNoTracking()
            .Where(d => d.ChildProfileId == childId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(limit)
            .Select(d => new ParentWardrobeEntryDto
            {
                Id = d.Id,
                Text = d.Text,
                Status = d.Status,
                Reason = d.Reason,
                HasImage = d.Status == WardrobeDesignStatus.Ready && d.AssetKey != string.Empty,
                DeletedByChild = d.DeletedAt != null,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync(ct);

        return ServiceResult<ParentWardrobeLogDto>.Ok(new ParentWardrobeLogDto
        {
            Enabled = child.WardrobeAiEnabled,
            ServiceReady = _provider.IsEnabled,
            DesignsToday = await UsedTodayAsync(childId, _clock.GetUtcNow().UtcDateTime, ct),
            DesignsPerDay = _options.DesignsPerChildPerDay,
            Entries = entries
        });
    }

    public async Task<ServiceResult<WardrobeSettingsRequest>> UpdateSettingsAsync(
        Guid parentId, Guid childId, WardrobeSettingsRequest request, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles.FirstOrDefaultAsync(c => c.Id == childId && c.ParentUserId == parentId, ct);

        if (child is null)
            return ServiceResult<WardrobeSettingsRequest>.NotFound(ChildNotFound);

        child.WardrobeAiEnabled = request.Enabled;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<WardrobeSettingsRequest>.Ok(request);
    }

    public async Task<WardrobeImageLookup?> GetParentImageAsync(
        Guid parentId, Guid childId, Guid designId, CancellationToken ct = default)
    {
        if (await LoadOwnedChildAsync(parentId, childId, ct) is null)
            return null;

        var design = await _db.WardrobeDesigns
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == designId && d.ChildProfileId == childId && d.DeletedAt == null, ct);

        return design is null ? null : Lookup(design);
    }

    private static string ChildNotFound => Localized.T("Uşaq profili tapılmadı.", "Child profile not found.");

    private Task<ChildProfile?> LoadChildAsync(Guid childId, CancellationToken ct) =>
        _db.ChildProfiles
            .Include(c => c.Pet)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

    private Task<ChildProfile?> LoadOwnedChildAsync(Guid parentId, Guid childId, CancellationToken ct) =>
        _db.ChildProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == childId && c.ParentUserId == parentId, ct);

    private async Task<WardrobeStateDto> BuildStateAsync(ChildProfile child, CancellationToken ct)
    {
        var language = child.LanguageCode;
        var perDay = Math.Max(0, _options.DesignsPerChildPerDay);

        var designs = await _db.WardrobeDesigns
            .AsNoTracking()
            .Where(d => d.ChildProfileId == child.Id && d.DeletedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .Take(Math.Max(1, _options.GalleryLimit))
            .ToListAsync(ct);

        var equipped = await _db.WardrobeDesigns
            .AsNoTracking()
            .Where(d => d.ChildProfileId == child.Id && d.IsEquipped && d.DeletedAt == null)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(ct);

        var used = await UsedTodayAsync(child.Id, _clock.GetUtcNow().UtcDateTime, ct);
        var hatched = child.Pet is { HatchedAt: not null };

        return new WardrobeStateDto
        {
            Enabled = child.WardrobeAiEnabled && _provider.IsEnabled && hatched,
            ParentAllowed = child.WardrobeAiEnabled,
            ServiceReady = _provider.IsEnabled,
            DesignsPerDay = perDay,
            DesignsLeftToday = Math.Max(0, perDay - used),
            MaxTextLength = WardrobeLimits.MaxTextLength,
            Examples = Examples(language),
            EquippedDesignId = equipped,
            Designs = [.. designs.Select(d => ToDto(d, language))]
        };
    }

    /// <summary>
    /// Bu gün uşağın həddindən sayılan dizaynlar: qəbul edilən, hazır olan və
    /// moderasiyanın və ya modelin rədd etdiyi. Determinist filtrin saxladığı
    /// və texniki səbəbdən alınmayan cəhd SAYILMIR — uşaq cəzalanmamalıdır.
    /// Gün UTC-yə görə hesablanır — söhbət limiti ilə eyni konvensiya.
    /// </summary>
    private Task<int> UsedTodayAsync(Guid childId, DateTime now, CancellationToken ct)
    {
        var dayStart = now.Date;

        return _db.WardrobeDesigns.CountAsync(d =>
            d.ChildProfileId == childId &&
            d.CreatedAt >= dayStart &&
            (d.Status == WardrobeDesignStatus.Pending ||
             d.Status == WardrobeDesignStatus.Ready ||
             (d.Status == WardrobeDesignStatus.Blocked &&
              (d.Reason == WardrobeBlockReason.Moderation || d.Reason == WardrobeBlockReason.ProviderRefused))),
            ct);
    }

    /// <summary>Bu gün pullu modelə çatan (və ya çatmaq üzrə olan) dizaynlar — bütün uşaqlar üzrə.</summary>
    private Task<int> PaidTodayAsync(DateTime now, CancellationToken ct)
    {
        var dayStart = now.Date;

        return _db.WardrobeDesigns.CountAsync(d =>
            d.CreatedAt >= dayStart && (d.ReachedProvider || d.Status == WardrobeDesignStatus.Pending), ct);
    }

    private static WardrobeDesign NewDesign(Guid childId, Pet pet, string text, DateTime now)
    {
        var trimmed = text.Trim();

        return new WardrobeDesign
        {
            Id = Guid.NewGuid(),
            ChildProfileId = childId,
            Text = trimmed.Length > StoredTextLength ? trimmed[..StoredTextLength] : trimmed,
            PetSpecies = SceneryKeys.ApprovedSpecies(pet.Species),
            PetStage = PetProgression.StageFor(pet),
            CreatedAt = now
        };
    }

    private static WardrobeImageLookup Lookup(WardrobeDesign design) =>
        new(design.Status,
            design.Status == WardrobeDesignStatus.Ready && design.AssetKey.Length > 0 ? design.AssetKey : null);

    private static WardrobeDesignDto ToDto(WardrobeDesign design, string language) => new()
    {
        Id = design.Id,
        Text = design.Text,
        Status = design.Status,
        Reason = design.Reason,
        Message = WardrobeMessages.For(design.Status, design.Reason, language),
        ImageUrl = design.Status is WardrobeDesignStatus.Pending or WardrobeDesignStatus.Ready
            ? $"/api/pet/wardrobe/designs/{design.Id}/image"
            : string.Empty,
        IsEquipped = design.IsEquipped,
        CreatedAt = design.CreatedAt
    };

    private static List<string> Examples(string language) =>
    [
        Localized.T(language, "Qırmızı super qəhrəman pelerini", "A red superhero cape"),
        Localized.T(language, "Ulduzlu mavi pijama", "Blue pyjamas with stars"),
        Localized.T(language, "Dəniz qulduru papağı və jilet", "A pirate hat and vest"),
        Localized.T(language, "Göy qurşağı rəngli sviter", "A rainbow sweater"),
        Localized.T(language, "Qar dənəli şərf və papaq", "A snowflake scarf and hat"),
        Localized.T(language, "Kosmonavt skafandrı", "An astronaut suit")
    ];
}

/// <summary>
/// Uşağa göstərilən mesajlar — nəzarətli şablondan, modeldən yox.
///
/// <para>Heç bir mesaj uşağı günahlandırmır: saxlanılan arzuya da «gəl başqa
/// bir şey fikirləşək» deyilir, texniki xətaya isə «bu cəhd sayılmadı».</para>
/// </summary>
public static class WardrobeMessages
{
    public static string For(WardrobeDesignStatus status, WardrobeBlockReason reason, string language) => status switch
    {
        WardrobeDesignStatus.Pending => Localized.T(language,
            "Dərzi tikir… bir az gözlə!", "The tailor is sewing… just a moment!"),

        WardrobeDesignStatus.Ready => Localized.T(language,
            "Hazırdır! Geyindirmək üçün toxun.", "Ready! Tap to dress up."),

        WardrobeDesignStatus.Failed => Localized.T(language,
            "Dərzi indi məşğuldur — bir az sonra yenə yoxla. Bu cəhd sayılmadı.",
            "The tailor is busy right now — try again a bit later. This try did not count."),

        _ => reason switch
        {
            WardrobeBlockReason.Empty => Localized.T(language,
                "Pet-ə nə geyindirmək istədiyini yaz.", "Write what you would like your pet to wear."),

            WardrobeBlockReason.TooLong => Localized.T(language,
                "Bir az qısa yaz — bir-iki cümlə kifayətdir.",
                "Make it a bit shorter — one or two sentences are enough."),

            WardrobeBlockReason.Injection => Localized.T(language,
                "Gəl yalnız paltardan danışaq.", "Let us only talk about clothes."),

            WardrobeBlockReason.ContactInfo => Localized.T(language,
                "Paltara telefon, e-poçt və ünvan yazmırıq — başqa bir şey fikirləş.",
                "We do not put phone numbers, emails or addresses on clothes — think of something else."),

            WardrobeBlockReason.Repetition => Localized.T(language,
                "Hərfləri yığma, sözlə yaz.", "Use words, not a pile of letters."),

            _ => Localized.T(language,
                "Bunu tikə bilmərik — gəl başqa, şən bir paltar fikirləşək!",
                "We cannot sew that one — let us think of another fun outfit!")
        }
    };
}
