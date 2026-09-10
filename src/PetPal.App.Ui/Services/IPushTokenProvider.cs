namespace PetPal.App.Ui.Services;

/// <summary>
/// Cihazın push ünvanını (FCM registration token) verir.
///
/// <para>Token PLATFORMANIN öz Firebase SDK-sından gəlir, ona görə interfeys
/// burada saxlanılır — eyni ilə <see cref="IPhotoPicker"/> və
/// <see cref="IShareService"/> kimi.</para>
///
/// <para><b>Standart tətbiq boşdur.</b> Firebase layihəsi (Android üçün
/// <c>google-services.json</c>, iOS üçün <c>GoogleService-Info.plist</c>)
/// olmadan native SDK qurula bilməz; onlar əlavə ediləndə yeganə iş bu
/// interfeysi tətbiq etməkdir — qalan bütün axın hazırdır.</para>
/// </summary>
public interface IPushTokenProvider
{
    /// <summary>Token yoxdursa (icazə verilməyib və ya SDK qurulmayıb) <c>null</c>.</summary>
    Task<string?> GetTokenAsync();

    /// <summary>"android" · "ios" · "web".</summary>
    string Platform { get; }
}

/// <summary>Firebase qurulmayan mühitlər üçün təhlükəsiz default.</summary>
public class NullPushTokenProvider : IPushTokenProvider
{
    public string Platform => "none";

    public Task<string?> GetTokenAsync() => Task.FromResult<string?>(null);
}
