using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.Games;
using PetPal.Shared.Dtos.Games;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Dtos.Rewards;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

public class GameCatalogTests
{
    [Fact]
    public void Reward_YuksekNeticeDahaCoxQazandirir()
    {
        var (lowStars, lowXp) = GameCatalog.Reward(20);
        var (highStars, highXp) = GameCatalog.Reward(100);

        Assert.True(highStars > lowStars);
        Assert.True(highXp > lowXp);
    }

    [Fact]
    public void Reward_OyrenmeTapsiriginfanAzMukafatVerir()
    {
        var (stars, _) = GameCatalog.Reward(100);

        // Ən yüksək oyun nəticəsi belə ən asan sualdan (11 ulduz) az gətirməlidir.
        Assert.True(stars < 11);
    }

    /// <summary>
    /// XP əyrisi sürətlənməlidir: səhvsiz oyun "yarısını bildim" ilə demək olar
    /// eyni mükafat versəydi, diqqətli oynamağın mənası qalmazdı.
    /// </summary>
    [Fact]
    public void Reward_SehvsizOyunNezerecarpanXpVerir()
    {
        var (_, half) = GameCatalog.Reward(50);
        var (_, good) = GameCatalog.Reward(80);
        var (_, flawless) = GameCatalog.Reward(100);

        Assert.True(good > half + 3, $"80 bal ({good} XP) 50 baldan ({half} XP) nəzərəçarpacaq çox olmalıdır.");
        Assert.True(flawless >= half * 3, $"Səhvsiz oyun ({flawless} XP) yarısından üç dəfə çox verməlidir.");
    }

    [Fact]
    public void Reward_AraliqdanKenarNeticeSixilir()
    {
        var (negative, _) = GameCatalog.Reward(-50);
        var (over, _) = GameCatalog.Reward(500);
        var (max, _) = GameCatalog.Reward(100);

        Assert.Equal(0, negative);
        Assert.Equal(max, over);
    }

    [Fact]
    public void For_HerIkiDildeKataloqQaytarir()
    {
        var az = GameCatalog.For("az", []);
        var en = GameCatalog.For("en", []);

        Assert.Equal(GameCatalog.Keys.Length, az.Count);
        Assert.Equal(az.Count, en.Count);
        Assert.NotEqual(az[0].Title, en[0].Title);
    }

    [Fact]
    public void For_PulsuzOyunlarEvvelcedenAcigdir()
    {
        var catalog = GameCatalog.For("az", []);

        Assert.True(catalog.Single(g => g.Key == GameCatalog.MemoryMatch).IsUnlocked);
        Assert.True(catalog.Single(g => g.Key == GameCatalog.QuickTap).IsUnlocked);
        Assert.False(catalog.Single(g => g.Key == GameCatalog.BubblePop).IsUnlocked);
        Assert.False(catalog.Single(g => g.Key == GameCatalog.ColorEcho).IsUnlocked);
    }

    [Fact]
    public void For_AcilmisOyunKataloqdaAcigGorunur()
    {
        var catalog = GameCatalog.For("az", [GameCatalog.BubblePop]);

        var bubble = catalog.Single(g => g.Key == GameCatalog.BubblePop);
        Assert.True(bubble.IsUnlocked);
        Assert.Equal(60, bubble.UnlockStarCost);
    }

    [Fact]
    public void UnlockCost_PulsuzOyunlarUcunSifirdir()
    {
        Assert.Equal(0, GameCatalog.UnlockCost(GameCatalog.MemoryMatch));
        Assert.Equal(0, GameCatalog.UnlockCost(GameCatalog.QuickTap));
        Assert.True(GameCatalog.UnlockCost(GameCatalog.ColorEcho) > GameCatalog.UnlockCost(GameCatalog.BubblePop));
    }

    [Fact]
    public void IsKnown_YalnizKataloqdakiAcarlariQebulEdir()
    {
        Assert.True(GameCatalog.IsKnown(GameCatalog.MemoryMatch));
        Assert.True(GameCatalog.IsKnown(GameCatalog.QuickTap));
        Assert.False(GameCatalog.IsKnown("uydurma-oyun"));
    }
}

public class GameApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public GameApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Unlock_UlduzCatismayandaImtinaEdir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.PostAsJsonAsync("/api/games/unlock",
            new UnlockGameRequest { GameKey = GameCatalog.BubblePop });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unlock_PulsuzOyunuAcmagaCalismaqImtinaEdilir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.PostAsJsonAsync("/api/games/unlock",
            new UnlockGameRequest { GameKey = GameCatalog.QuickTap });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Kataloq_UsagunDilindeQaytarilir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.GetAsync("/api/games");
        response.EnsureSuccessStatusCode();

        var games = (await response.Content.ReadFromJsonAsync<List<GameCatalogItemDto>>())!;

        Assert.Equal(GameCatalog.Keys.Length, games.Count);
        Assert.Contains(games, g => g.Key == GameCatalog.MemoryMatch);
        // Default dil azərbaycancadır.
        Assert.Contains("Yaddaş", games.First(g => g.Key == GameCatalog.MemoryMatch).Title);

        // Yeni profil yalnız pulsuz oyunlarla başlayır.
        Assert.Equal(2, games.Count(g => g.IsUnlocked));
    }

    [Fact]
    public async Task Netice_UlduzVeXpQazandirir()
    {
        var client = await NewChildAsync();

        var result = await PlayAsync(client, GameCatalog.MemoryMatch, 100);

        Assert.True(result.StarsEarned > 0);
        Assert.True(result.XpEarned > 0);
        Assert.False(result.RewardCapped);
    }

    [Fact]
    public async Task Netice_PetinSevincinArtirir()
    {
        var client = await NewChildAsync();

        var before = await GetPetHappinessAsync(client);
        var result = await PlayAsync(client, GameCatalog.QuickTap, 80);

        Assert.True(result.Pet.Happiness > before);
    }

    [Fact]
    public async Task GundelikLimitdenSonraUlduzVerilmir()
    {
        var client = await NewChildAsync();

        for (var i = 0; i < GameCatalog.RewardedGamesPerDay; i++)
            await PlayAsync(client, GameCatalog.MemoryMatch, 100);

        var capped = await PlayAsync(client, GameCatalog.MemoryMatch, 100);

        Assert.True(capped.RewardCapped);
        Assert.Equal(0, capped.StarsEarned);
    }

    [Fact]
    public async Task NamelumOyunQebulEdilmir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.PostAsJsonAsync("/api/games/results",
            new SubmitGameResultRequest { GameKey = "uydurma", Score = 50, DurationMs = 1000 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AraliqdanKenarNeticeQebulEdilmir()
    {
        var client = await NewChildAsync();

        var response = await client.Http.PostAsJsonAsync("/api/games/results",
            new SubmitGameResultRequest { GameKey = GameCatalog.MemoryMatch, Score = 500, DurationMs = 1000 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<ApiTestClient> NewChildAsync() =>
        ApiTestClient.CreateAsync(_factory, $"game-{Guid.NewGuid():N}@petpal.test");

    private static async Task<GameResultDto> PlayAsync(ApiTestClient client, string gameKey, int score)
    {
        var response = await client.Http.PostAsJsonAsync("/api/games/results",
            new SubmitGameResultRequest { GameKey = gameKey, Score = score, DurationMs = 45_000 });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<GameResultDto>())!;
    }

    private static async Task<int> GetPetHappinessAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync("/api/pet");
        response.EnsureSuccessStatusCode();
        var pet = (await response.Content.ReadFromJsonAsync<PetPal.Shared.Dtos.Pets.PetDto>())!;
        return pet.Happiness;
    }
}

public class SprintTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public SprintTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Sprint_UcSualQaytarir()
    {
        var client = await NewChildAsync();

        // QuestionCount 5 göndərilsə də sprint həmişə 3 sualdır.
        var session = await StartSprintAsync(client, requestedCount: 5);

        Assert.True(session.IsSprint);
        Assert.Equal(3, session.Questions.Count);
    }

    [Fact]
    public async Task Sprint_TamDogruTamamlanandaBonusVerir()
    {
        var client = await NewChildAsync();
        var session = await StartSprintAsync(client);

        var before = await GetWalletAsync(client);

        foreach (var question in session.Questions)
            await AnswerCorrectlyAsync(client, session.SessionId, question.Id);

        var summary = await CompleteAsync(client, session.SessionId);
        var after = await GetWalletAsync(client);

        Assert.True(summary.SprintBonusStars > 0);
        Assert.True(after.Stars - before.Stars >= summary.SprintBonusStars);
    }

    [Fact]
    public async Task Sprint_SehvCavabVarsaBonusVerilmir()
    {
        var client = await NewChildAsync();
        var session = await StartSprintAsync(client);

        await AnswerCorrectlyAsync(client, session.SessionId, session.Questions[0].Id);
        await AnswerWronglyAsync(client, session.SessionId, session.Questions[1].Id);
        await AnswerCorrectlyAsync(client, session.SessionId, session.Questions[2].Id);

        var summary = await CompleteAsync(client, session.SessionId);

        Assert.Equal(0, summary.SprintBonusStars);
    }

    [Fact]
    public async Task AdiSessiyaSprintBonusuAlmir()
    {
        var client = await NewChildAsync();

        var start = await client.Http.PostAsJsonAsync("/api/learn/sessions",
            new StartSessionRequest { Skill = SkillArea.Math, QuestionCount = 3 });
        start.EnsureSuccessStatusCode();
        var session = (await start.Content.ReadFromJsonAsync<LearningSessionDto>())!;

        Assert.False(session.IsSprint);

        foreach (var question in session.Questions)
            await AnswerCorrectlyAsync(client, session.SessionId, question.Id);

        var summary = await CompleteAsync(client, session.SessionId);

        Assert.Equal(0, summary.SprintBonusStars);
    }

    private Task<ApiTestClient> NewChildAsync() =>
        ApiTestClient.CreateAsync(_factory, $"sprint-{Guid.NewGuid():N}@petpal.test");

    private static async Task<LearningSessionDto> StartSprintAsync(ApiTestClient client, int requestedCount = 5)
    {
        var response = await client.Http.PostAsJsonAsync("/api/learn/sessions",
            new StartSessionRequest { Skill = SkillArea.Math, QuestionCount = requestedCount, IsSprint = true });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<LearningSessionDto>())!;
    }

    private async Task AnswerCorrectlyAsync(ApiTestClient client, Guid sessionId, Guid questionId) =>
        await SubmitAsync(client, sessionId, questionId, await GetCorrectIndexAsync(questionId));

    private async Task AnswerWronglyAsync(ApiTestClient client, Guid sessionId, Guid questionId)
    {
        var correct = await GetCorrectIndexAsync(questionId);
        await SubmitAsync(client, sessionId, questionId, correct == 0 ? 1 : 0);
    }

    private static async Task SubmitAsync(ApiTestClient client, Guid sessionId, Guid questionId, int chosenIndex)
    {
        var response = await client.Http.PostAsJsonAsync("/api/learn/answers", new SubmitAnswerRequest
        {
            SessionId = sessionId,
            QuestionId = questionId,
            ChosenIndex = chosenIndex,
            ElapsedMs = 900
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<SessionSummaryDto> CompleteAsync(ApiTestClient client, Guid sessionId)
    {
        var response = await client.Http.PostAsync($"/api/learn/sessions/{sessionId}/complete", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionSummaryDto>())!;
    }

    private static async Task<WalletDto> GetWalletAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync("/api/rewards/wallet");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WalletDto>())!;
    }

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
