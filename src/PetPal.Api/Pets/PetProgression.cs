using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.Pets;

/// <summary>
/// Pet-in inkişaf və qulluq qaydaları. Bütün riyaziyyat burada saxlanılır ki,
/// balans dəyişikliyi tək yerdən edilsin və test olunsun.
/// </summary>
public static class PetProgression
{
    /// <summary>Statların tam 100-dən 0-a düşməsi təxminən 2 gün çəkir (30 dəq = 1 bal).</summary>
    private const int DecayMinutesPerPoint = 30;

    /// <summary>
    /// Növbəti yaşa lazım olan XP. Əyri qəsdən sürətlənir: uşaq ilk yaşları
    /// tez keçir (ilk səviyyə bir dərs dəstidir), sonra hər yaş əvvəlkindən
    /// nəzərəçarpacaq dərəcədə gec gəlir — janr konvensiyası budur və pet-in
    /// böyüməsi "bir günlük iş" olmamalıdır.
    ///
    /// 1 → 100, 2 → 165, 3 → 250, 5 → 480, 10 → 1405, 20 → 4755 XP.
    /// </summary>
    public static int XpToNextLevel(int level)
    {
        var step = Math.Max(0, level - 1);
        return 100 + (55 * step) + (10 * step * step);
    }

    /// <summary>Pet-in yaşı = səviyyəsi. Ekranlarda yaş göstərilir, səviyyə yox.</summary>
    public static int AgeFor(Pet pet) => Math.Max(1, pet.Level);

    /// <summary>Yumurtanı açmağın ulduz qiyməti.</summary>
    public const int HatchStarCost = 50;

    /// <summary>
    /// Yaş aralığı → mərhələ. Sərhədlər XP əyrisi ilə birlikdə seçilib: hər
    /// mərhələ əvvəlkindən uzun sürür, ona görə görünüş dəyişikliyi mükafat
    /// kimi hiss olunur.
    /// </summary>
    public static PetStage StageFor(int level) => level switch
    {
        <= 2 => PetStage.Newborn,
        <= 5 => PetStage.Baby,
        <= 9 => PetStage.Child,
        <= 14 => PetStage.Teen,
        <= 21 => PetStage.Adult,
        _ => PetStage.Elder
    };

    /// <summary>
    /// Mərhələ yalnız səviyyədən deyil, yumurtadan çıxıb-çıxmamasından da asılıdır.
    /// Uşaq yumurtanı ulduzla açmayana qədər pet Egg qalır — səviyyəsi nə olursa olsun.
    /// </summary>
    public static PetStage StageFor(Pet pet) =>
        pet.HatchedAt is null ? PetStage.Egg : StageFor(pet.Level);

    /// <summary>XP əlavə edir və lazım gələrsə səviyyə(lər) qaldırır. Səviyyə artıbsa <c>true</c> qaytarır.</summary>
    public static bool AddXp(Pet pet, int xp)
    {
        if (xp <= 0)
            return false;

        pet.Xp += xp;
        var leveledUp = false;

        while (pet.Xp >= XpToNextLevel(pet.Level))
        {
            pet.Xp -= XpToNextLevel(pet.Level);
            pet.Level++;
            leveledUp = true;

            // Səviyyə artımı pet-i də "dincəldir" — uşaq dərhal cəzalandırılmış hiss etməsin.
            pet.Happiness = Clamp(pet.Happiness + 15);
            pet.Energy = Clamp(pet.Energy + 10);
        }

        return leveledUp;
    }

    /// <summary>
    /// Pet-in vəziyyətini oxunuş anına gətirir: statları azaldır və şərtini ödəmiş
    /// kosmetik əşyaları açır. Bütün oxu yolları bunu çağırır ki, əşya harada
    /// qazanılmasından asılı olmayaraq (dərs, oyun, qulluq) dərhal görünsün.
    /// </summary>
    /// <returns>Bu çağırışla təzəcə açılan əşyaların kodları.</returns>
    public static List<string> Refresh(Pet pet, DateTime now)
    {
        ApplyDecay(pet, now);
        return PetAccessories.UnlockEarned(pet);
    }

    /// <summary>
    /// Statların vaxta görə azalmasını tətbiq edir. Oxunuş anında çağırılır (lazy decay) —
    /// arxa planda işləyən job saxlamağa ehtiyac qalmır.
    /// </summary>
    public static void ApplyDecay(Pet pet, DateTime now)
    {
        var minutes = (int)(now - pet.LastDecayAt).TotalMinutes;
        if (minutes < DecayMinutesPerPoint)
            return;

        var points = minutes / DecayMinutesPerPoint;

        pet.Fullness = Clamp(pet.Fullness - points);
        pet.Cleanliness = Clamp(pet.Cleanliness - points / 2);
        pet.Happiness = Clamp(pet.Happiness - points / 2);
        pet.Energy = Clamp(pet.Energy - points / 3);

        pet.LastDecayAt = pet.LastDecayAt.AddMinutes(points * DecayMinutesPerPoint);
    }

    public static PetMood MoodFor(Pet pet)
    {
        if (pet.Fullness < 25) return PetMood.Hungry;
        if (pet.Energy < 25) return PetMood.Sleepy;
        if (pet.Cleanliness < 25) return PetMood.Dirty;
        if (pet.Happiness < 30) return PetMood.Sad;
        if (pet.Happiness >= 85) return PetMood.Excited;
        if (pet.Happiness >= 60) return PetMood.Happy;
        return PetMood.Neutral;
    }

    public static int Clamp(int value) => Math.Clamp(value, 0, 100);
}
