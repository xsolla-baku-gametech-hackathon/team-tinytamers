using System.Runtime.CompilerServices;
using PetPal.App.Ui.Services;
using PetPal.Shared.Enums;

namespace PetPal.App.Tests;

/// <summary>
/// Adventure V2 ekranlarının mənbə səviyyəsindəki qaydaları.
///
/// <para>Uzun macərada HUD, jurnal və bərpa kartı uşağın yeganə istiqamət
/// vasitəsidir. Onlarda buraxılan bir əlçatanlıq səhvi app-i sındırmır —
/// sadəcə uşaq harada olduğunu itirir. Ona görə qayda mətndə qorunur.</para>
/// </summary>
public class AdventureHudMarkupTests
{

    /// <summary>HUD uşağın üç sualını cavablandırır: harada, nə, nəyim var.</summary>
    [Fact]
    public void Hud_UcSualiCavablandirir()
    {
        var hud = ReadComponent("AdventureHud");

        Assert.Contains("State.CurrentChapterTitle", hud, StringComparison.Ordinal);
        Assert.Contains("State.ProgressPercent", hud, StringComparison.Ordinal);
        Assert.Contains("State.Objectives", hud, StringComparison.Ordinal);
        Assert.Contains("State.Inventory", hud, StringComparison.Ordinal);
        Assert.Contains("State.Clues", hud, StringComparison.Ordinal);
    }

    /// <summary>
    /// İrəliləmə barı RƏNGDƏN ƏLAVƏ mətn daşıyır — rəng tək daşıyıcı olmamalıdır.
    /// </summary>
    [Fact]
    public void Hud_RengeTekBaglanmir()
    {
        var hud = ReadComponent("AdventureHud");

        Assert.Contains("role=\"img\"", hud, StringComparison.Ordinal);
        Assert.Contains("faiz tamamlanıb", hud, StringComparison.Ordinal);

        Assert.Contains("MarkFor", hud, StringComparison.Ordinal);
    }

    /// <summary>Çanta, jurnal və xəritə düymələri ekran oxuyucusu üçün adlanır.</summary>
    [Fact]
    public void Hud_DuymelerEkranOxuyucusuUcunAdlanir()
    {
        var hud = ReadComponent("AdventureHud");

        Assert.Contains("aria-expanded", hud, StringComparison.Ordinal);
        Assert.Contains("Çanta", hud, StringComparison.Ordinal);
        Assert.Contains("Jurnal", hud, StringComparison.Ordinal);
        Assert.Contains("Fəsil xəritəsi", hud, StringComparison.Ordinal);
    }

    /// <summary>
    /// «Sonra davam edərəm» ekranda VAR və xəbərdarlıq dili daşımır.
    ///
    /// <para>Uzun macərada dayanmaq normaldır: uşağı davam etməyə emosional
    /// məcbur etmək qadağandır.</para>
    /// </summary>
    [Fact]
    public void FesilYekunu_DayanmaqIcazesiVerir()
    {
        var chapter = ReadComponent("ChapterComplete");

        Assert.Contains("Sonra davam edərəm", chapter, StringComparison.Ordinal);
        Assert.Contains("OnLater", chapter, StringComparison.Ordinal);

        foreach (var forbidden in new[] { "itirəcəksən", "you will lose", "son şans", "last chance" })
            Assert.DoesNotContain(forbidden, chapter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Fəsil yekunu seçimləri, mükafatları və növbətini göstərir.</summary>
    [Fact]
    public void FesilYekunu_SecimVeNovbetiniGosterir()
    {
        var chapter = ReadComponent("ChapterComplete");

        Assert.Contains("Model.KeyChoices", chapter, StringComparison.Ordinal);
        Assert.Contains("Model.CompletedObjectives", chapter, StringComparison.Ordinal);
        Assert.Contains("Model.NextChapterTitle", chapter, StringComparison.Ordinal);
        Assert.Contains("Model.MissedOptional", chapter, StringComparison.Ordinal);
    }

    /// <summary>Bərpa kartı «davam et» düyməsindən artıq məlumat daşıyır.</summary>
    [Fact]
    public void BerpaKarti_HaradaQaldigimiziGosterir()
    {
        var resume = ReadComponent("ResumeCard");

        Assert.Contains("Card.ChapterTitle", resume, StringComparison.Ordinal);
        Assert.Contains("Card.ProgressPercent", resume, StringComparison.Ordinal);
        Assert.Contains("Card.CurrentObjective", resume, StringComparison.Ordinal);
        Assert.Contains("Card.KeyItems", resume, StringComparison.Ordinal);
        Assert.Contains("Card.RemainingMinutes", resume, StringComparison.Ordinal);
        Assert.Contains("Card.LastEventSummary", resume, StringComparison.Ordinal);
    }

    /// <summary>
    /// Üç yeni tapmaca komponenti ekran oxuyucusu üçün TAM oxunur.
    ///
    /// <para>Naxış və müşahidə mexanikaları ikon zolağına söykənir; etiket
    /// olmasa onlar yalnız görən uşaq üçün oynanılan olardı.</para>
    /// </summary>
    [Theory]
    [InlineData("SignalPatternPuzzle", "PreviewLabel")]
    [InlineData("ObservationRecallPuzzle", "BeforeLabel")]
    [InlineData("MatchingPairsPuzzle", "NextSocket")]
    public void YeniTapmacalar_EkranOxuyucusuUcunOxunur(string component, string marker)
    {
        var source = ReadComponent(component);

        Assert.Contains(marker, source, StringComparison.Ordinal);
        Assert.Contains("aria-", source, StringComparison.Ordinal);

        Assert.DoesNotContain("MarkupString", source, StringComparison.Ordinal);
    }

    /// <summary>Hər yeni tapmaca formanı da daşıyır — rəng tək daşıyıcı deyil.</summary>
    [Theory]
    [InlineData("SignalPatternPuzzle")]
    [InlineData("ObservationRecallPuzzle")]
    [InlineData("MatchingPairsPuzzle")]
    public void YeniTapmacalar_FormaIsaresiDasiyir(string component) =>
        Assert.Contains("pbx-cell--@", ReadComponent(component), StringComparison.Ordinal);

    /// <summary>
    /// Reyestr HƏR ekran növünü əhatə edir — naməlum növ neytral görünüş alır,
    /// amma boş qalmır.
    /// </summary>
    [Fact]
    public void Reyestr_ButunNovleriEhateEdir() =>
        Assert.All(Enum.GetValues<PetBrainStageKind>(), kind =>
        {
            var look = NodePresentation.For(kind);

            Assert.False(string.IsNullOrWhiteSpace(look.Icon));
            Assert.False(string.IsNullOrWhiteSpace(look.StyleKey));
        });

    /// <summary>Yeni bloklar üçün üslub var — sinif adı boşa getmir.</summary>
    [Theory]
    [InlineData(".pbx-hud")]
    [InlineData(".pbx-panel")]
    [InlineData(".pbx-map")]
    [InlineData(".pbx-changes")]
    [InlineData(".pbx-chapter")]
    [InlineData(".pbx-resume")]
    public void YeniBloklar_UslubaSahibdir(string selector) =>
        Assert.Contains(selector, ReadStyles(), StringComparison.Ordinal);

    private static string ReadComponent(string name) =>
        File.ReadAllText(Path.Combine(UiRoot, "Components", "PetBrain", $"{name}.razor"));

    private static string ReadStyles() =>
        File.ReadAllText(Path.Combine(UiRoot, "wwwroot", "css", "app.css"));

    private static string UiRoot =>
        Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(ThisFile())!, "..", "..", "src", "PetPal.App.Ui"));

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
