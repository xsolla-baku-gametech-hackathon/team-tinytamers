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

    public PetBrainExperienceType ExperienceType { get; set; }

    /// <summary>Təsdiqlənmiş mövzu taksonomiyasından.</summary>
    public string Theme { get; set; } = string.Empty;

    public PetBrainDifficulty Difficulty { get; set; }

    public PetBrainRunStatus Status { get; set; } = PetBrainRunStatus.Active;

    /// <summary>Növbəti gözlənilən mərhələnin indeksi. Klient bunu təyin edə bilmir.</summary>
    public int CurrentStage { get; set; }

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
