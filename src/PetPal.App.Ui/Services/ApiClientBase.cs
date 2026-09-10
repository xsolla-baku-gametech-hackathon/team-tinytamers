using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PetPal.Shared.Dtos;
using PetPal.Shared.Dtos.Auth;

namespace PetPal.App.Ui.Services;

/// <summary>
/// Bütün API klientlərinin ortaq bazası: bearer token əlavə edir, 401 halında
/// bir dəfə token yeniləyib sorğunu təkrarlayır, xətanı istifadəçi mesajına çevirir.
/// </summary>
public abstract class ApiClientBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected ApiClientBase(HttpClient http, AppSession session, Loc loc)
    {
        Http = http;
        Session = session;
        Loc = loc;
    }

    protected HttpClient Http { get; }
    protected AppSession Session { get; }

    /// <summary>Xəta mesajlarını da uşaq oxuyur — onlar da onun dilində olmalıdır.</summary>
    protected Loc Loc { get; }

    /// <summary>Valideyn endpoint-lərinə uşaq tokeni ilə getmək olmaz — klient bunu açıq bildirir.</summary>
    protected virtual bool UseParentToken => false;

    protected Task<ApiResult<T>> GetAsync<T>(string url, CancellationToken ct = default) =>
        SendAsync<T>(() => new HttpRequestMessage(HttpMethod.Get, url), ct);

    protected Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(
        string url, TRequest body, CancellationToken ct = default) =>
        SendAsync<TResponse>(() => new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        }, ct);

    protected Task<ApiResult<TResponse>> PostAsync<TResponse>(string url, CancellationToken ct = default) =>
        SendAsync<TResponse>(() => new HttpRequestMessage(HttpMethod.Post, url), ct);

    protected Task<ApiResult<TResponse>> PutAsync<TRequest, TResponse>(
        string url, TRequest body, CancellationToken ct = default) =>
        SendAsync<TResponse>(() => new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        }, ct);

    protected Task<ApiResult<TResponse>> DeleteAsync<TResponse>(string url, CancellationToken ct = default) =>
        SendAsync<TResponse>(() => new HttpRequestMessage(HttpMethod.Delete, url), ct);

    /// <summary>
    /// Şəkil kimi ikili məzmunu <c>data:</c> URI-yə çevirir.
    ///
    /// <c>&lt;img src&gt;</c> bearer başlığı daşıya bilmir, ünvan isə token
    /// tələb edir — yəni şəkli birbaşa teqə vermək mümkün deyil. Bayt burada,
    /// adi sorğu ilə (401 → token yeniləmə də daxil) alınır və teqə hazır
    /// data URI kimi qaytarılır.
    /// </summary>
    protected Task<ApiResult<string>> GetDataUrlAsync(string url, CancellationToken ct = default) =>
        SendAsync<string>(() => new HttpRequestMessage(HttpMethod.Get, url), async (response, token) =>
        {
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var bytes = await response.Content.ReadAsByteArrayAsync(token);

            return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
        }, ct);

    /// <summary>
    /// İkili məzmunu BAYT kimi qaytarır — <c>data:</c> URI-yə sığmayan media üçün.
    ///
    /// <para>Video bir neçə meqabaytdır: onu base64 mətni kimi DOM-a yazmaq
    /// yaddaşı artırar və iOS WebView-da oynamaya bilər. Bayt burada eyni bearer
    /// sorğusu ilə (401 → token yeniləmə də daxil) alınır, ekran isə ondan Blob
    /// ünvanı düzəldir.</para>
    /// </summary>
    protected Task<ApiResult<byte[]>> GetBytesAsync(string url, CancellationToken ct = default) =>
        SendAsync<byte[]>(
            () => new HttpRequestMessage(HttpMethod.Get, url),
            async (response, token) => await response.Content.ReadAsByteArrayAsync(token),
            ct);

    private Task<ApiResult<T>> SendAsync<T>(Func<HttpRequestMessage> requestFactory, CancellationToken ct) =>
        SendAsync<T>(requestFactory, async (response, token) =>
        {
            // 204-də gövdə yoxdur; JSON oxumaq cəhdi xəta verərdi.
            if (response.StatusCode == HttpStatusCode.NoContent)
                return default;

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, token);
        }, ct);

    private async Task<ApiResult<T>> SendAsync<T>(
        Func<HttpRequestMessage> requestFactory,
        Func<HttpResponseMessage, CancellationToken, Task<T?>> readAsync,
        CancellationToken ct)
    {
        try
        {
            var response = await SendOnceAsync(requestFactory(), ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshAsync(ct))
            {
                response.Dispose();
                response = await SendOnceAsync(requestFactory(), ct);
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                    return ApiResult<T>.Ok(await readAsync(response, ct), (int)response.StatusCode);

                return ApiResult<T>.Fail(await ReadErrorAsync(response, ct), (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.Fail(Loc.T("İnternetə qoşulmaq alınmadı. Bağlantını yoxlayın.", "Could not reach the internet. Check your connection."));
        }
        catch (JsonException)
        {
            return ApiResult<T>.Fail(Loc.T("Serverdən gözlənilməz cavab gəldi.", "The server sent an unexpected answer."));
        }
    }

    private Task<HttpResponseMessage> SendOnceAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Serverin mesajları da uşağın dilində gəlsin deyə hər sorğu interfeysin
        // dilini daşıyır. Uşaq profili tapılmayanda serverin başqa mənbəyi yoxdur.
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(Loc.Language));

        var token = UseParentToken ? Session.ParentAccessToken : Session.CurrentAccessToken;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return Http.SendAsync(request, ct);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        var refreshToken = UseParentToken
            ? Session.ParentSession?.RefreshToken
            : Session.ChildSession?.RefreshToken ?? Session.ParentSession?.RefreshToken;

        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        using var response = await Http.PostAsJsonAsync(
            "api/auth/refresh", new RefreshRequest { RefreshToken = refreshToken }, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
            return false;

        var refreshed = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions, ct);
        if (refreshed is null)
            return false;

        await Session.ApplyRefreshedAsync(refreshed);
        return true;
    }

    private async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions, ct);
            if (!string.IsNullOrWhiteSpace(error?.Message))
                return error.Message;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Cavab JSON deyilsə, aşağıdakı ümumi mesaja düşürük.
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => Loc.T("Sessiya bitib. Yenidən daxil olun.", "Your session has ended. Please sign in again."),
            HttpStatusCode.Forbidden => Loc.T("Bu əməliyyat üçün icazə yoxdur.", "You do not have permission for this."),
            HttpStatusCode.NotFound => Loc.T("Məlumat tapılmadı.", "Nothing was found."),
            HttpStatusCode.TooManyRequests => Loc.T("Çox sürətli cəhd. Bir az gözləyin.", "Too many tries. Wait a moment."),
            _ => Loc.T("Nəsə səhv getdi. Bir azdan yenidən cəhd edin.", "Something went wrong. Try again in a moment.")
        };
    }
}
