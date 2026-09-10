using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Intent;
using PetPal.Api.PetBrain.Mind;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Pet-in niyyət planlayıcısı — <b>saf və determinist</b>.
///
/// <para>Prioritet sırası burada qorunur: ekran vaxtı → təcili qulluq →
/// gecə → missiya → yaddaş → xarakter. Sıra pozulanda pet uşağı qaydanı
/// pozmağa çağıra bilər, ona görə hər pillə ayrıca yoxlanılır.</para>
/// </summary>
public class PetIntentPlannerTests
{
    /// <summary>
    /// <b>Ekran vaxtı bloklu olanda YENİ fəaliyyət niyyəti başlamır.</b>
    ///
    /// <para>Pet «gəl oynayaq» deyə bilməz — bu, uşağı valideynin qoyduğu
    /// qaydanı pozmağa çağırardı. Yalnız dincəlmək qalır, çünki o, heç kimi
    /// heç yerə çağırmır.</para>
    /// </summary>
    [Fact]
    public void EkranVaxtiBloklu_YalnizDincelmekQalir()
    {
        foreach (var personality in Enum.GetValues<PetBrainPersonality>())
        {
            var plan = PetIntentPlanner.Plan(Mind(
                screenTime: PetBrainScreenTimeBand.Blocked, personality: personality));

            Assert.NotNull(plan);
            Assert.Equal(PetBrainIntentType.Rest, plan!.Type);
            Assert.Equal(PetIntentPlanner.ReasonQuietHours, plan.ReasonKey);
        }
    }

    /// <summary>Təcili qulluq ehtiyacı macəra niyyətindən ÜSTÜNDÜR.</summary>
    [Theory]
    [InlineData("fullness", PetBrainIntentType.Care, PetIntentPlanner.ReasonHungry)]
    [InlineData("cleanliness", PetBrainIntentType.Care, PetIntentPlanner.ReasonUnclean)]
    [InlineData("energy", PetBrainIntentType.Rest, PetIntentPlanner.ReasonLowEnergy)]
    public void TecilQulluq_MaceradanUstundur(string need, PetBrainIntentType type, string reason)
    {
        var mind = Mind(personality: PetBrainPersonality.ExplorerCompanion) with
        {
            Fullness = need == "fullness" ? PetBrainCareBand.Urgent : PetBrainCareBand.Great,
            Cleanliness = need == "cleanliness" ? PetBrainCareBand.Urgent : PetBrainCareBand.Great,
            Energy = need == "energy" ? PetBrainCareBand.Urgent : PetBrainCareBand.Great
        };

        var plan = PetIntentPlanner.Plan(mind);

        Assert.NotNull(plan);
        Assert.Equal(type, plan!.Type);
        Assert.Equal(reason, plan.ReasonKey);
    }

    /// <summary>Gecə pet də yatır — uşağı oyatmaq üçün bəhanə yoxdur.</summary>
    [Fact]
    public void Gece_DincelmeyeKecir()
    {
        var plan = PetIntentPlanner.Plan(Mind(bucket: PetBrainSessionBucket.Night));

        Assert.Equal(PetBrainIntentType.Rest, plan!.Type);
    }

    /// <summary>Aktiv missiya varsa pet KÖMƏK niyyəti seçir.</summary>
    [Fact]
    public void AktivMissiya_KomekNiyyetiVerir()
    {
        var plan = PetIntentPlanner.Plan(Mind(missions: ["daily-explore"]));

        Assert.Equal(PetBrainIntentType.Help, plan!.Type);
        Assert.Equal(PetIntentPlanner.ReasonMission, plan.ReasonKey);
        Assert.Equal("daily-explore", plan.MissionKey);
    }

    /// <summary>Öyrənilmiş naxış niyyətə çevrilir — yaddaş davranışa təsir edir.</summary>
    [Theory]
    [InlineData(SemanticMemory.PrefersExploring, PetBrainIntentType.Explore)]
    [InlineData(SemanticMemory.PrefersCreating, PetBrainIntentType.Create)]
    [InlineData(SemanticMemory.PrefersSolving, PetBrainIntentType.Learn)]
    [InlineData(SemanticMemory.PrefersHelping, PetBrainIntentType.Help)]
    public void OyrenilmisNaxis_NiyyeteCevrilir(string pattern, PetBrainIntentType expected)
    {
        var memory = new MindMemory(
            Guid.NewGuid(), PetBrainMemoryKind.PatternLearned, pattern, string.Empty,
            75, DateTime.UtcNow, null, []);

        var plan = PetIntentPlanner.Plan(Mind(memories: [memory]));

        Assert.Equal(expected, plan!.Type);
        Assert.Equal(PetIntentPlanner.ReasonRemembered, plan.ReasonKey);
    }

    /// <summary>Xarakter niyyəti dəyişir — pet ÖZÜ kimi davranır.</summary>
    [Fact]
    public void Xarakter_NiyyetiDeyisir()
    {
        var types = Enum.GetValues<PetBrainPersonality>()
            .Select(p => PetIntentPlanner.Plan(Mind(personality: p))!.Type)
            .ToList();

        Assert.True(types.Distinct().Count() >= 4, "Xarakterlər eyni niyyəti seçir.");
    }

    /// <summary>Eyni kontekst HƏMİŞƏ eyni niyyəti verir — determinizm.</summary>
    [Fact]
    public void EyniKontekst_EyniNiyyetiVerir()
    {
        var mind = Mind(personality: PetBrainPersonality.CuriousScientist);

        var first = PetIntentPlanner.Plan(mind);

        for (var i = 0; i < 5; i++)
        {
            var again = PetIntentPlanner.Plan(mind);

            Assert.Equal(first!.Type, again!.Type);
            Assert.Equal(first.ReasonKey, again.ReasonKey);
            Assert.Equal(first.Duration, again.Duration);
        }
    }

    /// <summary>Yumurta niyyət qurmur — açılmamış pet nə edir?</summary>
    [Fact]
    public void Yumurta_NiyyetQurmur() =>
        Assert.Null(PetIntentPlanner.Plan(Mind(hatched: false)));

    /// <summary>Nəticə açarı TƏSDİQLƏNMİŞ siyahıdandır və deterministdir.</summary>
    [Fact]
    public void Netice_TesdiqlenmisVeDeterministdir()
    {
        foreach (var type in Enum.GetValues<PetBrainIntentType>())
        {
            var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
            var outcome = PetIntentPlanner.OutcomeFor(type, id);

            Assert.Contains(outcome, PetIntentPlanner.Outcomes(type));
            Assert.Equal(outcome, PetIntentPlanner.OutcomeFor(type, id));
        }
    }

    private static PetMindContext Mind(
        PetBrainScreenTimeBand screenTime = PetBrainScreenTimeBand.Plenty,
        PetBrainSessionBucket bucket = PetBrainSessionBucket.Afternoon,
        PetBrainPersonality personality = PetBrainPersonality.Balanced,
        IReadOnlyList<string>? missions = null,
        IReadOnlyList<MindMemory>? memories = null,
        bool hatched = true) =>
        MindStub.Build(
            screenTime: screenTime,
            bucket: bucket,
            personality: personality,
            missions: missions,
            memories: memories,
            hatched: hatched);
}

/// <summary>
/// Niyyətin CÜMLƏSİ — pet uşağı heç vaxt günahlandırmır.
/// </summary>
public class PetIntentVoiceTests
{
    /// <summary>
    /// <b>Emosional təzyiq və günahlandırma QADAĞANDIR.</b>
    ///
    /// <para>«Sən yoxkən darıxdım», «məni tək qoydun», «niyə gəlmədin» —
    /// uşaq məhsulunda ən asan və ən zərərli qısayol. Bu test bütün
    /// variantları süzür.</para>
    /// </summary>
    [Fact]
    public void HecBirCumle_GunahlandirmirVeTezyiqQurmur()
    {
        string[] banned =
        [
            "tək qoydun", "darıxdım", "küsdüm", "unutdun", "niyə gəlmədin", "gözlədim",
            "sən yoxkən", "peşman", "məcbur", "tələs", "gec gəldin",
            "you left me", "i missed you", "you forgot", "why did you not",
            "i waited", "while you were away", "hurry", "you should have"
        ];

        foreach (var line in AllLines())
        foreach (var phrase in banned)
            Assert.DoesNotContain(phrase, line, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Hər niyyət və hər nəticə HƏR İKİ dildə mövcuddur.</summary>
    [Fact]
    public void ButunCumleler_HerIkiDildedir()
    {
        foreach (var type in Enum.GetValues<PetBrainIntentType>())
        {
            var az = PetIntentVoice.Doing(type, "az", "Luna");
            var en = PetIntentVoice.Doing(type, "en", "Luna");

            Assert.False(string.IsNullOrWhiteSpace(az));
            Assert.False(string.IsNullOrWhiteSpace(en));
            Assert.NotEqual(az, en);

            foreach (var outcome in PetIntentPlanner.Outcomes(type))
            {
                var outAz = PetIntentVoice.Outcome(type, outcome, "az", "Luna");
                var outEn = PetIntentVoice.Outcome(type, outcome, "en", "Luna");

                Assert.False(string.IsNullOrWhiteSpace(outAz));
                Assert.False(string.IsNullOrWhiteSpace(outEn));
                Assert.NotEqual(outAz, outEn);
            }
        }
    }

    /// <summary>Naməlum nəticə açarı boş ekran vermir — neytral cümləyə düşür.</summary>
    [Fact]
    public void NamelumNetice_NeytralCumleyeDusur()
    {
        var line = PetIntentVoice.Outcome(PetBrainIntentType.Play, "uydurma", "az", "Luna");

        Assert.False(string.IsNullOrWhiteSpace(line));
    }

    private static IEnumerable<string> AllLines()
    {
        foreach (var type in Enum.GetValues<PetBrainIntentType>())
        foreach (var language in new[] { "az", "en" })
        {
            yield return PetIntentVoice.Doing(type, language, "Luna");

            foreach (var outcome in PetIntentPlanner.Outcomes(type))
                yield return PetIntentVoice.Outcome(type, outcome, language, "Luna");
        }
    }
}

/// <summary>Niyyət açarı AÇIQ olanda (standart konfiqurasiya) uçdan-uca işləyir.</summary>
public sealed class IntentEnabledFactory : TestWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("PetBrainV2:IntentEnabled", "true");
    }
}

/// <summary>
/// Niyyət açarı BAĞLI olanda heç nə yazılmır.
///
/// <para>Açar standart olaraq AÇIQDIR, ona görə «bağlı» halı burada AÇIQ
/// şəkildə qurulur — testin nəyi yoxladığı konfiqurasiyanın standartından
/// asılı qalmamalıdır.</para>
/// </summary>
public sealed class IntentDisabledFactory : TestWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("PetBrainV2:IntentEnabled", "false");
    }
}

public class PetIntentIntegrationTests : IClassFixture<IntentEnabledFactory>
{
    private readonly IntentEnabledFactory _factory;

    public PetIntentIntegrationTests(IntentEnabledFactory factory) => _factory = factory;

    /// <summary>Ekran açılanda pet-in niyyəti yaranır və uşağa cümlə gəlir.</summary>
    [Fact]
    public async Task EkranAcilanda_NiyyetYaranir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "intent-create@petpal.test", "Aylin");
        await client.HatchAsync(_factory);

        var state = await StateAsync(client);

        Assert.NotNull(state.Intent);
        Assert.False(state.Intent!.IsComplete);
        Assert.False(string.IsNullOrWhiteSpace(state.Intent.Line));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(1, await db.PetIntents.CountAsync(i => i.ChildProfileId == client.ChildId));
    }

    /// <summary>
    /// Vaxt keçəndə niyyət TAMAMLANIR və nəticəsi danışılır — fon işçisi
    /// olmadan, yalnız saatla.
    /// </summary>
    [Fact]
    public async Task VaxtKecende_NiyyetTamamlanir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, "intent-complete@petpal.test", "Mia");
        await client.HatchAsync(_factory);

        await StateAsync(client);

        _factory.Clock.Advance(TimeSpan.FromHours(4));

        var state = await StateAsync(client);

        Assert.NotNull(state.Intent);
        Assert.True(state.Intent!.IsComplete);
        Assert.False(string.IsNullOrWhiteSpace(state.Intent.OutcomeKey));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var completed = await db.PetIntents
            .AsNoTracking()
            .Where(i => i.ChildProfileId == client.ChildId
                        && i.Status == PetBrainIntentStatus.Completed)
            .ToListAsync();

        Assert.NotEmpty(completed);
        Assert.All(completed, i => Assert.False(string.IsNullOrWhiteSpace(i.OutcomeKey)));
    }

    /// <summary>Açar BAĞLI olanda heç bir niyyət yaranmır və sahə boş qalır.</summary>
    [Fact]
    public async Task AcarBagliOlanda_NiyyetYaranmir()
    {
        using var off = new IntentDisabledFactory();

        var client = await ApiTestClient.CreateAsync(off, "intent-off@petpal.test", "Nur");
        await client.HatchAsync(off);

        var state = await StateAsync(client);

        Assert.Null(state.Intent);

        await using var scope = off.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(0, await db.PetIntents.CountAsync());
    }

    private static async Task<PetBrainStateDto> StateAsync(ApiTestClient client) =>
        (await client.Http.GetFromJsonAsync<PetBrainStateDto>("/api/pet-brain"))!;
}
