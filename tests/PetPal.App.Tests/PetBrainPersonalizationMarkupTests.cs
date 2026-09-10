using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PetPal.App.Tests;

/// <summary>
/// Fərdiləşdirmə ekranlarının qaydaları: tanışlıq, alternativlər və valideyn
/// paneli.
///
/// <para>Bu testlər DAVRANIŞI deyil, VƏDİ qoruyur: ekran uşağa seçim
/// göstərdiyini iddia edirsə, həmin seçim həqiqətən orada olmalıdır.</para>
/// </summary>
public class PetBrainPersonalizationMarkupTests
{
    // ==================== Tövsiyə kartı ====================

    /// <summary>
    /// Alternativlər ekranda REAL kartlardır və hər biri ÖZ qərar id-si ilə
    /// başladılır.
    ///
    /// <para>Hamısını eyni id ilə başlatmaq ən asan səhvdir və uşaq üçün
    /// görünməz olardı: o, «Ayı seçdim» deyər, ekran isə Marsı açardı.</para>
    /// </summary>
    [Fact]
    public void Alternativler_OzQerariIleBasladilir()
    {
        var page = ReadPage("PetBrain.razor");

        Assert.Contains("_state.Alternatives", page, StringComparison.Ordinal);
        Assert.Contains("alt.SlotLabel", page, StringComparison.Ordinal);

        Assert.Contains("StartAlternativeAsync(alt)", page, StringComparison.Ordinal);
        Assert.Contains("alt.TemplateKey, alt.DecisionId", page, StringComparison.Ordinal);
    }

    /// <summary>Kart çətinlik, dəstək və mükafat etiketlərini göstərir.</summary>
    [Fact]
    public void Kart_Cetinlik_Destek_VeMukafatiGosterir()
    {
        var page = ReadPage("PetBrain.razor");

        Assert.Contains("recommendation.ChallengeLabel", page, StringComparison.Ordinal);
        Assert.Contains("recommendation.SupportLabel", page, StringComparison.Ordinal);
        Assert.Contains("recommendation.RewardLabel", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// Açıq rəy düymələri SERVERİN icazəsinə bağlıdır — fərdiləşdirmə
    /// söndürüləndə uşağa yalan vəd verilmir.
    /// </summary>
    [Fact]
    public void AcigReyDuymeleri_ServerinIcazesineBaglidir()
    {
        var page = ReadPage("PetBrain.razor");

        var guard = page.IndexOf("recommendation.CanGiveFeedback", StringComparison.Ordinal);
        var like = page.IndexOf("LikeAsync", StringComparison.Ordinal);
        var showLess = page.IndexOf("ShowLessAsync", StringComparison.Ordinal);

        Assert.True(guard > 0, "«Bəyənirəm» düyməsi şərtsizdir.");
        Assert.True(like > guard, "«Bəyənirəm» şərtin içində deyil.");
        Assert.True(showLess > guard, "«Daha az göstər» şərtin içində deyil.");
    }

    // ==================== Tanışlıq ====================

    /// <summary>
    /// <b>Keçmək hər addımda mümkündür.</b> Tanışlıq məcburi olsaydı, o,
    /// oyunun qapısına çevrilərdi.
    /// </summary>
    [Fact]
    public void Tanisliq_HerAddimdaKecile_Bilir()
    {
        var wizard = ReadComponent("OnboardingWizard");

        Assert.Contains("Skipped = true", wizard, StringComparison.Ordinal);
    }

    /// <summary>Uşaq pet-in səsini SEÇMƏZDƏN ƏVVƏL eşidir.</summary>
    [Fact]
    public void Tanisliq_TonunNumunesiniGosterir()
    {
        var wizard = ReadComponent("OnboardingWizard");

        Assert.Contains("SampleLine", wizard, StringComparison.Ordinal);
    }

    /// <summary>Tanışlıq ekranı iki dillidir və server mətnini render etmir.</summary>
    [Fact]
    public void Tanisliq_IkidillidirVeServerHtmlRenderEtmir()
    {
        var wizard = ReadComponent("OnboardingWizard");

        Assert.DoesNotContain("MarkupString", wizard, StringComparison.Ordinal);
        Assert.True(Regex.Matches(wizard, @"Loc\.T\(").Count >= 10,
            "Tanışlıq mətnləri ikidilli deyil.");
    }

    // ==================== Əlçatanlıq ====================

    /// <summary>
    /// Əlçatanlıq ayarları ekrana TƏTBİQ olunur — saxlanılıb unudulmur.
    /// </summary>
    [Fact]
    public void ElcatanliqAyarlari_EkranaTetbiqOlunur()
    {
        var page = ReadPage("PetBrain.razor");
        var css = ReadCss("app.css");

        foreach (var className in new[]
                 { "pbx--reduced-motion", "pbx--large-text", "pbx--high-contrast" })
        {
            Assert.Contains(className, page, StringComparison.Ordinal);
            Assert.Contains($".{className}", css, StringComparison.Ordinal);
        }
    }

    // ==================== Valideyn paneli ====================

    /// <summary>
    /// <b>Bal və İNAM ayrı göstərilir.</b> Panelin bütün mənası budur: «72»
    /// rəqəmi tək başına valideynə heç nə demir.
    /// </summary>
    [Fact]
    public void ValideynPaneli_BaliVeInamiAyriGosterir()
    {
        var panel = ReadComponent("ParentPersonalizationPanel");

        Assert.Contains("Confidence", panel, StringComparison.Ordinal);
        Assert.Contains("ObservationCount", panel, StringComparison.Ordinal);
        Assert.Contains("SourceCount", panel, StringComparison.Ordinal);
    }

    /// <summary>
    /// Hər ayarın yanında onu KİMİN yazdığı görünür — təxmin ilə seçim
    /// qarışmamalıdır.
    /// </summary>
    [Fact]
    public void ValideynPaneli_AyarinMenbeyiniGosterir()
    {
        var panel = ReadComponent("ParentPersonalizationPanel");

        Assert.Contains("SourceLabel", panel, StringComparison.Ordinal);

        foreach (var source in new[] { "Parent", "Child", "Onboarding", "Inferred" })
            Assert.Contains($"PetBrainSettingSource.{source}", panel, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Sıfırlama TƏSDİQ tələb edir</b> və nəyin QALDIĞINI açıq deyir.
    ///
    /// <para>Geri qaytarıla bilməyən əməliyyat bir toxunuşdan uzaq
    /// olmamalıdır; «nə silinir» qədər «nə silinmir» də yazılmalıdır.</para>
    /// </summary>
    [Fact]
    public void ValideynPaneli_SifirlamaTesdiqTeleb_Edir()
    {
        var panel = ReadComponent("ParentPersonalizationPanel");

        var confirm = panel.IndexOf("_confirmingReset = true", StringComparison.Ordinal);
        var reset = panel.IndexOf("ResetAsync", StringComparison.Ordinal);

        Assert.True(confirm > 0, "Sıfırlama təsdiqsizdir.");
        Assert.True(reset > confirm, "Sıfırlama təsdiqdən əvvəl çağırılır.");

        Assert.Contains("SİLİNMİR", panel, StringComparison.Ordinal);
        Assert.Contains("NOT deleted", panel, StringComparison.Ordinal);
    }

    /// <summary>Panel server HTML-i render etmir və ikidillidir.</summary>
    [Fact]
    public void ValideynPaneli_ServerHtmlRenderEtmirVeIkidillidir()
    {
        var panel = ReadComponent("ParentPersonalizationPanel");

        Assert.DoesNotContain("MarkupString", panel, StringComparison.Ordinal);
        Assert.True(Regex.Matches(panel, @"Loc\.T\(").Count >= 40,
            "Valideyn paneli ikidilli deyil.");
    }

    /// <summary>Panelin toxunulan elementləri toxunuş həddini ödəyir.</summary>
    [Fact]
    public void ValideynPaneli_ToxunusHeddiniOdeyir()
    {
        var css = ReadCss("app.css");

        foreach (var selector in new[] { ".ppp__toggle", ".ppp__select" })
        {
            var start = css.IndexOf(selector + " {", StringComparison.Ordinal);
            Assert.True(start > 0, $"«{selector}» qaydası yoxdur.");

            var body = css[start..css.IndexOf('}', start)];
            Assert.Contains("min-height: 44px", body, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Rəng TƏK daşıyıcı deyil: hər vəziyyətin mətn qarşılığı var.
    /// </summary>
    [Fact]
    public void ValideynPaneli_RengeTekBaglanmir()
    {
        var panel = ReadComponent("ParentPersonalizationPanel");

        Assert.Contains("Bloklanıb", panel, StringComparison.Ordinal);
        Assert.Contains("Açıqdır", panel, StringComparison.Ordinal);
        Assert.Contains("Blocked", panel, StringComparison.Ordinal);
        Assert.Contains("Allowed", panel, StringComparison.Ordinal);
    }

    /// <summary>Panel valideyn ekranına həqiqətən bağlanıb.</summary>
    [Fact]
    public void ValideynPaneli_DashboardaBaglanib()
    {
        var dashboard = ReadPage("ParentDashboard.razor");

        Assert.Contains("<ParentPersonalizationPanel", dashboard, StringComparison.Ordinal);
    }

    // ==================== Şərh qaydası ====================

    /// <summary>
    /// Yeni fayllar layihənin şərh qaydasına tabedir: yalnız <c>///</c> XML
    /// sənədi.
    /// </summary>
    [Theory]
    [InlineData("OnboardingWizard")]
    [InlineData("ParentPersonalizationPanel")]
    public void YeniKomponentler_SerhQaydasinaTabedir(string component)
    {
        var text = ReadComponent(component);

        Assert.DoesNotContain("@*", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/*", text, StringComparison.Ordinal);

        foreach (var line in text.Split('\n'))
            Assert.False(Regex.IsMatch(line, @"^\s*//([^/]|$)"),
                $"«{component}» sətir şərhi daşıyır: {line.Trim()}");
    }

    private static string ReadPage(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Pages", name));

    private static string ReadComponent(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui",
            "Components", "PetBrain", name + ".razor"));

    private static string ReadCss(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "wwwroot", "css", name));
}
