using System.Reflection;

namespace PetPal.App;

/// <summary>
/// Backend ünvanı.
///
/// Emulyator/simulyator "localhost"-u fərqli həll edir, ona görə hər platforma
/// üçün ayrıca dev ünvanı lazımdır:
///  • Android emulyatoru — host maşın 10.0.2.2 ünvanındadır.
///  • iOS simulyatoru — host ilə eyni şəbəkədədir, "localhost" işləyir.
///  • Fiziki telefon — "localhost" telefonun özüdür, ona görə development
///    maşınının LAN ünvanı lazımdır.
///
/// Fiziki cihaz üçün ünvanı build zamanı ötürmək olar (mənbə kodu dəyişmədən):
/// <code>dotnet build ... -p:PetPalDevApiUrl=http://192.168.0.103:5180/</code>
///
/// Masaüstü paketi (dist/PetPal) Release build-i işlədir, amma API-si həmin
/// maşındadır — ona görə <see cref="ApiUrlEnvironmentVariable"/> hər
/// konfiqurasiyada digər mənbələrdən üstündür və launcher onu doldurur.
///
/// Başqa halda produksiya build-ində <see cref="ProductionBaseUrl"/> istifadə olunur.
/// </summary>
public static class AppConfig
{
    public const string ProductionBaseUrl = "https://aipetsforkids-api.up.railway.app/";

    /// <summary>Launcher (<c>PetPal.exe</c>) app-ı lokal API-yə yönləndirmək üçün bunu təyin edir.</summary>
    public const string ApiUrlEnvironmentVariable = "PETPAL_API_URL";

    public static Uri ApiBaseAddress => new(ResolveBaseUrl());

    private static string ResolveBaseUrl()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(ApiUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

#if DEBUG
        var fromBuild = ReadBuildTimeUrl();
        if (!string.IsNullOrWhiteSpace(fromBuild))
            return fromBuild!;

        if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual)
            return "http://10.0.2.2:5180/";

        return "http://localhost:5180/";
#else
        return ProductionBaseUrl;
#endif
    }

    /// <summary>csproj-dakı <c>PetPalDevApiUrl</c> property-si assembly metadata kimi yazılır.</summary>
    private static string? ReadBuildTimeUrl() =>
        typeof(AppConfig).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "PetPalDevApiUrl")
            ?.Value;
}
