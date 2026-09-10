using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>
/// Pet-in inkişaf mərhələsi. Yaş = səviyyə: hər səviyyə pet-ə bir yaş qatır və
/// mərhələ yaş aralığından çıxır — uşaq üçün "səviyyə" mücərrəd rəqəm deyil,
/// pet-in yaşıdır.
///
/// Mərhələlər qəsdən altıdır: pet böyüdükcə görünüşü bir neçə dəfə dəyişməlidir,
/// yoxsa uzun müddət eyni şəkil qalır və böyümə hiss olunmur.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetStage
{
    /// <summary>Hələ açılmayıb.</summary>
    Egg = 0,

    /// <summary>1–2 yaş — nəhəng baş, kiçicik bədən, ən şirin mərhələ.</summary>
    Newborn = 1,

    /// <summary>3–5 yaş — baş hələ böyükdür, bədən yumrulaşır.</summary>
    Baby = 2,

    /// <summary>6–9 yaş — proporsiya balanslaşır, ayaqlar görünür.</summary>
    Child = 3,

    /// <summary>10–14 yaş — uzanır və incəlir, qulaqlar böyüyür.</summary>
    Teen = 4,

    /// <summary>15–21 yaş — tam boy, cizgilər aydınlaşır.</summary>
    Adult = 5,

    /// <summary>22+ yaş — ən iri və enli, sinədə yumşaq yal.</summary>
    Elder = 6
}
