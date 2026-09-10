using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>Cavabın nəticəsi. <see cref="Rejected"/> = cavab ümumiyyətlə formaya uyğun deyil.</summary>
public sealed record PuzzleAnswerResult(bool IsCorrect, bool Rejected, string RejectReason)
{
    public static PuzzleAnswerResult Correct() => new(true, false, string.Empty);
    public static PuzzleAnswerResult Wrong() => new(false, false, string.Empty);
    public static PuzzleAnswerResult Invalid(string reason) => new(false, true, reason);
}

/// <summary>
/// Cavabı qiymətləndirən YEGANƏ yer.
///
/// <para>Qiymətləndirmə həmişə SAXLANMIŞ həllə qarşı aparılır. Klientin
/// göndərdiyi gövdədə nə <c>IsCorrect</c>, nə <c>Score</c>, nə <c>Reward</c>,
/// nə də <c>TraitDelta</c> sahəsi var — belə sahələr DTO-da ümumiyyətlə
/// mövcud deyil, ona görə "saxta doğru cavab" göndərmək mümkün deyil.</para>
///
/// <para>Sinif SAFDIR: I/O yoxdur, ona görə birbaşa unit test olunur və
/// generatorun özündən ASILI DEYİL — yəni generasiya səhv etsə, yoxlayıcı
/// onu tutur (bax <see cref="PuzzleValidator"/>).</para>
/// </summary>
public static class PuzzleAnswerEvaluator
{
    /// <summary>Bir cavabda göndərilə bilən ən çox element — sorğu şişirdilə bilməz.</summary>
    public const int MaxAnswerIds = 12;

    public static PuzzleAnswerResult Evaluate(
        PuzzleBlueprint blueprint,
        PetBrainPuzzleDto puzzle,
        PuzzleSolution solution,
        IReadOnlyList<string>? answer)
    {
        if (answer is null || answer.Count == 0)
            return PuzzleAnswerResult.Invalid("empty");

        if (answer.Count > MaxAnswerIds)
            return PuzzleAnswerResult.Invalid("too-many");

        // Naməlum id — tapmacada olmayan elementə (qraf lövhəsində: düyünə)
        // cavab vermək olmaz.
        var known = blueprint.IsGraphBoard
            ? puzzle.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal)
            : puzzle.Items.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);

        if (answer.Any(id => !known.Contains(id)))
            return PuzzleAnswerResult.Invalid("unknown-id");

        // Təkrar id — hər iki sxemdə qadağandır.
        if (answer.Distinct(StringComparer.Ordinal).Count() != answer.Count)
            return PuzzleAnswerResult.Invalid("duplicate-id");

        var schema = puzzle.AnswerSchema;
        if (answer.Count < schema.Min || answer.Count > schema.Max)
            return PuzzleAnswerResult.Invalid("count");

        // Yaradıcı yolda DOĞRU/SƏHV yoxdur: forma düzgündürsə, seçim qəbul edilir.
        // Bu, "hər şey doğrudur" demək deyil — yuxarıdakı yoxlamalar hələ də işləyir.
        if (blueprint.LowPressure)
            return PuzzleAnswerResult.Correct();

        return blueprint.AnswerKind switch
        {
            // Marşrut cavabı SAXLANMIŞ SİYAHI ilə MÜQAYİSƏ EDİLMİR, QAYDALARLA
            // yoxlanılır. Səbəb: uşaq eyni dərəcədə etibarlı, sadəcə başqa
            // marşrut planlaşdıra bilər (məsələn çətin pillədə qaya rəfindən
            // keçən yol). Onun dronu Roboya çatıbsa və antena bərpa olunubsa,
            // xilasetmə BAŞ TUTUB — «mənim yadda saxladığım sıra deyil» demək
            // düşünməni cəzalandırmaq olardı.
            PetBrainAnswerKind.OrderedNodeIds => EvaluateRoute(puzzle, answer),

            PetBrainAnswerKind.OrderIds => answer.SequenceEqual(solution.Ids, StringComparer.Ordinal)
                ? PuzzleAnswerResult.Correct()
                : PuzzleAnswerResult.Wrong(),

            _ => answer.ToHashSet(StringComparer.Ordinal).SetEquals(solution.Ids)
                ? PuzzleAnswerResult.Correct()
                : PuzzleAnswerResult.Wrong()
        };
    }

    /// <summary>
    /// Marşrutu <see cref="RouteRules"/> ilə yoxlayır.
    ///
    /// <para>Uğursuzluğun səbəbi <b>rədd</b> ilə <b>səhv</b> arasında fərq
    /// qoyur: quruluşca mənasız cavab (naməlum düyün, iki addımdan qısa yol)
    /// rədd edilir, oyun daxilindəki uğursuzluq (enerji çatmadı, antenadan
    /// keçmədi) isə NORMAL cəhddir — cəhd sayılır, uşaq ipucu ala bilir.</para>
    /// </summary>
    private static PuzzleAnswerResult EvaluateRoute(PetBrainPuzzleDto puzzle, IReadOnlyList<string> answer)
    {
        var check = RouteRules.Check(puzzle, answer);

        if (check.IsValid)
            return PuzzleAnswerResult.Correct();

        return check.Reason is "unknown-node" or "too-short"
            ? PuzzleAnswerResult.Invalid(check.Reason)
            : PuzzleAnswerResult.Wrong();
    }
}
