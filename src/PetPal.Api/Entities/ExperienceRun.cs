using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Bir təcrübənin gedişatı. Həm QISAMÜDDƏTLİ yaddaşdır (cari mərhələ, son seçim,
/// ipucu sayı), həm də mükafatın bir dəfə verilməsinin qeydidir.
///
/// <para>Qısamüddətli vəziyyət qəsdən bazadadır: uşaq ekranı yeniləyəndə,
/// app-i bağlayıb açanda və ya telefonu dəyişəndə macərası itməməlidir.</para>
/// </summary>
public class ExperienceRun
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    /// <summary>Kataloqdakı şablonun açarı — klientdən gələn ad yox, serverin təsdiqlədiyi.</summary>
    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>
    /// Run BAŞLAYANDA qüvvədə olan tərif versiyası.
    ///
    /// <para>Deploy kataloqu dəyişdirə bilər. Bu sahə olmasaydı, uşağın açıq
    /// macərası yenilənmədən sonra başqa mərhələ sayı və başqa variant açarları
    /// ilə davam edərdi — yəni yarımçıq run səssizcə pozulardı.</para>
    ///
    /// <para><c>0</c> = versiyalaşdırmadan ƏVVƏLki sətir; kataloqun cari
    /// versiyası ilə oxunur (bax <see cref="PetBrain.ExperienceCatalog.Resolve"/>).</para>
    /// </summary>
    public int DefinitionVersion { get; set; }

    public PetBrainExperienceType ExperienceType { get; set; }

    /// <summary>Təsdiqlənmiş mövzu taksonomiyasından.</summary>
    public string Theme { get; set; } = string.Empty;

    public PetBrainDifficulty Difficulty { get; set; }

    public PetBrainRunStatus Status { get; set; } = PetBrainRunStatus.Active;

    /// <summary>Növbəti gözlənilən mərhələnin indeksi. Klient bunu təyin edə bilmir.</summary>
    public int CurrentStage { get; set; }

    /// <summary>
    /// Qrafda dayandığımız düyün. Xətti şablonlarda boşdur.
    ///
    /// <para>Xətti <see cref="CurrentStage"/> ilə birlikdə saxlanılır: köhnə
    /// run-lar mərhələ indeksi ilə, qraf run-ları isə düyün açarı ilə davam
    /// edir və adapter ikisini eyni ekrana çevirir.</para>
    /// </summary>
    public string CurrentNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Hekayənin BAYRAQLARI — seçimlərin sonrakı düyünlərə daşınan nəticəsi.
    ///
    /// <para>Bayraq açarları QAPALIDIR (tərifdəki effektlərdən gəlir); klient
    /// nə bayraq qoya, nə də silə bilir.</para>
    /// </summary>
    public List<string> StoryFlags { get; set; } = new();

    /// <summary>
    /// Macərənin hansı sonluqla bitdiyi. Bitməyibsə boşdur.
    /// </summary>
    public string EndingKey { get; set; } = string.Empty;

    /// <summary>
    /// Bu run-ı doğuran tövsiyə qərarı — ölçmə üçün.
    ///
    /// <para>Qərar sətri PII saxlamır (bax <see cref="RecommendationDecision"/>),
    /// burada isə yalnız onun id-si durur: "göstərildi → başlandı" çevrilməsi
    /// məhz bu bağ ilə hesablanır.</para>
    /// </summary>
    public Guid? DecisionId { get; set; }

    /// <summary>Seçilmiş variantların açarları, mərhələ sırası ilə.</summary>
    public List<string> Choices { get; set; } = new();

    public int HintsUsed { get; set; }
    public int Mistakes { get; set; }

    /// <summary>0–100. Yaradıcı təcrübədə həmişə 100-dür: orada doğru/səhv yoxdur.</summary>
    public int ScorePercent { get; set; }

    /// <summary>
    /// Bu run üçün verilmiş tapmacalar. Sual, həll və barmaq izi orada
    /// saxlanılır — bax <see cref="IssuedPuzzle"/>.
    /// </summary>
    public ICollection<IssuedPuzzle> Puzzles { get; set; } = new List<IssuedPuzzle>();

    /// <summary>
    /// Atılan addımların REAL qeydi — xülasə və yaddaş buradan qurulur.
    ///
    /// <para><see cref="Choices"/> düz siyahısı budaqlanan hekayədə yetərli
    /// deyil: orada "hansı seçim hansı mərhələyə aiddir" sualının cavabı
    /// yoxdur, çünki iki uşaq eyni sayda addım atmır.</para>
    /// </summary>
    public ICollection<RunStageOutcome> StageOutcomes { get; set; } = new List<RunStageOutcome>();

    /// <summary>
    /// Chapter-li macəranın TAM vəziyyəti — inventar, jurnal, məqsədlər,
    /// checkpoint. Chapter-siz (köhnə, qısa) macərada <c>null</c>.
    ///
    /// <para>Ayrı sətirdə olmasının səbəbi <see cref="AdventureRunState"/>
    /// sənədindədir: bu sütunlar yalnız macəra OYNANARKƏN lazımdır, run sətri
    /// isə hər tövsiyə sorğusunda oxunur.</para>
    /// </summary>
    public AdventureRunState? State { get; set; }

    /// <summary>
    /// Əlavə dəstək rejimi: tapmacada variant sayı azdır və ipucu əvvəldən
    /// görünür. Qərar run BAŞLAYANDA verilir və içəridə DƏYİŞMİR — uşaq
    /// oynadığı sualın formasının qəfil dəyişməsini görməməlidir.
    /// </summary>
    public bool Assisted { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Mükafat tətbiq olunubmu. Tək açar ilə kifayətlənmirik: tamamlama
    /// tranzaksiya içində və <see cref="Status"/> yoxlanışı ilə birlikdə yazılır,
    /// yəni iki eyni vaxtlı sorğudan yalnız biri XP/bağ/kosmetika verə bilir.
    /// </summary>
    public bool RewardApplied { get; set; }
}
