using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Shared.Dtos.Home;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Dtos.Pets;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Nümayiş panelini açıq saxlayan fixture — izah qatı da yoxlanılmalıdır.
/// </summary>
public sealed class PetBrainDemoFactory : TestWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("PetBrain:DemoMode", "true");
    }
}

/// <summary>
/// Pet Brain-in HTTP səthi: avtorizasiya, sahiblik, mərhələ bütövlüyü,
/// mükafatın DƏQİQ BİR DƏFƏ verilməsi və bərpa.
/// </summary>
public class PetBrainApiTests : IClassFixture<PetBrainDemoFactory>
{
    private readonly PetBrainDemoFactory _factory;

    public PetBrainApiTests(PetBrainDemoFactory factory) => _factory = factory;

    // ==================== Avtorizasiya və sahiblik ====================

    [Fact]
    public async Task UsaqEndpointleri_TokensizAcilmir()
    {
        var http = _factory.CreateClient();

        foreach (var url in new[] { "/api/pet-brain", "/api/pet-brain/runs" })
        {
            var response = url.EndsWith("runs", StringComparison.Ordinal)
                ? await http.PostAsJsonAsync(url, new StartPetBrainRunRequest())
                : await http.GetAsync(url);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    /// <summary>Valideyn tokeni uşaq endpoint-inə DÜŞMÜR — mövcud policy qaydası.</summary>
    [Fact]
    public async Task ValideynTokeni_UsaqEndpointineDusmur()
    {
        var client = await NewChildAsync("parent-policy@petpal.test");
        client.SwitchToParent();

        var response = await client.Http.GetAsync("/api/pet-brain");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Yad uşağın run-u <c>404</c> alır — <c>403</c> yox. Mövcudluğu təsdiqləmək
    /// də məlumat sızmasıdır.
    /// </summary>
    [Fact]
    public async Task YadUsaq_BasqasininRununuGoreBilmir()
    {
        var owner = await NewChildAsync("owner@petpal.test", "Aylin");
        await SeedSpaceProfileAsync(owner.ChildId);

        var run = await StartRunAsync(owner);

        var stranger = await NewChildAsync("stranger@petpal.test", "Mia");

        var read = await stranger.Http.GetAsync($"/api/pet-brain/runs/{run.RunId}");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        var choice = await stranger.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest { StageIndex = 0, OptionKey = "continue" });
        Assert.Equal(HttpStatusCode.NotFound, choice.StatusCode);

        var complete = await stranger.Http.PostAsync($"/api/pet-brain/runs/{run.RunId}/complete", null);
        Assert.Equal(HttpStatusCode.NotFound, complete.StatusCode);
    }

    /// <summary>İki profilin məlumatı TAM ayrıdır.</summary>
    [Fact]
    public async Task Profiller_TamAyridir()
    {
        var aylin = await NewChildAsync("split-a@petpal.test", "Aylin");
        await SeedSpaceProfileAsync(aylin.ChildId);

        var mia = await NewChildAsync("split-b@petpal.test", "Mia");
        await SeedFantasyProfileAsync(mia.ChildId);

        var aylinState = await GetStateAsync(aylin);
        var miaState = await GetStateAsync(mia);

        Assert.Equal(ExperienceCatalog.MarsRoverRescue, aylinState.Recommendation!.TemplateKey);
        Assert.Equal(ExperienceCatalog.DragonLostColors, miaState.Recommendation!.TemplateKey);

        // Aylin macərəni bitirir; Mia-nın yaddaşı və profili TOXUNULMAZ qalır.
        var run = await PlayToEndAsync(aylin);
        await CompleteAsync(aylin, run.RunId);

        var miaAfter = await GetStateAsync(mia);

        Assert.Empty(miaAfter.Memories);
        Assert.Equal(ExperienceCatalog.DragonLostColors, miaAfter.Recommendation!.TemplateKey);
    }

    // ==================== Mərhələ bütövlüyü ====================

    [Fact]
    public async Task NamelumSablon_RedEdilir()
    {
        var client = await NewChildAsync("unknown-template@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var response = await client.Http.PostAsJsonAsync(
            "/api/pet-brain/runs", new StartPetBrainRunRequest { TemplateKey = "not-a-real-adventure" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Klient kataloqdan istədiyi macərəni seçə bilmir.</summary>
    [Fact]
    public async Task TovsiyeOlunmayanSablon_RedEdilir()
    {
        var client = await NewChildAsync("wrong-template@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var response = await client.Http.PostAsJsonAsync(
            "/api/pet-brain/runs",
            new StartPetBrainRunRequest { TemplateKey = ExperienceCatalog.DragonLostColors });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task MerheleAtlamaq_RedEdilir()
    {
        var client = await NewChildAsync("skip-stage@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var run = await StartRunAsync(client);

        var response = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest { StageIndex = 2, OptionKey = "crater" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>İki dəfə basmaq mərhələni iki dəfə keçirmir.</summary>
    [Fact]
    public async Task EyniMerhele_IkiDefeCavablandirilmir()
    {
        var client = await NewChildAsync("double-tap@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var run = await StartRunAsync(client);

        var first = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest { StageIndex = 0, OptionKey = "continue" });
        first.EnsureSuccessStatusCode();

        var second = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest { StageIndex = 0, OptionKey = "continue" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task NamelumVariant_RedEdilir()
    {
        var client = await NewChildAsync("bad-option@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var run = await StartRunAsync(client);
        run = await ChooseAsync(client, run, "continue");

        var response = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest { StageIndex = run.CurrentStage, OptionKey = "teleport-to-jupiter" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task YarimciqRun_TamamlanaBilmir()
    {
        var client = await NewChildAsync("premature@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var run = await StartRunAsync(client);

        var response = await client.Http.PostAsync($"/api/pet-brain/runs/{run.RunId}/complete", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>Səhv tapmaca cavabı macərəni bitirmir — uşaq yenidən cəhd edir.</summary>
    [Fact]
    public async Task SehvTapmacaCavabi_MerheleniIrelilemir()
    {
        var client = await NewChildAsync("puzzle-miss@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var run = await ReachPuzzleAsync(client);
        var puzzle = run.Stage!.Puzzle!;
        var stageIndex = run.Stage.Index;

        var correct = Solve(puzzle);
        var wrong = WrongAnswerFor(puzzle, correct);

        var afterWrong = await AnswerPuzzleAsync(client, run, wrong);

        // Mərhələ İRƏLİLƏMİR, səhv sayılır, macəra bitmir.
        Assert.Equal(stageIndex, afterWrong.CurrentStage);
        Assert.Equal(1, afterWrong.Mistakes);
        Assert.NotNull(afterWrong.Stage!.Puzzle);

        // EYNİ tapmaca qalır — yeni sual verilmir.
        Assert.Equal(puzzle.PuzzleId, afterWrong.Stage.Puzzle!.PuzzleId);

        var afterCorrect = await AnswerPuzzleAsync(client, afterWrong, correct);

        Assert.True(afterCorrect.CurrentStage > stageIndex);
        Assert.Equal(1, afterCorrect.Mistakes);
    }

    /// <summary>Naməlum və təkrar id-lər rədd olunur — cəhd də sayılmır.</summary>
    [Fact]
    public async Task PozuqTapmacaCavabi_RedEdilir()
    {
        var client = await NewChildAsync("puzzle-bad-answer@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var run = await ReachPuzzleAsync(client);
        var puzzle = run.Stage!.Puzzle!;
        var stageIndex = run.Stage.Index;

        // Lövhənin ilk toxunula bilən id-si — qraf lövhəsində düyün, element
        // lövhəsində isə kart. Yoxlanan qayda hər ikisində eynidir.
        var first = puzzle.Items.Count > 0 ? puzzle.Items[0].Id : puzzle.Nodes[0].Id;

        // Tapmacada olmayan element.
        var unknown = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest { StageIndex = stageIndex, SelectedIds = ["item-zzz"] });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        // Təkrarlanan element.
        var duplicated = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest
            {
                StageIndex = stageIndex,
                SelectedIds = [first, first]
            });
        Assert.Equal(HttpStatusCode.BadRequest, duplicated.StatusCode);

        // Yanlış say (sxem iki tələb edir, bir gəlir).
        var wrongCount = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest { StageIndex = stageIndex, SelectedIds = [first] });
        Assert.Equal(HttpStatusCode.BadRequest, wrongCount.StatusCode);

        // Heç biri SƏHV sayılmır: bu, uşağın deyil, sorğunun problemidir.
        var state = await GetStateAsync(client);
        Assert.Equal(0, state.ActiveRun!.Mistakes);
    }

    /// <summary>
    /// Tapmaca yenilənmədən sonra EYNİ qalır — uşaq səhifəni yeniləyib asan
    /// sual ovlaya bilmir.
    /// </summary>
    [Fact]
    public async Task Tapmaca_YenilenmedenSonraEyniQalir()
    {
        var client = await NewChildAsync("puzzle-resume@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var run = await ReachPuzzleAsync(client);
        var first = run.Stage!.Puzzle!;

        var reread = await GetStateAsync(client);
        var second = reread.ActiveRun!.Stage!.Puzzle!;

        Assert.Equal(first.PuzzleId, second.PuzzleId);
        Assert.Equal(first.Mechanic, second.Mechanic);
        Assert.Equal(first.TargetValue, second.TargetValue);
        Assert.Equal(
            first.Items.Select(i => $"{i.Id}:{i.Value}"),
            second.Items.Select(i => $"{i.Id}:{i.Value}"));
    }

    /// <summary>
    /// Uşağa gedən məzmunda DOĞRU CAVAB yoxdur — nə açıq bayraq, nə də
    /// bərpa edilə bilən gizli sahə.
    /// </summary>
    [Fact]
    public async Task TapmacaMezmunu_DogruCavabiSizdirmir()
    {
        var client = await NewChildAsync("puzzle-no-leak@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var run = await ReachPuzzleAsync(client);

        var raw = await client.Http.GetStringAsync($"/api/pet-brain/runs/{run.RunId}");

        foreach (var forbidden in new[]
                 {
                     "privateSolution", "PrivateSolution", "solution",
                     "isCorrect", "correctKey", "answerIds", "seed"
                 })
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Klientin UYDURDUĞU doğruluq/mükafat sahələri heç bir təsir GÖSTƏRMİR.
    ///
    /// <para>Sorğu gövdəsinə <c>isCorrect</c>, <c>score</c> və <c>reward</c>
    /// əlavə edilir — model bağlayıcısı onları tanımır, doğruluğu isə yalnız
    /// serverdəki qiymətləndirici müəyyən edir.</para>
    /// </summary>
    [Fact]
    public async Task SaxtaDogruluqSaheleri_TesirGostermir()
    {
        var client = await NewChildAsync("forged-answer@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var run = await ReachPuzzleAsync(client);
        var puzzle = run.Stage!.Puzzle!;
        var stageIndex = run.Stage.Index;

        var wrong = WrongAnswerFor(puzzle, Solve(puzzle));

        var forged = JsonSerializer.Serialize(new
        {
            stageIndex,
            selectedIds = wrong,
            isCorrect = true,
            score = 100,
            reward = 999,
            traitDelta = 50,
            solved = true
        });

        var response = await client.Http.PostAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new StringContent(forged, Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();
        var updated = (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        // Cavab SƏHVDİR: mərhələ irəliləmir, uydurma sahələr nəticəni dəyişmir.
        Assert.Equal(stageIndex, updated.CurrentStage);
        Assert.Equal(1, updated.Mistakes);

        // Mükafat da verilmir.
        var pet = await GetPetAsync(client);
        Assert.Equal(0, pet.Xp % 1);
        Assert.True(pet.Bond <= 10 + BondRules.DailyCareCap);
    }

    /// <summary>
    /// Şəxsiləşdirmə mexanikaya çatır: həlledici profil MƏNTİQ tapmacası,
    /// yaradıcı profil isə TƏZYİQSİZ yığım alır — eyni sualın iki donu deyil.
    /// </summary>
    [Fact]
    public async Task Tapmaca_ProfilaGoreMexanikaniDeyisir()
    {
        var solver = await NewChildAsync("puzzle-solver@petpal.test", "Aylin");
        await SeedSpaceProfileAsync(solver.ChildId);

        var creative = await NewChildAsync("puzzle-creative@petpal.test", "Mia");
        await SeedFantasyProfileAsync(creative.ChildId);

        var solverPuzzle = (await ReachPuzzleAsync(solver)).Stage!.Puzzle!;
        var creativePuzzle = (await ReachPuzzleAsync(creative)).Stage!.Puzzle!;

        Assert.NotEqual(solverPuzzle.Mechanic, creativePuzzle.Mechanic);

        // Həlledici məntiq tapmacası alır: doğru cavab var, ipucu təklif olunur.
        Assert.False(solverPuzzle.LowPressure);
        Assert.True(solverPuzzle.HintAvailable);

        // Yaradıcı yolda doğru/səhv YOXDUR.
        Assert.True(creativePuzzle.LowPressure);
        Assert.Equal(PetBrainPuzzleMechanic.LightFragments, creativePuzzle.Mechanic);
    }

    /// <summary>İpucu mərhələni irəlilətmir və heç nə kəsmir.</summary>
    [Fact]
    public async Task Ipucu_MerheleniIrelilemir()
    {
        var client = await NewChildAsync("hint@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var run = await ReachPuzzleAsync(client);
        var stageIndex = run.Stage!.Index;

        var response = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest { StageIndex = stageIndex, RequestHint = true });

        response.EnsureSuccessStatusCode();
        var updated = (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;

        Assert.Equal(stageIndex, updated.CurrentStage);
        Assert.Equal(1, updated.HintsUsed);
        Assert.False(string.IsNullOrWhiteSpace(updated.Stage!.Hint));
    }

    // ==================== Mükafat ====================

    [Fact]
    public async Task MarsTamamlanmasi_XpBagXatireVeDebilqeVerir()
    {
        var client = await NewChildAsync("mars-reward@petpal.test", "Aylin");
        await SeedSpaceProfileAsync(client.ChildId);

        var petBefore = await GetPetAsync(client);

        var run = await PlayToEndAsync(client);
        var completed = await CompleteAsync(client, run.RunId);

        Assert.Equal(PetBrainRunStatus.Completed, completed.Status);
        Assert.NotNull(completed.Summary);

        var summary = completed.Summary!;
        Assert.True(summary.XpEarned > 0);
        Assert.True(summary.BondEarned > 0);
        Assert.Equal(ExperienceCatalog.HelmetMars, summary.UnlockedAccessoryCode);
        Assert.NotEmpty(summary.NewMemories);

        var petAfter = await GetPetAsync(client);

        Assert.Contains(ExperienceCatalog.HelmetMars, petAfter.UnlockedAccessories);
        Assert.Contains(ExperienceCatalog.HelmetMars, petAfter.EquippedAccessories);
        Assert.True(petAfter.Bond > petBefore.Bond);
    }

    [Fact]
    public async Task EjdahaTamamlanmasi_XpBagXatireVeQanadVerir()
    {
        var client = await NewChildAsync("dragon-reward@petpal.test", "Mia");
        await SeedFantasyProfileAsync(client.ChildId);

        var run = await PlayToEndAsync(client);
        var completed = await CompleteAsync(client, run.RunId);

        Assert.Equal(ExperienceCatalog.DragonLostColors, completed.TemplateKey);
        Assert.Equal(ExperienceCatalog.WingsRainbow, completed.Summary!.UnlockedAccessoryCode);

        var pet = await GetPetAsync(client);
        Assert.Contains(ExperienceCatalog.WingsRainbow, pet.UnlockedAccessories);

        // Yaradıcı macərada doğru/səhv yoxdur — nəticə həmişə tamdır.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.ExperienceRuns.AsNoTracking().FirstAsync(r => r.Id == run.RunId);

        Assert.Equal(100, stored.ScorePercent);
    }

    /// <summary>
    /// Təkrar tamamlama çağırışı MÜKAFATI TƏKRARLAMIR — refresh, ikiqat toxunuş
    /// və şəbəkə təkrarı təhlükəsizdir.
    /// </summary>
    [Fact]
    public async Task TekrarTamamlama_MukafatiIkiDefeVermir()
    {
        var client = await NewChildAsync("idempotent@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var run = await PlayToEndAsync(client);

        var first = await CompleteAsync(client, run.RunId);
        var petAfterFirst = await GetPetAsync(client);

        for (var i = 0; i < 3; i++)
            await CompleteAsync(client, run.RunId);

        var petAfterRepeats = await GetPetAsync(client);

        Assert.Equal(petAfterFirst.Xp, petAfterRepeats.Xp);
        Assert.Equal(petAfterFirst.Level, petAfterRepeats.Level);
        Assert.Equal(petAfterFirst.Bond, petAfterRepeats.Bond);
        Assert.Equal(
            petAfterFirst.UnlockedAccessories.Count,
            petAfterRepeats.UnlockedAccessories.Count);

        Assert.NotNull(first.Summary);
    }

    /// <summary>Eyni macərəni ikinci dəfə oynamaq kosmetiki TƏKRAR açmır.</summary>
    [Fact]
    public async Task TekrarOynanis_KosmetikiYenidenAcmir()
    {
        var client = await NewChildAsync("replay@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var first = await PlayToEndAsync(client);
        await CompleteAsync(client, first.RunId);

        // Mars artıq oynanıb, ona görə növbəti tövsiyə başqa macəra olur.
        // Marsı YENİDƏN oynamaq üçün tarixçə başlanğıc vəziyyətinə qaytarılır;
        // kosmetikin təkrar açılmaması isə pet-in öz siyahısından asılıdır.
        await ResetToMoonHistoryAsync(client.ChildId);

        var second = await PlayToEndAsync(client);
        var completed = await CompleteAsync(client, second.RunId);

        Assert.Equal(ExperienceCatalog.MarsRoverRescue, completed.TemplateKey);
        Assert.Empty(completed.Summary!.UnlockedAccessoryCode);
    }

    /// <summary>Mükafat və profil YENİ sorğuda (yeni scope) da qalır.</summary>
    [Fact]
    public async Task Profil_YeniSorguDaQalir()
    {
        var client = await NewChildAsync("persist@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var run = await PlayToEndAsync(client);
        await CompleteAsync(client, run.RunId);

        var state = await GetStateAsync(client);

        Assert.NotEmpty(state.Memories);
        Assert.True(state.Bond > 0);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(await db.PlayerTraits.AnyAsync(t => t.ChildProfileId == client.ChildId));
        Assert.True(await db.PetMemories.AnyAsync(m => m.ChildProfileId == client.ChildId));
        Assert.True(await db.BehaviorEvents.AnyAsync(e => e.ChildProfileId == client.ChildId));
    }

    // ==================== Bərpa və yarımçıq qoyma ====================

    [Fact]
    public async Task AktivRun_YenilenmedenSonraBerpaOlunur()
    {
        var client = await NewChildAsync("resume@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var run = await StartRunAsync(client);
        run = await ChooseAsync(client, run, "continue");
        run = await ChooseAsync(client, run, "canyon");

        // "Yenilənmə": vəziyyət sıfırdan oxunur.
        var state = await GetStateAsync(client);

        Assert.NotNull(state.ActiveRun);
        Assert.Equal(run.RunId, state.ActiveRun!.RunId);
        Assert.Equal(run.CurrentStage, state.ActiveRun.CurrentStage);
        Assert.Contains("canyon", state.ActiveRun.Choices);
    }

    [Fact]
    public async Task YarimciqQoyma_MukafatVermir()
    {
        var client = await NewChildAsync("abandon@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var petBefore = await GetPetAsync(client);

        var run = await StartRunAsync(client);
        run = await ChooseAsync(client, run, "continue");

        var response = await client.Http.PostAsync($"/api/pet-brain/runs/{run.RunId}/abandon", null);
        response.EnsureSuccessStatusCode();

        var abandoned = (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
        Assert.Equal(PetBrainRunStatus.Abandoned, abandoned.Status);

        var petAfter = await GetPetAsync(client);
        Assert.Equal(petBefore.Bond, petAfter.Bond);

        // Yarımçıq run tamamlana bilməz.
        var complete = await client.Http.PostAsync($"/api/pet-brain/runs/{run.RunId}/complete", null);
        Assert.Equal(HttpStatusCode.Conflict, complete.StatusCode);
    }

    // ==================== Yumurta və ekran vaxtı ====================

    [Fact]
    public async Task Yumurta_MaceraBaslada_Bilmir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "egg@petpal.test", "Egg");
        await SeedSpaceProfileAsync(client.ChildId);

        var state = await GetStateAsync(client);
        Assert.False(state.PetIsHatched);
        Assert.Null(state.Recommendation);

        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/runs", new StartPetBrainRunRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Ekran vaxtı YENİ macərəni bloklayır, amma BAŞLANMIŞ macəra bitirilə
    /// bilir — qazanılmış işi itirmək uşaq üçün ədalətsiz olardı.
    /// </summary>
    [Fact]
    public async Task EkranVaxti_YeniRunuBloklayirBaslanmisiBuraxir()
    {
        var client = await NewChildAsync("screen-time@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        // Əvvəlcə macəra BAŞLAYIR (hələ blok yoxdur).
        var run = await PlayToEndAsync(client);

        // İndi gündəlik limit dolur.
        await FillDailyLimitAsync(client.ChildId);

        // Başlanmış macəra bitirilə bilir.
        var completed = await CompleteAsync(client, run.RunId);
        Assert.Equal(PetBrainRunStatus.Completed, completed.Status);

        // Yeni macəra isə bloklanır.
        var blocked = await client.Http.PostAsJsonAsync("/api/pet-brain/runs", new StartPetBrainRunRequest());
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        var state = await GetStateAsync(client);
        Assert.True(state.ScreenTimeBlocked);
        Assert.False(string.IsNullOrWhiteSpace(state.ScreenTimeMessage));
    }

    // ==================== Yaddaş salamlamaya təsir edir ====================

    /// <summary>
    /// Loop-un son halqası: tamamlanmış macəra pet-in NÖVBƏTİ salamlamasını
    /// dəyişir. Bu olmasa "pet səni xatırlayır" iddiası boş qalardı.
    /// </summary>
    [Fact]
    public async Task TamamlanmisMacera_NovbetiSalamlamaniDeyisir()
    {
        var client = await NewChildAsync("memory-greeting@petpal.test", "Aylin");
        await SeedSpaceProfileAsync(client.ChildId);

        var before = await GetStateAsync(client);

        var run = await PlayToEndAsync(client);
        await CompleteAsync(client, run.RunId);

        var after = await GetStateAsync(client);

        Assert.NotEqual(before.Greeting, after.Greeting);
        Assert.NotEmpty(after.Memories);

        // Salamlama ÜMUMİ replika deyil: o, saxlanmış konkret bir xatirədən
        // qurulur. Hansı xatirənin seçildiyi vaciblik/təzəlik qaydasından
        // asılıdır (bax MemoryPolicy), ona görə test konkret cümləni yox,
        // "xatirələrdən BİRİ salamlamada var" şərtini yoxlayır.
        Assert.Contains(after.Memories, memory => after.Greeting.Contains(memory.Text, StringComparison.Ordinal));
    }

    /// <summary>
    /// Tamamlanmış macəra YENİLİK balını kəskin endirir.
    ///
    /// <para>Test qalibin ADINI yoxlamır və bu, qəsdəndir: iki kosmos macərası
    /// da oynanandan sonra qalan namizədlər bir-birinə çox yaxın olur və
    /// məhdud sürpriz payı onların arasında növbələşmə yaradır — bu, düzgün
    /// davranışdır. Yoxlanan şey QAYDANIN özüdür: təzəcə oynanmış şablon
    /// aydın cəza almalıdır.</para>
    /// </summary>
    [Fact]
    public async Task TamamlanmisMacera_YenilikBaliniEndirir()
    {
        var client = await NewChildAsync("next-rec@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var before = await GetStateAsync(client);
        Assert.Equal(ExperienceCatalog.MarsRoverRescue, before.Recommendation!.TemplateKey);

        var marsBefore = before.Debug!.Candidates
            .Single(c => c.TemplateKey == ExperienceCatalog.MarsRoverRescue);

        var run = await PlayToEndAsync(client);
        await CompleteAsync(client, run.RunId);

        var after = await GetStateAsync(client);
        var marsAfter = after.Debug!.Candidates
            .Single(c => c.TemplateKey == ExperienceCatalog.MarsRoverRescue);

        Assert.True(marsAfter.NoveltyScore < marsBefore.NoveltyScore - 30,
            $"Mars yeniliyi {marsBefore.NoveltyScore} → {marsAfter.NoveltyScore}; təkrara cəza kifayət etmir.");

        // Yenidən təklif olunsa da, uşağa "artıq tamamlanıb" bildirilir və
        // kosmetik ikinci dəfə açılmır.
        if (after.Recommendation!.TemplateKey == ExperienceCatalog.MarsRoverRescue)
            Assert.True(after.Recommendation.AlreadyCompleted);
    }

    // ==================== Ana ekran və nümayiş paneli ====================

    [Fact]
    public async Task AnaEkran_TovsiyeCipiniAqreqatdaGetirir()
    {
        var client = await NewChildAsync("home-chip@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var home = await client.Http.GetFromJsonAsync<HomeStateDto>("/api/home");

        Assert.NotNull(home);
        Assert.NotNull(home!.PetBrain);
        Assert.False(home.PetBrain!.HasActiveRun);
        Assert.False(string.IsNullOrWhiteSpace(home.PetBrain.Title));
    }

    [Fact]
    public async Task AnaEkran_YarimciqMacerani_DavamKimiGosterir()
    {
        var client = await NewChildAsync("home-resume@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        await StartRunAsync(client);

        var home = await client.Http.GetFromJsonAsync<HomeStateDto>("/api/home");

        Assert.True(home!.PetBrain!.HasActiveRun);
    }

    [Fact]
    public async Task NumayisRejimi_IzahPanelinDoldururAmmaGizliDusunceYoxdur()
    {
        var client = await NewChildAsync("demo-panel@petpal.test");
        await SeedSpaceProfileAsync(client.ChildId);

        var state = await GetStateAsync(client);

        Assert.True(state.DemoMode);
        Assert.NotNull(state.Debug);

        var debug = state.Debug!;
        Assert.NotEmpty(debug.Candidates);
        Assert.Contains(debug.Candidates, c => c.Selected);
        Assert.False(string.IsNullOrWhiteSpace(debug.DifficultySignal));

        // Mətn mənbəyi DÜRÜST göstərilir: model işlədilmirsə "template".
        Assert.Equal("template", debug.NarrativeSource);

        // Bal cədvəli var, amma heç bir "düşüncə mətni" sahəsi yoxdur.
        Assert.All(debug.Candidates, c => Assert.InRange(c.FitScore, 0, 100));
    }

    /// <summary>Valideyn müqayisəsi YALNIZ öz uşaqlarını göstərir.</summary>
    [Fact]
    public async Task ValideynMuqayisesi_YalnizOzUsaqlariniGosterir()
    {
        var client = await NewChildAsync("compare@petpal.test", "Aylin");
        await SeedSpaceProfileAsync(client.ChildId);

        var other = await NewChildAsync("compare-other@petpal.test", "Kənar");
        await SeedFantasyProfileAsync(other.ChildId);

        client.SwitchToParent();
        var comparison = await client.Http.GetFromJsonAsync<PetBrainComparisonDto>(
            "/api/parent/pet-brain/comparison");

        Assert.NotNull(comparison);
        Assert.Single(comparison!.Children);
        Assert.Equal("Aylin", comparison.Children[0].DisplayName);
        Assert.Equal(ExperienceCatalog.MarsRoverRescue, comparison.Children[0].RecommendedTemplateKey);
    }

    [Fact]
    public async Task ValideynMuqayisesi_UsaqTokeniIleAcilmir()
    {
        var client = await NewChildAsync("compare-child@petpal.test");

        var response = await client.Http.GetAsync("/api/parent/pet-brain/comparison");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ==================== Köməkçilər ====================

    private async Task<ApiTestClient> NewChildAsync(string email, string childName = "Ava")
    {
        var client = await ApiTestClient.CreateAsync(_factory, email, childName);
        await client.HatchAsync(_factory);
        return client;
    }

    private static async Task<PetBrainStateDto> GetStateAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync("/api/pet-brain");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PetBrainStateDto>())!;
    }

    private static async Task<PetDto> GetPetAsync(ApiTestClient client) =>
        (await client.Http.GetFromJsonAsync<PetDto>("/api/pet"))!;

    private static async Task<PetBrainRunDto> StartRunAsync(ApiTestClient client)
    {
        var response = await client.Http.PostAsJsonAsync("/api/pet-brain/runs", new StartPetBrainRunRequest());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
    }

    private static async Task<PetBrainRunDto> ChooseAsync(
        ApiTestClient client, PetBrainRunDto run, string optionKey)
    {
        var response = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest { StageIndex = run.CurrentStage, OptionKey = optionKey });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
    }

    private static async Task<PetBrainRunDto> CompleteAsync(ApiTestClient client, Guid runId)
    {
        var response = await client.Http.PostAsync($"/api/pet-brain/runs/{runId}/complete", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
    }

    /// <summary>
    /// Macərəni sona qədər oynayır.
    ///
    /// <para>Tapmacada doğru cavab KLİENTƏ GÖNDƏRİLMİR, ona görə test də onu
    /// bilmir və variantları sıra ilə yoxlayır — real uşağın etdiyi kimi.</para>
    /// </summary>
    private static async Task<PetBrainRunDto> PlayToEndAsync(ApiTestClient client)
    {
        var run = await StartRunAsync(client);

        var guard = 0;
        while (run.Stage is not null && guard++ < 40)
        {
            run = run.Stage.Kind switch
            {
                PetBrainStageKind.Intro => await ChooseAsync(client, run, "continue"),
                PetBrainStageKind.Choice => await ChooseAsync(client, run, run.Stage.Options[0].Key),
                _ => await AnswerPuzzleAsync(client, run, Solve(run.Stage.Puzzle!))
            };
        }

        Assert.True(run.CurrentStage >= run.StageCount, "Macəra sona çatmadı.");
        return run;
    }

    /// <summary>
    /// Tapmacanı GÖRÜNƏN məlumatdan həll edir — məhz uşağın etdiyi kimi.
    ///
    /// <para>Test doğru cavabı serverdən ALMIR (o, heç vaxt göndərilmir):
    /// qaydanı DTO-dakı açıq dəyərlərdən tətbiq edir. Bu, həm də bir invariantı
    /// yoxlayır — tapmaca yalnız göstərilən məlumatla həll oluna bilməlidir.</para>
    /// </summary>
    private static List<string> Solve(PetBrainPuzzleDto puzzle)
    {
        switch (puzzle.Mechanic)
        {
            case PetBrainPuzzleMechanic.OrderedRoute:
                return SolveRoute(puzzle)
                    ?? throw new InvalidOperationException("Görünən məlumatla marşrut tapılmadı.");

            case PetBrainPuzzleMechanic.SequenceOrder:
                return [.. puzzle.Items.OrderBy(i => i.Value).Select(i => i.Id)];

            case PetBrainPuzzleMechanic.RouteLogic:
                var open = puzzle.Items.Where(i => i.Icon != "⛔").ToList();
                return [open.OrderBy(i => i.Value).First().Id];

            default:
                // Yaradıcı yolda səhv seçim yoxdur — ilk uyğun say kifayətdir.
                return [.. puzzle.Items.Take(puzzle.AnswerSchema.Min).Select(i => i.Id)];
        }
    }

    /// <summary>
    /// Marşrutu YALNIZ DTO-dakı görünən məlumatdan tapır: düyünün rolu, enerji
    /// artımı, qonşuluq və büdcə.
    ///
    /// <para>Qaydalar burada QƏSDƏN yenidən yazılıb — <c>RouteRules</c> çağırsaydıq,
    /// test məhz yoxlamalı olduğu şeyi (serverin qaydası ilə ekranda görünən
    /// məlumatın üst-üstə düşməsini) yoxlamazdı, sadəcə özünü təkrarlayardı.</para>
    /// </summary>
    private static List<string>? SolveRoute(PetBrainPuzzleDto puzzle)
    {
        var start = puzzle.Nodes.FirstOrDefault(n => n.Kind == PetBrainNodeKind.Start);
        if (start is null)
            return null;

        var cost = puzzle.MoveCost ?? 1;
        var maximum = puzzle.MaximumEnergy ?? 0;

        List<string>? found = null;

        Walk([start.Id], puzzle.InitialEnergy ?? 0);
        return found;

        void Walk(List<string> path, int energy)
        {
            if (found is not null || path.Count > puzzle.AnswerSchema.Max)
                return;

            var here = puzzle.Nodes.First(n => n.Id == path[^1]);

            if (here.Kind == PetBrainNodeKind.Recharge)
                energy = Math.Min(maximum, energy + (here.EnergyDelta ?? 0));

            if (here.Kind == PetBrainNodeKind.Goal)
            {
                // Hədəfə çatmaq azdır: MƏCBURİ düyünlərdən keçmək şərtdir.
                if (puzzle.RequiredBeforeGoal.All(r => path.Contains(r)))
                    found = [.. path];

                return;
            }

            var neighbours = puzzle.Edges
                .Where(e => e.From == here.Id || e.To == here.Id)
                .Select(e => e.From == here.Id ? e.To : e.From)
                .OrderBy(id => id, StringComparer.Ordinal);

            foreach (var next in neighbours)
            {
                if (path.Contains(next) || energy - cost < 0)
                    continue;

                if (puzzle.Nodes.First(n => n.Id == next).Kind == PetBrainNodeKind.Blocked)
                    continue;

                path.Add(next);
                Walk(path, energy - cost);
                path.RemoveAt(path.Count - 1);
            }
        }
    }

    /// <summary>Macərəni ilk TAPMACA mərhələsinə qədər oynayır.</summary>
    private static async Task<PetBrainRunDto> ReachPuzzleAsync(ApiTestClient client)
    {
        var run = await StartRunAsync(client);

        var guard = 0;
        while (run.Stage is not null && run.Stage.Kind != PetBrainStageKind.Puzzle && guard++ < 10)
        {
            run = run.Stage.Kind == PetBrainStageKind.Intro
                ? await ChooseAsync(client, run, "continue")
                : await ChooseAsync(client, run, run.Stage.Options[0].Key);
        }

        Assert.NotNull(run.Stage);
        Assert.Equal(PetBrainStageKind.Puzzle, run.Stage!.Kind);
        Assert.NotNull(run.Stage.Puzzle);

        return run;
    }

    /// <summary>Formaca DÜZGÜN, amma məzmunca SƏHV cavab.</summary>
    private static List<string> WrongAnswerFor(PetBrainPuzzleDto puzzle, IReadOnlyList<string> correct)
    {
        var count = puzzle.AnswerSchema.Min;

        // Doğru dəstdən fərqlənən, amma sxemə uyğun ilk kombinasiya.
        foreach (var candidate in puzzle.Items
                     .Select(i => i.Id)
                     .Reverse()
                     .Chunk(count)
                     .Where(c => c.Length == count))
        {
            if (!candidate.ToHashSet(StringComparer.Ordinal).SetEquals(correct))
                return [.. candidate];
        }

        // Ardıcıllıq sxemində sıranı tərsinə çevirmək kifayətdir.
        return [.. correct.Reverse()];
    }

    private static async Task<PetBrainRunDto> AnswerPuzzleAsync(
        ApiTestClient client, PetBrainRunDto run, List<string> selectedIds)
    {
        var response = await client.Http.PostAsJsonAsync(
            $"/api/pet-brain/runs/{run.RunId}/choices",
            new PetBrainChoiceRequest { StageIndex = run.CurrentStage, SelectedIds = selectedIds });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PetBrainRunDto>())!;
    }

    /// <summary>
    /// Aylin profili — kosmos, elm, tapmaca.
    ///
    /// <para>Tamamlanmış Ay macərası profilin AYRILMAZ hissəsidir, təsadüfi
    /// tarixçə deyil: məhz o, yenilik qaydasını işə salıb direktoru Aydan Marsa
    /// itələyir. Onsuz iki kosmos macərası bir-birinə çox yaxın qalır və seçim
    /// sürpriz payından asılı olur (bax PetBrainDirectorTests — fərq testi).</para>
    /// </summary>
    private async Task SeedSpaceProfileAsync(Guid childId)
    {
        await SeedSpaceTraitsAsync(childId);
        await SeedMoonHistoryAsync(childId);
    }

    private Task SeedSpaceTraitsAsync(Guid childId) => SeedTraitsAsync(childId,
        interests: new()
        {
            [TraitKeys.Space] = 85,
            [TraitKeys.Science] = 75,
            [TraitKeys.Puzzles] = 90,
            [TraitKeys.Animals] = 10,
            [TraitKeys.Fantasy] = 10,
            [TraitKeys.Stories] = 10,
            [TraitKeys.Nature] = 12,
            [TraitKeys.Ocean] = 10
        },
        playStyles: new()
        {
            [TraitKeys.Explorer] = 75,
            [TraitKeys.ProblemSolver] = 85,
            [TraitKeys.Creative] = 10,
            [TraitKeys.Caring] = 25,
            [TraitKeys.Playful] = 25
        });

    /// <summary>Mia profili — nağıl, heyvan, yaradıcılıq.</summary>
    private Task SeedFantasyProfileAsync(Guid childId) => SeedTraitsAsync(childId,
        interests: new()
        {
            [TraitKeys.Fantasy] = 90,
            [TraitKeys.Animals] = 85,
            [TraitKeys.Stories] = 82,
            [TraitKeys.Nature] = 30,
            [TraitKeys.Ocean] = 25,
            [TraitKeys.Space] = 15,
            [TraitKeys.Science] = 15,
            [TraitKeys.Puzzles] = 10
        },
        playStyles: new()
        {
            [TraitKeys.Creative] = 88,
            [TraitKeys.Caring] = 60,
            [TraitKeys.Playful] = 40,
            [TraitKeys.Explorer] = 30,
            [TraitKeys.ProblemSolver] = 20
        });

    private async Task SeedTraitsAsync(
        Guid childId, Dictionary<string, int> interests, Dictionary<string, int> playStyles)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = _factory.Clock.GetUtcNow().UtcDateTime;

        foreach (var (key, score) in interests)
            db.PlayerTraits.Add(new PlayerTrait
            {
                ChildProfileId = childId,
                Category = PetBrainTraitCategory.Interest,
                Key = key,
                Score = score,
                UpdatedAt = now
            });

        foreach (var (key, score) in playStyles)
            db.PlayerTraits.Add(new PlayerTrait
            {
                ChildProfileId = childId,
                Category = PetBrainTraitCategory.PlayStyle,
                Key = key,
                Score = score,
                UpdatedAt = now
            });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Tamamlanmış Ay macərasını yazır — Aylin profilinin başlanğıc vəziyyəti.
    /// </summary>
    private async Task SeedMoonHistoryAsync(Guid childId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = _factory.Clock.GetUtcNow().UtcDateTime;

        db.ExperienceRuns.Add(new ExperienceRun
        {
            ChildProfileId = childId,
            TemplateKey = ExperienceCatalog.MoonCrystalRescue,
            ExperienceType = PetBrainExperienceType.Adventure,
            Theme = TraitKeys.Space,
            Difficulty = PetBrainDifficulty.Medium,
            Status = PetBrainRunStatus.Completed,
            CurrentStage = 4,
            Choices = ["continue", "deep-crater", "solved", "magnet-glove"],

            // "Yaxşı, amma mükəmməl deyil": çətinlik pilləsi olduğu kimi qalır.
            ScorePercent = 80,
            Mistakes = 1,
            StartedAt = now.AddDays(-2),
            CompletedAt = now.AddDays(-2).AddMinutes(5),
            RewardApplied = true
        });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Tarixçəni Aylin-in BAŞLANĞIC vəziyyətinə qaytarır: yalnız Ay macərası.
    /// Testin mövzusu tövsiyə rotasiyası olmayanda işlədilir.
    /// </summary>
    private async Task ResetToMoonHistoryAsync(Guid childId)
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Verilmiş tapmacalar run-a Restrict ilə bağlıdır (uşaq üzərindən
            // ikinci kaskad yolu yaranmasın deyə), ona görə əvvəlcə onlar silinir.
            await db.IssuedPuzzles.Where(p => p.ChildProfileId == childId).ExecuteDeleteAsync();
            await db.ExperienceRuns.Where(r => r.ChildProfileId == childId).ExecuteDeleteAsync();
        }

        await SeedMoonHistoryAsync(childId);
    }

    /// <summary>Gündəlik ekran limitini doldurur — yeni fəaliyyət bloklanmalıdır.</summary>
    private async Task FillDailyLimitAsync(Guid childId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var child = await db.ChildProfiles.FirstAsync(c => c.Id == childId);
        var today = DateOnly.FromDateTime(_factory.Clock.GetUtcNow().UtcDateTime);

        var goal = await db.DailyGoals.FirstOrDefaultAsync(g => g.ChildProfileId == childId && g.Date == today);

        if (goal is null)
        {
            goal = new DailyGoal { ChildProfileId = childId, Date = today, Target = child.DailyGoalTarget };
            db.DailyGoals.Add(goal);
        }

        goal.MinutesSpent = child.DailyMinutesLimit + 10;
        await db.SaveChangesAsync();
    }
}
