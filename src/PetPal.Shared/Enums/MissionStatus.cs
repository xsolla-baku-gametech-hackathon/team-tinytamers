using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MissionStatus
{
    Locked = 0,
    Active = 1,
    Completed = 2,
    Claimed = 3
}
