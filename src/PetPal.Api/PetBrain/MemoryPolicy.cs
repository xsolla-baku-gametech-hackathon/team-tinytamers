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

    /// <summary>
    /// Saxlama limitini aşan, ən az vacib və ən köhnə xatirələr.
    ///
    /// <para>SEMANTİK nəticələr əvvəl gəlir və praktiki olaraq heç vaxt
    /// düşmür: «kosmosu sevir» faktını itirmək «üçüncü Ay macərasını bitirdi»
    /// faktını itirməkdən qat-qat bahalıdır.</para>
    /// </summary>
    public static IReadOnlyList<PetMemory> Prune(IEnumerable<PetMemory> memories) =>
        [.. memories
            .OrderByDescending(m => m.Tier == PetBrainMemoryTier.Semantic)
            .ThenByDescending(m => m.Importance)
            .ThenByDescending(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Skip(RetentionLimit)];

    // ==================== Axtarış balı ====================

    /// <summary>
    /// Yaxınlarda işlədilmiş xatirəyə verilən cəza — pet özünü təkrarlamamalıdır.
    /// </summary>
    public const int RecentlyUsedPenalty = 40;

    /// <summary>Bu qədər saatdan sonra «yaxınlarda işlədilib» sayılmır.</summary>
    public const int RecentlyUsedHours = 20;

    /// <summary>
    /// Bir xatirənin CARİ AN üçün uyğunluq balı.
    ///
    /// <para>Əvvəl seçim yalnız vacibliyə baxırdı, ona görə pet həmişə eyni
    /// bir neçə «ən vacib» xatirəni deyirdi və zamanla yalnız ilk günlərini
    /// xatırlayan olurdu. Bal indi altı şeyi birlikdə çəkir:</para>
    ///
    /// <list type="number">
    ///   <item><b>Mövzu uyğunluğu</b> — indi Aydayıqsa, Ay xatirəsi öndədir.</item>
    ///   <item><b>Niyyət uyğunluğu</b> — seçim anında seçim xatirəsi işə yarayır.</item>
    ///   <item><b>Yenilik</b> — təzə hadisə daha canlıdır.</item>
    ///   <item><b>Vaciblik</b> — ilk macəra hər zaman qiymətlidir.</item>
    ///   <item><b>İnam</b> — semantik nəticə neçə müşahidəyə söykənir.</item>
    ///   <item><b>Təkrar cəzası</b> — yaxınlarda deyilən cümlə arxaya keçir.</item>
    /// </list>
    /// </summary>
    /// <param name="theme">İndiki mövzu; bilinmirsə boş.</param>
    /// <param name="intent">İndiki an: <c>choice</c>, <c>greeting</c>, <c>puzzle</c>.</param>
    public static int RetrievalScore(
        PetMemory memory, DateTime now, string theme = "", string intent = "")
    {
        var score = memory.Importance;

        // 1) Mövzu uyğunluğu — ən güclü siqnal, çünki uşaq məhz orada durur.
        if (!string.IsNullOrEmpty(theme) &&
            (memory.Tags.Contains(theme, StringComparer.Ordinal)
             || string.Equals(memory.FactKey, theme, StringComparison.Ordinal)))
            score += 45;

        // 2) Niyyət uyğunluğu.
        score += (intent, memory.Kind) switch
        {
            ("choice", PetBrainMemoryKind.ChoiceMade) => 30,
            ("greeting", PetBrainMemoryKind.FirstAdventure) => 25,
            ("greeting", PetBrainMemoryKind.CosmeticUnlocked) => 15,
            ("puzzle", PetBrainMemoryKind.PatternLearned) => 25,
            _ => 0
        };

        // 3) Yenilik — bir aydan köhnə hadisə tədricən sönür.
        var ageDays = Math.Max(0, (now - memory.CreatedAt).TotalDays);
        score += (int)Math.Round(20 * Math.Max(0, 1 - (ageDays / 45.0)), MidpointRounding.AwayFromZero);

        // 5) İnam — semantik nəticə nə qədər müşahidəyə söykənir.
        if (memory.Tier == PetBrainMemoryTier.Semantic)
            score += Math.Min(25, memory.SupportCount * 8);

        // 6) Təkrar cəzası — pet eyni cümləni dalbadal deməməlidir.
        if (memory.LastUsedAt is { } used && (now - used).TotalHours < RecentlyUsedHours)
            score -= RecentlyUsedPenalty;

        return score;
    }

    /// <summary>
    /// Cari an üçün ən uyğun xatirələr — MÜXTƏLİFLİK qorunmaqla.
    ///
    /// <para>Eyni növdən ikinci xatirə siyahıya düşmür: üç «macəranı
    /// bitirdik» cümləsi bir ekranda pet-i lentə çevirir.</para>
    /// </summary>
    public static IReadOnlyList<PetMemory> Retrieve(
        IEnumerable<PetMemory> memories,
        DateTime now,
        string theme = "",
        string intent = "",
        int limit = RetrievalLimit)
    {
        var ranked = memories
            .Where(m => m.ExpiresAt is null || m.ExpiresAt > now)
            .OrderByDescending(m => RetrievalScore(m, now, theme, intent))
            .ThenByDescending(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToList();

        List<PetMemory> picked = [];
        HashSet<PetBrainMemoryKind> kinds = [];

        foreach (var memory in ranked)
        {
            if (picked.Count >= limit)
                break;

            if (kinds.Add(memory.Kind))
                picked.Add(memory);
        }

        // Müxtəliflik limiti doldurmadısa qalanı sırayla tamamlanır — boş
        // ekran müxtəliflikdən vacibdir.
        foreach (var memory in ranked)
        {
            if (picked.Count >= limit)
                break;

            if (!picked.Contains(memory))
                picked.Add(memory);
        }

        return picked;
    }

    /// <summary>
    /// Xatirəni uşağın dilində TƏBİİ cümləyə çevirir.
    ///
    /// <para>Qəsdən "dinozavr üstünlüyün 84%-dir" DEYİL: bal uşağa heç vaxt
    /// göstərilmir, o, nümayiş panelinin işidir.</para>
    /// </summary>
    public static string Render(PetMemory memory, string language, string petName)
    {
        // Semantik nəticə şablona bağlı deyil — o, bir hadisə yox, NAXIŞDIR.
        if (memory.Kind == PetBrainMemoryKind.PatternLearned)
            return SemanticMemory.Render(memory.FactKey, language, petName);

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
        PetBrainMemoryKind.PatternLearned => "🧠",
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
