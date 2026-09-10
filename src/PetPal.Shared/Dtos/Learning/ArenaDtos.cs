using System.ComponentModel.DataAnnotations;
using PetPal.Shared.Dtos.Rewards;
using PetPal.Shared.Enums;
using PetPal.Shared.Validation;

namespace PetPal.Shared.Dtos.Learning;

/// <summary>
/// Bilik Arenası — Öyrən bölməsindəki uşaq-uşaq yarışı.
///
/// <para>Sosial qapalı dövrə burada da qüvvədədir: sərbəst mətn yoxdur,
/// tanımadığı rəqibin əsl adı görünmür və uşaq uşağı axtara bilmir — rəqibi
/// yalnız server seçir. Məhz buna görə rəqib hovuzu HAMIDIR, yalnız dostlar deyil.</para>
/// </summary>
public class ArenaStatusDto
{
    /// <summary>Valideyn açarı — bağlıdırsa arena ümumiyyətlə işləmir.</summary>
    public bool Enabled { get; set; }

    /// <summary>Valideyn hovuzu dostlarla məhdudlaşdırıbsa <c>true</c> (standart: bağlı).</summary>
    public bool FriendsOnly { get; set; }

    public int Rating { get; set; }

    /// <summary>Gündəlik duel həddi. <b>0 — hədd yoxdur</b>; belə halda <see cref="DuelsLeftToday"/> oxunmur.</summary>
    public int DuelsPerDay { get; set; }

    public int DuelsLeftToday { get; set; }

    /// <summary>Yarımçıq qalmış duel — uşaq ona qayıda bilər.</summary>
    public Guid? ActiveDuelId { get; set; }

    /// <summary>Nəticəsi hazır, uşağın hələ görmədiyi duellər — "nəticə hazırdır" nişanı.</summary>
    public List<Guid> PendingResultDuelIds { get; set; } = new();

    /// <summary>
    /// Dostlardan gələn və hələ cavablanmamış çağırışlar. Real vaxt kanalı
    /// olmasa da ekran bunları görür — kanal sürət verir, məlumatı yox.
    /// </summary>
    public List<ArenaChallengeDto> IncomingChallenges { get; set; } = new();

    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
}

/// <summary>
/// Rəqibin uşağa göstərilən üzü. <see cref="DisplayName"/> YALNIZ dost olan
/// rəqibdə dolur — tanımadığı uşağın əsl adı heç vaxt göstərilmir.
/// </summary>
public class ArenaOpponentDto
{
    public string? DisplayName { get; set; }
    public string PetName { get; set; } = string.Empty;
    public string AvatarKey { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsFriend { get; set; }

    /// <summary>Süni məşq rəqibi — ekranda AÇIQ şəkildə belə işarələnməlidir.</summary>
    public bool IsPractice { get; set; }
}

public class ArenaDuelDto
{
    public Guid DuelId { get; set; }
    public SkillArea Skill { get; set; }
    public int Difficulty { get; set; }
    public DuelStatus Status { get; set; }

    /// <summary>Dəst hər iki uşaq üçün eynidir və duel yaradılanda dondurulur.</summary>
    public List<QuestionDto> Questions { get; set; } = new();

    public int AnsweredCount { get; set; }

    /// <summary>
    /// Dəstin başlamasına qalan millisaniyə. <c>null</c> — rəqib hələ tapılmayıb,
    /// yəni yarış BAŞLAMAYIB və cavab qəbul edilmir. 0 — dəst gedir.
    ///
    /// <para>Mütləq vaxt yox, QALIQ göndərilir: uşağın cihazının saatı serverinki
    /// ilə üst-üstə düşməyə bilər. Hər iki uşaq öz qalığını alır və sayğaclar
    /// fərqli rəqəmdən başlasa da eyni anda bitir.</para>
    /// </summary>
    public int? StartsInMilliseconds { get; set; }

    /// <summary>Rəqib hələ qoşulmayıbsa <c>null</c>.</summary>
    public ArenaOpponentDto? Opponent { get; set; }

    public bool OpponentFinished { get; set; }

    /// <summary>Uşaq bu duelin sual dəstini artıq bitiribsə <c>true</c>.</summary>
    public bool Finished { get; set; }

    /// <summary>Məşq dueli: mükafat və reytinq yoxdur, rəqib süni.</summary>
    public bool IsPractice { get; set; }
}

public class SubmitDuelAnswerRequest
{
    [Required]
    public Guid DuelId { get; set; }

    [Required]
    public Guid QuestionId { get; set; }

    [Range(0, 5, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.AnswerChoiceRange))]
    public int ChosenIndex { get; set; }
}

public class DuelAnswerResultDto
{
    public bool IsCorrect { get; set; }
    public int CorrectIndex { get; set; }
    public string Explanation { get; set; } = string.Empty;

    public int AnsweredCount { get; set; }
    public int TotalCount { get; set; }

    /// <summary>Uşaq dəsti bitirdi (rəqib bitirmiş olmaya bilər).</summary>
    public bool Finished { get; set; }

    /// <summary>Hər iki tərəf bitiribsə nəticə dərhal buradadır; əks halda <c>null</c>.</summary>
    public ArenaDuelResultDto? Result { get; set; }

    public string PetReaction { get; set; } = string.Empty;
}

/// <summary>Nəticə ekranı — rəqibin cavabları sual-sual yanaşı göstərilir.</summary>
public class ArenaDuelResultDto
{
    public Guid DuelId { get; set; }
    public SkillArea Skill { get; set; }
    public DuelStatus Status { get; set; }
    public DuelOutcome Outcome { get; set; }

    public int TotalCount { get; set; }

    public int MyCorrectCount { get; set; }
    public int MyTotalMilliseconds { get; set; }

    public int OpponentCorrectCount { get; set; }
    public int OpponentTotalMilliseconds { get; set; }

    public ArenaOpponentDto? Opponent { get; set; }

    public int StarsEarned { get; set; }

    /// <summary>
    /// <see cref="StarsEarned"/>-in sürət bonusu hissəsi — cəmin İÇİNDƏDİR.
    /// Uşaq "niyə bu dəfə daha çox ulduz aldım?" sualının cavabını ekranda
    /// görməlidir, ona görə ayrıca gəlir.
    /// </summary>
    public int SpeedBonusStars { get; set; }

    public int RatingBefore { get; set; }
    public int RatingAfter { get; set; }

    /// <summary>
    /// Nəticəni SÜRƏT həll etdi: doğru cavab sayı bərabər idi, qalibi vaxt seçdi.
    /// Belə halda hesab lövhəsində 3–3 yazılır və uşaq udduğunu ANLAMIR — ekran
    /// bunu açıq sözlə deməlidir.
    /// </summary>
    public bool DecidedBySpeed { get; set; }

    /// <summary>Məşq dueli: ulduz və reytinq dəyişmir.</summary>
    public bool IsPractice { get; set; }

    public List<DuelQuestionResultDto> Questions { get; set; } = new();
    public List<BadgeDto> NewBadges { get; set; } = new();

    public string PetMessage { get; set; } = string.Empty;
}

/// <summary>
/// Həftəlik liqa. Cədvəl bazar ertəsi UTC 00:00-da sıfırlanır.
///
/// <para>Qapalı dövrə qaydası burada da qüvvədədir: ümumi siyahıda uşaqlar
/// YALNIZ pet adı və avatarla görünür; əsl ad ancaq dostlarda (və uşağın öz
/// sətrində) olur.</para>
/// </summary>
public class ArenaLeagueDto
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }

    /// <summary>Uşağın yeri; bu həftə duel oynamayıbsa 0.</summary>
    public int MyRank { get; set; }
    public int MyPoints { get; set; }
    public int MyDuels { get; set; }

    /// <summary>İlk 20 sətir. Uşaq siyahıdan kənardadırsa öz sətri sona əlavə olunur.</summary>
    public List<ArenaLeagueRowDto> Rows { get; set; } = new();
}

public class ArenaLeagueRowDto
{
    public int Rank { get; set; }

    /// <summary>Yalnız dostda və uşağın öz sətrində dolur.</summary>
    public string? DisplayName { get; set; }

    public string PetName { get; set; } = string.Empty;
    public string AvatarKey { get; set; } = string.Empty;
    public int Level { get; set; }

    public int Points { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }

    public bool IsMe { get; set; }
    public bool IsFriend { get; set; }
}

public class DuelQuestionResultDto
{
    public int Order { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public int CorrectIndex { get; set; }

    public int MyChosenIndex { get; set; } = -1;
    public bool MyCorrect { get; set; }
    public int MyElapsedMs { get; set; }

    public int OpponentChosenIndex { get; set; } = -1;
    public bool OpponentCorrect { get; set; }
    public int OpponentElapsedMs { get; set; }
}

/// <summary>
/// Dostdan gələn birbaşa çağırış. Adi açıq dueldən yalnız rəqibin necə
/// tapılması ilə fərqlənir — dəst, geri sayım və mükafat eynidir.
/// </summary>
public class ArenaChallengeDto
{
    public Guid DuelId { get; set; }

    /// <summary>Çağıran dostun ƏSL adı — çağırış yalnız təsdiqlənmiş dostdan gəlir.</summary>
    public string FromName { get; set; } = string.Empty;

    public string AvatarKey { get; set; } = string.Empty;

    /// <summary>Bu andan sonra çağırış özü sönür — dost ekran qarşısında gözləyir.</summary>
    public DateTime ExpiresAt { get; set; }
}
