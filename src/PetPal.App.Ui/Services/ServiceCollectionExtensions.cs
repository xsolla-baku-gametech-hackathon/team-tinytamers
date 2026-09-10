using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PetPal.App.Ui.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// UI kitabxanasının bütün servislərini qeydiyyatdan keçirir.
    /// Host layihə yalnız API ünvanını və platformaya bağlı implementasiyaları verir.
    /// </summary>
    public static IServiceCollection AddPetPalUi(this IServiceCollection services, Uri apiBaseAddress)
    {
        // Dil servisi sessiyadan ƏVVƏL qurulur: AppSession dil dəyişikliyini
        // ona ötürür, ekranlar isə ondan oxuyur.
        services.AddSingleton<Loc>();
        services.AddSingleton<AppSession>();
        services.AddSingleton<AppState>();

        // Platforma implementasiyası verilməyibsə, app yenə də işə düşür.
        services.TryAddSingleton<ITokenStore, InMemoryTokenStore>();
        services.TryAddSingleton<IPhotoPicker, NullPhotoPicker>();
        services.TryAddSingleton<IPetSpeech, NullPetSpeech>();
        services.TryAddSingleton<IShareService, NullShareService>();
        services.TryAddSingleton<IBiometricGate, NullBiometricGate>();
        services.AddSingleton<AchievementCard>();
        services.TryAddSingleton<IPushTokenProvider, NullPushTokenProvider>();
        services.AddSingleton<PushRegistration>();
        services.AddSingleton<PendingInvite>();
        services.AddSingleton<InviteCodeReader>();

        services.AddHttpClient<AuthApiClient>(client => ConfigureClient(client, apiBaseAddress));
        services.AddHttpClient<GameApiClient>(client => ConfigureClient(client, apiBaseAddress));
        services.AddHttpClient<ParentApiClient>(client => ConfigureClient(client, apiBaseAddress));

        // Arenanın real vaxt kanalı: bir app = bir bağlantı, ona görə singleton.
        // Kanal açılmasa app polling-siz işləməyə davam edir — bax LiveClient.
        services.AddSingleton(sp => new LiveClient(sp.GetRequiredService<AppSession>(), apiBaseAddress));

        // Açılışda saxlanmış sessiyanı bərpa edir. Singleton-dur ki, bərpa
        // app-in ömründə bir dəfə işləsin.
        services.AddSingleton<SessionRestorer>();

        return services;
    }

    private static void ConfigureClient(HttpClient client, Uri baseAddress)
    {
        client.BaseAddress = baseAddress;

        // Mobil şəbəkədə sonsuz gözləmə əvəzinə aydın xəta mesajı göstərilir.
        client.Timeout = TimeSpan.FromSeconds(20);
    }
}

/// <summary>Yaddaşda saxlayan default — app bağlananda token itir, təhlükəsizdir.</summary>
public class InMemoryTokenStore : ITokenStore
{
    private readonly Dictionary<string, string> _values = new();

    public Task<string?> GetAsync(string key) =>
        Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

    public Task SetAsync(string key, string value)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _values.Remove(key);
        return Task.CompletedTask;
    }
}
