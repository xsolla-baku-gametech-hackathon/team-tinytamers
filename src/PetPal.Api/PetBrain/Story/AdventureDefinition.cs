using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Story;

/// <summary>
/// Bir CHAPTER — 5–12 dəqiqəlik, öz məqsədi və öz checkpoint-i olan hissə.
///
/// <para>Chapter macəranın texniki bölgüsü deyil, <b>dayanma icazəsidir</b>:
/// uşaq 40 dəqiqəlik macəranı bir oturuşda oynamağa məcbur olmamalıdır. Hər
/// chapter-in sonunda hekayə kiçik bir nəticəyə çatır və vəziyyət serverdə
/// saxlanılır — «yarımçıq qoydum, hər şey itdi» hissi yaranmır.</para>
/// </summary>
/// <param name="ChapterId">Tərif daxilində unikal açar.</param>
/// <param name="Order">Sıra nömrəsi — xəritədə göstərilir.</param>
/// <param name="StartNodeId">Chapter-in ilk düyünü.</param>
/// <param name="EstimatedMinutes">Təxmini uzunluq — uşağa «indi başlasam nə qədər sürər» cavabı.</param>
/// <param name="MainObjectiveIds">Bitirmək üçün lazım olan məqsədlər.</param>
/// <param name="OptionalObjectiveIds">Buraxıla bilən yan tapşırıqlar.</param>
public sealed record AdventureChapterDefinition(
    string ChapterId,
    int Order,
    string TitleAz,
    string TitleEn,
    string SummaryAz,
    string SummaryEn,
    string StartNodeId,
    int EstimatedMinutes,
    IReadOnlyList<string> MainObjectiveIds,
    IReadOnlyList<string> OptionalObjectiveIds)
{
    public string Title(string language) => Localized.T(language, TitleAz, TitleEn);
    public string Summary(string language) => Localized.T(language, SummaryAz, SummaryEn);
}

/// <summary>Məqsədin növü — irəliləmənin NƏ ilə ölçüldüyü.</summary>
public enum AdventureObjectiveKind
{
    ReachNode = 0,
    FindItem = 1,
    FindClue = 2,
    SolvePuzzle = 3,
    HelpNpc = 4,
    BuildObject = 5,
    CollectItems = 6,
    MakeChoice = 7,
    ExploreLocations = 8,
    UsePetAbility = 9,
    FinishChapter = 10
}

/// <summary>
/// Bir MƏQSƏD.
///
/// <para>Ekranda eyni anda yalnız 1–3 məqsəd göstərilir: uzun checklist uşağı
/// yükləyir və macəranı tapşırıq siyahısına çevirir. Gizli məqsəd isə yalnız
/// tamamlananda görünür — kəşfin özü mükafatdır.</para>
/// </summary>
/// <param name="RequiredCount">Neçə dəfə irəlilədikdə tamamlanır.</param>
/// <param name="IsOptional">Yan tapşırıqdır — buraxılsa macəra bağlanmır.</param>
/// <param name="IsHidden">Tamamlanana qədər izləyicidə görünmür.</param>
public sealed record AdventureObjectiveDefinition(
    string ObjectiveId,
    string ChapterId,
    AdventureObjectiveKind Kind,
    string TitleAz,
    string TitleEn,
    string DescriptionAz,
    string DescriptionEn,
    int RequiredCount = 1,
    bool IsOptional = false,
    bool IsHidden = false)
{
    public string Title(string language) => Localized.T(language, TitleAz, TitleEn);
    public string Description(string language) => Localized.T(language, DescriptionAz, DescriptionEn);
}

/// <summary>
/// İnventardakı bir ƏŞYA.
///
/// <para>Əşya kosmetik siyahı elementi deyil: ən azı bir keçidi, tapmacanı və
/// ya sonluğu dəyişməlidir — validator bunu tələb edir. «Toplayırsan, çünki
/// toplanır» tipli obyekt qəsdən yoxdur.</para>
/// </summary>
/// <param name="IsQuestItem">Hekayə üçün vacibdir — istifadə olunanda da itmir.</param>
public sealed record AdventureItemDefinition(
    string ItemId,
    string NameAz,
    string NameEn,
    string DescriptionAz,
    string DescriptionEn,
    string Icon,
    bool IsConsumable = false,
    bool IsQuestItem = false)
{
    public string Name(string language) => Localized.T(language, NameAz, NameEn);
    public string Description(string language) => Localized.T(language, DescriptionAz, DescriptionEn);
}

/// <summary>
/// Jurnala düşən bir İPUCU.
///
/// <para>İpucu inventardan AYRIDIR və bu, qəsdəndir: əşya istifadə olunur,
/// ipucu isə BİLİK verir. İkisini bir siyahıda saxlamaq uşağa «açarı işlət»
/// ilə «yadda saxla» arasındakı fərqi itirərdi.</para>
/// </summary>
/// <param name="RelatedPuzzleNodeId">Hansı tapmacanın həllinə kömək edir; boş ola bilər.</param>
public sealed record AdventureClueDefinition(
    string ClueId,
    string TitleAz,
    string TitleEn,
    string TextAz,
    string TextEn,
    string Icon,
    AdventureClueImportance Importance = AdventureClueImportance.Helpful,
    string RelatedObjectiveId = "",
    string RelatedPuzzleNodeId = "")
{
    public string Title(string language) => Localized.T(language, TitleAz, TitleEn);
    public string Text(string language) => Localized.T(language, TextAz, TextEn);
}

/// <summary>
/// Bir SONLUQ və ona aparan şərt.
///
/// <para>Sonluq son düyməyə basmaqla seçilmir: macəra boyu toplanan BALLA
/// müəyyən olunur (bax <see cref="ExperienceEffectKind.ScoreEnding"/>). Yəni
/// birinci chapter-dəki seçim finalda görünür.</para>
///
/// <para>Bütün sonluqlar müsbətdir. «Pis sonluq» yoxdur — fərq uşağın nəyi
/// bacardığında deyil, nəyi SEÇDİYİNDƏdir.</para>
/// </summary>
/// <param name="MinimumScore">Bu sonluğun açılması üçün lazım olan ən az bal.</param>
/// <param name="NodeId">Sonluq düyünü — epiloqa aparır.</param>
public sealed record AdventureEndingDefinition(
    string EndingKey,
    string NodeId,
    string TitleAz,
    string TitleEn,
    string TitleKeyAz,
    string TitleKeyEn,
    string RewardCode,
    int MinimumScore = 0)
{
    public string Title(string language) => Localized.T(language, TitleAz, TitleEn);

    /// <summary>Uşağın qazandığı ünvan — «Ayın Qoruyucusu».</summary>
    public string EarnedTitle(string language) => Localized.T(language, TitleKeyAz, TitleKeyEn);
}
