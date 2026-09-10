namespace PetPal.App.Ui.Services;

/// <summary>
/// Kamera/qalereya girişi platformaya bağlıdır. UI kitabxanası MAUI-dən asılı
/// olmasın deyə interfeys burada saxlanılır (host layihə MediaPicker ilə implementasiya edir).
/// </summary>
public interface IPhotoPicker
{
    /// <summary>Şəkli base64 mətn kimi qaytarır; istifadəçi imtina edərsə <c>null</c>.</summary>
    Task<string?> CaptureAsync();
}

/// <summary>Kamerası olmayan mühitlər (test, masaüstü preview) üçün təhlükəsiz default.</summary>
public class NullPhotoPicker : IPhotoPicker
{
    public Task<string?> CaptureAsync() => Task.FromResult<string?>(null);
}
