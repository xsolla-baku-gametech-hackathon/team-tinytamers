using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Mind;
using PetPal.Api.PetBrain.Recommendation;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Tövsiyə siyasəti — <b>iki mərhələ, izah edilə bilən bal, real
/// alternativlər</b>.
///
/// <para>Bütün testlər SAF qatı yoxlayır: baza yoxdur, saat yoxdur, təsadüf
/// yoxdur. «Niyə bu macəra?» sualının cavabı burada yenidən qurula
/// bilməlidir.</para>
/// </summary>
public class PetBrainRecommendationPolicyTests
{
    private const string Seed = "test-seed";

    /// <summary>
    /// Çəkilərin cəmi 1.0 olmalıdır — əks halda bütün ballar şişər və «80 bal»
    /// ifadəsi mənasını itirər.
    /// </summary>
    [Fact]
    public void StandartCekiler_CemiBirdir()
    {
        var options = new RecommendationPolicyOptions();

        Assert.True(options.Validate(out var error), error);
        Assert.Equal(1.0, options.WeightSum, precision: 4);
    }

    /// <summary>Səhv konfiqurasiya SƏSSİZ keçmir.</summary>
    [Fact]
    public void SehvCeki_RedOlunur()
    {
        var options = new RecommendationPolicyOptions { TopicFit = 0.9 };

        Assert.False(options.Validate(out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    /// <summary>
    /// <b>Eyni giriş, eyni seed, eyni siyasət → eyni nəticə.</b> Testlərin
    /// stabilliyi buna dayanır.
    /// </summary>
    [Fact]
    public void EyniGiris_EyniNeticeVerir()
    {
        var mind = MindStub.Build(interests: Space());

        var first = RecommendationPolicy.Decide(mind, Options(), Seed);
        var second = RecommendationPolicy.Decide(mind, Options(), Seed);

        Assert.Equal(
            first.Cards.Select(c => c.Candidate.Key),
            second.Cards.Select(c => c.Candidate.Key));
    }

    /// <summary>
    /// <b>Fərqli profil görünən fərqli nəticə verir.</b> Definition of Done-un
    /// birinci bəndi.
    /// </summary>
    [Fact]
    public void FerqliProfil_FerqliMaceraAlir()
    {
        var spaceChild = MindStub.Build(
            childId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            interests: Space(),
            mechanics: new Dictionary<string, int>
            {
                [MechanicKeys.Route] = 85,
                [MechanicKeys.Exploration] = 80
            });

        var makerChild = MindStub.Build(
            childId: Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"),
            interests: new Dictionary<string, int>
            {
                [TraitKeys.Animals] = 90,
                [TraitKeys.Nature] = 85
            },
            mechanics: new Dictionary<string, int>
            {
                [MechanicKeys.Building] = 90,
                [MechanicKeys.Decorating] = 88
            });

        var spacePick = RecommendationPolicy.Decide(spaceChild, Options(), Seed).Primary!.Candidate.Key;
        var makerPick = RecommendationPolicy.Decide(makerChild, Options(), Seed).Primary!.Candidate.Key;

        Assert.NotEqual(spacePick, makerPick);
    }

    /// <summary>
    /// <b>Mövzu ilə mexanika ayrı öyrənilir.</b> Uşaq kosmosu sevə, kosmos
    /// marşrutunu sevməyə bilər — və sistem bunu görməlidir.
    /// </summary>
    [Fact]
    public void Movzu_VeMexanika_AyriCekilir()
    {
        var interests = new Dictionary<string, int> { [TraitKeys.Space] = 90 };

        var lovesRoute = MindStub.Build(
            interests: interests,
            mechanics: new Dictionary<string, int> { [MechanicKeys.Route] = 95 });

        var dislikesRoute = MindStub.Build(
            interests: interests,
            mechanics: new Dictionary<string, int> { [MechanicKeys.Route] = 5 });

        var mars = ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!;

        var withRoute = RecommendationPolicy.Score(mars, lovesRoute, Options());
        var withoutRoute = RecommendationPolicy.Score(mars, dislikesRoute, Options());

        Assert.Equal(withRoute.TopicFit, withoutRoute.TopicFit);
        Assert.True(withRoute.MechanicFit > withoutRoute.MechanicFit);
        Assert.True(withRoute.Total > withoutRoute.Total);
    }

    /// <summary>
    /// Siyahı FƏRQLİ rollar daşıyır — üç «ən uyğun» kart seçim deyil, təkrardır.
    /// </summary>
    [Fact]
    public void Siyahi_FerqliRollarDasiyir()
    {
        var set = RecommendationPolicy.Decide(MindStub.Build(interests: Space()), Options(), Seed);

        Assert.True(set.Cards.Count > 1, "Uşaq ən azı bir alternativ görməlidir.");
        Assert.Equal(PetBrainRecommendationSlot.Primary, set.Cards[0].Slot);

        Assert.Equal(
            set.Cards.Count,
            set.Cards.Select(c => c.Candidate.Key).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// <b>Əsas kart HƏMİŞƏ ən yüksək baldır.</b> Kəşf payı onu ələ keçirsəydi,
    /// eyni profil eyni ekranda fərqli başlıq görərdi.
    /// </summary>
    [Fact]
    public void EsasKart_HemiseEnYuksekBaldir()
    {
        var set = RecommendationPolicy.Decide(MindStub.Build(interests: Space()), Options(), Seed);

        Assert.Equal(set.Ranked[0].Key, set.Primary!.Candidate.Key);
        Assert.False(set.Primary.WasExploration);
    }

    /// <summary>
    /// Siyahı bir mövzuya kilidlənmir.
    ///
    /// <para>DAVAM kartı istisnadır və bu, qəsdəndir: «keçən dəfəki dünyaya
    /// qayıt» təklifi təbii olaraq eyni mövzudadır. Qalan kartlar isə
    /// mövzuca fərqli olmalıdır — üç kosmos kartı seçim deyil, təkrardır.</para>
    /// </summary>
    [Fact]
    public void Siyahi_BirMovzuyaKilidlenmir()
    {
        var set = RecommendationPolicy.Decide(MindStub.Build(interests: Space()), Options(), Seed);

        var themes = set.Cards.Select(c => c.Candidate.Theme).ToList();

        Assert.True(themes.Distinct(StringComparer.Ordinal).Count() > 1,
            "Bütün kartlar eyni mövzudadır — bu, filter bubble-dır.");

        var withoutContinuity = set.Cards
            .Where(c => c.Slot != PetBrainRecommendationSlot.Continuity)
            .Select(c => c.Candidate.Theme)
            .ToList();

        Assert.Equal(
            withoutContinuity.Count,
            withoutContinuity.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// <b>Yaş həddi TƏHLÜKƏSİZLİK sərhədidir</b> — süzgəc yumşalanda da qalır.
    /// </summary>
    [Fact]
    public void YasHeddi_YumsalmadaDaQalir()
    {
        var mind = MindStub.Build(
            age: 5,
            declinedTemplates: ExperienceCatalog.Templates.Select(t => t.Key).ToHashSet(StringComparer.Ordinal));

        var set = RecommendationPolicy.Decide(mind, Options(), Seed);

        Assert.All(set.Cards, card => Assert.True(card.Candidate.Template.MinAge <= 5));
    }

    /// <summary>Valideyn bloku namizəd hovuzundan TAM çıxarır.</summary>
    [Fact]
    public void ValideynBloku_NamizedHovuzundanCixarir()
    {
        var mind = MindStub.Build(
            interests: Space(),
            blockedThemes: new HashSet<string>(StringComparer.Ordinal) { TraitKeys.Space });

        var set = RecommendationPolicy.Decide(mind, Options(), Seed);

        Assert.DoesNotContain(set.Cards, c => c.Candidate.Theme == TraitKeys.Space);

        Assert.Contains(set.Filtered,
            f => f.Reason == PetBrainFilterReason.ParentBlocked);
    }

    /// <summary>
    /// Blok süzgəc YUMŞALANDA da qalır: uşağa «heç nə yoxdur» demək blokdan
    /// yayınmaq üçün bəhanə ola bilməz.
    /// </summary>
    [Fact]
    public void ValideynBloku_YumsalmadaDaQalir()
    {
        var all = ExperienceCatalog.Templates.Select(t => t.Key).ToHashSet(StringComparer.Ordinal);

        var mind = MindStub.Build(
            declinedTemplates: all,
            blockedThemes: new HashSet<string>(StringComparer.Ordinal) { TraitKeys.Space });

        var set = RecommendationPolicy.Decide(mind, Options(), Seed);

        Assert.DoesNotContain(set.Cards, c => c.Candidate.Theme == TraitKeys.Space);
    }

    /// <summary>
    /// Hər şey süzülübsə süzgəc yumşalır — uşağa «sənə heç nə təklif etmirəm»
    /// ekranı göstərmək ən pis nəticədir.
    /// </summary>
    [Fact]
    public void HamisiSuzulubse_SuzgecYumsalir()
    {
        var all = ExperienceCatalog.Templates.Select(t => t.Key).ToHashSet(StringComparer.Ordinal);

        var set = RecommendationPolicy.Decide(MindStub.Build(declinedTemplates: all), Options(), Seed);

        Assert.NotNull(set.Primary);
    }

    /// <summary>Yumurta heç bir macəra təklifi almır.</summary>
    [Fact]
    public void Yumurta_TeklifAlmir()
    {
        var set = RecommendationPolicy.Decide(MindStub.Build(hatched: false), Options(), Seed);

        Assert.Null(set.Primary);
        Assert.All(set.Filtered, f => Assert.Equal(PetBrainFilterReason.PetNotHatched, f.Reason));
    }

    /// <summary>Süzülən hər namizədin SƏBƏBİ yazılır — jurnal susmur.</summary>
    [Fact]
    public void SuzulenNamized_SebebDasiyir()
    {
        var mind = MindStub.Build(
            blockedTemplates: new HashSet<string>(StringComparer.Ordinal)
            {
                ExperienceCatalog.MarsRoverRescue
            });

        var set = RecommendationPolicy.Decide(mind, Options(), Seed);

        Assert.All(set.Filtered, f => Assert.NotEqual(PetBrainFilterReason.None, f.Reason));
    }

    /// <summary>
    /// <b>Açıq «daha az göstər» görünən nəticə verir.</b> Uşaq dediyinin
    /// nəticəsini görməlidir.
    /// </summary>
    [Fact]
    public void DahaAzGoster_KartiGeriCekir()
    {
        var mind = MindStub.Build(
            interests: Space(),
            showLessTemplates: new HashSet<string>(StringComparer.Ordinal)
            {
                ExperienceCatalog.MarsRoverRescue
            });

        var set = RecommendationPolicy.Decide(mind, Options(), Seed);

        Assert.NotEqual(ExperienceCatalog.MarsRoverRescue, set.Primary!.Candidate.Key);
    }

    /// <summary>Açıq bəyənmə namizədi irəli çəkir.</summary>
    [Fact]
    public void Beyenme_NamizediIreliCekir()
    {
        var neutral = MindStub.Build();
        var liked = MindStub.Build(
            likedTemplates: new HashSet<string>(StringComparer.Ordinal)
            {
                ExperienceCatalog.ForestFriendsParade
            });

        var forest = ExperienceCatalog.Find(ExperienceCatalog.ForestFriendsParade)!;

        var before = RecommendationPolicy.Score(forest, neutral, Options());
        var after = RecommendationPolicy.Score(forest, liked, Options());

        Assert.True(after.Total > before.Total);
        Assert.True(after.ExplicitAdjustment > 0);
    }

    /// <summary>Ekran vaxtı azalanda uzun macəra təklif olunmur.</summary>
    [Fact]
    public void EkranVaxtiAzalanda_UzunMaceraTeklifOlunmur()
    {
        var options = Options();
        options.ShortSessionMaxMinutes = 3;

        var mind = MindStub.Build(screenTime: PetBrainScreenTimeBand.Ending);
        var set = RecommendationPolicy.Decide(mind, options, Seed);

        Assert.All(set.Cards, card =>
            Assert.True(card.Candidate.Template.TargetMinutes <= options.ShortSessionMaxMinutes));
    }

    /// <summary>
    /// Sessiya uzunluğu SEÇİMİ sıralamanı dəyişir.
    ///
    /// <para>Test kataloqun REAL aralığına baxır (4–5 mərhələ, 3–4 dəqiqə):
    /// hazırda «gerçək uzun» macəra yoxdur, ona görə iddia mütləq dəyər deyil,
    /// NİSBƏTDİR — qısa seçən uşaq üçün ən qısa macəra, uzun seçən uşaq üçün
    /// ən uzunu qabağa keçir.</para>
    /// </summary>
    [Fact]
    public void SessiyaUzunlugu_SiralamaniDeyisir()
    {
        var shortMind = MindStub.Build(personalization: Profile(PetBrainSessionLength.Short));
        var longMind = MindStub.Build(personalization: Profile(PetBrainSessionLength.Long));

        var shortest = ExperienceCatalog.Templates
            .OrderBy(t => t.StageCount).ThenBy(t => t.TargetMinutes).First();

        var longest = ExperienceCatalog.Templates
            .OrderByDescending(t => t.StageCount).ThenByDescending(t => t.TargetMinutes).First();

        Assert.True(
            RecommendationPolicy.PaceFit(shortest, shortMind)
            > RecommendationPolicy.PaceFit(longest, shortMind),
            "Qısa seçən uşaq üçün ən qısa macəra daha yaxşı uyğun gəlməlidir.");

        Assert.True(
            RecommendationPolicy.PaceFit(longest, longMind)
            > RecommendationPolicy.PaceFit(shortest, longMind),
            "Uzun seçən uşaq üçün ən uzun macəra daha yaxşı uyğun gəlməlidir.");
    }

    /// <summary>Bacarığa uyğun çətinlik daha yüksək bal alır.</summary>
    [Fact]
    public void UygunCetinlik_DahaYuksekBalAlir()
    {
        var mars = ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!;

        var matched = MindStub.Build(
            difficulty: PetBrainDifficulty.Medium,
            mechanicBands: mars.MechanicAffinity.ToDictionary(
                key => key, _ => PetBrainDifficulty.Medium, StringComparer.Ordinal));

        var mismatched = MindStub.Build(
            difficulty: PetBrainDifficulty.Hard,
            mechanicBands: mars.MechanicAffinity.ToDictionary(
                key => key, _ => PetBrainDifficulty.Easy, StringComparer.Ordinal));

        Assert.True(
            RecommendationPolicy.MasteryChallengeFit(mars, matched)
            > RecommendationPolicy.MasteryChallengeFit(mars, mismatched));
    }

    /// <summary>Ustalıq məlumatı yoxdursa namizəd CƏZALANMIR.</summary>
    [Fact]
    public void UstaliqMelumatiYoxdursa_NamizedCezalanmir()
    {
        var mars = ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!;

        Assert.Equal(
            RecommendationPolicy.UnknownMasteryFit,
            RecommendationPolicy.MasteryChallengeFit(mars, MindStub.Build()));
    }

    /// <summary>Təzəcə oynanmış şablon aydın yenilik cəzası alır.</summary>
    [Fact]
    public void TezeceOynanmis_YenilikCezasiAlir()
    {
        var mars = ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!;

        var fresh = MindStub.Build();
        var played = MindStub.Build(recentOutcomes: [Outcome(ExperienceCatalog.MarsRoverRescue)]);

        Assert.True(
            RecommendationPolicy.NoveltyValue(mars, played)
            < RecommendationPolicy.NoveltyValue(mars, fresh) - 30);
    }

    /// <summary>
    /// Təzəcə oynanmış macəra SÜZÜLMÜR — təkrar oynamaq ən güclü müsbət
    /// siqnaldır və onu görməyə davam etməliyik.
    /// </summary>
    [Fact]
    public void TezeceOynanmis_SuzulmurYalnizGeriCekilir()
    {
        var mind = MindStub.Build(recentOutcomes: [Outcome(ExperienceCatalog.MarsRoverRescue)]);
        var set = RecommendationPolicy.Decide(mind, Options(), Seed);

        Assert.Contains(set.Ranked, c => c.Key == ExperienceCatalog.MarsRoverRescue);
        Assert.DoesNotContain(set.Filtered, f => f.TemplateKey == ExperienceCatalog.MarsRoverRescue);
    }

    /// <summary>Hər kart üçün ən azı bir izah SƏBƏBİ var — kart susmur.</summary>
    [Fact]
    public void HerKart_IzahSebebiDasiyir()
    {
        var set = RecommendationPolicy.Decide(MindStub.Build(interests: Space()), Options(), Seed);

        Assert.All(set.Cards, card => Assert.NotEmpty(card.Candidate.Why));
    }

    /// <summary>
    /// <b>Sistem dürüstdür:</b> profil zəif tanınanda bunu gizlətmir.
    /// </summary>
    [Fact]
    public void ZeifTaninanProfil_IzahdaEtirafEdilir()
    {
        var set = RecommendationPolicy.Decide(
            MindStub.Build(profileConfidence: 5), Options(), Seed);

        Assert.Contains(set.Cards, c => c.Candidate.Why.Contains(PetBrainWhyReason.StillLearning));
    }

    /// <summary>
    /// Sürpriz bağlananda təhlükəsiz kəşf kartı GÖSTƏRİLMİR — vəd
    /// pozulmamalıdır.
    /// </summary>
    [Fact]
    public void SurprizBaglananda_KesfKartiGosterilmir()
    {
        var settings = new ChildPersonalizationSettings
        {
            SurpriseEnabled = false,
            NoveltyTolerance = PetBrainNoveltyTolerance.High
        };

        var mind = MindStub.Build(
            personalization: PersonalizationProfileFactory.Build(settings, []));

        var set = RecommendationPolicy.Decide(mind, Options(), Seed);

        Assert.DoesNotContain(set.Cards,
            c => c.Slot == PetBrainRecommendationSlot.SafeExploration);
    }

    /// <summary>Zəif tanınan profil daha çox kəşf payı alır.</summary>
    [Fact]
    public void ZeifTaninanProfil_DahaCoxKesfEdir()
    {
        var options = Options();

        var lowShare = ExploreRate(profileConfidence: 5, options);
        var highShare = ExploreRate(profileConfidence: 90, options);

        Assert.True(lowShare >= highShare,
            $"Zəif tanınan profil daha çox kəşf etməlidir: {lowShare} vs {highShare}.");
    }

    /// <summary>Bal parçalanması VALİDEYN üçün tam gəlir.</summary>
    [Fact]
    public void BalParcalanmasi_ButunOlculeriDasiyir()
    {
        var set = RecommendationPolicy.Decide(MindStub.Build(interests: Space()), Options(), Seed);
        var factors = PersonalizationMapper.FactorsOf(set.Primary!.Candidate, Options(), "az");

        foreach (var key in new[]
                 { "topic", "mechanic", "mastery", "style", "support", "pace", "continuity", "reward", "novelty" })
            Assert.Contains(factors, f => f.Key == key);

        Assert.All(factors, f => Assert.False(string.IsNullOrWhiteSpace(f.Label)));
    }

    /// <summary>
    /// Kataloqdakı hər mexanika açarı TƏSDİQLƏNMİŞ siyahıdandır —
    /// uydurma açar profilə düşə bilməz.
    /// </summary>
    [Fact]
    public void Kataloq_YalnizTesdiqlenmisMexanikalariIsledir() =>
        Assert.All(ExperienceCatalog.Templates, template =>
            Assert.All(template.MechanicAffinity, key =>
                Assert.True(MechanicKeys.IsKnown(key), $"Naməlum mexanika: {key}")));

    /// <summary>
    /// Hər mexanikanın ƏN AZI BİR istehlakçısı var — saxlanılan, amma heç nəyə
    /// təsir etməyən ölçü profil deyil, ölü sütundur.
    /// </summary>
    [Fact]
    public void HerMexanika_EnAziBirMacerada_Islenir()
    {
        var used = ExperienceCatalog.Templates
            .SelectMany(t => t.MechanicAffinity)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(MechanicKeys.All, key =>
            Assert.True(used.Contains(key), $"«{key}» heç bir macərada işlənmir — ölü sütundur."));
    }

    /// <summary>
    /// Mükafat elanı DÜRÜSTDÜR: kosmetik açarı olmayan macəra «pet geyimi»
    /// vəd edə bilməz.
    /// </summary>
    [Fact]
    public void MukafatElani_Durustdur() =>
        Assert.All(ExperienceCatalog.Templates, template =>
        {
            if (template.RewardFlavor == PetBrainRewardPreference.PetCosmetic)
                Assert.False(string.IsNullOrWhiteSpace(template.RewardCode),
                    $"«{template.Key}» kosmetik vəd edir, amma kodu yoxdur.");
        });

    /// <summary>Mexanika açarları xassə açarları ilə TOQQUŞMUR.</summary>
    [Fact]
    public void MexanikaAcarlari_XasseAcarlariIleToqqusmur()
    {
        var traits = TraitKeys.Interests.Concat(TraitKeys.PlayStyles).ToHashSet(StringComparer.Ordinal);

        Assert.All(MechanicKeys.All, key =>
            Assert.False(traits.Contains(key), $"«{key}» həm mexanika, həm xassə açarıdır."));
    }

    private static double ExploreRate(int profileConfidence, RecommendationPolicyOptions options)
    {
        var explored = 0;

        for (var i = 0; i < 200; i++)
        {
            var mind = MindStub.Build(
                childId: Guid.Parse($"00000000-0000-0000-0000-{i:D12}"),
                profileConfidence: profileConfidence);

            if (RecommendationPolicy.ShouldExplore(mind, options, Seed))
                explored++;
        }

        return explored / 200.0;
    }

    private static PersonalizationProfile Profile(PetBrainSessionLength length) =>
        PersonalizationProfileFactory.Build(
            new ChildPersonalizationSettings { SessionLength = length }, []);

    private static Dictionary<string, int> Space() => new(StringComparer.Ordinal)
    {
        [TraitKeys.Space] = 90,
        [TraitKeys.Science] = 75
    };

    private static MindRunOutcome Outcome(string templateKey) => new(
        templateKey,
        ExperienceCatalog.Find(templateKey)!.Theme,
        PetBrainExperienceType.Adventure,
        PetBrainRunStatus.Completed,
        ScorePercent: 90,
        HintsUsed: 0,
        Mistakes: 0,
        EndingKey: string.Empty);

    private static RecommendationPolicyOptions Options() => new();
}
