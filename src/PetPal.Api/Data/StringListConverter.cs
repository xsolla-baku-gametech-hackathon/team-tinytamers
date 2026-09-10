using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PetPal.Api.Data;

/// <summary>
/// <c>List&lt;string&gt;</c> sahələrini JSON mətn kimi saxlayır.
/// Provider-dən asılı olmayan həll — eyni model həm PostgreSQL (produksiya),
/// həm də SQLite (testlər) üzərində işləyir.
/// </summary>
internal static class StringListConverter
{
    public static readonly ValueConverter<List<string>, string> Converter = new(
        list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
        json => string.IsNullOrWhiteSpace(json)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>());

    public static readonly ValueComparer<List<string>> Comparer = new(
        (a, b) => a != null && b != null && a.SequenceEqual(b),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        list => list.ToList());
}
