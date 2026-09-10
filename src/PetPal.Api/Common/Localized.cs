using System.Globalization;

namespace PetPal.Api.Common;

/// <summary>
/// Sadə iki dilli mətn seçimi. Uşaq məzmunu az/en dillərində saxlanılır;
/// tam i18n platformasına ehtiyac yaranana qədər bu kifayətdir və
/// dil məntiqinin tək yerdə qalmasını təmin edir.
/// </summary>
public static class Localized
{
    public const string Azerbaijani = "az";
    public const string English = "en";

    public static readonly string[] Supported = [Azerbaijani, English];

    /// <summary>Naməlum/boş dil kodunu dəstəklənən dilə çevirir.</summary>
    public static string Normalize(string? languageCode) =>
        Supported.Contains(languageCode, StringComparer.OrdinalIgnoreCase)
            ? languageCode!.ToLowerInvariant()
            : Azerbaijani;

    /// <summary>Azərbaycanca variant boşdursa ingiliscəyə düşür — məzmun heç vaxt itmir.</summary>
    public static string Pick(string languageCode, string english, string? azerbaijani) =>
        Normalize(languageCode) == Azerbaijani && !string.IsNullOrWhiteSpace(azerbaijani)
            ? azerbaijani!
            : english;

    /// <summary>
    /// Cari SORĞUNUN dili. <c>UseRequestLocalization</c> onu Accept-Language
    /// başlığından qurur, klient isə hər sorğuda interfeysin dilini göndərir.
    /// Uşaq profili məlum olmayan yerlərdə (giriş, qeydiyyat, profil tapılmadı)
    /// mesajın dilini yalnız bu müəyyən edir.
    /// </summary>
    public static string Current =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals(English, StringComparison.OrdinalIgnoreCase)
            ? English
            : Azerbaijani;

    /// <summary>
    /// Koddakı mesajın iki dili. Sıra qəsdən "əvvəl az, sonra en"-dir: mənbə
    /// mətnlər Azərbaycancadır (<see cref="Pick"/> isə bazadakı sütunlar üçündür,
    /// orada baza dili ingiliscədir).
    /// </summary>
    public static string T(string? languageCode, string az, string en) =>
        Normalize(languageCode ?? Current) == Azerbaijani ? az : en;

    /// <summary>Dil məlum deyilsə (uşaq tapılmayıb) sorğunun dili işlədilir.</summary>
    public static string T(string az, string en) => T(null, az, en);
}
