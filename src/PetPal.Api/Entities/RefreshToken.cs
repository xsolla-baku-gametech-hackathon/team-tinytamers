namespace PetPal.Api.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Token-in özü deyil, SHA-256 hash-i saxlanılır.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Uşaq sessiyası üçün verilmiş token — hansı profilə aid olduğunu bilməliyik.</summary>
    public Guid? ChildProfileId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
