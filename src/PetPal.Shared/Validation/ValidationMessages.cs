using System.Globalization;

namespace PetPal.Shared.Validation;

/// <summary>
/// Forma yoxlamalarının (DataAnnotations) iki dilli mətnləri.
///
/// Atributlar yalnız SABİT mətn qəbul edir, ona görə mesaj birbaşa yazıla
/// bilməz. DataAnnotations-un öz yolu işlədilir: <c>ErrorMessageResourceType</c>
/// + <c>ErrorMessageResourceName</c> — atribut həmin adda ictimai statik
/// xassəni refleksiya ilə oxuyur. Bu, adətən .resx faylının generasiya etdiyi
/// sinifdir; burada isə sinif əl ilə yazılıb, çünki dil ikidir və hər mətnin
/// tərcüməsi elə yanında dursun istəyirik (bax UI-dakı <c>Loc</c>).
///
/// Dili <see cref="CultureInfo.CurrentUICulture"/> həll edir:
///   • serverdə — <c>UseRequestLocalization</c> (Accept-Language başlığı),
///   • app-də   — <c>Loc</c> dili tətbiq edəndə mədəniyyəti də qurur.
/// </summary>
public static class ValidationMessages
{
    public static string EmailRequired => T("E-poçt vacibdir.", "Email is required.");
    public static string EmailFormat => T("E-poçt formatı düzgün deyil.", "That email address is not valid.");
    public static string PasswordRequired => T("Şifrə vacibdir.", "Password is required.");
    public static string PasswordLength => T("Şifrə ən azı 8 simvol olmalıdır.", "The password must be at least 8 characters.");

    public static string NameRequired => T("Ad vacibdir.", "Name is required.");
    public static string NameLength60 => T("Ad 2–60 simvol olmalıdır.", "The name must be 2–60 characters.");
    public static string NameLength40 => T("Ad 2–40 simvol olmalıdır.", "The name must be 2–40 characters.");

    public static string ChildNameRequired => T("Uşağın adı vacibdir.", "The child's name is required.");
    public static string AgeRange => T("Yaş 3–16 aralığında olmalıdır.", "Age must be between 3 and 16.");
    public static string TimeZoneOffset => T("Saat qurşağı fərqi düzgün deyil.", "That time-zone offset is not valid.");
    public static string LanguageChoice => T("Dil yalnız az və ya en ola bilər.", "The language can only be az or en.");

    public static string PinRequired => T("PIN vacibdir.", "A PIN is required.");
    public static string PinFormat => T("PIN 4 rəqəmdən ibarət olmalıdır.", "The PIN must be 4 digits.");

    public static string PetNameRequired => T("Pet adı vacibdir.", "The pet's name is required.");
    public static string PetNameLength => T("Pet adı 2–24 simvol olmalıdır.", "The pet's name must be 2–24 characters.");
    public static string SpeciesRequired => T("Növ vacibdir.", "A kind is required.");
    public static string SpeciesUnknown => T("Belə növ yoxdur.", "There is no such kind.");

    public static string DiscoveryLabelRequired => T("Kəşfin adı vacibdir.", "The discovery needs a name.");

    public static string ChatMessageRequired => T("Əvvəlcə nəsə yaz.", "Type something first.");
    public static string ChatMessageLength => T("Mesaj 1–200 simvol olmalıdır.", "The message must be 1–200 characters.");

    public static string GameKeyRequired => T("Oyun açarı vacibdir.", "The game key is required.");
    public static string ScoreRange => T("Nəticə 0–100 aralığında olmalıdır.", "The score must be between 0 and 100.");
    public static string DurationRange => T("Müddət düzgün deyil.", "That duration is not valid.");

    public static string QuestionCountRange => T("Sual sayı 1–10 aralığında olmalıdır.", "The number of questions must be between 1 and 10.");
    public static string AnswerChoiceRange => T("Cavab variantı düzgün deyil.", "That answer choice is not valid.");

    public static string DailyGoalRange => T("Gündəlik hədəf 1–50 tapşırıq aralığında olmalıdır.", "The daily goal must be between 1 and 50 tasks.");
    public static string ScreenLimitRange => T("Gündəlik ekran limiti 5–240 dəqiqə aralığında olmalıdır.", "The daily screen limit must be between 5 and 240 minutes.");
    public static string HourRange => T("Saat 0–23 aralığında olmalıdır.", "The hour must be between 0 and 23.");

    public static string FriendCodeRequired => T("Dost kodu vacibdir.", "A friend code is required.");
    public static string FriendCodeFormat => T("Dost kodu 6 simvoldan ibarət olmalıdır.", "The friend code must be 6 characters.");

    private static string T(string az, string en) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase)
            ? en
            : az;
}
