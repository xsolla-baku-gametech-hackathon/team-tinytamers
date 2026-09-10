using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PetPal.App.Tests;

/// <summary>
/// Mini oyunların üç qaydası kompilyasiya xətası vermir, amma pozulanda oyun
/// sınır və ya səssizcə başqa oyun açılır. Hər üçü mənbə səviyyəsində qorunur.
/// </summary>
public class GameMarkupTests
{
    /// <summary>Kataloqdakı hər açar üçün burada bir komponent olmalıdır.</summary>
    private static readonly string[] Games =
    [
        "MemoryMatchGame", "QuickTapGame", "BubblePopGame", "ColorEchoGame",
        "StarRunGame", "FruitSliceGame", "BasketCatchGame", "CloudJumpGame",
        "LetterHuntGame", "ChefOrderGame"
    ];

    /// <summary>
    /// Hər mini oyunda can sistemi olmalıdır: səhv etmək bədəllidir, üçüncü
    /// səhvdə oyun bitir. Yeni oyun əlavə edən adam bunu unudarsa, oyun
    /// "sonsuz şans" rejimində qalır və qalanlarından fərqlənir.
    ///
    /// Ürəkləri artıq ortaq çərçivə çəkir (GameShell), ona görə oyunun özündə
    /// axtarılan şey <c>Lives</c> parametrinin ötürülməsidir.
    /// </summary>
    [Fact]
    public void ButunMiniOyunlar_CanSistemiIsledir()
    {
        foreach (var game in Games)
        {
            var markup = ReadComponent(game);

            Assert.Contains("Lives=\"_lives\"", markup, StringComparison.Ordinal);
            Assert.Contains("StartingLives", markup, StringComparison.Ordinal);

            // Can itkisinin ASENXRON olması şərt deyil — "Səbət tut"da gözləmə
            // tik dövrünü bağlayırdı, ona görə orada metod sinxrondur. Axtarılan
            // şey adın forması yox, can itkisinin işlənməsidir.
            Assert.Contains("LoseLife", markup, StringComparison.Ordinal);
        }

        // Ürəkləri həqiqətən çəkən yer çərçivədir.
        Assert.Contains("<GameLives", ReadComponent("GameShell"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Hər oyun eyni çərçivəni geyinir: üst zolaq, qayda, vaxt, nəticə. Bir
    /// oyun öz HUD-unu özü çəksə, uşaq həmin ekranda interfeysi yenidən
    /// öyrənməli olur — və çıxış düyməsi də yerini dəyişir.
    /// </summary>
    [Fact]
    public void ButunMiniOyunlar_OrtaqCerceveniGeyinir()
    {
        foreach (var game in Games)
        {
            var markup = ReadComponent(game);

            Assert.Contains("<GameShell", markup, StringComparison.Ordinal);
            Assert.Contains("ScoreLabel", markup, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Kataloqa yeni açar əlavə edib Play ekranında hal yazmamaq SƏSSİZ xətadır:
    /// switch-in default budağı işə düşür və uşaq kafeldə gördüyü oyunun
    /// əvəzinə "Sürətli hesab" oynayır. Ona görə açarlar mənbədən oxunur.
    /// </summary>
    [Fact]
    public void HerKataloqAcari_PlayEkranindaBaglanib()
    {
        var keys = Regex.Matches(ReadCatalog(), @"public const string \w+ = ""([a-z-]+)"";")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.True(keys.Count >= 10, $"Kataloqdan yalnız {keys.Count} açar oxundu — test köhnəlib.");

        var play = ReadPlayPage();

        foreach (var key in keys)
            Assert.Contains($"case \"{key}\":", play, StringComparison.Ordinal);
    }

    private static string ReadComponent(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Components", "Shared", name + ".razor"));

    private static string ReadPlayPage([CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Pages", "Play.razor"));

    private static string ReadCatalog([CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.Api", "Games", "GameCatalog.cs"));
}
