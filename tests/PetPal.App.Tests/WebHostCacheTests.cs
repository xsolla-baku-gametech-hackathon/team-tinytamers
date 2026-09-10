using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PetPal.App.Tests;

/// <summary>
/// Brauzer versiyasının cache qaydası.
///
/// Qayda bir cümlədir: <b>uzun cache yalnız adı məzmunla birlikdə dəyişən fayla
/// verilir</b>. Adı sabit qalan fayl (Blazor yükləyiciləri, dizayn CSS-i, JS)
/// hər deploy-da yenidən soruşulmalıdır.
///
/// Bu, iki dəfə real problemə çevrilib və hər iki dəfə də gec görünüb, çünki
/// yalnız SAYTI ƏVVƏL AÇMIŞ cihazda baş verir: developer-in öz brauzeri təzə
/// deploy-u görür, istifadəçinin telefonu isə köhnə faylda ilişib qalır —
/// ya sayt açılmır, ya da yeni ekranlar köhnə CSS ilə çəkilir.
///
/// Deploy-dan sonra eyni şeyi <c>scripts/railway/smoke-test.ps1</c> canlı
/// domendə yoxlayır; bu test isə səhvi hələ commit mərhələsində tutur.
/// </summary>
public class WebHostCacheTests
{
    /// <summary>Adında hash olmayan, yəni hər deploy-da eyni ünvanda qalan fayllar.</summary>
    private static readonly string[] UnfingerprintedEntryPoints =
    [
        "blazor.webassembly.js",   // index.html birbaşa bunu yükləyir
        "dotnet.js",               // hash-lı faylların xəritəsi buradadır
        "css",                     // app.css / theme.css / pet.css
        "js"                       // petCare.js, photoPicker.js
    ];

    [Fact]
    public void HashDasimayanGirisFayllari_UzunCacheAlmir()
    {
        var config = ReadNginxTemplate();

        foreach (var block in CacheBlocks(config))
        {
            if (!UnfingerprintedEntryPoints.Any(entry => block.Location.Contains(entry, StringComparison.Ordinal)))
                continue;

            Assert.False(block.Cache.Contains("immutable", StringComparison.Ordinal),
                $"{block.Location} — hash daşımayan fayl 'immutable' ilə verilir: {block.Cache}");

            Assert.DoesNotMatch(@"max-age=[1-9]", block.Cache);
        }
    }

    /// <summary>
    /// Əks tərəf də qorunmalıdır: problemi "hər şeyi no-cache et" ilə həll etmək
    /// meqabaytlarla .wasm faylını hər açılışda yenidən yoxlamağa məcbur edərdi.
    /// Hash daşıyan fayllar uzun cache-də qalmalıdır.
    /// </summary>
    [Fact]
    public void HashDasiyanFayllar_UzunCachedeQalir()
    {
        var config = ReadNginxTemplate();

        var framework = CacheBlocks(config).FirstOrDefault(b => b.Location == "location /_framework/");

        Assert.True(framework is not null, "_framework/ bloku tapılmadı — test köhnəlib.");
        Assert.Contains("immutable", framework!.Cache, StringComparison.Ordinal);
    }

    /// <summary>Start zamanı yenidən yazılan fayllar heç saxlanılmamalıdır.</summary>
    [Theory]
    [InlineData("location = /index.html")]
    [InlineData("location = /appsettings.json")]
    public void DeploydanDeployaDeyisenFayllar_SaxlanilmirCache(string location)
    {
        var block = CacheBlocks(ReadNginxTemplate()).FirstOrDefault(b => b.Location == location);

        Assert.True(block is not null, $"{location} bloku tapılmadı — test köhnəlib.");
        Assert.Contains("no-store", block!.Cache, StringComparison.Ordinal);
    }

    /// <summary>
    /// nginx qaydası yalnız BUNDAN SONRAKI deploy-ları düzəldir: "immutable"
    /// almış brauzer serverə bir daha müraciət etmir, ona görə yeni başlıq
    /// həmin cihaza çatmır. Çıxış yolu ünvanı dəyişməkdir — <c>?v=</c> damğası.
    ///
    /// Damğa iki yerdə olmalıdır: skript teqində (yükləyicinin özü üçün) və
    /// <c>loadBootResource</c>-da (yükləyicinin çəkdiyi dotnet.js üçün).
    /// Biri olmasa, cihaz yarımçıq təzələnir və app yenə açılmır.
    /// </summary>
    [Fact]
    public void GirisFayllari_CachedenCixisDamgasiDasiyir()
    {
        var html = ReadIndexHtml();

        var script = Regex.Match(html, @"<script src=""_framework/blazor\.webassembly\.js\?v=(?<stamp>[^""]+)""");
        Assert.True(script.Success, "blazor.webassembly.js ?v= damğası olmadan yüklənir.");

        Assert.Contains("loadBootResource", html, StringComparison.Ordinal);
        Assert.Contains($"'?v={script.Groups["stamp"].Value}'", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Damğa YALNIZ yükləyicilərə vurulanda yarımçıq düzəliş alınır: sayt açılır,
    /// amma köhnə CSS ilə çəkilir və ekran dağılır. Bu, real olaraq baş verib.
    /// Ona görə index.html-dən çağırılan hər hash-sız fayl damğa daşımalıdır.
    /// </summary>
    [Fact]
    public void IndexdekiButunHashsizFayllar_EyniDamganiDasiyir()
    {
        var html = ReadIndexHtml();

        var stamp = Regex.Match(html, @"blazor\.webassembly\.js\?v=(?<stamp>[^""]+)").Groups["stamp"].Value;
        Assert.False(string.IsNullOrEmpty(stamp), "Damğa tapılmadı — test köhnəlib.");

        // href/src ilə çağırılan yerli .css/.js faylları (kənar ünvanlar sayılmır).
        var references = Regex.Matches(html, @"(?:href|src)=""(?<url>[^""]*\.(?:css|js)(?:\?[^""]*)?)""")
            .Select(m => m.Groups["url"].Value)
            .Where(url => !url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                       && !url.StartsWith("data:", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(references);

        foreach (var url in references)
            Assert.EndsWith($"?v={stamp}", url, StringComparison.Ordinal);
    }

    private sealed record CacheBlock(string Location, string Cache);

    /// <summary>
    /// Şablondan <c>location … { … Cache-Control … }</c> cütlərini çıxarır.
    /// Cache-Control yazmayan bloklar (health, SPA marşrutu) nəzərə alınmır.
    /// </summary>
    private static IEnumerable<CacheBlock> CacheBlocks(string config)
    {
        var matches = Regex.Matches(
            config,
            @"(?<location>location[^\{]*)\{(?<body>[^\}]*)\}",
            RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var cache = Regex.Match(match.Groups["body"].Value, @"Cache-Control\s+""(?<value>[^""]*)""");

            if (cache.Success)
                yield return new CacheBlock(match.Groups["location"].Value.Trim(), cache.Groups["value"].Value);
        }
    }

    private static string ReadIndexHtml([CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.Web", "wwwroot", "index.html"));

    private static string ReadNginxTemplate([CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.Web", "nginx.conf.template"));
}
