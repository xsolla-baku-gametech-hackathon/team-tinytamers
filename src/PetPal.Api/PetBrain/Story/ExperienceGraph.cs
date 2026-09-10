using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Story;

/// <summary>
/// Bir düyünün tətbiq etdiyi EFFEKT.
///
/// <para>Effekt <b>ifadə deyil</b>: nə skript, nə şərt dili, nə də hesablama.
/// Yalnız qapalı bir növ və təsdiqlənmiş bir açar. Bu, qəsdən belədir — qrafın
/// gücü artdıqca "kiçik bir ifadə dili" əlavə etmək cazibədar olur, o isə
/// serverin nəzarətini itirməsi deməkdir.</para>
/// </summary>
/// <param name="Kind">Effektin növü.</param>
/// <param name="Key">Bayraq, əşya, ipucu, məqsəd və ya sonluq açarı.</param>
public sealed record ExperienceEffect(ExperienceEffectKind Kind, string Key)
{
    /// <summary>Sayla işləyən effektlərdə miqdar: əşya sayı, məqsəd irəliləməsi, sonluq balı.</summary>
    public int Amount { get; init; } = 1;
}

/// <summary>
/// Effektin qapalı növləri.
///
/// <para>Siyahı uzandı, prinsip dəyişmədi: effekt hələ də <b>bir növ və bir
/// təsdiqlənmiş açardır</b>. Nə şərt, nə hesablama, nə də klientdən gələn
/// dəyər. Server hansı effektin nə etdiyini bilir; tərif yalnız onu ADLANDIRIR.</para>
/// </summary>
public enum ExperienceEffectKind
{
    /// <summary>Hekayə bayrağı qoyur — sonrakı keçidlər onu oxuya bilər.</summary>
    SetFlag = 0,

    /// <summary>Səhnənin görünüş variantını dəyişir.</summary>
    SceneVariant = 1,

    /// <summary>Bu addımı yaddaşa yazılası fakt kimi işarələyir.</summary>
    RememberChoice = 2,

    /// <summary>İnventara əşya əlavə edir.</summary>
    GrantItem = 3,

    /// <summary>
    /// Sərf olunan əşyanı azaldır.
    ///
    /// <para>Hekayə üçün VACİB əşya (<c>IsQuestItem</c>) heç vaxt tam
    /// silinmir: uşağın macərası əlindən bir predmet çıxdığı üçün bloklana
    /// bilməz — bu, engine-in zəmanətidir, tərifin diqqətli yazılması deyil.</para>
    /// </summary>
    ConsumeItem = 4,

    /// <summary>Jurnala ipucu yazır.</summary>
    DiscoverClue = 5,

    /// <summary>Məqsədi aktivləşdirir.</summary>
    StartObjective = 6,

    /// <summary>Məqsədin sayğacını irəli aparır.</summary>
    AdvanceObjective = 7,

    /// <summary>Məqsədi dərhal tamamlayır.</summary>
    CompleteObjective = 8,

    /// <summary>Yan tapşırığı «buraxıldı» kimi bağlayır — əsas hekayə davam edir.</summary>
    SkipOptionalObjective = 9,

    /// <summary>Dünya bayrağı — macəra bitəndən sonra da digər ekranlara keçir.</summary>
    SetWorldFlag = 10,

    /// <summary>Bir sonluğun balını artırır; finalda ən yüksək bal qalib gəlir.</summary>
    ScoreEnding = 11,

    /// <summary>Chapter-i tamamlanmış kimi bağlayır və checkpoint yazır.</summary>
    CompleteChapter = 12,

    /// <summary>NPC-nin vəziyyətini dəyişir — sonrakı dialoq ondan asılıdır.</summary>
    SetNpcState = 13
}

/// <summary>
/// Bir düyündən digərinə KEÇİD.
///
/// <para>Şərt yalnız iki formadadır: seçilmiş variantın açarı və ya qoyulmuş
/// bayraq. Hər ikisi tərifin öz taksonomiyasındandır.</para>
/// </summary>
/// <param name="TargetNodeId">Hara gedirik.</param>
/// <param name="RequiredOptionKey">Bu variant seçilibsə; boşdursa baxılmır.</param>
/// <param name="RequiredFlag">Bu bayraq varsa; boşdursa baxılmır.</param>
/// <param name="RequiredResult">Tapmacanın nəticəsi bu olmalıdır; <c>null</c> = fərqi yoxdur.</param>
/// <param name="Priority">Kiçik rəqəm əvvəl yoxlanılır — nəticə determinist olsun.</param>
/// <param name="IsFallback">
/// Heç bir şərt tutmayanda işə düşən keçid. Hər düyündə (sonluqdan başqa)
/// MÜTLƏQ biri olmalıdır — bu, "dalan yoxdur" zəmanətidir.
/// </param>
public sealed record ExperienceTransition(
    string TargetNodeId,
    string RequiredOptionKey = "",
    string RequiredFlag = "",
    PetBrainStageResult? RequiredResult = null,
    int Priority = 100,
    bool IsFallback = false)
{
    /// <summary>
    /// Əlavə şərtlər — inventar, ipucu, məqsəd, chapter, variant, bağ pilləsi.
    ///
    /// <para>V1-in iki sahəsi (<see cref="RequiredOptionKey"/>,
    /// <see cref="RequiredFlag"/>) qaldı: onlarla yazılmış tərifləri
    /// dəyişdirmək lazım deyil. Yeni şərtlər onların ÜSTÜNƏ gəlir və hamısı
    /// eyni anda ödənməlidir.</para>
    /// </summary>
    public ExperienceCondition Requires { get; init; } = ExperienceCondition.None;
}

/// <summary>
/// Qrafın bir DÜYÜNÜ — bir ekran.
/// </summary>
/// <param name="Id">Tərif daxilində unikal açar.</param>
/// <param name="Kind">Ekranın növü.</param>
/// <param name="SceneVariant">UI-ın çəkdiyi səhnə variantı — seçimlər onu dəyişir.</param>
/// <param name="PuzzleFamily">
/// Tapmaca düyünündə hansı şablon ailəsi istənilir; boşdursa generator özü seçir.
/// </param>
/// <param name="MemoryCallbackKey">
/// Bu düyün əvvəlki macəradan bir xatirəni xatırlada bilər. Açar QAPALIDIR —
/// pet yalnız serverin təsdiqlədiyi faktı deyir.
/// </param>
/// <param name="EndingKey">Sonluq düyünündə hansı sonluqdur.</param>
public sealed record ExperienceNode(
    string Id,
    PetBrainStageKind Kind,
    string PromptAz,
    string PromptEn,
    string PetLineAz,
    string PetLineEn,
    IReadOnlyList<ExperienceOption> Options,
    IReadOnlyList<ExperienceTransition> Transitions,
    IReadOnlyList<ExperienceEffect> Effects,
    string SceneVariant = "",
    string PuzzleFamily = "",
    string MemoryCallbackKey = "",
    string EndingKey = "")
{
    public string Prompt(string language) => Localized.T(language, PromptAz, PromptEn);
    public string PetLine(string language) => Localized.T(language, PetLineAz, PetLineEn);

    public bool IsEnding => Kind == PetBrainStageKind.Ending;

    /// <summary>
    /// Bu düyün hansı chapter-ə aiddir; chapter-siz tərifdə boşdur.
    ///
    /// <para>Boş qalması QƏSDƏN mümkündür: köhnə, chapter-siz təriflər (Ay
    /// Kristalı V1) eyni engine üzərində işləməyə davam edir və onları
    /// bölmək üçün yenidən yazmaq lazım gəlmir.</para>
    /// </summary>
    public string ChapterId { get; init; } = string.Empty;

    /// <summary>
    /// Buraya çatmaq TƏHLÜKƏSİZ dayanma nöqtəsidir.
    ///
    /// <para>Checkpoint hər düyündə yazılmır: uşaq tapmacanın ortasında
    /// dayanıb qayıdanda yarımçıq lövhə görməməlidir. Checkpoint hekayənin
    /// NƏFƏS ALDIĞI yerdədir — səhnə bitəndə, chapter qurtaranda.</para>
    /// </summary>
    public bool IsCheckpoint { get; init; }

    /// <summary>Bu düyünə çatanda tamamlanmış sayılan chapter; boş = heç biri.</summary>
    public string CompletesChapterId { get; init; } = string.Empty;

    /// <summary>
    /// Bu düyünün göstərilməsi üçün şərt — ödənmirsə keçid onu SEÇMİR.
    ///
    /// <para>Yalnız könüllü səhnələr üçündür (uzun variantın əlavə dialoqu,
    /// gizli otaq). Əsas yolun düyünü şərtli OLA BİLMƏZ — validator bunu
    /// yoxlayır, yoxsa hekayə bəzi profillərdə dalana düşərdi.</para>
    /// </summary>
    public ExperienceCondition Requires { get; init; } = ExperienceCondition.None;

    /// <summary>Yan tapşırıq düyünüdür — buraxıla bilər, əsas hekayəni bloklamır.</summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// Pet-in bu səhnədə etdiyi ƏMƏKDAŞLIQ hərəkəti — boş = etmir.
    ///
    /// <para>Pet burada dekorasiya deyil: açar UI-a hansı bacarığın
    /// işlədildiyini deyir və tamamlamada xatirəyə yazılır.</para>
    /// </summary>
    public string PetAbilityKey { get; init; } = string.Empty;

    /// <summary>Səhnədəki NPC-nin açarı — dialoq və vəziyyət ona bağlanır.</summary>
    public string NpcKey { get; init; } = string.Empty;

    /// <summary>
    /// Bu düyündən sonrakı yolu SONLUQ BALI müəyyən edir.
    ///
    /// <para>Sonluq son düyməyə basmaqla seçilmir: macəra boyu toplanan bal
    /// qərar verir (bax <see cref="ExperienceEffectKind.ScoreEnding"/>), yəni
    /// birinci fəsildəki seçim finalda görünür. Keçid şərtləri ilə yazsaydıq,
    /// yalnız SONUNCU seçim əhəmiyyət daşıyardı və əvvəlki qırx dəqiqə
    /// bəzəyə çevrilərdi.</para>
    ///
    /// <para>Qərarın qrafda GÖRÜNMƏSİ vacibdir: bu bayraq həmin nöqtəni
    /// adlandırır, məntiqi kodun içində gizlətmir. Ehtiyat keçid yenə
    /// məcburidir — bal heç bir sonluğa çatmasa, uşaq sonluqsuz qalmır.</para>
    /// </summary>
    public bool ResolvesEnding { get; init; }
}

/// <summary>
/// Budaqlanan macəranın TAM tərifi.
///
/// <para>Xətti <see cref="ExperienceTemplate"/> silinmir: kataloqdakı altı
/// macəradan beşi hələ də onunla işləyir və yarımçıq run-ları pozmadan
/// miqrasiya olunacaq (bax <c>docs/PET_BRAIN.md</c>). Qraf yalnız onu ƏVƏZ
/// EDƏN şablonlarda qoşulur.</para>
/// </summary>
/// <param name="Key">Şablon açarı — <see cref="ExperienceTemplate.Key"/> ilə eyni.</param>
/// <param name="Version">Tərifin versiyası; run bunu yadda saxlayır.</param>
/// <param name="StartNodeId">Başlanğıc düyün.</param>
/// <param name="AllowedPuzzleFamilies">
/// Bu macərada işlədilə bilən tapmaca şablonları — boşdursa uyğunluq süzgəci
/// tək başına qərar verir.
/// </param>
public sealed record ExperienceDefinition(
    string Key,
    int Version,
    string StartNodeId,
    IReadOnlyList<ExperienceNode> Nodes,
    IReadOnlyList<string> AllowedPuzzleFamilies)
{
    private Dictionary<string, ExperienceNode>? _index;
    private Dictionary<string, AdventureChapterDefinition>? _chapters;

    /// <summary>
    /// Chapter-lər — boş siyahı = bölünməmiş, qısa macəra.
    ///
    /// <para>Boşluq etibarlı vəziyyətdir: engine chapter-siz tərifi eyni yolla
    /// oynadır, sadəcə checkpoint və recap ekranları olmur.</para>
    /// </summary>
    public IReadOnlyList<AdventureChapterDefinition> Chapters { get; init; } = [];

    public IReadOnlyList<AdventureObjectiveDefinition> Objectives { get; init; } = [];

    public IReadOnlyList<AdventureItemDefinition> Items { get; init; } = [];

    public IReadOnlyList<AdventureClueDefinition> Clues { get; init; } = [];

    /// <summary>
    /// Sonluqlar və onların bal həddi.
    ///
    /// <para>Boş buraxıla bilər — o halda sonluq sadəcə çatılan düyündür
    /// (V1 davranışı). Dolu olanda isə finalda BAL qərar verir.</para>
    /// </summary>
    public IReadOnlyList<AdventureEndingDefinition> Endings { get; init; } = [];

    /// <summary>
    /// Növbəti macəranın QARMAĞI — epiloqun son cümləsi.
    ///
    /// <para>Macəra bitəndə uşaq «bu qədər» yox, «bəs sonra?» hissi ilə
    /// qalmalıdır. Cümlə tərifin özündədir, çünki hekayənin davamını yalnız
    /// hekayənin müəllifi bilir.</para>
    /// </summary>
    public string SequelHookAz { get; init; } = string.Empty;

    public string SequelHookEn { get; init; } = string.Empty;

    public string SequelHook(string language) =>
        PetPal.Api.Common.Localized.T(language, SequelHookAz, SequelHookEn);

    /// <summary>Bütün chapter-lərin təxmini cəmi — «bu macəra nə qədər sürər».</summary>
    public int EstimatedTotalMinutes => Chapters.Sum(c => c.EstimatedMinutes);

    public bool HasChapters => Chapters.Count > 0;

    public AdventureChapterDefinition? Chapter(string? chapterId)
    {
        if (string.IsNullOrEmpty(chapterId))
            return null;

        _chapters ??= Chapters.ToDictionary(c => c.ChapterId, StringComparer.Ordinal);

        return _chapters.GetValueOrDefault(chapterId);
    }

    public AdventureObjectiveDefinition? Objective(string? objectiveId) =>
        string.IsNullOrEmpty(objectiveId)
            ? null
            : Objectives.FirstOrDefault(o => string.Equals(o.ObjectiveId, objectiveId, StringComparison.Ordinal));

    public AdventureItemDefinition? Item(string? itemId) =>
        string.IsNullOrEmpty(itemId)
            ? null
            : Items.FirstOrDefault(i => string.Equals(i.ItemId, itemId, StringComparison.Ordinal));

    public AdventureClueDefinition? Clue(string? clueId) =>
        string.IsNullOrEmpty(clueId)
            ? null
            : Clues.FirstOrDefault(c => string.Equals(c.ClueId, clueId, StringComparison.Ordinal));

    public AdventureEndingDefinition? Ending(string? endingKey) =>
        string.IsNullOrEmpty(endingKey)
            ? null
            : Endings.FirstOrDefault(e => string.Equals(e.EndingKey, endingKey, StringComparison.Ordinal));

    /// <summary>Sıraya düzülmüş chapter-lər — xəritə və irəliləmə göstəricisi üçün.</summary>
    public IReadOnlyList<AdventureChapterDefinition> OrderedChapters =>
        [.. Chapters.OrderBy(c => c.Order)];

    /// <summary>Bu chapter-in düyünləri.</summary>
    public IReadOnlyList<ExperienceNode> NodesOf(string chapterId) =>
        [.. Nodes.Where(n => string.Equals(n.ChapterId, chapterId, StringComparison.Ordinal))];

    public ExperienceNode? Find(string? nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return null;

        _index ??= Nodes
            .GroupBy(n => n.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        return _index.GetValueOrDefault(nodeId);
    }

    public ExperienceNode Start => Find(StartNodeId)
        ?? throw new InvalidOperationException($"«{Key}» tərifində başlanğıc düyün yoxdur.");

    /// <summary>Sonluqların açarları — üçü də uşağın çata biləcəyi yollardır.</summary>
    public IReadOnlyList<string> EndingKeys =>
        [.. Nodes.Where(n => n.IsEnding).Select(n => n.EndingKey).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// Ən uzun mümkün yolun düyün sayı — irəliləmə göstəricisi üçün.
    ///
    /// <para>Budaqlanan hekayədə "5 mərhələdən 3-cü" ifadəsi yanlışdır, çünki
    /// yollar müxtəlif uzunluqdadır. Uşağa bunun yerinə keçilmiş addımlar və
    /// təxmini uzunluq göstərilir.</para>
    /// </summary>
    /// <para><b>YADDA SAXLAYAN</b> hesablama. Sadə variantı — hər yolu ayrıca
    /// gəzmək — kiçik qrafda işləyirdi, altı chapter-lik qrafda isə budaqların
    /// sayı üstlü artdığı üçün yoxlama dəqiqələrlə çəkirdi. Düyün başına bir
    /// dəfə hesablamaq eyni cavabı verir.</para>
    ///
    /// <para>Dövrə (olmamalıdır, amma tərif səhv yazıla bilər) yığında olan
    /// düyünü sıfır sayaraq kəsilir — sonsuz reküsiya əvəzinə sonlu, determinist
    /// cavab.</para>
    public int LongestPath => LongestFrom(StartNodeId, [], []);

    private int LongestFrom(string nodeId, HashSet<string> onStack, Dictionary<string, int> memo)
    {
        if (memo.TryGetValue(nodeId, out var cached))
            return cached;

        var node = Find(nodeId);

        if (node is null || !onStack.Add(nodeId))
            return 0;

        var deepest = 0;

        foreach (var transition in node.Transitions)
            deepest = Math.Max(deepest, LongestFrom(transition.TargetNodeId, onStack, memo));

        onStack.Remove(nodeId);
        memo[nodeId] = deepest + 1;

        return deepest + 1;
    }
}
