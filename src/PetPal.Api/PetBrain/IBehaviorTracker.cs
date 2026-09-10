using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Bir hadisənin daşıdığı TƏSDİQLƏNMİŞ məlumat.
///
/// <para>Sərbəst mətn yoxdur və ola bilməz: <paramref name="Source"/> domenin
/// öz açarıdır (şablon kodu, oyun açarı, bacarıq adı), <paramref name="Detail"/>
/// isə qısa strukturlu əlavədir. Uşağın yazdığı heç nə buraya düşmür.</para>
/// </summary>
public sealed record PetBrainEventData(
    string Source,
    string Detail,
    IReadOnlyList<TraitAdjustment> Adjustments);

/// <summary>
/// Davranış izləməsinin YEGANƏ giriş nöqtəsi.
///
/// <para>Qəsdən dar saxlanılır və <b>açıq endpoint-i yoxdur</b>: uşaq klienti
/// ixtiyari hadisə adı, bal dəyişikliyi və ya JSON göndərə bilmir. Domen
/// endpoint-ləri əvvəlcə əməliyyatı yoxlayır, sonra bu interfeysə ETİBARLI
/// məlumat verir.</para>
///
/// <para>Metod <c>SaveChangesAsync</c> ÇAĞIRMIR — mövcud
/// <see cref="Missions.IMissionProgressTracker"/> ilə eyni müqavilə: yazma anını
/// çağıran əməliyyat idarə edir, ona görə tamamlama bir tranzaksiyada qalır.</para>
/// </summary>
public interface IBehaviorTracker
{
    /// <param name="idempotencyKey">
    /// Təkrarı mümkün olan hadisələr üçün unikal açar (məsələn
    /// <c>run:{runId}:stage:3</c>). Verilibsə eyni hadisə iki dəfə sayılmır —
    /// nə jurnalda, nə də xassələrdə. <c>null</c> buraxıla bilər.
    /// </param>
    /// <returns>Hadisə YENİ idisə <c>true</c>; təkrar olduğuna görə buraxılıbsa <c>false</c>.</returns>
    Task<bool> TrackAsync(
        Guid childId,
        PetBrainEventType type,
        PetBrainEventData data,
        string? idempotencyKey,
        CancellationToken ct = default);
}
