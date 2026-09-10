using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>
/// Pet-in ÖZ niyyəti — uşaq baxmayanda nə etdiyi.
///
/// <para>Siyahı QAPALIDIR və qəsdən kiçikdir: pet "istədiyini" edə bilmir,
/// yalnız bu yeddi işdən birini seçir. Hər birinin nəticəsi də serverin
/// əvvəlcədən təsdiqlədiyi artefakt və cümlə açarlarındandır.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainIntentType
{
    /// <summary>Dincəlir — enerji az olanda.</summary>
    Rest = 0,

    /// <summary>Ətrafı gəzir.</summary>
    Explore = 1,

    /// <summary>Bir şey qurur, rəsm çəkir.</summary>
    Create = 2,

    /// <summary>Kiməsə kömək edir (missiya, dost).</summary>
    Help = 3,

    /// <summary>Nəyisə öyrənir.</summary>
    Learn = 4,

    /// <summary>Oynayır.</summary>
    Play = 5,

    /// <summary>Öz qayğısına qalır — çirkli və ya ac olanda.</summary>
    Care = 6
}

/// <summary>Niyyətin vəziyyəti.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainIntentStatus
{
    /// <summary>Davam edir.</summary>
    Active = 0,

    /// <summary>Vaxtı çatdı və nəticəsi hazırdır.</summary>
    Completed = 1,

    /// <summary>
    /// Kənara qoyuldu — vəziyyət dəyişdi (uşaq macərəya çıxdı, ekran vaxtı
    /// bağlandı). <b>Uşağın günahı deyil</b> və ona belə göstərilmir.
    /// </summary>
    Superseded = 2
}
