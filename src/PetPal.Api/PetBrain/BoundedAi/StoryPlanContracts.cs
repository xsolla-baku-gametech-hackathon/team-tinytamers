using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.BoundedAi;

/// <summary>
/// Modelin təklif edə biləcəyi BİR düyün.
///
/// <para>Diqqət: burada nə mətn, nə rəqəm, nə də qayda var — yalnız
/// TƏSDİQLƏNMİŞ ID-lər. Model «nə yazılacağını» seçmir, «hansı hazır
/// parçaların hansı sıra ilə düzüləcəyini» seçir. Mətn həmişə serverin
/// kataloqundan gəlir.</para>
/// </summary>
/// <param name="Id">Düyünün açarı — plan daxilində unikal.</param>
/// <param name="BeatId">Təsdiqlənmiş hekayə anı (<c>arrival</c>, <c>discovery</c>).</param>
/// <param name="SceneId">Təsdiqlənmiş səhnə.</param>
/// <param name="ToneId">Təsdiqlənmiş ton.</param>
/// <param name="Kind">Ekranın növü.</param>
/// <param name="PuzzleAdapterId">Tapmaca düyünündə hansı adapter; başqa halda boş.</param>
/// <param name="MemoryCallbackId">Təsdiqlənmiş yaddaş çağırışı; yoxdursa boş.</param>
/// <param name="EndingId">Sonluq düyünündə hansı sonluq.</param>
/// <param name="Transitions">Bu düyündən çıxan keçidlər.</param>
public sealed record StoryPlanNode(
    string Id,
    string BeatId,
    string SceneId,
    string ToneId,
    PetBrainStageKind Kind,
    string PuzzleAdapterId,
    string MemoryCallbackId,
    string EndingId,
    IReadOnlyList<StoryPlanTransition> Transitions);

/// <summary>
/// Keçid. <b>Şərt dili yoxdur</b> — yalnız təsdiqlənmiş keçid növü və hədəf.
/// </summary>
/// <param name="TypeId">Təsdiqlənmiş keçid növü (<c>choice</c>, <c>always</c>, <c>solved</c>).</param>
/// <param name="OptionId">Seçim keçidində hansı variant; başqa halda boş.</param>
public sealed record StoryPlanTransition(string TypeId, string TargetNodeId, string OptionId);

/// <summary>
/// Modelin təklif etdiyi TAM plan.
///
/// <para>Bu, hələ macəra DEYİL — namizəddir. Serverin validatoru onu
/// təsdiqləyənə qədər heç bir uşaq onu görmür, təsdiqlənəndən sonra isə
/// deterministik tərifə çevrilir və bütün adi qaydalar (mükafat, çətinlik,
/// tapmacanın həqiqəti) əvvəlki kimi serverin əlində qalır.</para>
/// </summary>
public sealed record StoryPlan(
    string ExperienceKey,
    string CharacterId,
    string StartNodeId,
    IReadOnlyList<StoryPlanNode> Nodes);

/// <summary>
/// Modelin plan təklif edən qatı.
///
/// <para>Standart implementasiya <b>heç nə təklif etmir</b>: özəllik açarı
/// bağlıdır və deterministik planlayıcı bütün işi görür. Bu, «ehtiyat variant»
/// deyil — ƏSAS variantdır.</para>
/// </summary>
public interface IStoryPlanProvider
{
    /// <summary>
    /// Plan təklif edir; təklif yoxdursa <c>null</c>.
    ///
    /// <para>Heç vaxt istisna atmır: model sıradan çıxsa, ləngisə, pozuq JSON
    /// qaytarsa — cavab <c>null</c> olur və çağıran deterministik yola davam
    /// edir.</para>
    /// </summary>
    Task<StoryPlan?> ProposeAsync(StoryPlanRequest request, CancellationToken ct = default);
}

/// <summary>
/// Modelə verilə bilən TAM giriş dəsti.
///
/// <para>Uşağa aid heç nə yoxdur: nə ad, nə id, nə söhbət, nə yaddaş cümləsi,
/// nə dəqiq yaş. Yalnız yaş ZOLAĞI, dil, mövzu və icazə verilən ID-lər.</para>
/// </summary>
public sealed record StoryPlanRequest(
    string Language,
    string AgeBand,
    string Theme,
    string ExperienceKey,
    IReadOnlyList<string> AllowedBeatIds,
    IReadOnlyList<string> AllowedSceneIds,
    IReadOnlyList<string> AllowedToneIds,
    IReadOnlyList<string> AllowedPuzzleAdapterIds,
    IReadOnlyList<string> AllowedMemoryCallbackIds,
    IReadOnlyList<string> AllowedEndingIds,
    IReadOnlyList<string> AllowedCharacterIds);

/// <summary>
/// Standart provayder: <b>heç nə təklif etmir</b>.
///
/// <para>Bu sinif qəsdən boşdur. Özəllik açarı bağlı olanda (standart) heç bir
/// model çağırışı olmur, heç bir şəbəkə gedişi yoxdur və app tam işləyir.</para>
/// </summary>
public sealed class NoStoryPlanProvider : IStoryPlanProvider
{
    public Task<StoryPlan?> ProposeAsync(StoryPlanRequest request, CancellationToken ct = default) =>
        Task.FromResult<StoryPlan?>(null);
}
