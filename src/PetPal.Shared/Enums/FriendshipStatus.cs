using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>
/// Dostluğun vəziyyəti. Dost kodu yazmaq dostluq QURMUR — yalnız sorğu yaradır;
/// dostluq qarşı tərəf özü qəbul edəndən sonra işləyir.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FriendshipStatus
{
    /// <summary>Sorğu göndərilib, qəbul edən tərəfin valideyni hələ təsdiqləməyib.</summary>
    Pending = 0,

    /// <summary>Təsdiqlənib — yalnız bu vəziyyət «dost» sayılır.</summary>
    Active = 1
}
