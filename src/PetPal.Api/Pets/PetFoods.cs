using PetPal.Api.Common;
using PetPal.Shared.Dtos.Pets;

namespace PetPal.Api.Pets;

/// <summary>
/// Yemək kataloqu. Tək bir "yem" düyməsi əvəzinə uşaq seçim edir və seçimin
/// qiyməti var: ucuz yemlər az doydurur, şirniyyat isə çox sevindirir, amma
/// pet-i bulaşdırır — yəni sonra çimizdirmə lazım gəlir.
///
/// Qiymətlər qəsdən kiçikdir (2–9 ulduz): xərc seçimi mənalı etməlidir, öyrənmə
/// axınını dayandırmamalı.
/// </summary>
public sealed record PetFood(
    string Code,
    string Name,
    string NameAz,
    string Icon,
    int StarCost,
    int Fullness,
    int Happiness,
    int Energy = 0,
    int Cleanliness = 0)
{
    /// <summary>Təmizliyi azaldan yeməklər şirniyyat sayılır — ekranda ayrıca işarələnir.</summary>
    public bool IsTreat => Cleanliness < 0;
}

public static class PetFoods
{
    /// <summary>
    /// Yemək kodu göndərilməyəndə seçilən porsiya. Köhnə klientlər (və sadə
    /// "yedizdir" çağırışları) əvvəlki kimi 5 ulduza 30 toxluq alır.
    /// </summary>
    public const string DefaultCode = "meat";

    public static IReadOnlyList<PetFood> Catalog { get; } =
    [
        new("carrot", "Carrot", "Kök", "🥕", StarCost: 2, Fullness: 12, Happiness: 2),
        new("apple", "Apple", "Alma", "🍎", StarCost: 3, Fullness: 16, Happiness: 4),
        new("milk", "Milk", "Süd", "🥛", StarCost: 3, Fullness: 14, Happiness: 3, Energy: 6),
        new("berry", "Strawberry", "Çiyələk", "🍓", StarCost: 4, Fullness: 18, Happiness: 9),
        new("cookie", "Cookie", "Peçenye", "🍪", StarCost: 4, Fullness: 20, Happiness: 12, Cleanliness: -4),
        new(DefaultCode, "Meat", "Ət", "🍖", StarCost: 5, Fullness: 30, Happiness: 5),
        new("fish", "Fish", "Balıq", "🐟", StarCost: 6, Fullness: 34, Happiness: 6, Energy: 4),
        new("cake", "Cake", "Tort", "🍰", StarCost: 9, Fullness: 38, Happiness: 20, Cleanliness: -8),
    ];

    public static PetFood Default { get; } = Catalog.First(f => f.Code == DefaultCode);

    /// <summary>Ən ucuz porsiya — dock düyməsindəki "⭐2+" nişanı üçün.</summary>
    public static int CheapestCost => Catalog.Min(f => f.StarCost);

    /// <summary>
    /// Kodu kataloqda tapır. Boş kod standart yeməyə düşür; naməlum kod isə
    /// <c>null</c> qaytarır — səhvən yazılmış kod sükutla başqa yeməyə çevrilməməlidir.
    /// </summary>
    public static PetFood? Resolve(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? Default
            : Catalog.FirstOrDefault(f => string.Equals(f.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    public static List<PetFoodDto> Describe(string language) =>
        [.. Catalog.Select(f => new PetFoodDto
        {
            Code = f.Code,
            Name = IsAzerbaijani(language) ? f.NameAz : f.Name,
            Icon = f.Icon,
            StarCost = f.StarCost,
            Effect = EffectText(f, language),
            IsTreat = f.IsTreat
        })];

    public static string NameOf(PetFood food, string language) =>
        IsAzerbaijani(language) ? food.NameAz : food.Name;

    /// <summary>"Toxluq +30 · Sevinc +5 · Təmizlik −4" — rəqəmlər gizlədilmir,
    /// uşaq seçimin nəticəsini əvvəlcədən görməlidir.</summary>
    private static string EffectText(PetFood food, string language)
    {
        var az = IsAzerbaijani(language);

        List<string> parts =
        [
            $"{(az ? "Toxluq" : "Fullness")} {Signed(food.Fullness)}",
            $"{(az ? "Sevinc" : "Joy")} {Signed(food.Happiness)}"
        ];

        if (food.Energy != 0)
            parts.Add($"{(az ? "Enerji" : "Energy")} {Signed(food.Energy)}");

        if (food.Cleanliness != 0)
            parts.Add($"{(az ? "Təmizlik" : "Clean")} {Signed(food.Cleanliness)}");

        return string.Join(" · ", parts);
    }

    /// <summary>Mənfi işarə üçün U+2212 — defis rəqəmdən kiçik görünür və uşaq üçün itir.</summary>
    private static string Signed(int value) => value >= 0 ? $"+{value}" : $"−{Math.Abs(value)}";

    private static bool IsAzerbaijani(string language) =>
        Localized.Normalize(language) == Localized.Azerbaijani;
}
