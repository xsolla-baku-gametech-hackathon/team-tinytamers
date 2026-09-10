using System.Runtime.CompilerServices;

namespace PetPal.App.Tests;

/// <summary>
/// Yumurta halının qaydası: pet doğulmayıb, deməli ona QULLUQ da yoxdur.
///
/// <para>Server bunu onsuz da bilir — <c>PetService</c> yumurtaya qulluğu
/// <c>StillAnEgg</c> ilə rədd edir. Ev ekranı isə bilmirdi: dörd ölçər həmişə
/// çəkilirdi (hamısı 0%) və dörd qulluq kafeli <c>action--alert</c> alıb
/// çəhrayı yanıb-sönürdü. Nəticədə ekran uşağa «petin acdır, çirklidir»
/// deyirdi, halbuki pet hələ yumurtadadır və edilməli olan tək iş ulduz
/// yığmaqdır.</para>
///
/// <para>Bu testlər həmin qaydanı mənbə səviyyəsində saxlayır — pozulanda
/// kompilyasiya sınmır, sadəcə ekran yenidən yalan danışır.</para>
/// </summary>
public class EggStateMarkupTests
{
    /// <summary>Ölçərlər yalnız pet çıxandan sonra göstərilir.</summary>
    [Fact]
    public void QulluqOlcerleri_YalnizCixandanSonraGorunur()
    {
        var markup = ReadHome();

        var guard = markup.IndexOf("@if (_home.Pet.IsHatched)", StringComparison.Ordinal);
        var gauges = markup.IndexOf("<div class=\"gauges\">", StringComparison.Ordinal);

        Assert.True(guard >= 0, "IsHatched şərti yoxdur — ölçərlər yumurtada da çəkilir.");
        Assert.True(gauges > guard, "Ölçərlər şərtin içində deyil.");
        Assert.True(gauges - guard < 300, "Ölçərlər şərtdən çox uzaqdır — şərt başqa bloka aiddir.");
    }

    /// <summary>
    /// Yumurtada qulluq kafelləri OLMAMALIDIR: server onları rədd edir, yəni
    /// uşaq basıb yalnız xəta alır.
    /// </summary>
    [Fact]
    public void QulluqKafelleri_YumurtadaGosterilmir()
    {
        var markup = ReadHome();

        var eggBranch = markup.IndexOf("@if (!_home.Pet.IsHatched)", StringComparison.Ordinal);
        Assert.True(eggBranch >= 0, "Kafellər üçün yumurta budağı yoxdur.");

        var fiveTiles = markup.IndexOf("actions actions--five", StringComparison.Ordinal);
        Assert.True(fiveTiles > eggBranch, "Beş kafellik sıra yumurta budağından əvvəl gəlir.");

        // Yumurta budağında yalnız oyun kafeli var — qulluq marşrutları yoxdur.
        var eggMarkup = markup[eggBranch..fiveTiles];
        Assert.DoesNotContain("care?mode=feed", eggMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("care?mode=bath", eggMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("care?mode=sleep", eggMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("care?mode=closet", eggMarkup, StringComparison.Ordinal);
        Assert.Contains("href=\"play\"", eggMarkup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Yumurta DANIŞMIR: söhbət də qulluq kimi çıxandan sonra açılan
    /// özəllikdir. Bulud qalır, amma toxunulan giriş nöqtəsi deyil.
    /// </summary>
    [Fact]
    public void Sohbet_YumurtadaBaglidir()
    {
        var markup = ReadHome();

        Assert.Contains("@if (_home.ChatEnabled && _home.Pet.IsHatched)", markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ölçərlərin yerini yumurta proqresi tutur — uşaq nə etməli olduğunu
    /// görməlidir, boş yer qalmamalıdır.
    /// </summary>
    [Fact]
    public void OlcerlerinYerinde_YumurtaProqresiVar()
    {
        Assert.Contains("hatchbar", ReadHome(), StringComparison.Ordinal);
        Assert.Contains(".hatchbar {", ReadCss("app.css"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Barmaq izi qapını ÖZÜ açmamalıdır.
    ///
    /// <para>İlk variant biometrika uğurlu olan kimi <c>_unlocked = true</c>
    /// edirdi və serverə heç nə soruşmurdu. Ailə planşetində uşağın barmaq izi
    /// qeydiyyatdadırsa, bu, uşağın qarşısına qoyulmuş açıq düymə idi. Zəncir
    /// belə olmalıdır: barmaq izi → anbardakı PIN → server yoxlaması.</para>
    /// </summary>
    [Fact]
    public void Biometrika_ServerYoxlamasindanKecir()
    {
        var page = ReadParent();
        var body = Body(page, "private async Task UnlockWithBiometricsAsync()");

        Assert.Contains("Tokens.GetAsync(TokenStoreKeys.ParentGatePin)", body, StringComparison.Ordinal);
        Assert.Contains("ParentApi.UnlockGateAsync", body, StringComparison.Ordinal);

        // Server cavabı yoxlanmadan qapı açılmamalıdır.
        var unlockCall = body.IndexOf("ParentApi.UnlockGateAsync", StringComparison.Ordinal);
        var open = body.IndexOf("await OpenAsync()", StringComparison.Ordinal);
        Assert.True(open > unlockCall, "Qapı server cavabından ƏVVƏL açılır.");
    }

    /// <summary>
    /// Qısa yol avtomatik təklif olunmamalıdır: valideyn onu özü qoşur, çünki
    /// cihazda uşağın da barmaq izi qeydiyyatda ola bilər.
    /// </summary>
    [Fact]
    public void Biometrika_ValideynOzuQosur()
    {
        var page = ReadParent();

        Assert.Contains("EnableBiometricsAsync", page, StringComparison.Ordinal);
        Assert.Contains("DisableBiometricsAsync", page, StringComparison.Ordinal);

        // Düymə yalnız anbarda PIN olanda görünür (_biometricsAvailable),
        // cihaz dəstəyi (_biometricsSupported) tək başına kifayət etmir.
        var enable = Body(page, "private async Task EnableBiometricsAsync()");
        Assert.Contains("ParentApi.UnlockGateAsync", enable, StringComparison.Ordinal);
        Assert.Contains("Tokens.SetAsync(TokenStoreKeys.ParentGatePin", enable, StringComparison.Ordinal);
    }

    /// <summary>Metodun gövdəsini adı ilə çıxarır (sadə mötərizə sayğacı).</summary>
    private static string Body(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{signature} tapılmadı — test köhnəlib.");

        var open = source.IndexOf('{', start);
        var depth = 0;

        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0)
                return source[open..i];
        }

        throw new InvalidOperationException($"{signature} gövdəsi bağlanmayıb.");
    }

    private static string ReadParent([CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Pages", "ParentDashboard.razor"));

    private static string ReadHome([CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Pages", "Home.razor"));

    private static string ReadCss(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "wwwroot", "css", name));
}
