using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PetPal.App.Tests;

/// <summary>
/// Otaq ekranlarının dörd qaydası kompilyasiya xətası vermir, amma pozulanda
/// oyun sınır. Hər üçü işləyən app-də görünür, ona görə mənbə səviyyəsində
/// qorunur.
/// </summary>
public class CareScreenMarkupTests
{
    /// <summary>
    /// Otaq açıq olanda səhnə render olunmamalıdır: pet-in SVG-si
    /// <c>data-pet-target</c> daşıyır və JS sürükləməni sənədin BİRİNCİ belə
    /// elementinə bağlayır. Səhnə arxada qalsa, sabun və su görünməyən pet-in
    /// üstünə düşür — köpük heç vaxt görünmür.
    /// </summary>
    [Fact]
    public void OtaqAcıqOlanda_SehneRenderOlunmur()
    {
        var markup = ReadCare();

        var guard = markup.IndexOf("@if (!IsRoomOpen)", StringComparison.Ordinal);
        var stage = markup.IndexOf("<section class=\"stage\"", StringComparison.Ordinal);

        Assert.True(guard >= 0, "IsRoomOpen şərti yoxdur — səhnə otaqla birlikdə render olunur.");
        Assert.True(stage > guard, "Səhnə şərtin içində deyil.");
        Assert.True(stage - guard < 400, "Səhnə şərtdən çox uzaqdır — şərt başqa bloka aiddir.");
    }

    /// <summary>
    /// Otaq qutuları 390x620 üçün çəkilib və ekrana JS ilə oturdulur. Qutuda
    /// <c>data-room</c> yoxdursa miqyas tətbiq olunmur: otaq geniş ekranın
    /// ortasında kiçik şəkil kimi qalır.
    /// </summary>
    [Fact]
    public void ButunOtaqQutulari_DataRoomDasiyir()
    {
        var markup = ReadCare();

        foreach (var room in new[] { "bathroom__room", "bedroom__room", "closet__room" })
        {
            var match = Regex.Match(markup, $"<div class=\"{room}\"[^>]*>");

            Assert.True(match.Success, $"{room} tapılmadı — test köhnəlib.");
            Assert.Contains("data-room", match.Value, StringComparison.Ordinal);
        }
    }

    /// <summary>Miqyas hər renderdən sonra yenilənməlidir, yoxsa rejim dəyişəndə köhnə qalır.</summary>
    [Fact]
    public void HerRenderdenSonra_FitRoomsCagirilir()
    {
        var markup = ReadCare();

        Assert.Contains("InvokeVoidAsync(\"fitRooms\")", markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Hamamda alət ayrıca ikona deyil, otağın öz əşyasıdır: sabun və duş
    /// düymələri <c>data-drag-id</c> daşıyır və JS sürükləmə kölgəsini məhz o
    /// id ilə tapıb klonlayır. Id-lər <c>BathToolDragId</c>-dəkindən fərqlənsə,
    /// heç bir xəta olmur — barmağın altında əşya əvəzinə emoji görünür.
    /// </summary>
    [Fact]
    public void HamamEsyalari_SuruklemeIdiIleTapilir()
    {
        var markup = ReadCare();

        foreach (var id in new[] { "care-soap", "care-shower" })
        {
            Assert.Contains($"data-drag-id=\"{id}\"", markup, StringComparison.Ordinal);
            Assert.Contains($"\"{id}\"", markup[markup.IndexOf("BathToolDragId =>", StringComparison.Ordinal)..],
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Dörd qulluq işinin dördü də AYRICA TAM EKRAN otaqdır. Yem vermək uzun
    /// müddət istisna idi: pet yuxarıdakı balaca səhnədə qalır, yemlər isə
    /// aşağıdakı kartda dururdu — uşaq "yemi ağzına apar" göstərişini görür,
    /// amma ağzı görmürdü.
    /// </summary>
    [Fact]
    public void YemekOtagi_DigerOtaqlarlaEyniSistemdedir()
    {
        var markup = ReadCare();

        Assert.Contains("class=\"kitchen\"", markup, StringComparison.Ordinal);

        // Miqyas JS-dən gəlir; `data-room` olmadan otaq ekrana oturdulmur və
        // 390x620 illüstrasiya geniş ekranın ortasında kiçik qalır.
        var room = Regex.Match(markup, "<div class=\"kitchen__room\"[^>]*>");
        Assert.True(room.Success, "kitchen__room tapılmadı.");
        Assert.Contains("data-room", room.Value, StringComparison.Ordinal);

        // Yemlər otağın öz səthindədir, kartda deyil.
        Assert.Contains("tray--counter", markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Otaq açıq olanda səhnə render olunmamalıdır — yem rejimi də daxil.
    /// Səbəb <see cref="OtaqAcıqOlanda_SehneRenderOlunmur"/> ilə eynidir: JS
    /// sürükləməni sənədin BİRİNCİ <c>data-pet-target</c> elementinə bağlayır,
    /// yəni səhnə arxada qalsa, yem otaqdakı pet-in yox, görünməyən pet-in
    /// ağzına aparılır və heç vaxt "ağız" zonasına düşmür.
    /// </summary>
    [Fact]
    public void YemRejimi_SehneniBaglayir()
    {
        var markup = ReadCare();

        var guard = Regex.Match(markup, @"IsRoomOpen =>[^;]*;");

        Assert.True(guard.Success, "IsRoomOpen tapılmadı — test köhnəlib.");
        Assert.Contains("CareMode.Feed", guard.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Yorğan pet-in ÖNÜNDƏ olmalıdır, yəni dekorun İÇİNDƏ yox.
    /// <c>.bedroom__decor</c> öz yığın kontekstindədir (<c>isolation: isolate</c>)
    /// — oraya qoyulan yorğan heç bir z-index ilə pet-in üstünə çıxa bilmir və
    /// pet yorğanın üstündə oturmuş kimi görünür.
    /// </summary>
    [Fact]
    public void Yorgan_DekorunIcindeDeyil()
    {
        var markup = ReadCare();

        var decorStart = markup.IndexOf("class=\"bedroom__decor\"", StringComparison.Ordinal);
        var petStart = markup.IndexOf("class=\"bedroom__pet ", StringComparison.Ordinal);
        var quiltStart = markup.IndexOf("class=\"bedroom__quilt ", StringComparison.Ordinal);

        Assert.True(decorStart >= 0 && petStart >= 0, "Yataq otağının qatları tapılmadı — test köhnəlib.");
        Assert.True(quiltStart >= 0, "Yorğan qatı yoxdur — pet yatağın üstündə oturmuş kimi görünür.");
        Assert.True(quiltStart > petStart, "Yorğan pet-dən ƏVVƏLdir, yəni onun arxasında qalır.");
    }

    /// <summary>
    /// Yemək otağı yem veriləndən sonra ÖZÜ bağlanmır. Əvvəl <c>OnDrop</c>-un
    /// sonunda <c>SetModeAsync(CareMode.None)</c> vardı: uşaq yemi ağıza aparan
    /// kimi otaqdan eşiyə atılırdı — pet-in cavabını görmürdü və ikinci porsiya
    /// üçün otağı yenidən açmalı olurdu. Otaqdan çıxmaq qərarı uşağındır, ✕
    /// düyməsinin işidir.
    /// </summary>
    [Fact]
    public void YemVerildikden_SonraOtaqOzuBaglanmir()
    {
        var markup = ReadCare();

        var start = markup.IndexOf("public async Task OnDrop(", StringComparison.Ordinal);
        Assert.True(start >= 0, "OnDrop tapılmadı — test köhnəlib.");

        var end = markup.IndexOf("private async Task RunCareAsync", start, StringComparison.Ordinal);
        Assert.True(end > start, "OnDrop-un sonu tapılmadı — test köhnəlib.");

        var onDrop = markup[start..end];

        Assert.Contains("RunCareAsync(CareAction.Feed", onDrop, StringComparison.Ordinal);
        Assert.DoesNotContain("SetModeAsync(CareMode.None)", onDrop, StringComparison.Ordinal);

        // Otaq özü bağlanmırsa, ✕ düyməsi məcburidir — yoxsa uşaq içəridə qalır.
        var kitchenStart = markup.IndexOf("<div class=\"kitchen\"", StringComparison.Ordinal);
        var kitchenEnd = markup.IndexOf("===== Hamam", kitchenStart, StringComparison.Ordinal);

        Assert.True(kitchenStart >= 0 && kitchenEnd > kitchenStart, "Yemək otağı tapılmadı — test köhnəlib.");

        var kitchen = markup[kitchenStart..kitchenEnd];

        Assert.Contains("overlay-close", kitchen, StringComparison.Ordinal);
        Assert.Contains("SetModeAsync(CareMode.None)", kitchen, StringComparison.Ordinal);
    }

    private static string ReadCare([CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Pages", "Care.razor"));
}
