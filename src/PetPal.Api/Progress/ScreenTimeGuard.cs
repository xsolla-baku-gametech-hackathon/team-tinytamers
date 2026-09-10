using PetPal.Api.Common;
using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.Progress;

/// <summary>
/// Ekran vaxtı qaydalarının saf hissəsi — I/O yoxdur, birbaşa test olunur.
///
/// İki qayda var:
///  1. Yuxu rejimi — uşağın YERLİ saatına görə hesablanır (server UTC-dədir).
///  2. Gündəlik dəqiqə limiti.
///
/// Limit yalnız YENİ fəaliyyət başlatmağı bloklayır. Başlanmış sessiyanı
/// yarımçıq kəsmək uşaq üçün ədalətsiz olardı və qazanılmış mükafatı itirərdi.
/// </summary>
public static class ScreenTimeGuard
{
    /// <param name="enforced">
    /// <c>false</c> olduqda qaydalar hesablanmır və nəticə həmişə
    /// <see cref="ScreenTimeState.Allowed"/> olur — bax <see cref="ScreenTimeOptions"/>.
    /// </param>
    public static ScreenTimeState Evaluate(ChildProfile child, DailyGoal goal, DateTime utcNow, bool enforced = true)
    {
        if (!enforced)
            return ScreenTimeState.Allowed;

        var localNow = utcNow.AddMinutes(child.UtcOffsetMinutes);

        if (IsWithinBedtime(localNow.Hour, child.BedtimeStartHour, child.BedtimeEndHour))
            return ScreenTimeState.Bedtime;

        if (child.DailyMinutesLimit > 0 && goal.MinutesSpent >= child.DailyMinutesLimit)
            return ScreenTimeState.LimitReached;

        return ScreenTimeState.Allowed;
    }

    /// <summary>
    /// Yuxu pəncərəsi gecə yarısını keçə bilər (məs. 21:00 → 07:00),
    /// ona görə sadə "start ≤ saat &lt; end" müqayisəsi kifayət etmir.
    /// </summary>
    public static bool IsWithinBedtime(int hour, int startHour, int endHour)
    {
        if (startHour == endHour)
            return false;

        return startHour < endHour
            ? hour >= startHour && hour < endHour
            : hour >= startHour || hour < endHour;
    }

    public static string MessageFor(ScreenTimeState state, string language, ChildProfile child)
    {
        var az = Localized.Normalize(language) == Localized.Azerbaijani;

        return state switch
        {
            ScreenTimeState.Bedtime => az
                ? $"Yuxu vaxtıdır 🌙 Pet-in də yatıb. Saat {child.BedtimeEndHour}:00-dan sonra görüşərik!"
                : $"It's bedtime 🌙 Your pet is asleep too. See you after {child.BedtimeEndHour}:00!",
            ScreenTimeState.LimitReached => az
                ? $"Bu günlük {child.DailyMinutesLimit} dəqiqə tamamlandı 🎉 Sabah yenə gözləyirəm!"
                : $"You've used today's {child.DailyMinutesLimit} minutes 🎉 See you tomorrow!",
            _ => string.Empty
        };
    }
}
