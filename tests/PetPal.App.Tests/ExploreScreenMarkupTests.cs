using System.Runtime.CompilerServices;

namespace PetPal.App.Tests;

/// <summary>
/// Kəşf ekranının iki qaydası. Hər ikisi kompilyasiya xətası vermir və yalnız
/// telefonda, şəkil seçiləndən SONRA görünür — ona görə mənbə səviyyəsində
/// qorunur.
/// </summary>
public class ExploreScreenMarkupTests
{
    /// <summary>
    /// Forma sürüşən qutunun İÇİNDƏ olmalıdır.
    ///
    /// <c>.app-main</c> qəsdən <c>overflow: hidden</c>-dir (app-dir, sayt deyil).
    /// Forma qutudan kənarda olanda şəkil önizləməsi onu ekrandan uzun edirdi:
    /// "Şəkil çək" və "Kəşfi qeyd et" düymələri aşağıda kəsilib toxunulmaz
    /// qalırdı, yəni şəkilli kəşfi saxlamaq mümkün olmurdu.
    /// </summary>
    [Fact]
    public void KesfFormasi_SurusenQutununIcindedir()
    {
        var markup = ReadExplore();

        var scroll = markup.IndexOf("<div class=\"list-scroll\">", StringComparison.Ordinal);
        var form = markup.IndexOf("<div class=\"card\">", StringComparison.Ordinal);
        var saveButton = markup.IndexOf("SubmitAsync", StringComparison.Ordinal);
        var scrollEnd = markup.IndexOf("@code {", StringComparison.Ordinal);

        Assert.True(scroll >= 0, "list-scroll qutusu yoxdur — forma kəsilə bilər.");
        Assert.True(form > scroll, "Forma sürüşən qutudan ƏVVƏLdir, yəni onun içində deyil.");
        Assert.True(saveButton > scroll && saveButton < scrollEnd,
            "Saxlama düyməsi sürüşən qutunun içində deyil.");
    }

    /// <summary>
    /// Önizləmənin ünvanı prefiksi KOR-KORANƏ əlavə etməməlidir.
    ///
    /// Brauzer seçicisi hazır <c>data:…</c> mətni qaytarır, MAUI isə təmiz
    /// base64. Prefiksi hər halda qoşmaq brauzerdə
    /// <c>data:image/jpeg;base64,data:image/png;base64,…</c> yaradırdı — şəkil
    /// heç vaxt görünmürdü, uşaq isə şəklin getmədiyini düşünürdü.
    /// </summary>
    [Fact]
    public void SekilOnizlemesi_IkiqatDataPrefiksiQoymur()
    {
        var markup = ReadExplore();

        Assert.DoesNotContain("src=\"data:image/jpeg;base64,@", markup, StringComparison.Ordinal);
        Assert.Contains("PhotoSource(", markup, StringComparison.Ordinal);
        Assert.Contains("StartsWith(\"data:\"", markup, StringComparison.Ordinal);
    }

    private static string ReadExplore([CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Pages", "Explore.razor"));
}
