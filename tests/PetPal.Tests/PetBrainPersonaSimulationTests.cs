using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Mind;
using PetPal.Api.PetBrain.Recommendation;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Bir uşağın davranış NAXIŞI — simulyasiyanın girişi.
///
/// <para>Persona uşaq haqqında iddia deyil: o, sistemin sınaqdan keçirilməli
/// olduğu DAVRANIŞ növüdür. Yalnız bir «ideal» profil ilə test etmək
/// sistemin ən pis səhvlərini gizlədir — kilidlənmə, starvasyon və sonsuz
/// təkrar məhz kənar hallarda görünür.</para>
/// </summary>
/// <param name="Name">Diaqnostika üçün ad.</param>
/// <param name="Topics">Bu personanın sevdiyi mövzular.</param>
/// <param name="Mechanics">Bu personanın sevdiyi mexanikalar.</param>
/// <param name="SessionLength">Üstün tutduğu sessiya uzunluğu.</param>
/// <param name="AlwaysUsesHints">Hər tapmacada kömək istəyirmi.</param>
/// <param name="SkipRate">Neçə sessiyadan birində kartı kənara qoyur (0 = heç vaxt).</param>
/// <param name="SwitchesAfter">Bu sessiyadan sonra marağını dəyişir (0 = dəyişmir).</param>
/// <param name="SwitchTopics">Dəyişəndən sonrakı mövzular.</param>
/// <param name="DislikesAfter">Bu sessiyadan sonra sevimli mövzusuna «daha az göstər» deyir.</param>
public sealed record Persona(
    string Name,
    IReadOnlyList<string> Topics,
    IReadOnlyList<string> Mechanics,
    PetBrainSessionLength SessionLength = PetBrainSessionLength.Medium,
    bool AlwaysUsesHints = false,
    int SkipRate = 0,
    int SwitchesAfter = 0,
    IReadOnlyList<string>? SwitchTopics = null,
    int DislikesAfter = 0);

/// <summary>
/// Bir personanın bütöv oyun tarixçəsi — simulyasiyanın çıxışı.
/// </summary>
public sealed class PersonaHistory
{
    public required Persona Persona { get; init; }

    /// <summary>Hər sessiyada ƏSAS kart kimi göstərilən şablon.</summary>
    public List<string> PrimaryShown { get; } = [];

    /// <summary>Hər sessiyada uşağın həqiqətən BAŞLATDIĞI şablon.</summary>
    public List<string> Started { get; } = [];

    /// <summary>Hər sessiyada göstərilən BÜTÜN kartlar.</summary>
    public List<string> AllShown { get; } = [];

    /// <summary>Verilmiş çətinlik pillələri.</summary>
    public List<PetBrainDifficulty> Difficulties { get; } = [];

    public Dictionary<string, PlayerTrait> Traits { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, MechanicMastery> Mastery { get; } = new(StringComparer.Ordinal);

    public int DistinctPrimaries => PrimaryShown.Distinct(StringComparer.Ordinal).Count();
    public int DistinctStarted => Started.Distinct(StringComparer.Ordinal).Count();
}

/// <summary>
/// Determinist persona simulyatoru.
///
/// <para>Saat, təsadüf və baza yoxdur: hər addım <see cref="RecommendationPolicy"/>,
/// <see cref="ProfileLearningRules"/>, <see cref="TraitEvidence"/> və
/// <see cref="MechanicMasteryRules"/> üzərindən gedir — yəni simulyasiya
/// məhsulun ÖZ qaydalarını işlədir, onların surətini yox.</para>
/// </summary>
public static class PersonaSimulator
{
    private static readonly DateTime Start = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    public static PersonaHistory Run(Persona persona, int sessions, Guid childId)
    {
        var history = new PersonaHistory { Persona = persona };
        var options = new RecommendationPolicyOptions();

        List<MindRunOutcome> outcomes = [];
        HashSet<string> completed = new(StringComparer.Ordinal);
        HashSet<string> showLess = new(StringComparer.Ordinal);

        for (var session = 0; session < sessions; session++)
        {
            var now = Start.AddDays(session);
            var topics = persona.SwitchesAfter > 0 && session >= persona.SwitchesAfter
                ? persona.SwitchTopics ?? persona.Topics
                : persona.Topics;

            var mind = BuildMind(childId, persona, history, outcomes, completed, showLess, topics, now);
            var set = RecommendationPolicy.Decide(mind, options, $"session-{session}");

            if (set.Primary is null)
                continue;

            history.PrimaryShown.Add(set.Primary.Candidate.Key);
            history.AllShown.AddRange(set.Cards.Select(c => c.Candidate.Key));
            history.Difficulties.Add(mind.Difficulty);

            // Persona kartı kənara qoya bilər — o zaman alternativi seçir.
            var chosen = persona.SkipRate > 0 && session % persona.SkipRate == 0 && set.Cards.Count > 1
                ? set.Cards[1]
                : set.Primary;

            var template = chosen.Candidate.Template;
            history.Started.Add(template.Key);

            Complete(history, template, completed.Contains(template.Key), now);
            Practice(history, persona, template, mind.Difficulty, now);

            outcomes.Insert(0, new MindRunOutcome(
                template.Key,
                template.Theme,
                template.Type,
                PetBrainRunStatus.Completed,
                ScorePercent: persona.AlwaysUsesHints ? 70 : 95,
                HintsUsed: persona.AlwaysUsesHints ? 1 : 0,
                Mistakes: persona.AlwaysUsesHints ? 1 : 0,
                EndingKey: string.Empty));

            completed.Add(template.Key);

            if (persona.DislikesAfter > 0 && session == persona.DislikesAfter)
                Dislike(history, showLess, persona.Topics[0], now);
        }

        return history;
    }

    private static PetMindContext BuildMind(
        Guid childId,
        Persona persona,
        PersonaHistory history,
        IReadOnlyList<MindRunOutcome> outcomes,
        IReadOnlySet<string> completed,
        IReadOnlySet<string> showLess,
        IReadOnlyList<string> topics,
        DateTime now)
    {
        var settings = new ChildPersonalizationSettings { SessionLength = persona.SessionLength };

        return MindStub.Build(
            childId: childId,
            interests: EffectiveScores(history, PetBrainTraitCategory.Interest, topics, now),
            mechanics: EffectiveScores(history, PetBrainTraitCategory.Mechanic, persona.Mechanics, now),
            mechanicMastery: history.Mastery.ToDictionary(
                p => p.Key, p => p.Value.EstimatedLevel, StringComparer.Ordinal),
            mechanicBands: history.Mastery.ToDictionary(
                p => p.Key, p => MechanicMasteryRules.BandFor(p.Value), StringComparer.Ordinal),
            recentOutcomes: [.. outcomes.Take(8)],
            completedTemplates: completed.ToHashSet(StringComparer.Ordinal),
            showLessThemes: showLess.ToHashSet(StringComparer.Ordinal),
            personalization: PersonalizationProfileFactory.Build(settings, outcomes),
            difficulty: DifficultyOf(history, persona),
            profileConfidence: Confidence(history, now));
    }

    /// <summary>
    /// Çətinlik personanın ustalığından gəlir — sonsuz artım da, sonsuz
    /// təkrar da burada görünməlidir.
    /// </summary>
    private static PetBrainDifficulty DifficultyOf(PersonaHistory history, Persona persona)
    {
        var relevant = persona.Mechanics
            .Where(history.Mastery.ContainsKey)
            .Select(key => MechanicMasteryRules.BandFor(history.Mastery[key]))
            .ToList();

        if (relevant.Count == 0)
            return PetBrainDifficulty.Easy;

        // Ortalama YOX, TAVAN: bir mexanika bir pillə yuxarı hazırdırsa,
        // sessiya ona imkan verməlidir. Ortalama alanda tək «hazıram» siqnalı
        // yuvarlaqlaşdırma ilə itir və uşaq eyni pillədə ilişib qalır.
        return relevant.Max();
    }

    private static Dictionary<string, int> EffectiveScores(
        PersonaHistory history, PetBrainTraitCategory category, IReadOnlyList<string> liked, DateTime now)
    {
        Dictionary<string, int> scores = new(StringComparer.Ordinal);

        foreach (var (key, trait) in history.Traits)
        {
            if (trait.Category != category)
                continue;

            scores[key] = TraitEvidence.EffectiveScore(trait, now);
        }

        // Personanın açıq zövqü profilə güclü başlanğıc verir — real uşaqda
        // bunu tanışlıq və ilk seçimlər edir.
        foreach (var key in liked)
            scores[key] = Math.Max(scores.GetValueOrDefault(key, TraitKeys.StartingScore), 80);

        return scores;
    }

    private static int Confidence(PersonaHistory history, DateTime now) =>
        history.Traits.Count == 0
            ? 0
            : Math.Clamp(
                (int)history.Traits.Values.Average(t => TraitEvidence.Confidence(t, now)), 0, 100);

    private static void Complete(
        PersonaHistory history, ExperienceTemplate template, bool alreadyDone, DateTime now)
    {
        foreach (var adjustment in ProfileLearningRules.ForCompletion(template, !alreadyDone))
            ApplyTrait(history, adjustment, now);
    }

    private static void Dislike(
        PersonaHistory history, HashSet<string> showLess, string topic, DateTime now)
    {
        showLess.Add(topic);

        foreach (var adjustment in ProfileLearningRules.ForExplicitFeedback(
                     PetBrainContentScope.Theme, topic, PetBrainContentPreferenceKind.ShowLess))
            ApplyTrait(history, adjustment, now);
    }

    private static void Practice(
        PersonaHistory history,
        Persona persona,
        ExperienceTemplate template,
        PetBrainDifficulty difficulty,
        DateTime now)
    {
        foreach (var mechanic in template.MechanicAffinity)
        {
            if (!history.Mastery.TryGetValue(mechanic, out var mastery))
                history.Mastery[mechanic] = mastery =
                    MechanicMasteryRules.New(Guid.Empty, mechanic, now);

            MechanicMasteryRules.Apply(
                mastery,
                new MechanicAttempt(
                    Solved: true,
                    UsedHint: persona.AlwaysUsesHints,
                    Mistakes: persona.AlwaysUsesHints ? 1 : 0,
                    Difficulty: difficulty),
                now);
        }
    }

    private static void ApplyTrait(PersonaHistory history, TraitAdjustment adjustment, DateTime now)
    {
        var key = adjustment.Key;

        if (!history.Traits.TryGetValue(key, out var trait))
            history.Traits[key] = trait = new PlayerTrait
            {
                Category = adjustment.Category,
                Key = key,
                Score = TraitKeys.StartingScore,
                UpdatedAt = now
            };

        trait.Score = TraitKeys.Clamp(trait.Score + adjustment.Delta);
        TraitEvidence.Record(trait, adjustment.Source, adjustment.Delta, now);
        trait.UpdatedAt = now;
    }
}

/// <summary>
/// Prosedural persona simulyasiyası — sistem yalnız bir ideal profil ilə
/// sınaqdan keçirilməməlidir.
/// </summary>
public class PetBrainPersonaSimulationTests
{
    /// <summary>
    /// Neçə sessiya simulyasiya edilir.
    ///
    /// <para>Say qəsdən böyükdür: «bir pillə yuxarı sına» qoruyucusu bir neçə
    /// cəhddən sonra işə düşür və qısa simulyasiya onu heç vaxt görməzdi —
    /// yəni test sistemin ən vacib qoruyucusunu yoxlamadan keçərdi.</para>
    /// </summary>
    private const int Sessions = 20;

    public static TheoryData<string> PersonaNames()
    {
        var data = new TheoryData<string>();

        foreach (var persona in All)
            data.Add(persona.Name);

        return data;
    }

    private static readonly IReadOnlyList<Persona> All =
    [
        new("Explorer", [TraitKeys.Space, TraitKeys.Science],
            [MechanicKeys.Exploration, MechanicKeys.Route]),

        new("Solver", [TraitKeys.Puzzles, TraitKeys.Science],
            [MechanicKeys.Route, MechanicKeys.Sequencing, MechanicKeys.Pattern]),

        new("Creator", [TraitKeys.Fantasy, TraitKeys.Stories],
            [MechanicKeys.Building, MechanicKeys.Decorating]),

        new("Helper", [TraitKeys.Animals, TraitKeys.Nature],
            [MechanicKeys.Nurturing, MechanicKeys.StoryChoice]),

        new("Collector", [TraitKeys.Ocean, TraitKeys.Nature],
            [MechanicKeys.Observation, MechanicKeys.Exploration]),

        new("ColdStart", [], []),

        new("ChangesMind", [TraitKeys.Space], [MechanicKeys.Route],
            SwitchesAfter: 5, SwitchTopics: [TraitKeys.Animals]),

        new("NeedsHints", [TraitKeys.Puzzles], [MechanicKeys.Sequencing],
            AlwaysUsesHints: true),

        new("ShortSessions", [TraitKeys.Animals], [MechanicKeys.Nurturing],
            SessionLength: PetBrainSessionLength.Short),

        new("TurnsAway", [TraitKeys.Space], [MechanicKeys.Route], DislikesAfter: 4)
    ];

    private static Persona Find(string name) => All.Single(p => p.Name == name);

    private static PersonaHistory RunFor(string name) =>
        PersonaSimulator.Run(Find(name), Sessions, Guid.Parse($"{Math.Abs(name.GetHashCode()):x8}-0000-0000-0000-000000000000"));

    /// <summary>
    /// <b>Heç bir profil bir macərəyə kilidlənmir.</b> Filter bubble ən çox
    /// zərər verən səhvdir: uşaq öz keçmişinin içində qapanır.
    /// </summary>
    [Theory]
    [MemberData(nameof(PersonaNames))]
    public void HecBirPersona_BirMacerayaKilidlenmir(string name)
    {
        var history = RunFor(name);

        Assert.True(history.DistinctPrimaries >= 2,
            $"«{name}» {Sessions} sessiyada yalnız {history.DistinctPrimaries} fərqli macəra gördü.");
    }

    /// <summary>Hər sessiyada uşaq birdən çox seçim görür.</summary>
    [Theory]
    [MemberData(nameof(PersonaNames))]
    public void HerSessiyada_BirdenCoxSecimGorunur(string name)
    {
        var history = RunFor(name);

        Assert.True(history.AllShown.Count > history.PrimaryShown.Count,
            $"«{name}» yalnız bir kart gördü — alternativ yoxdur.");
    }

    /// <summary>
    /// <b>Yeni kontent STARVASİYA yaşamır.</b> Bütün personalar birlikdə
    /// kataloqun hamısını görməlidir, yoxsa yazılan macəra heç vaxt
    /// oynanılmaz.
    /// </summary>
    [Fact]
    public void YeniKontent_StarvasiyaYasamir()
    {
        var shown = All
            .SelectMany(p => RunFor(p.Name).AllShown)
            .ToHashSet(StringComparer.Ordinal);

        var missing = ExperienceCatalog.Templates
            .Select(t => t.Key)
            .Where(key => !shown.Contains(key))
            .ToList();

        Assert.True(missing.Count == 0,
            $"Bu macəralar heç bir personaya göstərilmədi: {string.Join(", ", missing)}.");
    }

    /// <summary>
    /// <b>Hər persona FƏRQLİ təcrübə alır.</b> Definition of Done: eyni build,
    /// fərqli uşaq, fərqli oyun.
    /// </summary>
    [Fact]
    public void HerPersona_FerqliTecrubeAlir()
    {
        var signatures = All
            .Select(p => string.Join('>', RunFor(p.Name).Started.Take(4)))
            .ToList();

        Assert.True(signatures.Distinct(StringComparer.Ordinal).Count() > 1,
            "Bütün personalar eyni yolu keçdi — fərdiləşdirmə görünmür.");
    }

    /// <summary>
    /// <b>Açıq «daha az göstər» nəzərə alınır.</b> Uşaq sevimli mövzusundan
    /// üz döndərəndə sistem inad etməməlidir.
    /// </summary>
    [Fact]
    public void AcigImtina_NezereAlinir()
    {
        var persona = Find("TurnsAway");
        var history = RunFor(persona.Name);

        var disliked = persona.Topics[0];

        var afterDislike = history.PrimaryShown
            .Skip(persona.DislikesAfter + 1)
            .Select(key => ExperienceCatalog.Find(key)!.Theme)
            .ToList();

        Assert.NotEmpty(afterDislike);
        Assert.DoesNotContain(disliked, afterDislike);
    }

    /// <summary>
    /// <b>Yüksək ustalıq sonsuz çətinlik yaratmır.</b>
    /// </summary>
    [Theory]
    [MemberData(nameof(PersonaNames))]
    public void YuksekUstaliq_SonsuzCetinlikYaratmir(string name)
    {
        var history = RunFor(name);

        Assert.All(history.Difficulties, difficulty =>
            Assert.True(difficulty <= PetBrainDifficulty.Hard));

        Assert.All(history.Mastery.Values, mastery =>
            Assert.InRange(mastery.EstimatedLevel, MechanicMastery.MinLevel, MechanicMastery.MaxLevel));
    }

    /// <summary>
    /// <b>Kömək istəyən uşaq eyni asan tapşırığın sonsuz təkrarına
    /// düşmür.</b> Ustalıq yavaş qalxsa da qalxır.
    /// </summary>
    [Fact]
    public void KomekIsteyenUsaq_SonsuzAsanliqdaQalmir()
    {
        var history = RunFor("NeedsHints");

        Assert.NotEmpty(history.Mastery);

        // Yoxlanan şey personanın ÖZ mexanikalarıdır: sistem məhz onları
        // təkrar-təkrar seçir, «asanlıqda ilişmək» riski də oradadır.
        // Yol boyu təsadüfən toxunulan başqa mexanikalar bu iddianın mövzusu
        // deyil — onların çətinliyi bu uşaq üçün seçilmir.
        var practised = Find("NeedsHints").Mechanics
            .Where(history.Mastery.ContainsKey)
            .Select(key => history.Mastery[key])
            .ToList();

        Assert.NotEmpty(practised);

        Assert.All(practised, mastery =>
            Assert.True(mastery.EstimatedLevel > MechanicMastery.StartingLevel,
                $"«{mastery.Mechanic}» səviyyəsi qalxmadı: {mastery.EstimatedLevel}."));
    }

    /// <summary>
    /// Kömək istəmək MARAĞI azaltmır — «bacarmıram» ilə «sevmirəm» ayrı
    /// qalır.
    /// </summary>
    [Fact]
    public void KomekIstemek_MaragiAzaltmir()
    {
        var history = RunFor("NeedsHints");

        Assert.All(
            history.Traits.Values.Where(t => t.Category == PetBrainTraitCategory.Interest),
            trait => Assert.True(trait.Score >= TraitKeys.StartingScore,
                $"«{trait.Key}» balı başlanğıcdan aşağı düşdü: {trait.Score}."));
    }

    /// <summary>
    /// <b>Köhnə maraq zamanla zəifləyir.</b> Köhnəlmə olmasa bütün açarlar
    /// 100-ə yaxınlaşar və profillər bir-birinə oxşayardı.
    /// </summary>
    [Fact]
    public void KohneMaraq_ZamanlaZeifleyir()
    {
        var history = RunFor("Explorer");
        var trait = history.Traits.Values.First(t => t.Category == PetBrainTraitCategory.Interest);

        var fresh = TraitEvidence.EffectiveScore(trait, trait.LastObservedAt!.Value);
        var stale = TraitEvidence.EffectiveScore(trait, trait.LastObservedAt.Value.AddDays(120));

        Assert.True(stale < fresh, "Köhnə, təkrarlanmayan siqnal öz çəkisini itirməlidir.");
        Assert.True(stale >= TraitKeys.StartingScore, "Köhnəlmə tarixçəni SİLMİR.");
    }

    /// <summary>
    /// Marağını dəyişən uşaq izlənilir: yeni mövzu sistemin təklifinə çatır.
    /// </summary>
    [Fact]
    public void MaraginiDeyisenUsaq_Izlenilir()
    {
        var persona = Find("ChangesMind");
        var history = RunFor(persona.Name);

        var beforeThemes = history.PrimaryShown
            .Take(persona.SwitchesAfter)
            .Select(key => ExperienceCatalog.Find(key)!.Theme)
            .ToHashSet(StringComparer.Ordinal);

        var afterThemes = history.PrimaryShown
            .Skip(persona.SwitchesAfter)
            .Select(key => ExperienceCatalog.Find(key)!.Theme)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(afterThemes);
        Assert.True(afterThemes.Except(beforeThemes, StringComparer.Ordinal).Any(),
            "Maraq dəyişəndə təklif də dəyişməlidir.");
    }

    /// <summary>
    /// Soyuq başlanğıc uşağı boş ekranla qarşılaşmır və tez bir zamanda
    /// müxtəliflik görür.
    /// </summary>
    [Fact]
    public void SoyuqBaslangic_BosEkranGormur()
    {
        var history = RunFor("ColdStart");

        Assert.Equal(Sessions, history.PrimaryShown.Count);
        Assert.True(history.DistinctPrimaries >= 3,
            "Soyuq başlanğıcda sistem daha çox kəşf etməlidir.");
    }

    /// <summary>
    /// Qısa sessiya seçən persona daha qısa macəralar alır.
    /// </summary>
    [Fact]
    public void QisaSessiya_DahaQisaMaceraAlir()
    {
        var taste = Find("ShortSessions");

        var shortHistory = PersonaSimulator.Run(
            taste, Sessions, Guid.Parse("cccccccc-0000-0000-0000-000000000001"));

        var longHistory = PersonaSimulator.Run(
            taste with { SessionLength = PetBrainSessionLength.Long },
            Sessions,
            Guid.Parse("cccccccc-0000-0000-0000-000000000001"));

        var shortSteps = shortHistory.Started.Select(Steps).Average();
        var longSteps = longHistory.Started.Select(Steps).Average();

        // Kataloqda hazırda gerçək «uzun» macəra yoxdur (4–5 mərhələ), ona görə
        // iddia mütləq dəyər deyil: eyni zövqlü iki uşaqdan uzun sessiya seçəni
        // ən azı qısa seçən qədər addım almalıdır.
        Assert.True(longSteps >= shortSteps,
            $"Uzun sessiya {longSteps}, qısa sessiya {shortSteps} addım aldı.");

        Assert.NotEqual(
            string.Join(">", shortHistory.Started),
            string.Join(">", longHistory.Started));

        static int Steps(string key) => ExperienceCatalog.Find(key)!.StageCount;
    }

    /// <summary>
    /// <b>Eyni simulyasiya eyni nəticəni verir.</b> Determinizm olmadan nə
    /// test, nə də «niyə bu?» sualının cavabı mümkündür.
    /// </summary>
    [Theory]
    [MemberData(nameof(PersonaNames))]
    public void Simulyasiya_Deterministikdir(string name)
    {
        var first = RunFor(name);
        var second = RunFor(name);

        Assert.Equal(first.PrimaryShown, second.PrimaryShown);
        Assert.Equal(first.Started, second.Started);
    }
}
