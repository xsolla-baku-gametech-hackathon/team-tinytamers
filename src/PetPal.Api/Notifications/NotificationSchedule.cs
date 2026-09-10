using PetPal.Api.Entities;
using PetPal.Api.Progress;

namespace PetPal.Api.Notifications;

/// <summary>
/// Bildirişin GÖNDƏRİLİB-GÖNDƏRİLMƏYƏCƏYİNİN saf qaydası — I/O yoxdur,
/// birbaşa test olunur (eyni ilə <c>ArenaMatchmaker</c> kimi).
///
/// <para>Qayda məhsul qərarıdır: app gecə ekran vaxtını bloklayır, ona görə
/// telefon bildirişi ilə həmin uşağı oyatmaq eyni vədin əksi olardı.</para>
/// </summary>
public static class NotificationSchedule
{
    /// <summary>
    /// Uşağa bildiriş göndərmək olar? Yuxu rejimində <c>false</c>.
    ///
    /// <para>Saat uşağın YERLİ vaxtına görə hesablanır: server UTC-də işləyir,
    /// yuxu vaxtı isə uşağın öz gecəsidir.</para>
    /// </summary>
    public static bool CanSendToChild(ChildProfile child, DateTime utcNow, bool respectBedtime)
    {
        if (!respectBedtime)
            return true;

        var localHour = utcNow.AddMinutes(child.UtcOffsetMinutes).Hour;
        return !ScreenTimeGuard.IsWithinBedtime(localHour, child.BedtimeStartHour, child.BedtimeEndHour);
    }

    /// <summary>
    /// Valideynə gedən bildiriş yuxu rejimindən ASILI DEYİL: qərar verməli
    /// olan odur və o, böyükdür. Metod qaydanı sənədləşdirmək üçün var.
    /// </summary>
    public static bool CanSendToParent() => true;
}
