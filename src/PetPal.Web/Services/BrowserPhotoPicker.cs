using Microsoft.JSInterop;
using PetPal.App.Ui.Services;

namespace PetPal.Web.Services;

/// <summary>
/// Brauzerdə şəkil seçimi gizli <c>&lt;input type="file"&gt;</c> ilə açılır; mobil brauzerdə
/// <c>capture</c> atributu birbaşa kameranı təklif edir. İstifadəçi imtina edərsə <c>null</c> qayıdır.
/// </summary>
public class BrowserPhotoPicker : IPhotoPicker, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public BrowserPhotoPicker(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<string?> CaptureAsync()
    {
        try
        {
            _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/photoPicker.js");
            return await _module.InvokeAsync<string?>("capture");
        }
        catch (JSException)
        {
            return null;
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
