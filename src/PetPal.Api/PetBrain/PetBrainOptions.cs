namespace PetPal.Api.PetBrain;

/// <summary>
/// Pet Brain-in konfiqurasiyası.
///
/// <para>Standartlar QORUYUCUDUR: özəllik açıqdır (o, oyunun bir hissəsidir),
/// amma nümayiş paneli, toxum məlumatı və model zənginləşdirməsi bağlıdır.
/// Yəni produksiyada heç bir nümayiş hesabı yaranmır və uşaq interfeysində
/// münsif paneli görünmür.</para>
/// </summary>
public class PetBrainOptions
{
    public const string SectionName = "PetBrain";

    /// <summary>Bağlı olduqda ekran nəzakətli boş hal göstərir, endpoint-lər isə iş görmür.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Nümayiş/izah paneli. Bağlı olduqda uşaq interfeysində NƏ bal barları,
    /// NƏ direktor səbəbləri, NƏ də namizəd cədvəli görünür.
    /// </summary>
    public bool DemoMode { get; set; }

    /// <summary>
    /// Nümayiş profillərini (Aylin + Mia) yaradır.
    ///
    /// <para>Yalnız Development mühitində işləyir. Produksiyada açılsa belə
    /// <see cref="AllowSeedOutsideDevelopment"/> olmadan toxum atılmır — nümayiş
    /// hesabının canlı bazaya düşməsi ən bahalı səhv olardı.</para>
    /// </summary>
    public bool SeedDemoData { get; set; }

    /// <summary>
    /// Development-dən kənarda toxum atmağa AÇIQ icazə. Sənədləşdirilmiş,
    /// produksiya üçün təhlükəli açardır — yalnız izolyasiya olunmuş nümayiş
    /// serverində qoşulmalıdır.
    /// </summary>
    public bool AllowSeedOutsideDevelopment { get; set; }

    /// <summary>
    /// Başlıq və giriş mətnini modelə yazdırmağa icazə. Bağlı olduqda (standart)
    /// bütün mətn deterministik şablondan gəlir və heç bir model çağırışı olmur.
    /// Açıq olsa belə model YALNIZ təqdimat mətninə toxunur: şablon seçimi,
    /// çətinlik, mükafat və doğruluq həmişə serverin öz qərarıdır.
    /// </summary>
    public bool UseAiNarrative { get; set; }

    /// <summary>
    /// Tapmacanın hekayə rəsmini modelə çəkdirməyə icazə.
    ///
    /// <para>Bağlı olduqda (standart) heç bir şəkil sorğusu getmir və ekranda
    /// deterministik SVG/CSS səhnəsi qalır. <b>Rəsm heç bir halda tapmacanı
    /// dəyişmir</b>: düyünlər, qaydalar, toxunuş hədəfləri və cavab overlay-də,
    /// serverin nəzarətindədir.</para>
    /// </summary>
    public bool UseAiIllustration { get; set; }

    /// <summary>Şəkil modelinin adı — provayderin sənədindəki açar.</summary>
    public string IllustrationModel { get; set; } = string.Empty;

    /// <summary>Şəkil sorğusunun sərt vaxt həddi (saniyə).</summary>
    public int IllustrationTimeoutSeconds { get; set; } = 25;

    /// <summary>
    /// Hazır rəsmlərin kök qovluğu. Boş buraxılsa app-in öz qovluğu işlənir.
    /// Konteynerin diski müvəqqətidir — kalıcı volume bura mount edilməlidir.
    /// </summary>
    public string IllustrationStorageRoot { get; set; } = string.Empty;

    /// <summary>Nümayiş valideyninin e-poçtu — sənədlərdəki giriş məlumatı ilə eyni.</summary>
    public string DemoParentEmail { get; set; } = "demo@petpal.test";

    /// <summary>Nümayiş valideyninin parolu. Yalnız lokal toxum üçündür.</summary>
    public string DemoParentPassword { get; set; } = "DemoPetPal1";

    /// <summary>Hər iki nümayiş uşağının PIN-i.</summary>
    public string DemoChildPin { get; set; } = "2468";
}
