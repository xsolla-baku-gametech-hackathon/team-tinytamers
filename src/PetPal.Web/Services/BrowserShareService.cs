using Microsoft.JSInterop;
using PetPal.App.Ui.Services;

namespace PetPal.Web.Services;

/// <summary>
/// Brauzerdə paylaşma <c>navigator.share</c> ilə açılır. Masaüstü brauzerlərin
/// çoxunda o yoxdur, ona görə iki geri düşmə var: mətn panoya kopyalanır,
/// şəkil isə sadəcə yüklənir və valideyn onu özü göndərir.
///
/// <para><see cref="IsSupported"/> həmişə <c>true</c>-dur: geri düşmələr
/// sayəsində düymə hər halda faydalı iş görür — bir şey etməyən düymə isə
/// göstərilməməlidir.</para>
/// </summary>
public class BrowserShareService : IShareService
{
    private readonly IJSRuntime _js;

    public BrowserShareService(IJSRuntime js) => _js = js;

    public bool IsSupported => true;

    public async Task ShareTextAsync(string title, string text)
    {
        try
        {
            await _js.InvokeVoidAsync("petpalShare.shareText", title, text);
        }
        catch (Exception)
        {
            // Paylaşma açılmadı — bu, uşağın oyununu dayandırmamalıdır.
        }
    }

    public async Task ShareImageAsync(string title, string fileName, string base64Png)
    {
        try
        {
            await _js.InvokeVoidAsync("petpalShare.shareImage", title, fileName, base64Png);
        }
        catch (Exception)
        {
        }
    }
}
