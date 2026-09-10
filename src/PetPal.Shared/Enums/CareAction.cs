using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>Care ekranındakı əməliyyatlar (Feed / Play / Clean / Sleep).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CareAction
{
    Feed = 0,
    Play = 1,
    Clean = 2,
    Sleep = 3
}
