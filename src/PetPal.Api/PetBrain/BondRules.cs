using PetPal.Api.Entities;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Bağın qaydaları — saf funksiyalar.
///
/// <para><b>Bağ xoşbəxtlik deyil.</b> Xoşbəxtlik qulluq statıdır: vaxta görə
/// azalır, yem və oyunla qalxır. Bağ isə uşaqla pet arasındakı münasibətin
/// tarixçəsidir — yavaş artır və <b>HEÇ VAXT AZALMIR</b>.</para>
///
/// <para>Bu, məhsul qərarıdır: səhv cavaba, uğursuz tapmacaya, buraxılan günə
/// görə bağı endirmək uşağa "pet məndən küsdü" hissi verərdi. Uşaq app-ində
/// itki hissi ilə geri qaytarma üsulu istifadə olunmur.</para>
/// </summary>
public static class BondRules
{
    public const int Min = 0;
    public const int Max = 100;

    /// <summary>İlk BÖYÜK birgə macəra — bir dəfəlik.</summary>
    public const int FirstAdventureBonus = 8;

    /// <summary>Bir qulluq əməliyyatının verdiyi bağ.</summary>
    public const int CareGain = 1;

    /// <summary>Bir gündə qulluqdan yığıla bilən ƏN ÇOX bağ — sonsuz təkrarın qarşısını alır.</summary>
    public const int DailyCareCap = 3;

    public static int Clamp(int value) => Math.Clamp(value, Min, Max);

    /// <summary>
    /// Təcrübə tamamlandı. Şablonun öz dəyəri (+5…+8) verilir; ilk böyük macəra
    /// isə ayrıca bonus alır.
    /// </summary>
    public static int ForCompletion(ExperienceTemplate template, bool isFirstEverAdventure) =>
        template.BondReward + (isFirstEverAdventure ? FirstAdventureBonus : 0);

    /// <summary>
    /// Bağı artırır və NEÇƏ bal artdığını qaytarır. Mənfi dəyər qəbul edilmir —
    /// bağın azalması ümumiyyətlə mümkün olmamalıdır.
    /// </summary>
    public static int Grant(Pet pet, int amount)
    {
        if (amount <= 0)
            return 0;

        var before = Clamp(pet.Bond);
        pet.Bond = Clamp(before + amount);

        return pet.Bond - before;
    }

    /// <summary>Gündəlik qulluq limitinə görə verilə bilən bağ.</summary>
    public static int ForCare(int bondFromCareToday) =>
        bondFromCareToday >= DailyCareCap ? 0 : CareGain;
}
