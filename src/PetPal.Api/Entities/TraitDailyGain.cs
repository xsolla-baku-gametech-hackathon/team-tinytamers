using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Bir uşağın bir xassə açarı üzrə BİR GÜNDƏ topladığı müsbət bal.
///
/// <para>Gündəlik tavan əvvəllər <c>BehaviorTracker</c>-in içindəki lüğətdə
/// saxlanılırdı. O lüğət yalnız BİR sorğunun (bir DI scope-un) ömrü boyu
/// yaşayırdı, yəni tavan əslində «sorğu daxilində tavan» idi: uşaq ekranı
/// bağlayıb yenidən açanda sayğac sıfırlanır və eyni açar günə neçə dəfə
/// istəsə artırıla bilirdi.</para>
///
/// <para>İndi sayğac BAZADADIR və günə bağlıdır. Artım
/// <see cref="PetBrain.TraitDailyLedger"/> tərəfindən müqayisə-və-yaz (CAS)
/// ilə aparılır, ona görə paralel iki sorğu birlikdə də tavanı keçə bilmir.</para>
///
/// <para>Sətir uşağın nə etdiyini DEYİL, yalnız neçə bal qazandığını saxlayır —
/// burada nə sərbəst mətn, nə hadisə adı, nə də vaxt möhürü zənciri var.</para>
/// </summary>
public class TraitDailyGain
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public PetBrainTraitCategory Category { get; set; }

    /// <summary>Təsdiqlənmiş açar — bax <see cref="PetBrain.TraitKeys"/>.</summary>
    public string TraitKey { get; set; } = string.Empty;

    /// <summary>
    /// UTC gün, <c>yyyyMMdd</c> formasında tam ədəd.
    ///
    /// <para>Tarix tipi əvəzinə tam ədəd QƏSDƏN seçilib: sayğac həm PostgreSQL,
    /// həm də testlərdəki SQLite üzərində eyni işləməlidir və tam ədəd hər iki
    /// provayderdə eyni müqayisə və eyni parametr çevrilməsini verir.</para>
    /// </summary>
    public int DayKey { get; set; }

    /// <summary>Bu gün toplanan müsbət balların cəmi (0–tavan).</summary>
    public int Gained { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>UTC tarixdən gün açarı.</summary>
    public static int KeyFor(DateTime utc) => (utc.Year * 10_000) + (utc.Month * 100) + utc.Day;
}
