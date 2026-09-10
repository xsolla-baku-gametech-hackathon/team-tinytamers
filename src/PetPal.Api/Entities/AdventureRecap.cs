using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Tamamlanmış macəranın 10 saniyəlik recap videosu.
///
/// <para>Açar <see cref="RecapSpecHash"/>-dır və UNİKALDIR: eyni seçimlər eyni
/// videonu verir, deməli <b>ikinci pullu iş başlamır</b>. Təkrar oynanan run
/// keşdən gəlir və heç nə xərclənmir.</para>
///
/// <para>Sətir tamamlama tranzaksiyasından SONRA yaradılır: XP, bağ, xatirə və
/// kosmetik dərhal və dəqiq bir dəfə verilir, video isə arxa fonda gəlir. Uşaq
/// mükafatını gözləmir.</para>
/// </summary>
public class AdventureRecap
{
    public Guid Id { get; set; }

    /// <summary>Seçimlərin kanonik hash-ı — UNİKAL.</summary>
    public string RecapSpecHash { get; set; } = string.Empty;

    /// <summary>Sahiblik: yad uşaq videonu görə bilmir.</summary>
    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    /// <summary>Videonu doğuran run — sahiblik və nümayiş üçün.</summary>
    public Guid ExperienceRunId { get; set; }

    public string ExperienceKey { get; set; } = string.Empty;

    public int SpecVersion { get; set; }

    public PetBrainRecapStatus Status { get; set; } = PetBrainRecapStatus.Pending;

    /// <summary>
    /// Provayderin tapşırıq id-si.
    ///
    /// <para>Prosesin yenidən başlaması işi İTİRMİR: sətir <c>Generating</c>
    /// qalır və işçi məhz bu id ilə davam edir — yəni ikinci dəfə pul
    /// xərclənmir. Klientə heç vaxt verilmir.</para>
    /// </summary>
    public string ProviderJobId { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    public int PromptTemplateVersion { get; set; }
    public string PromptHash { get; set; } = string.Empty;

    /// <summary>App-in öz saxlancındakı açar. Provayderin URL-i SAXLANMIR.</summary>
    public string AssetKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Yoxlanmış müddət (millisaniyə) — başlıqdan yox, fayldan.</summary>
    public int DurationMs { get; set; }

    /// <summary>Razılaşdırılmış ən pis hal krediti.</summary>
    public int EstimatedCredits { get; set; }

    /// <summary>Provayder bildirsə — HƏQİQİ kredit. Böyüklərin panelinə düşür.</summary>
    public int RealizedCredits { get; set; }

    /// <summary>Uğursuzluğun səbəbi — uşağa GÖSTƏRİLMİR.</summary>
    public string FailureReason { get; set; } = string.Empty;

    /// <summary>Neçə dəfə cəhd edilib — sonsuz təkrarın qarşısını alır.</summary>
    public int Attempts { get; set; }

    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
