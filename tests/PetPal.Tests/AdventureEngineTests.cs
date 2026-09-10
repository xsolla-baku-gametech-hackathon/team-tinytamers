using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Story;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Engine-in SAF zəmanətləri — baza yoxdur, HTTP yoxdur.
///
/// <para>Burada yoxlanan şey uçdan-uca testlərin tuta bilmədiyidir: effektin
/// ideal-potentliyi, hekayə əşyasının qorunması, bal hesablanması. Bunların
/// hər biri yalnız müəyyən bir yol oynananda üzə çıxardı.</para>
/// </summary>
public class AdventureEngineTests
{
    private static readonly ExperienceDefinition Graph = MoonCrystalSecret.Definition;

    private static AdventureState Fresh => AdventureState.Empty with { Variant = AdventureVariants.Standard };

    /// <summary>
    /// Eyni düyünə İKİNCİ dəfə daxil olmaq heç nəyi ikiqat etmir.
    ///
    /// <para>Uşaq geri qayıdıb eyni səhnəni görsə də kristalı ikinci dəfə
    /// almamalıdır — yoxsa təkrar gəzmək «əşya fabriki»nə çevrilərdi.</para>
    /// </summary>
    [Fact]
    public void EyniDuyuneIkinciGiris_HecNeyiIkiqatEtmir()
    {
        var node = Graph.Find("c4-found")!;

        var (first, firstReport) = AdventureEngine.Enter(Graph, Fresh, node, alreadyVisited: false);

        Assert.Contains(MoonKeys.ItemShardTwo, firstReport.GrantedItems);
        Assert.Equal(1, first.ItemCount(MoonKeys.ItemShardTwo));

        var (second, secondReport) = AdventureEngine.Enter(Graph, first, node, alreadyVisited: true);

        Assert.True(secondReport.IsEmpty);
        Assert.Equal(1, second.ItemCount(MoonKeys.ItemShardTwo));
    }

    /// <summary>Sonluq balı təkrar girişdə ikinci dəfə artmır.</summary>
    [Fact]
    public void SonluqBali_TekrarGirisdeArtmir()
    {
        var node = Graph.Find("c2-map-on")!;

        var (once, _) = AdventureEngine.Enter(Graph, Fresh, node, alreadyVisited: false);
        var scored = once.EndingScores[MoonKeys.EndingExplorer];

        var (twice, _) = AdventureEngine.Enter(Graph, once, node, alreadyVisited: true);

        Assert.Equal(scored, twice.EndingScores[MoonKeys.EndingExplorer]);
    }

    /// <summary>
    /// HEKAYƏ əşyası sərf oluna bilmir — macəra bir predmetin itməsi ilə
    /// bloklana bilməz.
    /// </summary>
    [Fact]
    public void HekayeEsyasi_SerfOlunmur()
    {
        var withShard = Grant(Fresh, MoonKeys.ItemShardOne);

        var consumed = Apply(withShard,
            new ExperienceEffect(ExperienceEffectKind.ConsumeItem, MoonKeys.ItemShardOne));

        Assert.True(consumed.HasItem(MoonKeys.ItemShardOne));
    }

    /// <summary>Sərf olunan əşya AZALIR və sıfırda siyahıdan çıxır.</summary>
    [Fact]
    public void SerfOlunanEsya_Azalir()
    {
        var withCell = Grant(Fresh, MoonKeys.ItemPowerCell);

        Assert.True(withCell.HasItem(MoonKeys.ItemPowerCell));

        var spent = Apply(withCell,
            new ExperienceEffect(ExperienceEffectKind.ConsumeItem, MoonKeys.ItemPowerCell));

        Assert.False(spent.HasItem(MoonKeys.ItemPowerCell));
    }

    /// <summary>Naməlum əşya inventara DÜŞMÜR — tərif səhvi sükutla keçmir.</summary>
    [Fact]
    public void NamelumEsya_InventaraDusmur()
    {
        var state = Apply(Fresh, new ExperienceEffect(ExperienceEffectKind.GrantItem, "item-typo"));

        Assert.Empty(state.Inventory);
    }

    /// <summary>Sayla ölçülən məqsəd addım-addım irəliləyir və hədddə bağlanır.</summary>
    [Fact]
    public void SayliMeqsed_HeddeCatandaBaglanir()
    {
        var step = new ExperienceEffect(ExperienceEffectKind.AdvanceObjective, MoonKeys.ObjectivePackKit);

        var afterOne = Apply(Fresh, step);
        Assert.Equal(AdventureObjectiveStatus.Active, afterOne.Objective(MoonKeys.ObjectivePackKit)!.Status);

        var afterTwo = Apply(afterOne, step);
        Assert.Equal(AdventureObjectiveStatus.Completed, afterTwo.Objective(MoonKeys.ObjectivePackKit)!.Status);
    }

    /// <summary>
    /// ƏSAS məqsəd buraxıla BİLMİR — tərif səhvən belə yazılsa da engine
    /// imtina edir.
    /// </summary>
    [Fact]
    public void EsasMeqsed_BuraxilaBilmir()
    {
        var state = Apply(Fresh,
            new ExperienceEffect(ExperienceEffectKind.SkipOptionalObjective, MoonKeys.ObjectiveFindSignal));

        Assert.Null(state.Objective(MoonKeys.ObjectiveFindSignal));
    }

    /// <summary>Yan tapşırıq buraxıla bilər və bu, uğursuzluq sayılmır.</summary>
    [Fact]
    public void YanTapsiriq_BuraxilaBiler()
    {
        var state = Apply(Fresh,
            new ExperienceEffect(ExperienceEffectKind.SkipOptionalObjective, MoonKeys.SideMoonGarden));

        Assert.Equal(AdventureObjectiveStatus.Skipped, state.Objective(MoonKeys.SideMoonGarden)!.Status);
    }

    /// <summary>İzləyici ən çox ÜÇ məqsəd verir — uzun siyahı uşağı yükləyir.</summary>
    [Fact]
    public void Izleyici_UcdenCoxMeqsedVermir()
    {
        var state = Fresh;

        foreach (var objective in Graph.Objectives)
            state = Apply(state, new ExperienceEffect(ExperienceEffectKind.StartObjective, objective.ObjectiveId));

        var visible = AdventureEngine.VisibleObjectives(Graph, state, chapterId: string.Empty);

        Assert.True(visible.Count <= 3);
    }

    [Fact]
    public void Sert_InventariOxuyur()
    {
        var condition = new ExperienceCondition { RequiredItem = MoonKeys.ToolScanner };

        Assert.False(Fresh.Satisfies(condition));
        Assert.True(Grant(Fresh, MoonKeys.ToolScanner).Satisfies(condition));
    }

    [Fact]
    public void Sert_VariantiOxuyur()
    {
        var onlyLong = new ExperienceCondition { RequiredVariant = AdventureVariants.Long };

        Assert.False(Fresh.Satisfies(onlyLong));
        Assert.True((Fresh with { Variant = AdventureVariants.Long }).Satisfies(onlyLong));
    }

    [Fact]
    public void Sert_QadaganEdilmisVariantiOxuyur()
    {
        var notShort = new ExperienceCondition { ForbiddenVariant = AdventureVariants.Short };

        Assert.True(Fresh.Satisfies(notShort));
        Assert.False((Fresh with { Variant = AdventureVariants.Short }).Satisfies(notShort));
    }

    [Fact]
    public void Sert_BagPillesiniOxuyur()
    {
        var needsTrust = new ExperienceCondition { MinimumBondTier = PetBrainBondTier.AdventurePartner };

        Assert.False(Fresh.Satisfies(needsTrust));
        Assert.True((Fresh with { BondTier = PetBrainBondTier.BestCompanion }).Satisfies(needsTrust));
    }

    /// <summary>
    /// TƏKRAR CƏHD şərti — yumşaq uğursuzluq yolunu açan sayğac.
    /// </summary>
    [Fact]
    public void Sert_TekrarCehdiOxuyur()
    {
        var afterTwo = new ExperienceCondition { MinimumRetries = 2 };
        var at = Fresh with { CurrentNodeId = "c1-pattern" };

        Assert.False(at.Satisfies(afterTwo));

        var retried = AdventureEngine.WithRetry(AdventureEngine.WithRetry(at, "c1-pattern"), "c1-pattern");

        Assert.True((retried with { CurrentNodeId = "c1-pattern" }).Satisfies(afterTwo));
    }

    /// <summary>Boş şərt HƏMİŞƏ ödənir — şərtsiz keçid şərtsiz qalır.</summary>
    [Fact]
    public void BosSert_HemiseOdenir() => Assert.True(Fresh.Satisfies(ExperienceCondition.None));

    /// <summary>Ən yüksək bal qazanır və nəticə DETERMİNİSTdir.</summary>
    [Fact]
    public void Sonluq_EnYuksekBallaSecilir()
    {
        var state = Fresh with
        {
            EndingScores = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [MoonKeys.EndingGuardian] = 4,
                [MoonKeys.EndingExplorer] = 9,
                [MoonKeys.EndingRobotFriend] = 3
            }
        };

        Assert.Equal(MoonKeys.EndingExplorer, AdventureEngine.ResolveEnding(Graph, state)!.EndingKey);
        Assert.Equal(MoonKeys.EndingExplorer, AdventureEngine.ResolveEnding(Graph, state)!.EndingKey);
    }

    /// <summary>
    /// Heç bir hədd ödənməsə də uşaq SONLUQSUZ qalmır — ehtiyat sonluq gəlir.
    /// </summary>
    [Fact]
    public void Sonluq_BalYoxdursaDaVerilir() =>
        Assert.NotNull(AdventureEngine.ResolveEnding(Graph, Fresh));

    /// <summary>İrəliləmə TAMAMLANMIŞ fəsillərdən hesablanır, addımdan yox.</summary>
    [Fact]
    public void Irelileme_FesillerdenHesablanir()
    {
        Assert.Equal(0, AdventureEngine.ProgressPercent(Graph, Fresh));

        var half = Fresh with
        {
            CompletedChapterIds = new HashSet<string>(StringComparer.Ordinal)
            {
                MoonKeys.Chapter1, MoonKeys.Chapter2, MoonKeys.Chapter3
            }
        };

        Assert.Equal(50, AdventureEngine.ProgressPercent(Graph, half));
    }

    private static AdventureState Grant(AdventureState state, string itemId) =>
        Apply(state, new ExperienceEffect(ExperienceEffectKind.GrantItem, itemId));

    /// <summary>
    /// Bir effekti sınaq düyünü ilə tətbiq edir.
    ///
    /// <para>Sınaq düyünü qəsdən qurulur: engine düyün üzərində işləyir və
    /// effekti tək başına tətbiq edən açıq yol yoxdur — olsaydı, sınaq
    /// istehsalda işlənməyən ikinci yol yaradardı.</para>
    /// </summary>
    private static AdventureState Apply(AdventureState state, params ExperienceEffect[] effects)
    {
        var probe = new ExperienceNode(
            Id: $"probe-{Guid.NewGuid():N}",
            Kind: PetBrainStageKind.Narration,
            PromptAz: "sınaq", PromptEn: "probe",
            PetLineAz: "sınaq", PetLineEn: "probe",
            Options: [], Transitions: [], Effects: effects);

        return AdventureEngine.Enter(Graph, state, probe, alreadyVisited: false).State;
    }
}
