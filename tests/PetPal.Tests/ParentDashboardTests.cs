using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Shared.Dtos.Discovery;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Dtos.Parent;
using PetPal.Shared.Dtos.Progress;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

public class ParentDashboardTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ParentDashboardTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Dashboard_YeddiGunlukTrendQaytarir()
    {
        var client = await NewChildAsync();
        client.SwitchToParent();

        var dashboard = await GetDashboardAsync(client);

        Assert.Equal(7, dashboard.Last7Days.Count);
        Assert.Equal(client.ChildId, dashboard.ChildId);
        Assert.NotEmpty(dashboard.Skills);
        Assert.NotEmpty(dashboard.Insights);
    }

    [Fact]
    public async Task Dashboard_HellEdilmisTapsiriqlariGosterir()
    {
        var client = await NewChildAsync();
        await SolveAsync(client, 2);

        client.SwitchToParent();
        var dashboard = await GetDashboardAsync(client);

        Assert.Equal(2, dashboard.TasksDoneToday);
        Assert.Equal(100, dashboard.AccuracyPercentToday);
    }

    [Fact]
    public async Task Dashboard_BesBacarigiDaHemiseGosterir()
    {
        var client = await NewChildAsync();
        client.SwitchToParent();

        var dashboard = await GetDashboardAsync(client);

        Assert.Equal(Enum.GetValues<SkillArea>().Length, dashboard.Skills.Count);
        Assert.Equal(2, dashboard.Skills.Count(s => s.IsFocusArea));
    }

    [Fact]
    public async Task ScreenTime_YenilenirVeGundelikHedefeTetbiqOlunur()
    {
        var client = await NewChildAsync();
        client.SwitchToParent();

        var settings = new ScreenTimeSettingsDto
        {
            DailyGoalTarget = 8,
            DailyMinutesLimit = 45,
            BedtimeStartHour = 20,
            BedtimeEndHour = 8
        };

        var response = await client.Http.PutAsJsonAsync(
            $"/api/parent/children/{client.ChildId}/screen-time", settings);
        response.EnsureSuccessStatusCode();

        var dashboard = await GetDashboardAsync(client);
        Assert.Equal(8, dashboard.ScreenTime.DailyGoalTarget);
        Assert.Equal(45, dashboard.ScreenTime.DailyMinutesLimit);

        client.SwitchToChild();
        var progressResponse = await client.Http.GetAsync("/api/progress");
        progressResponse.EnsureSuccessStatusCode();
        var progress = (await progressResponse.Content.ReadFromJsonAsync<ProgressSummaryDto>())!;

        Assert.Equal(8, progress.DailyGoal.Target);
    }

    [Fact]
    public async Task ScreenTime_AraliqdanKenarDeyerQebulEdilmir()
    {
        var client = await NewChildAsync();
        client.SwitchToParent();

        var response = await client.Http.PutAsJsonAsync(
            $"/api/parent/children/{client.ChildId}/screen-time",
            new ScreenTimeSettingsDto { DailyGoalTarget = 999, DailyMinutesLimit = 45 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ParentChildren_YalnizOzUsaqlariniQaytarir()
    {
        var first = await NewChildAsync();
        await NewChildAsync();

        first.SwitchToParent();
        var response = await first.Http.GetAsync("/api/parent/children");
        response.EnsureSuccessStatusCode();

        var children = (await response.Content.ReadFromJsonAsync<List<PetPal.Shared.Dtos.Auth.ChildSummaryDto>>())!;

        Assert.Single(children);
        Assert.Equal(first.ChildId, children[0].Id);
    }

    /// <summary>
    /// Kolleksiyanı uşaq da, valideyn də görməlidir. Valideyn tərəfi uzun müddət
    /// yox idi: panel yalnız rəqəm göstərirdi, uşağın ekrandan kənarda nə
    /// tapdığı isə heç yerdə görünmürdü.
    /// </summary>
    [Fact]
    public async Task Kesfler_ValideynPanelindeGorunur()
    {
        var client = await NewChildAsync();
        var photo = new byte[] { 0xFF, 0xD8, 0xFF, 0, 0, 0, 0, 0 };

        var created = await client.Http.PostAsJsonAsync("/api/discoveries", new DiscoveryRequest
        {
            Label = "Pine cone",
            PhotoBase64 = Convert.ToBase64String(photo)
        });
        created.EnsureSuccessStatusCode();
        var discovery = (await created.Content.ReadFromJsonAsync<DiscoveryResultDto>())!;

        client.SwitchToParent();

        var list = (await client.Http.GetFromJsonAsync<List<DiscoveryDto>>(
            $"/api/parent/children/{client.ChildId}/discoveries"))!;

        Assert.Single(list);
        Assert.Equal("Pine cone", list[0].Label);
        Assert.True(list[0].HasPhoto);

        var photoResponse = await client.Http.GetAsync(
            $"/api/parent/children/{client.ChildId}/discoveries/{discovery.Id}/photo");
        photoResponse.EnsureSuccessStatusCode();

        Assert.Equal(photo, await photoResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Kesfler_YadUsaqUcunQaytarilmir()
    {
        var owner = await NewChildAsync();
        var stranger = await NewChildAsync();

        stranger.SwitchToParent();
        var response = await stranger.Http.GetAsync($"/api/parent/children/{owner.ChildId}/discoveries");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Kesfler_UsaqTokeniIleValideynYolunaGirmekOlmur()
    {
        var client = await NewChildAsync();

        var response = await client.Http.GetAsync($"/api/parent/children/{client.ChildId}/discoveries");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- köməkçilər ----------

    private Task<ApiTestClient> NewChildAsync() =>
        ApiTestClient.CreateAsync(_factory, $"parent-{Guid.NewGuid():N}@petpal.test");

    private static async Task<ParentDashboardDto> GetDashboardAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync($"/api/parent/children/{client.ChildId}/dashboard");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ParentDashboardDto>())!;
    }

    private async Task SolveAsync(ApiTestClient client, int count)
    {
        var startResponse = await client.Http.PostAsJsonAsync("/api/learn/sessions",
            new StartSessionRequest { Skill = SkillArea.Math, QuestionCount = count });
        startResponse.EnsureSuccessStatusCode();

        var session = (await startResponse.Content.ReadFromJsonAsync<LearningSessionDto>())!;

        foreach (var question in session.Questions)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var correctIndex = await db.Questions
                .Where(q => q.Id == question.Id)
                .Select(q => q.CorrectIndex)
                .FirstAsync();

            (await client.Http.PostAsJsonAsync("/api/learn/answers", new SubmitAnswerRequest
            {
                SessionId = session.SessionId,
                QuestionId = question.Id,
                ChosenIndex = correctIndex,
                ElapsedMs = 800
            })).EnsureSuccessStatusCode();
        }
    }
}
