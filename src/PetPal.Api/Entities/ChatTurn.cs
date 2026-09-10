namespace PetPal.Api.Entities;

/// <summary>
/// Söhbətin bir replikası — uşağınkı və ya pet-inki.
///
/// Hər replika, o cümlədən filtrin saxladığı uşaq mesajı da saxlanılır:
/// valideyn uşağın nə yazdığını tam görməlidir. Filtr nəyisə saxlayıbsa
/// səbəb <see cref="BlockedReason"/>-da qalır — valideyn panelində
/// "pet cavab vermədi" sətri məhz bununla izah olunur.
/// </summary>
public class ChatTurn
{
    public Guid Id { get; set; }

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    /// <summary>Uşaq yazıb (<c>true</c>) yoxsa pet cavab verib (<c>false</c>).</summary>
    public bool FromChild { get; set; }

    /// <summary>
    /// Uşaq üzrə artan sıra nömrəsi.
    ///
    /// <para>Sıralama üçün <see cref="CreatedAt"/> KİFAYƏT ETMİR: bir sorğuda
    /// yazılan iki replika (uşağın mesajı və pet-in cavabı) eyni vaxt damğasını
    /// alır və hansının əvvəl gəldiyi itir. Bu sütun sıranı saatın dəqiqliyindən
    /// asılı olmaqdan çıxarır.</para>
    /// </summary>
    public int Sequence { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Pet replikası modeldən gəldi, yoxsa qayda əsaslı mətndir.</summary>
    public bool FromAi { get; set; }

    /// <summary>Filtrin saxlama səbəbi (yalnız uşaq mesajları üçün); normal halda <c>null</c>.</summary>
    public string? BlockedReason { get; set; }

    public DateTime CreatedAt { get; set; }
}
