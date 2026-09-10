using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Mini oyunların AİLƏSİ — hansı düşünmə növünü işlədirlər.
///
/// <para>Cədvəl qapalıdır və <c>Games/GameCatalog.cs</c> açarlarına baxır.
/// Naməlum açar heç nə əlavə etmir (fail closed): yeni oyun gələndə profil
/// səssizcə uydurma siqnal almır, sadəcə ümumi «şənlik» qalır.</para>
///
/// <para><b>Nə üçün ehtiyatlı?</b> Mini oyun mövzu seçimi deyil. Uşaq orada
/// «kosmos» yox, «əylən» seçir — ona görə ailə siqnalı ən zəif pillədədir və
/// heç vaxt macəra seçimi ilə eyni çəkidə olmur.</para>
/// </summary>
public static class MiniGameFamilies
{
    /// <summary>Yaddaş və ardıcıllıq — tapmaca düşüncəsi.</summary>
    private static readonly string[] PatternGames =
        ["memory-match", "color-echo", "letter-hunt", "chef-order"];

    /// <summary>Refleks və hərəkət — kəşfiyyatçı enerjisi.</summary>
    private static readonly string[] MotionGames =
        ["quick-tap", "star-run", "cloud-jump", "basket-catch", "fruit-slice"];

    /// <summary>Sərbəst, təzyiqsiz oyun — yaradıcı ox.</summary>
    private static readonly string[] PlayfulGames = ["bubble-pop"];

    /// <summary>
    /// Oyunun ailəsindən gələn İKİNCİ siqnal; naməlum oyun üçün <c>null</c>.
    /// </summary>
    public static (PetBrainTraitCategory Category, string Key)? FamilyOf(string? gameKey)
    {
        if (string.IsNullOrWhiteSpace(gameKey))
            return null;

        if (PatternGames.Contains(gameKey, StringComparer.Ordinal))
            return (PetBrainTraitCategory.Interest, TraitKeys.Puzzles);

        if (MotionGames.Contains(gameKey, StringComparer.Ordinal))
            return (PetBrainTraitCategory.PlayStyle, TraitKeys.Explorer);

        if (PlayfulGames.Contains(gameKey, StringComparer.Ordinal))
            return (PetBrainTraitCategory.PlayStyle, TraitKeys.Creative);

        return null;
    }
}
