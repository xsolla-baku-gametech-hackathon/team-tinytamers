using PetPal.Api.Common;
using PetPal.Api.Entities;
using PetPal.Api.Pets;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>Salamlamanın haradan gəldiyi — nümayiş panelində dürüstlük üçün.</summary>
public enum GreetingSource
{
    /// <summary>Pet hələ yumurtadadır.</summary>
    Egg = 0,

    /// <summary>Qulluq ehtiyacı hər şeydən üstündür.</summary>
    CareUrgency = 1,

    /// <summary>Yaddaşdan gələn şəxsi salamlama.</summary>
    Memory = 2,

    /// <summary>Adi qayda əsaslı replika.</summary>
    Idle = 3
}

public sealed record GreetingResult(string Text, GreetingSource Source, PetMemory? UsedMemory);

/// <summary>
/// Pet Brain salamlaması — saf funksiya, I/O yoxdur.
///
/// <para>Prioritet sırası qəsdəndir və pozulmamalıdır:</para>
/// <list type="number">
///   <item><b>Yumurta</b> — çıxmamış pet nə danışır, nə də macəraya çıxır.</item>
///   <item><b>Qulluq ehtiyacı</b> — pet acdırsa, yorğundursa və ya çirklidirsə,
///   xatirə cümləsi bu ehtiyacı ÖRTMÜR. Uşaq əvvəlcə ona baxmalıdır.</item>
///   <item><b>Yaddaş</b> — sağlam və çıxmış pet birlikdə yaşadıqları bir anı
///   xatırlayır.</item>
///   <item><b>Adi replika</b> — yaddaş boşdursa mövcud <see cref="PetVoice"/>.</item>
/// </list>
/// </summary>
public static class PetBrainGreeting
{
    /// <summary>Bu əhvallar "indi mənə bax" deməkdir və xatirədən üstündür.</summary>
    private static readonly PetMood[] UrgentMoods =
        [PetMood.Hungry, PetMood.Sleepy, PetMood.Dirty, PetMood.Sad];

    public static GreetingResult Build(
        Pet pet,
        string childName,
        string language,
        IEnumerable<PetMemory> memories,
        DateTime now)
    {
        if (pet.HatchedAt is null)
            return new GreetingResult(PetVoice.StillAnEgg(language), GreetingSource.Egg, null);

        var mood = PetProgression.MoodFor(pet);

        if (UrgentMoods.Contains(mood))
            return new GreetingResult(
                PetVoice.Idle(language, mood, childName, pet.Name),
                GreetingSource.CareUrgency,
                null);

        var memory = MemoryPolicy.PickForGreeting(memories, now);

        if (memory is null)
            return new GreetingResult(
                PetVoice.Idle(language, mood, childName, pet.Name),
                GreetingSource.Idle,
                null);

        var recalled = MemoryPolicy.Render(memory, language, pet.Name);

        // Xatirə cümləsinin qarşısına qısa bir körpü qoyulur ki, replika
        // "arxivdən oxunmuş" kimi yox, danışıq kimi səslənsin.
        var opener = Localized.T(language,
            $"{childName}, yadındadır?",
            $"{childName}, do you remember?");

        return new GreetingResult($"{opener} {recalled}", GreetingSource.Memory, memory);
    }
}
