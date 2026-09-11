using PetPal.Shared.Enums;

namespace PetPal.App.Ui.Services;

/// <summary>
/// Macəra ekranındakı BİR AI rəsminin yüklənmə vəziyyəti — arxa fon və obraz
/// üçün eyni məntiq.
///
/// <para>Ekran rəsmi HEÇ VAXT gözləmir: mərhələ deterministik səhnə ilə dərhal
/// açılır, rəsm isə hazır olanda üstünə düşür. Ona görə burada "yüklənir"
/// vəziyyəti ekranı bloklamır, sadəcə sehrli tozu yandırır.</para>
///
/// <para>Açar rəsmin ÜNVANIDIR (içində səhnənin hash-ı var). Seçim dəyişəndə
/// ünvan da dəyişir — yəni köhnə rəsm bir kadr belə yeni səhnədə görünmür.</para>
/// </summary>
public sealed class PetBrainArtSlot
{
    /// <summary>Hələ çəkilən rəsm üçün ən çox neçə yoxlama.</summary>
    private const int MaxAttempts = 40;

    private const int FirstDelayMilliseconds = 250;
    private const int MaxDelayMilliseconds = 2_000;

    private string _url = string.Empty;
    private bool _loading;

    /// <summary>Rəsmin gəlməyəcəyi bəllidir: yoxlama dayanır, toz sönür.</summary>
    private bool _gone;

    public string? Image { get; private set; }

    /// <summary>Rəsm yoldadır, amma hələ ekranda deyil.</summary>
    public bool Waiting => _url.Length > 0 && Image is null && !_gone;

    /// <summary>
    /// Serverin verdiyi vəziyyəti izləyir və lazımdırsa yükləməni başladır.
    ///
    /// <para>Hər render-dən çağırılır və təkrardan özü qorunur: eyni ünvan üçün
    /// ikinci yükləmə başlamır, dəyişən ünvan isə köhnə rəsmi ATIR.</para>
    /// </summary>
    public void Track(
        string url,
        PetBrainIllustrationStatus status,
        Func<string, CancellationToken, Task<string?>> fetchAsync,
        Func<Task> changedAsync,
        CancellationToken ct)
    {
        var target = status == PetBrainIllustrationStatus.Fallback ? string.Empty : url;

        if (!string.Equals(target, _url, StringComparison.Ordinal))
        {
            _url = target;
            Image = null;
            _loading = false;
            _gone = false;
        }

        if (_url.Length == 0 || Image is not null || _loading || _gone)
            return;

        _loading = true;
        _ = LoadAsync(_url, status == PetBrainIllustrationStatus.Pending, fetchAsync, changedAsync, ct);
    }

    /// <summary>Ekrandan çıxarkən: növbəti render köhnə rəsmi göstərməsin.</summary>
    public void Clear()
    {
        _url = string.Empty;
        Image = null;
        _loading = false;
        _gone = false;
    }

    private async Task LoadAsync(
        string url,
        bool waitUntilReady,
        Func<string, CancellationToken, Task<string?>> fetchAsync,
        Func<Task> changedAsync,
        CancellationToken ct)
    {
        var delay = FirstDelayMilliseconds;
        var attempts = waitUntilReady ? MaxAttempts : 1;

        try
        {
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                if (ct.IsCancellationRequested || !Owns(url))
                    return;

                var image = await fetchAsync(url, ct);

                if (!Owns(url))
                    return;

                if (!string.IsNullOrEmpty(image))
                {
                    Image = image;
                    await changedAsync();
                    return;
                }

                if (attempt < attempts - 1)
                {
                    await Task.Delay(delay, ct);
                    delay = Math.Min(delay * 2, MaxDelayMilliseconds);
                }
            }

            _gone = true;
            await changedAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (Owns(url))
                _loading = false;
        }
    }

    private bool Owns(string url) => string.Equals(_url, url, StringComparison.Ordinal);
}
