using PetPal.Api.Common;

namespace PetPal.Api.PetBrain.Recap;

/// <summary>
/// Bir kadr: vaxt aralığı, modelə gedən İNGİLİS ifadə və uşağa göstərilən
/// LOKALLAŞDIRILMIŞ altyazı.
/// </summary>
/// <param name="StartSeconds">Başlanğıc — 0.0, 3.0, 7.0.</param>
/// <param name="EndSeconds">Son — 3.0, 7.0, 10.0.</param>
/// <param name="Motion">Modelə gedən nəzərdən keçirilmiş ifadə.</param>
/// <param name="Caption">Uşağın dilində altyazı — DETERMİNİSTİK qatdadır.</param>
/// <param name="Icon">Altyazının işarəsi.</param>
public sealed record RecapShot(
    double StartSeconds,
    double EndSeconds,
    string Motion,
    string Caption,
    string Icon);

/// <summary>
/// Üç kadrlı, <b>tam 10 saniyəlik</b> storyboard.
///
/// <para>Kadrlar uşağın HƏQİQİ seçimlərini əks etdirir: seçilməyən variant heç
/// vaxt göstərilmir. Şablonda olmayan an isə uydurulmur — sadəcə buraxılır və
/// yerinə neytral, hekayəyə uyğun kadr qalır.</para>
///
/// <para>Altyazılar burada, serverdə qurulur və <b>videonun içinə düşmür</b>.
/// Nəticə: model kiçik vizual uyğunsuzluq versə də, ekranda yazılan mətn
/// uşağın həqiqətən seçdiyi şeydir.</para>
/// </summary>
public static class RecapStoryboard
{
    public const double TotalSeconds = 10.0;

    public static IReadOnlyList<RecapShot> Build(AdventureRecapSpec spec)
    {
        var language = spec.Language;

        return spec.ExperienceKey switch
        {
            ExperienceCatalog.MarsRoverRescue => Mars(spec, language),
            ExperienceCatalog.DragonLostColors => Dragon(spec, language),
            _ => Neutral(language)
        };
    }

    // ==================== Mars ====================

    private static IReadOnlyList<RecapShot> Mars(AdventureRecapSpec spec, string language)
    {
        var route = Choice(spec, "route");
        var rescue = Choice(spec, "rescue");

        return
        [
            new(0.0, 3.0,
                $"the pet launches a tiny repair drone into {MarsRouteMotion(route)} after a gentle dust storm",
                Localized.T(language,
                    $"Robonu {MarsRouteAz(route)} axtardıq.",
                    $"We searched for Robo in {MarsRouteEn(route)}."),
                MarsRouteIcon(route)),

            new(3.0, 7.0,
                "the drone travels onward, pauses at a solar charging station, then reaches a ridge antenna; " +
                "the antenna lights up with a soft blue signal",
                Localized.T(language,
                    "Dron enerji topladı və antenanı bərpa etdi.",
                    "The drone gathered energy and repaired the antenna."),
                "📡"),

            new(7.0, 10.0,
                $"{MarsRescueMotion(rescue)}; Robo powers on safely and celebrates with the pet" +
                Cosmetic(spec.PetCosmetic),
                Localized.T(language,
                    $"Robonu {MarsRescueAz(rescue)} xilas etdin!",
                    $"You rescued Robo by {MarsRescueEn(rescue)}!"),
                MarsRescueIcon(rescue))
        ];
    }

    private static string MarsRouteMotion(string key) => key switch
    {
        "crater" => "a wide shadowed Martian crater",
        "mountain" => "a tall windswept Martian mountain slope",
        _ => "a warm orange Martian canyon"
    };

    private static string MarsRouteAz(string key) => key switch
    {
        "crater" => "kraterdə",
        "mountain" => "dağda",
        _ => "kanyonda"
    };

    private static string MarsRouteEn(string key) => key switch
    {
        "crater" => "the crater",
        "mountain" => "the mountain",
        _ => "the canyon"
    };

    private static string MarsRouteIcon(string key) => key switch
    {
        "crater" => "🕳️",
        "mountain" => "⛰️",
        _ => "🏜️"
    };

    private static string MarsRescueMotion(string key) => key switch
    {
        "battery" => "a fresh rounded battery clicks gently into place beside Robo",
        "carry-to-ship" => "the pet carefully carries Robo aboard a friendly rounded ship",
        _ => "a compact solar panel unfolds beside Robo"
    };

    private static string MarsRescueAz(string key) => key switch
    {
        "battery" => "batareyanı dəyişib",
        "carry-to-ship" => "gəmiyə aparıb",
        _ => "günəş paneli qurub"
    };

    private static string MarsRescueEn(string key) => key switch
    {
        "battery" => "swapping the battery",
        "carry-to-ship" => "carrying it to the ship",
        _ => "building a solar panel"
    };

    private static string MarsRescueIcon(string key) => key switch
    {
        "battery" => "🔋",
        "carry-to-ship" => "🛸",
        _ => "☀️"
    };

    // ==================== Əjdaha ====================

    private static IReadOnlyList<RecapShot> Dragon(AdventureRecapSpec spec, string language)
    {
        var palette = Choice(spec, "palette");
        var habitat = Choice(spec, "habitat");

        return
        [
            new(0.0, 3.0,
                $"a small friendly dragon appears in {DragonHabitatMotion(habitat)}, its wings still pale",
                Localized.T(language,
                    $"Əjdahanı {DragonHabitatAz(habitat)} tapdıq.",
                    $"We found the dragon in {DragonHabitatEn(habitat)}."),
                DragonHabitatIcon(habitat)),

            new(3.0, 7.0,
                $"soft {DragonPaletteMotion(palette)} light fragments drift safely onto the dragon's wings, " +
                "forming a gentle pattern",
                Localized.T(language,
                    $"{DragonPaletteAz(palette)} rəngləri qanadlara qayıtdı.",
                    $"The {DragonPaletteEn(palette)} colours returned to its wings."),
                DragonPaletteIcon(palette)),

            new(7.0, 10.0,
                "the restored dragon opens its colored wings beside the pet and the surroundings glow warmly" +
                Cosmetic(spec.PetCosmetic),
                Localized.T(language,
                    "Əjdaha yenidən parıldayır — rəngləri sən seçdin!",
                    "The dragon shines again — and you chose its colours!"),
                "✨")
        ];
    }

    private static string DragonPaletteMotion(string key) => key switch
    {
        "ocean" => "blue and turquoise",
        "forest" => "green and emerald",
        "berry" => "purple and pink",
        _ => "orange and berry"
    };

    private static string DragonPaletteAz(string key) => key switch
    {
        "ocean" => "Okean",
        "forest" => "Meşə",
        "berry" => "Moruq bağı",
        _ => "Gün batımı"
    };

    private static string DragonPaletteEn(string key) => key switch
    {
        "ocean" => "ocean",
        "forest" => "forest",
        "berry" => "berry garden",
        _ => "sunset"
    };

    private static string DragonPaletteIcon(string key) => key switch
    {
        "ocean" => "🌊",
        "forest" => "🌿",
        "berry" => "🫐",
        _ => "🌅"
    };

    private static string DragonHabitatMotion(string key) => key switch
    {
        "cloud-castle" => "a calm castle of soft clouds",
        "flower-meadow" => "a wide meadow of gentle flowers",
        _ => "a sparkling crystal cave"
    };

    private static string DragonHabitatAz(string key) => key switch
    {
        "cloud-castle" => "bulud qalasında",
        "flower-meadow" => "çiçək çəmənliyində",
        _ => "kristal mağarada"
    };

    private static string DragonHabitatEn(string key) => key switch
    {
        "cloud-castle" => "a cloud castle",
        "flower-meadow" => "a flower meadow",
        _ => "a crystal cave"
    };

    private static string DragonHabitatIcon(string key) => key switch
    {
        "cloud-castle" => "☁️",
        "flower-meadow" => "🌸",
        _ => "💎"
    };

    // ==================== Ümumi ====================

    private static IReadOnlyList<RecapShot> Neutral(string language) =>
    [
        new(0.0, 3.0, "the pet steps into a soft storybook landscape",
            Localized.T(language, "Macəra başladı.", "The adventure began."), "🌤️"),

        new(3.0, 7.0, "the pet explores calmly and finds what it was looking for",
            Localized.T(language, "Birlikdə axtardıq.", "We searched together."), "🔎"),

        new(7.0, 10.0, "the pet celebrates gently as the scene brightens",
            Localized.T(language, "Bacardıq!", "We did it!"), "🎉")
    ];

    /// <summary>Kosmetik yalnız qazanılıbsa görünür — uydurma geyim yoxdur.</summary>
    private static string Cosmetic(string key) => key switch
    {
        "helmet-mars" => ", the pet wearing a rounded Mars explorer helmet",
        "wings-rainbow" => ", the pet wearing soft rainbow wings",
        _ => string.Empty
    };

    /// <summary>
    /// Bu an üçün uşağın HƏQİQİ seçimi. Şablonda belə an yoxdursa boş qayıdır
    /// və ifadə neytral variantı işlədir — <b>əks seçim heç vaxt göstərilmir</b>.
    /// </summary>
    private static string Choice(AdventureRecapSpec spec, string beatKey) =>
        spec.Beats.FirstOrDefault(b => string.Equals(b.BeatKey, beatKey, StringComparison.Ordinal))?.ChoiceKey
        ?? string.Empty;
}
