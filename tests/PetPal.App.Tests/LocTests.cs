using System.Globalization;
using PetPal.App.Ui.Services;
using PetPal.Shared.Dtos.Auth;
using PetPal.Shared.Enums;
using PetPal.Shared.Validation;

namespace PetPal.App.Tests;

/// <summary>
/// İnterfeysin dili: uşağın profilindən gəlir, cihazda qalır və heç bir
/// ekranda tərcüməsiz mətn buraxmır.
/// </summary>
public class LocTests
{
    [Fact]
    public async Task T_DileGoreVariantSecir()
    {
        var loc = new Loc(new InMemoryTokenStore());

        Assert.Equal("Otaq", loc.T("Otaq", "Room"));

        await loc.UseAsync("en");

        Assert.Equal("Room", loc.T("Otaq", "Room"));
    }

    [Fact]
    public void Normalize_DestelenmeyenDilAzerbaycancaDusur()
    {
        Assert.Equal("az", Loc.Normalize("de"));
        Assert.Equal("az", Loc.Normalize(null));
        Assert.Equal("en", Loc.Normalize("EN"));
    }

    /// <summary>
    /// Dil cihazda qalır: giriş və profil ekranları uşaq seçilməmişdən ƏVVƏL
    /// açılır, yəni sessiyadan dil almağa imkan yoxdur.
    /// </summary>
    [Fact]
    public async Task Dil_CihazdaSaxlanilirVeBerpaOlunur()
    {
        var store = new InMemoryTokenStore();

        await new Loc(store).UseAsync("en");

        var restored = new Loc(store);
        await restored.RestoreAsync();

        Assert.True(restored.IsEnglish);
    }

    /// <summary>Uşaq profilinə keçid dili də dəyişir — sual da, düymə də.</summary>
    [Fact]
    public async Task UsaqSessiyasi_InterfeysinDiliniDeyisir()
    {
        var store = new InMemoryTokenStore();
        var loc = new Loc(store);
        var session = new AppSession(store, loc);

        await session.ActivateChildAsync(new AuthResponse
        {
            AccessToken = "child-token",
            RefreshToken = "child-refresh",
            ProfileKind = ProfileKind.Child,
            ChildId = Guid.NewGuid(),
            LanguageCode = "en"
        });

        Assert.True(loc.IsEnglish);
    }

    /// <summary>
    /// Yeni enum dəyəri əlavə edəndə tərcüməsi unudulursa, ad hər iki dildə
    /// eyni qalır — bu test məhz onu tutur.
    /// </summary>
    [Fact]
    public async Task EnumAdlari_HerIkiDildeVar()
    {
        var az = new Loc(new InMemoryTokenStore());
        var en = new Loc(new InMemoryTokenStore());
        await en.UseAsync("en");

        foreach (var stage in Enum.GetValues<PetStage>())
            Assert.NotEqual(DisplayNames.Stage(az, stage), DisplayNames.Stage(en, stage));

        foreach (var mood in Enum.GetValues<PetMood>())
            Assert.NotEqual(DisplayNames.Mood(az, mood), DisplayNames.Mood(en, mood));

        foreach (var skill in Enum.GetValues<SkillArea>())
            Assert.NotEqual(DisplayNames.Skill(az, skill), DisplayNames.Skill(en, skill));

        Assert.Equal(
            DisplayNames.SpeciesOptions(az).Select(o => o.Key),
            DisplayNames.SpeciesOptions(en).Select(o => o.Key));

        Assert.All(
            DisplayNames.SpeciesOptions(az).Zip(DisplayNames.SpeciesOptions(en)),
            pair => Assert.NotEqual(pair.First.Label, pair.Second.Label));
    }

    /// <summary>
    /// Forma qaydalarının mesajı DataAnnotations atributundan gəlir və dili
    /// <see cref="CultureInfo.CurrentUICulture"/> həll edir — <c>Loc</c> onu
    /// dil dəyişəndə özü qurur.
    /// </summary>
    [Fact]
    public async Task FormaMesajlari_DilDeyisendeTercumeOlunur()
    {
        var loc = new Loc(new InMemoryTokenStore());

        await loc.UseAsync("az");
        Assert.Equal("PIN vacibdir.", ValidationMessages.PinRequired);

        await loc.UseAsync("en");
        Assert.Equal("A PIN is required.", ValidationMessages.PinRequired);
    }
}
