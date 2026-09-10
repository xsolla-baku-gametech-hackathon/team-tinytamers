using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PetPal.Api.Notifications;

/// <summary>
/// FCM HTTP v1 üzərindən göndərir.
///
/// <para>Hər token AYRICA sorğudur: v1-də toplu göndəriş yoxdur. Bir ailənin
/// cihaz sayı azdır (valideyn + bir-iki uşaq cihazı), ona görə bu, praktikada
/// bir neçə sorğu deməkdir.</para>
///
/// <para><b>Xəta bildirişi udur, əməliyyatı yox.</b> Bildiriş göndərilməməsi
/// dostluq sorğusunun yaranmasını və ya missiyanın tamamlanmasını ləğv
/// etməməlidir — məlumat app-də onsuz da görünür.</para>
/// </summary>
public class FirebaseNotificationSender : INotificationSender
{
    private readonly HttpClient _http;
    private readonly FirebaseAccessToken _accessToken;
    private readonly NotificationOptions _options;
    private readonly ILogger<FirebaseNotificationSender> _logger;
    private readonly ServiceAccountKey? _key;

    public FirebaseNotificationSender(
        HttpClient http,
        FirebaseAccessToken accessToken,
        IOptions<NotificationOptions> options,
        ILogger<FirebaseNotificationSender> logger)
    {
        _http = http;
        _accessToken = accessToken;
        _options = options.Value;
        _logger = logger;

        _key = ParseKey(_options.ServiceAccountJson, logger);
    }

    public bool IsConfigured => _options.IsConfigured && _key is { IsValid: true };

    public async Task<IReadOnlyCollection<string>> SendAsync(
        IReadOnlyCollection<string> tokens, PushMessage message, CancellationToken ct = default)
    {
        if (!IsConfigured || tokens.Count == 0)
            return [];

        var accessToken = await _accessToken.GetAsync(_key!, ct);

        if (accessToken is null)
        {
            _logger.LogWarning("FCM giriş bileti alınmadı — bildiriş göndərilmədi.");
            return [];
        }

        var url = $"https://fcm.googleapis.com/v1/projects/{_options.ProjectId}/messages:send";
        var stale = new List<string>();

        foreach (var token in tokens)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(new
                    {
                        message = new
                        {
                            token,
                            notification = new { title = message.Title, body = message.Body },

                            // Marşrut app-in hansı ekranı açacağını deyir.
                            data = message.Route is null
                                ? null
                                : new Dictionary<string, string> { ["route"] = message.Route }
                        }
                    })
                };

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                using var response = await _http.SendAsync(request, ct);

                // 404/403 — token artıq yoxdur (app silinib və ya token dəyişib).
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                {
                    stale.Add(token);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning("FCM bildirişi göndərilmədi: {Status}", response.StatusCode);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "FCM bildirişi göndərilmədi.");
            }
        }

        return stale;
    }

    private static ServiceAccountKey? ParseKey(string json, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ServiceAccountKey>(json);
        }
        catch (JsonException ex)
        {
            // Səhv açar app-in qalxmasını DAYANDIRMIR: bildirişlər könüllü qatdır.
            logger.LogError(ex, "Notifications:ServiceAccountJson oxunmadı — bildirişlər söndürülüb.");
            return null;
        }
    }
}
