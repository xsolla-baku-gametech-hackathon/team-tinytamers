using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PetPal.Api.Data;
using PetPal.Api.Learning.Arena;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Dtos.Parent;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

// ===================== Saf qaydalar =====================

/// <summary>
/// Uyğunlaşdırmanın və reytinqin saf hissəsi — I/O yoxdur. Bu qaydalar arenanın
/// ədalətini müəyyən edir, ona görə ayrıca və birbaşa yoxlanılır.
/// </summary>
public class ArenaRulesTests
{
    private readonly ArenaOptions _options = new();

    [Fact]
    public void Zolaq_VaxtlaGenislenir()
    {
        // Ölçü SANİYƏDİR: sinxron yarışda uşaq axtarış ekranında canlı gözləyir.
        Assert.Equal(_options.RatingBand, ArenaMatchmaker.RatingBandFor(0, _options));
        Assert.Equal(_options.RatingBand, ArenaMatchmaker.RatingBandFor(_options.WidenAfterSeconds - 1, _options));
        Assert.Equal(_options.WidenedRatingBand, ArenaMatchmaker.RatingBandFor(_options.WidenAfterSeconds, _options));
        Assert.Equal(ArenaMatchmaker.NoBand, ArenaMatchmaker.RatingBandFor(_options.WidenAfterSeconds * 2, _options));
    }

    [Fact]
    public void UzaqReytinq_ZolaqDaralmisKenardaQalir()
    {
        // 300 və 600 arasında 300 fərq var — başlanğıc zolaq ±150-dir.
        Assert.False(ArenaMatchmaker.Matches(0, 600, 300, 3, 3, _options));

        // Zolaq genişlənəndən sonra ±300 olur və eyni cüt uyğun gəlir.
        Assert.True(ArenaMatchmaker.Matches(_options.WidenAfterSeconds, 600, 300, 3, 3, _options));
    }

    [Fact]
    public void CetinlikDozumu_HerIkiUsagiOrtadaSaxlayir()
    {
        // Dəst duel yaradılanda dondurulur, ona görə qoşulan yalnız öz hədəfinə
        // yaxın dueli qəbul edir — nəticədə çətinlik hər ikisinə ±1 uzaqdır.
        Assert.True(ArenaMatchmaker.Matches(0, 300, 300, 4, 3, _options));
        Assert.False(ArenaMatchmaker.Matches(0, 300, 300, 7, 3, _options));
    }

    [Fact]
    public void Netice_DogruCavabSayinaGoreHellOlunur()
    {
        Assert.Equal(DuelOutcome.Win, ArenaRatingEngine.Decide(4, 9000, 3, 1000));
        Assert.Equal(DuelOutcome.Loss, ArenaRatingEngine.Decide(2, 1000, 3, 9000));
    }

    [Fact]
    public void Beraberlikde_SuretHellEdir()
    {
        Assert.Equal(DuelOutcome.Win, ArenaRatingEngine.Decide(3, 4000, 3, 5000));
        Assert.Equal(DuelOutcome.Draw, ArenaRatingEngine.Decide(3, 4000, 3, 4000));
    }

    [Fact]
    public void Uduzmaq_UlduzItirmir()
    {
        Assert.True(ArenaRatingEngine.StarsFor(DuelOutcome.Loss, _options) > 0);
        Assert.True(ArenaRatingEngine.StarsFor(DuelOutcome.Win, _options)
                    > ArenaRatingEngine.StarsFor(DuelOutcome.Loss, _options));
    }

    [Fact]
    public void SuretBonusu_ReqibiNeQederTezUdursansaBirOQederCoxdur()
    {
        var wide = ArenaRatingEngine.SpeedBonusFor(DuelOutcome.Win, 5_000, 20_000, _options);
        var narrow = ArenaRatingEngine.SpeedBonusFor(DuelOutcome.Win, 18_000, 20_000, _options);

        Assert.True(wide > narrow);
        Assert.InRange(wide, 1, _options.SpeedBonusStars);
        Assert.True(narrow >= 0);
    }

    [Fact]
    public void SuretBonusu_YalnizQabaqlayanQalibeVerilir()
    {
        // Uduzan uşaq sürətinə görə qalibi keçməməlidir.
        Assert.Equal(0, ArenaRatingEngine.SpeedBonusFor(DuelOutcome.Loss, 5_000, 20_000, _options));
        Assert.Equal(0, ArenaRatingEngine.SpeedBonusFor(DuelOutcome.Draw, 5_000, 20_000, _options));

        // Qalib daha çox doğru cavabla, amma YAVAŞ udubsa bonus yoxdur.
        Assert.Equal(0, ArenaRatingEngine.SpeedBonusFor(DuelOutcome.Win, 20_000, 5_000, _options));

        // Rəqibin vaxtı yoxdursa müqayisə üçün baza da yoxdur.
        Assert.Equal(0, ArenaRatingEngine.SpeedBonusFor(DuelOutcome.Win, 5_000, 0, _options));
    }

    [Fact]
    public void Reytinq_QalibdeArtir_UduzandaAzalir()
    {
        Assert.True(ArenaRatingEngine.UpdateRating(300, 300, DuelOutcome.Win) > 300);
        Assert.True(ArenaRatingEngine.UpdateRating(300, 300, DuelOutcome.Loss) < 300);
        Assert.Equal(300, ArenaRatingEngine.UpdateRating(300, 300, DuelOutcome.Draw));
    }

    [Fact]
    public void Reytinq_AraliqdanKenaraCixmir()
    {
        Assert.Equal(ArenaRatingEngine.MinRating,
            ArenaRatingEngine.UpdateRating(ArenaRatingEngine.MinRating, 1000, DuelOutcome.Loss));

        Assert.Equal(ArenaRatingEngine.MaxRating,
            ArenaRatingEngine.UpdateRating(ArenaRatingEngine.MaxRating, 100, DuelOutcome.Win));
    }
}

// ===================== Duel axını =====================

/// <summary>
/// Bilik Arenasının uçdan-uca axını: uyğunlaşdırma → eyni sual dəsti →
/// nəticə → mükafat. Rəqib hovuzu standart olaraq HAMIDIR, ona görə burada
/// yarışan uşaqlar dost DEYİL.
///
/// <para>Yarış SİNXRONDUR: dəst rəqibsiz başlamır. Ona görə testlər duelə
/// həmişə <see cref="MatchAsync"/> ilə, yəni İKİ tərəflə girir.</para>
/// </summary>
public class ArenaTests : IClassFixture<TestWebAppFactory>
{
    /// <summary>Serverin geri sayımı (Arena:CountdownSeconds) — testdə saat bu qədər irəli sürülür.</summary>
    private const int CountdownSeconds = 3;

    private readonly TestWebAppFactory _factory;

    public ArenaTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task ReqibYoxdursa_DuelReqibGozleyir()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();

        var duel = await StartDuelAsync(child);

        Assert.Equal(DuelStatus.WaitingOpponent, duel.Status);
        Assert.Null(duel.Opponent);
        Assert.Equal(5, duel.Questions.Count);
        Assert.All(duel.Questions, q => Assert.NotEmpty(q.Options));

        // Dəst hazırdır, amma BAŞLAMAYIB: geri sayım yoxdur.
        Assert.Null(duel.StartsInMilliseconds);
    }

    /// <summary>
    /// Yarışın əsas qaydası: rəqibsiz oynanmır. Uşaq (və ya saxta klient) dəsti
    /// tək başına cavablaya bilməməlidir — qayda SERVERDƏ saxlanılır.
    /// </summary>
    [Fact]
    public async Task ReqibTapilmayanda_CavabQebulEdilmir()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();

        var duel = await StartDuelAsync(child);
        var question = duel.Questions[0];

        var response = await AnswerAsync(child, duel.DuelId, question.Id, await CorrectIndexAsync(question.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>Geri sayım bitməyincə də cavab qəbul edilmir — start hər ikisi üçün eynidir.</summary>
    [Fact]
    public async Task GeriSayimBitmeyince_CavabQebulEdilmir()
    {
        await ClearDuelsAsync();
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        var created = await StartDuelAsync(first);
        await StartDuelAsync(second);

        var question = created.Questions[0];
        var response = await AnswerAsync(first, created.DuelId, question.Id, await CorrectIndexAsync(question.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task IkinciUsaq_DostOlmasaDaEyniDueleQosulur()
    {
        await ClearDuelsAsync();
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        var created = await StartDuelAsync(first);
        var joined = await StartDuelAsync(second);

        // Hovuz hamıdır: iki tanımadığı uşaq eyni duelə düşür.
        Assert.Equal(created.DuelId, joined.DuelId);

        // Sual dəsti SNAPSHOT-dur: eyni suallar, eyni sırada.
        Assert.Equal(
            created.Questions.Select(q => q.Id).ToList(),
            joined.Questions.Select(q => q.Id).ToList());
    }

    /// <summary>
    /// Rəqib qoşulan kimi duel CANLI olur və hər iki uşaq eyni geri sayımı görür.
    /// Yaradan uşaq üçün də sayım qoşulma anından başlayır — onun boş yerə
    /// gözlədiyi saniyələr yarışa yazılmır.
    /// </summary>
    [Fact]
    public async Task ReqibQosulanda_DestHerIkisiUcunEyniAndaBaslayir()
    {
        await ClearDuelsAsync();
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        var created = await StartDuelAsync(first);
        var joined = await StartDuelAsync(second);

        var creatorView = await GetDuelAsync(first, created.DuelId);

        Assert.Equal(DuelStatus.Live, creatorView.Status);
        Assert.Equal(DuelStatus.Live, joined.Status);
        Assert.NotNull(creatorView.Opponent);
        Assert.NotNull(joined.Opponent);

        // Saat testdə donubdur, ona görə hər ikisinin qalıq vaxtı eynidir.
        Assert.Equal(CountdownSeconds * 1000, creatorView.StartsInMilliseconds);
        Assert.Equal(CountdownSeconds * 1000, joined.StartsInMilliseconds);
    }

    /// <summary>
    /// Axtarışı dayandıran uşağın dueli hovuzdan ÇIXIR: əks halda başqa uşaq
    /// ekran qarşısında olmayan rəqiblə üz-üzə qalardı.
    /// </summary>
    [Fact]
    public async Task AxtarisDayandirilanda_DuelHovuzdanCixir()
    {
        await ClearDuelsAsync();
        var leaver = await NewChildAsync();
        var next = await NewChildAsync();

        var created = await StartDuelAsync(leaver);

        var cancel = await leaver.Http.PostAsync($"/api/learn/arena/duels/{created.DuelId}/cancel", null);
        cancel.EnsureSuccessStatusCode();

        // Növbəti uşaq ləğv olunmuş duelə DÜŞMÜR — özünə yenisini açır.
        var fresh = await StartDuelAsync(next);
        Assert.NotEqual(created.DuelId, fresh.DuelId);
    }

    /// <summary>
    /// Ard-arda oynanan duellər EYNİ dəst olmamalıdır.
    ///
    /// <para>Bu, real bir səhv idi: arena həmişə ən zəif bacarıq sahəsini
    /// seçirdi, duel isə <c>SkillMastery.Rating</c>-ə toxunmadığına görə həmin
    /// sahə heç vaxt dəyişmirdi. Üstəlik duel cavabları "yaxınlarda görülən"
    /// siyahısına düşmürdü (onlar ayrı cədvəldədir). Nəticədə uşaq eyni dar
    /// sual dəstəsini dövrə vururdu.</para>
    /// </summary>
    [Fact]
    public async Task ArdArdaDuellerde_EyniSuallarTekrarlanmir()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();

        var seen = new List<List<Guid>>();

        for (var i = 0; i < 4; i++)
        {
            var (duel, _) = await MatchAsync(child, await NewChildAsync());
            seen.Add(duel.Questions.Select(q => q.Id).ToList());
            await PlayAsync(child, duel, correct: true);
        }

        // Heç bir iki dəst tam eyni olmamalıdır.
        for (var i = 0; i < seen.Count; i++)
        {
            for (var j = i + 1; j < seen.Count; j++)
                Assert.NotEqual(seen[i], seen[j]);
        }

        // Ümumi mənzərə: dörd dueldə görülən sualların əksəriyyəti fərqlidir.
        var all = seen.SelectMany(s => s).ToList();
        var distinct = all.Distinct().Count();

        Assert.True(distinct >= all.Count * 3 / 4,
            $"20 sualdan yalnız {distinct} fərqli çıxdı — dəstlər hələ də təkrarlanır.");
    }

    /// <summary>
    /// Axtarıb geri qayıtmaq günün yarışını YEMİR. Uşaq heç nə oynamayıb:
    /// rəqib tapılmayıb, dəst başlamayıb.
    /// </summary>
    [Fact]
    public async Task LegvOlunmusAxtaris_GundelikHeddiYemir()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();

        var before = await GetStatusAsync(child);

        var duel = await StartDuelAsync(child);
        var cancel = await child.Http.PostAsync($"/api/learn/arena/duels/{duel.DuelId}/cancel", null);
        cancel.EnsureSuccessStatusCode();

        var after = await GetStatusAsync(child);

        Assert.Equal(before.DuelsLeftToday, after.DuelsLeftToday);
    }

    [Fact]
    public async Task HerIkiTerefBitirende_NeticeVeUlduzlarYazilir()
    {
        await ClearDuelsAsync();
        var loser = await NewChildAsync();
        var winner = await NewChildAsync();

        var (created, joined) = await MatchAsync(loser, winner);
        await PlayAsync(loser, created, correct: false);
        var winnerResult = await PlayAsync(winner, joined, correct: true);

        // Rəqib artıq bitirdiyinə görə nəticə son cavabla DƏRHAL gəlir.
        Assert.NotNull(winnerResult);
        Assert.Equal(DuelOutcome.Win, winnerResult!.Outcome);
        Assert.Equal(DuelStatus.Complete, winnerResult.Status);
        Assert.Equal(5, winnerResult.MyCorrectCount);
        Assert.Equal(0, winnerResult.OpponentCorrectCount);
        Assert.True(winnerResult.RatingAfter > winnerResult.RatingBefore);

        // Uduzan uşaq nəticəni sonra açır — ulduzu isə onu gözləmir.
        var loserResult = await GetResultAsync(loser, created.DuelId);
        Assert.Equal(DuelOutcome.Loss, loserResult.Outcome);
        Assert.True(loserResult.StarsEarned > 0);
        Assert.True(loserResult.RatingAfter < loserResult.RatingBefore);
        Assert.Equal(5, loserResult.Questions.Count);
        Assert.All(loserResult.Questions, q => Assert.True(q.OpponentCorrect));
    }

    /// <summary>
    /// Hesab bərabər, qalibi VAXT seçir. İki şey yoxlanılır: uşaq udduğunu
    /// rəqəmlərdən yox, ayrıca bayraqdan öyrənir (ekran bunu sözlə yazır) və
    /// rəqibi qabaqlamaq əlavə ulduz gətirir.
    /// </summary>
    [Fact]
    public async Task SuretleQalibGelmek_IsarelenirVeElaveUlduzGetirir()
    {
        await ClearDuelsAsync();
        var slow = await NewChildAsync();
        var quick = await NewChildAsync();

        var (created, joined) = await MatchAsync(slow, quick);
        await PlayAsync(slow, created, correct: true);

        // Testdə saat donubdur, yəni hər iki uşağın vaxtı sıfırdır — birinci
        // uşağın vaxtını qəsdən uzadırıq ki, fərqi sürət yaratsın, təsadüf yox.
        await SetTotalMillisecondsAsync(created.DuelId, slow.ChildId, 40_000);

        var winner = await PlayAsync(quick, joined, correct: true);

        Assert.NotNull(winner);
        Assert.Equal(DuelOutcome.Win, winner!.Outcome);

        // Rəqəmlər eynidir — məhz buna görə bayraq lazımdır.
        Assert.Equal(winner.MyCorrectCount, winner.OpponentCorrectCount);
        Assert.True(winner.DecidedBySpeed);

        // Bonus cəmin İÇİNDƏDİR: baza + bonus = verilən ulduz.
        var options = _factory.Services.GetRequiredService<IOptions<ArenaOptions>>().Value;
        Assert.True(winner.SpeedBonusStars > 0);
        Assert.Equal(options.WinStars + winner.SpeedBonusStars, winner.StarsEarned);

        // Uduzan uşaq da eyni izahı görür, amma bonus almır.
        var loser = await GetResultAsync(slow, created.DuelId);
        Assert.Equal(DuelOutcome.Loss, loser.Outcome);
        Assert.True(loser.DecidedBySpeed);
        Assert.Equal(0, loser.SpeedBonusStars);
        Assert.Equal(options.LossStars, loser.StarsEarned);
    }

    /// <summary>Rəqəm fərqi ilə udulan dueldə "sürətlə qalib" yazısı ÇIXMAMALIDIR.</summary>
    [Fact]
    public async Task DogruCavabFerqiIleUdulanda_SuretIsaresiCixmir()
    {
        await ClearDuelsAsync();
        var loser = await NewChildAsync();
        var winner = await NewChildAsync();

        var (created, joined) = await MatchAsync(loser, winner);
        await PlayAsync(loser, created, correct: false);
        var result = await PlayAsync(winner, joined, correct: true);

        Assert.NotNull(result);
        Assert.Equal(DuelOutcome.Win, result!.Outcome);
        Assert.False(result.DecidedBySpeed);
    }

    [Fact]
    public async Task Yaris_OyrenmeReytinqineToxunmur()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();
        var rival = await NewChildAsync();

        var (duel, joined) = await MatchAsync(child, rival);
        var before = await SkillRatingsAsync(child.ChildId);

        await PlayAsync(child, duel, correct: false);
        await PlayAsync(rival, joined, correct: true);

        var after = await SkillRatingsAsync(child.ChildId);

        // Öyrənmə reytinqi yarışdan təsirlənmir — bu, adaptiv seçimin şərtidir.
        Assert.NotEmpty(before);
        Assert.Equal(before, after);

        // Arena reytinqi isə dəyişir.
        Assert.NotEqual(ArenaRatingEngine.StartingRating, await ArenaRatingAsync(child.ChildId));
    }

    [Fact]
    public async Task TanimadigiReqibin_EslAdiGorunmur()
    {
        await ClearDuelsAsync();
        var first = await NewChildAsync();
        var second = await NewChildAsync(childName: "Nihat");

        var created = await StartDuelAsync(first);
        await StartDuelAsync(second);

        var view = await GetDuelAsync(first, created.DuelId);

        Assert.NotNull(view.Opponent);
        Assert.Null(view.Opponent!.DisplayName);
        Assert.False(view.Opponent.IsFriend);
        Assert.Equal("Max", view.Opponent.PetName);
    }

    [Fact]
    public async Task YalnizDostlarAcilanda_TanimadigiUsaqQosulmur()
    {
        await ClearDuelsAsync();
        var restricted = await NewChildAsync();
        var stranger = await NewChildAsync();

        await UpdateArenaSettingsAsync(restricted, enabled: true, friendsOnly: true);

        var created = await StartDuelAsync(restricted);
        var strangerDuel = await StartDuelAsync(stranger);

        // Tanımadığı uşaq həmin duelə düşmür — özünə ayrı duel açılır.
        Assert.NotEqual(created.DuelId, strangerDuel.DuelId);
    }

    [Fact]
    public async Task ValideynBaglayanda_YarisIslemir()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();

        await UpdateArenaSettingsAsync(child, enabled: false, friendsOnly: false);

        var response = await child.Http.PostAsync("/api/learn/arena/duels", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EyniSuala_IkinciCavabQebulEdilmir()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();
        var (duel, _) = await MatchAsync(child, await NewChildAsync());

        var question = duel.Questions[0];
        var index = await CorrectIndexAsync(question.Id);

        var first = await AnswerAsync(child, duel.DuelId, question.Id, index);
        first.EnsureSuccessStatusCode();

        var second = await AnswerAsync(child, duel.DuelId, question.Id, index);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task DuelaAidOlmayanSual_QebulEdilmir()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();
        var (duel, _) = await MatchAsync(child, await NewChildAsync());

        Guid outsiderId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var inDuel = duel.Questions.Select(q => q.Id).ToList();
            outsiderId = await db.Questions.Where(q => !inDuel.Contains(q.Id)).Select(q => q.Id).FirstAsync();
        }

        var response = await AnswerAsync(child, duel.DuelId, outsiderId, 0);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task YarimciqDuel_YenidenBasladilmir()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();

        var duel = await StartDuelAsync(child);
        var again = await StartDuelAsync(child);

        // Uşaq həmişə başladığı dəstə qayıdır — yeni duel açılmır.
        Assert.Equal(duel.DuelId, again.DuelId);
    }

    /// <summary>
    /// Arenaya girişin gündəlik həddi YOXDUR (2026-08-23-də ləğv edildi).
    /// Uşağı saxlayan ekran vaxtı qaydasıdır — sayğac yox.
    /// </summary>
    [Fact]
    public async Task GundelikHedd_YoxdurVeUsaqDayanmadanOynayir()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();

        var status = await GetStatusAsync(child);
        Assert.Equal(0, status.DuelsPerDay);

        // Köhnə həddən (5) ÇOX oynayırıq: heç biri rədd olunmamalıdır. Hər duelə
        // təzə rəqib gətirilir, çünki yarış rəqibsiz başlamır.
        for (var i = 0; i < 6; i++)
        {
            var (duel, _) = await MatchAsync(child, await NewChildAsync());
            await PlayAsync(child, duel, correct: true);
        }

        var response = await child.Http.PostAsync("/api/learn/arena/duels", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Status_HazirNeticeleriIsareleyir()
    {
        await ClearDuelsAsync();
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        var (created, joined) = await MatchAsync(first, second);
        await PlayAsync(first, created, correct: true);

        // Rəqib hələ bitirməyib: nəticə yoxdur, uşaq gözləmir.
        var waiting = await GetStatusAsync(first);
        Assert.Empty(waiting.PendingResultDuelIds);

        await PlayAsync(second, joined, correct: false);

        // İndi nəticə hazırdır — ana ekrandakı nişan buna baxır.
        var ready = await GetStatusAsync(first);
        Assert.Contains(created.DuelId, ready.PendingResultDuelIds);
        Assert.Equal(1, ready.Wins);

        // Nəticəyə baxandan sonra nişan sönür.
        await GetResultAsync(first, created.DuelId);
        var seen = await GetStatusAsync(first);
        Assert.Empty(seen.PendingResultDuelIds);
    }

    // ---------- Məşq rəqibi ----------

    [Fact]
    public async Task MesqDueli_AcigIsarelenmisReqibleGelir()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();

        var duel = await StartPracticeAsync(child);

        Assert.True(duel.IsPractice);
        Assert.Equal(5, duel.Questions.Count);
        Assert.NotNull(duel.Opponent);

        // Süni rəqib əsl uşaq kimi göstərilmir — adı açıq şəkildə "Məşq rəqibi"dir.
        Assert.True(duel.Opponent!.IsPractice);
        Assert.Equal("Məşq rəqibi", duel.Opponent.DisplayName);
    }

    [Fact]
    public async Task MesqDueli_ReytinqeVeUlduzaToxunmur()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();

        var starsBefore = await StarsAsync(child.ChildId);
        var duel = await StartPracticeAsync(child);
        var result = await PlayAsync(child, duel, correct: true);

        // Nəticə DƏRHAL gəlir: rəqibin cavabları duel yaradılanda hesablanıb.
        Assert.NotNull(result);
        Assert.True(result!.IsPractice);
        Assert.Equal(DuelStatus.Complete, result.Status);
        Assert.Equal(0, result.StarsEarned);
        Assert.Equal(result.RatingBefore, result.RatingAfter);

        Assert.Equal(ArenaRatingEngine.StartingRating, await ArenaRatingAsync(child.ChildId));
        Assert.Equal(starsBefore, await StarsAsync(child.ChildId));
    }

    [Fact]
    public async Task MesqDueli_GundelikHeddiYemir()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();

        var before = await GetStatusAsync(child);
        var duel = await StartPracticeAsync(child);
        await PlayAsync(child, duel, correct: true);
        var after = await GetStatusAsync(child);

        Assert.Equal(before.DuelsLeftToday, after.DuelsLeftToday);
        Assert.Equal(0, after.Wins);
        Assert.Empty(after.PendingResultDuelIds);
    }

    [Fact]
    public async Task MesqDueli_EslYarisdaReqibKimiGorunmur()
    {
        await ClearDuelsAsync();
        var practising = await NewChildAsync();
        var rival = await NewChildAsync();

        var practice = await StartPracticeAsync(practising);
        var real = await StartDuelAsync(rival);

        // Məşq dueli hovuzda deyil — başqa uşaq ona düşə bilməz.
        Assert.NotEqual(practice.DuelId, real.DuelId);
        Assert.False(real.IsPractice);
        Assert.Null(real.Opponent);
    }

    [Fact]
    public async Task YarimciqMesq_EslYarisaManeOlmur()
    {
        await ClearDuelsAsync();
        var child = await NewChildAsync();

        var practice = await StartPracticeAsync(child);
        var real = await StartDuelAsync(child);

        Assert.NotEqual(practice.DuelId, real.DuelId);
        Assert.False(real.IsPractice);
    }

    // ---------- Birbaşa çağırış ----------

    [Fact]
    public async Task DostOlmayani_YarisaCagirmaqOlmur()
    {
        await ClearDuelsAsync();

        var child = await NewChildAsync();
        var stranger = await NewChildAsync();

        var response = await child.Http.PostAsync($"/api/learn/arena/challenge/{stranger.ChildId}", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cagiris_AdiHovuzaDusmur()
    {
        await ClearDuelsAsync();

        var (challenger, friend) = await FriendPairAsync();

        var challenge = await ChallengeAsync(challenger, friend.ChildId);

        // Üçüncü uşaq adi axtarışa çıxır: ünvanlanmış duelə DÜŞMƏMƏLİDİR,
        // yoxsa çağırılan dost heç vaxt oynaya bilməzdi.
        var outsider = await NewChildAsync();
        var theirs = await StartDuelAsync(outsider);

        Assert.NotEqual(challenge.DuelId, theirs.DuelId);
    }

    [Fact]
    public async Task CagirisQebulEdilende_HerIkisiEyniDeseDusur()
    {
        await ClearDuelsAsync();

        var (challenger, friend) = await FriendPairAsync();

        var challenge = await ChallengeAsync(challenger, friend.ChildId);

        // Çağırış status sorğusunda görünür — real vaxt kanalı olmasa da.
        var status = await GetStatusAsync(friend);
        Assert.Equal(challenge.DuelId, Assert.Single(status.IncomingChallenges).DuelId);

        var accepted = await AcceptChallengeAsync(friend, challenge.DuelId);
        Assert.Equal(challenge.DuelId, accepted.DuelId);

        _factory.Clock.Advance(TimeSpan.FromSeconds(CountdownSeconds));

        var creatorView = await GetDuelAsync(challenger, challenge.DuelId);
        Assert.NotNull(creatorView.Opponent);
    }

    [Fact]
    public async Task CagirisdanImtinaEdilende_DuelSonur()
    {
        await ClearDuelsAsync();

        var (challenger, friend) = await FriendPairAsync();

        var challenge = await ChallengeAsync(challenger, friend.ChildId);

        (await friend.Http.PostAsync($"/api/learn/arena/challenge/{challenge.DuelId}/decline", null))
            .EnsureSuccessStatusCode();

        Assert.Empty((await GetStatusAsync(friend)).IncomingChallenges);

        // Sönmüş çağırışı qəbul etmək olmaz.
        var late = await friend.Http.PostAsync($"/api/learn/arena/challenge/{challenge.DuelId}/accept", null);
        Assert.Equal(HttpStatusCode.NotFound, late.StatusCode);
    }

    [Fact]
    public async Task BasqasinaUnvanlanmisCagirisi_QebulEtmekOlmur()
    {
        await ClearDuelsAsync();

        var (challenger, friend) = await FriendPairAsync();
        var outsider = await NewChildAsync();

        var challenge = await ChallengeAsync(challenger, friend.ChildId);

        var response = await outsider.Http.PostAsync($"/api/learn/arena/challenge/{challenge.DuelId}/accept", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ArenaBagliDirsa_CagirisGetmir()
    {
        await ClearDuelsAsync();

        var (challenger, friend) = await FriendPairAsync();

        await UpdateArenaSettingsAsync(challenger, enabled: false, friendsOnly: false);

        var response = await challenger.Http.PostAsync($"/api/learn/arena/challenge/{friend.ChildId}", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- Köməkçilər ----------

    /// <summary>Təsdiqlənmiş dost cütü — çağırış yalnız belə cütdə mümkündür.</summary>
    private async Task<(ApiTestClient First, ApiTestClient Second)> FriendPairAsync()
    {
        var first = await NewChildAsync();
        var second = await NewChildAsync();

        var codeResponse = await second.Http.GetAsync("/api/social/friend-code");
        codeResponse.EnsureSuccessStatusCode();
        var code = (await codeResponse.Content.ReadFromJsonAsync<string>())!;

        (await first.Http.PostAsJsonAsync("/api/social/friends",
            new PetPal.Shared.Dtos.Social.AddFriendRequest { FriendCode = code })).EnsureSuccessStatusCode();

        // Sorğunu kodun sahibi ÖZÜ qəbul edir — valideyn sessiyası lazım deyil.
        (await second.Http.PostAsJsonAsync($"/api/social/friend-requests/{first.ChildId}",
            new PetPal.Shared.Dtos.Social.FriendRequestDecision { Approve = true }))
            .EnsureSuccessStatusCode();

        return (first, second);
    }

    private static async Task<ArenaDuelDto> ChallengeAsync(ApiTestClient client, Guid friendChildId)
    {
        var response = await client.Http.PostAsync($"/api/learn/arena/challenge/{friendChildId}", null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ArenaDuelDto>())!;
    }

    private static async Task<ArenaDuelDto> AcceptChallengeAsync(ApiTestClient client, Guid duelId)
    {
        var response = await client.Http.PostAsync($"/api/learn/arena/challenge/{duelId}/accept", null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ArenaDuelDto>())!;
    }

    private Task<ApiTestClient> NewChildAsync(string childName = "Ayan") =>
        ApiTestClient.CreateAsync(_factory, $"arena-{Guid.NewGuid():N}@petpal.test", childName);

    private static async Task<ArenaDuelDto> StartDuelAsync(ApiTestClient client)
    {
        var response = await client.Http.PostAsync("/api/learn/arena/duels", null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ArenaDuelDto>())!;
    }

    private static async Task<ArenaDuelDto> StartPracticeAsync(ApiTestClient client)
    {
        var response = await client.Http.PostAsync("/api/learn/arena/practice", null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ArenaDuelDto>())!;
    }

    /// <summary>
    /// Sinxron yarışın cütü: birinci uşaq duel açır, ikinci qoşulur, sonra saat
    /// geri sayım qədər irəli sürülür — dəst hər ikisi üçün başlayır.
    ///
    /// <para>Testin saatı DONUBDUR, ona görə geri sayım öz-özünə bitmir və onu
    /// açıq şəkildə keçmək lazımdır. Bu, həm də qaydanı təsdiqləyir: sayım
    /// bitməyincə cavab qəbul edilmir.</para>
    /// </summary>
    private async Task<(ArenaDuelDto Creator, ArenaDuelDto Joiner)> MatchAsync(
        ApiTestClient creator, ApiTestClient joiner)
    {
        var created = await StartDuelAsync(creator);
        var joined = await StartDuelAsync(joiner);

        Assert.Equal(created.DuelId, joined.DuelId);

        _factory.Clock.Advance(TimeSpan.FromSeconds(CountdownSeconds));

        // Yaradanın görünüşü rəqib qoşulandan sonra yenilənir.
        return (await GetDuelAsync(creator, created.DuelId), joined);
    }

    private static async Task<ArenaDuelDto> GetDuelAsync(ApiTestClient client, Guid duelId)
    {
        var response = await client.Http.GetAsync($"/api/learn/arena/duels/{duelId}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ArenaDuelDto>())!;
    }

    private static async Task<ArenaDuelResultDto> GetResultAsync(ApiTestClient client, Guid duelId)
    {
        var response = await client.Http.GetAsync($"/api/learn/arena/duels/{duelId}/result");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ArenaDuelResultDto>())!;
    }

    private static async Task<ArenaStatusDto> GetStatusAsync(ApiTestClient client)
    {
        var response = await client.Http.GetAsync("/api/learn/arena/status");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ArenaStatusDto>())!;
    }

    private static Task<HttpResponseMessage> AnswerAsync(
        ApiTestClient client, Guid duelId, Guid questionId, int chosenIndex) =>
        client.Http.PostAsJsonAsync("/api/learn/arena/answers", new SubmitDuelAnswerRequest
        {
            DuelId = duelId,
            QuestionId = questionId,
            ChosenIndex = chosenIndex
        });

    /// <summary>Dəsti sona qədər oynayır və (hazırdırsa) yekun nəticəni qaytarır.</summary>
    private async Task<ArenaDuelResultDto?> PlayAsync(ApiTestClient client, ArenaDuelDto duel, bool correct)
    {
        DuelAnswerResultDto? last = null;

        foreach (var question in duel.Questions)
        {
            var correctIndex = await CorrectIndexAsync(question.Id);
            var chosen = correct
                ? correctIndex
                : Enumerable.Range(0, question.Options.Count).First(i => i != correctIndex);

            var response = await AnswerAsync(client, duel.DuelId, question.Id, chosen);
            response.EnsureSuccessStatusCode();

            last = await response.Content.ReadFromJsonAsync<DuelAnswerResultDto>();
        }

        return last?.Result;
    }

    private static async Task UpdateArenaSettingsAsync(ApiTestClient client, bool enabled, bool friendsOnly)
    {
        client.SwitchToParent();

        var response = await client.Http.PutAsJsonAsync(
            $"/api/parent/children/{client.ChildId}/arena",
            new ArenaSettingsRequest { Enabled = enabled, FriendsOnly = friendsOnly });

        response.EnsureSuccessStatusCode();
        client.SwitchToChild();
    }

    /// <summary>
    /// Testlər eyni bazanı bölüşür, arena isə QLOBAL hovuzdur — əvvəlki testdən
    /// qalan açıq duel növbəti testin rəqibi ola bilər. Ona görə hər test təmiz
    /// hovuzdan başlayır.
    /// </summary>
    private async Task ClearDuelsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Duels.RemoveRange(await db.Duels.ToListAsync());
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Bir uşağın duel vaxtını qəsdən dəyişir. Testdə hər iki tərəf eyni
    /// millisaniyələrdə oynayır, sürət qaydasını isə ölçülə bilən fərq olmadan
    /// yoxlamaq mümkün deyil.
    /// </summary>
    private async Task SetTotalMillisecondsAsync(Guid duelId, Guid childId, int milliseconds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entry = await db.DuelEntries.FirstAsync(e => e.DuelId == duelId && e.ChildProfileId == childId);
        entry.TotalMilliseconds = milliseconds;

        await db.SaveChangesAsync();
    }

    private async Task<int> CorrectIndexAsync(Guid questionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Questions.Where(q => q.Id == questionId).Select(q => q.CorrectIndex).FirstAsync();
    }

    private async Task<List<int>> SkillRatingsAsync(Guid childId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.SkillMasteries
            .Where(m => m.ChildProfileId == childId)
            .OrderBy(m => m.Skill)
            .Select(m => m.Rating)
            .ToListAsync();
    }

    private async Task<int> StarsAsync(Guid childId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.ChildProfiles.Where(c => c.Id == childId).Select(c => c.Stars).FirstAsync();
    }

    private async Task<int> ArenaRatingAsync(Guid childId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.ChildProfiles.Where(c => c.Id == childId).Select(c => c.ArenaRating).FirstAsync();
    }
}
