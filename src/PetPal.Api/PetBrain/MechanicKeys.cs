using PetPal.Api.Common;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Oyun MEXANİKALARININ qapalı siyahısı — «hansı qarşılıqlı təsiri sevir».
///
/// <para><b>Mövzudan qəsdən ayrıdır.</b> Uşaq kosmosu sevə, kosmos marşrut
/// tapmacasını isə sevməyə bilər. Tək bal bu iki fərqli faktı saxlaya bilmirdi
/// və sistem «kosmos təklif et» deyib eyni sevilməyən mexanikanı
/// təkrarlayırdı.</para>
///
/// <para><b>Siyahı niyə qısadır?</b> Hər açarın burada həm MƏNBƏYİ
/// (<see cref="ProfileLearningRules"/>), həm də İSTEHLAKÇISI
/// (<see cref="ExperienceTemplate.MechanicAffinity"/> və mexanika ustalığı)
/// olmalıdır. Saxlanılan, amma heç nəyə təsir etməyən ölçü profil deyil, ölü
/// sütundur — ona görə kataloqda qarşılığı olmayan mexanika bura
/// düşmür.</para>
///
/// <para>Klientdən heç bir açar qəbul edilmir: naməlum ad səssizcə nəzərə
/// alınmır (fail closed).</para>
/// </summary>
public static class MechanicKeys
{
    /// <summary>Şərtə uyğun yolu planlaşdırmaq.</summary>
    public const string Route = "route";

    /// <summary>Addımları düzgün sıraya düzmək.</summary>
    public const string Sequencing = "sequencing";

    /// <summary>Naxış tapmaq və bərpa etmək.</summary>
    public const string Pattern = "pattern";

    /// <summary>Gördüyünü yadda saxlayıb geri çağırmaq.</summary>
    public const string Memory = "memory";

    /// <summary>Səhnədə vacib detalı görmək.</summary>
    public const string Observation = "observation";

    /// <summary>Hissələrdən bir şey qurmaq.</summary>
    public const string Building = "building";

    /// <summary>Bəzəmək, rəng və görünüş seçmək.</summary>
    public const string Decorating = "decorating";

    /// <summary>Hekayənin gedişatını seçmək.</summary>
    public const string StoryChoice = "story-choice";

    /// <summary>Qayğı göstərmək, kömək etmək.</summary>
    public const string Nurturing = "nurturing";

    /// <summary>Kəşf etmək, yeni yerə getmək.</summary>
    public const string Exploration = "exploration";

    /// <summary>Sınamaq, nə baş verdiyini görmək.</summary>
    public const string Experimentation = "experimentation";

    public static readonly IReadOnlyList<string> All =
    [
        Route, Sequencing, Pattern, Memory, Observation,
        Building, Decorating, StoryChoice, Nurturing, Exploration, Experimentation
    ];

    public static bool IsKnown(string? key) =>
        !string.IsNullOrWhiteSpace(key) && All.Contains(key, StringComparer.Ordinal);

    /// <summary>
    /// Tapmaca mexanikasının profil açarı.
    ///
    /// <para>İki marşrut mexanikası (<c>RouteLogic</c>, <c>OrderedRoute</c>)
    /// eyni açara düşür: uşaq üçün ikisi də «yolu tap»dır və onları ayrı
    /// öyrənmək hər birinin sübutunu yarıya bölərdi.</para>
    /// </summary>
    public static string? For(PetBrainPuzzleMechanic mechanic) => mechanic switch
    {
        PetBrainPuzzleMechanic.RouteLogic => Route,
        PetBrainPuzzleMechanic.OrderedRoute => Route,
        PetBrainPuzzleMechanic.SequenceOrder => Sequencing,
        PetBrainPuzzleMechanic.LightFragments => Pattern,
        PetBrainPuzzleMechanic.SignalPattern => Pattern,
        PetBrainPuzzleMechanic.ObservationRecall => Memory,
        PetBrainPuzzleMechanic.MatchingPairs => Observation,
        _ => null
    };

    public static string Label(string key, string language) => key switch
    {
        Route => Localized.T(language, "Yol tapmaq", "Route finding"),
        Sequencing => Localized.T(language, "Sıraya düzmək", "Putting in order"),
        Pattern => Localized.T(language, "Naxış tapmaq", "Spotting patterns"),
        Memory => Localized.T(language, "Yadda saxlamaq", "Remembering"),
        Observation => Localized.T(language, "Diqqətlə baxmaq", "Looking closely"),
        Building => Localized.T(language, "Qurmaq", "Building"),
        Decorating => Localized.T(language, "Bəzəmək", "Decorating"),
        StoryChoice => Localized.T(language, "Hekayə seçmək", "Choosing the story"),
        Nurturing => Localized.T(language, "Qayğı göstərmək", "Caring"),
        Exploration => Localized.T(language, "Kəşf etmək", "Exploring"),
        Experimentation => Localized.T(language, "Sınamaq", "Experimenting"),
        _ => key
    };

    public static string Icon(string key) => key switch
    {
        Route => "🧭",
        Sequencing => "🔢",
        Pattern => "🔷",
        Memory => "🧠",
        Observation => "🔍",
        Building => "🧱",
        Decorating => "🎨",
        StoryChoice => "📖",
        Nurturing => "💚",
        Exploration => "🗺️",
        Experimentation => "⚗️",
        _ => "✨"
    };

    public static PetBrainTraitDto ToDto(string key, int score, string language) => new()
    {
        Key = key,
        Label = Label(key, language),
        Icon = Icon(key),
        Score = TraitKeys.Clamp(score)
    };
}
