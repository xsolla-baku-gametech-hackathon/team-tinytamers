using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Bir tövsiyə QƏRARININ qeydi — serverin nə təklif etdiyi və uşağın nə cavab
/// verdiyi.
///
/// <para><b>Nə üçün lazımdır?</b> Əvvəllər tövsiyəni başlatmağın ÖZÜ həmin
/// mövzunun maraq balını qaldırırdı və göstərilmə ilə seçilmə arasında fərq
/// yox idi. Nəticə özünü təsdiqləyən dövrə idi: sistem kosmos təklif edir,
/// uşaq başqa seçim görmür, sistem "deməli kosmosu sevir" deyirdi.</para>
///
/// <para>İndi qərar sətri əvvəlcədən yazılır və uşağın cavabı ona bağlanır.
/// Klient nə şablon, nə bal dəyişikliyi göndərə bilir — yalnız serverin verdiyi
/// <see cref="Id"/>-ni geri qaytarır.</para>
///
/// <para><b>PII yoxdur:</b> nə ad, nə söhbət, nə yaddaş cümləsi. Yalnız açarlar,
/// ballar və kontekstin hash-ı.</para>
/// </summary>
public class RecommendationDecision
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    /// <summary>Qərarı verən siyasətin versiyası — balans dəyişəndə artır.</summary>
    public int PolicyVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Kontekstin barmaq izi. Xam dəyər YOX, hash: eyni vəziyyətin eyni qərarı
    /// verdiyini sonradan yoxlamaq üçün kifayətdir və heç nə sızdırmır.
    /// </summary>
    public string ContextHash { get; set; } = string.Empty;

    /// <summary>Bu qərarda baxılan namizədlərin açarları, bal sırası ilə.</summary>
    public List<string> CandidateKeys { get; set; } = new();

    /// <summary>Seçilən şablon.</summary>
    public string SelectedTemplateKey { get; set; } = string.Empty;

    /// <summary>Seçimin bal komponentləri — nümayiş panelində olduğu kimi.</summary>
    public int FitScore { get; set; }
    public int NoveltyScore { get; set; }
    public int SurpriseScore { get; set; }

    public PetBrainDifficulty Difficulty { get; set; }

    /// <summary>
    /// Uşağın cavabı. Standart <c>Shown</c>-dur: kartın görünməsi hələ
    /// ÜSTÜNLÜK deyil.
    /// </summary>
    public PetBrainRecommendationFeedback Feedback { get; set; } = PetBrainRecommendationFeedback.Shown;

    public DateTime? FeedbackAt { get; set; }

    /// <summary>
    /// Bu qərar neçənci kartdır. <c>0</c> — ilk təklif; hər "başqa fikir"
    /// növbəti sıra nömrəsini yaradır.
    /// </summary>
    public int Ordinal { get; set; }
}
