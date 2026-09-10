using System.Globalization;

namespace PetPal.Launcher;

/// <summary>
/// Launcher hər şeydən ƏVVƏL açılır: nə uşaq profili var, nə də seçilmiş dil —
/// onlar app-in içindədir (bax <c>PetPal.App.Ui.Loc</c>). Ona görə burada yeganə
/// mövcud mənbə Windows-un öz dilidir: ingilis dilli kompüterdə launcher da
/// ingiliscə danışır.
///
/// Loglar qəsdən tərcümə olunmur — onları uşaq yox, tərtibatçı oxuyur.
/// </summary>
internal static class LauncherText
{
    public static bool IsEnglish =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("en", StringComparison.OrdinalIgnoreCase);

    /// <summary>Sıra "əvvəl az, sonra en" — mənbə mətnlər Azərbaycancadır.</summary>
    public static string T(string az, string en) => IsEnglish ? en : az;
}
