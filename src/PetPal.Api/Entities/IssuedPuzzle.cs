using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Bir uşağa, bir run-ın bir mərhələsi üçün VERİLMİŞ tapmaca.
///
/// <para>Tapmaca yaddaşda saxlanılmır, çünki üç şey ondan asılıdır:</para>
/// <list type="number">
///   <item><b>Bərpa.</b> Uşaq ekranı yeniləyəndə EYNİ tapmaca qayıtmalıdır —
///   yenidən generasiya "asan sual ovlamağa" imkan verərdi.</item>
///   <item><b>Cavabın həqiqəti.</b> Doğru həll YALNIZ burada,
///   <see cref="PrivateSolution"/> sahəsindədir və heç bir DTO onu daşımır.</item>
///   <item><b>Təkrarın qarşısı.</b> <see cref="ContentSignature"/> son
///   tapmacalarla müqayisə olunur, yəni uşaq eyni sualı dalbadal görmür.</item>
/// </list>
/// </summary>
public class IssuedPuzzle
{
    public Guid Id { get; set; }

    /// <summary>Sahiblik — yad uşağın tapmacası <c>404</c> alır.</summary>
    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public Guid ExperienceRunId { get; set; }
    public ExperienceRun ExperienceRun { get; set; } = null!;

    /// <summary>Run daxilində mərhələnin indeksi — (run, mərhələ) cütü unikaldır.</summary>
    public int StageIndex { get; set; }

    /// <summary>Kataloqdakı şablonun açarı.</summary>
    public string BlueprintKey { get; set; } = string.Empty;

    /// <summary>
    /// Şablonun versiyası. Qaydalar dəyişəndə köhnə tapmacalar öz versiyası ilə
    /// qalır — yəni yarımçıq run başqa cür qiymətləndirilmir.
    /// </summary>
    public int BlueprintVersion { get; set; }

    /// <summary>Generatorun versiyası — toxum müqaviləsi dəyişsə fərq görünsün.</summary>
    public int GeneratorVersion { get; set; }

    public PetBrainPuzzleMechanic Mechanic { get; set; }

    /// <summary>
    /// Kanonik toxumun hex təsviri (SHA-256, 64 simvol). Eyni toxum eyni
    /// tapmacanı bərpa edir — bax <see cref="PetBrain.Puzzles.PuzzleSeed"/>.
    /// </summary>
    public string Seed { get; set; } = string.Empty;

    /// <summary>Uşağa göndərilən CAVABSIZ məzmun (JSON).</summary>
    public string PublicPayload { get; set; } = string.Empty;

    /// <summary>
    /// Doğru həll (JSON). <b>Heç bir DTO-ya, heç bir endpoint-ə, nümayiş
    /// panelinə də düşmür.</b> Yalnız serverdəki qiymətləndirici oxuyur.
    /// </summary>
    public string PrivateSolution { get; set; } = string.Empty;

    /// <summary>Məzmunun barmaq izi — son tapmacalarla təkrar müqayisəsi üçün.</summary>
    public string ContentSignature { get; set; } = string.Empty;

    /// <summary>
    /// Hekayə səhnəsinin kanonik hash-ı (bax <see cref="PetBrain.Puzzles.PuzzleSceneSpec"/>).
    ///
    /// <para>Rəsm ayrıca cədvəldədir və PAYLAŞILA bilər, çünki səhnə təsvirində
    /// uşağa aid heç nə yoxdur. Bu, həm də keşin açarıdır: yenilənmədən sonra
    /// uşaq eyni səhnəni görür və eyni səhnə üçün ikinci pullu sorğu getmir.</para>
    /// </summary>
    public string SceneSpecHash { get; set; } = string.Empty;

    public PetBrainDifficulty Difficulty { get; set; }

    /// <summary>Dəstək rejimi: az variant, ipucu əvvəldən görünür.</summary>
    public bool Assisted { get; set; }

    public int Attempts { get; set; }
    public int HintsUsed { get; set; }

    public PetBrainPuzzleStatus Status { get; set; } = PetBrainPuzzleStatus.Issued;

    /// <summary>Vaxt HƏMİŞƏ serverdən — klientin bildirdiyi müddət səlahiyyətli deyil.</summary>
    public DateTime IssuedAt { get; set; }
    public DateTime? SolvedAt { get; set; }
}
