using PetPal.Api.Common;
using PetPal.Api.Learning;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Puzzles;

/// <summary>
/// Uşağa xas tapmaca yaradan determinist generator.
///
/// <para>Boru xətti belədir:</para>
/// <code>
/// etibarlı kontekst → şablon süzgəci → mexanika/mövzu balı → toxumlanmış
/// generasiya → həll və təhlükəsizlik yoxlaması → təkrar yoxlaması →
/// (uğursuzsa) determinist yenidən cəhd → (yenə uğursuzsa) hazır ehtiyat
/// </code>
///
/// <para><b>Şəxsiləşdirmə ad dəyişmək deyil.</b> Oyun üslubu MEXANİKANI seçir,
/// maraqlar MÖVZUNU və lüğəti çəkir, mənimsəmə və son nəticə isə çətinliyi
/// incələyir. Ona görə həlledici uşaqla yaradıcı uşaq eyni macərada belə
/// fərqli DÜŞÜNMƏ tələb edən tapmaca alır.</para>
///
/// <para>Nə model, nə şəbəkə, nə də saat lazımdır — sinif tamamilə safdır.</para>
/// </summary>
public sealed class DeterministicPuzzleGenerator : IPersonalizedPuzzleGenerator
{
    /// <summary>Bir şablon üçün determinist yenidən cəhd sayı.</summary>
    private const int MaxAttemptsPerBlueprint = 3;

    public GeneratedPuzzle Generate(PuzzleGenerationContext context)
    {
        foreach (var blueprint in Rank(context))
        {
            // Hədəf MEXANİKAYA görə hesablanır: uşaq marşrutda güclü,
            // sıralamada təzə ola bilər və tək rəqəm bunu gizlədir.
            var target = TargetDifficulty(context, blueprint.Key);

            for (var attempt = 0; attempt < MaxAttemptsPerBlueprint; attempt++)
            {
                var seedHex = PuzzleSeed.Hex(
                    context.ChildId, context.RunId, blueprint.Key, blueprint.Version, target, attempt);

                var candidate = Build(blueprint, context, target, seedHex, attempt);
                if (candidate is null)
                    continue;

                // Müstəqil yoxlayıcı — generator səhv etsə burada dayanır.
                if (PuzzleValidator.Validate(blueprint, candidate.Public, candidate.Solution) is not null)
                    continue;

                // Eyni sual dalbadal görünməsin.
                if (context.RecentSignatures.Contains(candidate.Signature))
                    continue;

                return candidate;
            }
        }

        return Fallback(context, TargetDifficulty(context, blueprintKey: null));
    }

    // ==================== Şablon seçimi ====================

    /// <summary>
    /// Namizədlər bala görə sıralanır: <b>70% oyun üslubu</b> (mexanikanı o
    /// seçir) + <b>30% maraq</b>. Bərabərlikdə açarın əlifba sırası —
    /// nəticə prosesdən-prosesə eynidir.
    /// </summary>
    public static IReadOnlyList<PuzzleBlueprint> Rank(PuzzleGenerationContext context) =>
        [.. PuzzleBlueprintCatalog.Blueprints
            .Where(b => b.FitsExperienceType(context.ExperienceType)
                        && context.Age >= b.MinAge
                        && b.OfferedFor(context.PreferredBlueprintKey)

                        // Hekayə mətni daşıyan mexanika YALNIZ öz macərasında
                        // işlədilir. Bu süzgəc olmasaydı, «həlledici» profilli
                        // uşaq Ay macərasında Marsın Robosunu xilas etməyə
                        // çağırılardı — mexanika uyğun, hekayə yad.
                        && b.SupportsExperience(context.TemplateKey))

            // Hekayənin istədiyi mexanika ƏVVƏLƏ keçir — amma yalnız yuxarıdakı
            // süzgəcdən keçibsə: düyün uyğunsuz şablon istəsə, o, sadəcə
            // nəzərə alınmır.
            .OrderByDescending(b => string.Equals(b.Key, context.PreferredBlueprintKey, StringComparison.Ordinal))
            .ThenByDescending(b => Score(b, context))
            .ThenBy(b => b.Key, StringComparer.Ordinal)];

    private static double Score(PuzzleBlueprint blueprint, PuzzleGenerationContext context)
    {
        var style = Average(blueprint.PlayStyles, context.PlayStyles);
        var interest = Average(blueprint.Interests, context.Interests);

        return (0.7 * style) + (0.3 * interest);
    }

    private static double Average(IReadOnlyList<string> keys, IReadOnlyDictionary<string, int> scores) =>
        keys.Count == 0
            ? TraitKeys.StartingScore
            : keys.Average(key => scores.TryGetValue(key, out var value)
                ? TraitKeys.Clamp(value)
                : TraitKeys.StartingScore);

    /// <summary>
    /// Hədəf çətinlik (1–10). Təcrübə pilləsi ilə mövcud
    /// <see cref="AdaptiveEngine.TargetDifficulty"/> dəyəri BİRLƏŞDİRİLİR —
    /// ikinci akademik reytinq sistemi yaradılmır.
    ///
    /// <para>Yaş TAVANDIR və hər şeyin üstündədir.</para>
    /// </summary>
    public static int TargetDifficulty(PuzzleGenerationContext context) =>
        TargetDifficulty(context, blueprintKey: null);

    /// <param name="blueprintKey">
    /// Hansı mexanika üçün. Verilibsə və uşağın orada tarixçəsi varsa, pillə
    /// MƏHZ o mexanikadan götürülür — qlobaldan yox.
    /// </param>
    public static int TargetDifficulty(PuzzleGenerationContext context, string? blueprintKey)
    {
        var tier = blueprintKey is not null
                   && context.MechanicTiers.TryGetValue(blueprintKey, out var mechanic)
            ? mechanic
            : context.Difficulty;

        var tierBase = tier switch
        {
            PetBrainDifficulty.Easy => 2,
            PetBrainDifficulty.Hard => 8,
            _ => 5
        };

        var mastery = Math.Clamp(context.MasteryTargetDifficulty, 1, 10);
        var blended = (int)Math.Round((tierBase + mastery) / 2.0, MidpointRounding.AwayFromZero);

        // Kömək lazımdırsa bir pillə yumşaldılır — bu, cəza deyil, dəstəkdir.
        if (context.Assisted)
            blended--;

        var ceiling = context.Age switch
        {
            <= 6 => 4,
            <= 8 => 7,
            _ => 10
        };

        return Math.Clamp(blended, 1, ceiling);
    }

    /// <summary>Hədəf rəqəmindən görünən quruluş pilləsi.</summary>
    public static PetBrainDifficulty TierOf(int target) => target switch
    {
        <= 3 => PetBrainDifficulty.Easy,
        <= 6 => PetBrainDifficulty.Medium,
        _ => PetBrainDifficulty.Hard
    };

    // ==================== Qurma ====================

    private static GeneratedPuzzle? Build(
        PuzzleBlueprint blueprint,
        PuzzleGenerationContext context,
        int target,
        string seedHex,
        int attempt)
    {
        var random = new PuzzleRandom(seedHex);
        var tier = TierOf(target);

        var puzzle = blueprint.Mechanic switch
        {
            // Marşrutun QAYDASI birdir, HEKAYƏSİ isə şablonun paketindən gəlir —
            // yəni Ay macərası heç vaxt Marsın mətnini almır.
            PetBrainPuzzleMechanic.OrderedRoute when RouteStoryPacks.For(blueprint.Key) is { } pack
                => BuildOrderedRoute(context, tier, random, pack),
            PetBrainPuzzleMechanic.SequenceOrder => BuildSequenceOrder(context, tier, random),
            PetBrainPuzzleMechanic.RouteLogic => BuildRouteLogic(context, tier, random),
            PetBrainPuzzleMechanic.LightFragments => BuildLightFragments(context, tier, random),
            PetBrainPuzzleMechanic.SignalPattern
                => MoonPuzzleGenerators.BuildSignalPattern(context, tier, random),
            PetBrainPuzzleMechanic.ObservationRecall
                => MoonPuzzleGenerators.BuildObservationRecall(context, tier, random),
            PetBrainPuzzleMechanic.MatchingPairs
                => MoonPuzzleGenerators.BuildMatchingPairs(context, tier, random),
            PetBrainPuzzleMechanic.PictureAssembly => BuildPictureAssembly(context, tier, random),
            _ => null
        };

        if (puzzle is null)
            return null;

        var (dto, solution) = puzzle.Value;

        dto.BlueprintKey = blueprint.Key;
        dto.BlueprintVersion = blueprint.Version;

        return new GeneratedPuzzle(
            blueprint, dto, solution, Signature(blueprint, dto), seedHex, attempt, UsedFallback: false);
    }

    /// <summary>
    /// Təkrarı tutan imza. Qraf lövhəsində elementlər YOXDUR, ona görə imza
    /// düyün rolları, keçidlər və enerji büdcəsi üzərindən qurulur — eyni
    /// quruluş dalbadal iki dəfə verilməsin.
    /// </summary>
    private static string Signature(PuzzleBlueprint blueprint, PetBrainPuzzleDto dto) =>
        blueprint.IsGraphBoard
            ? PuzzleSeed.Signature(
                blueprint.Key,
                dto.Nodes.Select(n => $"{n.Id}:{(int)n.Kind}:{n.EnergyDelta}")
                    .Concat(dto.Edges.Select(e => $"{e.From}>{e.To}"))
                    .Append($"e:{dto.InitialEnergy}/{dto.MaximumEnergy}/{dto.MoveCost}"))
            : PuzzleSeed.Signature(
                blueprint.Key,
                dto.Items.Select(i => $"{i.Id}:{i.Value}:{i.Icon}").Append($"t:{dto.TargetValue}"));

    /// <summary>Elementin sayı pillədən gəlir; dəstək rejimi birini azaldır.</summary>
    private static int ItemCount(PetBrainDifficulty tier, bool assisted, int max)
    {
        var count = tier switch
        {
            PetBrainDifficulty.Easy => 3,
            PetBrainDifficulty.Hard => 5,
            _ => 4
        };

        if (assisted)
            count--;

        return Math.Clamp(count, PuzzleValidator.MinItems, max);
    }

    // ---------- Referans mexanika: dron marşrutu ----------

    /// <summary>
    /// Marsda rabitə bərpası — <b>referans keyfiyyət həddi</b>.
    ///
    /// <para>Uşaq ayrıca sual cavablandırmır: o, təmir dronunu planlaşdırır.
    /// Enerji çatmalıdır, antena bərpa olunmalıdır, yalnız bundan sonra Roboya
    /// çatmaq olar. Yəni düşünmə hərəkəti xilasetmənin ÖZÜDÜR.</para>
    ///
    /// <para>Lövhədə inandırıcı TƏLƏ var: mağara yolu Roboya çatır və qısadır,
    /// amma antenadan keçmədiyi üçün siqnal bərpa olunmur. Uşaq bunu yalnız
    /// hekayəni oxuyanda anlayır — təsadüfi tapmaq mümkün deyil.</para>
    /// </summary>
    private static (PetBrainPuzzleDto, PuzzleSolution)? BuildOrderedRoute(
        PuzzleGenerationContext context, PetBrainDifficulty tier, PuzzleRandom random, RouteStoryPack pack)
    {
        var language = context.Language;

        // Enerji dəyərləri pilləyə görə dəyişir: asan pillədə hesablama
        // praktiki olaraq maneə deyil, çətin pillədə isə hər addım sayılır.
        var (initialEnergy, recharge) = tier switch
        {
            PetBrainDifficulty.Easy => (5, 2),
            PetBrainDifficulty.Hard => (2, 2),
            _ => (3, 2)
        };

        // Çətin pillədə enerji büdcəsi TOXUNULMAZDIR: orada paralel qaya rəfi
        // yolu var və artıq bir vahid onu da mümkün edərdi — yəni tapmacanın
        // iki həlli olardı. Aşağı pillələrdə paralel yol yoxdur, ona görə
        // büdcəni oynatmaq təhlükəsizdir.
        if (tier != PetBrainDifficulty.Hard)
            initialEnergy += random.Next(0, 2);

        var maximumEnergy = initialEnergy + 1;

        // Lövhə hər run-da EYNİ olmamalıdır: uşaq marşrutu düşünmək əvəzinə
        // əzbərləyərdi. Ona görə iki ox toxumdan seçilir — yaxınlaşma yolu və
        // tələnin haradan ayrıldığı. Hər ikisi HƏNDƏSƏNİ dəyişir, qaydanı yox.
        var approach = random.Next(0, 2) == 0
            ? Node("ridge", PetBrainNodeKind.Path, 29, 65,
                Localized.T(language, pack.RidgeAz, pack.RidgeEn), pack.RidgeIcon)
            : Node("dune", PetBrainNodeKind.Path, 26, 58,
                Localized.T(language, pack.DuneAz, pack.DuneEn), pack.DuneIcon);

        // Tələ ya elə eniş modulundan ayrılır (uşaq dərhal seçim qarşısında
        // qalır), ya da yolun ortasında görünür (əvvəlcə düz getdiyini
        // sanır). İkinci hal daha çətindir, çünki tələ gec üzə çıxır.
        var decoyFrom = random.Next(0, 2) == 0 ? "lander" : approach.Id;

        List<PetBrainNodeDto> nodes =
        [
            Node("lander", PetBrainNodeKind.Start, 12, 82,
                Localized.T(language, pack.StartAz, pack.StartEn), pack.StartIcon),
            approach,
            Node("solar", PetBrainNodeKind.Recharge, 47, 77,
                Localized.T(language, pack.RechargeAz, pack.RechargeEn), pack.RechargeIcon, recharge),
            Node("antenna", PetBrainNodeKind.Required, 61, 50,
                Localized.T(language, pack.RequiredAz, pack.RequiredEn), pack.RequiredIcon),
            Node("cave", PetBrainNodeKind.Decoy, 31, 38,
                Localized.T(language, pack.DecoyAz, pack.DecoyEn), pack.DecoyIcon),
            Node("robo", PetBrainNodeKind.Goal, 82, 25,
                Localized.T(language, pack.GoalAz, pack.GoalEn), pack.GoalIcon)
        ];

        List<PetBrainEdgeDto> edges =
        [
            Edge("lander", approach.Id),
            Edge(decoyFrom, "cave"),
            Edge(approach.Id, "solar"),
            Edge("solar", "antenna"),
            Edge("antenna", "robo"),
            Edge("cave", "robo")
        ];

        // Çətin pillədə ikinci doldurma məhdudiyyəti: əlavə silsilə yolu uzadır,
        // yəni enerjini düzgün bölmək lazım gəlir. Tələ dili YOXDUR — sadəcə
        // bir addım artıq.
        if (tier == PetBrainDifficulty.Hard)
        {
            nodes.Insert(4, Node("shelf", PetBrainNodeKind.Path, 46, 58,
                Localized.T(language, pack.ShelfAz, pack.ShelfEn), pack.ShelfIcon));

            edges.Add(Edge("solar", "shelf"));
            edges.Add(Edge("shelf", "antenna"));
        }

        // Asan pillədə tələ ümumiyyətlə qurulmur: uşaq əvvəlcə qaydanı
        // öyrənməlidir, sonra ona tələ qurmaq olar.
        if (tier == PetBrainDifficulty.Easy || context.Assisted)
        {
            nodes.RemoveAll(n => n.Id == "cave");
            edges.RemoveAll(e => e.From == "cave" || e.To == "cave");
        }

        var dto = new PetBrainPuzzleDto
        {
            Mechanic = PetBrainPuzzleMechanic.OrderedRoute,
            Title = pack.Title(language),
            StoryPrompt = pack.Story(language),
            Instruction = pack.Instruction(language),
            Nodes = nodes,
            Edges = edges,

            // Səhnə DETERMİNİSTİK açılır. AI rəsmi (açıqdırsa) yalnız fonu
            // əvəzləyir — həndəsə, toxunuş hədəfləri və qaydalar dəyişmir.
            Scene = new PetBrainSceneDto
            {
                IllustrationStatus = PetBrainIllustrationStatus.Fallback,

                // Overlay HƏNDƏSƏSİ hər iki marşrut paketində eynidir — komponent
                // qraf lövhəsini eyni cür çəkir, dəyişən yalnız hekayədir.
                OverlayLayout = PuzzleBlueprintCatalog.MarsSignalRouteKey,
                AltText = pack.AltText(language)
            },
            InitialEnergy = initialEnergy,
            MaximumEnergy = maximumEnergy,
            MoveCost = 1,
            RequiredBeforeGoal = ["antenna"],
            AnswerSchema = new PetBrainAnswerSchemaDto
            {
                Kind = PetBrainAnswerKind.OrderedNodeIds,

                // Alt hədd qəsdən aşağıdır: TƏLƏ yolu (3 düyün) formaca
                // düzgün olmalıdır ki, «səhv cavab» kimi qiymətləndirilsin.
                // Sxem onu rədd etsəydi, uşaq niyə işləmədiyini öyrənməzdi.
                Min = 3,
                Max = nodes.Count
            },
            HintAvailable = true,
            Hint = pack.Hint(language),
            AssistHighlight = tier == PetBrainDifficulty.Easy || context.Assisted
        };

        // Həll GENERASİYA OLUNMUR, TAPILIR: qaydaları elə həmin yoxlayıcı
        // tətbiq edir, yəni saxlanan cavab qaydalardan kənara düşə bilməz.
        var solution = RouteRules.FindSolution(dto, dto.AnswerSchema.Max);
        if (solution is null)
            return null;

        return (dto, new PuzzleSolution(solution, PetBrainAnswerKind.OrderedNodeIds));
    }

    private static PetBrainNodeDto Node(
        string id, PetBrainNodeKind kind, int x, int y, string label, string icon, int? energyDelta = null) =>
        new()
        {
            Id = id,
            Kind = kind,
            X = x,
            Y = y,
            Label = label,
            Icon = icon,
            EnergyDelta = energyDelta
        };

    private static PetBrainEdgeDto Edge(string from, string to) => new() { From = from, To = to };

    // ---------- Ardıcıllıq ----------

    private static (PetBrainPuzzleDto, PuzzleSolution)? BuildSequenceOrder(
        PuzzleGenerationContext context, PetBrainDifficulty tier, PuzzleRandom random)
    {
        var count = ItemCount(tier, context.Assisted, max: 5);

        var start = random.Next(1, 6);
        var step = tier == PetBrainDifficulty.Easy ? 1 : random.Next(2, 4);

        var ordered = Enumerable.Range(0, count).Select(i => start + (i * step)).ToList();

        // Elementlər QARIŞIQ göstərilir — sıralamaq uşağın işidir.
        var shuffled = ordered.ToList();
        random.Shuffle(shuffled);

        var items = BuildItems(context, shuffled, v => v.ToString());

        var dto = new PetBrainPuzzleDto
        {
            Mechanic = PetBrainPuzzleMechanic.SequenceOrder,
            Title = Localized.T(context.Language, "Sıraya düz", "Put them in order"),
            Instruction = Localized.T(context.Language,
                "Kiçikdən böyüyə doğru sırala.",
                "Arrange them from smallest to largest."),
            Items = items,
            AnswerSchema = new PetBrainAnswerSchemaDto
            {
                Kind = PetBrainAnswerKind.OrderIds,
                Min = count,
                Max = count
            },
            HintAvailable = true,
            Hint = Localized.T(context.Language,
                $"Ən kiçik {ordered[0]}-dir. Sonra hər dəfə {step} artır.",
                $"The smallest is {ordered[0]}. After that it grows by {step} each time.")
        };

        var solutionIds = ordered
            .Select(value => items.First(i => i.Value == value).Id)
            .ToList();

        return (dto, new PuzzleSolution(solutionIds, PetBrainAnswerKind.OrderIds));
    }

    // ---------- Marşrut məntiqi ----------

    private static (PetBrainPuzzleDto, PuzzleSolution)? BuildRouteLogic(
        PuzzleGenerationContext context, PetBrainDifficulty tier, PuzzleRandom random)
    {
        var count = ItemCount(tier, context.Assisted, max: 4);

        // Uzunluqlar fərqlidir ki, "ən qısa" birmənalı olsun.
        var lengths = DistinctValues(random, count, 2, 10);
        if (lengths is null)
            return null;

        // Bağlı yolların sayı pilləyə görə artır — çətinlik burada gəlir.
        var hazards = tier switch
        {
            PetBrainDifficulty.Easy => 1,
            PetBrainDifficulty.Hard => count - 2,
            _ => Math.Max(1, count - 3)
        };

        var indexes = Enumerable.Range(0, count).ToList();
        random.Shuffle(indexes);

        var hazardous = indexes.Take(hazards).ToHashSet();

        // Ən qısa AÇIQ yol həlldir. Bağlı yollardan biri qəsdən daha qısadır —
        // uşaq yalnız uzunluğa baxsa səhv edər.
        var open = Enumerable.Range(0, count).Where(i => !hazardous.Contains(i)).ToList();
        if (open.Count == 0)
            return null;

        var answerIndex = open.OrderBy(i => lengths[i]).First();

        var vocabulary = PuzzleBlueprintCatalog.VocabularyFor(context.Theme);

        List<PetBrainPuzzleItemDto> items = [];
        for (var i = 0; i < count; i++)
        {
            var blocked = hazardous.Contains(i);

            items.Add(new PetBrainPuzzleItemDto
            {
                Id = $"route-{(char)('a' + i)}",
                Label = Localized.T(context.Language,
                    blocked ? $"{lengths[i]} addım · bağlı" : $"{lengths[i]} addım",
                    blocked ? $"{lengths[i]} steps · blocked" : $"{lengths[i]} steps"),
                Icon = blocked ? PuzzleBlueprintCatalog.HazardIcon : vocabulary.Icons[i % vocabulary.Icons.Count],
                Shape = blocked ? "triangle" : "circle",
                Value = lengths[i]
            });
        }

        var dto = new PetBrainPuzzleDto
        {
            Mechanic = PetBrainPuzzleMechanic.RouteLogic,
            Title = Localized.T(context.Language, "Təhlükəsiz yolu seç", "Choose the safe path"),
            Instruction = Localized.T(context.Language,
                $"{PuzzleBlueprintCatalog.HazardIcon} işarəli yollar bağlıdır. Açıq yollardan ƏN QISASINI seç.",
                $"Paths marked {PuzzleBlueprintCatalog.HazardIcon} are blocked. Pick the SHORTEST open path."),
            Items = items,
            AnswerSchema = new PetBrainAnswerSchemaDto
            {
                Kind = PetBrainAnswerKind.SelectIds,
                Min = 1,
                Max = 1
            },
            HintAvailable = true,
            Hint = Localized.T(context.Language,
                "Əvvəlcə bağlı yolları kənara qoy, sonra qalanların addımlarını müqayisə et.",
                "First set the blocked paths aside, then compare the steps of the rest.")
        };

        return (dto, new PuzzleSolution([items[answerIndex].Id], PetBrainAnswerKind.SelectIds));
    }

    // ---------- İşıq parçaları (təzyiqsiz) ----------

    /// <summary>
    /// Əjdahanın qanad naxışını işıq parçalarından BƏRPA etmək.
    ///
    /// <para>Marsdakı məntiq lövhəsi ilə eyni keyfiyyət həddi, amma tamam
    /// başqa qarşılıqlı təsir: uşaq düşünmür, QURUR. Qanadda boş yuvalar var,
    /// hekayə ipucu isə naxışın necə parıldadığını xatırladır.</para>
    ///
    /// <para><b>Doğru/səhv yoxdur.</b> Bir neçə palitra eyni dərəcədə
    /// doğrudur — yuvalar dolanda səhnəyə işıq və rəng qayıdır. Cəza dili
    /// heç bir pillədə işlədilmir.</para>
    /// </summary>
    private static (PetBrainPuzzleDto, PuzzleSolution)? BuildLightFragments(
        PuzzleGenerationContext context, PetBrainDifficulty tier, PuzzleRandom random)
    {
        var language = context.Language;

        var pieces = PuzzleBlueprintCatalog.AssemblyPieces.ToList();
        random.Shuffle(pieces);

        // Yuva sayı pilləyə görə artır — TƏZYİQ yox, kompozisiya böyüyür.
        var slotCount = tier == PetBrainDifficulty.Easy || context.Assisted ? 2 : 3;
        var offered = Math.Min(pieces.Count, slotCount + 2);

        var shown = pieces.Take(offered).ToList();

        var items = shown.Select(piece => new PetBrainPuzzleItemDto
        {
            Id = piece.Id,
            Label = Localized.T(language, piece.LabelAz, piece.LabelEn),
            Icon = piece.Icon,
            Shape = piece.Shape
        }).ToList();

        // Yuvalar qanadın üstündə sabit koordinatdadır — ehtiyat rəsmdə də,
        // AI rəsmində də EYNİ yerdə qalır.
        var slotAnchors = new[] { (34, 46), (52, 38), (46, 58), (63, 52) };

        var slots = Enumerable.Range(0, slotCount)
            .Select(i => new PetBrainSlotDto
            {
                Id = $"slot-{i + 1}",
                X = slotAnchors[i % slotAnchors.Length].Item1,
                Y = slotAnchors[i % slotAnchors.Length].Item2
            })
            .ToList();

        var dto = new PetBrainPuzzleDto
        {
            Mechanic = PetBrainPuzzleMechanic.LightFragments,
            Title = Localized.T(language, "Qanadın işığını qaytar", "Bring the wing back to light"),
            StoryPrompt = Localized.T(language,
                "Kristal bağçada işıq söndü və əjdahanın qanadı sonuncu naxışını itirdi.",
                "The light went out in the crystal garden and the dragon's wing lost its last pattern."),
            Instruction = slotCount == 2
                ? Localized.T(language,
                    "İki işıq parçası seç — hansını seçsən, qanad elə parıldayacaq.",
                    "Pick two light fragments — whichever you choose is how the wing will shine.")
                : Localized.T(language,
                    "Üç işıq parçası seç — hansını seçsən, qanad elə parıldayacaq.",
                    "Pick three light fragments — whichever you choose is how the wing will shine."),
            Items = items,
            Slots = slots,
            Scene = new PetBrainSceneDto
            {
                IllustrationStatus = PetBrainIllustrationStatus.Fallback,
                OverlayLayout = PuzzleBlueprintCatalog.LightFragmentsKey,
                AltText = Localized.T(language,
                    "Kristal bağçada əjdahanın qanadı — naxış yuvaları hələ boşdur.",
                    "The dragon's wing in the crystal garden — the pattern slots are still empty.")
            },

            // Hekayə ipucu naxışın ƏHVALINI xatırladır, cavabı yox: hər parça
            // etibarlıdır, ona görə burada gizlədiləsi bir şey də yoxdur.
            ClueIcons = [.. shown.Take(2).Select(p => p.Icon)],
            AnswerSchema = new PetBrainAnswerSchemaDto
            {
                Kind = PetBrainAnswerKind.SelectIds,
                Min = slotCount,
                Max = slotCount
            },

            // Təzyiqsiz yolda ipucu mənasızdır: səhv cavab yoxdur ki, kömək lazım olsun.
            HintAvailable = false,
            LowPressure = true
        };

        // "Həll" yalnız arayışdır — qiymətləndirici hər etibarlı seçimi qəbul edir.
        var reference = items.Take(slotCount).Select(i => i.Id).ToList();

        return (dto, new PuzzleSolution(reference, PetBrainAnswerKind.SelectIds));
    }

    /// <summary>
    /// Macəranın ŞƏKLİNİ yığmaq — hekayə rəsmi parçalara bölünür, uşaq onu
    /// çərçivəyə qaytarır.
    ///
    /// <para>Parça sayı pillədən gəlir: asan 2×3, orta 3×3, çətin 3×4; dəstək
    /// rejimi bir pillə kiçildir. Parçaların id-ləri toxumla qarışdırılır, ona
    /// görə id-nin adı yeri açmır; qabdakı sıra isə heç vaxt yığılmış sıra ilə
    /// eyni çıxmır.</para>
    ///
    /// <para><b>Rəsm yenə yalnız görüntüdür.</b> Parçanın <c>Value</c>-su onun
    /// çərçivədəki yeridir və overlay şəklin həmin hissəsini məhz bu rəqəmdən
    /// kəsir. Rəsm gəlməsə deterministik şəkil eyni həndəsə ilə kəsilir, cavab
    /// dəyişmir: doğruluq serverdə, saxlanmış sıraya qarşı yoxlanılır.</para>
    /// </summary>
    private static (PetBrainPuzzleDto, PuzzleSolution)? BuildPictureAssembly(
        PuzzleGenerationContext context, PetBrainDifficulty tier, PuzzleRandom random)
    {
        var language = context.Language;
        var (columns, rows) = PictureGrid(context.Assisted ? Easier(tier) : tier);
        var count = columns * rows;

        var ids = PictureTileIds.ToList();
        random.Shuffle(ids);

        var solution = ids.Take(count).ToList();

        var tray = Enumerable.Range(0, count).ToList();
        random.Shuffle(tray);

        if (tray.SequenceEqual(Enumerable.Range(0, count)))
            tray = [.. tray.Skip(1), tray[0]];

        var items = tray
            .Select((slot, position) => new PetBrainPuzzleItemDto
            {
                Id = solution[slot],
                Label = Localized.T(language, $"Parça {position + 1}", $"Piece {position + 1}"),
                Icon = PictureTileIcon,
                Shape = PuzzleBlueprintCatalog.Shapes[position % PuzzleBlueprintCatalog.Shapes.Count],
                Value = slot
            })
            .ToList();

        var dto = new PetBrainPuzzleDto
        {
            Mechanic = PetBrainPuzzleMechanic.PictureAssembly,
            Title = Localized.T(language, "Şəkli yenidən yığ", "Put the picture back together"),
            StoryPrompt = Localized.T(language,
                "Macəramızın şəkli parçalara ayrıldı. Onu yığ ki, pet albomuna yapışdırsın.",
                "The picture of our adventure fell into pieces. Put it together so the pet can add it to the album."),
            Instruction = Localized.T(language,
                "Parçaya toxun, sonra çərçivədə onun yerinə toxun.",
                "Tap a piece, then tap its spot in the frame."),
            Items = items,
            GridColumns = columns,
            GridRows = rows,
            Scene = new PetBrainSceneDto
            {
                IllustrationStatus = PetBrainIllustrationStatus.Fallback,
                OverlayLayout = PuzzleBlueprintCatalog.SceneJigsawKey
            },
            AnswerSchema = new PetBrainAnswerSchemaDto
            {
                Kind = PetBrainAnswerKind.OrderIds,
                Min = count,
                Max = count
            },
            HintAvailable = true,
            Hint = Localized.T(language,
                "Çərçivədəki solğun şəklə bax: parçanın rəngini və əşyasını orada tap.",
                "Look at the faint picture in the frame: find the piece's colours and objects there."),
            AssistHighlight = tier == PetBrainDifficulty.Easy || context.Assisted
        };

        return (dto, new PuzzleSolution(solution, PetBrainAnswerKind.OrderIds));
    }

    /// <summary>
    /// Parça id-ləri — yeri açmayan sabit adlar. Toxum onları qarışdırır,
    /// yəni eyni ad hər tapmacada başqa yerə düşür.
    /// </summary>
    private static readonly string[] PictureTileIds =
    [
        "tile-amber", "tile-berry", "tile-cloud", "tile-dune", "tile-ember", "tile-fern",
        "tile-glow", "tile-haze", "tile-iris", "tile-jade", "tile-kelp", "tile-lumen"
    ];

    private const string PictureTileIcon = "🧩";

    /// <summary>Çərçivənin ölçüsü — sütun × sətir.</summary>
    private static (int Columns, int Rows) PictureGrid(PetBrainDifficulty tier) => tier switch
    {
        PetBrainDifficulty.Easy => (2, 3),
        PetBrainDifficulty.Hard => (3, 4),
        _ => (3, 3)
    };

    /// <summary>Dəstək rejimi bir pillə aşağı — cəza deyil, kömək.</summary>
    private static PetBrainDifficulty Easier(PetBrainDifficulty tier) => tier switch
    {
        PetBrainDifficulty.Hard => PetBrainDifficulty.Medium,
        _ => PetBrainDifficulty.Easy
    };

    // ==================== Köməkçilər ====================

    private static List<PetBrainPuzzleItemDto> BuildItems(
        PuzzleGenerationContext context, IReadOnlyList<int> values, Func<int, string> label)
    {
        var vocabulary = PuzzleBlueprintCatalog.VocabularyFor(context.Theme);
        List<PetBrainPuzzleItemDto> items = [];

        for (var i = 0; i < values.Count; i++)
        {
            items.Add(new PetBrainPuzzleItemDto
            {
                Id = $"item-{(char)('a' + i)}",
                Label = label(values[i]),
                Icon = vocabulary.Icons[i % vocabulary.Icons.Count],

                // Forma rəngdən ƏLAVƏ işarədir — rəng korluğunda da ayrılır.
                Shape = PuzzleBlueprintCatalog.Shapes[i % PuzzleBlueprintCatalog.Shapes.Count],
                Value = values[i]
            });
        }

        return items;
    }

    /// <summary>Fərqli dəyərlər. Aralıq darsa <c>null</c> qaytarır — çağıran yenidən cəhd edir.</summary>
    private static List<int>? DistinctValues(PuzzleRandom random, int count, int min, int maxExclusive)
    {
        if (maxExclusive - min < count)
            return null;

        var pool = Enumerable.Range(min, maxExclusive - min).ToList();
        random.Shuffle(pool);

        return [.. pool.Take(count)];
    }

    /// <summary>
    /// Hazır, HƏMİŞƏ etibarlı ehtiyat variantı.
    ///
    /// <para>Namizədlərin hamısı yoxlamadan keçməsə də uşaq boş ekran
    /// görməməlidir. Rəqəmlər sabitdir və yeganə həll zəmanətlidir.</para>
    /// </summary>
    private static GeneratedPuzzle Fallback(PuzzleGenerationContext context, int target)
    {
        var creative = context.ExperienceType == PetBrainExperienceType.Creative;

        // Ehtiyat variant da UYĞUNLUQ süzgəcindən keçir: «heç nə alınmadı» hal
        // uşağa yad hekayə göstərmək üçün əsas deyil. Sıralama boş qalırsa
        // (kataloqda bu macəra üçün ümumiyyətlə şablon yoxdur) mövzudan asılı
        // olmayan mexanikaya düşürük.
        var blueprint = Rank(context).FirstOrDefault()
            ?? PuzzleBlueprintCatalog.Find(
                creative ? PuzzleBlueprintCatalog.LightFragmentsKey : PuzzleBlueprintCatalog.SequenceOrderKey)!;

        var seedHex = PuzzleSeed.Hex(
            context.ChildId, context.RunId, blueprint.Key, blueprint.Version, target, attempt: 99);

        var random = new PuzzleRandom(seedHex);

        // Ehtiyat variant ASAN pillədədir: tələ yolu yoxdur, enerji boldur.
        // Uşaq üçün nəticə eynidir — hekayə davam edir.
        var built = blueprint.Mechanic switch
        {
            PetBrainPuzzleMechanic.OrderedRoute when RouteStoryPacks.For(blueprint.Key) is { } pack
                => BuildOrderedRoute(context, PetBrainDifficulty.Easy, random, pack),
            PetBrainPuzzleMechanic.LightFragments
                => BuildLightFragments(context, PetBrainDifficulty.Easy, random),
            PetBrainPuzzleMechanic.RouteLogic
                => BuildRouteLogic(context, PetBrainDifficulty.Easy, random),
            PetBrainPuzzleMechanic.PictureAssembly
                => BuildPictureAssembly(context, PetBrainDifficulty.Easy, random),
            _ => BuildSequenceOrder(context, PetBrainDifficulty.Easy, random)
        };

        // Son sığınacaq: ardıcıllıq mexanikası heç vaxt uğursuz olmur, çünki
        // onun aralığı sabitdir və mövzudan asılı deyil.
        if (built is null)
        {
            blueprint = PuzzleBlueprintCatalog.Find(PuzzleBlueprintCatalog.SequenceOrderKey)!;
            built = BuildSequenceOrder(context, PetBrainDifficulty.Easy, random);
        }

        var (dto, solution) = built!.Value;

        dto.BlueprintKey = blueprint.Key;
        dto.BlueprintVersion = blueprint.Version;

        var signature = PuzzleSeed.Signature(blueprint.Key, ["fallback"]);

        return new GeneratedPuzzle(blueprint, dto, solution, signature, seedHex, Attempt: 99, UsedFallback: true);
    }
}
