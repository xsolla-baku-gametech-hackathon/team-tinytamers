using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Bir macərada BİR addımın nəticəsi.
///
/// <para>Əvvəllər həqiqətin mənbəyi <see cref="ExperienceRun.Choices"/> — düz
/// bir siyahı — idi. Orada "continue", "solved" və seçim açarları qarışırdı,
/// yəni xülasə qurmaq üçün indekslə təxmin etmək lazım gəlirdi: üçüncü element
/// hansı mərhələyə aiddir? Budaqlanan hekayədə bu sual ümumiyyətlə cavabsızdır,
/// çünki iki uşaq eyni sayda addım atmır.</para>
///
/// <para>Bu sətir isə dəqiqdir: hansı düyün, neçənci addım, hansı variant,
/// tapmaca nəticəsi, neçə cəhd, neçə ipucu.</para>
///
/// <para>(run, düyün) cütü UNİKALDIR: eyni addım iki dəfə tətbiq oluna bilmir,
/// yəni təkrar göndərilən sorğu nəticəni ikiqat saymır.</para>
/// </summary>
public class RunStageOutcome
{
    public Guid Id { get; set; }

    public Guid ExperienceRunId { get; set; }
    public ExperienceRun ExperienceRun { get; set; } = null!;

    /// <summary>Sahiblik sorğuları üçün — run-dan asılı olmadan süzülə bilsin.</summary>
    public Guid ChildProfileId { get; set; }

    /// <summary>Qraf düyününün açarı; xətti şablonda mərhələnin sintetik açarı.</summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>Uşağın neçənci addımıdır — xülasənin sırası bundan qurulur.</summary>
    public int StageOrdinal { get; set; }

    public PetBrainStageKind Kind { get; set; }

    /// <summary>Seçilmiş variant açarları (seçim mərhələsində bir dənə).</summary>
    public List<string> SelectedOptionKeys { get; set; } = new();

    /// <summary>Tapmaca nəticəsi; tapmaca deyilsə <c>None</c>.</summary>
    public PetBrainStageResult Result { get; set; } = PetBrainStageResult.None;

    public int Attempts { get; set; }
    public int HintsUsed { get; set; }

    /// <summary>Bu addımın tətbiq etdiyi effekt açarları — bayraqların mənbəyi.</summary>
    public List<string> EffectKeys { get; set; } = new();

    public DateTime CompletedAt { get; set; }
}
