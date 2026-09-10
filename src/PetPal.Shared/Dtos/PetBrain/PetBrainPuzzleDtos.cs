using PetPal.Shared.Enums;

namespace PetPal.Shared.Dtos.PetBrain;

/// <summary>
/// Uşağa göndərilən tapmaca — <b>CAVABSIZ</b>.
///
/// <para>Burada nə doğru cavab, nə həll, nə də onu bərpa etməyə imkan verən
/// gizli çəki var. Həll yalnız serverdə, ayrıca sahədə saxlanılır və heç bir
/// endpoint onu qaytarmır (nümayiş paneli də daxil olmaqla).</para>
/// </summary>
public class PetBrainPuzzleDto
{
    public Guid PuzzleId { get; set; }

    /// <summary>Qapalı siyahıdan — UI hansı komponenti çəkəcəyini bundan bilir.</summary>
    public PetBrainPuzzleMechanic Mechanic { get; set; }

    /// <summary>Kataloqdakı şablonun açarı (məsələn <c>mars-signal-route</c>).</summary>
    public string BlueprintKey { get; set; } = string.Empty;

    public int BlueprintVersion { get; set; }

    /// <summary>Tapmacanın başlığı (uşağın dilində).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Hekayə ilə bağlayan cümlə: «Dronu əvvəl enerji stansiyasına, sonra
    /// antenaya, sonda Roboya çatdır.»
    ///
    /// <para>Bu, göstərişdən fərqlidir: göstəriş MEXANİKANI, bu isə NİYƏ-ni
    /// izah edir. Tapmaca hekayənin problemini həll etməlidir, ayrıca
    /// viktorina olmamalıdır.</para>
    /// </summary>
    public string StoryPrompt { get; set; } = string.Empty;

    /// <summary>Hekayə səhnəsi — AI rəsm və ya deterministik ehtiyat.</summary>
    public PetBrainSceneDto Scene { get; set; } = new();

    /// <summary>Ekranın ən böyük yazısı — mərhələ boyu qalır.</summary>
    public string Instruction { get; set; } = string.Empty;

    public List<PetBrainPuzzleItemDto> Items { get; set; } = new();

    /// <summary>Mexanikaya görə görünən hədəf dəyər. Marşrut lövhəsində istifadə olunmur.</summary>
    public int? TargetValue { get; set; }

    public PetBrainAnswerSchemaDto AnswerSchema { get; set; } = new();

    /// <summary>İpucu istənə bilərmi.</summary>
    public bool HintAvailable { get; set; }

    /// <summary>
    /// İpucu mətni — YALNIZ istənəndən sonra (və ya dəstək rejimində) dolur.
    /// İpucu heç vaxt cavabın özünü vermir.
    /// </summary>
    public string Hint { get; set; } = string.Empty;

    /// <summary>
    /// Doğru/səhv təzyiqi YOXDUR: yaradıcı yolda hər etibarlı seçim qəbul edilir.
    /// UI bunu bilməlidir — "səhv oldu" dili işlədilməməlidir.
    /// </summary>
    public bool LowPressure { get; set; }

    /// <summary>İndiyə qədər neçə cəhd olub — ekranda ruhlandırıcı mesaj üçün.</summary>
    public int Attempts { get; set; }

    // ---------- Marşrut lövhəsi (OrderedRoute) ----------

    /// <summary>Marşrut düyünləri. Mövqelər NORMALLAŞDIRILMIŞDIR (0–100).</summary>
    public List<PetBrainNodeDto> Nodes { get; set; } = new();

    /// <summary>Qonşuluq — hər cüt iki istiqamətli keçiddir.</summary>
    public List<PetBrainEdgeDto> Edges { get; set; } = new();

    /// <summary>Dronun başlanğıc enerjisi.</summary>
    public int? InitialEnergy { get; set; }

    /// <summary>Enerjinin təhlükəsiz tavanı — doldurma bundan yuxarı qalxmır.</summary>
    public int? MaximumEnergy { get; set; }

    /// <summary>Bir keçidin enerji dəyəri.</summary>
    public int? MoveCost { get; set; }

    /// <summary>
    /// Hədəfdən ƏVVƏL mütləq uğranmalı düyünlər. Tələ yolu məhz buna görə
    /// uğursuz olur: Roboya çatır, amma antenadan keçmir.
    /// </summary>
    public List<string> RequiredBeforeGoal { get; set; } = new();

    /// <summary>
    /// Dəstək rejimi: növbəti gedilə bilən düyünlər UI-da işıqlandırılır.
    ///
    /// <para>Bu, cavabı VERMİR — yalnız qonşuluğu görünən edir. Qonşuluq onsuz
    /// da <see cref="Edges"/>-dədir, yəni gizli məlumat açılmır; sadəcə kiçik
    /// uşaq üçün oxunaqlı olur.</para>
    /// </summary>
    public bool AssistHighlight { get; set; }

    // ---------- İşıq parçaları (LightFragments) ----------

    /// <summary>Qanadın doldurulacaq yuvaları — sıra ilə.</summary>
    public List<PetBrainSlotDto> Slots { get; set; } = new();

    /// <summary>
    /// Hekayə ipucu: uşağın bərpa etməli olduğu naxışın qısa vizual işarəsi.
    /// Cavabı VERMİR — yalnız istiqamət göstərir.
    /// </summary>
    public List<string> ClueIcons { get; set; } = new();
}

/// <summary>
/// Marşrut düyünü. Rol və enerji dəyişikliyi GÖRÜNƏNDİR — uşaq da, yoxlayıcı
/// da eyni məlumata baxır, gizli «doğru» bayrağı yoxdur.
/// </summary>
public class PetBrainNodeDto
{
    public string Id { get; set; } = string.Empty;

    public PetBrainNodeKind Kind { get; set; }

    /// <summary>Normallaşdırılmış üfüqi mövqe (0–100) — illüstrasiyanın üstündə.</summary>
    public int X { get; set; }

    /// <summary>Normallaşdırılmış şaquli mövqe (0–100).</summary>
    public int Y { get; set; }

    /// <summary>Yalnız <see cref="PetBrainNodeKind.Recharge"/> üçün: enerji artımı.</summary>
    public int? EnergyDelta { get; set; }

    public string Label { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;
}

/// <summary>İki düyün arasındakı keçid.</summary>
public class PetBrainEdgeDto
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}

/// <summary>Qanaddakı bir yuva — işıq parçası bura düşür.</summary>
public class PetBrainSlotDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Normallaşdırılmış mövqe (0–100).</summary>
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>
/// Hekayə səhnəsi.
///
/// <para>Rəsm ATMOSFERDİR: bütün qaydalar, düyünlər, toxunuş hədəfləri və
/// geri dönüş deterministik overlay-dədir. Rəsm gəlməsə də tapmaca tam
/// oynanandır — bu, sərt tələbdir.</para>
/// </summary>
public class PetBrainSceneDto
{
    public PetBrainIllustrationStatus IllustrationStatus { get; set; } = PetBrainIllustrationStatus.Fallback;

    /// <summary>
    /// Rəsmin ünvanı — YALNIZ app-in öz, sahiblik yoxlanan endpoint-i.
    /// Xarici URL heç vaxt klientə verilmir.
    /// </summary>
    public string AssetUrl { get; set; } = string.Empty;

    /// <summary>
    /// Ekran oxuyucusu üçün mətn. Modelin çıxışından DEYİL — nəzarətli
    /// şablondan qurulur və uşağın dilindədir.
    /// </summary>
    public string AltText { get; set; } = string.Empty;

    /// <summary>Overlay həndəsəsinin açarı — ehtiyat rəsmdə də EYNİ qalır.</summary>
    public string OverlayLayout { get; set; } = string.Empty;
}

/// <summary>
/// Bir element. <see cref="Shape"/> qəsdən var: rəng TƏK məlumat daşıyıcısı
/// olmamalıdır (bax docs/DESIGN_SYSTEM.md, 3-cü qayda).
/// </summary>
public class PetBrainPuzzleItemDto
{
    /// <summary>Sabit, təhlükəsiz identifikator — cavab məhz bunlarla verilir.</summary>
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    /// <summary>Rəngdən əlavə forma işarəsi: <c>circle</c>, <c>square</c>, <c>triangle</c>, <c>diamond</c>.</summary>
    public string Shape { get; set; } = "circle";

    /// <summary>Mexanikaya görə görünən dəyər (enerji, addım nömrəsi). Boş ola bilər.</summary>
    public int? Value { get; set; }
}

/// <summary>Cavabın gözlənilən forması — klient bunu OXUYUR, dəyişmir.</summary>
public class PetBrainAnswerSchemaDto
{
    public PetBrainAnswerKind Kind { get; set; } = PetBrainAnswerKind.SelectIds;

    /// <summary>Ən az neçə element seçilməlidir.</summary>
    public int Min { get; set; } = 1;

    /// <summary>Ən çox neçə element seçilə bilər.</summary>
    public int Max { get; set; } = 1;
}
