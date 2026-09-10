using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>
/// Arena duelinin vəziyyəti. Duel SİNXRONDUR — dəst yalnız hər iki uşaq
/// qoşulandan sonra, ikisi üçün eyni anda başlayır.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DuelStatus
{
    /// <summary>Rəqib axtarılır. Bu vəziyyətdə HEÇ KİM cavab verə bilmir.</summary>
    WaitingOpponent = 0,

    /// <summary>Hər iki uşaq dəsti bitirib — nəticə hazırdır.</summary>
    Complete = 1,

    /// <summary>Rəqib vaxtında tapılmadı; duel ləğv olundu.</summary>
    Expired = 2,

    /// <summary>
    /// Rəqib tapıldı, dəst gedir. Bu vəziyyət ayrıca lazımdır: gözləyən duellər
    /// dəqiqələr içində ləğv olunur, oynanan duel isə heç vaxt ləğv olunmamalıdır.
    /// </summary>
    Live = 3
}
