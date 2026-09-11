using System.Runtime.CompilerServices;

namespace PetPal.App.Tests;

/// <summary>
/// Paltar otağının dizayn studiyası ekranda: uşaq arzusunu yazır, dizayn
/// güzgüdə görünür, valideyn isə açarı və hər arzunu görür.
/// </summary>
public class WardrobeMarkupTests
{
    [Fact]
    public void PaltarOtagi_StudiyaniVeGuzgudekiGorunusu_Gosterir()
    {
        var care = ReadPage("Care.razor");

        Assert.Contains("<WardrobeStudio Open=\"_studioOpen\"", care, StringComparison.Ordinal);
        Assert.Contains("closet__design", care, StringComparison.Ordinal);
        Assert.Contains("class=\"closet__look\"", care, StringComparison.Ordinal);
        Assert.Contains("OnLookChanged=\"ShowLook\"", care, StringComparison.Ordinal);
    }

    [Fact]
    public void Studiya_MetinHeddini_ValideynAcariniVeXidmetiNezereAlir()
    {
        var studio = ReadComponent("WardrobeStudio");

        Assert.Contains("maxlength=\"@_state.MaxTextLength\"", studio, StringComparison.Ordinal);
        Assert.Contains("!_state.ParentAllowed", studio, StringComparison.Ordinal);
        Assert.Contains("!_state.ServiceReady", studio, StringComparison.Ordinal);
        Assert.Contains("DesignsLeftToday", studio, StringComparison.Ordinal);
        Assert.Contains("DesignsPerDay: > 0", studio, StringComparison.Ordinal);
    }

    /// <summary>Hazır olmayan dizayn sonsuz soruşulmur — yoxlama sayı sərhədlidir.</summary>
    [Fact]
    public void Studiya_GozlemeniSerhedleyir()
    {
        var studio = ReadComponent("WardrobeStudio");

        Assert.Contains("private const int MaxPolls", studio, StringComparison.Ordinal);
        Assert.Contains("attempt < MaxPolls", studio, StringComparison.Ordinal);
    }

    [Fact]
    public void ValideynPaneli_StudiyaAcariVeArzuJurnaliVar()
    {
        var dashboard = ReadPage("ParentDashboard.razor");

        Assert.Contains("ToggleWardrobeAsync", dashboard, StringComparison.Ordinal);
        Assert.Contains("_wardrobeLog.Entries", dashboard, StringComparison.Ordinal);
        Assert.Contains("gpt-image-2.5-flare", dashboard, StringComparison.Ordinal);
    }

    private static string ReadPage(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Pages", name));

    private static string ReadComponent(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui",
            "Components", "PetBrain", name + ".razor"));
}
