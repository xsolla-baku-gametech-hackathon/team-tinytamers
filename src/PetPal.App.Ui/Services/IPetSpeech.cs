namespace PetPal.App.Ui.Services;

/// <summary>
/// Pet-in səsi. Mətni ucadan oxumaq platformaya bağlıdır (brauzerdə Web Speech,
/// app-də MAUI TextToSpeech), ona görə UI kitabxanasında yalnız interfeys durur —
/// <see cref="IPhotoPicker"/> və <see cref="ITokenStore"/> ilə eyni qayda.
///
/// <para><b>Səs məcburi deyil.</b> Cihazda danışma dəstəyi olmasa da söhbət eyni
/// ilə işləyir: replika onsuz da yazı kimi görünür, pet isə ağzını tərpədir.
/// Məhz buna görə <see cref="SpeakAsync"/> xəta atmır — səs çıxmadığını
/// <c>false</c> ilə bildirir və ekran ağız animasiyasını özü ölçür.</para>
/// </summary>
public interface IPetSpeech
{
    /// <summary>
    /// Mətni ucadan oxuyur və oxuyub qurtaranda qayıdır — ağız animasiyası
    /// məhz bu müddət boyu davam edir.
    /// </summary>
    /// <param name="text">Oxunacaq replika.</param>
    /// <param name="language">Uşağın dili ("az" / "en").</param>
    /// <returns>Səs çıxdısa <c>true</c>; cihaz danışa bilmirsə <c>false</c>.</returns>
    Task<bool> SpeakAsync(string text, string language, CancellationToken ct = default);

    /// <summary>Danışığı yarımçıq kəsir — uşaq otaqdan çıxanda səs arxadan gəlməməlidir.</summary>
    Task StopAsync();
}

/// <summary>
/// Səsi olmayan mühitlər (test, brauzersiz host) üçün təhlükəsiz default:
/// heç nə oxumur və <c>false</c> qaytarır, yəni ekran ağzı öz ölçüsü ilə tərpədir.
/// </summary>
public class NullPetSpeech : IPetSpeech
{
    public Task<bool> SpeakAsync(string text, string language, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task StopAsync() => Task.CompletedTask;
}

/// <summary>
/// Danışıq dilinin seçimi — hər iki platforma eyni qaydanı işlədir, ona görə
/// qayda burada, tək yerdə saxlanılır.
/// </summary>
public static class PetSpeechLocales
{
    /// <summary>
    /// Dilin namizəd kodları, ÜSTÜNLÜK SIRASI ilə. Azərbaycan dili üçün türk
    /// səsi ikinci namizəddir: az-AZ səsi əksər Android və Windows cihazında
    /// qurulu olmur, türk səsi isə Azərbaycan mətnini anlaşıqlı oxuyur —
    /// yalnız vurğu fərqlənir. Heç biri tapılmasa cihazın öz standart səsi
    /// işlədilir; susmaqdansa vurğusu fərqli səs uşaq üçün daha yaxşıdır.
    /// </summary>
    public static string[] CandidatesFor(string? language) =>
        (language ?? string.Empty).StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? ["en-US", "en-GB", "en"]
            : ["az-AZ", "az", "tr-TR", "tr"];

    /// <summary>
    /// Səs olmayanda ağzın nə qədər tərpənəcəyi. Uşaq üçün replikanın "danışıldığı"
    /// hiss oxuma sürətindən gəlir — hərf başına təxminən bu qədər.
    /// </summary>
    private const int MillisecondsPerCharacter = 55;

    private const int MinMilliseconds = 1_200;
    private const int MaxMilliseconds = 6_000;

    /// <summary>Səssiz halda ağız animasiyasının müddəti.</summary>
    public static int SilentDurationMs(string? text) =>
        Math.Clamp((text?.Length ?? 0) * MillisecondsPerCharacter, MinMilliseconds, MaxMilliseconds);
}
