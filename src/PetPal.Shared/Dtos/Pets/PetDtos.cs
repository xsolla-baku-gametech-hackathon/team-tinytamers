using System.ComponentModel.DataAnnotations;
using PetPal.Shared.Enums;
using PetPal.Shared.Validation;

namespace PetPal.Shared.Dtos.Pets;

public class PetDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;

    public PetStage Stage { get; set; }

    /// <summary>
    /// Yumurtadan çıxıbmı. Çıxmayıbsa qulluq və kosmetik əşyalar bağlıdır —
    /// uşaq əvvəlcə ulduz toplayıb yumurtanı açmalıdır.
    /// </summary>
    public bool IsHatched { get; set; }

    /// <summary>Yumurtanı açmağın ulduz qiyməti.</summary>
    public int HatchStarCost { get; set; }

    public int Level { get; set; }
    public int Xp { get; set; }
    public int XpToNextLevel { get; set; }

    /// <summary>Care statları 0–100 aralığında; vaxt keçdikcə azalır, care əməliyyatı ilə artır.</summary>
    public int Happiness { get; set; }
    public int Energy { get; set; }
    public int Fullness { get; set; }
    public int Cleanliness { get; set; }

    /// <summary>
    /// Bağ (0–100) — uşaqla pet arasındakı uzunmüddətli münasibət.
    /// <see cref="Happiness"/> ilə qarışdırılmamalıdır: o, vaxta görə azalan
    /// qulluq statıdır, bu isə yalnız artır.
    /// </summary>
    public int Bond { get; set; }

    /// <summary>
    /// Bağın PİLLƏSİ — server hesablayır.
    ///
    /// <para>Hədləri klientdə təkrarlamaq olmaz: onda balans dəyişəndə iki
    /// yerdə düzəliş lazım gələr və biri unudular.</para>
    /// </summary>
    public PetBrainBondTier BondTier { get; set; }

    /// <summary>
    /// Pillənin açdığı POZA açarı — pet-in duruşu. Qapalı siyahıdandır.
    /// </summary>
    public string BondPose { get; set; } = string.Empty;

    /// <summary>
    /// Pillənin açdığı otaq bəzəyinin açarı; yoxdursa boş. Qulluq otağı onu
    /// çəkir — «qazandığım şey otağımda görünür».
    /// </summary>
    public string BondRoomDecor { get; set; } = string.Empty;

    public PetMood Mood { get; set; }

    /// <summary>Pet-in ekranda dediyi replika (mood + son fəaliyyətdən seçilir).</summary>
    public string Message { get; set; } = string.Empty;

    public List<string> UnlockedAccessories { get; set; } = new();

    /// <summary>
    /// Hazırda pet-in üstündə çəkilən əşyalar. Açılmış ≠ taxılmış — görünüşü
    /// uşaq özü qurur.
    /// </summary>
    public List<string> EquippedAccessories { get; set; } = new();

    /// <summary>
    /// Bütün kosmetik kataloq — açılmışlar və hələ açılmamışlar birlikdə.
    /// Kilidli əşyanı da göstərmək məqsədlidir: uşaq nəyə doğru getdiyini görür.
    /// </summary>
    public List<PetAccessoryDto> Accessories { get; set; } = new();
}

public class PetAccessoryDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool IsUnlocked { get; set; }

    /// <summary>Əşya hazırda pet-in üstündədirmi.</summary>
    public bool IsEquipped { get; set; }

    /// <summary>Hazır mətn: "Səviyyə 8 + xoşbəxtlik 95".</summary>
    public string Requirement { get; set; } = string.Empty;
}

/// <summary>Pet-in görünüşü: taxılacaq əşyaların tam siyahısı (boş = heç nə taxılmır).</summary>
public class EquipAccessoriesRequest
{
    public List<string> Codes { get; set; } = new();
}

/// <summary>
/// Yemək kataloqunun bir elementi — qulluq ekranındakı yem qabına düzülür.
/// Kataloq statikdir və pet-in vəziyyətindən asılı deyil, ona görə <see cref="PetDto"/>
/// içində deyil, ayrıca endpoint-də saxlanılır.
/// </summary>
public class PetFoodDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    /// <summary>Bir porsiyanın ulduz qiyməti.</summary>
    public int StarCost { get; set; }

    /// <summary>Hazır mətn: "Toxluq +30 · Sevinc +5".</summary>
    public string Effect { get; set; } = string.Empty;

    /// <summary>
    /// Şirniyyat: sevinc çox verir, amma pet bulaşır. Uşaq seçiminin nəticəsini
    /// əvvəlcədən görsün deyə ekranda ayrıca işarələnir.
    /// </summary>
    public bool IsTreat { get; set; }
}

public class CarePetRequest
{
    [Required]
    public CareAction Action { get; set; }

    /// <summary>
    /// Yalnız <see cref="CareAction.Feed"/> üçün: hansı yeməyin verildiyi
    /// (<see cref="PetFoodDto.Code"/>). Boş buraxılarsa standart yemək seçilir.
    /// </summary>
    public string? Food { get; set; }
}

public class CarePetResultDto
{
    public PetDto Pet { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public int XpEarned { get; set; }

    /// <summary>Care əməliyyatının ulduz dəyəri (məs. yemək almaq). 0 = pulsuz.</summary>
    public int StarsSpent { get; set; }

    /// <summary>Bu əməliyyatla təzəcə açılan kosmetik əşyalar — adətən boş olur.</summary>
    public List<PetAccessoryDto> NewAccessories { get; set; } = new();
}

public class HatchPetResultDto
{
    public PetDto Pet { get; set; } = new();

    /// <summary>Yumurtadan çıxma anında pet-in dediyi replika.</summary>
    public string Message { get; set; } = string.Empty;

    public int StarsSpent { get; set; }
}

/// <summary>
/// Pet-in növünü dəyişir. Növ yalnız GÖRÜNÜŞDÜR — yaş, statlar, əşyalar və
/// açılışlar toxunulmaz qalır, ona görə uşaq fikrini dəyişəndə heç nə itmir.
/// </summary>
public class ChangeSpeciesRequest
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.SpeciesRequired))]
    [RegularExpression("^(fox|cat|dragon|bunny)$", ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.SpeciesUnknown))]
    public string Species { get; set; } = "fox";
}

public class RenamePetRequest
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PetNameRequired))]
    [StringLength(24, MinimumLength = 2, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PetNameLength))]
    public string Name { get; set; } = string.Empty;
}
