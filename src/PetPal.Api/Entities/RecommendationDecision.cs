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

    /// <summary>
    /// Eyni ekranda GÖSTƏRİLƏN kartların ortaq id-si.
    ///
    /// <para>Ekran bir əsas və bir neçə alternativ göstərir. Hər kartın öz
    /// qərar sətri var (uşaq hər hansı birini seçə bilər), amma hamısı eyni
    /// baxışın parçasıdır — «uşaq əsas təklifi seçmədi, yanındakını seçdi»
    /// sualı yalnız bu qrupla cavablana bilir.</para>
    /// </summary>
    public Guid GroupId { get; set; }

    /// <summary>Bu kartın siyahıdakı ROLU — əsas, davam, yaxın kəşf, sürpriz.</summary>
    public PetBrainRecommendationSlot Slot { get; set; } = PetBrainRecommendationSlot.Primary;

    /// <summary>
    /// Bu kart KƏŞF payından gəldimi (uyğunluq sırasından yox).
    ///
    /// <para>Ölçmə üçün vacibdir: kəşf kartının rədd edilməsi siyasətin
    /// səhvi deyil, onun qiymətidir — ikisini qarışdırmaq sistemi getdikcə
    /// daha ehtiyatlı və daha darıxdırıcı edərdi.</para>
    /// </summary>
    public bool WasExploration { get; set; }

    /// <summary>
    /// Qərar anında profilin ÜMUMİ inamı (0–100).
    ///
    /// <para>Kalibrləmə üçün: «aşağı inamla verilən təkliflər həqiqətənmi
    /// daha çox rədd olunur?» sualı yalnız bu sahə ilə cavablanır.</para>
    /// </summary>
    public int ProfileConfidence { get; set; }

    /// <summary>Bu qərarda baxılan namizədlərin açarları, bal sırası ilə.</summary>
    public List<string> CandidateKeys { get; set; } = new();

    /// <summary>
    /// SƏRT şərtdən keçməyən namizədlər: <c>"açar=səbəb"</c>.
    ///
    /// <para>Sərbəst mətn deyil — səbəb <see cref="PetBrainFilterReason"/>
    /// adıdır. «Niyə bu macəra göstərilmədi?» sualı bununla cavablanır və
    /// jurnala uşağa aid heç nə düşmür.</para>
    /// </summary>
    public List<string> FilteredCandidates { get; set; } = new();

    /// <summary>
    /// «Niyə bunu göstərirəm?» səbəb KODLARI
    /// (<see cref="PetBrainWhyReason"/> adları).
    ///
    /// <para>Cümlə deyil, kod saxlanılır: izah tərcümə oluna bilir və jurnal
    /// dil dəyişəndə köhnəlmir.</para>
    /// </summary>
    public List<string> WhyReasons { get; set; } = new();

    /// <summary>Seçilən şablon.</summary>
    public string SelectedTemplateKey { get; set; } = string.Empty;

    /// <summary>Seçimin bal komponentləri — nümayiş panelində olduğu kimi.</summary>
    public int FitScore { get; set; }
    public int NoveltyScore { get; set; }
    public int SurpriseScore { get; set; }

    /// <summary>Mövzu uyğunluğu (0–100).</summary>
    public int TopicFit { get; set; }

    /// <summary>Mexanika uyğunluğu (0–100) — mövzudan AYRI.</summary>
    public int MechanicFit { get; set; }

    /// <summary>Çətinliyin uşağın ustalığına uyğunluğu (0–100).</summary>
    public int MasteryChallengeFit { get; set; }

    /// <summary>Lazım olan dəstəyin mövcudluğu (0–100).</summary>
    public int SupportFit { get; set; }

    /// <summary>Sessiya uzunluğu və tempə uyğunluq (0–100).</summary>
    public int PaceFit { get; set; }

    /// <summary>Yarımçıq hekayənin davamı olma dərəcəsi (0–100).</summary>
    public int ContinuityFit { get; set; }

    /// <summary>Mükafat növünün uşağın seçiminə uyğunluğu (0–100).</summary>
    public int RewardFit { get; set; }

    /// <summary>Təkrar cəzası (0–100, çıxılır).</summary>
    public int RepetitionPenalty { get; set; }

    /// <summary>Yekun bal — çəkilər <c>PolicyVersion</c> ilə versiyalanır.</summary>
    public int TotalScore { get; set; }

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
