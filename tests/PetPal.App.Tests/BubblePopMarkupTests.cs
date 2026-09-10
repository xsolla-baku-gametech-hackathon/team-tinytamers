using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PetPal.App.Tests;

/// <summary>
/// Baloncuq ovunun axını bir Razor detalından asılıdır və o detal kompilyasiya
/// xətası vermir: siyahı elementi <c>@key</c> ilə açarlanmasa, Blazor DOM
/// elementlərini yenidən istifadə edir və davam edən CSS animasiyası sıfırlanır —
/// yəni hər renderdə baloncuqlar ekranın dibindən yenidən başlayır.
///
/// Səhv yalnız işləyən app-də görünür, ona görə mənbə səviyyəsində qorunur.
/// </summary>
public class BubblePopMarkupTests
{
    [Fact]
    public void BaloncuqDuymeleri_KeyIleAcarlanir()
    {
        var markup = ReadGame();

        var button = Regex.Match(markup, @"<button[^>]*class=""bubble-pop[^>]*>", RegexOptions.Singleline);

        Assert.True(button.Success, "bubble-pop düyməsi tapılmadı — test köhnəlib.");
        Assert.Contains("@key", button.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Raund dəyişəndə yalnız qayda dəyişməlidir. Siyahının təmizlənməsi
    /// (<c>_bubbles.Clear()</c>) axını sıfırlayır — köhnə davranış məhz bu idi.
    /// </summary>
    [Fact]
    public void RaundDeyisikliyi_BaloncuqlariSilmir()
    {
        var markup = ReadGame();

        var startRound = Regex.Match(
            markup,
            @"private void StartRound\(\).*?\n    \}",
            RegexOptions.Singleline);

        Assert.True(startRound.Success, "StartRound metodu tapılmadı — test köhnəlib.");
        Assert.DoesNotContain("_bubbles", startRound.Value, StringComparison.Ordinal);
    }

    private static string ReadGame([CallerFilePath] string testFilePath = "")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFilePath)!, "..", ".."));
        var game = Path.Combine(
            repositoryRoot, "src", "PetPal.App.Ui", "Components", "Shared", "BubblePopGame.razor");

        Assert.True(File.Exists(game), $"Fayl tapılmadı: {game}");
        return File.ReadAllText(game);
    }
}
