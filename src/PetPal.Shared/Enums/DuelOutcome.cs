using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>Duelin uşaq baxımından nəticəsi.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DuelOutcome
{
    /// <summary>Rəqib hələ bitirməyib — nəticə sonra hazır olacaq.</summary>
    Pending = 0,
    Win = 1,
    Draw = 2,
    Loss = 3,

    /// <summary>Rəqib tapılmadı, duel ləğv olundu.</summary>
    Expired = 4
}
