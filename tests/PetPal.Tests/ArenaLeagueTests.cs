using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.Learning.Arena;
using PetPal.Api.Rewards;
using PetPal.Shared.Dtos.Learning;

namespace PetPal.Tests;

// ===================== Liqanın saf qaydaları =====================

/// <summary>
/// Həftəlik liqa ayrıca cədvəldə saxlanılmır — sərhəd və xal qaydaları saf
/// funksiyalardır, ona görə birbaşa yoxlanılır.
/// </summary>
public class ArenaLeagueRulesTests
{
    [Theory]
    // Bazar ertəsi özü — həmin gün başlanğıcdır.
    [InlineData("2026-08-17T09:00:00Z", "2026-08-17")]
    // Çərşənbə axşamı, cümə, bazar — hamısı eyni bazar ertəsinə düşür.
    [InlineData("2026-08-18T23:59:00Z", "2026-08-17")]
    [InlineData("2026-08-21T12:00:00Z", "2026-08-17")]
    [InlineData("2026-08-23T23:00:00Z", "2026-08-17")]
    // Növbəti bazar ertəsi — yeni həftə.
    [InlineData("2026-08-24T00:00:00Z", "2026-08-24")]
    public void HefteninBaslangici_BazarErtesidir(string utcNow, string expected)
    {
        var now = DateTime.Parse(utcNow, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        Assert.Equal(DateTime.Parse(expected), ArenaLeague.WeekStart(now));
    }

    [Fact]
    public void HefteninSonu_YeddiGunSonradir()
    {
        var now = new DateTime(2026, 8, 21, 15, 0, 0, DateTimeKind.Utc);

        Assert.Equal(ArenaLeague.WeekStart(now).AddDays(7), ArenaLeague.WeekEnd(now));
    }

    [Fact]
    public void Xal_QalibiyyetUc_BeraberlikBirdir()
    {
        Assert.Equal(0, ArenaLeague.Points(0, 0));
        Assert.Equal(3, ArenaLeague.Points(1, 0));
        Assert.Equal(7, ArenaLeague.Points(2, 1));
    }

    [Fact]
    public void LiqaNisani_AzDuelleVerilmir()
    {
        // Bir duelli "çempion" olmasın deyə həftədə ən azı üç duel tələb olunur.
        var few = new BadgeStats { ArenaLeagueRank = 1, ArenaWeeklyDuels = 2 };
        var enough = new BadgeStats { ArenaLeagueRank = 1, ArenaWeeklyDuels = 3 };
        var fourth = new BadgeStats { ArenaLeagueRank = 4, ArenaWeeklyDuels = 9 };

        Assert.DoesNotContain("arena-league-top3", BadgeRules.Evaluate(few));
        Assert.Contains("arena-league-top3", BadgeRules.Evaluate(enough));
        Assert.DoesNotContain("arena-league-top3", BadgeRules.Evaluate(fourth));
    }
}

// ===================== Liqa cədvəli və real vaxt =====================

/// <summary>
/// Faza 3 (liqa) və Faza 4 (real vaxt kanalı) uçdan-uca: cədvəl duel
/// qeydlərindən yığılır, xəbərlər isə SignalR ilə rəqibə çatır.
/// </summary>
public class ArenaLeagueTests : IClassFixture<TestWebAppFactory>
{
    /// <summary>Serverin geri sayımı (Arena:CountdownSeconds) — testdə saat bu qədər irəli sürülür.</summary>
    private const int CountdownSeconds = 3;

    private readonly TestWebAppFactory _factory;

    public ArenaLeagueTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Liqa_DuelBitendeCedveleDusur()
    {
        await ClearDuelsAsync();
        var winner = await NewChildAsync("Ayan");
        var loser = await NewChildAsync("Nihat");

        await PlayDuelAsync(loser, winner);

        var league = await GetLeagueAsync(winner);

        Assert.Equal(2, league.Rows.Count);
        Assert.Equal(1, league.MyRank);
        Assert.Equal(ArenaLeague.WinPoints, league.MyPoints);

        var top = league.Rows[0];
        Assert.True(top.IsMe);
        Assert.Equal(1, top.Wins);
        Assert.Equal(ArenaLeague.WinPoints, top.Points);

        var second = league.Rows[1];
        Assert.Equal(1, second.Losses);
        Assert.Equal(0, second.Points);
    }

    [Fact]
    public async Task Liqa_UmumiSiyahidaEslAdGorunmur()
    {
        await ClearDuelsAsync();
        var me = await NewChildAsync("Ayan");
        var rival = await NewChildAsync("Nihat");

        await PlayDuelAsync(rival, me);

        var league = await GetLeagueAsync(me);
        var mine = league.Rows.Single(r => r.IsMe);
        var other = league.Rows.Single(r => !r.IsMe);

        // Öz sətrində uşaq öz adını görür...
        Assert.Equal("Ayan", mine.DisplayName);

        // ...tanımadığı rəqib isə yalnız pet adı ilə qalır.
        Assert.Null(other.DisplayName);
        Assert.False(other.IsFriend);
        Assert.Equal("Max", other.PetName);
    }

    [Fact]
    public async Task Liqa_MesqDuelleriniSaymir()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync("Solo");

        var practice = await StartAsync(child, "/api/learn/arena/practice");
        await PlayAsync(child, practice, correct: true);

        var league = await GetLeagueAsync(child);

        Assert.Empty(league.Rows);
        Assert.Equal(0, league.MyRank);
    }

    [Fact]
    public async Task RealVaxt_ReqibinGedisiVeNeticesiCatir()
    {
        await ClearDuelsAsync();
        var waiting = await NewChildAsync("Ayan");
        var rival = await NewChildAsync("Nihat");

        var duel = await StartAsync(waiting, "/api/learn/arena/duels");

        var matched = new TaskCompletionSource<Guid>();
        var progress = new TaskCompletionSource<(Guid Duel, int Answered, int Total)>();
        var completed = new TaskCompletionSource<Guid>();

        await using var live = BuildConnection(waiting.Child.AccessToken);
        live.On<Guid>("DuelMatched", id => matched.TrySetResult(id));
        live.On<Guid, int, int>("OpponentProgress", (id, answered, total) => progress.TrySetResult((id, answered, total)));
        live.On<Guid>("DuelCompleted", id => completed.TrySetResult(id));

        await live.StartAsync();

        // 1) Rəqib qoşulur → gözləyən uşağa xəbər gedir. Sinxron yarışda bu, ən
        //    vacib xəbərdir: axtarış ekranı məhz onunla bağlanır.
        var joined = await StartAsync(rival, "/api/learn/arena/duels");
        Assert.Equal(duel.DuelId, joined.DuelId);
        Assert.Equal(duel.DuelId, await Wait(matched.Task));

        // Geri sayım bitməyincə server cavab qəbul etmir.
        _factory.Clock.Advance(TimeSpan.FromSeconds(CountdownSeconds));

        await PlayAsync(waiting, duel, correct: false);

        // 2) Rəqibin ilk cavabı → canlı gediş xəbəri.
        await AnswerAsync(rival, joined, joined.Questions[0], correct: true);
        var step = await Wait(progress.Task);
        Assert.Equal(duel.DuelId, step.Duel);
        Assert.Equal(1, step.Answered);
        Assert.Equal(5, step.Total);

        // 3) Rəqib dəsti bitirir → nəticə hazırdır xəbəri.
        foreach (var question in joined.Questions.Skip(1))
            await AnswerAsync(rival, joined, question, correct: true);

        Assert.Equal(duel.DuelId, await Wait(completed.Task));
    }

    [Fact]
    public async Task RealVaxt_TokensizQosulmaqOlmur()
    {
        await using var live = BuildConnection(accessToken: null);

        await Assert.ThrowsAnyAsync<Exception>(() => live.StartAsync());
    }

    // ---------- Köməkçilər ----------

    /// <summary>
    /// Test serveri üçün SignalR bağlantısı: nəqliyyat uzun sorğudur (TestServer
    /// üçün ən sadə yol), token isə əsl klientdəki kimi ötürülür.
    /// </summary>
    private HubConnection BuildConnection(string? accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/arena"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .Build();

    private static Task<T> Wait<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(10));

    private Task<ApiTestClient> NewChildAsync(string name) =>
        ApiTestClient.CreateAsync(_factory, $"league-{Guid.NewGuid():N}@petpal.test", name);

    private static async Task<ArenaDuelDto> StartAsync(ApiTestClient client, string url)
    {
        var response = await client.Http.PostAsync(url, null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ArenaDuelDto>())!;
    }

    private static async Task<ArenaLeagueDto> GetLeagueAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync("/api/learn/arena/league");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ArenaLeagueDto>())!;
    }

    /// <summary>
    /// Bir duel: birinci uşaq hamısını səhv, ikinci hamısını doğru cavablayır.
    ///
    /// <para>Yarış SİNXRONDUR — dəst rəqib qoşulmayınca başlamır, ona görə əvvəlcə
    /// hər iki uşaq duelə girir, sonra geri sayım qədər saat irəli sürülür.</para>
    /// </summary>
    private async Task PlayDuelAsync(ApiTestClient loser, ApiTestClient winner)
    {
        var created = await StartAsync(loser, "/api/learn/arena/duels");
        var joined = await StartAsync(winner, "/api/learn/arena/duels");

        Assert.Equal(created.DuelId, joined.DuelId);
        _factory.Clock.Advance(TimeSpan.FromSeconds(CountdownSeconds));

        await PlayAsync(loser, created, correct: false);
        await PlayAsync(winner, joined, correct: true);
    }

    private async Task PlayAsync(ApiTestClient client, ArenaDuelDto duel, bool correct)
    {
        foreach (var question in duel.Questions)
            await AnswerAsync(client, duel, question, correct);
    }

    private async Task AnswerAsync(ApiTestClient client, ArenaDuelDto duel, QuestionDto question, bool correct)
    {
        var correctIndex = await CorrectIndexAsync(question.Id);
        var chosen = correct
            ? correctIndex
            : Enumerable.Range(0, question.Options.Count).First(i => i != correctIndex);

        var response = await client.Http.PostAsJsonAsync("/api/learn/arena/answers", new SubmitDuelAnswerRequest
        {
            DuelId = duel.DuelId,
            QuestionId = question.Id,
            ChosenIndex = chosen
        });

        response.EnsureSuccessStatusCode();
    }

    private async Task<int> CorrectIndexAsync(Guid questionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Questions.Where(q => q.Id == questionId).Select(q => q.CorrectIndex).FirstAsync();
    }

    /// <summary>Liqa cədvəli QLOBALDIR — hər test təmiz həftədən başlamalıdır.</summary>
    private async Task ClearDuelsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Duels.RemoveRange(await db.Duels.ToListAsync());
        await db.SaveChangesAsync();
    }
}
