using Microsoft.JSInterop;
using PetPal.App.Ui.Services;

namespace PetPal.Web.Services;

/// <summary>
/// Brauzerdə pet-in səsi — Web Speech API (<c>wwwroot/js/petSpeech.js</c>).
///
/// <para>Dəstək olmayan brauzerdə heç nə sınmır: metod <c>false</c> qaytarır və
/// söhbət ekranı ağzı mətnin uzunluğu qədər tərpədir. Xəta uşağa göstərilmir —
/// səs söhbətin şərti deyil, bəzəyidir.</para>
/// </summary>
public class BrowserPetSpeech : IPetSpeech, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public BrowserPetSpeech(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<bool> SpeakAsync(string text, string language, CancellationToken ct = default)
    {
        try
        {
            _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ct, "./js/petSpeech.js");
            return await _module.InvokeAsync<bool>("speak", ct, text, PetSpeechLocales.CandidatesFor(language));
        }
        catch (OperationCanceledException)
        {
            // Uşaq otaqdan çıxdı və ya yeni replika gəldi — səsi burada da kəsirik,
            // yoxsa brauzer köhnə cümləni oxumağa davam edər.
            await StopAsync();
            throw;
        }
        catch (JSException)
        {
            return false;
        }
    }

    public async Task StopAsync()
    {
        if (_module is null)
            return;

        try
        {
            await _module.InvokeVoidAsync("stop");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
        {
            // Səhifə bağlanır — susdurulacaq səs onsuz da qalmayıb.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        GC.SuppressFinalize(this);
    }
}
