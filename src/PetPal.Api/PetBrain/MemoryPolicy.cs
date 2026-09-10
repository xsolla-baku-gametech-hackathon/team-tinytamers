using PetPal.Api.Common;
using PetPal.Api.Entities;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Uzunmüddətli yaddaşın saxlanma və seçilmə qaydaları — saf funksiyalar.
///
/// <para>Yaddaş STRUKTURLUDUR: bazada açarlar durur, cümlə isə render zamanı
/// uşağın dilində qurulur. Ona görə:</para>
/// <list type="bullet">
///   <item>Valideyn dili dəyişəndə köhnə xatirələr də tərcümə olunur.</item>
///   <item>Bazada uşağın yazdığı heç bir sərbəst mətn qalmır.</item>
/// </list>
///
/// <para>Söhbət replikaları buraya HEÇ VAXT düşmür — onlar ayrı audit
/// jurnalıdır (<see cref="ChatTurn"/>) və oradan maraq çıxarılmır.</para>
/// </summary>
public static class MemoryPolicy
{
    /// <summary>Ekranda və salamlamada nəzərə alınan ən çox xatirə sayı.</summary>
    public const int RetrievalLimit = 3;

    /// <summary>Bir uşaq üçün saxlanan ən çox xatirə — köhnə və az vacib olan düşür.</summary>
    public const int RetentionLimit = 40;

    // Vaciblik dəyərləri: böyük rəqəm əvvəl göstərilir.
    public const int FirstAdventureImportance = 95;
    public const int CosmeticImportance = 80;
    public const int CompletionImportance = 70;
    public const int ChoiceImportance = 55;
    public const int PreferenceImportance = 45;

    /// <summary>
    /// Salamlama və ekran üçün ən uyğun xatirələr.
    ///
    /// <para>Sıra: vaciblik → yenilik. Bərabər hallarda son yaranan qazanır,
    /// yəni pet "bu yaxınlarda birlikdə etdiyimiz" şeyi xatırlayır.</para>
    /// </summary>
    public static IReadOnlyList<PetMemory> Select(
        IEnumerable<PetMemory> memories,
        DateTime now,
        int limit = RetrievalLimit) =>
        [.. memories
            .Where(m => m.ExpiresAt is null || m.ExpiresAt > now)
            .OrderByDescending(m => m.Importance)
            .ThenByDescending(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Take(limit)];

    /// <summary>
    /// Salamlamada işlədiləcək BİR xatirə. Son dəfə istifadə olunan xatirə
    /// arxaya atılır — pet hər açılışda eyni cümləni təkrarlamamalıdır.
    /// </summary>
    public static PetMemory? PickForGreeting(IEnumerable<PetMemory> memories, DateTime now) =>
        memories
            .Where(m => m.ExpiresAt is null || m.ExpiresAt > now)
            .OrderBy(m => m.LastUsedAt ?? DateTime.MinValue)
            .ThenByDescending(m => m.Importance)
            .ThenByDescending(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .FirstOrDefault();

    /// <summary>Saxlama limitini aşan, ən az vacib və ən köhnə xatirələr.</summary>
    public static IReadOnlyList<PetMemory> Prune(IEnumerable<PetMemory> memories) =>
        [.. memories
            .OrderByDescending(m => m.Importance)
            .ThenByDescending(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Skip(RetentionLimit)];

    /// <summary>
    /// Xatirəni uşağın dilində TƏBİİ cümləyə çevirir.
    ///
    /// <para>Qəsdən "dinozavr üstünlüyün 84%-dir" DEYİL: bal uşağa heç vaxt
    /// göstərilmir, o, nümayiş panelinin işidir.</para>
    /// </summary>
    public static string Render(PetMemory memory, string language, string petName)
    {
        var template = ExperienceCatalog.Find(memory.FactKey);
        var title = template?.Title(language) ?? memory.FactKey;

        return memory.Kind switch
        {
            PetBrainMemoryKind.FirstAdventure => Localized.T(language,
                $"Bizim ilk böyük macəramız «{title}» idi — onu heç vaxt unutmaram!",
                $"Our first big adventure was «{title}» — I will never forget it!"),

            PetBrainMemoryKind.ExperienceCompleted => Localized.T(language,
                $"«{title}» macərasını birlikdə bitirdik!",
                $"We finished «{title}» together!"),

            PetBrainMemoryKind.ChoiceMade => RenderChoice(memory, template, language),

            PetBrainMemoryKind.CosmeticUnlocked => Localized.T(language,
                $"{AccessoryName(memory.FactKey, language)} — onu sən qazandın, indi mənim üstümdədir.",
                $"{AccessoryName(memory.FactKey, language)} — you earned it, and now I wear it."),

            PetBrainMemoryKind.PreferenceObserved => Localized.T(language,
                $"{TraitKeys.Label(memory.FactKey, language)} mövzusunu təkrar-təkrar seçirsən — mən də sevdim!",
                $"You keep choosing {TraitKeys.Label(memory.FactKey, language).ToLowerInvariant()} — I love it too!"),

            _ => Localized.T(language,
                $"{petName} bu anı yadda saxladı.",
                $"{petName} remembered this moment.")
        };
    }

    public static string Icon(PetMemory memory) => memory.Kind switch
    {
        PetBrainMemoryKind.FirstAdventure => "🏅",
        PetBrainMemoryKind.ExperienceCompleted => "🏁",
        PetBrainMemoryKind.ChoiceMade => "💡",
        PetBrainMemoryKind.CosmeticUnlocked => "🎁",
        PetBrainMemoryKind.PreferenceObserved => "💜",
        _ => "✨"
    };

    public static PetBrainMemoryDto ToDto(PetMemory memory, string language, string petName) => new()
    {
        Kind = memory.Kind,
        Text = Render(memory, language, petName),
        Icon = Icon(memory),
        Importance = memory.Importance
    };

    private static string RenderChoice(PetMemory memory, ExperienceTemplate? template, string language)
    {
        var option = template?.Stages
            .SelectMany(s => s.Options)
            .FirstOrDefault(o => string.Equals(o.Key, memory.ValueKey, StringComparison.Ordinal));

        if (option is null || template is null)
            return Localized.T(language, "Seçimini yaxşı xatırlayıram!", "I remember your choice well!");

        return Localized.T(language,
            $"«{template.Title(language)}» macərasında sən «{option.Label(language)}» seçdin.",
            $"In «{template.Title(language)}» you chose «{option.Label(language)}».");
    }

    private static string AccessoryName(string code, string language) => code switch
    {
        ExperienceCatalog.HelmetMars => Localized.T(language, "Mars dəbilqəsi", "The Mars helmet"),
        ExperienceCatalog.WingsRainbow => Localized.T(language, "Göy qurşağı qanadları", "The rainbow wings"),
        _ => Localized.T(language, "Yeni əşya", "A new item")
    };
}
