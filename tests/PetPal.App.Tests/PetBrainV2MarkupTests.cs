using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PetPal.App.Tests;

/// <summary>
/// Pet Brain V2 ekranlarının mənbə səviyyəsindəki qaydaları.
///
/// <para>Budaqlanan hekayə ekrana iki yeni növ gətirdi (<c>Consequence</c>,
/// <c>Ending</c>) və uşağa iki yeni hərəkət verdi ("başqa fikir", "sonra").
/// Bunların hər biri unudulanda app KOMPİLYASİYA OLUNUR — səhv yalnız uşaq
/// həmin ekrana çatanda görünür. Ona görə mətnin özündə qorunur.</para>
/// </summary>
public class PetBrainV2MarkupTests
{
    // ==================== Yeni ekran növləri ====================

    /// <summary>
    /// <b>Hər</b> mərhələ növünün ekran qarşılığı var.
    ///
    /// <para>Açarlar enum-un MƏNBƏYİNDƏN oxunur: yeni növ əlavə edib ekran
    /// yazmamaq səssiz xətadır — uşaq boş panel görər.</para>
    /// </summary>
    [Fact]
    public void HerMerheleNovu_EkranaBaglidir()
    {
        var kinds = EnumMembers("PetBrainStageKind");

        Assert.True(kinds.Count >= 5, $"Yalnız {kinds.Count} növ oxundu — test köhnəlib.");

        var page = ReadPage("PetBrain.razor");

        foreach (var kind in kinds)
        {
            // Tapmaca lövhəyə, seçim isə StageChoices komponentinə gedir.
            if (kind == "Puzzle")
            {
                Assert.Contains("<PuzzleBoard", page, StringComparison.Ordinal);
                continue;
            }

            if (kind == "Choice")
            {
                Assert.Contains("<StageChoices", page, StringComparison.Ordinal);
                continue;
            }

            Assert.Contains($"PetBrainStageKind.{kind}", page, StringComparison.Ordinal);
        }
    }

    /// <summary>Nəticə və sonluq ekranlarının uşağa görünən düyməsi var.</summary>
    [Fact]
    public void NeticeVeSonluq_DavamDuymesiDasiyir()
    {
        var page = ReadPage("PetBrain.razor");

        Assert.Contains("PetBrainStageKind.Consequence", page, StringComparison.Ordinal);
        Assert.Contains("PetBrainStageKind.Ending", page, StringComparison.Ordinal);

        Assert.Contains("Loc.T(\"Davam\", \"Keep going\")", page, StringComparison.Ordinal);
        Assert.Contains("Loc.T(\"Macərəni bitir\", \"Finish the adventure\")", page, StringComparison.Ordinal);
    }

    // ==================== Uşağın "yox" demək hüququ ====================

    /// <summary>«Başqa fikir» və «sonra» ekranda VAR və hər ikisi ikidillidir.</summary>
    [Fact]
    public void AlternativTekliflər_EkrandaVarVeIkidillidir()
    {
        var page = ReadPage("PetBrain.razor");

        Assert.Contains("Loc.T(\"Başqa fikir\", \"Another idea\")", page, StringComparison.Ordinal);
        Assert.Contains("Loc.T(\"Sonra\", \"Later\")", page, StringComparison.Ordinal);

        Assert.Contains("ShowAnotherAsync", page, StringComparison.Ordinal);
        Assert.Contains("NotNowAsync", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// «Başqa fikir» yalnız SERVER icazə verəndə görünür — sonsuz yeniləmə
    /// klient qərarı ola bilməz.
    /// </summary>
    [Fact]
    public void BasqaFikir_ServerinIcazesineBaglidir()
    {
        var page = ReadPage("PetBrain.razor");

        Assert.Contains("recommendation.CanShowAnother", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// Cavab SERVERİN qərar id-si ilə göndərilir: klient nə şablon, nə də bal
    /// dəyişikliyi seçə bilmir.
    /// </summary>
    [Fact]
    public void Cavab_QerarIdIleGondərilir()
    {
        var page = ReadPage("PetBrain.razor");
        var client = ReadService("GameApiClient.cs");

        Assert.Contains("_state.Recommendation.DecisionId", page, StringComparison.Ordinal);
        Assert.Contains("SendPetBrainFeedbackAsync", client, StringComparison.Ordinal);

        // Klient gövdəsində bal, mükafat və ya doğruluq sahəsi YOXDUR.
        foreach (var forbidden in new[] { "TraitDelta", "ScoreDelta", "Reward =", "IsCorrect" })
            Assert.DoesNotContain(forbidden, page, StringComparison.Ordinal);
    }

    // ==================== Yol və yaddaş ====================

    /// <summary>
    /// Yol göstəricisi FAİZ vəd etmir və rəngdən ƏLAVƏ işarə/etiket daşıyır.
    /// </summary>
    [Fact]
    public void YolGostericisi_RengeTekBaglanmir()
    {
        var page = ReadPage("PetBrain.razor");

        Assert.Contains("pbx-path", page, StringComparison.Ordinal);
        Assert.Contains("step.Icon", page, StringComparison.Ordinal);
        Assert.Contains("step.Label", page, StringComparison.Ordinal);

        // Siyahının ekran oxuyucusu üçün adı var.
        Assert.Contains("Loc.T(\"Sənin yolun\", \"Your path\")", page, StringComparison.Ordinal);
    }

    /// <summary>«Mən bunu xatırlayıram» nişanı ekrandadır və ikidillidir.</summary>
    [Fact]
    public void YaddasNisani_EkrandaVarVeIkidillidir()
    {
        var page = ReadPage("PetBrain.razor");

        Assert.Contains("MemoryCallback", page, StringComparison.Ordinal);
        Assert.Contains(
            "Loc.T(\"Mən bunu xatırlayıram\", \"I remember this\")", page, StringComparison.Ordinal);
        Assert.Contains("aria-live", page, StringComparison.Ordinal);
    }

    // ==================== Bağ pilləsi ====================

    /// <summary>
    /// Bağ pilləsi ADI ilə göstərilir və ekran oxuyucusu üçün rəqəmdən artıq
    /// məlumat verilir.
    /// </summary>
    [Fact]
    public void BagPillesi_AdiVeEkranOxuyucuMetniIleGelir()
    {
        var page = ReadPage("PetBrain.razor");

        Assert.Contains("BondTierLabel", page, StringComparison.Ordinal);
        Assert.Contains("BondEmote", page, StringComparison.Ordinal);
        Assert.Contains("BondAriaLabel", page, StringComparison.Ordinal);

        // Bar hələ də var, amma TƏK daşıyıcı deyil.
        Assert.Contains("pbx-bond__fill", page, StringComparison.Ordinal);
    }

    // ==================== Təhlükəsizlik və əlçatanlıq ====================

    /// <summary>
    /// Server HAZIR HTML göndərmir — ekran <c>MarkupString</c> işlətmir.
    /// </summary>
    [Fact]
    public void Ekran_ServerHtmlRenderEtmir()
    {
        foreach (var source in new[] { ReadPage("PetBrain.razor"), ReadComponent("ExperienceScene") })
            Assert.DoesNotContain("MarkupString", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Yeni düymələr toxunuş hədəfi həddindən (44px) AŞAĞI düşmür.
    /// </summary>
    [Fact]
    public void YeniDuymeler_MinimumToxunusHeddineUygundur()
    {
        var css = ReadCss("app.css");
        var block = Block(css, ".pbx-card__alt");

        Assert.NotNull(block);
        Assert.Contains("min-height: 44px", block!, StringComparison.Ordinal);
    }

    /// <summary>Yeni səhnə hərəkət həssaslığı qaydasını pozmur.</summary>
    [Fact]
    public void AySehnesi_HereketHessasligiQaydasiniQoruyur()
    {
        var css = ReadCss("app.css");

        var reduced = css[css.IndexOf(".pbx-moon", StringComparison.Ordinal)..];

        Assert.Contains("prefers-reduced-motion", reduced, StringComparison.Ordinal);
        Assert.Contains(".pbx-moon__echo { animation: none; }", reduced, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ay səhnəsi ÖZ komponentindədir — ümumi fon artıq kifayət etmir.
    /// </summary>
    [Fact]
    public void AySehnesi_ModulyarSehneOlaraqCekilir()
    {
        var scene = ReadComponent("ExperienceScene");

        Assert.Contains("case \"moon\":", scene, StringComparison.Ordinal);
        Assert.Contains("pbx-moon__crystal", scene, StringComparison.Ordinal);

        // Variant SERVERDƏN gəlir — klient onu uydurmur.
        Assert.Contains("SceneVariant", scene, StringComparison.Ordinal);

        // Naməlum variant ümumi kadra düşür (fail closed).
        Assert.Contains("MoonVariants.Contains", scene, StringComparison.Ordinal);
    }

    /// <summary>
    /// Xəta halında ekran BOŞ qalmır: mesaj və yenidən cəhd yolu var.
    /// </summary>
    [Fact]
    public void XetaHalinda_EkranBosQalmir()
    {
        var page = ReadPage("PetBrain.razor");

        Assert.Contains("Error=\"@_error\"", page, StringComparison.Ordinal);
        Assert.Contains("OnRetry=\"LoadAsync\"", page, StringComparison.Ordinal);

        // Cavab uğursuz olanda mövcud kart yerində qalır.
        Assert.Contains("_error = result.Error;", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// Süni gecikmə YOXDUR: yükləmə mətni sorğu qədər görünür, taymer qoyulmur.
    /// </summary>
    [Fact]
    public void Ekran_SuniGecikmeQurmur()
    {
        var page = StripComments(ReadPage("PetBrain.razor"));

        // Səhnə yoxlaması istisnadır: o, arxa fon işini gözləyir, uşağı yox.
        var withoutScenePolling = page.Replace("await Task.Delay(delayMilliseconds, _sceneLifetime.Token);", string.Empty);

        Assert.DoesNotContain("Task.Delay", withoutScenePolling, StringComparison.Ordinal);
    }

    // ==================== Köməkçilər ====================

    /// <summary>
    /// Enum üzvlərini MƏNBƏDƏN oxuyur.
    ///
    /// <para>Sətir sonları normallaşdırılır: fayl Windows-da <c>CRLF</c> ilə
    /// yazılır və çoxsətirli <c>$</c> lövbəri <c>\r</c> ilə uyğunlaşmır.</para>
    /// </summary>
    private static List<string> EnumMembers(string name, [CallerFilePath] string path = "")
    {
        var source = Normalize(File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.Shared", "Enums", "PetBrainEnums.cs")));

        var start = source.IndexOf($"enum {name}", StringComparison.Ordinal);
        Assert.True(start > 0, $"{name} tapılmadı — test köhnəlib.");

        var end = source.IndexOf("\n}", start, StringComparison.Ordinal);
        var block = source[start..end];

        return [.. Regex.Matches(block, @"^\s{4}(\w+) = \d+,?$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)];
    }

    /// <summary>Bir CSS seçicisinin gövdəsi; tapılmasa <c>null</c>.</summary>
    private static string? Block(string css, string selector)
    {
        var start = css.IndexOf(selector + " {", StringComparison.Ordinal);

        if (start < 0)
            return null;

        var end = css.IndexOf('}', start);

        return end < 0 ? null : css[start..end];
    }

    private static string StripComments(string text)
    {
        text = Regex.Replace(text, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        text = Regex.Replace(text, @"^\s*///.*$", string.Empty, RegexOptions.Multiline);
        text = Regex.Replace(text, @"^\s*//.*$", string.Empty, RegexOptions.Multiline);

        return text;
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private static string ReadPage(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Pages", name));

    private static string ReadComponent(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui",
            "Components", "PetBrain", name + ".razor"));

    private static string ReadService(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Services", name));

    private static string ReadCss(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "wwwroot", "css", name));
}
