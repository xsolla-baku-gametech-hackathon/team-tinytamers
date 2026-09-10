using Microsoft.JSInterop;
using PetPal.App.Ui.Services;

namespace PetPal.Web.Services;

/// <summary>
/// Brauzer versiyasında token-lər <c>localStorage</c>-də saxlanılır — səhifə yenilənəndə
/// (F5) valideyn sessiyası itmir. MAUI-dəki qarşılığı SecureStorage-dır.
/// </summary>
public class LocalStorageTokenStore : ITokenStore
{
    private readonly IJSRuntime _js;

    public LocalStorageTokenStore(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await _js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            // Privat rejimdə localStorage bloklana bilər — app token-siz də açılmalıdır.
            return null;
        }
    }

    public async Task SetAsync(string key, string value)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (JSException)
        {
        }
    }
}
