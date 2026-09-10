using Microsoft.EntityFrameworkCore;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.Learning;

public class QuestionSelector : IQuestionSelector
{
    /// <summary>Son bu qədər cavabda görünən suallar təkrarlanmır.</summary>
    private const int RecentAnswerWindow = 40;

    private readonly AppDbContext _db;

    public QuestionSelector(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Question>> SelectAsync(
        Guid childId, SkillArea skill, string languageCode, int rating, int age, int count, CancellationToken ct = default)
    {
        var target = AdaptiveEngine.TargetDifficulty(rating);
        var language = Localized.Normalize(languageCode);

        var recentQuestionIds = await _db.SessionAnswers
            .AsNoTracking()
            .Where(a => a.LearningSession.ChildProfileId == childId && a.AnsweredAt != null)
            .OrderByDescending(a => a.AnsweredAt)
            .Take(RecentAnswerWindow)
            .Select(a => a.QuestionId)
            .ToListAsync(ct);

        // ARENA cavabları da "yaxınlarda görülən" sayılır. Onlar ayrı cədvəldədir
        // (DuelAnswers), ona görə əvvəl bura düşmürdü və nəticədə uşaq dueldə
        // gördüyü sualı növbəti dueldə yenidən alırdı — yarışın hər dəfə eyni
        // görünməsinin əsas səbəbi bu idi.
        var recentDuelQuestionIds = await _db.DuelAnswers
            .AsNoTracking()
            .Where(a => a.DuelEntry.ChildProfileId == childId && a.AnsweredAt != null)
            .OrderByDescending(a => a.AnsweredAt)
            .Take(RecentAnswerWindow)
            .Select(a => a.QuestionId)
            .ToListAsync(ct);

        var recent = recentQuestionIds.Concat(recentDuelQuestionIds).ToHashSet();

        var pool = await _db.Questions
            .AsNoTracking()
            .Where(q => q.IsActive && q.Skill == skill && q.MinAge <= age && q.LanguageCode == language)
            .ToListAsync(ct);

        // Bu dildə hələ məzmun yoxdursa, uşağı boş ekranla qarşılamırıq —
        // ingilis bankına düşürük (bax docs/ADAPTIVE_LEARNING.md).
        if (pool.Count == 0 && language != Localized.English)
        {
            pool = await _db.Questions
                .AsNoTracking()
                .Where(q => q.IsActive && q.Skill == skill && q.MinAge <= age && q.LanguageCode == Localized.English)
                .ToListAsync(ct);
        }

        if (pool.Count == 0)
            return [];

        var rng = Random.Shared;

        // Çətinlik SƏRT sıralama deyil, ARALIQ olmalıdır. Əvvəl "hədəfə ən yaxın"
        // sualı həmişə qazanırdı: reytinq 300 olan uşaq eyni bir neçə sualı
        // dövrə vurub yenidən görürdü. İndi hədəfin ±1-i bir səbətdir və seçim
        // onun içindən təsadüfidir — bu, çətinliyi saxlayır, təkrarı isə açır.
        var band = Band(pool, target, 1);

        // Səbətdə bir sessiyalıq namizəd yoxdursa aralıq genişlənir, lazım gəlsə
        // bütün bank işə düşür — uşaq "sual qurtardı" ekranı görməməlidir.
        if (band.Count < count * 3)
            band = Band(pool, target, 2);

        if (band.Count < count)
            band = pool;

        // Yaxınlarda görülənlər sadəcə arxaya atılmır, TAM KƏNARDA qalır — o
        // qədər ki, təzə sual çatır. Bank kiçik olanda köhnəyə qayıdılır.
        var fresh = band.Where(q => !recent.Contains(q.Id)).ToList();
        var source = fresh.Count >= count ? fresh : band;

        var chosen = source
            .OrderBy(_ => rng.Next())
            .Take(count)
            .ToList();

        // Sual sırası asandan çətinə doğru — uşaq isinişməmiş ən çətinlə qarşılaşmasın.
        return chosen.OrderBy(q => q.Difficulty).ToList();
    }

    /// <summary>Hədəf çətinliyin ətrafındakı səbət — seçim onun içindən edilir.</summary>
    private static List<Question> Band(List<Question> pool, int target, int spread) =>
        pool.Where(q => Math.Abs(q.Difficulty - target) <= spread).ToList();
}
