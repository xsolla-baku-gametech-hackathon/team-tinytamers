namespace PetPal.Api.Hosting;

public enum HostRunMode
{
    Normal = 0,

    /// <summary>Yalnız migrasiya + seed işlədib çıxır — deploy pipeline-ı üçün.</summary>
    MigrateOnly = 1
}

public static class HostRunModeParser
{
    public const string MigrateOnlyFlag = "--migrate-only";

    public static HostRunMode Parse(string[] args) =>
        args.Contains(MigrateOnlyFlag, StringComparer.OrdinalIgnoreCase)
            ? HostRunMode.MigrateOnly
            : HostRunMode.Normal;

    public static string[] StripFlags(string[] args) =>
        args.Where(a => !string.Equals(a, MigrateOnlyFlag, StringComparison.OrdinalIgnoreCase)).ToArray();
}
