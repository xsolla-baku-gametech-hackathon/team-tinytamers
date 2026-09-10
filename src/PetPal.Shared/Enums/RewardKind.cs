using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>Mükafat valyutası. Star gündəlik axında qazanılır, Gem nadir/böyük nailiyyətlərdə verilir.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RewardKind
{
    Star = 0,
    Gem = 1
}
