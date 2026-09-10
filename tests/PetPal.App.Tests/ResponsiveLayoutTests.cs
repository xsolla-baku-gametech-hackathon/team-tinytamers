using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PetPal.App.Tests;

/// <summary>
/// Kətan qaydasının qoruyucusu.
///
/// <para>App-in bütün ekranları 390×690 piksellik kətan üçün çəkilib və
/// ölçülər PİKSELDİR. Ekrana uyğunlaşma bir yerdə baş verir: <c>.app-shell</c>
/// bütün qatı <c>zoom: var(--pp-zoom)</c> ilə miqyaslayır. Bu üsul yalnız o
/// halda işləyir ki, kətanın İÇİNDƏ heç kim birbaşa ekran ölçüsünə (vw/vh)
/// müraciət etməsin və heç nə pəncərəyə yapışmasın (position: fixed) —
/// əks halda həmin element miqyasdan qaçır və qonşularının üstünə düşür.</para>
///
/// <para>Bu qaydalar pozulanda app işləməyə davam edir: səhv yalnız başqa
/// ölçülü telefonda, ya da kompüterdə görünür. Ona görə mənbə səviyyəsində
/// qorunur.</para>
/// </summary>
public class ResponsiveLayoutTests
{
    /// <summary>
    /// Kətanın ölçüləri zoom-a BÖLÜNMƏLİDİR: `height: 100dvh` yazsaq, ekran
    /// 0.82 miqyasda cihazın yalnız 82%-ni tutar və altda boz zolaq qalar.
    /// </summary>
    [Fact]
    public void AppShell_KetaniEkranaSigisdirir()
    {
        var body = RuleBody(ReadCss("app.css"), ".app-shell {");

        Assert.Contains("zoom: var(--pp-zoom", body, StringComparison.Ordinal);

        foreach (var property in new[] { "width", "height" })
        {
            var value = Declaration(body, property);
            Assert.False(string.IsNullOrEmpty(value), $".app-shell — {property} yazılmayıb.");
            Assert.Contains("var(--pp-zoom", value, StringComparison.Ordinal);
        }

        // Notch və jest zolağı ekran pikselidir: miqyaslansa, məzmun onların
        // altına girər. Ona görə kətanın içində geri bölünür.
        Assert.Contains("--pp-safe-top: calc(env(safe-area-inset-top, 0px) / var(--pp-zoom", body, StringComparison.Ordinal);
        Assert.Contains("--pp-safe-bottom: calc(env(safe-area-inset-bottom, 0px) / var(--pp-zoom", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// JS yüklənməmişdən əvvəl də (Blazor WASM bir neçə saniyə çəkir) dar
    /// ekranda məzmun daşmamalıdır — media query-lər həmin boşluğu doldurur.
    /// </summary>
    [Fact]
    public void Tema_JsSizDeMiqyasVerir()
    {
        var css = ReadCss("theme.css");

        Assert.Contains("--pp-zoom: min(var(--pp-zoom-w), var(--pp-zoom-h))", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 319px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-height: 567px)", css, StringComparison.Ordinal);
    }

    /// <summary>Hər iki host kətan skriptini yükləməlidir — yoxsa miqyas pilləli qalır.</summary>
    [Theory]
    [InlineData("PetPal.App")]
    [InlineData("PetPal.Web")]
    public void HerIkiHost_KetanSkriptiniYukleyir(string host)
    {
        var html = ReadHostPage(host);

        Assert.Contains("_content/PetPal.App.Ui/js/appFrame.js", html, StringComparison.Ordinal);

        // Skript Blazor-dan ƏVVƏL gəlməlidir: açılış ekranı da doğru ölçüdə olsun.
        var frame = html.IndexOf("appFrame.js", StringComparison.Ordinal);
        var blazor = html.IndexOf("blazor.web", StringComparison.Ordinal);
        Assert.True(frame < blazor, "appFrame.js Blazor yükləyicisindən sonra gəlir.");
    }

    /// <summary>
    /// Kətanın içində <c>position: fixed</c> pəncərəyə yapışır, yəni miqyasdan
    /// və kompüterdəki telefon çərçivəsindən qaçır. Söhbət otağı məhz belə
    /// bütün masaüstü pəncərəsini örtürdü.
    ///
    /// Yeganə istisna sürüklənən kölgələrdir (<c>.care-ghost</c>,
    /// <c>.drag-ghost</c>): onlar qəsdən <c>body</c>-yə əlavə olunur və
    /// barmağın EKRAN koordinatı ilə hərəkət edir. Əvəzində miqyası özləri
    /// vurmalıdırlar — bu da aşağıda ayrıca yoxlanılır.
    /// </summary>
    [Fact]
    public void Ketanin_IcindeFixedElementYoxdur()
    {
        var css = StripComments(ReadCss("app.css"));
        var problems = new List<string>();

        foreach (var (selector, body) in Rules(css))
        {
            if (Declaration(body, "position") != "fixed")
                continue;

            if (selector.Contains("-ghost", StringComparison.Ordinal))
                continue;

            problems.Add($"{selector.Trim()} — position: fixed kətandan qaçır; absolute işlədin.");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Sürüklənən kölgə kətandan kənardadır, yəni <c>.app-shell</c>-in
    /// zoom-unu MİRAS ALMIR. Miqyası özü vurmasa, kölgə dar ekranda
    /// səhnədəki əşyadan böyük görünür — barmağın altındakı çiyələk
    /// boşqabdakından iri olur.
    /// </summary>
    [Fact]
    public void SuruklenenKolge_MiqyasiOzuVurur()
    {
        var css = StripComments(ReadCss("app.css"));
        var problems = new List<string>();

        foreach (var (selector, body) in Rules(css))
        {
            if (!selector.Contains("-ghost", StringComparison.Ordinal))
                continue;

            var transform = Declaration(body, "transform");

            // Yalnız miqyas verən qaydalar yoxlanılır — sırf yerini dəyişən
            // (translate) və ya klonu sıfırlayan köməkçi qaydalar azaddır.
            if (!transform.Contains("scale(", StringComparison.Ordinal))
                continue;

            if (!transform.Contains("var(--pp-zoom", StringComparison.Ordinal))
                problems.Add($"{selector.Trim()} — scale var(--pp-zoom) ilə vurulmayıb.");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Kətanın içində ekran vahidi (vw/vh/dvh) ölçünü İKİ dəfə kiçildir: bir
    /// dəfə vahidin özü, bir dəfə də zoom. İcazə yalnız zoom-a bölünmüş
    /// hallara və kətanı ekrana oturdan qaydalara verilir.
    /// </summary>
    [Fact]
    public void Ketanin_IcindeEkranVahidiYoxdur()
    {
        var problems = new List<string>();

        foreach (var name in new[] { "app.css", "pet.css" })
        {
            var lines = StripComments(ReadCss(name)).Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (!Regex.IsMatch(line, @"\d\s*(vw|vh|dvh|svh|lvh)\b"))
                    continue;

                // Kətanı ekrana oturdan qaydalar — burada ekran vahidi doğrudur.
                if (line.Contains("var(--pp-zoom", StringComparison.Ordinal))
                    continue;

                if (name == "app.css" && IsShellHost(lines, i))
                    continue;

                problems.Add($"{name}:{i + 1} — {line.Trim()}");
            }
        }

        Assert.True(problems.Count == 0,
            "Kətanın içində ekran vahidi işlənib (zoom ölçünü ikinci dəfə kiçildəcək):"
            + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    /// <summary>Kətanı saxlayan `#app` qutusu — o, kətandan kənardadır.</summary>
    private static bool IsShellHost(string[] lines, int index)
    {
        for (var i = index; i >= 0 && index - i < 6; i--)
        {
            if (lines[i].TrimStart().StartsWith("#app", StringComparison.Ordinal))
                return true;

            if (lines[i].Contains('}'))
                return false;
        }

        return false;
    }

    /// <summary>
    /// Ev ekranı kətanın HÜNDÜRLÜYÜNƏ sığmalıdır.
    ///
    /// <para><c>.app-main</c>-də <c>overflow: hidden</c> var: sığmayan hissə
    /// sürüşmür, KƏSİLİR. Naviqasiya isə mütləq mövqelidir, yəni kəsilən
    /// kafellər onun ALTINDA qalır və basılmaz olur.</para>
    ///
    /// <para>2026-09-08-də məhz belə oldu: <c>.scene--hero</c> 540px istəyirdi,
    /// büdcə isə 475-dir — kompüterdə kafellər naviqasiya ilə üst-üstə düşdü.
    /// Telefonda görünmürdü, çünki orada miqyası ENİN nisbəti təyin edir və
    /// kətan təsadüfən hündür olur; kompüterdə hədd hündürlükdür və kətan düz
    /// DESIGN_H-də dayanır.</para>
    /// </summary>
    [Fact]
    public void EvSehnesi_KetanBudcesindenBoyukDeyil()
    {
        var css = StripComments(ReadCss("app.css"));

        var designHeight = DesignHeight();
        var gutter = Px(RuleBody(StripComments(ReadCss("theme.css")), ":root {"), "--pp-gutter");
        var navReserve = NavReserve(css);
        var actionHeight = Px(RuleBody(css, ".action {"), "min-height");
        var actionMargin = Px(RuleBody(css, ".actions {"), "margin-top");

        var budget = designHeight - (gutter + navReserve) - (actionHeight + actionMargin);
        var sceneMin = Px(RuleBody(css, ".scene--hero {"), "min-height");

        Assert.True(sceneMin <= budget,
            $".scene--hero min-height {sceneMin}px, kətan büdcəsi isə {budget}px "
            + $"(kətan {designHeight} − naviqasiya {gutter + navReserve} − kafellər "
            + $"{actionHeight + actionMargin}). Fərq kəsilir və kafellər naviqasiyanın "
            + "altında qalır.");
    }

    /// <summary>
    /// Danışıq balonu pet-in ÜSTÜNDƏ boyanmalıdır. İkisi də z-index 4-də
    /// olanda DOM sırası qərar verirdi və pet sonra gəldiyi üçün balonun
    /// mətnini örtürdü — cümlə yarımçıq oxunurdu.
    /// </summary>
    [Fact]
    public void DanisiqBalonu_PetinUstundeQalir()
    {
        var css = StripComments(ReadCss("app.css"));

        var bubble = Px(RuleBody(css, ".scene--hero .bubble {"), "z-index");
        var pet = Px(RuleBody(css, ".scene--hero .scene__pet {"), "z-index");

        Assert.True(bubble > pet,
            $"Balon z-index {bubble}, pet {pet} — pet balonun mətnini örtəcək.");
    }

    /// <summary>
    /// Üzən çip (sprint / duel nəticəsi) mərhələ nişanı ilə eyni zolağa düşür:
    /// nişan mərkəzdə, çip sağda, ikisi də enlidir. Ona görə çip görünəndə pet
    /// sütunu ona yer ayırmalıdır — sinfi Home.razor qoyur.
    /// </summary>
    [Fact]
    public void UzenCip_MerheleNisaninaYerAcir()
    {
        var css = StripComments(ReadCss("app.css"));
        var home = ReadPage("Home.razor");

        var reserve = Px(RuleBody(css, ".scene--hero--chip .scene__pet {"), "padding-bottom");
        Assert.True(reserve >= 58,
            $"Ayrılan zolaq {reserve}px — 25 ölçüdə süpürüldü, 50-də hələ kəsişir, 58-dən təmizdir.");

        Assert.Contains("scene--hero--chip", home, StringComparison.Ordinal);
        Assert.Contains("HasFloatingChip", home, StringComparison.Ordinal);
    }

    /// <summary>
    /// Miqyasın alt həddindən (appFrame.js → MIN) sonra kətan artıq 690-a
    /// çatmır. Orada məzmun kəsilməməlidir: sahə sürüşür və naviqasiya üzməyi
    /// dayandırıb öz sırasını tutur.
    /// </summary>
    [Fact]
    public void AlcaqPencerede_MezmunKesilmir()
    {
        var css = ReadCss("app.css");

        var block = css[css.IndexOf("@media (max-height: 334px)", StringComparison.Ordinal)..];
        Assert.Contains("overflow-y: auto", block[..800], StringComparison.Ordinal);

        // Zolağın qaydası .bottomnav-DAN SONRA gəlməlidir: eyni dəyərlidir,
        // sonuncu qazanır. Əvvəl yazılanda sadəcə udulurdu.
        var navRule = css.IndexOf(".bottomnav {", StringComparison.Ordinal);
        var staticRule = css.IndexOf("position: static", StringComparison.Ordinal);
        Assert.True(staticRule > navRule,
            "Alçaq pəncərə qaydası .bottomnav-dan əvvəl gəlir — udulacaq.");
    }

    /// <summary>appFrame.js-in kətan hündürlüyü — büdcənin başlanğıc rəqəmi.</summary>
    private static int DesignHeight()
    {
        var js = ReadJs("appFrame.js");
        var match = Regex.Match(js, @"DESIGN_H\s*=\s*(\d+)");
        Assert.True(match.Success, "appFrame.js — DESIGN_H tapılmadı.");
        return int.Parse(match.Groups[1].Value);
    }

    /// <summary>`.app-main`-in naviqasiya üçün ayırdığı piksel.</summary>
    private static int NavReserve(string css)
    {
        var body = RuleBody(css, ".app-main {");
        var match = Regex.Match(Declaration(body, "padding-bottom"), @"(\d+)px");
        Assert.True(match.Success, ".app-main — padding-bottom oxunmadı.");
        return int.Parse(match.Groups[1].Value);
    }

    private static int Px(string body, string property)
    {
        var match = Regex.Match(Declaration(body, property), @"-?\d+");
        Assert.True(match.Success, $"{property} rəqəm kimi oxunmadı.");
        return int.Parse(match.Value);
    }

    /// <summary>
    /// Qaydanın gövdəsini gətirir. Axtarış SƏTİR BAŞINA bağlıdır: sadə
    /// <c>IndexOf</c> ilə <c>.scene--hero {</c> özündən əvvəl gələn
    /// <c>.app-main--immersive .scene--hero {</c> qaydasına düşürdü və səhv
    /// gövdə oxunurdu.
    /// </summary>
    private static string RuleBody(string css, string opener)
    {
        var start = css.IndexOf('\n' + opener, StringComparison.Ordinal);
        var offset = 1;

        if (start < 0 && css.StartsWith(opener, StringComparison.Ordinal))
            (start, offset) = (0, 0);

        Assert.True(start >= 0, $"{opener} qaydası tapılmadı — test köhnəlib.");

        var open = start + offset + opener.Length;
        return css[open..css.IndexOf('}', open)];
    }

    private static IEnumerable<(string Selector, string Body)> Rules(string css)
    {
        foreach (Match match in Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}"))
        {
            var selector = match.Groups[1].Value;

            if (selector.Contains('@'))
                continue;

            yield return (selector, match.Groups[2].Value);
        }
    }

    private static string Declaration(string body, string property)
    {
        var match = Regex.Match(body, $@"(?:^|;)\s*{property}\s*:\s*([^;]+)", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string StripComments(string css) =>
        Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    private static string ReadCss(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "wwwroot", "css", name));

    private static string ReadJs(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "wwwroot", "js", name));

    private static string ReadPage(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Pages", name));

    private static string ReadHostPage(string project, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", project, "wwwroot", "index.html"));
}
