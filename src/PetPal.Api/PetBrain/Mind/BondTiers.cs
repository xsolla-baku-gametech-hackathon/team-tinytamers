using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Mind;

/// <summary>Bir pillənin AÇDIĞI görünən şey — hamısı təsdiqlənmiş açarlardır.</summary>
/// <param name="Emote">Pet-in yeni emosiya işarəsi.</param>
/// <param name="GreetingVariant">Salamlamanın hansı variantı açıqdır.</param>
/// <param name="RoomDecor">Otağın kiçik bəzəyi.</param>
/// <param name="AdventureReaction">Macərada pet-in reaksiya açarı.</param>
/// <param name="Pose">Kiçik poza/animasiya açarı.</param>
public sealed record BondUnlock(
    string Emote,
    string GreetingVariant,
    string RoomDecor,
    string AdventureReaction,
    string Pose);

/// <summary>
/// Bağın pillələri.
///
/// <para>Bağ əvvəllər yalnız faiz barı idi — uşaq üçün rəqəmin 41-dən 44-ə
/// qalxması heç nə demirdi. Pillə isə GÖRÜNƏN bir hadisədir: yeni emote, yeni
/// salamlama, otaqda yeni detal.</para>
///
/// <para>Pillə HEÇ VAXT geri düşmür. Bağın özü azalmır (bax
/// <see cref="BondRules"/>), ona görə "pilləni itirmək" qorxusu da yoxdur —
/// uşağı geri qaytarmaq üçün itki mexanikası bu məhsulda istifadə olunmur.</para>
/// </summary>
public static class BondTiers
{
    public static PetBrainBondTier Of(int bond) => BondRules.Clamp(bond) switch
    {
        >= 100 => PetBrainBondTier.LifelongTeam,
        >= 75 => PetBrainBondTier.BestCompanion,
        >= 50 => PetBrainBondTier.AdventurePartner,
        >= 25 => PetBrainBondTier.TrustedFriend,
        _ => PetBrainBondTier.NewFriend
    };

    /// <summary>Pillənin başladığı bal — irəliləmə barı bunu göstərir.</summary>
    public static int StartOf(PetBrainBondTier tier) => tier switch
    {
        PetBrainBondTier.LifelongTeam => 100,
        PetBrainBondTier.BestCompanion => 75,
        PetBrainBondTier.AdventurePartner => 50,
        PetBrainBondTier.TrustedFriend => 25,
        _ => 0
    };

    /// <summary>Növbəti pilləyə çatmaq üçün lazım olan bal; sonuncuda <c>null</c>.</summary>
    public static int? NextThreshold(PetBrainBondTier tier) => tier switch
    {
        PetBrainBondTier.NewFriend => 25,
        PetBrainBondTier.TrustedFriend => 50,
        PetBrainBondTier.AdventurePartner => 75,
        PetBrainBondTier.BestCompanion => 100,
        _ => null
    };

    public static string Label(PetBrainBondTier tier, string language) => tier switch
    {
        PetBrainBondTier.TrustedFriend => Localized.T(language, "Etibarlı Dost", "Trusted Friend"),
        PetBrainBondTier.AdventurePartner => Localized.T(language, "Macəra Yoldaşı", "Adventure Partner"),
        PetBrainBondTier.BestCompanion => Localized.T(language, "Ən Yaxın Dost", "Best Companion"),
        PetBrainBondTier.LifelongTeam => Localized.T(language, "Ömürlük Komanda", "Lifelong Team"),
        _ => Localized.T(language, "Yeni Dost", "New Friend")
    };

    /// <summary>Pillə açılanda uşağa deyilən cümlə — vəd verilir, tələb olunmur.</summary>
    public static string UnlockLine(PetBrainBondTier tier, string language, string petName) => tier switch
    {
        PetBrainBondTier.TrustedFriend => Localized.T(language,
            $"{petName} artıq sənə etibar edir — indi səni fərqli qarşılayacaq.",
            $"{petName} trusts you now — you will be greeted differently from here."),

        PetBrainBondTier.AdventurePartner => Localized.T(language,
            $"{petName} macəra yoldaşın oldu — yolda daha çox danışacaq.",
            $"{petName} is your adventure partner now — expect more chatter on the way."),

        PetBrainBondTier.BestCompanion => Localized.T(language,
            $"{petName} ən yaxın dostundur — otağınıza yeni bir detal gəldi.",
            $"{petName} is your closest friend — a new detail arrived in your room."),

        PetBrainBondTier.LifelongTeam => Localized.T(language,
            $"Siz ömürlük komandasınız. {petName} bunu heç vaxt unutmayacaq.",
            $"You are a lifelong team. {petName} will never forget that."),

        _ => Localized.T(language,
            $"{petName} ilə dostluğunuz başladı.",
            $"Your friendship with {petName} has begun.")
    };

    /// <summary>
    /// Pillənin açdığı görünən şeylər. Açarlar QAPALIDIR — UI onları tanıyır,
    /// klient isə yenisini uydura bilmir.
    /// </summary>
    public static BondUnlock UnlockFor(PetBrainBondTier tier) => tier switch
    {
        PetBrainBondTier.TrustedFriend => new(
            Emote: "😊", GreetingVariant: "warm", RoomDecor: "",
            AdventureReaction: "cheer", Pose: "lean-in"),

        PetBrainBondTier.AdventurePartner => new(
            Emote: "🤩", GreetingVariant: "adventurous", RoomDecor: "pennant",
            AdventureReaction: "high-five", Pose: "ready-stance"),

        PetBrainBondTier.BestCompanion => new(
            Emote: "💛", GreetingVariant: "close", RoomDecor: "photo-wall",
            AdventureReaction: "proud-nod", Pose: "shoulder-lean"),

        PetBrainBondTier.LifelongTeam => new(
            Emote: "🌟", GreetingVariant: "lifelong", RoomDecor: "star-lantern",
            AdventureReaction: "team-cheer", Pose: "victory-hop"),

        _ => new(
            Emote: "🙂", GreetingVariant: "simple", RoomDecor: "",
            AdventureReaction: "smile", Pose: "idle")
    };

    /// <summary>Bu pilləyə qədər açılmış HƏR ŞEY — pillə geri düşmədiyi üçün siyahı yığılır.</summary>
    public static IReadOnlyList<PetBrainBondTier> UnlockedThrough(PetBrainBondTier tier) =>
        [.. Enum.GetValues<PetBrainBondTier>().Where(t => t <= tier)];
}
