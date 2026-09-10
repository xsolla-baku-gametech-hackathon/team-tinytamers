using Microsoft.Extensions.Configuration;
using PetPal.Api.Hosting;

namespace PetPal.Tests;

/// <summary>
/// Railway/Render/Heroku bazanı <c>DATABASE_URL</c> ilə verir — bu çevirmə səhv olarsa
/// deploy zamanı app baza tapmır, ona görə qaydalar burada sabitlənir.
/// </summary>
public class DatabaseConnectionStringTests
{
    [Fact]
    public void FromUrl_ButunSahelariCixarir()
    {
        var result = DatabaseConnectionString.FromUrl(
            "postgresql://petpal:sirr123@monorail.proxy.rlwy.net:41234/railway");

        Assert.Contains("Host=monorail.proxy.rlwy.net", result);
        Assert.Contains("Port=41234", result);
        Assert.Contains("Database=railway", result);
        Assert.Contains("Username=petpal", result);
        Assert.Contains("Password=sirr123", result);
    }

    [Fact]
    public void FromUrl_PortVerilmeyibse5432Isledir()
    {
        var result = DatabaseConnectionString.FromUrl("postgres://user:pass@postgres.railway.internal/petpal");

        Assert.Contains("Port=5432", result);
    }

    [Fact]
    public void FromUrl_ParoldakiXususiSimvollariDekodEdir()
    {
        // Platformalar parolu URL-kodlaşdırılmış verir; kodlaşma açılmasa giriş rədd edilir.
        var result = DatabaseConnectionString.FromUrl("postgres://user:p%40ss%3Aword@db.internal:5432/petpal");

        Assert.Contains("Password=p@ss:word", result);
    }

    [Fact]
    public void FromUrl_DefoltSslPreferDir()
    {
        // Daxili şəbəkə TLS-siz, public proxy TLS ilə işləyir — Prefer hər ikisini tutur.
        var result = DatabaseConnectionString.FromUrl("postgres://user:pass@db.internal:5432/petpal");

        Assert.Contains("SSL Mode=Prefer", result);
    }

    [Fact]
    public void FromUrl_SslmodeParametriniSaxlayir()
    {
        var result = DatabaseConnectionString.FromUrl("postgres://user:pass@db.internal:5432/petpal?sslmode=require");

        Assert.Contains("SSL Mode=Require", result);
    }

    [Theory]
    [InlineData("mysql://user:pass@host:3306/db")]
    [InlineData("bu-url-deyil")]
    [InlineData("postgres://host:5432/db")]
    [InlineData("postgres://user:pass@host:5432/")]
    public void FromUrl_YararsizDeyerdeXetaVerir(string url)
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseConnectionString.FromUrl(url));
    }

    [Fact]
    public void Resolve_AcıqKonfiqurasiyaDatabaseUrlUstundedir()
    {
        // Platformanın avtomatik verdiyi dəyişən qəsdən yazılmış ayarı əvəz etməməlidir.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=petpal",
                ["DATABASE_URL"] = "postgres://user:pass@platform.internal:5432/other"
            })
            .Build();

        Assert.Equal("Host=localhost;Database=petpal", DatabaseConnectionString.Resolve(configuration));
    }

    [Fact]
    public void Resolve_AcıqKonfiqurasiyaYoxdursaDatabaseUrlIsledir()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATABASE_URL"] = "postgres://user:pass@platform.internal:5432/petpal"
            })
            .Build();

        var result = DatabaseConnectionString.Resolve(configuration);

        Assert.NotNull(result);
        Assert.Contains("Host=platform.internal", result);
    }

    [Fact]
    public void Resolve_HecNe_YoxdursaNullQaytarir()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Null(DatabaseConnectionString.Resolve(configuration));
    }
}
