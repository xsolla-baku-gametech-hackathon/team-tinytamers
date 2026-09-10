using Microsoft.Maui.Graphics.Platform;
using PetPal.App.Ui.Services;

namespace PetPal.App.Services;

/// <summary>
/// "Real Life Connect" üçün kamera. Kamera yoxdursa və ya icazə verilməyibsə,
/// qalereyaya keçir; hər ikisi mümkün deyilsə <c>null</c> qaytarır və axın şəkilsiz davam edir.
/// </summary>
public class MauiPhotoPicker : IPhotoPicker
{
    /// <summary>
    /// Server limiti ilə eyni olmalıdır (Discovery:MaxPhotoBytes). Kiçik olsa,
    /// uşaq serverin qəbul etdiyi şəkli app-də seçə bilməzdi; böyük olsa, fayl
    /// şəbəkədən keçib serverdə rədd olunardı — yəni gözləmə boşuna gedərdi.
    /// </summary>
    private const long MaxBytes = 30 * 1024 * 1024;

    /// <summary>
    /// Kamera 10–20 MB-lıq şəkil çıxarır. Belə fayl həm yavaş yüklənir, həm də
    /// kolleksiyada geri baxanda eyni yavaşlığı təkrarlayır. Kəşf şəkli üçün
    /// 1600 px artıqlaması ilə kifayətdir — brauzer versiyası da eyni ölçünü
    /// işlədir (<c>wwwroot/js/photoPicker.js</c>).
    /// </summary>
    private const float MaxEdge = 1600f;

    private const float JpegQuality = 0.85f;

    public async Task<string?> CaptureAsync()
    {
        var photo = await TakeOrPickAsync();
        if (photo is null)
            return null;

        using var stream = await photo.OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);

        if (memory.Length > MaxBytes)
            throw new InvalidOperationException("Şəkil çox böyükdür.");

        var bytes = Shrink(memory) ?? memory.ToArray();

        // Brauzer versiyası da data URL qaytarır — ekran və API iki platforma
        // arasında fərq görməsin deyə format burada da eyniləşdirilir.
        return $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}";
    }

    /// <summary>
    /// Kiçiltmə alınmasa <c>null</c> qayıdır və orijinal göndərilir: şəkilsiz
    /// qalmaqdansa böyük şəkil yaxşıdır. Cihaz kodlayıcısı bəzi formatları
    /// (məs. HEIC) tanımaya bilər.
    /// </summary>
    private static byte[]? Shrink(MemoryStream source)
    {
        try
        {
            source.Position = 0;

            using var image = PlatformImage.FromStream(source);
            if (image.Width <= MaxEdge && image.Height <= MaxEdge)
                return null;

            using var resized = image.Downsize(MaxEdge, disposeOriginal: false);
            using var output = new MemoryStream();
            resized.Save(output, ImageFormat.Jpeg, JpegQuality);

            return output.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<FileResult?> TakeOrPickAsync()
    {
        if (MediaPicker.Default.IsCaptureSupported)
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.Camera>();

            // Kamera bağlananda null qayıdır — o, "imtina" deməkdir və axın
            // burada bitir; arxasınca qalereyanı açmaq imtinanı saymamaq olardı.
            if (status == PermissionStatus.Granted)
                return await MediaPicker.Default.CapturePhotoAsync();
        }

        var picked = await MediaPicker.Default.PickPhotosAsync();
        return picked?.FirstOrDefault();
    }
}
