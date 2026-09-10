using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Shared.Dtos.Home;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Dtos.Progress;
using PetPal.Shared.Dtos.Rewards;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>Əsas oyun dövrəsi: sessiya → cavab → mükafat → pet inkişafı → gündəlik hədəf.</summary>
public class LearningFlowTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public LearningFlowTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task StartSession_AdaptivSualDestiQaytarir()
    {
        var client = await NewChildAsync();

        var session = await StartSessionAsync(client, SkillArea.Math, 5);

        Assert.Equal(SkillArea.Math, session.Skill);
        Assert.Equal(5, session.Questions.Count);
        Assert.All(session.Questions, q => Assert.NotEmpty(q.Options));
        Assert.All(session.Questions, q => Assert.Equal(SkillArea.Math, q.Skill));
    }

    [Fact]
    public async Task StartSession_BacariqSecilmeyendeEnZeifSaheSecilir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.PostAsJsonAsync("/api/learn/sessions",
            new StartSessionRequest { QuestionCount = 3 });
        response.EnsureSuccessStatusCode();

        var session = (await response.Content.ReadFromJsonAsync<LearningSessionDto>())!;

        Assert.Equal(3, session.Questions.Count);
    }

    [Fact]
    public async Task DogruCavab_UlduzVeXpQazandirir()
    {
        var client = await NewChildAsync();
        var session = await StartSessionAsync(client, SkillArea.Math, 3);

        var result = await AnswerAsync(client, session, session.Questions[0], correct: true);

        Assert.True(result.IsCorrect);
        Assert.True(result.StarsEarned > 0);
        Assert.True(result.XpEarned > 0);
        Assert.Equal(1, result.DailyGoalDone);
    }

    [Fact]
    public async Task SehvCavab_UlduzVermir_AmmaIzahQaytarir()
    {
        var client = await NewChildAsync();
        var session = await StartSessionAsync(client, SkillArea.Math, 3);

        var result = await AnswerAsync(client, session, session.Questions[0], correct: false);

        Assert.False(result.IsCorrect);
        Assert.Equal(0, result.StarsEarned);
        Assert.False(string.IsNullOrWhiteSpace(result.Explanation));
        Assert.Equal(0, result.DailyGoalDone);
    }

    [Fact]
    public async Task EyniSualaIkinciCavab409Qaytarir()
    {
        var client = await NewChildAsync();
        var session = await StartSessionAsync(client, SkillArea.Math, 3);
        var question = session.Questions[0];

        await AnswerAsync(client, session, question, correct: true);

        var second = await client.Http.PostAsJsonAsync("/api/learn/answers", new SubmitAnswerRequest
        {
            SessionId = session.SessionId,
            QuestionId = question.Id,
            ChosenIndex = 0,
            ElapsedMs = 500
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task SessiyayaAidOlmayanSualQebulEdilmir()
    {
        var client = await NewChildAsync();
        var session = await StartSessionAsync(client, SkillArea.Math, 3);

        // Başqa bacarıqdan sual götürürük — bu sessiyanın dəstində ola bilməz.
        Guid outsiderId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            outsiderId = await db.Questions
                .Where(q => q.Skill == SkillArea.Science)
                .Select(q => q.Id)
                .FirstAsync();
        }

        var response = await client.Http.PostAsJsonAsync("/api/learn/answers", new SubmitAnswerRequest
        {
            SessionId = session.SessionId,
            QuestionId = outsiderId,
            ChosenIndex = 0,
            ElapsedMs = 500
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BasqaUsaginSessiyasinaCavabVermekOlmur()
    {
        var owner = await NewChildAsync();
        var other = await NewChildAsync();
        var session = await StartSessionAsync(owner, SkillArea.Math, 3);

        var response = await other.Http.PostAsJsonAsync("/api/learn/answers", new SubmitAnswerRequest
        {
            SessionId = session.SessionId,
            QuestionId = session.Questions[0].Id,
            ChosenIndex = 0,
            ElapsedMs = 400
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TamDogruSessiya_MukemmelRaundBonusuVerir()
    {
        var client = await NewChildAsync();
        var session = await StartSessionAsync(client, SkillArea.Math, 3);

        foreach (var question in session.Questions)
            await AnswerAsync(client, session, question, correct: true);

        var summary = await CompleteAsync(client, session.SessionId);

        Assert.Equal(3, summary.CorrectCount);
        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(100, summary.AccuracyPercent);
        Assert.Contains(summary.NewBadges, b => b.Code == "perfect-round");

        var wallet = await GetAsync<WalletDto>(client, "/api/rewards/wallet");
        Assert.True(wallet.Gems >= 1);
    }

    [Fact]
    public async Task CavabsizSuallarDeqiqlikHesabinaDaxilEdilmir()
    {
        var client = await NewChildAsync();
        var session = await StartSessionAsync(client, SkillArea.Math, 5);

        // 5 sualdan yalnız 2-sinə cavab veririk.
        await AnswerAsync(client, session, session.Questions[0], correct: true);
        await AnswerAsync(client, session, session.Questions[1], correct: true);

        var summary = await CompleteAsync(client, session.SessionId);

        Assert.Equal(2, summary.TotalCount);
        Assert.Equal(100, summary.AccuracyPercent);
    }

    [Fact]
    public async Task DogruCavab_CuzdaniVeUmumiProqresiArtirir()
    {
        var client = await NewChildAsync();
        var before = await GetAsync<WalletDto>(client, "/api/rewards/wallet");

        var session = await StartSessionAsync(client, SkillArea.Math, 3);
        await AnswerAsync(client, session, session.Questions[0], correct: true);

        var after = await GetAsync<WalletDto>(client, "/api/rewards/wallet");
        var progress = await GetAsync<ProgressSummaryDto>(client, "/api/progress");

        Assert.True(after.Stars > before.Stars);
        Assert.Equal(1, progress.TotalAnswered);
        Assert.Equal(100, progress.AverageAccuracyPercent);
    }

    [Fact]
    public async Task GundelikHedefTamamlananda_BonusVeArdicilliqVerilir()
    {
        var client = await NewChildAsync();

        // Standart hədəf 5 doğru cavabdır.
        var answered = 0;
        while (answered < 5)
        {
            var session = await StartSessionAsync(client, SkillArea.Math, 5);
            foreach (var question in session.Questions)
            {
                if (answered >= 5)
                    break;

                await AnswerAsync(client, session, question, correct: true);
                answered++;
            }

            await CompleteAsync(client, session.SessionId);
        }

        var progress = await GetAsync<ProgressSummaryDto>(client, "/api/progress");

        Assert.True(progress.DailyGoal.IsReached);
        Assert.Equal(1, progress.DailyGoal.StreakDays);
        Assert.Contains(progress.Badges, b => b.Code == "first-steps" && b.IsEarned);
    }

    [Fact]
    public async Task AnaEkran_TamVeziyyetiBirSorguIleQaytarir()
    {
        var client = await NewChildAsync();

        var home = await GetAsync<HomeStateDto>(client, "/api/home");

        Assert.Equal(client.ChildId, home.ChildId);
        Assert.Equal("Max", home.Pet.Name);
        Assert.Equal(5, home.DailyGoal.Target);
        Assert.False(string.IsNullOrWhiteSpace(home.PetMessage));
        Assert.NotEmpty(home.FeaturedMissions);
    }

    // ---------- köməkçilər ----------

    private Task<ApiTestClient> NewChildAsync() =>
        ApiTestClient.CreateAsync(_factory, $"learn-{Guid.NewGuid():N}@petpal.test");

    private static async Task<LearningSessionDto> StartSessionAsync(ApiTestClient client, SkillArea skill, int count)
    {
        var response = await client.Http.PostAsJsonAsync("/api/learn/sessions",
            new StartSessionRequest { Skill = skill, QuestionCount = count });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<LearningSessionDto>())!;
    }

    private async Task<AnswerResultDto> AnswerAsync(
        ApiTestClient client, LearningSessionDto session, QuestionDto question, bool correct)
    {
        var correctIndex = await GetCorrectIndexAsync(question.Id);
        var chosen = correct
            ? correctIndex
            : Enumerable.Range(0, question.Options.Count).First(i => i != correctIndex);

        var response = await client.Http.PostAsJsonAsync("/api/learn/answers", new SubmitAnswerRequest
        {
            SessionId = session.SessionId,
            QuestionId = question.Id,
            ChosenIndex = chosen,
            ElapsedMs = 1200
        });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AnswerResultDto>())!;
    }

    private static async Task<SessionSummaryDto> CompleteAsync(ApiTestClient client, Guid sessionId)
    {
        var response = await client.Http.PostAsync($"/api/learn/sessions/{sessionId}/complete", null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<SessionSummaryDto>())!;
    }

    /// <summary>
    /// Bankdan çıxarılan sual mövcud bazalarda da passivləşməlidir: seed yalnız
    /// boş banka işlədiyinə görə sətri silmək kifayət etmir.
    /// </summary>
    [Fact]
    public async Task LegvEdilmisSual_MovcudBazadaDaPassivlesir()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const string retired = "Həşəratın neçə ayağı var?";

        db.Questions.Add(new PetPal.Api.Entities.Question
        {
            Skill = SkillArea.Science,
            LanguageCode = "az",
            Difficulty = 3,
            Prompt = retired,
            Options = ["4", "6", "8", "10"],
            CorrectIndex = 1,
            IsActive = true
        });
        await db.SaveChangesAsync();

        await DbInitializer.SeedAsync(scope.ServiceProvider);

        var stillActive = await db.Questions
            .AsNoTracking()
            .AnyAsync(q => q.IsActive && q.Prompt == retired);

        Assert.False(stillActive);
    }

    private static async Task<T> GetAsync<T>(ApiTestClient client, string url)
    {
        var response = await client.Http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    /// <summary>Doğru cavab API-də gizlidir — test onu birbaşa bazadan oxuyur.</summary>
    private async Task<int> GetCorrectIndexAsync(Guid questionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Questions
            .Where(q => q.Id == questionId)
            .Select(q => q.CorrectIndex)
            .FirstAsync();
    }
}
