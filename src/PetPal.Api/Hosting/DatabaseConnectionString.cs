using Npgsql;

namespace PetPal.Api.Hosting;

/// <summary>
/// Bazanın əlaqə sətrini konfiqurasiyadan tapır.
///
/// Railway, Render, Heroku və Fly kimi platformalar bazanı ayrıca sahələr yerinə
/// tək bir <c>DATABASE_URL</c> mühit dəyişənində, URI formatında verir:
/// <c>postgresql://istifadeci:parol@host:5432/baza</c>. Npgsql bu formatı başa düşmür,
/// ona görə burada onun anladığı açar=dəyər sətrinə çevrilir.
///
/// Üstünlük sırası açıq konfiqurasiyadadır: <c>ConnectionStrings:DefaultConnection</c>
/// verilibsə, <c>DATABASE_URL</c>-ə baxılmır — beləliklə platformanın avtomatik
/// verdiyi dəyişən heç vaxt qəsdən yazılmış ayarı səssizcə əvəz etmir.
/// </summary>
public static class DatabaseConnectionString
{
    public const string DatabaseUrlKey = "DATABASE_URL";

    public static string? Resolve(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var databaseUrl = configuration[DatabaseUrlKey];
        return string.IsNullOrWhiteSpace(databaseUrl) ? null : FromUrl(databaseUrl);
    }

    /// <summary>URI formatını Npgsql əlaqə sətrinə çevirir.</summary>
    /// <exception cref="InvalidOperationException">URI oxuna bilmirsə.</exception>
    public static string FromUrl(string databaseUrl)
    {
        if (!Uri.TryCreate(databaseUrl.Trim(), UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"{DatabaseUrlKey} düzgün URI deyil.");

        if (uri.Scheme is not ("postgres" or "postgresql"))
            throw new InvalidOperationException(
                $"{DatabaseUrlKey} yalnız postgres:// və ya postgresql:// ola bilər (verilən: {uri.Scheme}://).");

        var userInfo = uri.UserInfo.Split(':', 2);
        var database = uri.AbsolutePath.Trim('/');

        if (string.IsNullOrEmpty(userInfo[0]) || string.IsNullOrEmpty(database))
            throw new InvalidOperationException($"{DatabaseUrlKey} istifadəçi adı və baza adı ehtiva etməlidir.");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(database),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null
        };

        // Platformadan asılı olaraq baza həm daxili şəbəkədə (TLS-siz), həm də public
        // proxy üzərindən (TLS ilə) verilə bilər — Prefer hər ikisində işləyir.
        // Npgsql-də Require şifrələyir, amma sertifikat zəncirini yoxlamır (libpq ilə eyni);
        // zəncir yoxlaması lazım olsa URL-də sslmode=verify-full verilir.
        var sslMode = ReadQueryValue(uri.Query, "sslmode");
        builder.SslMode = sslMode?.ToLowerInvariant() switch
        {
            "disable" => SslMode.Disable,
            "allow" => SslMode.Allow,
            "require" => SslMode.Require,
            "verify-ca" => SslMode.VerifyCA,
            "verify-full" => SslMode.VerifyFull,
            _ => SslMode.Prefer
        };

        return builder.ConnectionString;
    }

    private static string? ReadQueryValue(string query, string key)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0], key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(parts[1]);
        }

        return null;
    }
}
