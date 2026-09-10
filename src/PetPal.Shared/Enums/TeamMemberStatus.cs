using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>
/// Komanda missiyasında üzvlüyün vəziyyəti. Missiyanı başladan avtomatik
/// <see cref="Joined"/> olur; dəvət edilənlər isə qəbul edənə qədər
/// <see cref="Invited"/> qalır və töhfələri sayılmır.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TeamMemberStatus
{
    /// <summary>Dəvət göndərilib, cavab gözlənilir.</summary>
    Invited = 0,

    /// <summary>Qoşulub — töhfəsi sayılır və mükafat alır.</summary>
    Joined = 1,

    /// <summary>İmtina edib — missiya siyahısında görünmür.</summary>
    Declined = 2
}
