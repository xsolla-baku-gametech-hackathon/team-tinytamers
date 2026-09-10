using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Dtos.Missions;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

public class WorldMissionTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public WorldMissionTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task World_ZonalariVeMissiyalariQaytarir()
    {
        var client = await NewChildAsync();

        var world = await GetWorldAsync(client);

        Assert.NotEmpty(world.Zones);

        var meadow = world.Zones.First(z => z.Code == "meadow");
        Assert.True(meadow.IsUnlocked);
        Assert.NotEmpty(meadow.Missions);
    }

    [Fact]
    public async Task World_UlduzCatismayanZonaBagliQalir()
    {
        var client = await NewChildAsync();

        var world = await GetWorldAsync(client);
        var castle = world.Zones.First(z => z.Code == "castle");

        Assert.False(castle.IsUnlocked);
        Assert.DoesNotContain(castle.Missions, m => m.Status == MissionStatus.Claimed);
    }

    [Fact]
    public async Task World_FealiyyetOlmayandaHavaFirtinalidir()
    {
        var client = await NewChildAsync();

        var world = await GetWorldAsync(client);

        Assert.Equal(WorldWeather.Storm, world.Weather);
        Assert.Equal(0, world.EngagementScore);
    }

    [Fact]
    public async Task TamamlanmamisMissiyaninMukafatiAlinmir()
    {
        var client = await NewChildAsync();
        var world = await GetWorldAsync(client);
        var mission = world.Zones.First(z => z.Code == "meadow").Missions.First();

        var response = await client.Http.PostAsync($"/api/world/missions/{mission.Id}/claim", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UcSualHellEdilende_MeadowMissiyasiTamamlanir()
    {
        var client = await NewChildAsync();

        await SolveCorrectAnswersAsync(client, 3);

        var world = await GetWorldAsync(client);
        var mission = world.Zones
            .First(z => z.Code == "meadow")
            .Missions
            .First(m => m.Code == "meadow-first-3");

        Assert.Equal(MissionStatus.Completed, mission.Status);
        Assert.Equal(3, mission.Progress);
    }

    [Fact]
    public async Task TamamlanmisMissiyaninMukafatiUlduzArtirir()
    {
        var client = await NewChildAsync();
        await SolveCorrectAnswersAsync(client, 3);

        var world = await GetWorldAsync(client);
        var mission = world.Zones
            .First(z => z.Code == "meadow")
            .Missions
            .First(m => m.Code == "meadow-first-3");

        var response = await client.Http.PostAsync($"/api/world/missions/{mission.Id}/claim", null);
        response.EnsureSuccessStatusCode();

        var claim = (await response.Content.ReadFromJsonAsync<ClaimMissionResultDto>())!;

        Assert.Equal(MissionStatus.Claimed, claim.Mission.Status);
        Assert.True(claim.Wallet.Stars >= mission.RewardStars);
    }

    [Fact]
    public async Task MukafatIkinciDefeAlinmir()
    {
        var client = await NewChildAsync();
        await SolveCorrectAnswersAsync(client, 3);

        var world = await GetWorldAsync(client);
        var mission = world.Zones
            .First(z => z.Code == "meadow")
            .Missions
            .First(m => m.Code == "meadow-first-3");

        (await client.Http.PostAsync($"/api/world/missions/{mission.Id}/claim", null)).EnsureSuccessStatusCode();
        var second = await client.Http.PostAsync($"/api/world/missions/{mission.Id}/claim", null);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task BacarigaBagliMissiyaYalnizOzBacarigindanIrelileyir()
    {
        var client = await NewChildAsync();

        // Yalnız Vocabulary sualları həll edirik; "meadow-math-5" irəliləməməlidir.
        await SolveCorrectAnswersAsync(client, 3, SkillArea.Vocabulary);

        var world = await GetWorldAsync(client);
        var mathMission = world.Zones
            .First(z => z.Code == "meadow")
            .Missions
            .First(m => m.Code == "meadow-math-5");

        Assert.Equal(0, mathMission.Progress);
    }

    // ---------- köməkçilər ----------

    private Task<ApiTestClient> NewChildAsync() =>
        ApiTestClient.CreateAsync(_factory, $"world-{Guid.NewGuid():N}@petpal.test");

    private static async Task<WorldStateDto> GetWorldAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync("/api/world");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorldStateDto>())!;
    }

    private async Task SolveCorrectAnswersAsync(ApiTestClient client, int count, SkillArea skill = SkillArea.Math)
    {
        var answered = 0;

        while (answered < count)
        {
            var startResponse = await client.Http.PostAsJsonAsync("/api/learn/sessions",
                new StartSessionRequest { Skill = skill, QuestionCount = Math.Min(count - answered, 5) });
            startResponse.EnsureSuccessStatusCode();

            var session = (await startResponse.Content.ReadFromJsonAsync<LearningSessionDto>())!;

            foreach (var question in session.Questions)
            {
                if (answered >= count)
                    break;

                var correctIndex = await GetCorrectIndexAsync(question.Id);
                var answerResponse = await client.Http.PostAsJsonAsync("/api/learn/answers", new SubmitAnswerRequest
                {
                    SessionId = session.SessionId,
                    QuestionId = question.Id,
                    ChosenIndex = correctIndex,
                    ElapsedMs = 900
                });
                answerResponse.EnsureSuccessStatusCode();
                answered++;
            }

            await client.Http.PostAsync($"/api/learn/sessions/{session.SessionId}/complete", null);
        }
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
