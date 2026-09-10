using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Açıq məzmun seçimlərinin qaydaları — <b>saf funksiyalar</b>, I/O yoxdur.
///
/// <para><b>Açıq söz dolayı davranışdan güclüdür.</b> Uşaq «bunu daha az
/// göstər» deyəndə sistem onun keçmiş davranışını təfsir etməməlidir: bu, bir
/// ehtimal deyil, bir qərardır. Ona görə açıq siqnal balı BİRBAŞA endirir,
/// halbuki kartı kənara qoymaq yalnız inama toxunur.</para>
///
/// <para><b>Uşaq öz dünyasını bağlaya bilmir.</b> «Daha az göstər» blok
/// deyil: mövzu geri çəkilir, amma yox olmur və müddəti bitəndə öz-özünə
/// qayıdır. Tam blok yalnız valideyn qərarıdır.</para>
/// </summary>
public static class ContentPreferenceRules
{
    /// <summary>«Daha az göstər» ilk dəfə bu qədər gün qüvvədə qalır.</summary>
    public const int ShowLessDays = 14;

    /// <summary>Hər təkrar müddəti bu qədər uzadır…</summary>
    public const int ShowLessRepeatDays = 21;

    /// <summary>…amma bu həddi keçmir: bir gün deyilən söz həmişəlik qapı bağlamamalıdır.</summary>
    public const int ShowLessMaxDays = 90;

    /// <summary>Açıq bəyənmənin balı qaldırdığı ən böyük dəyər.</summary>
    public const int LikeDelta = 4;

    /// <summary>
    /// Açıq «daha az göstər»in balı endirdiyi dəyər.
    ///
    /// <para>Bəyənmədən kiçikdir və bu, qəsdəndir: uşaq bir gün əsəbi olanda
    /// sevimli mövzusunu bir toxunuşla silməməlidir.</para>
    /// </summary>
    public const int ShowLessDelta = -3;

    /// <summary>
    /// Bal yerinə TÖVSİYƏ balına tətbiq olunan cəza (0–100 miqyasında).
    ///
    /// <para>Ayrıca var, çünki «daha az göstər» iki fərqli iş görməlidir:
    /// profili bir az düzəltmək VƏ kartı gözlə görünən şəkildə geri
    /// çəkmək. Yalnız birincisi olsaydı, uşaq dediyinin nəticəsini
    /// görməzdi.</para>
    /// </summary>
    public const int ShowLessRankPenalty = 45;

    /// <summary>Açıq bəyənmənin tövsiyə balına verdiyi üstünlük.</summary>
    public const int LikeRankBonus = 20;

    /// <summary>«Daha az göstər» qeydinin bitmə tarixi — təkrar sayına görə uzanır.</summary>
    public static DateTime ExpiryFor(int repeatCount, DateTime now) =>
        now.AddDays(Math.Min(ShowLessMaxDays, ShowLessDays + (Math.Max(0, repeatCount) * ShowLessRepeatDays)));

    /// <summary>
    /// Bir açıq seçimin xassə balına təsiri.
    ///
    /// <para>Valideyn bloku burada SIFIRDIR: blok namizədi hovuzdan çıxarır,
    /// amma uşağın marağı haqqında heç nə demir — valideynin qərarını uşağın
    /// zövqü kimi yazmaq yanlış olardı.</para>
    /// </summary>
    public static int TraitDeltaFor(PetBrainContentPreferenceKind kind) => kind switch
    {
        PetBrainContentPreferenceKind.Liked => LikeDelta,
        PetBrainContentPreferenceKind.ShowLess => ShowLessDelta,
        _ => 0
    };

    /// <summary>Namizəd balına düşən düzəliş (mənfi = geri çəkilir).</summary>
    public static int RankAdjustmentFor(PetBrainContentPreferenceKind kind) => kind switch
    {
        PetBrainContentPreferenceKind.Liked => LikeRankBonus,
        PetBrainContentPreferenceKind.ShowLess => -ShowLessRankPenalty,
        _ => 0
    };

    /// <summary>
    /// Qüvvədə olan seçimlər — vaxtı keçənlər süzülür.
    ///
    /// <para>Vaxtı keçmiş sətir SİLİNMİR: valideyn «uşaq nə dedi» tarixçəsini
    /// görə bilməlidir. O, sadəcə qərar qatına düşmür.</para>
    /// </summary>
    public static IReadOnlyList<ContentPreference> ActiveOf(
        IEnumerable<ContentPreference> preferences, DateTime now) =>
        [.. preferences.Where(p => p.IsActiveAt(now))];

    /// <summary>
    /// Namizəd hovuzundan TAM çıxarılan açarlar — yalnız valideyn bloku.
    /// </summary>
    public static IReadOnlySet<string> BlockedKeys(
        IEnumerable<ContentPreference> preferences, PetBrainContentScope scope, DateTime now) =>
        preferences
            .Where(p => p.Scope == scope
                        && p.Kind == PetBrainContentPreferenceKind.Blocked
                        && p.IsActiveAt(now))
            .Select(p => p.Key)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Geri çəkilən (amma qadağan olunmayan) açarlar.</summary>
    public static IReadOnlySet<string> ShowLessKeys(
        IEnumerable<ContentPreference> preferences, PetBrainContentScope scope, DateTime now) =>
        preferences
            .Where(p => p.Scope == scope
                        && p.Kind == PetBrainContentPreferenceKind.ShowLess
                        && p.IsActiveAt(now))
            .Select(p => p.Key)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Açıq bəyənilən açarlar.</summary>
    public static IReadOnlySet<string> LikedKeys(
        IEnumerable<ContentPreference> preferences, PetBrainContentScope scope, DateTime now) =>
        preferences
            .Where(p => p.Scope == scope
                        && p.Kind == PetBrainContentPreferenceKind.Liked
                        && p.IsActiveAt(now))
            .Select(p => p.Key)
            .ToHashSet(StringComparer.Ordinal);
}
