using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Mind;

/// <summary>
/// Dəstək planının GÖRÜNƏN forması — <b>saf funksiyalar</b>.
///
/// <para><b>İpucunun MƏZMUNU dəyişmir.</b> Doğru cavaba aparan fakt eyni
/// qalır — dəyişən yalnız onun necə çərçivəyə salınmasıdır. Bu, qəsdəndir:
/// forma seçimi uşağa fərqli QƏDƏR kömək verməməlidir, yoxsa «vizual» seçən
/// uşaq «qayda» seçəndən asan oyun oynayardı və seçim gizli çətinlik
/// tənzimləyicisinə çevrilərdi.</para>
///
/// <para>Forma uşağın bacarığı, əlilliyi və ya diaqnozu haqqında iddia
/// DEYİL: yalnız «hansı izah bu uşaq üçün işlədi» seçimidir.</para>
/// </summary>
public static class SupportVoice
{
    /// <summary>
    /// Bu qədər səhvdən sonra <see cref="PetBrainHintTiming.Delayed"/>
    /// rejimində kömək öz-özünə görünür.
    /// </summary>
    public const int DelayedHintAfterMistakes = 2;

    /// <summary>
    /// İpucunu seçilmiş FORMAYA salır.
    ///
    /// <para>Boş ipucu boş qalır: yoxdursa uydurulmur.</para>
    /// </summary>
    public static string Frame(string hint, PetBrainHintStyle style, string language)
    {
        if (string.IsNullOrWhiteSpace(hint))
            return hint;

        var prefix = style switch
        {
            PetBrainHintStyle.StepByStep => Localized.T(language, "Birinci addım:", "First step:"),
            PetBrainHintStyle.Example => Localized.T(language, "Məsələn:", "For example:"),
            PetBrainHintStyle.Rule => Localized.T(language, "Qayda belədir:", "The rule is:"),
            _ => Localized.T(language, "Yaxşı bax:", "Take a look:")
        };

        return $"{prefix} {hint}";
    }

    /// <summary>
    /// Kömək İSTƏNİLMƏDƏN göstərilməlidirmi.
    ///
    /// <para><c>Immediate</c> açıq seçimdir və dərhal işləyir. <c>Delayed</c>
    /// isə yalnız uşaq bir neçə dəfə səhv edəndən sonra: pet əl uzadır, amma
    /// cavabı əvvəlcədən pıçıldamır.</para>
    /// </summary>
    public static bool ShouldRevealHint(SupportPlan support, int mistakes) => support.Timing switch
    {
        PetBrainHintTiming.Immediate => true,
        PetBrainHintTiming.Delayed => mistakes >= DelayedHintAfterMistakes,
        _ => false
    };

    /// <summary>
    /// Eyni anda göstərilən ƏN ÇOX variant.
    ///
    /// <para>İkidən aşağı düşmür: bir variant seçim deyil, düymədir — və
    /// uşağın seçim hüququ dəstək adı altında əlindən alına bilməz.</para>
    /// </summary>
    public const int ReducedOptionLimit = 2;

    /// <summary>
    /// Variantları dəstək planına görə daraldır.
    ///
    /// <para>Sıra POZULMUR: ilk variantlar saxlanılır, çünki hekayə onları
    /// müəyyən sıra ilə yazıb və «təsadüfi iki variant» başqa bir oyundur.</para>
    /// </summary>
    public static IReadOnlyList<T> Narrow<T>(IReadOnlyList<T> options, SupportPlan support)
    {
        if (!support.ReducedOptions || options.Count <= ReducedOptionLimit)
            return options;

        return [.. options.Take(ReducedOptionLimit)];
    }
}
