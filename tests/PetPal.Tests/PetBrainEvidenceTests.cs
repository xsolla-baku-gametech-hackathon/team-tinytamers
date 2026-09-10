using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Sübut modeli — <b>saf qaydalar</b>.
///
/// <para>Əsas iddia: tək bal «uşaq bunu sevir» ilə «uşağa bu göstərildi və
/// başqa yolu yox idi» arasındakı fərqi saxlaya bilmir. Sübut sahələri məhz
/// bunu ayırır.</para>
/// </summary>
public class TraitEvidenceTests
{
    private static readonly DateTime Now = new(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);

    private static PlayerTrait Trait(int score = 70, DateTime? seen = null) => new()
    {
        Category = PetBrainTraitCategory.Interest,
        Key = TraitKeys.Space,
        Score = score,
        LastObservedAt = seen ?? Now,
        UpdatedAt = seen ?? Now
    };

    // ==================== Köhnəlmə ====================

    /// <summary>Təzə müşahidə KÖHNƏLMİR — güzəşt müddəti var.</summary>
    [Fact]
    public void TezeMusahide_Kohnelmir()
    {
        var trait = Trait(seen: Now.AddDays(-3));

        Assert.Equal(0, TraitEvidence.DecayFor(trait, Now));
        Assert.Equal(70, TraitEvidence.EffectiveScore(trait, Now));
    }

    /// <summary>Uzun fasilədən sonra bal TƏDRİCƏN geri gedir.</summary>
    [Fact]
    public void UzunFasile_BaliTedricenAzaldir()
    {
        var trait = Trait(seen: Now.AddDays(-60));

        var decay = TraitEvidence.DecayFor(trait, Now);

        Assert.True(decay > 0, "Altmış gündən sonra köhnəlmə gözlənilir.");
        Assert.True(TraitEvidence.EffectiveScore(trait, Now) < 70);
    }

    /// <summary>
    /// Köhnəlmə heç vaxt BAŞLANĞIC balından aşağı endirmir — uşağın tarixçəsi
    /// silinmir, sadəcə əminlik azalır.
    /// </summary>
    [Fact]
    public void Kohnelme_BaslangicBalindanAsagiDusmur()
    {
        var trait = Trait(score: 35, seen: Now.AddYears(-2));

        Assert.Equal(TraitKeys.StartingScore, TraitEvidence.EffectiveScore(trait, Now));
    }

    /// <summary>Köhnəlmənin ÜST HƏDDİ var — bir ay gəlməmək profili silmir.</summary>
    [Fact]
    public void Kohnelme_UstHeddiKecmir()
    {
        var trait = Trait(score: 100, seen: Now.AddYears(-5));

        Assert.True(TraitEvidence.DecayFor(trait, Now) <= TraitEvidence.MaxDecay);
    }

    /// <summary>
    /// MÜXTƏLİF mənbəli sübut daha yavaş köhnəlir: bir mövzunu üç ayrı yerdə
    /// seçmək təsadüf deyil.
    /// </summary>
    [Fact]
    public void MuxtelifMenbeliSubut_DahaYavasKohnelir()
    {
        var narrow = Trait(seen: Now.AddDays(-90));
        var broad = Trait(seen: Now.AddDays(-90));

        broad.SourceMask = TraitEvidence.WithSource(
            TraitEvidence.WithSource(
                TraitEvidence.WithSource(0, PetBrainEvidenceSource.Adventure),
                PetBrainEvidenceSource.MiniGame),
            PetBrainEvidenceSource.Learning);

        Assert.True(TraitEvidence.DecayFor(broad, Now) < TraitEvidence.DecayFor(narrow, Now));
    }

    // ==================== İnam ====================

    /// <summary>Müşahidəsiz açar üçün inam SIFIRDIR — bal olsa da.</summary>
    [Fact]
    public void MusahidesizAcar_InamsizQalir() =>
        Assert.Equal(0, TraitEvidence.Confidence(Trait(score: 90), Now));

    /// <summary>Çox müşahidə inamı ARTIRIR.</summary>
    [Fact]
    public void CoxMusahide_InamiArtirir()
    {
        var few = Trait();
        var many = Trait();

        for (var i = 0; i < 2; i++)
            TraitEvidence.Record(few, PetBrainEvidenceSource.Adventure, 2, Now);

        for (var i = 0; i < 12; i++)
            TraitEvidence.Record(many, PetBrainEvidenceSource.Adventure, 2, Now);

        Assert.True(TraitEvidence.Confidence(many, Now) > TraitEvidence.Confidence(few, Now));
    }

    /// <summary>Müxtəlif mənbə inamı ARTIRIR — eyni say müşahidə ilə belə.</summary>
    [Fact]
    public void MuxtelifMenbe_InamiArtirir()
    {
        var narrow = Trait();
        var broad = Trait();

        for (var i = 0; i < 6; i++)
            TraitEvidence.Record(narrow, PetBrainEvidenceSource.Adventure, 2, Now);

        foreach (var source in new[]
                 {
                     PetBrainEvidenceSource.Adventure, PetBrainEvidenceSource.Choice,
                     PetBrainEvidenceSource.Puzzle, PetBrainEvidenceSource.MiniGame,
                     PetBrainEvidenceSource.Learning, PetBrainEvidenceSource.Care
                 })
            TraitEvidence.Record(broad, source, 2, Now);

        Assert.True(TraitEvidence.Confidence(broad, Now) > TraitEvidence.Confidence(narrow, Now));
    }

    // ==================== Exposure ≠ preference ====================

    /// <summary>
    /// <b>Skip BALI AZALTMIR.</b> Bir dəfə kənara qoymaq bir mövzunu sevməmək
    /// deyil — yalnız inam azalır.
    /// </summary>
    [Fact]
    public void Skip_BaliAzaltmirYalnizInamiAzaldir()
    {
        var trait = Trait();

        for (var i = 0; i < 5; i++)
            TraitEvidence.Record(trait, PetBrainEvidenceSource.Adventure, 2, Now);

        var scoreBefore = trait.Score;
        var confidenceBefore = TraitEvidence.Confidence(trait, Now);

        TraitEvidence.RecordSkip(trait, Now);
        TraitEvidence.RecordSkip(trait, Now);

        Assert.Equal(scoreBefore, trait.Score);
        Assert.True(TraitEvidence.Confidence(trait, Now) < confidenceBefore);
    }

    /// <summary>Skip MÜSBƏT sübut yazmır — göstərilmə üstünlük deyil.</summary>
    [Fact]
    public void Skip_MusbetSubutYazmir()
    {
        var trait = Trait();

        TraitEvidence.RecordSkip(trait, Now);

        Assert.Equal(0, trait.PositiveEvidence);
        Assert.Equal(1, trait.SkipEvidence);
        Assert.Equal(1, trait.ObservationCount);
    }

    /// <summary>Güclü siqnal ayrıca qeyd olunur — zəif toxunuşdan fərqlənir.</summary>
    [Fact]
    public void GucluSiqnal_AyricaQeydOlunur()
    {
        var weak = Trait();
        var strong = Trait();

        TraitEvidence.Record(weak, PetBrainEvidenceSource.Care, 1, Now);
        TraitEvidence.Record(strong, PetBrainEvidenceSource.Adventure, 3, Now);

        Assert.Null(weak.LastStrongEvidenceAt);
        Assert.NotNull(strong.LastStrongEvidenceAt);
    }
}

/// <summary>
/// Mini oyunun AİLƏSİ sayılır — hamısı eyni «playful +1» deyil.
/// </summary>
public class MiniGameFamilyTests
{
    [Theory]
    [InlineData("memory-match", PetBrainTraitCategory.Interest, TraitKeys.Puzzles)]
    [InlineData("color-echo", PetBrainTraitCategory.Interest, TraitKeys.Puzzles)]
    [InlineData("star-run", PetBrainTraitCategory.PlayStyle, TraitKeys.Explorer)]
    [InlineData("quick-tap", PetBrainTraitCategory.PlayStyle, TraitKeys.Explorer)]
    [InlineData("bubble-pop", PetBrainTraitCategory.PlayStyle, TraitKeys.Creative)]
    public void OyunAilesi_IkinciSiqnalVerir(
        string gameKey, PetBrainTraitCategory category, string key)
    {
        var adjustments = ProfileLearningRules.ForMiniGame(gameKey);

        // Şənlik HƏMİŞƏ var.
        Assert.Contains(adjustments, a =>
            a.Category == PetBrainTraitCategory.PlayStyle && a.Key == TraitKeys.Playful);

        Assert.Contains(adjustments, a => a.Category == category && a.Key == key);
    }

    /// <summary>Naməlum oyun yalnız ümumi siqnal verir — uydurma xassə yaranmır.</summary>
    [Fact]
    public void NamelumOyun_YalnizUmumiSiqnalVerir()
    {
        var adjustments = ProfileLearningRules.ForMiniGame("uydurma-oyun");

        Assert.Single(adjustments);
        Assert.Equal(TraitKeys.Playful, adjustments[0].Key);
    }

    /// <summary>Ailə siqnalı MƏNBƏ daşıyır — sübut modelinə düşür.</summary>
    [Fact]
    public void OyunSiqnali_MenbeDasiyir() =>
        Assert.All(
            ProfileLearningRules.ForMiniGame("memory-match"),
            a => Assert.Equal(PetBrainEvidenceSource.MiniGame, a.Source));
}

/// <summary>
/// HƏR qayda mənbə daşıyır — <c>Unknown</c> qalan sətir səssiz nasazlıqdır.
///
/// <para>Bu testin öz səbəbi var: mənbə istəyə bağlı parametrdir, ona görə
/// yeni qayda yazan adam onu asanlıqla unuda bilir. Nəticə kompilyasiya
/// olunur, işləyir və yalnız sübut modelində — «bu açar haradan gəldi?»
/// sualında — üzə çıxır.</para>
/// </summary>
public class ProfileLearningSourceTests
{
    public static TheoryData<string, IReadOnlyList<TraitAdjustment>> AllRules()
    {
        var template = ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!;
        var option = template.Stages.SelectMany(s => s.Options).First();

        return new TheoryData<string, IReadOnlyList<TraitAdjustment>>
        {
            { nameof(ProfileLearningRules.ForSelection), ProfileLearningRules.ForSelection(template) },
            { nameof(ProfileLearningRules.ForCompletion), ProfileLearningRules.ForCompletion(template, true) },
            { "ForCompletionRepeat", ProfileLearningRules.ForCompletion(template, false) },
            { nameof(ProfileLearningRules.ForAbandon), ProfileLearningRules.ForAbandon(template, 2) },
            { nameof(ProfileLearningRules.ForChoice), ProfileLearningRules.ForChoice(option) },
            { nameof(ProfileLearningRules.ForPuzzleSolved), ProfileLearningRules.ForPuzzleSolved() },
            { nameof(ProfileLearningRules.ForCare), ProfileLearningRules.ForCare() },
            { nameof(ProfileLearningRules.ForAccessoryEquipped), ProfileLearningRules.ForAccessoryEquipped() },
            { nameof(ProfileLearningRules.ForMiniGame), ProfileLearningRules.ForMiniGame("memory-match") },
            { "ForSkillAnswerScience", ProfileLearningRules.ForSkillAnswer(SkillArea.Science, true) },
            { "ForSkillAnswerLogic", ProfileLearningRules.ForSkillAnswer(SkillArea.Logic, true) },
            { "ForSkillAnswerMath", ProfileLearningRules.ForSkillAnswer(SkillArea.Math, true) },
            { "ForSkillAnswerReading", ProfileLearningRules.ForSkillAnswer(SkillArea.Reading, true) }
        };
    }

    [Theory]
    [MemberData(nameof(AllRules))]
    public void HerQayda_MenbeDasiyir(string rule, IReadOnlyList<TraitAdjustment> adjustments) =>
        Assert.All(adjustments, a =>
            Assert.True(
                a.Source != PetBrainEvidenceSource.Unknown,
                $"{rule} → «{a.Key}» mənbəsizdir."));
}

/// <summary>
/// Sübut BAZAYA yazılır və «göstərildi, seçilmədi» qeydi işləyir.
/// </summary>
public class PetBrainEvidenceIntegrationTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainEvidenceIntegrationTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>Macəra tamamlananda sübut sahələri DOLUR.</summary>
    [Fact]
    public async Task Tamamlama_SubutSahelerinDoldurur()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "evidence-complete@petpal.test", "Aylin");
        await client.HatchAsync(_factory);

        var run = await PetBrainPlaythrough.PlayToEndAsync(client);
        await PetBrainPlaythrough.CompleteAsync(client, run.RunId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var traits = await db.PlayerTraits
            .AsNoTracking()
            .Where(t => t.ChildProfileId == client.ChildId && t.ObservationCount > 0)
            .ToListAsync();

        Assert.NotEmpty(traits);

        Assert.All(traits, t =>
        {
            Assert.True(t.ObservationCount > 0);
            Assert.NotNull(t.LastObservedAt);
            Assert.NotEqual(0, t.SourceMask);
        });
    }

    /// <summary>
    /// «Başqa fikir» EXPOSURE qeydi yazır: bal toxunulmaz qalır, skip sayı artır.
    /// </summary>
    [Fact]
    public async Task BasqaFikir_ExposureQeydiYazir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "evidence-skip@petpal.test", "Mia");
        await client.HatchAsync(_factory);

        // Əvvəlcə bir macəra bitirilir ki, xassə sətri ümumiyyətlə yaransın.
        var run = await PetBrainPlaythrough.PlayToEndAsync(client);
        await PetBrainPlaythrough.CompleteAsync(client, run.RunId);

        var state = await StateAsync(client);
        var recommendation = state.Recommendation!;

        var before = await TraitAsync(client.ChildId, recommendation.TemplateKey);

        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/recommendation/feedback",
            new PetPal.Shared.Dtos.PetBrain.PetBrainFeedbackRequest
            {
                DecisionId = recommendation.DecisionId,
                Feedback = PetBrainRecommendationFeedback.ShowAnother
            });

        response.EnsureSuccessStatusCode();

        var after = await TraitAsync(client.ChildId, recommendation.TemplateKey);

        if (before is null || after is null)
            return;

        Assert.Equal(before.Score, after.Score);
        Assert.True(after.SkipEvidence > before.SkipEvidence);
    }

    private static async Task<PetPal.Shared.Dtos.PetBrain.PetBrainStateDto> StateAsync(ApiTestClient client) =>
        (await client.Http.GetFromJsonAsync<PetPal.Shared.Dtos.PetBrain.PetBrainStateDto>("/api/pet-brain"))!;

    private async Task<PlayerTrait?> TraitAsync(Guid childId, string templateKey)
    {
        var template = ExperienceCatalog.Find(templateKey);

        if (template is null)
            return null;

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.PlayerTraits
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ChildProfileId == childId
                                      && t.Category == PetBrainTraitCategory.Interest
                                      && t.Key == template.PrimaryInterest);
    }
}
