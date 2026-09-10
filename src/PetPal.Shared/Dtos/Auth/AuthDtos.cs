using System.ComponentModel.DataAnnotations;
using PetPal.Shared.Enums;
using PetPal.Shared.Validation;

namespace PetPal.Shared.Dtos.Auth;

/// <summary>
/// Qeydiyyatı yalnız valideyn edir. Uşaq hesabı e-poçt/şifrə saxlamır —
/// uşaq profili valideyn hesabına bağlanır və 4 rəqəmli PIN ilə seçilir (KVKK/COPPA yanaşması).
/// </summary>
public class RegisterParentRequest
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.EmailRequired))]
    [EmailAddress(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.EmailFormat))]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PasswordRequired))]
    [StringLength(100, MinimumLength = 8, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PasswordLength))]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.NameRequired))]
    [StringLength(60, MinimumLength = 2, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.NameLength60))]
    public string DisplayName { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.EmailRequired))]
    [EmailAddress(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.EmailFormat))]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PasswordRequired))]
    public string Password { get; set; } = string.Empty;
}

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }

    /// <summary>Token-in hansı profil üçün verildiyi — uşaq sessiyası valideyn ekranlarına giriş vermir.</summary>
    public ProfileKind ProfileKind { get; set; }

    public Guid ParentId { get; set; }
    public string ParentDisplayName { get; set; } = string.Empty;

    /// <summary>Uşaq sessiyasında yalnız aktiv profil, valideyn sessiyasında bütün uşaqlar.</summary>
    public Guid? ChildId { get; set; }

    public List<ChildSummaryDto> Children { get; set; } = new();

    /// <summary>
    /// İnterfeysin dili ("az" / "en"). Uşaq sessiyasında bu, uşağın öz dilidir;
    /// valideyn sessiyasında isə birinci uşağın dili — valideyn ekranları da
    /// ailənin dilində açılsın deyə. Uşaq yoxdursa "az".
    /// </summary>
    public string LanguageCode { get; set; } = "az";
}

public class CreateChildRequest
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.ChildNameRequired))]
    [StringLength(40, MinimumLength = 2, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.NameLength40))]
    public string DisplayName { get; set; } = string.Empty;

    [Range(3, 16, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.AgeRange))]
    public int Age { get; set; } = 8;

    [StringLength(40)]
    public string AvatarKey { get; set; } = "avatar-fox";

    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PinRequired))]
    [RegularExpression("^[0-9]{4}$", ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PinFormat))]
    public string Pin { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PetNameRequired))]
    [StringLength(24, MinimumLength = 2, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PetNameLength))]
    public string PetName { get; set; } = "Max";

    [StringLength(24)]
    public string PetSpecies { get; set; } = "fox";

    /// <summary>Sual bankı və pet replikalarının dili ("az" / "en").</summary>
    [RegularExpression("^(az|en)$", ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.LanguageChoice))]
    public string LanguageCode { get; set; } = "az";

    /// <summary>
    /// Cihazın UTC-dən fərqi (dəqiqə). Yuxu rejimi uşağın yerli saatına görə
    /// hesablandığı üçün lazımdır.
    /// </summary>
    [Range(-840, 840, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.TimeZoneOffset))]
    public int UtcOffsetMinutes { get; set; }
}

public class ChildLoginRequest
{
    [Required]
    public Guid ChildId { get; set; }

    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PinRequired))]
    [RegularExpression("^[0-9]{4}$", ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PinFormat))]
    public string Pin { get; set; } = string.Empty;
}

public class ChildSummaryDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarKey { get; set; } = string.Empty;
    public int Age { get; set; }
    public int Stars { get; set; }
    public int Gems { get; set; }
    public int Level { get; set; }
    public string PetName { get; set; } = string.Empty;
    public PetStage PetStage { get; set; }

    /// <summary>Uşağın dili ("az" / "en") — həm məzmun, həm interfeys üçün.</summary>
    public string LanguageCode { get; set; } = "az";
}
