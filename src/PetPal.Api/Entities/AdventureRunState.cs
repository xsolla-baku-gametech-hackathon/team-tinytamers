using PetPal.Api.PetBrain.Story;

namespace PetPal.Api.Entities;

/// <summary>
/// Bir macəranın TAM icra vəziyyəti — <see cref="ExperienceRun"/> ilə bir-birə.
///
/// <para><b>Nə üçün ayrı sətir?</b> <see cref="ExperienceRun"/> macəranın
/// KİMLİYİdir (kim, hansı şablon, hansı versiya, mükafat verilibmi) və hər
/// tövsiyə sorğusunda oxunur. Vəziyyət isə yalnız macəra oynanarkən lazımdır
/// və böyükdür: inventar, jurnal, məqsədlər, dünya bayraqları, sonluq balları.
/// İkisini bir cədvəldə saxlamaq hər tövsiyə sorğusunu bu sütunları da
/// oxumağa məcbur edərdi.</para>
///
/// <para><b>Niyə JSON sütunlar?</b> Vəziyyət heç vaxt SORĞULANMIR — server onu
/// bütöv oxuyur, bütöv yazır. Ayrı cədvəllər (inventar sətirləri, jurnal
/// sətirləri) beş əlavə birləşmə və beş miqrasiya gətirərdi, üstəlik hər addım
/// tranzaksiyasını böyüdərdi. Struktur <see cref="AdventureState"/> ilə eyni
/// olduğu üçün oxunuş tipli qalır.</para>
/// </summary>
public class AdventureRunState
{
    public Guid Id { get; set; }

    public Guid ExperienceRunId { get; set; }
    public ExperienceRun ExperienceRun { get; set; } = null!;

    /// <summary>Sahiblik sorğuları üçün — run-a qoşulmadan süzülə bilsin.</summary>
    public Guid ChildProfileId { get; set; }

    /// <summary>Hazırda oynanılan chapter; chapter-siz tərifdə boşdur.</summary>
    public string CurrentChapterId { get; set; } = string.Empty;

    /// <summary>
    /// Ən son TƏHLÜKƏSİZ dayanma nöqtəsi.
    ///
    /// <para>Bərpa buradan başlayır, cari düyündən yox: uşaq tapmacanın
    /// ortasında bağlayıb qayıdanda yarımçıq lövhə deyil, səhnənin əvvəlini
    /// görməlidir.</para>
    /// </summary>
    public string CheckpointNodeId { get; set; } = string.Empty;

    public string CheckpointChapterId { get; set; } = string.Empty;

    public DateTime? CheckpointAt { get; set; }

    /// <summary>
    /// Run başlayanda seçilmiş fərdiləşdirmə variantı — <see cref="AdventureVariants"/>.
    ///
    /// <para>İçəridə DƏYİŞMİR. Uşağın ayarı macəra ortasında dəyişsə belə, bu
    /// macəra başladığı formada bitir: səhnələrin qəfil yoxa çıxması hekayəni
    /// pozardı.</para>
    /// </summary>
    public string Variant { get; set; } = AdventureVariants.Standard;

    /// <summary>
    /// Optimistik kilid.
    ///
    /// <para>Hər mutasiyada artır. Klient gördüyü nömrəni geri göndərir; köhnə
    /// nömrə ilə gələn sorğu <c>409</c> alır və ekran yenilənir. Bu, iki cihazda
    /// açıq macəranın bir-birinin addımını səssizcə üstələməsinin qarşısını
    /// alır.</para>
    /// </summary>
    public int Revision { get; set; }

    /// <summary>Faktiki oyun vaxtı — pauza aradakı boşluğu saymır.</summary>
    public int TotalPlaySeconds { get; set; }

    public DateTime? LastPlayedAt { get; set; }
    public DateTime? PausedAt { get; set; }

    public List<string> VisitedNodeIds { get; set; } = new();
    public List<string> CompletedChapterIds { get; set; } = new();
    public List<string> WorldFlags { get; set; } = new();
    public List<string> SelectedChoiceIds { get; set; } = new();

    /// <summary>İnventar — <c>itemId|quantity|nodeId</c> sətirləri.</summary>
    public List<string> Inventory { get; set; } = new();

    /// <summary>Jurnal — <c>clueId|nodeId|isNew</c> sətirləri.</summary>
    public List<string> Clues { get; set; } = new();

    /// <summary>Məqsədlər — <c>objectiveId|status|count</c> sətirləri.</summary>
    public List<string> Objectives { get; set; } = new();

    /// <summary>Sonluq balları — <c>endingKey|score</c> sətirləri.</summary>
    public List<string> EndingScores { get; set; } = new();

    /// <summary>Düyün üzrə təkrar cəhdlər — <c>nodeId|count</c> sətirləri.</summary>
    public List<string> RetryCounts { get; set; } = new();

    /// <summary>NPC vəziyyətləri — <c>npcKey|state</c> sətirləri.</summary>
    public List<string> NpcStates { get; set; } = new();

    /// <summary>
    /// Tətbiq edilmiş idempotentlik açarları.
    ///
    /// <para>Zəif şəbəkədə klient eyni addımı iki dəfə göndərir. Açar burada
    /// olduqda ikinci sorğu heç nə etmir və cari vəziyyəti qaytarır — yəni
    /// uşaq bir seçim üçün iki dəfə əşya almır.</para>
    /// </summary>
    public List<string> AppliedActionKeys { get; set; } = new();
}
