using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>Missiyanın hansı davranışla irəlilədiyini müəyyən edir.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MissionType
{
    SolveQuestions = 0,
    CareForPet = 1,
    DiscoverRealWorld = 2,
    KeepStreak = 3,
    TeamChallenge = 4
}
