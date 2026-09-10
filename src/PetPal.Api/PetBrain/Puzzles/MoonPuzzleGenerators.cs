using PetPal.Api.Common;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// Ay macərasının ÜÇ yeni mexanikası.
///
/// <para>Ayrı fayldadır, çünki <see cref="DeterministicPuzzleGenerator"/>
/// artıq dörd mexanika daşıyır və hamısını bir faylda saxlamaq onu oxunmaz
/// edərdi. Müqavilə eynidir: giriş determinist toxumdur, çıxış isə DTO və
/// saxlanan həll — burada da nə saat, nə baza, nə də <c>Random.Shared</c> var.</para>
///
/// <para><b>Keyfiyyət həddi dəyişmir:</b> hər üçü hekayənin problemini
/// birbaşa həll edir. Naxış tapmacası siqnalı deşifrə edir, müşahidə
/// tapmacası roverin izini oxuyur, uyğunlaşdırma isə kristal parçalarını öz
/// yuvasına qaytarır — heç biri macərəyə yapışdırılmış viktorina deyil.</para>
/// </summary>
public static class MoonPuzzleGenerators
{
    /// <summary>
    /// <b>Siqnal naxışı.</b> Ay siqnalı təkrarlanan qayda ilə gəlir; uşaq
    /// qaydanı nümunədən çıxarıb davamını qurur.
    ///
    /// <para>Qayda MƏTNDƏ yazılmır — yazılsaydı, tapmaca «göstərişi izlə»yə
    /// çevrilərdi. Nümunə iki tam dövr göstərir, yəni qayda tapılan qədər
    /// məlumat var və təxmin etmək lazım gəlmir.</para>
    /// </summary>
    public static (PetBrainPuzzleDto, PuzzleSolution)? BuildSignalPattern(
        PuzzleGenerationContext context, PetBrainDifficulty tier, PuzzleRandom random)
    {
        var language = context.Language;
        var vocabulary = PuzzleBlueprintCatalog.VocabularyFor(context.Theme);

        var cycle = tier switch
        {
            PetBrainDifficulty.Easy => 3,
            PetBrainDifficulty.Hard => 5,
            _ => 4
        };

        if (context.Assisted)
            cycle = Math.Max(PuzzleValidator.MinItems, cycle - 1);

        var answerLength = cycle;
        var alphabet = Enumerable.Range(0, cycle).ToList();
        random.Shuffle(alphabet);

        List<PetBrainPuzzleItemDto> Palette()
        {
            List<PetBrainPuzzleItemDto> palette = [];

            for (var i = 0; i < cycle; i++)
                palette.Add(new PetBrainPuzzleItemDto
                {
                    Id = $"beat-{(char)('a' + i)}",
                    Label = PulseLabel(language, i),
                    Icon = vocabulary.Icons[i % vocabulary.Icons.Count],
                    Shape = PuzzleBlueprintCatalog.Shapes[i % PuzzleBlueprintCatalog.Shapes.Count],
                    Value = i + 1
                });

            return palette;
        }

        var items = Palette();

        List<PetBrainPuzzleItemDto> preview = [];

        for (var repeat = 0; repeat < 2; repeat++)
        foreach (var index in alphabet)
        {
            var beat = items[index];

            preview.Add(new PetBrainPuzzleItemDto
            {
                Id = $"preview-{preview.Count}",
                Label = beat.Label,
                Icon = beat.Icon,
                Shape = beat.Shape,
                Value = beat.Value
            });
        }

        var solution = alphabet.Select(index => items[index].Id).ToList();

        var dto = new PetBrainPuzzleDto
        {
            Mechanic = PetBrainPuzzleMechanic.SignalPattern,
            Title = Localized.T(language, "Siqnalı davam etdir", "Continue the signal"),
            Instruction = Localized.T(language,
                "Siqnal təkrarlanır. Növbəti dövrü sən düz.",
                "The signal repeats. Build the next round yourself."),
            Items = items,
            PatternPreview = preview,
            AnswerSchema = new PetBrainAnswerSchemaDto
            {
                Kind = PetBrainAnswerKind.OrderIds,
                Min = answerLength,
                Max = answerLength
            },
            HintAvailable = true,
            Hint = Localized.T(language,
                $"Naxış hər dəfə {cycle} addımdan sonra baştan başlayır. Əvvəlki dövrə bax.",
                $"The pattern starts over after {cycle} beats. Look at the round before.")
        };

        return (dto, new PuzzleSolution(solution, PetBrainAnswerKind.OrderIds));
    }

    /// <summary>
    /// <b>Müşahidə.</b> Jurnaldakı əvvəlki səhnə ilə indiki səhnəni tutuşdur:
    /// nə dəyişib?
    ///
    /// <para>Bu mexanika ipucu jurnalını TƏLƏB EDİR — «əvvəl necə idi» məlumatı
    /// oradan gəlir. Jurnalı bəzək olmaqdan çıxaran şey budur.</para>
    /// </summary>
    public static (PetBrainPuzzleDto, PuzzleSolution)? BuildObservationRecall(
        PuzzleGenerationContext context, PetBrainDifficulty tier, PuzzleRandom random)
    {
        var language = context.Language;
        var vocabulary = PuzzleBlueprintCatalog.VocabularyFor(context.Theme);

        var total = tier switch
        {
            PetBrainDifficulty.Easy => 4,
            PetBrainDifficulty.Hard => 6,
            _ => 5
        };

        var changed = tier == PetBrainDifficulty.Easy || context.Assisted ? 1 : 2;

        if (total <= changed)
            return null;

        List<PetBrainPuzzleItemDto> now = [];
        List<PetBrainPuzzleItemDto> before = [];

        var order = Enumerable.Range(0, total).ToList();
        random.Shuffle(order);

        var changedIndexes = order.Take(changed).ToHashSet();

        for (var i = 0; i < total; i++)
        {
            var id = $"mark-{(char)('a' + i)}";
            var icon = vocabulary.Icons[i % vocabulary.Icons.Count];
            var shape = PuzzleBlueprintCatalog.Shapes[i % PuzzleBlueprintCatalog.Shapes.Count];

            before.Add(new PetBrainPuzzleItemDto
            {
                Id = $"before-{i}",
                Label = TrackLabel(language, i, dimmed: false),
                Icon = icon,
                Shape = shape,
                Value = i + 1
            });

            var isChanged = changedIndexes.Contains(i);

            now.Add(new PetBrainPuzzleItemDto
            {
                Id = id,
                Label = TrackLabel(language, i, dimmed: isChanged),
                Icon = isChanged ? PuzzleValidator.ChangedMarkIcon : icon,
                Shape = shape,
                Value = i + 1
            });
        }

        var solution = now
            .Where((_, i) => changedIndexes.Contains(i))
            .Select(i => i.Id)
            .ToList();

        var dto = new PetBrainPuzzleDto
        {
            Mechanic = PetBrainPuzzleMechanic.ObservationRecall,
            Title = Localized.T(language, "Nə dəyişdi?", "What changed?"),
            Instruction = changed == 1
                ? Localized.T(language,
                    "Jurnaldakı qeydlə tutuşdur və dəyişən izi seç.",
                    "Compare with your journal note and pick the track that changed.")
                : Localized.T(language,
                    $"Jurnalla tutuşdur və dəyişən {changed} izi seç.",
                    $"Compare with your journal and pick the {changed} tracks that changed."),
            Items = now,
            RecalledScene = before,
            AnswerSchema = new PetBrainAnswerSchemaDto
            {
                Kind = PetBrainAnswerKind.SelectIds,
                Min = changed,
                Max = changed
            },
            HintAvailable = true,
            Hint = Localized.T(language,
                "Sual işarəsi olan izlərə diqqət et — jurnalda onların yerində başqa şey vardı.",
                "Look at the tracks with a question mark — your journal shows something else there.")
        };

        return (dto, new PuzzleSolution(solution, PetBrainAnswerKind.SelectIds));
    }

    /// <summary>
    /// <b>Uyğunlaşdırma.</b> Hər kristal parçası öz yuvasına.
    ///
    /// <para>Cavab SIRALIDIR: sıra «hansı parça hansı yuvaya» məlumatını
    /// daşıyır. Yuvalar sabit qalır, uşaq isə parçaları onların qarşısına
    /// düzür — yəni əlaqə qurulur, siyahı sıralanmır.</para>
    /// </summary>
    public static (PetBrainPuzzleDto, PuzzleSolution)? BuildMatchingPairs(
        PuzzleGenerationContext context, PetBrainDifficulty tier, PuzzleRandom random)
    {
        var language = context.Language;

        var pairs = tier switch
        {
            PetBrainDifficulty.Easy => 3,
            PetBrainDifficulty.Hard => 5,
            _ => 4
        };

        if (context.Assisted)
            pairs = Math.Max(3, pairs - 1);

        var energies = DistinctEnergy(random, pairs);

        if (energies is null)
            return null;

        List<PetBrainPuzzleItemDto> targets = [];
        List<PetBrainPuzzleItemDto> pieces = [];

        for (var i = 0; i < pairs; i++)
        {
            targets.Add(new PetBrainPuzzleItemDto
            {
                Id = $"socket-{(char)('a' + i)}",
                Label = Localized.T(language, $"{energies[i]} vahid yuva", $"{energies[i]}-unit socket"),
                Icon = "🔻",
                Shape = PuzzleBlueprintCatalog.Shapes[i % PuzzleBlueprintCatalog.Shapes.Count],
                Value = energies[i]
            });

            pieces.Add(new PetBrainPuzzleItemDto
            {
                Id = $"shard-{(char)('a' + i)}",
                Label = Localized.T(language, $"{energies[i]} vahid parça", $"{energies[i]}-unit shard"),
                Icon = "💎",
                Shape = PuzzleBlueprintCatalog.Shapes[i % PuzzleBlueprintCatalog.Shapes.Count],
                Value = energies[i]
            });
        }

        var solution = targets
            .Select(target => pieces.First(p => p.Value == target.Value).Id)
            .ToList();

        var shuffled = pieces.ToList();
        random.Shuffle(shuffled);

        var dto = new PetBrainPuzzleDto
        {
            Mechanic = PetBrainPuzzleMechanic.MatchingPairs,
            Title = Localized.T(language, "Parçaları yuvasına qaytar", "Return the shards to their sockets"),
            Instruction = Localized.T(language,
                "Hər yuvanın qarşısına eyni enerjili parçanı qoy.",
                "Place the shard with the same energy in front of each socket."),
            Items = shuffled,
            MatchTargets = targets,
            AnswerSchema = new PetBrainAnswerSchemaDto
            {
                Kind = PetBrainAnswerKind.OrderIds,
                Min = pairs,
                Max = pairs
            },
            HintAvailable = true,
            Hint = Localized.T(language,
                "Yuvanın üstündəki rəqəm parçanın rəqəmi ilə eyni olmalıdır.",
                "The number on the socket must match the number on the shard.")
        };

        return (dto, new PuzzleSolution(solution, PetBrainAnswerKind.OrderIds));
    }

    /// <summary>
    /// Hər addımın ADI fərqlidir — beşinə qədər.
    ///
    /// <para>Təkrarlanan ad iki addımı eyni göstərərdi və uşaq naxışı deyil,
    /// ekranı oxumaqda çətinlik çəkərdi.</para>
    /// </summary>
    private static string PulseLabel(string language, int index) => index switch
    {
        0 => Localized.T(language, "Uzun işıq", "Long light"),
        1 => Localized.T(language, "Qısa işıq", "Short light"),
        2 => Localized.T(language, "Cüt işıq", "Double light"),
        3 => Localized.T(language, "Sakit an", "Quiet beat"),
        _ => Localized.T(language, "Titrək işıq", "Flickering light")
    };

    private static string TrackLabel(string language, int index, bool dimmed)
    {
        var place = Localized.T(language, $"{index + 1}-ci iz", $"Track {index + 1}");

        return dimmed ? Localized.T(language, $"{place} (qeyri-səlis)", $"{place} (blurred)") : place;
    }

    /// <summary>Fərqli enerji dəyərləri — uyğunlaşdırma BİRMƏNALI olmalıdır.</summary>
    private static List<int>? DistinctEnergy(PuzzleRandom random, int count)
    {
        if (count > 8)
            return null;

        var pool = Enumerable.Range(2, 8).ToList();
        random.Shuffle(pool);

        return [.. pool.Take(count)];
    }
}
