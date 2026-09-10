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
/// <param name="Key">Bayraq və ya səhnə variantının açarı.</param>
public sealed record ExperienceEffect(ExperienceEffectKind Kind, string Key);

/// <summary>Effektin qapalı növləri.</summary>
public enum ExperienceEffectKind
{
    /// <summary>Hekayə bayrağı qoyur — sonrakı keçidlər onu oxuya bilər.</summary>
    SetFlag = 0,

    /// <summary>Səhnənin görünüş variantını dəyişir.</summary>
    SceneVariant = 1,

    /// <summary>Bu addımı yaddaşa yazılası fakt kimi işarələyir.</summary>
    RememberChoice = 2
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
    bool IsFallback = false);

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

    public ExperienceNode? Find(string? nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return null;

        _index ??= Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);

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
    public int LongestPath => LongestFrom(StartNodeId, []);

    private int LongestFrom(string nodeId, HashSet<string> visiting)
    {
        var node = Find(nodeId);

        if (node is null || !visiting.Add(nodeId))
            return 0;

        var deepest = 0;

        foreach (var transition in node.Transitions)
            deepest = Math.Max(deepest, LongestFrom(transition.TargetNodeId, visiting));

        visiting.Remove(nodeId);

        return deepest + 1;
    }
}
