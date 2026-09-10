using Microsoft.Maui.Media;
using PetPal.App.Ui.Services;

namespace PetPal.App.Services;

/// <summary>
/// App-də pet-in səsi — platformanın öz danışma mühərriki (MAUI TextToSpeech).
///
/// <para>Dil seçimi <see cref="PetSpeechLocales"/>-dədir və hər iki platformada
/// eynidir: az-AZ → türk səsi → cihazın standartı. Səbəb praktikdir — az-AZ səsi
/// əksər Android və Windows cihazında qurulu olmur, türk səsi isə Azərbaycan
/// mətnini anlaşıqlı oxuyur.</para>
///
/// <para>Danışma mühərriki ümumiyyətlə yoxdursa metod <c>false</c> qaytarır və
/// söhbət ekranı ağzı öz ölçüsü ilə tərpədir — uşağa xəta göstərilmir.</para>
/// </summary>
public class MauiPetSpeech : IPetSpeech
{
    /// <summary>Cihazın səs siyahısı dəyişmir — bir dəfə oxunub saxlanılır.</summary>
    private readonly Dictionary<string, Locale?> _localeCache = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CancellationTokenSource? _current;

    public async Task<bool> SpeakAsync(string text, string language, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Əvvəlki replika hələ oxunursa kəsilir: iki səs eyni anda danışmamalıdır.
        await StopAsync();

        var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _current = linked;

        try
        {
            var options = new SpeechOptions
            {
                Locale = await ResolveLocaleAsync(language),

                // Uşaq üçün bir az yavaş və bir az incə — brauzer versiyası ilə eyni.
                Pitch = 1.25f
            };

            await TextToSpeech.Default.SpeakAsync(text, options, linked.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Danışma mühərriki yoxdur, qurulmayıb və ya cihaz imtina etdi.
            // Söhbət səssiz davam edir.
            return false;
        }
        finally
        {
            if (ReferenceEquals(_current, linked))
                _current = null;

            linked.Dispose();
        }
    }

    public Task StopAsync()
    {
        try
        {
            _current?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Danışıq onsuz da bitib.
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Dilə uyğun ilk mövcud səsi tapır. Heç biri yoxdursa <c>null</c> qayıdır —
    /// bu, "cihazın standart səsi ilə oxu" deməkdir.
    /// </summary>
    private async Task<Locale?> ResolveLocaleAsync(string language)
    {
        var key = language ?? string.Empty;

        await _gate.WaitAsync();

        try
        {
            if (_localeCache.TryGetValue(key, out var cached))
                return cached;

            Locale? found = null;

            try
            {
                var locales = (await TextToSpeech.Default.GetLocalesAsync()).ToList();

                foreach (var candidate in PetSpeechLocales.CandidatesFor(key))
                {
                    found = Match(locales, candidate);
                    if (found is not null)
                        break;
                }
            }
            catch (Exception)
            {
                // Siyahı alınmadı — standart səslə davam edirik.
            }

            _localeCache[key] = found;
            return found;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Əvvəlcə tam uyğunluq ("az-AZ"), sonra dil prefiksi ("az"). MAUI-də dil
    /// və ölkə ayrı sahələrdədir, ona görə müqayisə birləşdirilmiş şəkildə gedir.
    /// </summary>
    private static Locale? Match(List<Locale> locales, string candidate)
    {
        var exact = locales.FirstOrDefault(l =>
            string.Equals(Combine(l), candidate, StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
            return exact;

        var prefix = candidate.Split('-')[0];

        return locales.FirstOrDefault(l =>
            string.Equals(l.Language, prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string Combine(Locale locale) =>
        string.IsNullOrWhiteSpace(locale.Country)
            ? locale.Language
            : $"{locale.Language}-{locale.Country}";
}
