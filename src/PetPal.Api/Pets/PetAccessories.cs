using PetPal.Api.Common;
using PetPal.Api.Entities;
using PetPal.Shared.Dtos.Pets;

namespace PetPal.Api.Pets;

/// <summary>
/// Pet-in kosmetik əşyaları — "pet nə qədər xoşbəxtdirsə, o qədər çox şey açılır"
/// dövrəsi.
///
/// Əşya bir dəfə açıldıqdan sonra <see cref="Pet.UnlockedAccessories"/> siyahısında
/// həmişəlik qalır. Bu qəsdən belədir: xoşbəxtlik vaxt keçdikcə özü azalır və
/// qazanılmış əşyanı geri almaq uşaq üçün cəza kimi görünərdi.
///
/// Kataloq yalnız <c>PetAvatar</c> komponentinin çəkə bildiyi əşyalardan ibarətdir —
/// açılan, amma görünməyən əşya boş vəd olardı.
/// </summary>
/// <summary>
/// Əşyanın necə açıldığı.
///
/// <para>Ayrım vacibdir: <see cref="Progression"/> əşyaları YAŞ və XOŞBƏXTLİK
/// həddi ilə ÖZLƏRİ açılır (<see cref="PetAccessories.UnlockEarned"/> hər
/// oxunuşda yoxlayır). <see cref="Experience"/> əşyaları isə yalnız konkret bir
/// macəra tamamlananda verilir — onları da eyni qiymətləndiriciyə salsaydıq,
/// aşağı səviyyə şərti ilə HƏR pet-ə səssizcə düşərdi və macəranın mükafatı
/// mənasını itirərdi.</para>
/// </summary>
public enum AccessoryUnlock
{
    /// <summary>Yaş + xoşbəxtlik həddi ilə özü açılır.</summary>
    Progression = 0,

    /// <summary>Yalnız təcrübə mükafatı kimi verilir.</summary>
    Experience = 1
}

public sealed record PetAccessory(
    string Code,
    string Name,
    string NameAz,
    string Icon,
    int MinLevel,
    int MinHappiness,
    AccessoryUnlock Unlock = AccessoryUnlock.Progression)
{
    /// <summary>
    /// Yalnız inkişaf əşyaları üçün doğrudur. Təcrübə əşyası şərtini nə qədər
    /// ödəsə də özü açılmır — onu yalnız
    /// <see cref="PetAccessories.GrantFromExperience"/> verə bilər.
    /// </summary>
    public bool IsEarnedBy(Pet pet) =>
        Unlock == AccessoryUnlock.Progression && pet.Level >= MinLevel && pet.Happiness >= MinHappiness;
}

public static class PetAccessories
{
    public static IReadOnlyList<PetAccessory> Catalog { get; } =
    [
        new("collar-classic", "Classic collar", "Klassik boyunbağı", "🔗", MinLevel: 2, MinHappiness: 0),
        new("bow-red", "Red bow", "Qırmızı bant", "🎀", MinLevel: 3, MinHappiness: 90),
        new("glasses-round", "Round glasses", "Dəyirmi eynək", "👓", MinLevel: 6, MinHappiness: 0),
        new("scarf-mint", "Green scarf", "Yaşıl şərf", "🧣", MinLevel: 8, MinHappiness: 95),
        new("crown-gold", "Golden crown", "Qızıl tac", "👑", MinLevel: 12, MinHappiness: 0),

        // ---- Macəra mükafatları (Pet Brain) ----
        // Yaş/xoşbəxtlik şərti QƏSDƏN sıfırdır: bu əşyalar ümumiyyətlə
        // qiymətləndirilmir. Yeganə yol macərəni tamamlamaqdır.
        new("helmet-mars", "Mars helmet", "Mars dəbilqəsi", "🪐",
            MinLevel: 0, MinHappiness: 0, Unlock: AccessoryUnlock.Experience),
        new("wings-rainbow", "Rainbow wings", "Göy qurşağı qanadları", "🌈",
            MinLevel: 0, MinHappiness: 0, Unlock: AccessoryUnlock.Experience),

        new("halo-guardian", "Guardian halo", "Qoruyucu haləsi", "🛡️",
            MinLevel: 0, MinHappiness: 0, Unlock: AccessoryUnlock.Experience),
        new("visor-explorer", "Explorer visor", "Tədqiqatçı eynəyi", "🔭",
            MinLevel: 0, MinHappiness: 0, Unlock: AccessoryUnlock.Experience),
        new("badge-robot-friend", "Robot friend badge", "Robot dostu nişanı", "🤖",
            MinLevel: 0, MinHappiness: 0, Unlock: AccessoryUnlock.Experience),
    ];

    /// <summary>
    /// Şərtini ödəmiş, amma hələ açılmamış əşyaları pet-ə əlavə edir.
    /// Yeni açılanların kodlarını qaytarır — ekran "yeni əşya!" anını göstərsin deyə.
    /// Yeni əşya həm də dərhal taxılır: mükafat pet-in üstündə görünməlidir,
    /// uşaq bəyənmirsə şkafdan çıxara bilər.
    /// </summary>
    public static List<string> UnlockEarned(Pet pet)
    {
        List<string> unlocked = [];

        // Yumurtaya boyunbağı taxmaq olmaz — əşyalar yalnız açılışdan sonra gəlir.
        if (pet.HatchedAt is null)
            return unlocked;

        foreach (var accessory in Catalog)
        {
            if (pet.UnlockedAccessories.Contains(accessory.Code) || !accessory.IsEarnedBy(pet))
                continue;

            pet.UnlockedAccessories.Add(accessory.Code);
            pet.EquippedAccessories.Add(accessory.Code);
            unlocked.Add(accessory.Code);
        }

        return unlocked;
    }

    /// <summary>
    /// Macəra mükafatı kimi əşya verir. İkinci dəfə çağırılanda heç nə etmir və
    /// <c>false</c> qaytarır — təkrar oynanış eyni kosmetiki yenidən "açmır".
    /// </summary>
    /// <returns>Əşya MƏHZ İNDİ açıldısa <c>true</c>.</returns>
    public static bool GrantFromExperience(Pet pet, string code)
    {
        // Yumurtaya əşya taxılmır — mövcud qayda ilə eyni.
        if (pet.HatchedAt is null || string.IsNullOrWhiteSpace(code))
            return false;

        var accessory = Catalog.FirstOrDefault(a =>
            string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase)
            && a.Unlock == AccessoryUnlock.Experience);

        if (accessory is null || pet.UnlockedAccessories.Contains(accessory.Code))
            return false;

        pet.UnlockedAccessories.Add(accessory.Code);

        // Yeni əşya dərhal taxılır: mükafat pet-in üstündə görünməlidir.
        if (!pet.EquippedAccessories.Contains(accessory.Code))
            pet.EquippedAccessories.Add(accessory.Code);

        return true;
    }

    /// <summary>Kodu kataloqdan tapır — mükafat mesajında ad və ikon lazımdır.</summary>
    public static PetAccessory? Find(string code) =>
        Catalog.FirstOrDefault(a => string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Uşağın seçdiyi görünüşü yazır. Yalnız açılmış əşyalar taxıla bilər;
    /// naməlum və ya kilidli kod varsa heç nə dəyişmir və <c>false</c> qayıdır.
    /// </summary>
    public static bool TryEquip(Pet pet, IEnumerable<string> codes)
    {
        List<string> equipped = [];

        foreach (var code in codes)
        {
            var accessory = Catalog.FirstOrDefault(a =>
                string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase));

            if (accessory is null || !pet.UnlockedAccessories.Contains(accessory.Code))
                return false;

            if (!equipped.Contains(accessory.Code))
                equipped.Add(accessory.Code);
        }

        pet.EquippedAccessories = equipped;
        return true;
    }

    /// <summary>Bütün kataloq — açılmışlar, taxılmışlar və hələ kilidlilər birlikdə.</summary>
    public static List<PetAccessoryDto> Describe(Pet pet, string language) =>
        [.. Catalog.Select(a => new PetAccessoryDto
        {
            Code = a.Code,
            Name = IsAzerbaijani(language) ? a.NameAz : a.Name,
            Icon = a.Icon,
            IsUnlocked = pet.UnlockedAccessories.Contains(a.Code),
            IsEquipped = pet.EquippedAccessories.Contains(a.Code),
            Requirement = RequirementText(a, language)
        })];

    public static List<PetAccessoryDto> Describe(IEnumerable<string> codes, string language) =>
        [.. Catalog.Select(a => new PetAccessoryDto
        {
            Code = a.Code,
            Name = IsAzerbaijani(language) ? a.NameAz : a.Name,
            Icon = a.Icon,
            IsUnlocked = codes.Contains(a.Code),
            Requirement = RequirementText(a, language)
        })];

    private static string RequirementText(PetAccessory accessory, string language)
    {
        var az = IsAzerbaijani(language);

        // Macəra əşyasının şərti yaş deyil — uşağa doğru yol göstərilməlidir,
        // yoxsa şkafda "6 yaş" yazılır və əşya yaş gələndə də açılmır.
        if (accessory.Unlock == AccessoryUnlock.Experience)
            return az ? "Macəra mükafatı" : "Adventure reward";

        // Şərt uşağa YAŞLA deyilir: səviyyə ilə yaş eyni rəqəmdir, amma
        // uşaq "6 yaş" ifadəsini oxumadan da tanıyır.
        var level = az ? $"{accessory.MinLevel} yaş" : $"Age {accessory.MinLevel}";
        if (accessory.MinHappiness <= 0)
            return level;

        var happiness = az
            ? $"xoşbəxtlik {accessory.MinHappiness}"
            : $"{accessory.MinHappiness} happiness";

        return az ? $"{level} + {happiness}" : $"{level} + {happiness}";
    }

    private static bool IsAzerbaijani(string language) =>
        Localized.Normalize(language) == Localized.Azerbaijani;
}
