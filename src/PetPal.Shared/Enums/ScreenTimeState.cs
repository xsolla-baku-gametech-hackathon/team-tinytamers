using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>Uşağın hazırda yeni fəaliyyət başlada bilib-bilməməsi.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScreenTimeState
{
    Allowed = 0,

    /// <summary>Yuxu rejimi pəncərəsindədir.</summary>
    Bedtime = 1,

    /// <summary>Gündəlik dəqiqə limiti dolub.</summary>
    LimitReached = 2
}
