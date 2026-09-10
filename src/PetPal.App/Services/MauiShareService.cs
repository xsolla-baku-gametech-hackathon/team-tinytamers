using PetPal.App.Ui.Services;

namespace PetPal.App.Services;

/// <summary>
/// Cihazın öz paylaşma vərəqi (MAUI <see cref="Share"/>). Şəkil fayl kimi
/// paylaşılır, ona görə əvvəlcə keş qovluğuna yazılır — paylaşma vərəqi
/// baytları yox, yol istəyir.
/// </summary>
public class MauiShareService : IShareService
{
    public bool IsSupported => true;

    public async Task ShareTextAsync(string title, string text)
    {
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = title,
                Text = text
            });
        }
        catch (Exception)
        {
            // İstifadəçi imtina etdi və ya vərəq açılmadı — app davam edir.
        }
    }

    public async Task ShareImageAsync(string title, string fileName, string base64Png)
    {
        try
        {
            // Keş qovluğu seçilib: kart bir dəfəlik paylaşma üçündür və
            // sistemin özü onu təmizləyə bilər.
            var path = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(path, Convert.FromBase64String(base64Png));

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = title,
                File = new ShareFile(path)
            });
        }
        catch (Exception)
        {
        }
    }
}
