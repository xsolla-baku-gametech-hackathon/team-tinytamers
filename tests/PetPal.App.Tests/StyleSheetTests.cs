using System.Runtime.CompilerServices;

namespace PetPal.App.Tests;

/// <summary>
/// Stil fayllarının sintaksis qoruyucusu.
///
/// CSS pozuq şərhə görə xəta VERMİR — brauzer səssizcə bərpa olunur və
/// yalnız bir neçə qaydanı udur. Məhz belə bir səhv canlı app-də vardı:
/// `.tray` qaydasının üstündəki şərhin AÇILIŞ nişanı əskik idi, ona görə
/// brauzer şərhin mətnini selektor, `.tray` blokunu isə onun gövdəsi sayırdı.
/// Nəticə: `grid-template-columns` heç vaxt tətbiq olunmurdu və səkkiz yem
/// dörd sütun yerinə bir sütunda düzülürdü.
///
/// Nə build, nə də hər hansı test bunu tutmurdu — səhv yalnız ekranda
/// görünürdü. Bu fayl həmin boşluğu bağlayır.
/// </summary>
public class StyleSheetTests
{
    private static readonly string[] StyleSheets = ["app.css", "theme.css", "pet.css"];

    [Fact]
    public void SerhlerBalansdadir()
    {
        var problems = new List<string>();

        foreach (var name in StyleSheets)
        {
            var css = ReadCss(name);
            var inComment = false;
            var openedAt = 0;
            var line = 1;

            for (var i = 0; i < css.Length; i++)
            {
                if (css[i] == '\n')
                {
                    line++;
                    continue;
                }

                if (i + 1 >= css.Length)
                    continue;

                if (!inComment && css[i] == '/' && css[i + 1] == '*')
                {
                    inComment = true;
                    openedAt = line;
                    i++;
                }
                else if (inComment && css[i] == '*' && css[i + 1] == '/')
                {
                    inComment = false;
                    i++;
                }
                else if (!inComment && css[i] == '*' && css[i + 1] == '/')
                {
                    // Məhz `.tray` səhvi: bağlanan nişan var, açılan yoxdur.
                    problems.Add($"{name}:{line} — şərh açılmadan bağlanır (*/). Yuxarıdakı mətnin başında /* çatışmır.");
                    i++;
                }
            }

            if (inComment)
                problems.Add($"{name}:{openedAt} — şərh açılıb, amma bağlanmayıb. Faylın qalanı udulur.");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Səkkiz yem ÜFÜQİ şəbəkədə düzülməlidir. Qayda ayrıca yoxlanılır, çünki
    /// pozulanda app işləməyə davam edir — sadəcə yem siyahısı ekranın solunda
    /// dar bir sütuna yığılır və otağın qalanı boş görünür.
    /// </summary>
    [Fact]
    public void YemQabi_CoxSutunludur()
    {
        var css = ReadCss("app.css");

        var rule = css.IndexOf(".tray {", StringComparison.Ordinal);
        Assert.True(rule >= 0, ".tray qaydası tapılmadı — test köhnəlib.");

        var body = css[rule..css.IndexOf('}', rule)];
        Assert.Contains("grid-template-columns", body, StringComparison.Ordinal);
        Assert.Contains("repeat(4", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Inline SVG-nin hər iki ölçüsü <c>auto</c> ola BİLMƏZ.
    ///
    /// <para>Pet-in SVG-sində yalnız <c>viewBox</c> var, en/hündürlük atributu
    /// yoxdur. Hər ikisi CSS-də <c>auto</c> olanda daxili ölçü qalmır və element
    /// 0×0-a yığılır — heç bir xəta olmadan. Məhz bu, söhbət otağında baş verdi:
    /// <c>.chatroom__pet :is(svg, img)</c> qaydası <c>width: auto</c> verirdi və
    /// spesifikliyinə görə <c>.pet</c>-in enini üstələyirdi. Nəticədə pet ekranda
    /// ümumiyyətlə görünmürdü, ana ekranda isə hər şey qaydasında idi.</para>
    ///
    /// <para>Bu test həmin tələni bağlayır: <c>width: auto</c> yazan hər SVG
    /// qaydası dəqiq hündürlük verməlidir (və ya əksinə).</para>
    /// </summary>
    [Fact]
    public void SvgQaydalari_HerIkiOlcunuAutoQoymur()
    {
        var css = ReadCss("app.css");
        var problems = new List<string>();

        foreach (var (selector, body) in Rules(css))
        {
            if (!selector.Contains("svg", StringComparison.OrdinalIgnoreCase))
                continue;

            var width = Declaration(body, "width");
            var height = Declaration(body, "height");

            // Yalnız enin `auto` olması özlüyündə problem deyil — problem
            // hündürlüyün də dəqiq olmamasıdır.
            if (width != "auto")
                continue;

            if (string.IsNullOrEmpty(height) || height == "auto")
                problems.Add($"{selector.Trim()} — width:auto verilib, dəqiq height yoxdur: SVG 0×0 olur.");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>Sadə selektor/gövdə ayırıcısı — media və keyframe blokları buraya düşmür.</summary>
    private static IEnumerable<(string Selector, string Body)> Rules(string css)
    {
        var withoutComments = System.Text.RegularExpressions.Regex.Replace(css, @"/\*.*?\*/", string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match match in
                 System.Text.RegularExpressions.Regex.Matches(withoutComments, @"([^{}]+)\{([^{}]*)\}"))
        {
            var selector = match.Groups[1].Value;

            if (selector.Contains('@'))
                continue;

            yield return (selector, match.Groups[2].Value);
        }
    }

    /// <summary>Gövdədən bir xassənin dəyərini oxuyur; yazılmayıbsa boş sətir.</summary>
    private static string Declaration(string body, string property)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            body, $@"(?:^|;)\s*{property}\s*:\s*([^;]+)",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string ReadCss(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "wwwroot", "css", name));
}
