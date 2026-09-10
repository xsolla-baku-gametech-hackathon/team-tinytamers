using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Chapter-li tərifin QURULUŞ qaydaları.
///
/// <para>Hər test bir SƏHVİ qəsdən qurur və yoxlayıcının onu tutduğunu
/// təsdiqləyir. Səbəb budur: qırx dəqiqəlik macərada belə səhvlər yalnız
/// müəyyən bir yolu sona qədər oynayanda görünür — yəni əl ilə tapılmır.</para>
/// </summary>
public class AdventureValidatorTests
{

    /// <summary>
    /// Checkpoint-siz fəsil RƏDD olunur — uşağın on dəqiqəsi girov qala bilməz.
    /// </summary>
    [Fact]
    public void CheckpointsizFesil_RedOlunur()
    {
        var problems = ExperienceGraphValidator.Validate(Build(withCheckpoint: false));

        Assert.Contains(problems, p => p.Contains("checkpoint", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Seçim təqdim etməyən fəsil rədd olunur — «davam et» zənciri qadağandır.</summary>
    [Fact]
    public void SecimsizFesil_RedOlunur()
    {
        var problems = ExperienceGraphValidator.Validate(Build(withChoice: false));

        Assert.Contains(problems, p => p.Contains("seçim təqdim etmir", StringComparison.Ordinal));
    }

    /// <summary>Heç bir düyündə bağlanmayan fəsil rədd olunur.</summary>
    [Fact]
    public void BaglanmayanFesil_RedOlunur()
    {
        var problems = ExperienceGraphValidator.Validate(Build(closesChapter: false));

        Assert.Contains(problems, p => p.Contains("bağlanmır", StringComparison.Ordinal));
    }

    /// <summary>Həddi aşan fəsil müddəti rədd olunur — 12 dəqiqə yuxarı hədddir.</summary>
    [Fact]
    public void CoxUzunFesil_RedOlunur()
    {
        var problems = ExperienceGraphValidator.Validate(Build(minutes: 25));

        Assert.Contains(problems, p => p.Contains("uyğun deyil", StringComparison.Ordinal));
    }

    /// <summary>Fəsil daxilində tamamlana bilməyən ƏSAS məqsəd rədd olunur.</summary>
    [Fact]
    public void TamamlanmayanEsasMeqsed_RedOlunur()
    {
        var problems = ExperienceGraphValidator.Validate(Build(completesObjective: false));

        Assert.Contains(problems, p => p.Contains("tamamlana bilmir", StringComparison.Ordinal));
    }

    /// <summary>Naməlum əşyaya toxunan effekt rədd olunur — yazılış səhvi tutulur.</summary>
    [Fact]
    public void NamelumEsyaAcari_RedOlunur()
    {
        var problems = ExperienceGraphValidator.Validate(
            Build(extraEffect: new ExperienceEffect(ExperienceEffectKind.GrantItem, "item-typo")));

        Assert.Contains(problems, p => p.Contains("naməlum əşya", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Naməlum ipucunu oxuyan şərt rədd olunur.</summary>
    [Fact]
    public void NamelumIpucuSerti_RedOlunur()
    {
        var problems = ExperienceGraphValidator.Validate(
            Build(gateCondition: new ExperienceCondition { RequiredClue = "clue-typo" }));

        Assert.Contains(problems, p => p.Contains("naməlum clue", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Heç yerdə oxunmayan əşya rədd olunur — mənasız kolleksiya.</summary>
    [Fact]
    public void OxunmayanEsya_RedOlunur()
    {
        var problems = ExperienceGraphValidator.Validate(Build(withUnusedItem: true));

        Assert.Contains(problems, p => p.Contains("mənasız kolleksiya", StringComparison.Ordinal));
    }

    /// <summary>
    /// ŞƏRTLİ ehtiyat keçid rədd olunur: şərtli ehtiyat ehtiyat deyil —
    /// dalanın adı dəyişir, özü qalır.
    /// </summary>
    [Fact]
    public void SertliEhtiyatKecid_RedOlunur()
    {
        var problems = ExperienceGraphValidator.Validate(Build(conditionalFallback: true));

        Assert.Contains(problems, p => p.Contains("ehtiyat", StringComparison.Ordinal));
    }

    /// <summary>
    /// Bir variantda dalana düşən tərif rədd olunur.
    ///
    /// <para>Bu, şərtli düyünlərin ən təhlükəli səhvidir: macəra «uzun»
    /// profildə işləyir, «qısa» profildə isə uşaq boş ekranla qalır.</para>
    /// </summary>
    [Fact]
    public void BirVariantdaDalan_RedOlunur()
    {
        var problems = ExperienceGraphValidator.Validate(Build(variantOnlyPath: true));

        Assert.Contains(problems, p => p.Contains("variant", StringComparison.Ordinal));
    }

    /// <summary>Səhvsiz qurulmuş tərif HEÇ BİR şikayət yaratmır.</summary>
    [Fact]
    public void DuzgunTerif_QebulOlunur() =>
        Assert.Empty(ExperienceGraphValidator.Validate(Build()));

    /// <summary>Kataloqdakı flaqman tərif bütün qaydalardan keçir.</summary>
    [Fact]
    public void Flaqman_ButunQaydalardanKecir()
    {
        var problems = ExperienceGraphValidator.Validate(MoonCrystalSecret.Definition);

        Assert.True(problems.Count == 0, string.Join(" | ", problems));
    }

    /// <summary>
    /// Bir fəsilli, ETİBARLI minimal tərif; parametrlər onu qəsdən pozur.
    ///
    /// <para>Hər testin öz tərifini yazması təkrar olardı və səhvin harada
    /// olduğunu gizlədərdi. Burada yalnız BİR şey dəyişir, qalan hər şey
    /// düzgün qalır — yəni tutulan şikayət məhz həmin dəyişikliyə aiddir.</para>
    /// </summary>
    private static ExperienceDefinition Build(
        bool withCheckpoint = true,
        bool withChoice = true,
        bool closesChapter = true,
        bool completesObjective = true,
        bool withUnusedItem = false,
        bool conditionalFallback = false,
        bool variantOnlyPath = false,
        int minutes = 8,
        ExperienceEffect? extraEffect = null,
        ExperienceCondition? gateCondition = null)
    {
        const string chapter = "test-chapter";
        const string objective = "test-objective";
        const string item = "test-item";

        List<ExperienceEffect> introEffects = [new(ExperienceEffectKind.GrantItem, item)];

        if (completesObjective)
            introEffects.Add(new ExperienceEffect(ExperienceEffectKind.CompleteObjective, objective));

        if (extraEffect is not null)
            introEffects.Add(extraEffect);

        var choiceOption = new ExperienceOption(
            "go-on", "Davam", "Go on", "▶", "", "", []);

        var otherOption = new ExperienceOption(
            "other-way", "Başqa yol", "Another way", "↗", "", "", []);

        var gate = gateCondition ?? new ExperienceCondition { RequiredItem = item };

        var choice = new ExperienceNode(
            Id: "choice",
            Kind: withChoice ? PetBrainStageKind.Choice : PetBrainStageKind.Narration,
            PromptAz: "Sual", PromptEn: "Question",
            PetLineAz: "Necə?", PetLineEn: "How?",
            Options: withChoice ? [choiceOption, otherOption] : [],

            Transitions:
            [
                new ExperienceTransition("finish", RequiredOptionKey: "go-on", Priority: 10) { Requires = gate },
                new ExperienceTransition("second", RequiredOptionKey: "other-way", Priority: 20),
                new ExperienceTransition("finish", Priority: 1000, IsFallback: true)
            ],
            Effects: [])
        {
            ChapterId = chapter
        };

        var intro = new ExperienceNode(
            Id: "start",
            Kind: PetBrainStageKind.Intro,
            PromptAz: "Başlanğıc", PromptEn: "Start",
            PetLineAz: "Gedək", PetLineEn: "Let us go",
            Options: [],
            Transitions: variantOnlyPath
                ?
                [
                    new ExperienceTransition("choice", Priority: 10)
                    {
                        Requires = new ExperienceCondition { RequiredVariant = AdventureVariants.Long }
                    }
                ]
                : [new ExperienceTransition("choice", Priority: 1000, IsFallback: true)],
            Effects: introEffects)
        {
            ChapterId = chapter
        };

        var finish = new ExperienceNode(
            Id: "finish",
            Kind: PetBrainStageKind.Ending,
            PromptAz: "Son", PromptEn: "The end",
            PetLineAz: "Bitdi", PetLineEn: "Done",
            Options: [],
            Transitions: [],
            Effects: [],
            EndingKey: "test-ending")
        {
            ChapterId = chapter,
            IsCheckpoint = withCheckpoint,
            CompletesChapterId = closesChapter ? chapter : string.Empty,
            Requires = conditionalFallback
                ? new ExperienceCondition { RequiredItem = item }
                : ExperienceCondition.None
        };

        var second = new ExperienceNode(
            Id: "second",
            Kind: PetBrainStageKind.Ending,
            PromptAz: "İkinci son", PromptEn: "Second end",
            PetLineAz: "Başqa yol", PetLineEn: "Another way",
            Options: [],
            Transitions: [],
            Effects: [],
            EndingKey: "test-ending-two")
        {
            ChapterId = chapter,
            IsCheckpoint = withCheckpoint
        };

        List<AdventureItemDefinition> items =
        [
            new(item, "Əşya", "Item", "Təsvir", "Description", "🔧")
        ];

        if (withUnusedItem)
            items.Add(new AdventureItemDefinition(
                "test-spare", "Artıq", "Spare", "Heç nə", "Nothing", "📦"));

        return new ExperienceDefinition(
            Key: "test-adventure",
            Version: 1,
            StartNodeId: "start",
            Nodes: [intro, choice, finish, second],
            AllowedPuzzleFamilies: [])
        {
            Chapters =
            [
                new AdventureChapterDefinition(
                    chapter, 1, "Fəsil", "Chapter", "Xülasə", "Summary",
                    "start", minutes, [objective], [])
            ],
            Objectives =
            [
                new AdventureObjectiveDefinition(
                    objective, chapter, AdventureObjectiveKind.ReachNode,
                    "Məqsəd", "Objective", "Təsvir", "Description")
            ],
            Items = items,
            Clues = [],
            Endings = []
        };
    }
}
