using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Profilin öyrənməsi, çətinlik, bağ, xarakter və yaddaş — hamısı SAF qaydalar.
///
/// <para>Bu qatın hər qaydası uşaq təhlükəsizliyi ilə bağlıdır, ona görə
/// testlər "işləyirmi" yox, "ZƏRƏR VERMİRMİ" sualına cavab verir: səhv cavab
/// marağı azaltmır, ipucu cəzalandırmır, bağ heç vaxt geri getmir.</para>
/// </summary>
public class PetBrainProfileTests
{
    private static ExperienceTemplate Mars => ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!;
    private static ExperienceTemplate Dragon => ExperienceCatalog.Find(ExperienceCatalog.DragonLostColors)!;

    // ==================== Tədricən öyrənmə ====================

    [Fact]
    public void Tamamlama_EsasMaraqiUcBalQaldirir()
    {
        var adjustments = ProfileLearningRules.ForCompletion(Mars, isFirstCompletion: true);

        var primary = adjustments.Single(a =>
            a.Category == PetBrainTraitCategory.Interest && a.Key == TraitKeys.Space);

        Assert.Equal(ProfileLearningRules.CompletedPrimaryInterest, primary.Delta);
    }

    [Fact]
    public void Tamamlama_UygunOyunUslubunuIkiBalQaldirir()
    {
        var adjustments = ProfileLearningRules.ForCompletion(Mars, isFirstCompletion: true);

        Assert.All(
            adjustments.Where(a => a.Category == PetBrainTraitCategory.PlayStyle),
            a => Assert.Equal(ProfileLearningRules.CompletedPlayStyle, a.Delta));
    }

    /// <summary>Təkrar oynanış eyni qazancı vermir — dövrə vurub profil şişirdilə bilməz.</summary>
    [Fact]
    public void TekrarTamamlama_DahaAzVerir()
    {
        var first = ProfileLearningRules.ForCompletion(Mars, isFirstCompletion: true);
        var repeat = ProfileLearningRules.ForCompletion(Mars, isFirstCompletion: false);

        Assert.True(repeat.Sum(a => a.Delta) < first.Sum(a => a.Delta));
    }

    /// <summary>
    /// Hər hansı bir hərəkət beş baldan çox dəyişə bilməz — cədvəldə səhv
    /// rəqəm yazılsa belə.
    /// </summary>
    [Fact]
    public void HerHereket_BesBaldanCoxDeyismir()
    {
        IEnumerable<TraitAdjustment>[] batches =
        [
            ProfileLearningRules.ForSelection(Mars),
            ProfileLearningRules.ForCompletion(Mars, true),
            ProfileLearningRules.ForCompletion(Dragon, true),
            ProfileLearningRules.ForAbandon(Mars, 2),
            ProfileLearningRules.ForPuzzleSolved(),
            ProfileLearningRules.ForCare(),
            ProfileLearningRules.ForMiniGame(),
            ProfileLearningRules.ForAccessoryEquipped(),
            .. ExperienceCatalog.Templates.SelectMany(t => t.Stages)
                .SelectMany(s => s.Options)
                .Select(ProfileLearningRules.ForChoice)
        ];

        foreach (var adjustment in batches.SelectMany(b => b))
            Assert.InRange(adjustment.Delta, -TraitKeys.MaxDeltaPerAction, TraitKeys.MaxDeltaPerAction);
    }

    /// <summary>
    /// Bacarmamaq sevməmək DEYİL: səhv cavab heç bir marağı azaltmır.
    /// </summary>
    [Fact]
    public void SehvCavab_HecBirMaragiAzaltmir()
    {
        foreach (var skill in Enum.GetValues<SkillArea>())
            Assert.Empty(ProfileLearningRules.ForSkillAnswer(skill, isCorrect: false));
    }

    [Fact]
    public void DogruCavab_YalnizMusbetSiqnalVerir()
    {
        foreach (var skill in Enum.GetValues<SkillArea>())
            Assert.All(ProfileLearningRules.ForSkillAnswer(skill, isCorrect: true),
                a => Assert.True(a.Delta > 0));
    }

    /// <summary>
    /// Giriş ekranından çıxmaq fikir bildirmək deyil — yalnız MƏNALI yarımçıq
    /// qoyma marağı azaldır.
    /// </summary>
    [Fact]
    public void YarimciqQoyma_YalnizMenaliOlandaMaragiAzaldir()
    {
        Assert.Empty(ProfileLearningRules.ForAbandon(Mars, completedStages: 0));

        var meaningful = ProfileLearningRules.ForAbandon(Mars, completedStages: 2);

        Assert.Single(meaningful);
        Assert.Equal(ProfileLearningRules.AbandonedPrimaryInterest, meaningful[0].Delta);
        Assert.True(meaningful[0].Delta < 0);
    }

    [Fact]
    public void TapmacaHelli_TapmacaVeHelledicileriQaldirir()
    {
        var adjustments = ProfileLearningRules.ForPuzzleSolved();

        Assert.Contains(adjustments, a => a.Key == TraitKeys.Puzzles && a.Delta > 0);
        Assert.Contains(adjustments, a => a.Key == TraitKeys.ProblemSolver && a.Delta > 0);
    }

    /// <summary>Yaradıcı həll (günəş paneli) yaradıcılığı və həllediciliyi qaldırır.</summary>
    [Fact]
    public void GunesPaneliSecimi_YaradiciligiQaldirir()
    {
        var option = Mars.Stages
            .SelectMany(s => s.Options)
            .Single(o => o.Key == "solar-panel");

        var adjustments = ProfileLearningRules.ForChoice(option);

        Assert.Contains(adjustments, a => a.Key == TraitKeys.Creative && a.Delta == 2);
        Assert.Contains(adjustments, a => a.Key == TraitKeys.ProblemSolver && a.Delta == 1);
    }

    [Fact]
    public void Xasse_SifirYuzArasindaSixilir()
    {
        Assert.Equal(0, TraitKeys.Clamp(-40));
        Assert.Equal(100, TraitKeys.Clamp(180));
        Assert.Equal(64, TraitKeys.Clamp(64));
    }

    // ==================== Çətinlik ====================

    [Fact]
    public void GucluNetice_YalnizBirPilleQaldirir()
    {
        var next = ExperienceDifficulty.Next(
            new RunPerformance(PetBrainDifficulty.Easy, ScorePercent: 100, Hints: 0, Mistakes: 0), age: 10);

        Assert.Equal(PetBrainDifficulty.Medium, next);
    }

    [Fact]
    public void CoxSehv_YalnizBirPilleEndirir()
    {
        var next = ExperienceDifficulty.Next(
            new RunPerformance(PetBrainDifficulty.Hard, ScorePercent: 20, Hints: 0, Mistakes: 5), age: 10);

        Assert.Equal(PetBrainDifficulty.Medium, next);
    }

    /// <summary>İpucu istəmək köməkdir: çətinlik yumşalır, amma MARAQ toxunulmur.</summary>
    [Fact]
    public void CoxIpucu_CetinliyiYumsaldirMaragaToxunmur()
    {
        var performance = new RunPerformance(
            PetBrainDifficulty.Hard, ScorePercent: 90, Hints: 3, Mistakes: 0);

        Assert.Equal(PetBrainDifficulty.Medium, ExperienceDifficulty.Next(performance, age: 10));
        Assert.True(ExperienceDifficulty.NeedsAssist(performance));
    }

    [Fact]
    public void OrtaNetice_PilleniDeyismir()
    {
        var next = ExperienceDifficulty.Next(
            new RunPerformance(PetBrainDifficulty.Medium, ScorePercent: 70, Hints: 0, Mistakes: 1), age: 10);

        Assert.Equal(PetBrainDifficulty.Medium, next);
    }

    /// <summary>
    /// Yaş TAVANDIR: 6 yaşlı uşaq mükəmməl nəticə göstərsə də Hard almır.
    /// Bu, onun bacarığı haqqında iddia deyil — təhlükəsizlik sərhədidir.
    /// </summary>
    [Fact]
    public void Yas_CetinliyinTavaniniQoyur()
    {
        var next = ExperienceDifficulty.Next(
            new RunPerformance(PetBrainDifficulty.Medium, ScorePercent: 100, Hints: 0, Mistakes: 0), age: 6);

        Assert.Equal(PetBrainDifficulty.Easy, next);
        Assert.Equal(PetBrainDifficulty.Easy, ExperienceDifficulty.Ceiling(6));
        Assert.Equal(PetBrainDifficulty.Hard, ExperienceDifficulty.Ceiling(10));
    }

    /// <summary>Pillə heç vaxt bir addımdan çox sıçramır.</summary>
    [Theory]
    [InlineData(PetBrainDifficulty.Easy)]
    [InlineData(PetBrainDifficulty.Medium)]
    [InlineData(PetBrainDifficulty.Hard)]
    public void Cetinlik_BirAddimdanCoxSicramir(PetBrainDifficulty current)
    {
        foreach (var score in new[] { 0, 40, 85, 100 })
        foreach (var hints in new[] { 0, 1, 3 })
        foreach (var mistakes in new[] { 0, 1, 4 })
        {
            var next = ExperienceDifficulty.Next(
                new RunPerformance(current, score, hints, mistakes), age: 10);

            Assert.True(Math.Abs((int)next - (int)current) <= 1,
                $"{current} → {next} (bal {score}, ipucu {hints}, səhv {mistakes})");
        }
    }

    // ==================== Bağ ====================

    [Fact]
    public void Bag_SifirYuzArasindaSixilir()
    {
        var pet = new Pet { Bond = 96 };

        BondRules.Grant(pet, 20);

        Assert.Equal(100, pet.Bond);
    }

    /// <summary>Bağ HEÇ VAXT azalmır — mənfi dəyər səssizcə buraxılır.</summary>
    [Fact]
    public void Bag_MenfiDeyerQebulEtmir()
    {
        var pet = new Pet { Bond = 40 };

        Assert.Equal(0, BondRules.Grant(pet, -15));
        Assert.Equal(40, pet.Bond);
    }

    [Fact]
    public void Bag_IlkBoyukMaceradaElaveBonusVerir()
    {
        var first = BondRules.ForCompletion(Mars, isFirstEverAdventure: true);
        var later = BondRules.ForCompletion(Mars, isFirstEverAdventure: false);

        Assert.Equal(later + BondRules.FirstAdventureBonus, first);
    }

    /// <summary>Qulluqdan gələn bağın gündəlik tavanı var — düymə basmaqla dolmur.</summary>
    [Fact]
    public void Bag_QulluqdanGundelikTavanaTabedir()
    {
        Assert.Equal(BondRules.CareGain, BondRules.ForCare(0));
        Assert.Equal(BondRules.CareGain, BondRules.ForCare(BondRules.DailyCareCap - 1));
        Assert.Equal(0, BondRules.ForCare(BondRules.DailyCareCap));
        Assert.Equal(0, BondRules.ForCare(BondRules.DailyCareCap + 5));
    }

    // ==================== Xarakter ====================

    [Fact]
    public void Xarakter_GucluIstiqametdenCixarilir()
    {
        var scientist = CompanionPersonality.Derive(
            new Dictionary<string, int> { [TraitKeys.Science] = 80, [TraitKeys.Puzzles] = 85 },
            new Dictionary<string, int> { [TraitKeys.ProblemSolver] = 85 });

        Assert.Equal(PetBrainPersonality.CuriousScientist, scientist);

        var creative = CompanionPersonality.Derive(
            new Dictionary<string, int> { [TraitKeys.Stories] = 82, [TraitKeys.Fantasy] = 90 },
            new Dictionary<string, int> { [TraitKeys.Creative] = 88 });

        Assert.Equal(PetBrainPersonality.CreativeCompanion, creative);
    }

    /// <summary>
    /// Yaxın ballarda etiket ATLAMIR. Histerezis olmasaydı pet hər sessiyada
    /// "fikrini dəyişən" görünərdi.
    /// </summary>
    [Fact]
    public void Xarakter_YaxinBallardaSabitQalir()
    {
        var interests = new Dictionary<string, int>
        {
            [TraitKeys.Science] = 70,
            [TraitKeys.Puzzles] = 70,
            [TraitKeys.Stories] = 72,
            [TraitKeys.Fantasy] = 72
        };

        var playStyles = new Dictionary<string, int>
        {
            [TraitKeys.ProblemSolver] = 70,
            [TraitKeys.Creative] = 72
        };

        // Hazırkı etiket alimdir və rəqib cəmi iki bal qabaqdadır → dəyişmir.
        var kept = CompanionPersonality.Derive(interests, playStyles, PetBrainPersonality.CuriousScientist);

        Assert.Equal(PetBrainPersonality.CuriousScientist, kept);
    }

    [Fact]
    public void Xarakter_ZeifSiqnaldaBalanslasdirilmisQalir()
    {
        var weak = CompanionPersonality.Derive(
            new Dictionary<string, int> { [TraitKeys.Science] = 20 },
            new Dictionary<string, int> { [TraitKeys.Creative] = 20 });

        Assert.Equal(PetBrainPersonality.Balanced, weak);
    }

    // ==================== Yaddaş ====================

    [Fact]
    public void Yaddas_VaciblikSirasiIleSecilir()
    {
        var now = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

        List<PetMemory> memories =
        [
            Memory(PetBrainMemoryKind.PreferenceObserved, TraitKeys.Space, MemoryPolicy.PreferenceImportance, now),
            Memory(PetBrainMemoryKind.FirstAdventure, ExperienceCatalog.MarsRoverRescue, MemoryPolicy.FirstAdventureImportance, now),
            Memory(PetBrainMemoryKind.ChoiceMade, ExperienceCatalog.MarsRoverRescue, MemoryPolicy.ChoiceImportance, now),
            Memory(PetBrainMemoryKind.CosmeticUnlocked, ExperienceCatalog.HelmetMars, MemoryPolicy.CosmeticImportance, now)
        ];

        var selected = MemoryPolicy.Select(memories, now);

        Assert.Equal(MemoryPolicy.RetrievalLimit, selected.Count);
        Assert.Equal(PetBrainMemoryKind.FirstAdventure, selected[0].Kind);
        Assert.Equal(PetBrainMemoryKind.CosmeticUnlocked, selected[1].Kind);
    }

    [Fact]
    public void Yaddas_MuddetiBitmisXatireniGoturmur()
    {
        var now = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

        var expired = Memory(PetBrainMemoryKind.ChoiceMade, ExperienceCatalog.MarsRoverRescue, 90, now);
        expired.ExpiresAt = now.AddDays(-1);

        Assert.Empty(MemoryPolicy.Select([expired], now));
    }

    /// <summary>Salamlamada eyni cümlə təkrarlanmır — ən köhnə işlənmiş xatirə əvvəl gəlir.</summary>
    [Fact]
    public void Salamlama_EnKohneIslenmisXatireniSecir()
    {
        var now = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

        var recentlyUsed = Memory(PetBrainMemoryKind.FirstAdventure, ExperienceCatalog.MarsRoverRescue, 95, now);
        recentlyUsed.LastUsedAt = now;

        var neverUsed = Memory(PetBrainMemoryKind.ChoiceMade, ExperienceCatalog.MarsRoverRescue, 55, now);

        var picked = MemoryPolicy.PickForGreeting([recentlyUsed, neverUsed], now);

        Assert.Equal(neverUsed, picked);
    }

    /// <summary>Xatirə uşağa TƏBİİ cümlə kimi görünür — bal və faiz yoxdur.</summary>
    [Fact]
    public void Yaddas_BalDeyilCumleGosterir()
    {
        var now = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
        var memory = Memory(PetBrainMemoryKind.ExperienceCompleted, ExperienceCatalog.MarsRoverRescue, 70, now);

        foreach (var language in new[] { "az", "en" })
        {
            var text = MemoryPolicy.Render(memory, language, "Luna");

            Assert.Contains(ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!.Title(language), text);
            Assert.DoesNotContain("%", text);
        }
    }

    /// <summary>Seçim xatirəsi konkret seçimi adı ilə xatırlayır.</summary>
    [Fact]
    public void Yaddas_SecimiAdiIleXatirlayir()
    {
        var now = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

        var memory = Memory(PetBrainMemoryKind.ChoiceMade, ExperienceCatalog.MarsRoverRescue, 55, now);
        memory.ValueKey = "solar-panel";

        Assert.Contains("Günəş paneli", MemoryPolicy.Render(memory, "az", "Luna"));
        Assert.Contains("solar panel", MemoryPolicy.Render(memory, "en", "Luna"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Yaddas_SaxlamaLimitiniAsanlariAyirir()
    {
        var now = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

        var memories = Enumerable.Range(0, MemoryPolicy.RetentionLimit + 5)
            .Select(i => Memory(PetBrainMemoryKind.ChoiceMade, $"key-{i}", i, now))
            .ToList();

        Assert.Equal(5, MemoryPolicy.Prune(memories).Count);
    }

    private static PetMemory Memory(PetBrainMemoryKind kind, string factKey, int importance, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        FactKey = factKey,
        ValueKey = string.Empty,
        Importance = importance,
        CreatedAt = now
    };
}
