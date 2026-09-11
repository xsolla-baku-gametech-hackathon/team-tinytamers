using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Shared.Enums;

namespace PetPal.Api.Wardrobe;

/// <summary>
/// Bir dizaynı başdan sona çəkir: moderasiya → baza portreti → paltar →
/// bayt yoxlaması → saxlanc.
///
/// <para>Hər addımın uğursuzluğu uşağa YUMŞAQ çatır: moderasiyanın və modelin
/// rəddi «başqa paltar fikirləşək», texniki xəta isə «sonra yoxla» deməkdir və
/// uşağın günlük həddindən sayılmır.</para>
///
/// <para>Mənaca ön yoxlama qurulmayıbsa (<see cref="ModerationVerdict.NotConfigured"/>)
/// dizayn davam edir — bu, yerləşdirmənin açıq qərarıdır; qurulub, amma
/// cavab vermirsə şəkil çəkilmir.</para>
///
/// <para>Sorğu pullu modelə getməzdən ƏVVƏL sətir <c>ReachedProvider</c> kimi
/// yazılır: proses zəngin ortasında ölsə də xərc gündəlik həddə düşür.</para>
/// </summary>
public sealed class WardrobeRenderer
{
    /// <summary>Bir dizayn üçün ən çox neçə cəhd — sonsuz təkrar olmasın.</summary>
    public const int MaxAttempts = 2;

    private static readonly string[] BaseFormats =
    [
        PuzzleIllustrationValidator.Webp, PuzzleIllustrationValidator.Png, PuzzleIllustrationValidator.Jpeg
    ];

    private readonly AppDbContext _db;
    private readonly IWardrobeImageProvider _provider;
    private readonly IWardrobeModeration _moderation;
    private readonly IWardrobeImageStore _store;
    private readonly WardrobeOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<WardrobeRenderer> _logger;

    public WardrobeRenderer(
        AppDbContext db,
        IWardrobeImageProvider provider,
        IWardrobeModeration moderation,
        IWardrobeImageStore store,
        IOptions<WardrobeOptions> options,
        TimeProvider clock,
        ILogger<WardrobeRenderer> logger)
    {
        _db = db;
        _provider = provider;
        _moderation = moderation;
        _store = store;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task RenderAsync(Guid designId, CancellationToken ct)
    {
        var design = await _db.WardrobeDesigns.FirstOrDefaultAsync(d => d.Id == designId, ct);

        if (design is null || design.Status != WardrobeDesignStatus.Pending)
            return;

        if (!_provider.IsEnabled || design.Attempts >= MaxAttempts)
        {
            await FinishAsync(design, WardrobeDesignStatus.Failed, WardrobeBlockReason.Unavailable, ct);
            return;
        }

        design.Attempts++;
        await _db.SaveChangesAsync(ct);

        switch (await _moderation.CheckAsync(design.Text, ct))
        {
            case ModerationVerdict.Flagged:
                await FinishAsync(design, WardrobeDesignStatus.Blocked, WardrobeBlockReason.Moderation, ct);
                return;

            case ModerationVerdict.Unavailable:
                await FinishAsync(design, WardrobeDesignStatus.Failed, WardrobeBlockReason.Unavailable, ct);
                return;

            case ModerationVerdict.NotConfigured:
                _logger.LogDebug("Wardrobe: mənaca ön yoxlama qurulmayıb ({DesignId}).", design.Id);
                break;
        }

        var reference = _options.UseBasePortrait
            ? await BasePortraitAsync(design.PetSpecies, design.PetStage, ct)
            : null;

        if (reference is not null && reference.Bytes.Length > _provider.MaxReferenceBytes)
        {
            _logger.LogInformation(
                "Wardrobe: baza portreti istinad üçün böyükdür ({Bytes} bayt) — dizayn sıfırdan çəkilir.",
                reference.Bytes.Length);

            reference = null;
        }

        var prompt = WardrobePromptBuilder.Outfit(design.PetSpecies, design.PetStage, design.Text, reference is not null);

        design.PromptVersion = WardrobePromptBuilder.TemplateVersion;
        design.PromptHash = WardrobePromptBuilder.Fingerprint(prompt);
        design.Provider = _provider.Name;
        design.Model = _provider.Model;
        design.ReachedProvider = true;
        await _db.SaveChangesAsync(ct);

        var result = reference is null
            ? await _provider.GenerateAsync(prompt, ct)
            : await _provider.EditAsync(prompt, reference.Bytes, reference.ContentType, ct);

        if (result.Refused)
        {
            await FinishAsync(design, WardrobeDesignStatus.Blocked, WardrobeBlockReason.ProviderRefused, ct);
            return;
        }

        if (!result.Succeeded)
        {
            _logger.LogInformation("Wardrobe: dizayn çəkilmədi ({DesignId}, səbəb {Reason}).", design.Id, result.Reason);
            await FinishAsync(design, WardrobeDesignStatus.Failed, WardrobeBlockReason.Unavailable, ct);
            return;
        }

        var check = PuzzleIllustrationValidator.Validate(result.Bytes, _options.MaxImageBytes);

        if (!check.IsValid)
        {
            _logger.LogInformation("Wardrobe: şəkil rədd edildi ({DesignId}, səbəb {Reason}).", design.Id, check.Reason);
            await FinishAsync(design, WardrobeDesignStatus.Failed, WardrobeBlockReason.Unavailable, ct);
            return;
        }

        design.AssetKey = await _store.SaveAsync(
            WardrobeImageKeys.Design(design.ChildProfileId, design.Id, check.ContentType), result.Bytes!, ct);
        design.ContentType = check.ContentType;
        design.Width = check.Width;
        design.Height = check.Height;

        await FinishAsync(design, WardrobeDesignStatus.Ready, WardrobeBlockReason.None, ct);
    }

    private sealed record Reference(byte[] Bytes, string ContentType);

    /// <summary>
    /// Növ+mərhələnin baza portreti — varsa saxlancdan, yoxdursa bir dəfə
    /// çəkilir. Alınmasa <c>null</c>: dizayn onda sıfırdan çəkilir, uşaq
    /// fərqi yalnız pet-in bir az başqa görünməsində hiss edir.
    ///
    /// <para>Barmaq izində provayder, model, ölçü və keyfiyyət var: provayder
    /// dəyişəndə köhnə portret yeni üslubla qarışmır.</para>
    /// </summary>
    private async Task<Reference?> BasePortraitAsync(string species, PetStage stage, CancellationToken ct)
    {
        var prompt = WardrobePromptBuilder.BasePortrait(species, stage);
        var fingerprint = WardrobePromptBuilder.Fingerprint(string.Join('|',
            prompt, _provider.Name, _provider.Model, _options.EffectiveSize, _options.EffectiveRunwayRatio,
            _options.EffectiveQuality));

        foreach (var format in BaseFormats)
        {
            if (await _store.ReadAsync(WardrobeImageKeys.Base(fingerprint, format), ct) is { } cached)
                return new Reference(cached, format);
        }

        var result = await _provider.GenerateAsync(prompt, ct);

        if (!result.Succeeded)
        {
            _logger.LogInformation("Wardrobe: baza portreti alınmadı ({Species}, {Stage}).", species, stage);
            return null;
        }

        var check = PuzzleIllustrationValidator.Validate(result.Bytes, _options.MaxImageBytes);

        if (!check.IsValid)
            return null;

        await _store.SaveAsync(WardrobeImageKeys.Base(fingerprint, check.ContentType), result.Bytes!, ct);

        return new Reference(result.Bytes!, check.ContentType);
    }

    private async Task FinishAsync(
        WardrobeDesign design, WardrobeDesignStatus status, WardrobeBlockReason reason, CancellationToken ct)
    {
        design.Status = status;
        design.Reason = reason;
        design.CompletedAt = _clock.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);
    }
}
