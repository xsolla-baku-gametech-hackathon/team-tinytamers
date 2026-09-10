using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Mind;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Tövsiyə qərarı və uşağın ona cavabı.
///
/// <para>Əsas iddia: <b>kartın görünməsi üstünlük deyil</b>. Əvvəllər tövsiyəni
/// başlatmağın özü həmin mövzunun balını qaldırırdı və uşağın başqa seçimi yox
/// idi — nəticə özünü təsdiqləyən dövrə idi.</para>
/// </summary>
public class PetBrainRecommendationTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainRecommendationTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>Hər tövsiyə ilə birlikdə serverin verdiyi qərar id-si gəlir.</summary>
    [Fact]
    public async Task Tovsiye_QerarIdIleGelir()
    {
        var client = await NewChildAsync("rec-id@petpal.test");
        var state = await StateAsync(client);

        Assert.NotNull(state.Recommendation);
        Assert.NotEqual(Guid.Empty, state.Recommendation!.DecisionId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var record = await db.RecommendationDecisions
            .AsNoTracking()
            .FirstAsync(d => d.Id == state.Recommendation.DecisionId);

        Assert.Equal(client.ChildId, record.ChildProfileId);
        Assert.Equal(PetBrainRecommendationFeedback.Shown, record.Feedback);
        Assert.Equal(state.Recommendation.TemplateKey, record.SelectedTemplateKey);
    }

    /// <summary>
    /// Qərar sətri PII saxlamır: nə ad, nə söhbət, nə yaddaş cümləsi.
    /// </summary>
    [Fact]
    public async Task QerarSetri_SexsiMelumatSaxlamir()
    {
        var client = await NewChildAsync("rec-pii@petpal.test", childName: "Aylin");
        var state = await StateAsync(client);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var record = await db.RecommendationDecisions
            .AsNoTracking()
            .FirstAsync(d => d.Id == state.Recommendation!.DecisionId);

        var serialised = string.Join('|',
            record.ContextHash, record.SelectedTemplateKey, string.Join(',', record.CandidateKeys));

        Assert.DoesNotContain("Aylin", serialised, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Max", serialised, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(client.ChildId.ToString("N"), serialised, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>«Başqa fikir» EYNİ şablonu təkrar göstərmir.</summary>
    [Fact]
    public async Task BasqaFikir_EyniSablonuTekrarGostermir()
    {
        var client = await NewChildAsync("rec-another@petpal.test");

        var first = (await StateAsync(client)).Recommendation!;
        var second = await ShowAnotherAsync(client, first.DecisionId);

        Assert.NotNull(second);
        Assert.NotEqual(first.TemplateKey, second!.TemplateKey);
    }

    /// <summary>
    /// Sonsuz yeniləmə YOXDUR: limit dolanda düymə gizlənir və kart sabitləşir.
    /// </summary>
    [Fact]
    public async Task BasqaFikir_SonsuzTekrarlanmir()
    {
        var client = await NewChildAsync("rec-limit@petpal.test");
        var current = (await StateAsync(client)).Recommendation!;

        var granted = 0;

        while (current.CanShowAnother && granted < 10)
        {
            var next = await ShowAnotherAsync(client, current.DecisionId);

            if (next is null)
                break;

            current = next;
            granted++;
        }

        Assert.False(current.CanShowAnother);
        Assert.True(granted <= 4, $"Kart {granted} dəfə dəyişdirildi — limit işləmir.");
    }

    /// <summary>
    /// «Sonra» MARAĞI AZALTMIR — bir dəfə imtina bir mövzunu sevməmək deyil.
    /// </summary>
    [Fact]
    public async Task Sonra_MaragiAzaltmir()
    {
        var client = await NewChildAsync("rec-notnow@petpal.test");
        var recommendation = (await StateAsync(client)).Recommendation!;

        var before = await TraitScoresAsync(client.ChildId);

        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/recommendation/feedback",
            new PetBrainFeedbackRequest
            {
                DecisionId = recommendation.DecisionId,
                Feedback = PetBrainRecommendationFeedback.NotNow
            });

        response.EnsureSuccessStatusCode();

        var after = await TraitScoresAsync(client.ChildId);

        foreach (var (key, score) in before)
            Assert.True(after.GetValueOrDefault(key, score) >= score,
                $"«{key}» balı azaldı: {score} → {after.GetValueOrDefault(key, score)}.");
    }

    /// <summary>
    /// Kartın GÖRÜNMƏSİ üstünlük sayılmır: yalnız baxmaqla heç bir bal artmır.
    /// </summary>
    [Fact]
    public async Task Gorunme_UstunlukSayilmir()
    {
        var client = await NewChildAsync("rec-impression@petpal.test");

        await StateAsync(client);
        var afterFirst = await TraitScoresAsync(client.ChildId);

        for (var i = 0; i < 3; i++)
            await StateAsync(client);

        var afterMany = await TraitScoresAsync(client.ChildId);

        Assert.Equal(afterFirst.Count, afterMany.Count);

        foreach (var (key, score) in afterFirst)
            Assert.Equal(score, afterMany[key]);
    }

    /// <summary>
    /// «Sonra» dediyi kart eyni sessiyada geri qayıtmır, amma bal cədvəlinə
    /// heç nə yazılmır.
    /// </summary>
    [Fact]
    public async Task Sonra_KartiSessiyadaKenaraQoyur()
    {
        var client = await NewChildAsync("rec-session@petpal.test");
        var first = (await StateAsync(client)).Recommendation!;

        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/recommendation/feedback",
            new PetBrainFeedbackRequest
            {
                DecisionId = first.DecisionId,
                Feedback = PetBrainRecommendationFeedback.NotNow
            });

        response.EnsureSuccessStatusCode();

        var state = (await response.Content.ReadFromJsonAsync<PetBrainStateDto>())!;

        Assert.NotNull(state.Recommendation);
        Assert.NotEqual(first.TemplateKey, state.Recommendation!.TemplateKey);
    }

    /// <summary>Yalnız serverin verdiyi qərar başlana bilir.</summary>
    [Fact]
    public async Task YadQerar_Basladila_Bilmir()
    {
        var client = await NewChildAsync("rec-foreign@petpal.test");
        var stranger = await NewChildAsync("rec-foreign-other@petpal.test");

        var strangerDecision = (await StateAsync(stranger)).Recommendation!.DecisionId;

        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/runs",
            new StartPetBrainRunRequest { DecisionId = strangerDecision });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>Uydurma qərar id-si də rədd olunur.</summary>
    [Fact]
    public async Task UydurmaQerar_RedOlunur()
    {
        var client = await NewChildAsync("rec-fake@petpal.test");

        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/runs",
            new StartPetBrainRunRequest { DecisionId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>Artıq cavab verilmiş qərar İKİNCİ DƏFƏ istifadə edilə bilmir.</summary>
    [Fact]
    public async Task KohnelmisQerar_TekrarIsledilmir()
    {
        var client = await NewChildAsync("rec-stale@petpal.test");
        var recommendation = (await StateAsync(client)).Recommendation!;

        await ShowAnotherAsync(client, recommendation.DecisionId);

        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/runs",
            new StartPetBrainRunRequest { DecisionId = recommendation.DecisionId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>Klient <c>Selected</c> və ya <c>Shown</c> göndərə bilmir.</summary>
    [Theory]
    [InlineData(PetBrainRecommendationFeedback.Shown)]
    [InlineData(PetBrainRecommendationFeedback.Selected)]
    public async Task QadaganCavab_RedOlunur(PetBrainRecommendationFeedback feedback)
    {
        var client = await NewChildAsync($"rec-forbidden-{feedback}@petpal.test");
        var recommendation = (await StateAsync(client)).Recommendation!;

        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/recommendation/feedback",
            new PetBrainFeedbackRequest { DecisionId = recommendation.DecisionId, Feedback = feedback });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Macəra başlayanda qərar «seçildi» olaraq bağlanır.</summary>
    [Fact]
    public async Task Baslama_QerariSecildiKimiBaglayir()
    {
        var client = await NewChildAsync("rec-selected@petpal.test");
        var recommendation = (await StateAsync(client)).Recommendation!;

        var started = await client.Http.PostAsJsonAsync("/api/pet-brain/runs",
            new StartPetBrainRunRequest { DecisionId = recommendation.DecisionId });

        started.EnsureSuccessStatusCode();

        var run = (await started.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var record = await db.RecommendationDecisions
            .AsNoTracking()
            .FirstAsync(d => d.Id == recommendation.DecisionId);

        var row = await db.ExperienceRuns.AsNoTracking().FirstAsync(r => r.Id == run.RunId);

        Assert.Equal(PetBrainRecommendationFeedback.Selected, record.Feedback);
        Assert.Equal(recommendation.DecisionId, row.DecisionId);
    }

    /// <summary>Xarakter tövsiyə kartında GÖRÜNƏN cümləyə çevrilir.</summary>
    [Fact]
    public async Task Xarakter_TovsiyeCumlesindeGorunur()
    {
        var client = await NewChildAsync("rec-voice@petpal.test");
        var recommendation = (await StateAsync(client)).Recommendation!;

        Assert.False(string.IsNullOrWhiteSpace(recommendation.PetLine));
    }

    // ==================== Köməkçilər ====================

    private async Task<ApiTestClient> NewChildAsync(string email, string childName = "Ava")
    {
        var client = await ApiTestClient.CreateAsync(_factory, email, childName);
        await client.HatchAsync(_factory);

        return client;
    }

    private static async Task<PetBrainStateDto> StateAsync(ApiTestClient client) =>
        (await client.Http.GetFromJsonAsync<PetBrainStateDto>("/api/pet-brain"))!;

    private static async Task<PetBrainRecommendationDto?> ShowAnotherAsync(
        ApiTestClient client, Guid decisionId)
    {
        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/recommendation/feedback",
            new PetBrainFeedbackRequest
            {
                DecisionId = decisionId,
                Feedback = PetBrainRecommendationFeedback.ShowAnother
            });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PetBrainStateDto>())!.Recommendation;
    }

    private async Task<Dictionary<string, int>> TraitScoresAsync(Guid childId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.PlayerTraits
            .AsNoTracking()
            .Where(t => t.ChildProfileId == childId)
            .ToDictionaryAsync(t => $"{t.Category}:{t.Key}", t => t.Score);
    }
}

/// <summary>
/// Bağ pillələri və xarakterin GÖRÜNƏN səsi.
/// </summary>
public class PetBrainBondAndVoiceTests
{
    [Theory]
    [InlineData(0, PetBrainBondTier.NewFriend)]
    [InlineData(24, PetBrainBondTier.NewFriend)]
    [InlineData(25, PetBrainBondTier.TrustedFriend)]
    [InlineData(49, PetBrainBondTier.TrustedFriend)]
    [InlineData(50, PetBrainBondTier.AdventurePartner)]
    [InlineData(74, PetBrainBondTier.AdventurePartner)]
    [InlineData(75, PetBrainBondTier.BestCompanion)]
    [InlineData(99, PetBrainBondTier.BestCompanion)]
    [InlineData(100, PetBrainBondTier.LifelongTeam)]
    public void PilleSerhedleri_SenededekiKimidir(int bond, PetBrainBondTier expected) =>
        Assert.Equal(expected, BondTiers.Of(bond));

    /// <summary>Hər pillə GÖRÜNƏN bir şey açır — pillə boş vəd deyil.</summary>
    [Fact]
    public void HerPille_GorunenBirSeyAcir()
    {
        foreach (var tier in Enum.GetValues<PetBrainBondTier>())
        {
            var unlock = BondTiers.UnlockFor(tier);

            Assert.False(string.IsNullOrWhiteSpace(unlock.Emote));
            Assert.False(string.IsNullOrWhiteSpace(unlock.GreetingVariant));
            Assert.False(string.IsNullOrWhiteSpace(unlock.AdventureReaction));
            Assert.False(string.IsNullOrWhiteSpace(unlock.Pose));
        }
    }

    /// <summary>Pillə hər iki dildə adlanır.</summary>
    [Fact]
    public void Pilleler_HerIkiDildeAdlanir()
    {
        foreach (var tier in Enum.GetValues<PetBrainBondTier>())
        {
            var az = BondTiers.Label(tier, "az");
            var en = BondTiers.Label(tier, "en");

            Assert.False(string.IsNullOrWhiteSpace(az));
            Assert.False(string.IsNullOrWhiteSpace(en));
            Assert.NotEqual(az, en);
        }
    }

    /// <summary>Bağ heç vaxt azalmır, deməli pillə də geri düşmür.</summary>
    [Fact]
    public void Pille_GeriDusmur()
    {
        var pet = new Pet { Bond = 40 };

        BondRules.Grant(pet, 20);
        Assert.Equal(PetBrainBondTier.AdventurePartner, BondTiers.Of(pet.Bond));

        // Mənfi artım QƏBUL EDİLMİR.
        BondRules.Grant(pet, -30);
        Assert.Equal(PetBrainBondTier.AdventurePartner, BondTiers.Of(pet.Bond));
    }

    /// <summary>Hər xarakter FƏRQLİ səs verir — nişan yox, ton.</summary>
    [Fact]
    public void HerXarakter_FerqliSesVerir()
    {
        var lines = Enum.GetValues<PetBrainPersonality>()
            .Select(p => PersonalityVoice.RecommendationLine(p, "az", "Ay Kristalı"))
            .ToList();

        Assert.Equal(lines.Count, lines.Distinct(StringComparer.Ordinal).Count());

        var reactions = Enum.GetValues<PetBrainPersonality>()
            .Select(p => PersonalityVoice.ChoiceReaction(p, "az"))
            .ToList();

        Assert.Equal(reactions.Count, reactions.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Xarakterin BÜTÜN mətnləri hər iki dildə mövcuddur.
    /// </summary>
    [Fact]
    public void XarakterMetnleri_HerIkiDildedir()
    {
        foreach (var personality in Enum.GetValues<PetBrainPersonality>())
        foreach (var pair in new[]
                 {
                     (PersonalityVoice.RecommendationLine(personality, "az", "T"),
                         PersonalityVoice.RecommendationLine(personality, "en", "T")),
                     (PersonalityVoice.ChoiceReaction(personality, "az"),
                         PersonalityVoice.ChoiceReaction(personality, "en")),
                     (PersonalityVoice.HintOffer(personality, "az"),
                         PersonalityVoice.HintOffer(personality, "en")),
                     (PersonalityVoice.Encouragement(personality, "az"),
                         PersonalityVoice.Encouragement(personality, "en")),
                     (PersonalityVoice.GreetingFlavour(personality, "az"),
                         PersonalityVoice.GreetingFlavour(personality, "en"))
                 })
        {
            Assert.False(string.IsNullOrWhiteSpace(pair.Item1));
            Assert.False(string.IsNullOrWhiteSpace(pair.Item2));
            Assert.NotEqual(pair.Item1, pair.Item2);
        }
    }

    /// <summary>
    /// <b>Günahlandıran və emosional təzyiq quran dil QADAĞANDIR.</b>
    ///
    /// <para>Pet uşağı geri qaytarmaq üçün "məni tək qoydun" deməməlidir —
    /// bu, uşaq məhsulunda ən asan və ən zərərli qısayoldur.</para>
    /// </summary>
    [Fact]
    public void HecBirCumle_GunahlandirmirVeTezyiqQurmur()
    {
        string[] banned =
        [
            "tək qoydun", "darıxdım", "küsdüm", "məni unutdun", "niyə gəlmədin",
            "günah", "peşman", "məcbur", "tələs",
            "you left me", "you forgot me", "i was sad", "why did you not",
            "you should have", "hurry", "you failed", "disappointed"
        ];

        List<string> all = [];

        foreach (var personality in Enum.GetValues<PetBrainPersonality>())
        foreach (var language in new[] { "az", "en" })
        {
            all.Add(PersonalityVoice.RecommendationLine(personality, language, "T"));
            all.Add(PersonalityVoice.ChoiceReaction(personality, language));
            all.Add(PersonalityVoice.HintOffer(personality, language));
            all.Add(PersonalityVoice.Encouragement(personality, language));
            all.Add(PersonalityVoice.GreetingFlavour(personality, language));
        }

        foreach (var tier in Enum.GetValues<PetBrainBondTier>())
        foreach (var language in new[] { "az", "en" })
            all.Add(BondTiers.UnlockLine(tier, language, "Max"));

        foreach (var text in all)
        foreach (var phrase in banned)
            Assert.DoesNotContain(phrase, text, StringComparison.OrdinalIgnoreCase);
    }
}
