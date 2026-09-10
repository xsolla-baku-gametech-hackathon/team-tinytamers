using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Progress;
using PetPal.Shared.Dtos.Home;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>Qaydaların saf hissəsi — vaxt/DB olmadan yoxlanılır.</summary>
public class ScreenTimeGuardTests
{
    [Theory]
    [InlineData(22, 21, 7, true)]   // gecə yarısından əvvəl
    [InlineData(3, 21, 7, true)]    // gecə yarısından sonra
    [InlineData(12, 21, 7, false)]  // gündüz
    [InlineData(7, 21, 7, false)]   // sərhəd: bitiş saatı daxil deyil
    [InlineData(21, 21, 7, true)]   // sərhəd: başlanğıc saatı daxildir
    public void IsWithinBedtime_GeceYarisindanKecenPencereniDuzgunHesablayir(
        int hour, int start, int end, bool expected)
    {
        Assert.Equal(expected, ScreenTimeGuard.IsWithinBedtime(hour, start, end));
    }

    /// <summary>
    /// Mühafizə söndürüləndə qaydalar heç hesablanmır — nə yuxu rejimi,
    /// nə də limit uşağı bloklaya bilər.
    /// </summary>
    [Fact]
    public void Evaluate_SondurulendeHemiseIcazeVerir()
    {
        var child = new ChildProfile { DailyMinutesLimit = 30, BedtimeStartHour = 21, BedtimeEndHour = 7 };
        var goal = new DailyGoal { MinutesSpent = 999 };
        var midnight = new DateTime(2026, 1, 1, 23, 30, 0, DateTimeKind.Utc);

        Assert.Equal(ScreenTimeState.Bedtime, ScreenTimeGuard.Evaluate(child, goal, midnight));
        Assert.Equal(ScreenTimeState.Allowed, ScreenTimeGuard.Evaluate(child, goal, midnight, enforced: false));
    }

    [Fact]
    public void IsWithinBedtime_EyniSaatlarPencereniSondurur()
    {
        Assert.False(ScreenTimeGuard.IsWithinBedtime(10, 0, 0));
    }

    [Fact]
    public void Evaluate_LimitDolandaBloklayir()
    {
        var child = new ChildProfile { DailyMinutesLimit = 30, BedtimeStartHour = 21, BedtimeEndHour = 7 };
        var goal = new DailyGoal { MinutesSpent = 30 };

        var state = ScreenTimeGuard.Evaluate(child, goal, new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ScreenTimeState.LimitReached, state);
    }

    [Fact]
    public void Evaluate_LimitAltindaIcazeVerir()
    {
        var child = new ChildProfile { DailyMinutesLimit = 30, BedtimeStartHour = 21, BedtimeEndHour = 7 };
        var goal = new DailyGoal { MinutesSpent = 29 };

        var state = ScreenTimeGuard.Evaluate(child, goal, new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ScreenTimeState.Allowed, state);
    }

    [Fact]
    public void Evaluate_YuxuRejimiLimitdenUstundur()
    {
        var child = new ChildProfile { DailyMinutesLimit = 30, BedtimeStartHour = 21, BedtimeEndHour = 7 };
        var goal = new DailyGoal { MinutesSpent = 100 };

        var state = ScreenTimeGuard.Evaluate(child, goal, new DateTime(2026, 8, 4, 22, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ScreenTimeState.Bedtime, state);
    }

    [Fact]
    public void Evaluate_YerliSaatQursagiNezereAlinir()
    {
        // UTC 18:00, +4 saat qurşağında yerli saat 22:00 → yuxu rejimi.
        var child = new ChildProfile
        {
            DailyMinutesLimit = 30,
            BedtimeStartHour = 21,
            BedtimeEndHour = 7,
            UtcOffsetMinutes = 240
        };

        var state = ScreenTimeGuard.Evaluate(child, new DailyGoal(), new DateTime(2026, 8, 4, 18, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ScreenTimeState.Bedtime, state);
    }

    [Fact]
    public void Evaluate_LimitSifirdirsaHecVaxtBloklamir()
    {
        var child = new ChildProfile { DailyMinutesLimit = 0, BedtimeStartHour = 0, BedtimeEndHour = 0 };
        var goal = new DailyGoal { MinutesSpent = 500 };

        var state = ScreenTimeGuard.Evaluate(child, goal, new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(ScreenTimeState.Allowed, state);
    }
}

/// <summary>Qaydanın API səviyyəsində həqiqətən tətbiq olunduğunu yoxlayır.</summary>
public class ScreenTimeEnforcementTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ScreenTimeEnforcementTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task LimitDolandaYeniSessiyaBaslamir()
    {
        var client = await NewChildAsync();
        await SetMinutesSpentAsync(client, 999);

        var response = await client.Http.PostAsJsonAsync("/api/learn/sessions",
            new StartSessionRequest { Skill = SkillArea.Math, QuestionCount = 3 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LimitDolandaOyunNeticesiQebulEdilmir()
    {
        var client = await NewChildAsync();
        await SetMinutesSpentAsync(client, 999);

        var response = await client.Http.PostAsJsonAsync("/api/games/results",
            new PetPal.Shared.Dtos.Games.SubmitGameResultRequest
            {
                GameKey = "memory-match",
                Score = 90,
                DurationMs = 40_000
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnaEkranBloklanmaVeziyyetiniQaytarir()
    {
        var client = await NewChildAsync();
        await SetMinutesSpentAsync(client, 999);

        var response = await client.Http.GetAsync("/api/home");
        response.EnsureSuccessStatusCode();

        var home = (await response.Content.ReadFromJsonAsync<HomeStateDto>())!;

        Assert.True(home.ScreenTime.IsBlocked);
        Assert.Equal(ScreenTimeState.LimitReached, home.ScreenTime.State);
        Assert.False(string.IsNullOrWhiteSpace(home.ScreenTime.Message));
    }

    [Fact]
    public async Task NormalVeziyyetdeBloklanmaYoxdur()
    {
        var client = await NewChildAsync();

        var response = await client.Http.GetAsync("/api/home");
        response.EnsureSuccessStatusCode();

        var home = (await response.Content.ReadFromJsonAsync<HomeStateDto>())!;

        Assert.False(home.ScreenTime.IsBlocked);
        Assert.Equal(30, home.ScreenTime.MinutesLimit);
    }

    [Fact]
    public async Task BaslanmisSessiyaLimitDolandaDaTamamlanaBilir()
    {
        var client = await NewChildAsync();

        var start = await client.Http.PostAsJsonAsync("/api/learn/sessions",
            new StartSessionRequest { Skill = SkillArea.Math, QuestionCount = 3 });
        start.EnsureSuccessStatusCode();
        var session = (await start.Content.ReadFromJsonAsync<LearningSessionDto>())!;

        await SetMinutesSpentAsync(client, 999);

        // Qazanılmış işi yarımçıq kəsmək uşaq üçün ədalətsiz olardı.
        var complete = await client.Http.PostAsync($"/api/learn/sessions/{session.SessionId}/complete", null);

        complete.EnsureSuccessStatusCode();
    }

    private Task<ApiTestClient> NewChildAsync() =>
        ApiTestClient.CreateAsync(_factory, $"screen-{Guid.NewGuid():N}@petpal.test");

    private async Task SetMinutesSpentAsync(ApiTestClient client, int minutes)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateOnly.FromDateTime(_factory.Clock.GetUtcNow().UtcDateTime);
        var goal = await db.DailyGoals.FirstOrDefaultAsync(g => g.ChildProfileId == client.ChildId && g.Date == today);

        if (goal is null)
        {
            goal = new DailyGoal { ChildProfileId = client.ChildId, Date = today, Target = 5 };
            db.DailyGoals.Add(goal);
        }

        goal.MinutesSpent = minutes;
        await db.SaveChangesAsync();
    }
}
