using PetPal.Api.Common;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>Bir ipucu pilləsinin uşağa göstərilən hissəsi.</summary>
/// <param name="Level">Hazırkı pillə.</param>
/// <param name="Text">Uşağın dilində cümlə; pillə yoxdursa boş.</param>
/// <param name="RevealedIds">Cavabın ARTIQ açılmış addımları — ekran onları işarələyir.</param>
/// <param name="AssistAvailable">Pet «birlikdə edək» təklif edə bilər.</param>
public sealed record HintStep(
    PetBrainHintLevel Level,
    string Text,
    IReadOnlyList<string> RevealedIds,
    bool AssistAvailable)
{
    public static HintStep None { get; } = new(PetBrainHintLevel.None, string.Empty, [], false);
}

/// <summary>
/// İpucu NƏRDİVANI — yumşaq uğursuzluğun mərkəzi.
///
/// <para>Uşaq ilişəndə sistemin ona verə biləcəyi cavablar bir sırada durur:
/// ruhlandırma → istiqamət → nümunə → cavabın yarısı → birgə tamamlama. Hər
/// ipucu istəyi bir pillə qalxır, səhv cavab isə yalnız ruhlandırma verir.
/// Beləliklə uşaq heç vaxt macərədən çıxarılmır və heç vaxt fəsli yenidən
/// başlamağa məcbur olmur.</para>
///
/// <para><b>Birinci istək İSTİQAMƏT verir</b>, ruhlandırma yox: ruhlandırma
/// artıq səhv cavabın özündən sonra avtomatik gəlir. İpucu düyməsini basan
/// uşaq real kömək istəyir və «tələsmə» eşitmək ona kömək deyil.</para>
///
/// <para>Sinif SAFDIR: həll serverdən gəlir, nəticə isə yalnız açılmış
/// addımları daşıyır — tam həll heç vaxt klientə getmir, son pillədə belə.</para>
/// </summary>
public static class HintLadder
{
    /// <summary>Neçə ipucu istəyindən sonra pet birgə tamamlamanı təklif edir.</summary>
    public const int AssistAfterHints = 4;

    public static PetBrainHintLevel LevelFor(int hintsUsed, int attempts) => hintsUsed switch
    {
        <= 0 => attempts > 0 ? PetBrainHintLevel.GentlePrompt : PetBrainHintLevel.None,
        1 => PetBrainHintLevel.DirectionalHint,
        2 => PetBrainHintLevel.WorkedExample,
        3 => PetBrainHintLevel.StepByStepHelp,
        _ => PetBrainHintLevel.AssistedCompletion
    };

    /// <summary>
    /// Cari pillənin göstərilən hissəsi.
    ///
    /// <para>Təzyiqsiz (yaradıcı) tapmacada cavab «açılmır»: orada doğru həll
    /// yoxdur, ona görə nümunə və yarım cavab mənasızdır — yalnız istiqamət
    /// və birgə tamamlama qalır.</para>
    /// </summary>
    public static HintStep For(
        int hintsUsed,
        int attempts,
        PetBrainPuzzleDto puzzle,
        PuzzleSolution solution,
        bool lowPressure,
        string directionalHint,
        string language)
    {
        var level = LevelFor(hintsUsed, attempts);

        if (level == PetBrainHintLevel.None)
            return HintStep.None;

        if (level == PetBrainHintLevel.GentlePrompt)
            return new HintStep(level,
                Localized.T(language,
                    "Tələsmə — hər şeyə bir də bax. Bu dəfə alınacaq.",
                    "No rush — take another look at everything. You will get it this time."),
                [], false);

        var revealCount = lowPressure ? 0 : RevealCount(level, solution.Ids.Count);
        var revealed = solution.Ids.Take(revealCount).ToList();
        var labels = revealed.Select(id => LabelOf(puzzle, id)).ToList();

        var text = level switch
        {
            PetBrainHintLevel.WorkedExample when labels.Count > 0 => Localized.T(language,
                $"{directionalHint} Birinci addım budur: «{labels[0]}». Qalanını sən tap.",
                $"{directionalHint} Here is the first step: «{labels[0]}». You find the rest."),

            PetBrainHintLevel.StepByStepHelp when labels.Count > 0 => Localized.T(language,
                $"Bu addımlar artıq yerindədir: {string.Join(", ", labels)}. Sonrakını sən seç.",
                $"These steps are already in place: {string.Join(", ", labels)}. Pick the next one."),

            PetBrainHintLevel.AssistedCompletion => Localized.T(language,
                "Bu, çətin idi və sən dayanmadın. İstəsən, gəl onu birlikdə bitirək.",
                "That was a tough one and you kept going. If you like, let us finish it together."),

            _ => directionalHint
        };

        return new HintStep(
            level,
            text,
            revealed,
            AssistAvailable: level == PetBrainHintLevel.AssistedCompletion);
    }

    /// <summary>
    /// Neçə addım açılır.
    ///
    /// <para>Son pillədə də cavabın hamısı açılmır: birgə tamamlama serverdə
    /// baş verir, klientə isə yalnız yarısı gedir. Tam həlli göndərmək onu
    /// ekrandan oxumağa imkan verərdi.</para>
    /// </summary>
    private static int RevealCount(PetBrainHintLevel level, int solutionLength) => level switch
    {
        PetBrainHintLevel.WorkedExample => Math.Min(1, solutionLength),
        PetBrainHintLevel.StepByStepHelp or PetBrainHintLevel.AssistedCompletion =>
            Math.Max(1, solutionLength / 2),
        _ => 0
    };

    private static string LabelOf(PetBrainPuzzleDto puzzle, string id) =>
        puzzle.Items.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.Ordinal))?.Label
        ?? puzzle.Nodes.FirstOrDefault(n => string.Equals(n.Id, id, StringComparison.Ordinal))?.Label
        ?? id;
}
