using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Mind;
using PetPal.Api.PetBrain.Recommendation;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Fərdiləşdirmənin SAF qatı: ustalıq, açıq seçim, dəstək planı və tövsiyə
/// siyasəti.
///
/// <para>Bu testlərin heç birində baza, şəbəkə və ya saat yoxdur — qərar qatı
/// saf olmalıdır, yoxsa «niyə bu macəra?» sualının cavabı yenidən qurula
/// bilməz.</para>
/// </summary>
public class MechanicMasteryRuleTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// <b>Kömək cəzalandırılmır.</b> İpucu ilə həll edilən tapmaca səviyyəni
    /// YENƏ DƏ qaldırır — sadəcə müstəqil həlldən az.
    /// </summary>
    [Fact]
    public void KomekliUgur_SeviyyeniQaldirir_AmmaAz()
    {
        var solo = New();
        var assisted = New();

        MechanicMasteryRules.Apply(solo, Attempt(solved: true), Now);
        MechanicMasteryRules.Apply(assisted, Attempt(solved: true, usedHint: true), Now);

        Assert.True(solo.EstimatedLevel > MechanicMastery.StartingLevel,
            "Müstəqil uğur səviyyəni qaldırmalıdır.");

        Assert.True(assisted.EstimatedLevel > MechanicMastery.StartingLevel,
            "Köməklə gələn uğur da HƏQİQİ uğurdur — səviyyə düşməməlidir.");

        Assert.True(assisted.EstimatedLevel < solo.EstimatedLevel,
            "Köməkli uğur müstəqil uğurla eyni siqnal verməməlidir.");
    }

    /// <summary>Kömək istəmək TRENDİ aşağı salmır — geriləmə deyil.</summary>
    [Fact]
    public void KomekIstemek_TrendiAsagiSalmir()
    {
        var mastery = New();

        MechanicMasteryRules.Apply(mastery, Attempt(solved: true, usedHint: true), Now);

        Assert.Equal(0, mastery.RecentTrend);
    }

    /// <summary>Köməkli və müstəqil uğur AYRI sayılır.</summary>
    [Fact]
    public void Ugurlar_AyriSayilir()
    {
        var mastery = New();

        MechanicMasteryRules.Apply(mastery, Attempt(solved: true), Now);
        MechanicMasteryRules.Apply(mastery, Attempt(solved: true, usedHint: true), Now);
        MechanicMasteryRules.Apply(mastery, Attempt(solved: false), Now);

        Assert.Equal(1, mastery.Successes);
        Assert.Equal(1, mastery.AssistedSuccesses);
        Assert.Equal(3, mastery.Attempts);
    }

    /// <summary>
    /// <b>Sonsuz çətinlik yoxdur.</b> Uşaq nə qədər güclü olsa da, tövsiyə
    /// olunan pillə <c>Hard</c>-dan yuxarı qalxmır.
    /// </summary>
    [Fact]
    public void YuksekUstaliq_SonsuzCetinlikYaratmir()
    {
        var mastery = New();

        for (var i = 0; i < 40; i++)
            MechanicMasteryRules.Apply(mastery, Attempt(solved: true, difficulty: PetBrainDifficulty.Hard), Now);

        Assert.Equal(PetBrainDifficulty.Hard, MechanicMasteryRules.BandFor(mastery));
        Assert.True(mastery.EstimatedLevel <= MechanicMastery.MaxLevel);
    }

    /// <summary>
    /// <b>Sonsuz asanlıq da yoxdur.</b> Aşağı pillədə uzun müddət qalan uşağa
    /// determinist şəkildə bir pillə yuxarı təklif olunur.
    /// </summary>
    [Fact]
    public void AsagiUstaliq_EyniAsanTapsiginSonsuzTekrarinaSebebOlmur()
    {
        var mastery = New();
        mastery.Attempts = MechanicMasteryRules.StretchAfterAttempts;
        mastery.EstimatedLevel = 20;
        mastery.RecentTrend = 0;

        Assert.True(MechanicMasteryRules.ShouldStretch(mastery));
        Assert.Equal(PetBrainDifficulty.Medium, MechanicMasteryRules.BandFor(mastery));
    }

    /// <summary>Trend mənfi olanda uşağın üstünə əlavə çətinlik qoyulmur.</summary>
    [Fact]
    public void MenfiTrend_YuxariPilleSinanmir()
    {
        var mastery = New();
        mastery.Attempts = MechanicMasteryRules.StretchAfterAttempts;
        mastery.EstimatedLevel = 20;
        mastery.RecentTrend = -2;

        Assert.False(MechanicMasteryRules.ShouldStretch(mastery));
        Assert.Equal(PetBrainDifficulty.Easy, MechanicMasteryRules.BandFor(mastery));
    }

    /// <summary>Bir cəhd profili sıçratmır — addım sərt hədd altındadır.</summary>
    [Fact]
    public void BirCehd_ProfiliSicratmir()
    {
        var mastery = New();
        var before = mastery.EstimatedLevel;

        MechanicMasteryRules.Apply(mastery, Attempt(solved: true, difficulty: PetBrainDifficulty.Hard), Now);

        Assert.True(Math.Abs(mastery.EstimatedLevel - before) <= MechanicMasteryRules.MaxStepPerAttempt);
    }

    /// <summary>
    /// İnam SƏVİYYƏDƏN ayrıdır: «50 səviyyə, bir cəhd» ilə «50 səviyyə, on
    /// cəhd» eyni şey deyil.
    /// </summary>
    [Fact]
    public void Inam_SeviyyedenAyridir()
    {
        var fresh = New();
        var practised = New();

        MechanicMasteryRules.Apply(fresh, Attempt(solved: true), Now);

        for (var i = 0; i < MechanicMasteryRules.ConfidenceSaturation; i++)
            MechanicMasteryRules.Apply(practised, Attempt(solved: true), Now);

        Assert.True(
            MechanicMasteryRules.Confidence(practised, Now) > MechanicMasteryRules.Confidence(fresh, Now));
    }

    /// <summary>Heç bir cəhd yoxdursa inam SIFIRDIR — təxmin fakt deyil.</summary>
    [Fact]
    public void CehdYoxdursa_InamSifirdir() =>
        Assert.Equal(0, MechanicMasteryRules.Confidence(New(), Now));

    /// <summary>Eyni giriş həmişə eyni nəticə verir.</summary>
    [Fact]
    public void Qayda_Deterministikdir()
    {
        var first = New();
        var second = New();

        foreach (var solved in new[] { true, false, true, true, false })
        {
            MechanicMasteryRules.Apply(first, Attempt(solved), Now);
            MechanicMasteryRules.Apply(second, Attempt(solved), Now);
        }

        Assert.Equal(first.EstimatedLevel, second.EstimatedLevel);
        Assert.Equal(first.RecentTrend, second.RecentTrend);
    }

    private static MechanicMastery New() =>
        MechanicMasteryRules.New(Guid.NewGuid(), MechanicKeys.Route, Now);

    private static MechanicAttempt Attempt(
        bool solved,
        bool usedHint = false,
        int mistakes = 0,
        PetBrainDifficulty difficulty = PetBrainDifficulty.Medium) =>
        new(solved, usedHint, mistakes, difficulty);
}

/// <summary>
/// Açıq məzmun seçimləri: uşağın sözü dolayı davranışdan güclüdür, amma
/// həmişəlik qapı bağlamır.
/// </summary>
public class ContentPreferenceRuleTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>«Daha az göstər» təkrarlandıqca müddət uzanır.</summary>
    [Fact]
    public void Tekrar_MuddetiUzadir()
    {
        var first = ContentPreferenceRules.ExpiryFor(0, Now);
        var second = ContentPreferenceRules.ExpiryFor(1, Now);

        Assert.True(second > first);
    }

    /// <summary>
    /// Müddət SONSUZ uzanmır: bir gün deyilən söz həmişəlik bir qapı
    /// bağlamamalıdır.
    /// </summary>
    [Fact]
    public void Muddet_SonsuzUzanmir()
    {
        var far = ContentPreferenceRules.ExpiryFor(100, Now);

        Assert.True(far <= Now.AddDays(ContentPreferenceRules.ShowLessMaxDays));
    }

    /// <summary>Bəyənmə «daha az göstər»dən GÜCLÜDÜR — səhvən silmək çətindir.</summary>
    [Fact]
    public void Beyenme_DahaAzGosterdenGuclüdur()
    {
        var like = ContentPreferenceRules.TraitDeltaFor(PetBrainContentPreferenceKind.Liked);
        var showLess = ContentPreferenceRules.TraitDeltaFor(PetBrainContentPreferenceKind.ShowLess);

        Assert.True(like > 0);
        Assert.True(showLess < 0);
        Assert.True(like > Math.Abs(showLess));
    }

    /// <summary>
    /// Valideyn bloku uşağın ZÖVQÜ haqqında heç nə demir — bal dəyişmir.
    /// </summary>
    [Fact]
    public void ValideynBloku_UsaginBalinaToxunmur() =>
        Assert.Equal(0, ContentPreferenceRules.TraitDeltaFor(PetBrainContentPreferenceKind.Blocked));

    /// <summary>Vaxtı keçmiş qeyd qərar qatına düşmür, amma sətir qalır.</summary>
    [Fact]
    public void VaxtiKecmisQeyd_QerarQatinaDusmur()
    {
        List<ContentPreference> preferences =
        [
            new()
            {
                Scope = PetBrainContentScope.Theme,
                Key = TraitKeys.Space,
                Kind = PetBrainContentPreferenceKind.ShowLess,
                ExpiresAt = Now.AddDays(-1)
            }
        ];

        Assert.Empty(ContentPreferenceRules.ActiveOf(preferences, Now));
        Assert.Empty(ContentPreferenceRules.ShowLessKeys(preferences, PetBrainContentScope.Theme, Now));
        Assert.Single(preferences);
    }
}

/// <summary>
/// Dəstək planı: açıq seçim təxmini HƏMİŞƏ üstələyir və əlçatanlıq heç vaxt
/// təxmin edilmir.
/// </summary>
public class SupportProfileTests
{
    /// <summary>
    /// <b>Açıq ayar təxmindən üstündür.</b> Valideyn «yalnız istəyəndə» deyibsə,
    /// uşağın üç çətin tapmacası bunu dəyişə bilmir.
    /// </summary>
    [Fact]
    public void AcigAyar_TexminiUsteleyir()
    {
        var settings = new ChildPersonalizationSettings
        {
            HintTiming = PetBrainHintTiming.OnRequest,
            HintTimingSource = PetBrainSettingSource.Parent
        };

        var plan = PersonalizationProfileFactory.SupportOf(settings, Struggling());

        Assert.Equal(PetBrainHintTiming.OnRequest, plan.Timing);
    }

    /// <summary>Heç kim toxunmayanda sistem köməyi bir az əvvəl təklif edir.</summary>
    [Fact]
    public void HecKimToxunmayanda_KomekBirAzEvvelTeklifOlunur()
    {
        var plan = PersonalizationProfileFactory.SupportOf(settings: null, Struggling());

        Assert.Equal(PetBrainHintTiming.Delayed, plan.Timing);
        Assert.Equal(PetBrainSettingSource.Inferred, plan.Source);
    }

    /// <summary>
    /// Bir-iki müşahidə KİFAYƏT ETMİR: uşaq haqqında tələsik nəticə
    /// çıxarılmır.
    /// </summary>
    [Fact]
    public void AzMusahide_TexminYaratmir()
    {
        var plan = PersonalizationProfileFactory.SupportOf(
            settings: null, [Outcome(mistakes: 3), Outcome(mistakes: 3)]);

        Assert.Equal(PetBrainHintTiming.OnRequest, plan.Timing);
    }

    /// <summary>
    /// <b>İpucunun FORMASI heç vaxt təxmin edilmir.</b> «Bu uşağa vizual izah
    /// lazımdır» qənaəti davranışdan çıxarıla bilməz.
    /// </summary>
    [Fact]
    public void IpucununFormasi_TexminEdilmir()
    {
        var plan = PersonalizationProfileFactory.SupportOf(settings: null, Struggling());

        Assert.Equal(PetBrainHintStyle.Visual, plan.Style);
    }

    /// <summary>
    /// <b>Fərdiləşdirmə söndürüləndə əlçatanlıq QALIR.</b> Bu, ayrı bir vəddir
    /// və bir açarla pozula bilməz.
    /// </summary>
    [Fact]
    public void FerdilesdirmeSondurulende_ElcatanliqQalir()
    {
        var settings = new ChildPersonalizationSettings
        {
            PersonalizationEnabled = false,
            ReducedMotion = true,
            LargeText = true,
            HighContrast = true
        };

        var profile = PersonalizationProfileFactory.Build(settings, []);

        Assert.False(profile.Enabled);
        Assert.True(profile.Accessibility.ReducedMotion);
        Assert.True(profile.Accessibility.LargeText);
        Assert.True(profile.Accessibility.HighContrast);
    }

    /// <summary>Söndürülmüş profil NEYTRALDIR — sürpriz və model mətni bağlanır.</summary>
    [Fact]
    public void SondurulmusProfil_Neytraldir()
    {
        var profile = PersonalizationProfileFactory.Build(
            new ChildPersonalizationSettings { PersonalizationEnabled = false }, []);

        Assert.False(profile.SurpriseEnabled);
        Assert.False(profile.AiNarrativeEnabled);
        Assert.Equal(PetBrainSessionLength.Medium, profile.SessionLength);
    }

    /// <summary>
    /// <b>Filter bubble mümkün deyil:</b> ən aşağı yenilik dözümündə də kəşf
    /// payı sıfır olmur.
    /// </summary>
    [Theory]
    [InlineData(PetBrainNoveltyTolerance.Low)]
    [InlineData(PetBrainNoveltyTolerance.Balanced)]
    [InlineData(PetBrainNoveltyTolerance.High)]
    public void KesfPayi_HecVaxtSifirOlmur(PetBrainNoveltyTolerance tolerance)
    {
        var profile = PersonalizationProfileFactory.Build(
            new ChildPersonalizationSettings { NoveltyTolerance = tolerance, SurpriseEnabled = false }, []);

        Assert.True(profile.ExplorationShare > 0);
    }

    /// <summary>Sessiya uzunluğu addım büdcəsini müəyyən edir.</summary>
    [Fact]
    public void SessiyaUzunlugu_AddimBudcesiniDeyisir()
    {
        var shortProfile = Profile(PetBrainSessionLength.Short);
        var longProfile = Profile(PetBrainSessionLength.Long);

        Assert.True(shortProfile.PreferredStepBudget < longProfile.PreferredStepBudget);
    }

    private static PersonalizationProfile Profile(PetBrainSessionLength length) =>
        PersonalizationProfileFactory.Build(
            new ChildPersonalizationSettings { SessionLength = length }, []);

    private static IReadOnlyList<MindRunOutcome> Struggling() =>
        [Outcome(mistakes: 3), Outcome(mistakes: 2), Outcome(mistakes: 4)];

    private static MindRunOutcome Outcome(int mistakes) => new(
        ExperienceCatalog.MarsRoverRescue,
        TraitKeys.Space,
        PetBrainExperienceType.Adventure,
        PetBrainRunStatus.Completed,
        ScorePercent: 60,
        HintsUsed: 0,
        Mistakes: mistakes,
        EndingKey: string.Empty);
}

/// <summary>
/// İpucunun FORMASI görünəndir, amma kömək MİQDARI dəyişmir.
/// </summary>
public class SupportVoiceTests
{
    /// <summary>Hər forma fərqli çərçivə verir.</summary>
    [Fact]
    public void HerForma_FerqliCercivedir()
    {
        var framed = Enum.GetValues<PetBrainHintStyle>()
            .Select(style => SupportVoice.Frame("mavi yolu izlə", style, "az"))
            .ToList();

        Assert.Equal(framed.Count, framed.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// <b>Forma kömək MİQDARINI dəyişmir.</b> İpucunun faktı hər variantda
    /// olduğu kimi qalır — əks halda forma seçimi gizli çətinlik
    /// tənzimləyicisinə çevrilərdi.
    /// </summary>
    [Theory]
    [InlineData(PetBrainHintStyle.Visual)]
    [InlineData(PetBrainHintStyle.StepByStep)]
    [InlineData(PetBrainHintStyle.Example)]
    [InlineData(PetBrainHintStyle.Rule)]
    public void Forma_KomeyinMiqdariniDeyismir(PetBrainHintStyle style)
    {
        const string hint = "mavi yolu izlə";

        Assert.Contains(hint, SupportVoice.Frame(hint, style, "az"), StringComparison.Ordinal);
    }

    /// <summary>Boş ipucu uydurulmur.</summary>
    [Fact]
    public void BosIpucu_Uydurulmur() =>
        Assert.Equal(string.Empty, SupportVoice.Frame(string.Empty, PetBrainHintStyle.Rule, "az"));

    /// <summary>Standart rejimdə kömək İSTƏNİLMƏDƏN açılmır.</summary>
    [Fact]
    public void StandartRejim_KomeyiOzuAcmir() =>
        Assert.False(SupportVoice.ShouldRevealHint(Plan(PetBrainHintTiming.OnRequest), mistakes: 5));

    /// <summary>Gecikmiş rejimdə kömək yalnız bir neçə səhvdən sonra görünür.</summary>
    [Fact]
    public void GecikmisRejim_BirNeceSehvdenSonraAcilir()
    {
        Assert.False(SupportVoice.ShouldRevealHint(Plan(PetBrainHintTiming.Delayed), mistakes: 0));
        Assert.True(SupportVoice.ShouldRevealHint(
            Plan(PetBrainHintTiming.Delayed), SupportVoice.DelayedHintAfterMistakes));
    }

    /// <summary>Dərhal rejimi açıq seçimdir və dərhal işləyir.</summary>
    [Fact]
    public void DerhalRejimi_DerhalIsleyir() =>
        Assert.True(SupportVoice.ShouldRevealHint(Plan(PetBrainHintTiming.Immediate), mistakes: 0));

    /// <summary>
    /// Variant sayı azalsa da BİRƏ düşmür: bir variant seçim deyil, düymədir.
    /// </summary>
    [Fact]
    public void AzVariant_SecimHuququnuElindenAlmir()
    {
        var narrowed = SupportVoice.Narrow(new[] { "a", "b", "c", "d" }, Reduced());

        Assert.Equal(SupportVoice.ReducedOptionLimit, narrowed.Count);
        Assert.True(narrowed.Count >= 2);
    }

    /// <summary>Daraltma SIRAYA toxunmur — hekayənin öz sırası qalır.</summary>
    [Fact]
    public void Daraltma_SirayaToxunmur()
    {
        var narrowed = SupportVoice.Narrow(new[] { "a", "b", "c" }, Reduced());

        Assert.Equal("a", narrowed[0]);
        Assert.Equal("b", narrowed[1]);
    }

    /// <summary>Dəstək istənilməyibsə bütün variantlar qalır.</summary>
    [Fact]
    public void DestekIstenilmeyibse_ButunVariantlarQalir()
    {
        string[] options = ["a", "b", "c"];

        Assert.Equal(options.Length, SupportVoice.Narrow(options, Plan(PetBrainHintTiming.OnRequest)).Count);
    }

    private static SupportPlan Plan(PetBrainHintTiming timing) => new(
        PetBrainHintStyle.Visual, timing,
        ReducedOptions: false, DemonstrationFirst: false, ExtraResponseTime: false,
        PetBrainSettingSource.Default);

    private static SupportPlan Reduced() => new(
        PetBrainHintStyle.Visual, PetBrainHintTiming.OnRequest,
        ReducedOptions: true, DemonstrationFirst: false, ExtraResponseTime: false,
        PetBrainSettingSource.Child);
}
