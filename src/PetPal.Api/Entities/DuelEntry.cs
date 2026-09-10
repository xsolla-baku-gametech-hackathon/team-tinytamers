using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Bir uşağın duel qeydi — duelə iki dənə olur (yaradan və qoşulan).
///
/// <para>Vaxt SERVERDƏ ölçülür: klientin göndərdiyi müddət qəbul edilmir, çünki
/// bərabərlik məhz sürətlə həll olunur və klient etibarsızdır. Bax
/// <see cref="DuelAnswer.ElapsedMs"/>.</para>
/// </summary>
public class DuelEntry
{
    public Guid Id { get; set; }

    public Guid DuelId { get; set; }
    public Duel Duel { get; set; } = null!;

    public Guid ChildProfileId { get; set; }
    public ChildProfile ChildProfile { get; set; } = null!;

    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public int CorrectCount { get; set; }

    /// <summary>Bütün cavabların serverdə ölçülmüş cəmi — bərabərliyi bu həll edir.</summary>
    public int TotalMilliseconds { get; set; }

    /// <summary>Duel tamamlananda yazılır — nəticə ekranı reytinq dəyişimini göstərir.</summary>
    public int ArenaRatingBefore { get; set; }
    public int ArenaRatingAfter { get; set; }

    /// <summary>Duel tamamlananda yazılır — statistika və nəticə ekranı bunu oxuyur.</summary>
    public DuelOutcome Outcome { get; set; } = DuelOutcome.Pending;

    /// <summary>Duel tamamlananda verilən ulduz. Uduzmaq HEÇ VAXT mənfi olmur.</summary>
    public int StarsEarned { get; set; }

    /// <summary>
    /// <see cref="StarsEarned"/>-in sürət bonusu hissəsi (cəmin İÇİNDƏDİR, üstünə
    /// gəlmir). Ayrıca saxlanılır ki, nəticə ekranı "20 + 6" ayrımını sonradan
    /// yenidən hesablamadan — yəni ayarlar dəyişsə də düz — göstərə bilsin.
    /// </summary>
    public int SpeedBonusStars { get; set; }

    /// <summary>
    /// Uşaq nəticəni görübsə dolur. "Duelin nəticəsi hazırdır" nişanı buna baxır —
    /// birinci oynayan uşaq nəticəni dərhal görmür, çünki rəqib hələ oynamayıb.
    /// </summary>
    public DateTime? ResultSeenAt { get; set; }

    public ICollection<DuelAnswer> Answers { get; set; } = new List<DuelAnswer>();

    public bool IsFinished => FinishedAt.HasValue;
}

/// <summary>
/// Uşağın duel daxilindəki bir cavabı. Duel başlayanda bütün sətirlər cavabsız
/// yaradılır — beləliklə uşaq dəstdə olmayan sualı cavablaya bilmir.
/// </summary>
public class DuelAnswer
{
    public Guid Id { get; set; }

    public Guid DuelEntryId { get; set; }
    public DuelEntry DuelEntry { get; set; } = null!;

    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    public int Order { get; set; }

    public int ChosenIndex { get; set; } = -1;
    public bool IsCorrect { get; set; }

    /// <summary>
    /// Serverdə ölçülmüş müddət: bu cavabın gəlmə anı ilə əvvəlki cavabın
    /// (ilk sualda isə dəstin başlama anının) arasındakı fərq.
    /// </summary>
    public int ElapsedMs { get; set; }

    public DateTime? AnsweredAt { get; set; }

    public bool IsAnswered => AnsweredAt.HasValue;
}
