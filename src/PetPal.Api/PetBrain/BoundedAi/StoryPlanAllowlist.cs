using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Story;

namespace PetPal.Api.PetBrain.BoundedAi;

/// <summary>
/// Modelin seçə biləcəyi HƏR ŞEYİN qapalı siyahısı.
///
/// <para>Model bu siyahılardan kənarda heç nə deyə bilməz — nə yeni personaj,
/// nə yeni səhnə, nə yeni ton, nə də yeni sonluq. Uydurulmuş ID validatorda
/// dayanır və plan bütövlükdə rədd olunur.</para>
///
/// <para><b>Nə üçün ID-lər, mətn yox?</b> Mətn qəbul etsəydik, hər cümləni
/// ayrıca yoxlamalı olardıq — uzunluq, dil, təhlükəsizlik, yaş uyğunluğu,
/// lokalizasiya. ID isə ya siyahıdadır, ya yoxdur; yoxlama birmənalıdır və
/// mətn həmişə bizim yazdığımız qalır.</para>
/// </summary>
public static class StoryPlanAllowlist
{
    /// <summary>Hekayə anları — düyünün «nə haqqında» olduğu.</summary>
    public static IReadOnlyList<string> Beats { get; } =
    [
        "arrival", "choice-point", "discovery", "setback", "insight",
        "helping-hand", "journey", "reunion", "farewell"
    ];

    /// <summary>Səhnələr — UI-ın çəkə bildiyi variantlar.</summary>
    public static IReadOnlyList<string> Scenes { get; } =
    [
        "moon-arrival", "moon-three-craters", "moon-light-ribbon", "moon-deep-hum",
        "moon-echo-walls", "moon-mirror-field", "moon-crystal-bright", "moon-crystal-glow",
        "moon-carry", "moon-summit-finale", "moon-signal-finale", "moon-lantern-finale"
    ];

    /// <summary>Tonlar — pet-in danışıq rəngi.</summary>
    public static IReadOnlyList<string> Tones { get; } =
        ["curious", "creative", "adventurous", "caring", "calm"];

    /// <summary>Personajlar — yalnız pet və kataloqdakı dostlar.</summary>
    public static IReadOnlyList<string> Characters { get; } = ["pet", "robo", "crystal", "moon-rover"];

    /// <summary>Keçid növləri — şərt DİLİ deyil, qapalı siyahı.</summary>
    public static IReadOnlyList<string> TransitionTypes { get; } = ["always", "choice", "solved"];

    /// <summary>Tapmaca adapterləri — mövcud kataloqdan.</summary>
    public static IReadOnlyList<string> PuzzleAdapters { get; } =
        [.. PuzzleBlueprintCatalog.Blueprints.Select(b => b.Key)];

    /// <summary>Yaddaş çağırışları — pet yalnız bunları xatırlaya bilər.</summary>
    public static IReadOnlyList<string> MemoryCallbacks { get; } =
        ["moon-first-choice", "last-adventure", "favourite-theme"];

    /// <summary>Sonluqlar — kataloqdakı mövcud sonluqlar.</summary>
    public static IReadOnlyList<string> Endings { get; } =
        [.. StoryCatalog.Definitions.SelectMany(d => d.EndingKeys).Distinct(StringComparer.Ordinal)];

    /// <summary>Bu macəra üçün modelə göndəriləcək icazə dəsti.</summary>
    public static StoryPlanRequest RequestFor(
        string language, string ageBand, string theme, string experienceKey) => new(
        Language: language,
        AgeBand: ageBand,
        Theme: theme,
        ExperienceKey: experienceKey,
        AllowedBeatIds: Beats,
        AllowedSceneIds: Scenes,
        AllowedToneIds: Tones,
        AllowedPuzzleAdapterIds: PuzzleAdapters,
        AllowedMemoryCallbackIds: MemoryCallbacks,
        AllowedEndingIds: Endings,
        AllowedCharacterIds: Characters);
}
