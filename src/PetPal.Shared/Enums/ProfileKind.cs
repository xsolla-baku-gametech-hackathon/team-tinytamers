using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>Hesab tipi — valideyn hesabı və ona bağlı uşaq profilləri.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProfileKind
{
    Parent = 0,
    Child = 1
}
