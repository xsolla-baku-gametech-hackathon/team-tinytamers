using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Fərdiləşdirmənin UÇDAN-UCA davranışı: tanışlıq, alternativlər, açıq rəy və
/// valideyn nəzarəti.
///
/// <para>Bu testlərin əsas iddiası Definition of Done-dan gəlir: <b>saxlanılan
/// hər ölçünün görünən və test edilə bilən istehlakçısı var</b>.</para>
/// </summary>
public class PetBrainPersonalizationApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public PetBrainPersonalizationApiTests(TestWebAppFactory factory) => _factory = factory;

    // ==================== Alternativlər ====================

    /// <summary>
    /// Uşaq bir yox, BİR NEÇƏ təklif görür — və hər birinin öz qərar id-si var.
    /// </summary>
    [Fact]
    public async Task Tovsiye_AlternativlerleGelir()
    {
        var client = await NewChildAsync("alt-list@petpal.test");
        var state = await StateAsync(client);

        Assert.NotNull(state.Recommendation);
        Assert.NotEmpty(state.Alternatives);

        var ids = state.Alternatives
            .Select(a => a.DecisionId)
            .Append(state.Recommendation!.DecisionId)
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.NotEqual(Guid.Empty, id));
    }

    /// <summary>Hər kart öz ROLUNU və uşağın dilində adını daşıyır.</summary>
    [Fact]
    public async Task HerKart_OzRolunuDasiyir()
    {
        var client = await NewChildAsync("alt-slot@petpal.test");
        var state = await StateAsync(client);

        Assert.Equal(PetBrainRecommendationSlot.Primary, state.Recommendation!.Slot);
        Assert.False(string.IsNullOrWhiteSpace(state.Recommendation.SlotLabel));

        Assert.All(state.Alternatives, alt =>
        {
            Assert.NotEqual(PetBrainRecommendationSlot.Primary, alt.Slot);
            Assert.False(string.IsNullOrWhiteSpace(alt.SlotLabel));
        });
    }

    /// <summary>
    /// <b>Uşağın alternativ seçimi QƏBUL EDİLİR.</b> Yalnız «əsas» kartı qəbul
    /// etmək uşağı bizim siyasətimizə məcbur etmək olardı.
    /// </summary>
    [Fact]
    public async Task Alternativ_Basladila_Bilir()
    {
        var client = await NewChildAsync("alt-start@petpal.test");
        var state = await StateAsync(client);

        var alternative = state.Alternatives[0];

        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/runs",
            new StartPetBrainRunRequest { DecisionId = alternative.DecisionId });

        response.EnsureSuccessStatusCode();

        var run = (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        Assert.Equal(alternative.TemplateKey, run.TemplateKey);
    }

    /// <summary>
    /// Alternativ seçiləndə ƏSAS kart rədd edilmiş sayılır — siyasətin səhvi
    /// öz uğuru kimi oxunmamalıdır.
    /// </summary>
    [Fact]
    public async Task Alternativ_SecilendeEsasKartRedSayilir()
    {
        var client = await NewChildAsync("alt-decline@petpal.test");
        var state = await StateAsync(client);

        var primaryId = state.Recommendation!.DecisionId;
        var alternative = state.Alternatives[0];

        (await client.Http.PostAsJsonAsync("/api/pet-brain/runs",
            new StartPetBrainRunRequest { DecisionId = alternative.DecisionId })).EnsureSuccessStatusCode();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var primary = await db.RecommendationDecisions.AsNoTracking().FirstAsync(d => d.Id == primaryId);
        var chosen = await db.RecommendationDecisions.AsNoTracking()
            .FirstAsync(d => d.Id == alternative.DecisionId);

        Assert.Equal(PetBrainRecommendationFeedback.NotNow, primary.Feedback);
        Assert.Equal(PetBrainRecommendationFeedback.Selected, chosen.Feedback);
        Assert.Equal(primary.GroupId, chosen.GroupId);
    }

    /// <summary>İzah UŞAĞIN DİLİNDƏDİR — daxili bal və texniki termin yoxdur.</summary>
    [Fact]
    public async Task Izah_UsaginDilindedir()
    {
        var client = await NewChildAsync("why-text@petpal.test");
        var recommendation = (await StateAsync(client)).Recommendation!;

        Assert.NotEmpty(recommendation.Reasons);
        Assert.NotEmpty(recommendation.ReasonCodes);

        foreach (var forbidden in new[] { "score", "bal:", "policy", "weight", "algorithm", "fit=" })
            Assert.All(recommendation.Reasons, reason =>
                Assert.DoesNotContain(forbidden, reason, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Kart çətinlik, dəstək və mükafat etiketlərini daşıyır.</summary>
    [Fact]
    public async Task Kart_Cetinlik_Destek_VeMukafatEtiketiDasiyir()
    {
        var client = await NewChildAsync("labels@petpal.test");
        var recommendation = (await StateAsync(client)).Recommendation!;

        Assert.False(string.IsNullOrWhiteSpace(recommendation.ChallengeLabel));
        Assert.False(string.IsNullOrWhiteSpace(recommendation.SupportLabel));
        Assert.False(string.IsNullOrWhiteSpace(recommendation.RewardLabel));
        Assert.True(recommendation.PolicyVersion > 0);
        Assert.True(recommendation.TargetMinutes > 0);
    }

    // ==================== İlk tanışlıq ====================

    /// <summary>Yeni uşaqda tanışlıq GÖZLƏYİR və variantlar serverdən gəlir.</summary>
    [Fact]
    public async Task YeniUsaq_TanisliqGozleyir()
    {
        var client = await NewChildAsync("onb-pending@petpal.test");

        Assert.True((await StateAsync(client)).OnboardingPending);

        var onboarding = (await client.Http
            .GetFromJsonAsync<PetBrainOnboardingDto>("/api/pet-brain/onboarding"))!;

        Assert.False(onboarding.Completed);
        Assert.NotEmpty(onboarding.Topics);
        Assert.NotEmpty(onboarding.Activities);
        Assert.NotEmpty(onboarding.Tones);
        Assert.Equal(OnboardingRules.MaxTopics, onboarding.MaxTopics);

        Assert.All(onboarding.Tones, tone =>
            Assert.False(string.IsNullOrWhiteSpace(tone.SampleLine)));
    }

    /// <summary>
    /// Tanışlığın seçimləri PRIOR kimi yazılır — kiçik, amma görünən artım.
    /// </summary>
    [Fact]
    public async Task Tanisliq_PriorYazir()
    {
        var client = await NewChildAsync("onb-prior@petpal.test");

        await SubmitOnboardingAsync(client, new PetBrainOnboardingRequest
        {
            Topics = [TraitKeys.Ocean],
            Activities = [MechanicKeys.Building],
            SessionLength = PetBrainSessionLength.Short,
            Tone = PetBrainPersonality.CaringCompanion
        });

        var traits = await TraitsAsync(client.ChildId);

        Assert.True(traits[$"{PetBrainTraitCategory.Interest}:{TraitKeys.Ocean}"] > TraitKeys.StartingScore);
        Assert.True(traits[$"{PetBrainTraitCategory.Mechanic}:{MechanicKeys.Building}"] > TraitKeys.StartingScore);

        var state = await StateAsync(client);

        Assert.False(state.OnboardingPending);
        Assert.Equal(PetBrainSessionLength.Short, state.Settings.SessionLength);
        Assert.Equal(PetBrainSettingSource.Onboarding, state.Settings.SessionLengthSource);
    }

    /// <summary>
    /// <b>Prior daimi həqiqət deyil.</b> Artım kiçikdir — bir seçim uşağı
    /// aylarla bir etiketə bağlamamalıdır.
    /// </summary>
    [Fact]
    public async Task TanisliqPrioru_KicikQalir()
    {
        var client = await NewChildAsync("onb-small@petpal.test");

        await SubmitOnboardingAsync(client, new PetBrainOnboardingRequest { Topics = [TraitKeys.Ocean] });

        var traits = await TraitsAsync(client.ChildId);
        var score = traits[$"{PetBrainTraitCategory.Interest}:{TraitKeys.Ocean}"];

        Assert.True(score <= TraitKeys.StartingScore + ProfileLearningRules.OnboardingPrior,
            $"Tanışlıq balı çox böyükdür: {score}.");
    }

    /// <summary>Tanışlıq tamamilə KEÇİLƏ bilər və bir daha soruşulmur.</summary>
    [Fact]
    public async Task Tanisliq_Kecile_Bilir()
    {
        var client = await NewChildAsync("onb-skip@petpal.test");

        await SubmitOnboardingAsync(client, new PetBrainOnboardingRequest { Skipped = true });

        var state = await StateAsync(client);

        Assert.False(state.OnboardingPending);
        Assert.NotNull(state.Recommendation);
        Assert.Empty(await TraitsAsync(client.ChildId));
    }

    /// <summary>Naməlum açar səssizcə buraxılır — uydurma xassə yaranmır.</summary>
    [Fact]
    public async Task Tanisliq_NamelumAcariBuraxir()
    {
        var client = await NewChildAsync("onb-unknown@petpal.test");

        await SubmitOnboardingAsync(client, new PetBrainOnboardingRequest
        {
            Topics = ["not-a-real-topic"],
            Activities = ["not-a-real-activity"]
        });

        Assert.Empty(await TraitsAsync(client.ChildId));
    }

    // ==================== Uşağın ayarları ====================

    /// <summary>Uşaq öz ayarlarını dəyişə bilir və nəticə dərhal görünür.</summary>
    [Fact]
    public async Task Usaq_OzAyarlariniDeyise_Bilir()
    {
        var client = await NewChildAsync("settings-child@petpal.test");

        var updated = await UpdateSettingsAsync(client, new UpdatePetBrainSettingsRequest
        {
            HintStyle = PetBrainHintStyle.Rule,
            ReducedMotion = true,
            LargeText = true
        });

        Assert.Equal(PetBrainHintStyle.Rule, updated.HintStyle);
        Assert.Equal(PetBrainSettingSource.Child, updated.HintStyleSource);
        Assert.True(updated.ReducedMotion);

        var state = await StateAsync(client);

        Assert.True(state.Settings.LargeText);
        Assert.Equal(PetBrainHintStyle.Rule, state.Settings.HintStyle);
    }

    /// <summary>
    /// <b>Valideynin yazdığını uşaq geri dəyişə bilmir.</b> Üstünlük sırası
    /// endpoint səviyyəsində qorunur.
    /// </summary>
    [Fact]
    public async Task Usaq_ValideynAyariniDeyise_Bilmir()
    {
        var client = await NewChildAsync("settings-lock@petpal.test");

        client.SwitchToParent();
        await UpdateParentAsync(client, new UpdateParentPersonalizationRequest
        {
            HintStyle = PetBrainHintStyle.StepByStep
        });

        client.SwitchToChild();
        var updated = await UpdateSettingsAsync(client, new UpdatePetBrainSettingsRequest
        {
            HintStyle = PetBrainHintStyle.Rule
        });

        Assert.Equal(PetBrainHintStyle.StepByStep, updated.HintStyle);
        Assert.Equal(PetBrainSettingSource.Parent, updated.HintStyleSource);
    }

    /// <summary>İpucunun FORMASI ekrandakı mətnə çatır.</summary>
    [Fact]
    public async Task IpucuFormasi_EkranaCatir()
    {
        var client = await NewChildAsync("hint-style@petpal.test");

        // Marşrut tapmacası olan macərəni seçdirir: yaradıcı yolda doğru/səhv
        // yoxdur və ipucu da yoxdur — bu test onun haqqında deyil.
        await SeedInterestAsync(client.ChildId, TraitKeys.Space, 95, MechanicKeys.Route, 95);

        await UpdateSettingsAsync(client, new UpdatePetBrainSettingsRequest
        {
            HintStyle = PetBrainHintStyle.Rule,
            HintTiming = PetBrainHintTiming.Immediate
        });

        var run = await PetBrainPlaythrough.AdvanceToPuzzleAsync(
            client, await PetBrainPlaythrough.StartAsync(client));

        Assert.True(run.Stage!.SupportsHint, "Bu tapmacada ipucu olmalıdır.");

        Assert.False(string.IsNullOrWhiteSpace(run.Stage.Hint),
            "«Dərhal» seçimi ilə kömək tapmaca açılan kimi görünməlidir.");

        Assert.StartsWith(SupportVoiceProbe.RulePrefix, run.Stage.Hint, StringComparison.Ordinal);
    }

    /// <summary>«Az variant» seçimi ekranda GÖRÜNƏN nəticə verir.</summary>
    [Fact]
    public async Task AzVariant_EkrandaGorunur()
    {
        var plain = await NewChildAsync("options-plain@petpal.test");
        var reduced = await NewChildAsync("options-reduced@petpal.test");

        await UpdateSettingsAsync(reduced, new UpdatePetBrainSettingsRequest { ReducedOptions = true });

        var plainChoice = await AdvanceToChoiceAsync(plain);
        var reducedChoice = await AdvanceToChoiceAsync(reduced);

        Assert.True(plainChoice.Options.Count > SupportVoiceProbe.ReducedLimit,
            "Test yalnız variantı çox olan ekranda mənalıdır.");

        Assert.Equal(SupportVoiceProbe.ReducedLimit, reducedChoice.Options.Count);
        Assert.True(reducedChoice.Options.Count >= 2, "Bir variant seçim deyil, düymədir.");
    }

    // ==================== Açıq rəy ====================

    /// <summary>«Bəyənirəm» balı QALDIRIR və kartı kənara qoymur.</summary>
    [Fact]
    public async Task Beyenirem_BaliQaldirir()
    {
        var client = await NewChildAsync("like@petpal.test");
        var recommendation = (await StateAsync(client)).Recommendation!;
        var template = ExperienceCatalog.Find(recommendation.TemplateKey)!;

        var before = await TraitsAsync(client.ChildId);

        await FeedbackAsync(client, recommendation.DecisionId, PetBrainRecommendationFeedback.Liked);

        var after = await TraitsAsync(client.ChildId);
        var key = $"{PetBrainTraitCategory.Interest}:{template.PrimaryInterest}";

        Assert.True(
            after[key] > before.GetValueOrDefault(key, TraitKeys.StartingScore),
            "Açıq bəyənmə balı qaldırmalıdır.");
    }

    /// <summary>«Daha az göstər» kartı geri çəkir və qeydi saxlayır.</summary>
    [Fact]
    public async Task DahaAzGoster_KartiGeriCekir()
    {
        var client = await NewChildAsync("show-less@petpal.test");
        var recommendation = (await StateAsync(client)).Recommendation!;

        var state = await FeedbackAsync(
            client, recommendation.DecisionId, PetBrainRecommendationFeedback.ShowLess);

        Assert.NotEqual(recommendation.TemplateKey, state.Recommendation!.TemplateKey);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var preference = await db.ContentPreferences.AsNoTracking()
            .FirstAsync(p => p.ChildProfileId == client.ChildId);

        Assert.Equal(PetBrainContentPreferenceKind.ShowLess, preference.Kind);
        Assert.NotNull(preference.ExpiresAt);
    }

    /// <summary>
    /// <b>Açıq siqnal dolayı siqnaldan güclüdür.</b> «Bəyənirəm» bir tamamlama
    /// qədər və ya daha çox təsir edir.
    /// </summary>
    [Fact]
    public async Task AcigSiqnal_DolayiSiqnaldanGucludur()
    {
        var explicitChild = await NewChildAsync("explicit-strong@petpal.test");
        var implicitChild = await NewChildAsync("implicit-weak@petpal.test");

        var explicitRec = (await StateAsync(explicitChild)).Recommendation!;
        var template = ExperienceCatalog.Find(explicitRec.TemplateKey)!;

        await FeedbackAsync(explicitChild, explicitRec.DecisionId, PetBrainRecommendationFeedback.Liked);

        // Dolayı yol: yalnız kartın başladılması (seçim siqnalı).
        var implicitRec = (await StateAsync(implicitChild)).Recommendation!;
        await client_StartAsync(implicitChild, implicitRec.DecisionId);

        var key = $"{PetBrainTraitCategory.Interest}:{template.PrimaryInterest}";

        var explicitScore = (await TraitsAsync(explicitChild.ChildId)).GetValueOrDefault(key);
        var implicitScore = (await TraitsAsync(implicitChild.ChildId))
            .GetValueOrDefault($"{PetBrainTraitCategory.Interest}:{ExperienceCatalog.Find(implicitRec.TemplateKey)!.PrimaryInterest}");

        Assert.True(explicitScore > implicitScore,
            $"Açıq bəyənmə ({explicitScore}) dolayı seçimdən ({implicitScore}) güclü olmalıdır.");
    }

    /// <summary>Uşaq BLOK göndərə bilmir — o, valideyn qərarıdır.</summary>
    [Fact]
    public async Task Usaq_BlokGondere_Bilmir()
    {
        var client = await NewChildAsync("child-block@petpal.test");

        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/content-feedback",
            new PetBrainContentFeedbackRequest
            {
                Scope = PetBrainContentScope.Theme,
                Key = TraitKeys.Space,
                Kind = PetBrainContentPreferenceKind.Blocked
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ==================== Valideyn nəzarəti ====================

    /// <summary>Valideyn toplanan profili GÖRÜR — bal və sübutu ilə birlikdə.</summary>
    [Fact]
    public async Task Valideyn_ProfiliGorur()
    {
        var client = await NewChildAsync("parent-view@petpal.test");
        await SubmitOnboardingAsync(client, new PetBrainOnboardingRequest
        {
            Topics = [TraitKeys.Space],
            Activities = [MechanicKeys.Route]
        });

        client.SwitchToParent();
        var view = await ParentViewAsync(client);

        Assert.Equal(client.ChildId, view.ChildId);
        Assert.NotEmpty(view.Interests);
        Assert.NotEmpty(view.Mechanics);
        Assert.NotEmpty(view.BlockableThemes);

        Assert.All(view.Interests, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Label));
            Assert.True(entry.ObservationCount >= 0);
        });
    }

    /// <summary>Valideyn mövzunu bloklaya bilir və blok DƏRHAL işləyir.</summary>
    [Fact]
    public async Task Valideyn_MovzuBloklaya_Bilir()
    {
        var client = await NewChildAsync("parent-block@petpal.test");

        client.SwitchToParent();

        foreach (var theme in new[] { TraitKeys.Space, TraitKeys.Fantasy })
            (await client.Http.PostAsJsonAsync(
                $"/api/parent/pet-brain/children/{client.ChildId}/personalization/blocks",
                new ParentBlockContentRequest { Scope = PetBrainContentScope.Theme, Key = theme, Blocked = true }))
                .EnsureSuccessStatusCode();

        client.SwitchToChild();
        var state = await StateAsync(client);

        Assert.NotEqual(TraitKeys.Space, state.Recommendation!.Theme);
        Assert.NotEqual(TraitKeys.Fantasy, state.Recommendation.Theme);
        Assert.All(state.Alternatives, alt =>
        {
            Assert.NotEqual(TraitKeys.Space, alt.Theme);
            Assert.NotEqual(TraitKeys.Fantasy, alt.Theme);
        });
    }

    /// <summary>Blok geri götürülə bilir.</summary>
    [Fact]
    public async Task Valideyn_BlokuGeriGotureBilir()
    {
        var client = await NewChildAsync("parent-unblock@petpal.test");

        client.SwitchToParent();

        var url = $"/api/parent/pet-brain/children/{client.ChildId}/personalization/blocks";

        (await client.Http.PostAsJsonAsync(url, new ParentBlockContentRequest
        {
            Scope = PetBrainContentScope.Theme, Key = TraitKeys.Space, Blocked = true
        })).EnsureSuccessStatusCode();

        (await client.Http.PostAsJsonAsync(url, new ParentBlockContentRequest
        {
            Scope = PetBrainContentScope.Theme, Key = TraitKeys.Space, Blocked = false
        })).EnsureSuccessStatusCode();

        var view = await ParentViewAsync(client);

        Assert.DoesNotContain(view.ContentPreferences,
            p => p.Kind == PetBrainContentPreferenceKind.Blocked);
    }

    /// <summary>
    /// <b>Fərdiləşdirmə söndürüləndə oyun TAM işləyir</b> — sadəcə profil
    /// oxunmur.
    /// </summary>
    [Fact]
    public async Task Ferdilesdirme_Sondurule_Bilir()
    {
        var client = await NewChildAsync("parent-off@petpal.test");

        client.SwitchToParent();
        await UpdateParentAsync(client, new UpdateParentPersonalizationRequest
        {
            PersonalizationEnabled = false
        });

        client.SwitchToChild();
        var state = await StateAsync(client);

        Assert.NotNull(state.Recommendation);
        Assert.False(state.Settings.PersonalizationEnabled);
        Assert.False(state.Recommendation!.CanGiveFeedback);
        Assert.False(state.Settings.SurpriseEnabled);
    }

    /// <summary>
    /// <b>Sıfırlama öyrənilmişi silir, AYARLARI saxlayır.</b> «Öyrəndiklərini
    /// unut» ilə «mənim seçimlərimi sil» iki fərqli əməliyyatdır.
    /// </summary>
    [Fact]
    public async Task Sifirlama_OgrenilmisiSilir_AyarlariSaxlayir()
    {
        var client = await NewChildAsync("parent-reset@petpal.test");

        await SubmitOnboardingAsync(client, new PetBrainOnboardingRequest
        {
            Topics = [TraitKeys.Space],
            Activities = [MechanicKeys.Route]
        });

        await UpdateSettingsAsync(client, new UpdatePetBrainSettingsRequest { ReducedMotion = true });

        client.SwitchToParent();

        var response = await client.Http.DeleteAsync(
            $"/api/parent/pet-brain/children/{client.ChildId}/personalization");

        response.EnsureSuccessStatusCode();

        var result = (await response.Content.ReadFromJsonAsync<PetBrainResetResultDto>())!;

        Assert.True(result.TraitsRemoved > 0);
        Assert.True(result.SettingsKept);

        Assert.Empty(await TraitsAsync(client.ChildId));

        var view = await ParentViewAsync(client);

        Assert.True(view.Settings.ReducedMotion, "Əlçatanlıq ayarı sıfırlama ilə getməməlidir.");
    }

    /// <summary>Valideyn bloku sıfırlamadan SONRA da qalır — o, qaydadır.</summary>
    [Fact]
    public async Task Sifirlama_ValideynBlokunuSilmir()
    {
        var client = await NewChildAsync("parent-reset-block@petpal.test");

        client.SwitchToParent();

        (await client.Http.PostAsJsonAsync(
            $"/api/parent/pet-brain/children/{client.ChildId}/personalization/blocks",
            new ParentBlockContentRequest
            {
                Scope = PetBrainContentScope.Theme, Key = TraitKeys.Space, Blocked = true
            })).EnsureSuccessStatusCode();

        (await client.Http.DeleteAsync(
            $"/api/parent/pet-brain/children/{client.ChildId}/personalization")).EnsureSuccessStatusCode();

        var view = await ParentViewAsync(client);

        Assert.Contains(view.ContentPreferences,
            p => p.Kind == PetBrainContentPreferenceKind.Blocked && p.Key == TraitKeys.Space);
    }

    /// <summary>İxrac PII saxlamır — yalnız açarlar, ballar və zolaqlar.</summary>
    [Fact]
    public async Task Ixrac_SexsiMelumatSaxlamir()
    {
        var client = await NewChildAsync("export@petpal.test", childName: "Aylin");
        await StateAsync(client);

        client.SwitchToParent();

        var export = (await client.Http.GetFromJsonAsync<PetBrainProfileExportDto>(
            $"/api/parent/pet-brain/children/{client.ChildId}/personalization/export"))!;

        Assert.Equal(client.ChildId, export.ChildId);
        Assert.False(string.IsNullOrWhiteSpace(export.AgeBand));
        Assert.DoesNotContain(':', export.AgeBand);

        // Dəqiq yaş DEYİL, zolaq.
        Assert.Contains('-', export.AgeBand);

        Assert.All(export.RecentDecisions, decision =>
        {
            Assert.DoesNotContain("Aylin", decision.TemplateKey, StringComparison.OrdinalIgnoreCase);
            Assert.All(decision.FilteredCandidates, entry =>
                Assert.DoesNotContain("Aylin", entry, StringComparison.OrdinalIgnoreCase));
        });
    }

    /// <summary>Qərar tarixçəsi «niyə bu macəra?» sualının izini saxlayır.</summary>
    [Fact]
    public async Task QerarTarixcesi_SebebKodlariniSaxlayir()
    {
        var client = await NewChildAsync("decisions@petpal.test");
        await StateAsync(client);

        client.SwitchToParent();

        var decisions = (await client.Http.GetFromJsonAsync<List<ParentDecisionEntryDto>>(
            $"/api/parent/pet-brain/children/{client.ChildId}/personalization/decisions"))!;

        Assert.NotEmpty(decisions);
        Assert.All(decisions, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Title));
            Assert.True(d.PolicyVersion > 0);
        });

        Assert.Contains(decisions, d => d.WhyReasons.Count > 0);
    }

    // ==================== İzolyasiya ====================

    /// <summary>Yad uşağın profili nə oxunur, nə dəyişdirilir.</summary>
    [Fact]
    public async Task YadUsaq_ProfiliOxuna_Bilmir()
    {
        var owner = await NewChildAsync("iso-owner@petpal.test");
        var stranger = await NewChildAsync("iso-stranger@petpal.test");

        stranger.SwitchToParent();

        var read = await stranger.Http.GetAsync(
            $"/api/parent/pet-brain/children/{owner.ChildId}/personalization");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        var reset = await stranger.Http.DeleteAsync(
            $"/api/parent/pet-brain/children/{owner.ChildId}/personalization");

        Assert.Equal(HttpStatusCode.NotFound, reset.StatusCode);
    }

    /// <summary>
    /// İki uşağın ayarları QARIŞMIR — biri hərəkəti azaltsa, digəri təsirlənmir.
    /// </summary>
    [Fact]
    public async Task IkiUsaq_AyarlariQarismir()
    {
        var first = await NewChildAsync("iso-a@petpal.test");
        var second = await NewChildAsync("iso-b@petpal.test");

        await UpdateSettingsAsync(first, new UpdatePetBrainSettingsRequest
        {
            ReducedMotion = true,
            HintStyle = PetBrainHintStyle.Rule
        });

        var secondState = await StateAsync(second);

        Assert.False(secondState.Settings.ReducedMotion);
        Assert.Equal(PetBrainHintStyle.Visual, secondState.Settings.HintStyle);
    }

    /// <summary>
    /// <b>İki fərqli uşaq eyni başlanğıcdan fərqli təcrübə alır.</b>
    /// Definition of Done-un birinci bəndi.
    /// </summary>
    [Fact]
    public async Task IkiFerqliProfil_FerqliTecrubeAlir()
    {
        var explorer = await NewChildAsync("persona-explorer@petpal.test");
        var maker = await NewChildAsync("persona-maker@petpal.test");

        await SubmitOnboardingAsync(explorer, new PetBrainOnboardingRequest
        {
            Topics = [TraitKeys.Space, TraitKeys.Science],
            Activities = [MechanicKeys.Route, MechanicKeys.Exploration],
            SessionLength = PetBrainSessionLength.Long
        });

        await SubmitOnboardingAsync(maker, new PetBrainOnboardingRequest
        {
            Topics = [TraitKeys.Animals, TraitKeys.Nature],
            Activities = [MechanicKeys.Building, MechanicKeys.Decorating],
            SessionLength = PetBrainSessionLength.Short
        });

        await SeedInterestAsync(explorer.ChildId, TraitKeys.Space, 92, MechanicKeys.Route, 90);
        await SeedInterestAsync(maker.ChildId, TraitKeys.Animals, 92, MechanicKeys.Building, 90);

        var explorerState = await StateAsync(explorer);
        var makerState = await StateAsync(maker);

        Assert.NotEqual(
            explorerState.Recommendation!.TemplateKey,
            makerState.Recommendation!.TemplateKey);

        // Fərq yalnız başlıqda deyil — izah da fərqlidir.
        Assert.NotEqual(
            string.Join('|', explorerState.Recommendation.Reasons),
            string.Join('|', makerState.Recommendation.Reasons));
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

    private static async Task SubmitOnboardingAsync(
        ApiTestClient client, PetBrainOnboardingRequest request) =>
        (await client.Http.PostAsJsonAsync("/api/pet-brain/onboarding", request)).EnsureSuccessStatusCode();

    private static async Task<PetBrainSettingsDto> UpdateSettingsAsync(
        ApiTestClient client, UpdatePetBrainSettingsRequest request)
    {
        var response = await client.Http.PutAsJsonAsync("/api/pet-brain/settings", request);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PetBrainSettingsDto>())!;
    }

    private static async Task<PetBrainSettingsDto> UpdateParentAsync(
        ApiTestClient client, UpdateParentPersonalizationRequest request)
    {
        var response = await client.Http.PutAsJsonAsync(
            $"/api/parent/pet-brain/children/{client.ChildId}/personalization", request);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PetBrainSettingsDto>())!;
    }

    private static async Task<ParentPersonalizationDto> ParentViewAsync(ApiTestClient client) =>
        (await client.Http.GetFromJsonAsync<ParentPersonalizationDto>(
            $"/api/parent/pet-brain/children/{client.ChildId}/personalization"))!;

    private static async Task<PetBrainStateDto> FeedbackAsync(
        ApiTestClient client, Guid decisionId, PetBrainRecommendationFeedback feedback)
    {
        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/recommendation/feedback",
            new PetBrainFeedbackRequest { DecisionId = decisionId, Feedback = feedback });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PetBrainStateDto>())!;
    }

    private static async Task client_StartAsync(ApiTestClient client, Guid decisionId) =>
        (await client.Http.PostAsJsonAsync("/api/pet-brain/runs",
            new StartPetBrainRunRequest { DecisionId = decisionId })).EnsureSuccessStatusCode();

    /// <summary>İlk SEÇİM ekranına qədər aparır.</summary>
    private static async Task<PetBrainStageDto> AdvanceToChoiceAsync(ApiTestClient client)
    {
        var run = await PetBrainPlaythrough.StartAsync(client);

        for (var step = 0; step < 12 && !PetBrainPlaythrough.IsFinished(run); step++)
        {
            if (run.Stage!.Kind == PetBrainStageKind.Choice)
                return run.Stage;

            run = await PetBrainPlaythrough.StepAsync(client, run);
        }

        throw new InvalidOperationException("Seçim ekranına çatılmadı.");
    }

    private async Task<Dictionary<string, int>> TraitsAsync(Guid childId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.PlayerTraits
            .AsNoTracking()
            .Where(t => t.ChildProfileId == childId)
            .ToDictionaryAsync(t => $"{t.Category}:{t.Key}", t => t.Score);
    }

    /// <summary>Profilə güclü, sübutlu maraq yazır — nümayiş üçün.</summary>
    private async Task SeedInterestAsync(
        Guid childId, string interest, int interestScore, string mechanic, int mechanicScore)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = _factory.Clock.GetUtcNow().UtcDateTime;

        await UpsertAsync(PetBrainTraitCategory.Interest, interest, interestScore);
        await UpsertAsync(PetBrainTraitCategory.Mechanic, mechanic, mechanicScore);

        await db.SaveChangesAsync();

        async Task UpsertAsync(PetBrainTraitCategory category, string key, int score)
        {
            var trait = await db.PlayerTraits.FirstOrDefaultAsync(
                t => t.ChildProfileId == childId && t.Category == category && t.Key == key);

            if (trait is null)
            {
                trait = new PlayerTrait { ChildProfileId = childId, Category = category, Key = key };
                db.PlayerTraits.Add(trait);
            }

            trait.Score = score;
            trait.ObservationCount = 8;
            trait.PositiveEvidence = 8;
            trait.SourceMask = TraitEvidence.WithSource(0, PetBrainEvidenceSource.Adventure);
            trait.LastObservedAt = now;
            trait.UpdatedAt = now;
        }
    }
}

/// <summary>
/// İpucu çərçivəsinin prefiksini testə açan kiçik köməkçi.
///
/// <para>Mətn <c>SupportVoice</c>-dadır; test onu təkrar yazsaydı, iki nüsxə
/// bir-birindən sürüşərdi.</para>
/// </summary>
internal static class SupportVoiceProbe
{
    public static int ReducedLimit => PetPal.Api.PetBrain.Mind.SupportVoice.ReducedOptionLimit;

    public static string RulePrefix =>
        PetPal.Api.PetBrain.Mind.SupportVoice.Frame("x", PetBrainHintStyle.Rule, "az")
            .Replace(" x", string.Empty, StringComparison.Ordinal);
}
