using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>Pet-in cari əhval-ruhiyyəsi — care statlarından hesablanır, UI-da animasiya/replika seçir.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetMood
{
    Sad = 0,
    Hungry = 1,
    Sleepy = 2,
    Dirty = 3,
    Neutral = 4,
    Happy = 5,
    Excited = 6
}
