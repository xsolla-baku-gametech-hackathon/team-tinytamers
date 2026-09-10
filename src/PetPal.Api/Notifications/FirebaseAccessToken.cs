using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace PetPal.Api.Notifications;

/// <summary>Xidmət hesabı JSON-unun bizə lazım olan üç sahəsi.</summary>
public class ServiceAccountKey
{
    [JsonPropertyName("client_email")] public string ClientEmail { get; set; } = string.Empty;
    [JsonPropertyName("private_key")] public string PrivateKey { get; set; } = string.Empty;
    [JsonPropertyName("token_uri")] public string TokenUri { get; set; } = "https://oauth2.googleapis.com/token";

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ClientEmail) && !string.IsNullOrWhiteSpace(PrivateKey);
}

/// <summary>
/// FCM HTTP v1 OAuth2 giriş biletini alır və keşləyir.
///
/// <para>Google-un axını iki addımdır: xidmət hesabının açarı ilə İMZALANMIŞ
/// JWT hazırlanır, sonra o, token endpoint-ində giriş biletinə dəyişilir.
/// Bilet bir saat yaşayır, ona görə keşlənir — hər bildiriş üçün ayrıca
/// şəbəkə gedişi etmək mənasızdır.</para>
///
/// <para>JWT-nin qurulması ŞƏBƏKƏSİZDİR və ayrıca test oluna bilir; buna görə
/// <see cref="BuildAssertion"/> ayrıca metoddur.</para>
/// </summary>
public class FirebaseAccessToken
{
    /// <summary>FCM göndərişi üçün tələb olunan yeganə səlahiyyət.</summary>
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    /// <summary>Bilet bitməmişdən bir az əvvəl yenilənir — saat fərqi üçün ehtiyat.</summary>
    private static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(5);

    private readonly HttpClient _http;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt;

    public FirebaseAccessToken(HttpClient http, TimeProvider clock)
    {
        _http = http;
        _clock = clock;
    }

    /// <summary>
    /// İmzalanmış JWT assertion-u qurur. Şəbəkəyə çıxmır — testdə birbaşa
    /// yoxlanıla bilir.
    /// </summary>
    public static string BuildAssertion(ServiceAccountKey key, DateTimeOffset now)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(key.PrivateKey);

        // RsaSecurityKey açarı SAXLAYIR, ona görə `using rsa` ilə birlikdə
        // atıla bilməz — imza burada, blokun içində yaradılır.
        var credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        var token = new JwtSecurityToken(
            issuer: key.ClientEmail,
            audience: key.TokenUri,
            claims: [new System.Security.Claims.Claim("scope", Scope)],
            notBefore: now.UtcDateTime,
            expires: now.UtcDateTime.AddMinutes(60),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Keşlənmiş bilet; alınmasa <c>null</c> — bildiriş sadəcə göndərilmir.</summary>
    public async Task<string?> GetAsync(ServiceAccountKey key, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();

        if (_token is not null && now < _expiresAt - RenewBefore)
            return _token;

        await _gate.WaitAsync(ct);

        try
        {
            // İkinci yoxlama: gözləyərkən başqa sorğu bileti artıq yeniləyə bilər.
            if (_token is not null && now < _expiresAt - RenewBefore)
                return _token;

            var request = new HttpRequestMessage(HttpMethod.Post, key.TokenUri)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = BuildAssertion(key, now)
                })
            };

            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

            if (!document.RootElement.TryGetProperty("access_token", out var accessToken))
                return null;

            var seconds = document.RootElement.TryGetProperty("expires_in", out var expires)
                ? expires.GetInt32()
                : 3600;

            _token = accessToken.GetString();
            _expiresAt = now.AddSeconds(seconds);

            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }
}
