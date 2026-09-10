using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetPal.Api.Data;

namespace PetPal.Tests;

/// <summary>
/// Testlər real PostgreSQL yerinə in-memory SQLite işlədir: sxem eynidir,
/// hər fixture öz izolyasiya olunmuş bazasını alır və Docker tələb olunmur.
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>Testlər vaxtı irəli sürə bilsin deyə saat sabitlənir.</summary>
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// Developer maşınında təyin oluna bilən AI dəyişənləri.
    ///
    /// <para>Program.cs <c>AddEnvironmentVariables()</c> çağırdığına görə environment
    /// <see cref="IWebHostBuilder.UseSetting"/> dəyərlərini ÜSTƏLƏYİR. Yəni masaüstü
    /// paketi üçün qoyulmuş <c>Ai__ApiKey</c> testləri real model serverinə çağırış
    /// etməyə məcbur edərdi — testlər isə şəbəkəsiz və determinist qalmalıdır.</para>
    /// </summary>
    private static readonly string[] AmbientAiVariables =
    [
        "Ai__Provider", "Ai__BaseUrl", "Ai__ApiKey", "Ai__Model",
        "Ai__ChatModel", "Ai__ReasoningEffort", "Ai__GuardModel", "Ai__GuardThreshold"
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var name in AmbientAiVariables)
            Environment.SetEnvironmentVariable(name, null);

        _connection.Open();

        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=test-yalniz;Database=test;Username=test;Password=test");
        builder.UseSetting("Jwt:Key", "test-mühiti-üçün-64-simvolluq-imza-açarı-0123456789-ABCDEFGHIJKLMN");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "1000000");
        builder.UseSetting("RateLimiting:Social:PermitLimit", "1000000");
        builder.UseSetting("RateLimiting:Chat:PermitLimit", "1000000");
        builder.UseSetting("RateLimiting:Arena:PermitLimit", "1000000");

        // Ekran vaxtı mühafizəsi appsettings-də hələlik söndürülüb, amma qaydalar
        // yerindədir və test olunmağa davam etməlidir — burada açıq saxlanılır.
        builder.UseSetting("ScreenTime:Enforced", "true");

        builder.ConfigureTestServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(AppDbContext)
                            || d.ServiceType == typeof(DbContextOptions)
                            || (d.ServiceType.IsGenericType && (
                                d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)
                                || d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>))))
                .ToList();

            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}

/// <summary>Testlərin vaxtı idarə etməsi üçün sadə <see cref="TimeProvider"/>.</summary>
public class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);

    public void SetUtcNow(DateTimeOffset value) => _now = value;
}
