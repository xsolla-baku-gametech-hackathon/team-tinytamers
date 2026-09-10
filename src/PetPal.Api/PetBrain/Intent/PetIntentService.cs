using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain.Mind;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Intent;

/// <summary>
/// Pet-in məhdud avtonomiyası.
///
/// <para><b>Fon işçisi YOXDUR və bu, qərardır.</b> Niyyət app açılanda
/// <see cref="TimeProvider"/> ilə irəlilədilir: nəticə determinist qalır, test
/// onu saatı sürüşdürərək yoxlaya bilir, server isə minlərlə uşaq üçün boş
/// yerə iş görmür. Uşaq üçün nəticə eynidir — o, onsuz da yalnız app-i
/// açanda görür.</para>
///
/// <para><b>Nə edə bilmir:</b> mükafat vermək, çətinlik dəyişmək, ekran vaxtı
/// qaydasını aşmaq, uşağa bildiriş göndərmək. Niyyət yalnız bir CÜMLƏ və bir
/// təsdiqlənmiş artefakt açarı yaradır.</para>
/// </summary>
public sealed class PetIntentService
{
    private readonly AppDbContext _db;
    private readonly PetBrainTelemetry _telemetry;
    private readonly TimeProvider _clock;
    private readonly PetBrainV2Options _options;

    public PetIntentService(
        AppDbContext db,
        PetBrainTelemetry telemetry,
        TimeProvider clock,
        IOptions<PetBrainV2Options> options)
    {
        _db = db;
        _telemetry = telemetry;
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>
    /// Niyyəti irəlilədir və uşağa göstəriləcək hissəni qaytarır.
    ///
    /// <para>Ardıcıllıq: bitmiş niyyəti tamamla → nəticəni oxu → şərtlər
    /// dəyişibsə köhnəlmişi kənara qoy → yenisini planla.</para>
    ///
    /// <para>Özəllik bağlıdırsa <c>null</c> qayıdır və heç bir sətir yazılmır.</para>
    /// </summary>
    public async Task<PetBrainIntentDto?> AdvanceAsync(
        ChildProfile child, PetMindContext mind, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.IntentEnabled || !mind.PetIsHatched)
            return null;

        var now = _clock.GetUtcNow().UtcDateTime;
        var language = child.LanguageCode;
        var petName = child.Pet?.Name ?? "Pet";

        var current = await _db.PetIntents
            .Where(i => i.ChildProfileId == child.Id && i.Status == PetBrainIntentStatus.Active)
            .OrderByDescending(i => i.StartedAt)
            .FirstOrDefaultAsync(ct);

        // ---- Bitmiş niyyət: nəticə İNDİ yazılır ----
        if (current is not null && current.ExpectedEndAt <= now)
        {
            current.Status = PetBrainIntentStatus.Completed;
            current.OutcomeKey = PetIntentPlanner.OutcomeFor(current.Type, current.Id);

            _telemetry.Intent("completed", current.Type, current.ReasonKey);

            var finished = ToDto(current, language, petName, isDone: true);

            // Nəticə BİR DƏFƏ danışılır: uşaq onu gördü, deməli növbəti
            // açılışda təkrarlanmır.
            current.SeenAt = now;

            await PlanNextAsync(child, mind, now, ct);
            await _db.SaveChangesAsync(ct);

            return finished;
        }

        // ---- Şərtlər dəyişdi: köhnə niyyət kənara qoyulur ----
        //
        // Uşaq gəldi və macərəya çıxdı, ya ekran vaxtı bağlandı. Bu, uşağın
        // səhvi DEYİL və ona belə göstərilmir — niyyət səssizcə dəyişir.
        if (current is not null && !StillFits(current, mind))
        {
            current.Status = PetBrainIntentStatus.Superseded;
            _telemetry.Intent("superseded", current.Type, current.ReasonKey);

            current = null;
        }

        current ??= await PlanNextAsync(child, mind, now, ct);

        await _db.SaveChangesAsync(ct);

        return current is null ? null : ToDto(current, language, petName, isDone: false);
    }

    /// <summary>
    /// Niyyət hələ də vəziyyətə uyğundurmu.
    ///
    /// <para>Ekran vaxtı bağlananda YALNIZ dincəlmək qalır: pet uşağı geri
    /// çağırmaq üçün bəhanə saxlamamalıdır.</para>
    /// </summary>
    private static bool StillFits(PetIntent intent, PetMindContext mind)
    {
        if (mind.ActivityBlocked)
            return intent.Type == PetBrainIntentType.Rest;

        // Təcili qulluq ehtiyacı yaranıbsa, pet-in "macəra" niyyəti yalan olur.
        if (mind.NeedsCare)
            return intent.Type is PetBrainIntentType.Care or PetBrainIntentType.Rest;

        return true;
    }

    private async Task<PetIntent?> PlanNextAsync(
        ChildProfile child, PetMindContext mind, DateTime now, CancellationToken ct)
    {
        if (PetIntentPlanner.Plan(mind) is not { } plan)
            return null;

        var intent = new PetIntent
        {
            ChildProfileId = child.Id,
            Type = plan.Type,
            ReasonKey = plan.ReasonKey,
            StartedAt = now,
            ExpectedEndAt = now + plan.Duration,
            Status = PetBrainIntentStatus.Active,
            RelatedMissionKey = plan.MissionKey,
            PlannerVersion = PetIntentPlanner.Version
        };

        _db.PetIntents.Add(intent);

        _telemetry.Intent("selected", plan.Type, plan.ReasonKey);

        await Task.CompletedTask;

        return intent;
    }

    private static PetBrainIntentDto ToDto(
        PetIntent intent, string language, string petName, bool isDone) => new()
    {
        Type = intent.Type,
        Icon = PetIntentVoice.Icon(intent.Type),
        IsComplete = isDone,
        Line = isDone
            ? PetIntentVoice.Outcome(intent.Type, intent.OutcomeKey, language, petName)
            : PetIntentVoice.Doing(intent.Type, language, petName),
        OutcomeKey = intent.OutcomeKey
    };
}
