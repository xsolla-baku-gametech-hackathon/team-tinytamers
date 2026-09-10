using Microsoft.EntityFrameworkCore;
using Npgsql;
using PetPal.Api.Data;
using PetPal.Api.PetBrain;

namespace PetPal.Api.Hosting;

public static class DatabaseBootstrapper
{
    /// <summary>
    /// Sxemi hazırlayır və seed məlumatlarını yazır.
    /// Relational migrasiya olan provider-lərdə <c>Migrate</c>, in-memory/SQLite
    /// test mühitində isə <c>EnsureCreated</c> işlədilir.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        LogTarget(scope.ServiceProvider, db);

        if (db.Database.IsNpgsql())
            await db.Database.MigrateAsync(ct);
        else
            await db.Database.EnsureCreatedAsync(ct);

        await DbInitializer.SeedAsync(scope.ServiceProvider, ct);

        // Nümayiş profilləri AYRICA və könüllüdür: standart olaraq heç nə
        // yazılmır və produksiyada açıq icazə olmadan ümumiyyətlə işə düşmür
        // (bax DemoDataSeeder).
        await DemoDataSeeder.SeedAsync(services, ct);
    }

    /// <summary>
    /// Hansı serverə qoşulduğumuzu açıq yazır. Maşında bir neçə Postgres işləyəndə
    /// (məs. başqa layihənin bazası) səhv hədəf dərhal görünsün deyə.
    /// Şifrə heç vaxt loglanmır.
    /// </summary>
    private static void LogTarget(IServiceProvider services, AppDbContext db)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseBootstrapper));

        if (!db.Database.IsNpgsql())
        {
            logger.LogInformation("Verilənlər bazası: {Provider} (test/in-memory)", db.Database.ProviderName);
            return;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
            logger.LogInformation(
                "Verilənlər bazası: {Host}:{Port}/{Database}",
                builder.Host, builder.Port, builder.Database);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("Bağlantı sətri təhlil edilə bilmədi — hədəf baza loglanmadı.");
        }
    }
}
