using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BadgeTier
{
    Bronze = 0,
    Silver = 1,
    Gold = 2
}
