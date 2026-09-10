using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>
/// Bilik Arenasının bir dueli — iki uşaq EYNİ sual dəstini EYNİ ANDA oynayır.
///
/// <para>Duel SİNXRONDUR: dəst yalnız rəqib qoşulanda başlayır. Ondan əvvəl
/// duel "rəqib gözləyir" vəziyyətindədir və heç kim cavab verə bilmir — yarış
/// tək başına oynanan bir şey deyil.</para>
///
/// <para>Sual dəsti duelə SNAPSHOT edilir (<see cref="Questions"/>): adaptiv seçim
/// duelin içində işləmir, yoxsa iki uşaq fərqli suallar alar və yarışın mənası itər.</para>
/// </summary>
public class Duel
{
    public Guid Id { get; set; }

    public SkillArea Skill { get; set; }

    /// <summary>Dəstin orta çətinliyi (1–10) — uyğunlaşdırma buna görə də filtrləyir.</summary>
    public int Difficulty { get; set; }

    public DuelStatus Status { get; set; } = DuelStatus.WaitingOpponent;

    /// <summary>Dueli yaradan uşaq. Uyğunlaşdırma "öz duelinə qoşulma" halını bununla kəsir.</summary>
    public Guid CreatedByChildProfileId { get; set; }
    public ChildProfile CreatedByChildProfile { get; set; } = null!;

    /// <summary>
    /// Yaradanın duel anındakı arena reytinqi. Uyğunlaşdırma sorğusu bunu
    /// oxuyur ki, hər namizəd üçün profilə əlavə join lazım gəlməsin.
    /// </summary>
    public int CreatorArenaRating { get; set; }

    /// <summary>
    /// Yaradanın valideyni "yalnız dostlar" açarını qoşubsa <c>true</c>. Açar sonradan
    /// dəyişsə də açıq duelin şərti dəyişməməlidir — ona görə duelin üstündə saxlanılır.
    /// </summary>
    public bool FriendsOnly { get; set; }

    /// <summary>
    /// Duel KONKRET dosta ünvanlanıbsa onun profili. Belə duel adi hovuza
    /// düşmür: yalnız çağırılan uşaq ona qoşula bilər.
    ///
    /// <para>Çağırış adi açıq dueldən yalnız bununla fərqlənir — dəst, geri sayım,
    /// reytinq və mükafat eynidir. Yəni "dostla oyna" ayrıca yarış rejimi deyil,
    /// sadəcə rəqibin necə tapılmasıdır.</para>
    /// </summary>
    public Guid? TargetChildProfileId { get; set; }
    public ChildProfile? TargetChildProfile { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Rəqib tapılmasa duel bu vaxtdan sonra ləğv olunur.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Dəstin BAŞLAMA anı — rəqib qoşulanda qoyulur və hər iki uşaq üçün EYNİDİR.
    /// Doldurulmayıbsa duel hələ başlamayıb və cavab qəbul edilmir.
    ///
    /// <para>Bu an gələcəkdədir: qoşulma anının üstünə geri sayım əlavə olunur
    /// (<see cref="Learning.Arena.ArenaOptions.CountdownSeconds"/>). Beləcə "3-2-1"
    /// ekranı birinci sualın vaxtına yazılmır və hər iki uşaq eyni saniyədə başlayır —
    /// şəbəkə gecikməsi kiminsə hesabına düşmür.</para>
    /// </summary>
    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Məşq dueli: rəqib SÜNİDİR və uşağa açıq şəkildə belə göstərilir.
    /// Bu duel nə arena reytinqinə, nə ulduza, nə də gündəlik həddə toxunur —
    /// süni rəqibdən qazanılan mükafat əsl yarışı dəyərsizləşdirərdi.
    /// </summary>
    public bool IsPractice { get; set; }

    public ICollection<DuelQuestion> Questions { get; set; } = new List<DuelQuestion>();
    public ICollection<DuelEntry> Entries { get; set; } = new List<DuelEntry>();
}

/// <summary>
/// Duelin sual dəstinin bir sətri. Sual MƏTNİ deyil, sual İD-si saxlanılır —
/// bank dəyişsə köhnə nəticələr yenə oxunur.
/// </summary>
public class DuelQuestion
{
    public Guid Id { get; set; }

    public Guid DuelId { get; set; }
    public Duel Duel { get; set; } = null!;

    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    /// <summary>Sıra hər iki uşaq üçün eynidir.</summary>
    public int Order { get; set; }

    /// <summary>
    /// Yalnız MƏŞQ duelində dolur: süni rəqibin bu suala cavabı duel yaradılanda
    /// bir dəfə hesablanır. Sonradan hesablansaydı rəqib uşağın nəticəsinə
    /// uyğunlaşardı — yəni yarış deyil, teatr olardı.
    /// </summary>
    public bool PracticeCorrect { get; set; }

    public int PracticeElapsedMs { get; set; }
}
