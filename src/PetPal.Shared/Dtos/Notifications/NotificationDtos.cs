using System.ComponentModel.DataAnnotations;

namespace PetPal.Shared.Dtos.Notifications;

/// <summary>
/// Cihazın push ünvanının qeydiyyatı. Token cihazın özü tərəfindən yaradılır;
/// server onu yalnız saxlayır və hansı profilə aid olduğunu yazır.
/// </summary>
public class RegisterDeviceRequest
{
    [Required]
    [StringLength(4096, MinimumLength = 8)]
    public string Token { get; set; } = string.Empty;

    /// <summary>"android" · "ios" · "web" — yalnız diaqnostika üçün.</summary>
    [StringLength(16)]
    public string Platform { get; set; } = string.Empty;
}
