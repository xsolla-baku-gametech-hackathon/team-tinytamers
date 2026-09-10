namespace PetPal.Shared.Dtos;

/// <summary>API-nin qaytardığı standart xəta cavabı — app tərəfdə birbaşa uşağa uyğun mesaja çevrilir.</summary>
public class ApiErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string[]> Errors { get; set; } = new();
}
