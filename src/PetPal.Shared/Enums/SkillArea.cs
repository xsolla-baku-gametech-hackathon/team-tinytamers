using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>Uşağın öyrəndiyi bacarıq sahələri — adaptiv tapşırıqlar bu ox üzrə seçilir.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SkillArea
{
    Math = 0,
    Vocabulary = 1,
    Logic = 2,
    Reading = 3,
    Science = 4
}
