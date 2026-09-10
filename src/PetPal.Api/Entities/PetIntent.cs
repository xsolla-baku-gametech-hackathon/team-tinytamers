using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Pet-in ÖZ niyyəti — uşaq baxmayanda nə etdiyi.
///
/// <para><b>Nə üçün saxlanılır?</b> "Sən yoxkən nə etdim" cümləsi yalnız
/// HƏQİQƏTƏN baş vermiş bir şeyə söykənəndə mənalıdır. Niyyət qabaqcadan
/// yazılır, vaxtı gələndə tamamlanır və yalnız ondan sonra danışılır — əks
/// halda pet hər açılışda uydururdu.</para>
///
/// <para><b>Fon işçisi YOXDUR.</b> Niyyət app açılanda <see cref="TimeProvider"/>
/// ilə irəlilədilir: nəticə determinist qalır, test onu saatı sürüşdürərək
/// yoxlaya bilir və server boş yerə iş görmür.</para>
///
/// <para><b>Emosional təzyiq qadağandır.</b> Nəticə açarları qapalı siyahıdadır
/// və heç biri uşağı günahlandırmır — "məni tək qoydun" tipli cümlə burada
/// ümumiyyətlə mövcud deyil (bax <c>PetIntentVoice</c> və onun testi).</para>
/// </summary>
public class PetIntent
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public PetBrainIntentType Type { get; set; }

    /// <summary>
    /// Niyyətin SƏBƏBİ — təsdiqlənmiş açar (<c>low-energy</c>, <c>mission</c>).
    /// Sərbəst mətn deyil: uşağa göstərilən cümlə bundan qurulur.
    /// </summary>
    public string ReasonKey { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    /// <summary>Nə vaxt bitəcəyi — irəliləmə bunu saatla müqayisə edir.</summary>
    public DateTime ExpectedEndAt { get; set; }

    public PetBrainIntentStatus Status { get; set; } = PetBrainIntentStatus.Active;

    /// <summary>Niyyəti doğuran xatirə; yoxdursa boş.</summary>
    public Guid? RelatedMemoryId { get; set; }

    /// <summary>Niyyəti doğuran missiya kodu; yoxdursa boş.</summary>
    public string RelatedMissionKey { get; set; } = string.Empty;

    /// <summary>
    /// Nəticənin açarı — TƏSDİQLƏNMİŞ artefakt siyahısından
    /// (<c>pebble</c>, <c>sketch</c>, <c>nap</c>). Niyyət bitməyibsə boş.
    /// </summary>
    public string OutcomeKey { get; set; } = string.Empty;

    /// <summary>Planlayıcının versiyası — qaydalar dəyişəndə artır.</summary>
    public int PlannerVersion { get; set; }

    /// <summary>Uşağa GÖSTƏRİLİBMİ — eyni nəticə iki dəfə danışılmır.</summary>
    public DateTime? SeenAt { get; set; }
}
