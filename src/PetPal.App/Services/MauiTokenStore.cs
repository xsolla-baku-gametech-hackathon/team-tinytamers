using System.Text.Json;
using PetPal.App.Ui.Services;

namespace PetPal.App.Services;

/// <summary>
/// Token-ləri platformanın təhlükəsiz anbarında saxlayır (Android Keystore /
/// iOS Keychain / Windows DataProtection).
///
/// SecureStorage bəzi quraşdırmalarda əlçatan olmur (xüsusən masaüstündə).
/// Əvvəl belə halda yalnız YADDAŞA keçilirdi — nəticədə app hər açılışda
/// sessiyanı itirirdi və valideyn e-poçtu yenidən yazırdı. İndi ehtiyat yol
/// app-in öz məlumat qovluğundakı fayldır: cihazdan çıxmır, app silinəndə
/// yoxa çıxır və yalnız təhlükəsiz anbar işləməyəndə istifadə olunur.
/// </summary>
public class MauiTokenStore : ITokenStore
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly string _filePath =
        Path.Combine(FileSystem.AppDataDirectory, "session.dat");

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            var secure = await SecureStorage.Default.GetAsync(key);
            if (secure is not null)
                return secure;
        }
        catch (Exception)
        {
            // Anbar əlçatan deyil — aşağıdakı fayl nüsxəsinə düşürük.
        }

        var values = await ReadFileAsync();
        return values.TryGetValue(key, out var value) ? value : null;
    }

    public async Task SetAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
            return;
        }
        catch (Exception)
        {
            // Aşağıda fayla yazılır.
        }

        await UpdateFileAsync(values => values[key] = value);
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            SecureStorage.Default.Remove(key);
        }
        catch (Exception)
        {
            // Anbar əlçatan deyilsə, yalnız fayl nüsxəsi təmizlənir.
        }

        await UpdateFileAsync(values => values.Remove(key));
    }

    private async Task<Dictionary<string, string>> ReadFileAsync()
    {
        await _fileLock.WaitAsync();

        try
        {
            if (!File.Exists(_filePath))
                return [];

            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (Exception)
        {
            // Fayl zədələnibsə sessiya itir, amma app açılır.
            return [];
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task UpdateFileAsync(Action<Dictionary<string, string>> change)
    {
        var values = await ReadFileAsync();
        change(values);

        await _fileLock.WaitAsync();

        try
        {
            Directory.CreateDirectory(FileSystem.AppDataDirectory);
            await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(values));
        }
        catch (Exception)
        {
            // Yazıla bilmirsə sessiya sadəcə yadda qalmır — app işləməyə davam edir.
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
