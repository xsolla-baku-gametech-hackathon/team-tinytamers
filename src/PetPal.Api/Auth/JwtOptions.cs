namespace PetPal.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "petpal-api";
    public string Audience { get; set; } = "petpal-app";

    /// <summary>Mobil app-da tez-tez login istəməmək üçün access token nisbətən uzunömürlüdür.</summary>
    public int AccessTokenMinutes { get; set; } = 60;

    public int RefreshTokenDays { get; set; } = 30;
}
