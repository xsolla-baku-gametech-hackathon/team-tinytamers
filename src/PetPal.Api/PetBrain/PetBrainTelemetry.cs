using System.Diagnostics.Metrics;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Pet Brain-in ölçmələri — <b>PII-siz</b>.
///
/// <para><b>Etiketlərə heç vaxt düşməyənlər:</b> uşaq id-si, uşaq və pet adı,
/// söhbət mətni, yaddaş cümləsi, sərbəst mətn. Metrik etiketi az sayda,
/// TƏKRARLANAN dəyər olmalıdır; uşaq id-si həm gizlilik pozuntusu, həm də
/// kardinallıq partlayışıdır.</para>
///
/// <para><b>İcazə verilən etiketlər:</b> siyasət versiyası, şablon açarı, tərif
/// versiyası, düyün növü, mexanika açarı, çətinlik pilləsi, xarakter, sonluq
/// açarı və ehtiyata düşmə səbəbi — hamısı qapalı kataloqdandır.</para>
/// </summary>
public sealed class PetBrainTelemetry
{
    public const string MeterName = "PetPal.PetBrain";

    private readonly Counter<long> _recommendations;
    private readonly Counter<long> _runs;
    private readonly Counter<long> _nodes;
    private readonly Counter<long> _puzzles;
    private readonly Counter<long> _memoryCallbacks;
    private readonly Counter<long> _narrativeCache;
    private readonly Counter<long> _fallbacks;
    private readonly Counter<long> _conflicts;
    private readonly Counter<long> _intents;
    private readonly Counter<long> _aiPlans;

    public PetBrainTelemetry(IMeterFactory factory)
    {
        var meter = factory.Create(MeterName);

        _recommendations = meter.CreateCounter<long>(
            "petbrain.recommendation", "count", "Tövsiyə göstərildi, seçildi, dəyişdirildi və ya təxirə salındı.");

        _runs = meter.CreateCounter<long>(
            "petbrain.run", "count", "Macəra başladı, tamamlandı və ya yarımçıq qaldı.");

        _nodes = meter.CreateCounter<long>(
            "petbrain.node", "count", "Bir düyün tamamlandı (budaq seçimi daxil).");

        _puzzles = meter.CreateCounter<long>(
            "petbrain.puzzle", "count", "Tapmaca cəhdi, ipucu və həlli.");

        _memoryCallbacks = meter.CreateCounter<long>(
            "petbrain.memory_callback", "count", "Yaddaş çağırışı göstərildi və ya işlədildi.");

        _narrativeCache = meter.CreateCounter<long>(
            "petbrain.narrative_cache", "count", "AI mətn keşinin tutması və tutmaması.");

        _fallbacks = meter.CreateCounter<long>(
            "petbrain.fallback", "count", "Deterministik ehtiyat yola düşmə.");

        _conflicts = meter.CreateCounter<long>(
            "petbrain.conflict", "count", "Eyni vaxtlı sorğu münaqişəsi.");

        _intents = meter.CreateCounter<long>(
            "petbrain.intent", "count", "Pet-in niyyəti seçildi, tamamlandı və ya kənara qoyuldu.");

        _aiPlans = meter.CreateCounter<long>(
            "petbrain.ai_plan", "count", "Modelin təklif etdiyi plan qəbul və ya rədd edildi.");
    }

    public void Recommendation(
        PetBrainRecommendationFeedback feedback, string templateKey, int policyVersion) =>
        _recommendations.Add(1,
            new KeyValuePair<string, object?>("feedback", feedback.ToString()),
            new KeyValuePair<string, object?>("template", templateKey),
            new KeyValuePair<string, object?>("policy", policyVersion));

    public void RunStarted(string templateKey, int definitionVersion, PetBrainDifficulty difficulty, bool graph) =>
        _runs.Add(1,
            new KeyValuePair<string, object?>("phase", "started"),
            new KeyValuePair<string, object?>("template", templateKey),
            new KeyValuePair<string, object?>("definition", definitionVersion),
            new KeyValuePair<string, object?>("difficulty", difficulty.ToString()),
            new KeyValuePair<string, object?>("model", graph ? "graph" : "linear"));

    public void RunCompleted(string templateKey, string endingKey, int stepsTaken) =>
        _runs.Add(1,
            new KeyValuePair<string, object?>("phase", "completed"),
            new KeyValuePair<string, object?>("template", templateKey),
            new KeyValuePair<string, object?>("ending", string.IsNullOrEmpty(endingKey) ? "linear" : endingKey),
            new KeyValuePair<string, object?>("steps", stepsTaken));

    public void RunAbandoned(string templateKey, int stepsTaken) =>
        _runs.Add(1,
            new KeyValuePair<string, object?>("phase", "abandoned"),
            new KeyValuePair<string, object?>("template", templateKey),
            new KeyValuePair<string, object?>("steps", stepsTaken));

    /// <summary>
    /// Bir fəsil bağlandı.
    ///
    /// <para>Ayrıca ölçülür, çünki chapter-li macərada ən vacib sual budur:
    /// <b>uşaqlar hansı fəsildə dayanır?</b> «Tamamlandı/yarımçıq» nisbəti
    /// bunu göstərmir — 40 dəqiqəlik macərada dördüncü fəsildə dayanmaq
    /// uğursuzluq deyil, gözlənilən davranışdır.</para>
    /// </summary>
    public void ChapterCompleted(string templateKey, string chapterId) =>
        _nodes.Add(1,
            new KeyValuePair<string, object?>("template", templateKey),
            new KeyValuePair<string, object?>("kind", "ChapterCompleted"),
            new KeyValuePair<string, object?>("branch", chapterId));

    /// <summary>Macəra dayandırıldı və ya davam etdirildi — bərpa nisbətini ölçür.</summary>
    public void RunSession(string phase, string templateKey, string chapterId) =>
        _runs.Add(1,
            new KeyValuePair<string, object?>("phase", phase),
            new KeyValuePair<string, object?>("template", templateKey),
            new KeyValuePair<string, object?>("chapter", string.IsNullOrEmpty(chapterId) ? "none" : chapterId));

    public void NodeCompleted(string templateKey, PetBrainStageKind kind, string optionKey) =>
        _nodes.Add(1,
            new KeyValuePair<string, object?>("template", templateKey),
            new KeyValuePair<string, object?>("kind", kind.ToString()),
            new KeyValuePair<string, object?>("branch", string.IsNullOrEmpty(optionKey) ? "none" : optionKey));

    public void Puzzle(string outcome, string blueprintKey, PetBrainDifficulty difficulty) =>
        _puzzles.Add(1,
            new KeyValuePair<string, object?>("outcome", outcome),
            new KeyValuePair<string, object?>("mechanic", blueprintKey),
            new KeyValuePair<string, object?>("difficulty", difficulty.ToString()));

    public void MemoryCallback(string outcome, PetBrainMemoryKind kind) =>
        _memoryCallbacks.Add(1,
            new KeyValuePair<string, object?>("outcome", outcome),
            new KeyValuePair<string, object?>("kind", kind.ToString()));

    public void NarrativeCache(bool hit) =>
        _narrativeCache.Add(1, new KeyValuePair<string, object?>("result", hit ? "hit" : "miss"));

    /// <summary>
    /// Pet-in niyyəti seçildi, tamamlandı və ya kənara qoyuldu.
    ///
    /// <para>Səbəb açarı qapalı siyahıdandır (<c>low-energy</c>, <c>mission</c>),
    /// ona görə etiket kimi təhlükəsizdir — sərbəst mətn deyil.</para>
    /// </summary>
    public void Intent(string phase, PetBrainIntentType type, string reasonKey) =>
        _intents.Add(1,
            new KeyValuePair<string, object?>("phase", phase),
            new KeyValuePair<string, object?>("type", type.ToString()),
            new KeyValuePair<string, object?>("reason", reasonKey));

    /// <summary>
    /// Modelin təklif etdiyi plan qəbul və ya RƏDD edildi.
    ///
    /// <para>Rədd səbəbi qapalı siyahıdandır: hansı yoxlamanın saxladığını
    /// bilmək validatoru təkmilləşdirmək üçün lazımdır.</para>
    /// </summary>
    public void AiPlan(string outcome, string reason) =>
        _aiPlans.Add(1,
            new KeyValuePair<string, object?>("outcome", outcome),
            new KeyValuePair<string, object?>("reason", reason));

    public void Fallback(string reason) =>
        _fallbacks.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void Conflict(string kind) =>
        _conflicts.Add(1, new KeyValuePair<string, object?>("kind", kind));
}
