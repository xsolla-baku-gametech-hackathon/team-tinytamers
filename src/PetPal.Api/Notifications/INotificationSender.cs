namespace PetPal.Api.Notifications;

/// <summary>Bir cihaza göndəriləcək bildiriş.</summary>
public record PushMessage(string Title, string Body, string? Route = null);

/// <summary>
/// Push nəqliyyatı. Servis FCM detallarını görməsin deyə dar interfeys
/// arxasındadır — bildirişin NƏ VAXT göndərildiyi məhsul qərarıdır,
/// NECƏ göndərildiyi isə nəqliyyat detalı.
/// </summary>
public interface INotificationSender
{
    /// <summary>Konfiqurasiya yoxdursa <c>false</c> — servis heç nə hesablamır.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Göndərir və İŞLƏMƏYƏN tokenləri qaytarır — çağıran onları bazadan silir.
    /// Cihaz app-i silibsə FCM tokeni «qeydiyyatdan çıxmış» kimi cavablayır.
    /// </summary>
    Task<IReadOnlyCollection<string>> SendAsync(
        IReadOnlyCollection<string> tokens, PushMessage message, CancellationToken ct = default);
}

/// <summary>
/// Konfiqurasiya olmayanda işləyən tətbiq: heç nə göndərmir.
/// Testlərdə və lokal işdə standart budur.
/// </summary>
public class NullNotificationSender : INotificationSender
{
    public bool IsConfigured => false;

    public Task<IReadOnlyCollection<string>> SendAsync(
        IReadOnlyCollection<string> tokens, PushMessage message, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyCollection<string>>([]);
}
