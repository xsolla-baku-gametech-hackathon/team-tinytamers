using PetPal.Shared.Enums;

namespace PetPal.Api.Entities;

/// <summary>Uşaq profili — bütün oyun/öyrənmə vəziyyəti bu obyektin ətrafında qurulur.</summary>
public class ChildProfile
{
    public Guid Id { get; set; }

    public Guid ParentUserId { get; set; }
    public ApplicationUser ParentUser { get; set; } = null!;

    public string DisplayName { get; set; } = string.Empty;
    public string AvatarKey { get; set; } = "avatar-fox";
    public int Age { get; set; } = 8;

    /// <summary>Sual bankı və pet replikaları bu dildə gəlir ("az" / "en").</summary>
    public string LanguageCode { get; set; } = "az";

    /// <summary>
    /// Cihazın UTC-dən fərqi (dəqiqə). Yuxu rejimi uşağın yerli saatına görə
    /// hesablanmalıdır — server UTC-də işləyir.
    /// </summary>
    public int UtcOffsetMinutes { get; set; }

    /// <summary>Profil seçimi üçün 4 rəqəmli PIN-in hash-i.</summary>
    public string PinHash { get; set; } = string.Empty;

    /// <summary>Dostluq üçün 6 simvolluq kod — sərbəst axtarış yoxdur, yalnız kod ilə əlavə olunur.</summary>
    public string FriendCode { get; set; } = string.Empty;

    public int Stars { get; set; }
    public int Gems { get; set; }

    public int StreakDays { get; set; }
    public DateOnly? LastActiveOn { get; set; }

    /// <summary>Ümumi qulluq əməliyyatlarının sayı — nişan qaydaları üçün.</summary>
    public int CareActionCount { get; set; }

    /// <summary>
    /// Ulduzla açılmış mini oyunların açarları. Pulsuz oyunlar burada saxlanılmır —
    /// onlar kataloqda <c>UnlockStarCost = 0</c> ilə işarələnir.
    /// </summary>
    public List<string> UnlockedGames { get; set; } = new();

    // Valideyn tənzimləmələri
    public int DailyGoalTarget { get; set; } = 5;
    public int DailyMinutesLimit { get; set; } = 30;
    public int BedtimeStartHour { get; set; } = 21;
    public int BedtimeEndHour { get; set; } = 7;

    /// <summary>
    /// Pet ilə söhbət. Standart olaraq BAĞLIDIR — uşağın yazdığı mətn model
    /// serverinə gedir, ona görə bunu yalnız valideyn aça bilər.
    /// </summary>
    public bool ChatEnabled { get; set; }

    /// <summary>
    /// Bilik Arenası (uşaq-uşaq yarışı). Söhbətdən fərqli olaraq standart
    /// AÇIQDIR: arenada sərbəst mətn yoxdur, üçüncü tərəf serverə heç nə getmir.
    /// </summary>
    public bool ArenaEnabled { get; set; } = true;

    /// <summary>
    /// Rəqib hovuzunu yalnız dostlara daraldır. Standart BAĞLIDIR — yəni uşaq
    /// bütün istifadəçilər arasından uyğunlaşdırılır. Tanımadığı rəqib onsuz da
    /// anonimdir (yalnız pet adı + avatar + səviyyə), dostu olmayan uşaq isə
    /// arenanı boş görməməlidir.
    /// </summary>
    public bool ArenaFriendsOnly { get; set; }

    /// <summary>
    /// Yarış reytinqi. Öyrənmə reytinqindən (<see cref="SkillMastery.Rating"/>)
    /// QƏSDƏN ayrıdır: yarışda uşaq tələsir və səhv edir, bu isə növbəti günün
    /// adaptiv sual seçimini korlayardı.
    /// </summary>
    public int ArenaRating { get; set; } = 300;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Pet? Pet { get; set; }
    public ICollection<SkillMastery> SkillMasteries { get; set; } = new List<SkillMastery>();
    public ICollection<LearningSession> Sessions { get; set; } = new List<LearningSession>();
    public ICollection<DailyGoal> DailyGoals { get; set; } = new List<DailyGoal>();
    public ICollection<RewardEntry> Rewards { get; set; } = new List<RewardEntry>();
    public ICollection<ChildBadge> Badges { get; set; } = new List<ChildBadge>();
    public ICollection<ChildMission> Missions { get; set; } = new List<ChildMission>();
    public ICollection<Discovery> Discoveries { get; set; } = new List<Discovery>();
    public ICollection<ChatTurn> ChatTurns { get; set; } = new List<ChatTurn>();
    public ICollection<DuelEntry> DuelEntries { get; set; } = new List<DuelEntry>();

    // ---------- Pet Brain ----------

    /// <summary>
    /// Pet-in SAXLANAN yoldaşlıq xarakteri.
    ///
    /// <para>Əvvəllər xarakter hər sorğuda xassələrdən yenidən çıxarılırdı və
    /// əvvəlki etiket ötürülmədiyi üçün <see cref="PetBrain.CompanionPersonality"/>
    /// histerezisi FAKTİKİ İŞLƏMİRDİ: iki yaxın bal arasında pet «fikrini
    /// dəyişən» görünürdü. İndi keçid yalnız aydın fərqlə baş verir, çünki
    /// müqayisə ediləcək əvvəlki etiket buradadır.</para>
    ///
    /// <para>Bu sahə həqiqətin MƏNBƏYİ deyil — xassələr elədir. O, yalnız
    /// keçidin yaddaşıdır və mükafata, tapmacaya, çətinliyə toxunmur.</para>
    /// </summary>
    public PetBrainPersonality Personality { get; set; } = PetBrainPersonality.Balanced;

    /// <summary>Xarakter sonuncu dəfə nə vaxt dəyişdi — nümayiş və audit üçün.</summary>
    public DateTime? PersonalityChangedAt { get; set; }

    public ICollection<PlayerTrait> Traits { get; set; } = new List<PlayerTrait>();
    public ICollection<BehaviorEvent> BehaviorEvents { get; set; } = new List<BehaviorEvent>();
    public ICollection<PetMemory> Memories { get; set; } = new List<PetMemory>();
    public ICollection<ExperienceRun> ExperienceRuns { get; set; } = new List<ExperienceRun>();
}
