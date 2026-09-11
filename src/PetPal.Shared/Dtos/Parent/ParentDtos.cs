using System.ComponentModel.DataAnnotations;
using PetPal.Shared.Dtos.Progress;
using PetPal.Shared.Validation;

namespace PetPal.Shared.Dtos.Parent;

public class DailyActivityPointDto
{
    public DateOnly Date { get; set; }
    public int TasksDone { get; set; }
    public int Minutes { get; set; }
    public int AccuracyPercent { get; set; }
}

public class ScreenTimeSettingsDto
{
    [Range(1, 50, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.DailyGoalRange))]
    public int DailyGoalTarget { get; set; } = 5;

    [Range(5, 240, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.ScreenLimitRange))]
    public int DailyMinutesLimit { get; set; } = 30;

    [Range(0, 23, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.HourRange))]
    public int BedtimeStartHour { get; set; } = 21;

    [Range(0, 23, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.HourRange))]
    public int BedtimeEndHour { get; set; } = 7;
}

/// <summary>
/// Uşağın dili. App-in bütün mətni bundan asılıdır — suallardan tutmuş
/// düymələrə qədər — ona görə seçim profil yaradılanda bitmir: valideyn onu
/// sonra da dəyişə bilməlidir (ailə dil öyrənir, uşaq böyüyür).
/// </summary>
public class ChildLanguageRequest
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.LanguageChoice))]
    [RegularExpression("^(az|en)$", ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.LanguageChoice))]
    public string LanguageCode { get; set; } = "az";
}

/// <summary>Parent View: proqres, bacarıq analitikası və ekran vaxtı balansı.</summary>
public class ParentDashboardDto
{
    public Guid ChildId { get; set; }
    public string ChildDisplayName { get; set; } = string.Empty;

    /// <summary>Uşağın dili — valideyn onu elə bu ekrandan dəyişir.</summary>
    public string LanguageCode { get; set; } = "az";
    public string AvatarKey { get; set; } = string.Empty;

    public int TasksDoneToday { get; set; }
    public int AccuracyPercentToday { get; set; }
    public int MinutesToday { get; set; }
    public int StreakDays { get; set; }

    /// <summary>
    /// Uşağın dost kodu. Valideyn panelindədir, çünki dəvəti PAYLAŞAN
    /// valideyndir — uşaq ekranında kod yalnız göstərilir.
    /// </summary>
    public string FriendCode { get; set; } = string.Empty;

    // Nailiyyət kartının məzmunu. Kartda uşağın əsl adı YOXDUR — yalnız pet adı.
    public string PetName { get; set; } = string.Empty;
    public int PetLevel { get; set; } = 1;
    public int Stars { get; set; }
    public int BadgeCount { get; set; }


    /// <summary>Pet ilə söhbət açıqdırmı — valideyn onu elə bu ekrandan idarə edir.</summary>
    public bool ChatEnabled { get; set; }

    /// <summary>Paltar otağının AI dizayn studiyası açıqdırmı (standart: bağlı).</summary>
    public bool WardrobeAiEnabled { get; set; }

    /// <summary>Bilik Arenası açıqdırmı (standart: açıq).</summary>
    public bool ArenaEnabled { get; set; }

    /// <summary>Arena rəqibləri yalnız dostlardan seçilsinmi (standart: bağlı).</summary>
    public bool ArenaFriendsOnly { get; set; }

    public List<SkillProgressDto> Skills { get; set; } = new();
    public List<DailyActivityPointDto> Last7Days { get; set; } = new();
    public ScreenTimeSettingsDto ScreenTime { get; set; } = new();

    /// <summary>Uşağa özəl qısa müşahidələr (məs. "Logic sahəsi bu həftə 12% yaxşılaşıb").</summary>
    public List<string> Insights { get; set; } = new();
}

/// <summary>
/// Söhbət açarı. Standart olaraq BAĞLIDIR: uşağın yazdığı mətn model
/// serverinə gedir, ona görə bunu yalnız valideyn aça bilər.
/// </summary>
public class ChatSettingsRequest
{
    public bool Enabled { get; set; }
}

/// <summary>
/// Arena açarları.
///
/// <para><see cref="Enabled"/> standart olaraq AÇIQDIR: arenada sərbəst mətn
/// yoxdur və üçüncü tərəf serverə heç nə getmir — söhbətdən fərqli olaraq
/// burada dostluq şərt deyil.</para>
///
/// <para><see cref="FriendsOnly"/> isə standart olaraq BAĞLIDIR: rəqib hovuzu
/// hamıdır. Bu açar valideynin könüllü daralmasıdır — qoşulsa, uşaq yalnız
/// qəbul edilmiş dostlarla yarışır və dost yoxdursa arena boş qala bilər.</para>
/// </summary>
public class ArenaSettingsRequest
{
    public bool Enabled { get; set; } = true;

    public bool FriendsOnly { get; set; }
}

/// <summary>
/// Valideyn baxışında bir söhbət replikası.
///
/// Uşaq baxışından fərqi <see cref="BlockedReason"/> sütunudur: filtrin
/// saxladığı mesaj da burada görünür və valideyn pet-in niyə "başqa şeydən
/// danışaq" dediyini anlayır.
/// </summary>
public class ParentChatTurnDto
{
    public Guid Id { get; set; }
    public bool FromChild { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>Pet replikası modeldən gəldi, yoxsa qayda əsaslı mətndir.</summary>
    public bool FromAi { get; set; }

    /// <summary>Filtrin saxlama səbəbi; normal replikalarda <c>null</c>.</summary>
    public string? BlockedReason { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class ParentChatLogDto
{
    public bool Enabled { get; set; }
    public int MessagesToday { get; set; }
    public int MessagesPerDay { get; set; }

    /// <summary>Ən yenidən ən köhnəyə — valideyn son söhbəti ilk görür.</summary>
    public List<ParentChatTurnDto> Turns { get; set; } = new();
}

// Dostluq sorğusu DTO-ları burada DEYİL: qərar uşağındır, ona görə onlar
// Social DTO-larındadır (FriendRequestDecision, PendingFriendDto).

/// <summary>Valideyn qapısının vəziyyəti — PIN qurulubmu.</summary>
public class ParentGateStatusDto
{
    public bool HasPin { get; set; }
}

/// <summary>
/// PIN-in qurulması və ya dəyişdirilməsi. Kimlik ya hesab PAROLU, ya da cari
/// PIN ilə təsdiqlənir — birincisi PIN unudulanda bərpa yoludur.
/// </summary>
public class SetParentPinRequest
{
    public string? Password { get; set; }

    public string? CurrentPin { get; set; }

    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PinRequired))]
    [RegularExpression("^[0-9]{4}$", ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PinFormat))]
    public string NewPin { get; set; } = string.Empty;
}

/// <summary>Qapının açılması üçün PIN.</summary>
public class ParentGateUnlockRequest
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PinRequired))]
    [RegularExpression("^[0-9]{4}$", ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PinFormat))]
    public string Pin { get; set; } = string.Empty;
}
