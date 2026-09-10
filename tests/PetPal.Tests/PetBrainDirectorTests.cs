using PetPal.Api.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Direktorun SAF qərar qatı: I/O yoxdur, saat yoxdur, təsadüf yoxdur.
///
/// <para>Ən vacib testlər nümayişin özünü qoruyur: Aylin Marsı, Mia isə
/// Əjdahanı almalıdır. Bunlar "böyük ehtimalla" deyil, HƏR sürpriz dəyəri
/// üçün doğru olmalıdır — ona görə aşağıda təkcə qalib yox, QALİBİN FƏRQİ də
/// yoxlanılır.</para>
/// </summary>
public class PetBrainDirectorTests
{
    // ---------- Nümayiş profilləri (DemoDataSeeder ilə eyni rəqəmlər) ----------

    private static readonly Guid AylinId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MiaId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Dictionary<string, int> AylinInterests => new()
    {
        [TraitKeys.Space] = 85,
        [TraitKeys.Science] = 75,
        [TraitKeys.Puzzles] = 90,
        [TraitKeys.Animals] = 10,
        [TraitKeys.Fantasy] = 10,
        [TraitKeys.Stories] = 10,
        [TraitKeys.Nature] = 12,
        [TraitKeys.Ocean] = 10
    };

    private static Dictionary<string, int> AylinPlayStyles => new()
    {
        [TraitKeys.Explorer] = 75,
        [TraitKeys.ProblemSolver] = 85,
        [TraitKeys.Creative] = 10,
        [TraitKeys.Caring] = 25,
        [TraitKeys.Playful] = 25
    };

    private static Dictionary<string, int> MiaInterests => new()
    {
        [TraitKeys.Fantasy] = 90,
        [TraitKeys.Animals] = 85,
        [TraitKeys.Stories] = 82,
        [TraitKeys.Nature] = 30,
        [TraitKeys.Ocean] = 25,
        [TraitKeys.Space] = 15,
        [TraitKeys.Science] = 15,
        [TraitKeys.Puzzles] = 10
    };

    private static Dictionary<string, int> MiaPlayStyles => new()
    {
        [TraitKeys.Creative] = 88,
        [TraitKeys.Caring] = 60,
        [TraitKeys.Playful] = 40,
        [TraitKeys.Explorer] = 30,
        [TraitKeys.ProblemSolver] = 20
    };

    /// <summary>Aylin Ay macərasını artıq oynayıb — yenilik onu Marsa itələməlidir.</summary>
    private static PetBrainDirectorContext AylinContext(int age = 9) => new(
        ChildId: AylinId,
        Age: age,
        Language: "az",
        Interests: AylinInterests,
        PlayStyles: AylinPlayStyles,
        RecentRuns:
        [
            new RunHistoryEntry(ExperienceCatalog.MoonCrystalRescue, TraitKeys.Space, PetBrainRunStatus.Completed)
        ],
        CompletedTemplates: new HashSet<string>(StringComparer.Ordinal) { ExperienceCatalog.MoonCrystalRescue },
        Difficulty: PetBrainDifficulty.Medium,
        PetIsHatched: true);

    private static PetBrainDirectorContext MiaContext(int age = 8) => new(
        ChildId: MiaId,
        Age: age,
        Language: "az",
        Interests: MiaInterests,
        PlayStyles: MiaPlayStyles,
        RecentRuns: [],
        CompletedTemplates: new HashSet<string>(StringComparer.Ordinal),
        Difficulty: PetBrainDifficulty.Medium,
        PetIsHatched: true);

    // ==================== Nümayişin sərt tələbləri ====================

    [Fact]
    public void Aylin_MarsMacerasiniAlir()
    {
        var decision = AdaptivePetDirector.Decide(AylinContext());

        Assert.NotNull(decision);
        Assert.Equal(ExperienceCatalog.MarsRoverRescue, decision!.Template.Key);
    }

    [Fact]
    public void Mia_EjdahaMacerasiniAlir()
    {
        var decision = AdaptivePetDirector.Decide(MiaContext());

        Assert.NotNull(decision);
        Assert.Equal(ExperienceCatalog.DragonLostColors, decision!.Template.Key);
    }

    /// <summary>
    /// Nümayiş "ümid" deyil, ZƏMANƏT olmalıdır.
    ///
    /// <para>Sürpriz payı ±<see cref="AdaptivePetDirector.SurpriseHalfBand"/> baldır,
    /// yəni iki namizədin fərqi ən pis halda iki qatı qədər sürüşə bilər. Bu test
    /// qalibin fərqinin həmin zolaqdan BÖYÜK olduğunu yoxlayır: yəni sürpriz
    /// hansı dəyəri alsa da nəticə dəyişmir.</para>
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NumayisSecimi_SurprizPayindanAsiliDeyil(bool aylin)
    {
        var ranked = AdaptivePetDirector.Rank(aylin ? AylinContext() : MiaContext());

        Assert.True(ranked.Count >= 2);

        // Sürprizi çıxarıb yalnız uyğunluq + yenilik payına baxırıq.
        static double WithoutSurprise(DirectorCandidate c) =>
            (AdaptivePetDirector.FitWeight * c.FitScore) + (AdaptivePetDirector.NoveltyWeight * c.NoveltyScore);

        var leader = WithoutSurprise(ranked[0]);
        var runnerUp = ranked.Skip(1).Max(WithoutSurprise);

        Assert.True(leader - runnerUp > AdaptivePetDirector.SurpriseHalfBand * 2,
            $"Qalibin fərqi {leader - runnerUp:F1} baldır, sürpriz zolağı isə " +
            $"±{AdaptivePetDirector.SurpriseHalfBand}. Nəticə təsadüfdən asılı ola bilər.");
    }

    /// <summary>Eyni kontekst həmişə eyni qərar — proses yenidən başlasa da.</summary>
    [Fact]
    public void Qerar_DeterministikdirVeTekrarlanir()
    {
        var first = AdaptivePetDirector.Decide(AylinContext())!.Template.Key;

        for (var i = 0; i < 5; i++)
            Assert.Equal(first, AdaptivePetDirector.Decide(AylinContext())!.Template.Key);
    }

    /// <summary>
    /// Sürpriz TARİXDƏN asılı olmamalıdır: nümayiş hansı gün göstərilməsindən
    /// asılı olaraq başqa nəticə verməməlidir.
    /// </summary>
    [Fact]
    public void Surpriz_YalnizUsaqVeSablondanAsilidir()
    {
        var a = AdaptivePetDirector.SurpriseScore(AylinId, ExperienceCatalog.MarsRoverRescue);
        var b = AdaptivePetDirector.SurpriseScore(AylinId, ExperienceCatalog.MarsRoverRescue);
        var other = AdaptivePetDirector.SurpriseScore(MiaId, ExperienceCatalog.MarsRoverRescue);

        Assert.Equal(a, b);
        Assert.NotEqual(a, other);
        Assert.InRange(a, 0, 100);
    }

    // ==================== Yenilik ====================

    /// <summary>
    /// Son Ay macərası AYIN təkrarını cəzalandırır, amma KOSMOS marağını
    /// olduğu kimi saxlayır — bu, direktorun ən incə qaydasıdır.
    /// </summary>
    [Fact]
    public void SonAyMacerasi_AyiCezalandirirKosmosuYox()
    {
        var context = AylinContext();

        var mars = ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!;
        var moon = ExperienceCatalog.Find(ExperienceCatalog.MoonCrystalRescue)!;

        var marsNovelty = AdaptivePetDirector.NoveltyScore(mars, context.RecentRuns);
        var moonNovelty = AdaptivePetDirector.NoveltyScore(moon, context.RecentRuns);

        // Eyni şablonun cəzası mövzu cəzasından ÇOX BÖYÜK olmalıdır.
        Assert.True(moonNovelty < marsNovelty - 30,
            $"Ay {moonNovelty}, Mars {marsNovelty} — şablon cəzası mövzu cəzasından kifayət qədər böyük deyil.");

        // Kosmos marağı toxunulmaz qalır: hər iki şablonun uyğunluğu hələ də yüksəkdir.
        Assert.True(AdaptivePetDirector.FitScore(mars, context) > 50);
        Assert.True(AdaptivePetDirector.FitScore(moon, context) > 50);
    }

    [Fact]
    public void Yenilik_HecOynanmamisSablonaTamBalVerir()
    {
        var dragon = ExperienceCatalog.Find(ExperienceCatalog.DragonLostColors)!;

        Assert.Equal(100, AdaptivePetDirector.NoveltyScore(dragon, []));
    }

    // ==================== Təhlükəsizlik sərhədləri ====================

    [Fact]
    public void Yumurta_HecBirMaceraTeklifAlmir()
    {
        var context = AylinContext() with { PetIsHatched = false };

        Assert.Null(AdaptivePetDirector.Decide(context));
        Assert.Empty(AdaptivePetDirector.Rank(context));
    }

    /// <summary>Yaş həddi keçilmir — bu, təhlükəsizlik sərhədidir.</summary>
    [Fact]
    public void KicikYas_YasHeddiAsanSablonlariGizledir()
    {
        var ranked = AdaptivePetDirector.Rank(AylinContext(age: 5));

        Assert.DoesNotContain(ranked, c => c.Template.MinAge > 5);
        Assert.All(ranked, c => Assert.True(c.Template.MinAge <= 5));
    }

    [Fact]
    public void Sebebler_IkiIleDordArasindadirVeBosDeyil()
    {
        foreach (var context in new[] { AylinContext(), MiaContext() })
        {
            var decision = AdaptivePetDirector.Decide(context)!;

            Assert.InRange(decision.Reasons.Count, 2, 4);
            Assert.All(decision.Reasons, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
        }
    }

    /// <summary>Kataloqdakı hər mövzu təsdiqlənmiş taksonomiyadan olmalıdır.</summary>
    [Fact]
    public void Kataloq_YalnizTesdiqlenmisMovzulariIsledir()
    {
        Assert.All(ExperienceCatalog.Templates,
            template => Assert.Contains(template.Theme, TraitKeys.AllowedThemes));
    }

    /// <summary>Hər şablon oynanandır: mərhələsi olmayan macəra boş vəd olardı.</summary>
    [Fact]
    public void Kataloq_HerSablonunOynanilanMerheleleriVar()
    {
        Assert.All(ExperienceCatalog.Templates, template =>
        {
            Assert.True(template.StageCount >= 3, $"{template.Key} — üç mərhələdən azdır.");

            // Giriş istisna olmaqla hər mərhələdə seçim olmalıdır (tapmaca
            // variantlarını server qurur).
            Assert.All(template.Stages.Where(s => s.Kind == PetBrainStageKind.Choice),
                stage => Assert.True(stage.Options.Count >= 2,
                    $"{template.Key} — seçim mərhələsində iki variantdan azdır."));
        });
    }

    /// <summary>Hər seçimin xassə təsiri TƏSDİQLƏNMİŞ açarlara düşməlidir.</summary>
    [Fact]
    public void Kataloq_SecimlerYalnizTaninanXasseleriDeyisir()
    {
        foreach (var template in ExperienceCatalog.Templates)
        foreach (var stage in template.Stages)
        foreach (var option in stage.Options)
        foreach (var delta in option.Traits)
        {
            Assert.True(TraitKeys.CategoryOf(delta.Key) is not null,
                $"{template.Key}/{option.Key} — naməlum xassə açarı: {delta.Key}");

            Assert.InRange(delta.Delta, -TraitKeys.MaxDeltaPerAction, TraitKeys.MaxDeltaPerAction);
        }
    }

    /// <summary>Yalnız iki macəranın kosmetik mükafatı var və onlar UNİKALDIR.</summary>
    [Fact]
    public void Kataloq_KosmetikMukafatlarTekrarlanmir()
    {
        var codes = ExperienceCatalog.Templates
            .Select(t => t.RewardCode)
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Contains(ExperienceCatalog.HelmetMars, codes);
        Assert.Contains(ExperienceCatalog.WingsRainbow, codes);
    }

    /// <summary>Hər şablonun hər iki dildə mətni olmalıdır — biri unudulsa test tutur.</summary>
    [Fact]
    public void Kataloq_HerSablonIkiDildedir()
    {
        Assert.All(ExperienceCatalog.Templates, template =>
        {
            Assert.NotEqual(template.Title("az"), template.Title("en"));
            Assert.NotEqual(template.Intro("az"), template.Intro("en"));
            Assert.NotEqual(template.Celebration("az"), template.Celebration("en"));

            foreach (var stage in template.Stages)
            {
                Assert.NotEqual(stage.Prompt("az"), stage.Prompt("en"));
                Assert.NotEqual(stage.PetLine("az"), stage.PetLine("en"));
            }
        });
    }
}
