using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Data.Questions;
using PetPal.Api.Entities;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Sual bankının ölçüsü və keyfiyyəti. Bunlar kompilyasiya xətası vermir, amma
/// pozulanda uşaq eyni sualları təkrar-təkrar görür — yəni app öz işini görmür.
/// </summary>
public class QuestionBankTests
{
    /// <summary>Bir sessiya 5 sualdır və seçim hədəf çətinliyin ±1 aralığındandır.</summary>
    private const int MinPerDifficulty = 5;

    private static readonly SkillArea[] WrittenSkills =
        [SkillArea.Vocabulary, SkillArea.Logic, SkillArea.Reading, SkillArea.Science];

    private static List<Question> Bank(string language) =>
    [
        .. MathQuestionFactory.Build(language),
        .. language == Localized.Azerbaijani ? AzQuestionBank.All() : EnQuestionBank.All()
    ];

    [Theory]
    [InlineData(Localized.Azerbaijani)]
    [InlineData(Localized.English)]
    public void HerBacariqVeCetinlikde_KifayetQederSualVar(string language)
    {
        var bank = Bank(language);

        foreach (var skill in WrittenSkills)
        {
            for (var difficulty = 1; difficulty <= 10; difficulty++)
            {
                var count = bank.Count(q => q.Skill == skill && q.Difficulty == difficulty);

                Assert.True(count >= MinPerDifficulty,
                    $"{language}/{skill}/çətinlik {difficulty}: cəmi {count} sual — ən azı {MinPerDifficulty} lazımdır.");
            }
        }

        // Riyaziyyat generasiya olunur; təkrarlanan mətnlər süzüldüyü üçün
        // hədəfdən bir az az ola bilər, amma xeyli çox olmalıdır.
        for (var difficulty = 1; difficulty <= 10; difficulty++)
        {
            var count = bank.Count(q => q.Skill == SkillArea.Math && q.Difficulty == difficulty);
            Assert.True(count >= 15, $"{language}/Math/çətinlik {difficulty}: cəmi {count} sual.");
        }
    }

    [Theory]
    [InlineData(Localized.Azerbaijani)]
    [InlineData(Localized.English)]
    public void HerSualin_DortVariantiVeIzahiVar(string language)
    {
        foreach (var question in Bank(language))
        {
            Assert.Equal(4, question.Options.Count);
            Assert.Equal(question.Options.Count, question.Options.Distinct().Count());
            Assert.InRange(question.CorrectIndex, 0, question.Options.Count - 1);
            Assert.False(string.IsNullOrWhiteSpace(question.Prompt));
            Assert.False(string.IsNullOrWhiteSpace(question.Explanation));
            Assert.False(string.IsNullOrWhiteSpace(question.Hint));
            Assert.Equal(language, question.LanguageCode);
        }
    }

    /// <summary>
    /// Düzgün cavab həmişə eyni yerdə olsa, uşaq sualı oxumadan naxışı tapır.
    /// Ona görə hər bacarıqda dörd mövqenin hamısı işlənməli və heç biri
    /// yarıdan çoxunu tutmamalıdır.
    /// </summary>
    [Theory]
    [InlineData(Localized.Azerbaijani)]
    [InlineData(Localized.English)]
    public void DuzgunCavabinYeri_BirMovqedeYigilmir(string language)
    {
        foreach (var skill in WrittenSkills)
        {
            var indexes = Bank(language).Where(q => q.Skill == skill).Select(q => q.CorrectIndex).ToList();

            Assert.Equal(4, indexes.Distinct().Count());

            foreach (var position in Enumerable.Range(0, 4))
            {
                var share = indexes.Count(i => i == position) * 100 / indexes.Count;
                Assert.True(share <= 50, $"{language}/{skill}: cavabların {share}%-i {position} mövqeyindədir.");
            }
        }
    }

    [Theory]
    [InlineData(Localized.Azerbaijani)]
    [InlineData(Localized.English)]
    public void EyniSualMetni_BankdaTekrarlanmir(string language)
    {
        var prompts = Bank(language).Select(q => q.Prompt).ToList();

        var duplicates = prompts.GroupBy(p => p, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, $"Təkrarlanan sual: {string.Join(" · ", duplicates.Take(3))}");
    }
}

/// <summary>
/// Seçimin özü: bank böyük olsa da, alqoritm dar seçsə təkrar qalır.
/// Əvvəl "hədəfə ən yaxın çətinlik" sərt sıralama idi — uşaq eyni bir neçə
/// sualı dövrə vururdu.
/// </summary>
public class QuestionSelectionTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public QuestionSelectionTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>
    /// Cavablandırılmış suallar növbəti sessiyada təkrarlanmamalıdır. Oxu
    /// bacarığı qəsdən seçilib: bank böyüməzdən əvvəl orada cəmi 7 sual vardı
    /// və ikinci sessiya demək olar ki, eyni dəsti qaytarırdı.
    /// </summary>
    [Fact]
    public async Task CavablandirilmisSuallar_NovbetiSessiyadaTekrarlanmir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, $"bank-{Guid.NewGuid():N}@petpal.test");

        var first = await StartAsync(client, SkillArea.Reading, 5);

        foreach (var question in first.Questions)
            await AnswerAsync(client, first.SessionId, question);

        var second = await StartAsync(client, SkillArea.Reading, 5);

        var repeated = second.Questions.Select(q => q.Id).Intersect(first.Questions.Select(q => q.Id)).ToList();

        Assert.Empty(repeated);
    }

    /// <summary>Seçilən suallar uşağın səviyyəsinin ətrafında qalmalıdır.</summary>
    [Fact]
    public async Task SecilenSuallar_HedefCetinliyinEtrafindadir()
    {
        var client = await ApiTestClient.CreateAsync(_factory, $"band-{Guid.NewGuid():N}@petpal.test");

        var session = await StartAsync(client, SkillArea.Science, 5);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ids = session.Questions.Select(q => q.Id).ToList();
        var difficulties = await db.Questions.AsNoTracking()
            .Where(q => ids.Contains(q.Id))
            .Select(q => q.Difficulty)
            .ToListAsync();

        // Başlanğıc reytinq 300 → hədəf çətinlik 3; aralıq ən çoxu ±2 genişlənir.
        Assert.All(difficulties, d => Assert.InRange(d, 1, 5));
    }

    private static async Task<LearningSessionDto> StartAsync(ApiTestClient client, SkillArea skill, int count)
    {
        var response = await client.Http.PostAsJsonAsync("/api/learn/sessions",
            new StartSessionRequest { Skill = skill, QuestionCount = count });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<LearningSessionDto>())!;
    }

    private async Task AnswerAsync(ApiTestClient client, Guid sessionId, QuestionDto question)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var correctIndex = await db.Questions.AsNoTracking()
            .Where(q => q.Id == question.Id)
            .Select(q => q.CorrectIndex)
            .FirstAsync();

        var response = await client.Http.PostAsJsonAsync("/api/learn/answers", new SubmitAnswerRequest
        {
            SessionId = sessionId,
            QuestionId = question.Id,
            ChosenIndex = correctIndex,
            ElapsedMs = 1200
        });

        response.EnsureSuccessStatusCode();
    }
}
