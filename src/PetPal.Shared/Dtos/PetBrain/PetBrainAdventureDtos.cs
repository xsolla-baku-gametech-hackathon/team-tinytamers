using PetPal.Shared.Enums;

namespace PetPal.Shared.Dtos.PetBrain;

/// <summary>
/// Bir chapter-in uşağa görünən vəziyyəti — xəritə və irəliləmə üçün.
/// </summary>
public class PetBrainChapterDto
{
    public string ChapterId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Tamamlanandan sonra göstərilən xülasə; bağlı chapter-də boşdur.</summary>
    public string Summary { get; set; } = string.Empty;

    public int EstimatedMinutes { get; set; }
    public AdventureChapterStatus Status { get; set; }
}

/// <summary>
/// İzləyicidəki bir MƏQSƏD.
///
/// <para>Ekranda eyni anda ən çox üç dənə görünür — uzun siyahı uşağı yükləyir
/// və macərəni tapşırıq idarəçiliyinə çevirir.</para>
/// </summary>
public class PetBrainObjectiveDto
{
    public string ObjectiveId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AdventureObjectiveStatus Status { get; set; }

    /// <summary>Neçəsi bitib — sayla ölçülən məqsədlərdə.</summary>
    public int CurrentCount { get; set; }
    public int RequiredCount { get; set; }

    /// <summary>Yan tapşırıqdır — buraxılsa macəra bağlanmır.</summary>
    public bool IsOptional { get; set; }
}

/// <summary>İnventardakı bir əşya.</summary>
public class PetBrainInventoryItemDto
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int Quantity { get; set; }

    /// <summary>Hekayə üçün vacibdir — sərf olunmur.</summary>
    public bool IsQuestItem { get; set; }

    /// <summary>Bu addımda YENİ düşdü — ekran onu işarələyir.</summary>
    public bool IsNew { get; set; }
}

/// <summary>Jurnaldakı bir ipucu.</summary>
public class PetBrainClueDto
{
    public string ClueId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public AdventureClueImportance Importance { get; set; }

    /// <summary>Hələ oxunmayıb.</summary>
    public bool IsNew { get; set; }
}

/// <summary>
/// Bir addımın DƏYİŞDİRDİKLƏRİ — ekran nəyi bildirməlidir.
///
/// <para>Tam siyahılardan ayrıdır: uşağa «inventarında 6 əşya var» yox,
/// <b>«indicə xəritə tapdın»</b> demək lazımdır. Fərqi serverin göstərməsi
/// klienti iki siyahını tutuşdurmaq işindən azad edir.</para>
/// </summary>
public class PetBrainStepChangesDto
{
    public List<PetBrainInventoryItemDto> GainedItems { get; set; } = new();
    public List<string> LostItemNames { get; set; } = new();
    public List<PetBrainClueDto> NewClues { get; set; } = new();
    public List<PetBrainObjectiveDto> CompletedObjectives { get; set; } = new();

    /// <summary>Bu addımda bağlanan chapter; boş = bağlanmadı.</summary>
    public string CompletedChapterId { get; set; } = string.Empty;

    public bool IsEmpty =>
        GainedItems.Count == 0
        && LostItemNames.Count == 0
        && NewClues.Count == 0
        && CompletedObjectives.Count == 0
        && string.IsNullOrEmpty(CompletedChapterId);
}

/// <summary>
/// Macəranın chapter-li vəziyyəti — HUD, xəritə və bərpa ekranı bunu oxuyur.
/// </summary>
public class PetBrainAdventureStateDto
{
    /// <summary>Optimistik kilid. Klient onu geri göndərir; köhnə nömrə <c>409</c> alır.</summary>
    public int Revision { get; set; }

    public string CurrentChapterId { get; set; } = string.Empty;
    public string CurrentChapterTitle { get; set; } = string.Empty;
    public int CurrentChapterOrder { get; set; }
    public int ChapterCount { get; set; }

    /// <summary>0–100, TAMAMLANMIŞ chapter sayından — addım sayından yox.</summary>
    public int ProgressPercent { get; set; }

    /// <summary>Qalan chapter-lərin təxmini dəqiqəsi — «nə qədər qalıb».</summary>
    public int RemainingMinutes { get; set; }

    public List<PetBrainChapterDto> Chapters { get; set; } = new();

    /// <summary>Ekranda göstəriləcək 1–3 məqsəd.</summary>
    public List<PetBrainObjectiveDto> Objectives { get; set; } = new();

    public List<PetBrainInventoryItemDto> Inventory { get; set; } = new();
    public List<PetBrainClueDto> Clues { get; set; } = new();

    /// <summary>Oxunmamış ipucu sayı — jurnal düyməsindəki nişan.</summary>
    public int UnreadClues { get; set; }

    /// <summary>Ən son təhlükəsiz dayanma nöqtəsi varmı.</summary>
    public bool HasCheckpoint { get; set; }

    /// <summary>Cari ekran özü checkpoint-dir — «indi dayana bilərsən».</summary>
    public bool IsAtCheckpoint { get; set; }

    /// <summary>Fərdiləşdirmə variantı — <c>short</c>, <c>standard</c>, <c>long</c>.</summary>
    public string Variant { get; set; } = string.Empty;

    /// <summary>Bu addımın dəyişdirdikləri; dəyişməyibsə boş.</summary>
    public PetBrainStepChangesDto Changes { get; set; } = new();
}

/// <summary>
/// Yarımçıq macəranın BƏRPA kartı.
///
/// <para>Uşaq bir həftə sonra qayıda bilər, ona görə burada «davam et»
/// düyməsindən artıq şey var: harada qaldığı, nə etməli olduğu və nəyi
/// daşıdığı. Xülasə serverin SAXLADIĞI faktlardan qurulur — pet uydurmur.</para>
/// </summary>
public class PetBrainResumeDto
{
    public Guid RunId { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SceneKey { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    public string ChapterTitle { get; set; } = string.Empty;
    public int ChapterOrder { get; set; }
    public int ChapterCount { get; set; }
    public int ProgressPercent { get; set; }
    public int RemainingMinutes { get; set; }

    /// <summary>Son baş verənin qısa, DETERMİNİST xülasəsi.</summary>
    public string LastEventSummary { get; set; } = string.Empty;

    /// <summary>Hazırkı əsas məqsəd.</summary>
    public string CurrentObjective { get; set; } = string.Empty;

    /// <summary>Çantadakı əsas əşyalar — ən çox üç dənə.</summary>
    public List<PetBrainInventoryItemDto> KeyItems { get; set; } = new();

    public DateTime? LastPlayedAt { get; set; }
    public PetBrainRunStatus Status { get; set; }
}

/// <summary>Bir chapter bitəndə göstərilən ekranın məlumatı.</summary>
public class PetBrainChapterCompleteDto
{
    public string ChapterId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>Nə baş verdi — chapter-in öz xülasəsi.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Uşağın bu chapter-də etdiyi mühüm seçimlər.</summary>
    public List<string> KeyChoices { get; set; } = new();

    public List<PetBrainObjectiveDto> CompletedObjectives { get; set; } = new();

    /// <summary>Buraxılmış yan tapşırıqlar — cəza deyil, xatırlatma.</summary>
    public List<PetBrainObjectiveDto> MissedOptional { get; set; } = new();

    public List<PetBrainInventoryItemDto> GainedItems { get; set; } = new();
    public List<PetBrainClueDto> NewClues { get; set; } = new();

    /// <summary>Növbəti chapter-in adı — «sonra nə olacaq» qarmağı.</summary>
    public string NextChapterTitle { get; set; } = string.Empty;
    public int NextChapterMinutes { get; set; }

    /// <summary>Macəranın sonuncu chapter-i idimi.</summary>
    public bool IsFinalChapter { get; set; }
}

/// <summary>
/// Fəsilli macəranın kartı — macəra mərkəzində.
///
/// <para>Uzun macəra qısa tövsiyə kartından fərqli sual cavablandırır: «bu
/// nə qədər sürər», «neçə fəsildir», «harada qalmışam», «neçə sonluq
/// tapmışam». Kart bunları uşaq macəraya girmədən göstərir.</para>
/// </summary>
public class PetBrainAdventureSummaryDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Intro { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string SceneKey { get; set; } = string.Empty;

    public int ChapterCount { get; set; }

    /// <summary>Bütün macəranın təxmini dəqiqəsi.</summary>
    public int EstimatedMinutes { get; set; }

    /// <summary>Bir oturuşun təxmini dəqiqəsi — bir fəsil.</summary>
    public int SessionMinutes { get; set; }

    /// <summary>Oyun mexanikalarının uşağın dilində adları.</summary>
    public List<string> Mechanics { get; set; } = new();

    /// <summary>Qazanıla bilən ünvanlar — hər sonluq bir ünvan.</summary>
    public List<string> RewardNames { get; set; } = new();

    public PetBrainAdventureProgress Progress { get; set; }

    /// <summary>Açıq run-ın irəliləməsi (0–100); açıq run yoxdursa 0.</summary>
    public int ProgressPercent { get; set; }

    public int EndingsFound { get; set; }
    public int EndingsTotal { get; set; }

    /// <summary>Davam etdirilə bilən run; yoxdursa <c>null</c>.</summary>
    public Guid? OpenRunId { get; set; }

    /// <summary>
    /// Buradan başlamaq/davam etmək mümkündürmü.
    ///
    /// <para>Macəra mərkəzi direktoru ƏVƏZ ETMİR: yeni macəra yalnız pet onu
    /// təklif edəndə başlayır. Davam etmək və bitirilmiş macərəni təkrar
    /// oynamaq isə həmişə açıqdır.</para>
    /// </summary>
    public bool CanStart { get; set; }

    /// <summary>Düymənin yanındakı qısa izah — nə üçün açıq və ya bağlıdır.</summary>
    public string StartHint { get; set; } = string.Empty;
}

/// <summary>Macəranın ÖN BAXIŞI — başlamazdan əvvəl.</summary>
public class PetBrainAdventurePreviewDto
{
    public PetBrainAdventureSummaryDto Summary { get; set; } = new();

    /// <summary>Fəsillər — adları görünür, məzmunu isə yalnız keçiləndən sonra.</summary>
    public List<PetBrainChapterDto> Chapters { get; set; } = new();

    /// <summary>Artıq tapılmış sonluqların adları.</summary>
    public List<string> EndingsFound { get; set; } = new();

    /// <summary>Əlçatanlıq dəstəyi — valideyn və uşaq əvvəlcədən bilsin.</summary>
    public List<string> Accessibility { get; set; } = new();

    /// <summary>«Hər fəsil təxminən 8 dəqiqə» tipli cümlə.</summary>
    public string PaceLabel { get; set; } = string.Empty;
}

/// <summary>
/// Macəranın EPİLOQU — sonluqdan sonra nə qaldı.
///
/// <para>Uzun macərada «bitdi, +120 xp» azdır: uşaq nəyi dəyişdiyini, kim
/// olduğunu, nəyi buraxdığını və bundan sonra nə gələcəyini görməlidir. Hər
/// sahə serverin SAXLADIĞI faktdan qurulur — heç nə uydurulmur.</para>
/// </summary>
public class PetBrainEpilogueDto
{
    public string EndingKey { get; set; } = string.Empty;
    public string EndingTitle { get; set; } = string.Empty;

    /// <summary>Uşağın qazandığı ünvan — «Ayın Qoruyucusu».</summary>
    public string EarnedTitle { get; set; } = string.Empty;

    /// <summary>Pet-in xatırlatdığı, macəranın başındakı həlledici seçim.</summary>
    public string PetRecall { get; set; } = string.Empty;

    /// <summary>Macəradan sonra dünyada qalan izlər.</summary>
    public List<string> WorldChanges { get; set; } = new();

    public int EndingsFound { get; set; }
    public int EndingsTotal { get; set; }

    /// <summary>Buraxılmış yan tapşırıqlar — təkrar oyunun səbəbi, qınaq yox.</summary>
    public List<string> MissedSideQuests { get; set; } = new();

    /// <summary>Hələ tapılmamış sonluğa işarə.</summary>
    public string ReplayHint { get; set; } = string.Empty;

    /// <summary>Növbəti macəranın qarmağı.</summary>
    public string SequelHook { get; set; } = string.Empty;
}
