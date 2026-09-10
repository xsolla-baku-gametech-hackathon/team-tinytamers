namespace PetPal.App.Ui.Services;

/// <summary>
/// Cihazın paylaşma vərəqi. Platformaya bağlıdır, ona görə interfeys burada
/// saxlanılır və host layihələr onu öz üsulu ilə tətbiq edir — eyni ilə
/// <see cref="IPhotoPicker"/> kimi.
///
/// <para><b>Bu servis yalnız VALİDEYN ekranlarından çağırılır.</b> Uşağa xam
/// paylaşma vərəqi vermək telefondakı bütün tətbiqlərə qapı açmaqdır: söhbət
/// özəlliyində beş qatlı filtr qurub, sonra uşağa istənilən mesajlaşma
/// proqramına birbaşa çıxış vermək ziddiyyət olardı. Uşaq ekranında paylaşım
/// yalnız «kodu göstər» formasındadır.</para>
/// </summary>
public interface IShareService
{
    /// <summary>Paylaşma cihazda ümumiyyətlə mümkündürmü — düymə boş yerə göstərilməsin.</summary>
    bool IsSupported { get; }

    /// <summary>Mətn (və könüllü link) paylaşır. İstifadəçi imtina edərsə heç nə olmur.</summary>
    Task ShareTextAsync(string title, string text);

    /// <summary>
    /// PNG şəkli paylaşır. <paramref name="base64Png"/> — «data:» prefiksi
    /// OLMADAN xam base64 məzmun.
    /// </summary>
    Task ShareImageAsync(string title, string fileName, string base64Png);
}

/// <summary>
/// Paylaşması olmayan mühitlər (test, preview) üçün təhlükəsiz default:
/// heç nə etmir və <see cref="IsSupported"/> ilə bunu açıq deyir.
/// </summary>
public class NullShareService : IShareService
{
    public bool IsSupported => false;

    public Task ShareTextAsync(string title, string text) => Task.CompletedTask;

    public Task ShareImageAsync(string title, string fileName, string base64Png) => Task.CompletedTask;
}
