using Microsoft.AspNetCore.Identity;

namespace PetPal.Api.Entities;

/// <summary>
/// Yalnız valideyn hesabı Identity istifadəçisidir. Uşaqlar ayrıca
/// <see cref="ChildProfile"/> kimi saxlanılır və e-poçt/şifrə tələb etmir.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Valideyn bölməsinə keçid üçün PIN (uşaq təsadüfən daxil olmasın deyə).</summary>
    public string? ParentGatePinHash { get; set; }

    public ICollection<ChildProfile> Children { get; set; } = new List<ChildProfile>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
