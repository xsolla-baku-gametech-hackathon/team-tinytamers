using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>
/// "Living world" konsepsiyası: dünya uşağın son günlərdəki iştirakına görə dəyişir.
/// Aktivlik aşağı olanda tutqun/fırtınalı, yüksək olanda günəşli olur.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorldWeather
{
    Storm = 0,
    Rain = 1,
    Cloudy = 2,
    Clear = 3,
    Sunny = 4
}
