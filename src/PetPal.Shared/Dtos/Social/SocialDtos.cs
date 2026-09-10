using System.ComponentModel.DataAnnotations;
using PetPal.Shared.Enums;
using PetPal.Shared.Validation;

namespace PetPal.Shared.Dtos.Social;

/// <summary>
/// Sosial hissə qapalı dövrədir: uşaqlar yalnız dost kodu ilə tanış olur,
/// sərbəst axtarış və mətn yazışması yoxdur. Kod yazmaq dostluğu QURMUR —
/// kodun sahibi onu özü qəbul edənə qədər sorğu gözləyir.
/// </summary>
public class FriendDto
{
    public Guid ChildId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarKey { get; set; } = string.Empty;
    public string PetName { get; set; } = string.Empty;
    public int Level { get; set; }

    /// <summary>
    /// Dost hazırda app-dədir. Yarış sinxrondur — kimin oynaya biləcəyini
    /// bilmək «boş hovuz» hissini aradan qaldırır. Yaddaşda saxlanılır,
    /// bazaya yazılmır.
    /// </summary>
    public bool IsOnline { get; set; }
}

/// <summary>Təsdiq gözləyən dostluq sorğusu — uşağın öz ekranı üçün.</summary>
public class PendingFriendDto
{
    public Guid ChildId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarKey { get; set; } = string.Empty;

    /// <summary>
    /// Sorğunu bu uşaq göndərib (yəni kodu O yazıb). <c>false</c> olanda sorğu
    /// bizə gəlib və cavabı BİZ özümüz veririk.
    /// </summary>
    public bool IsOutgoing { get; set; }

    public DateTime RequestedAt { get; set; }
}

/// <summary>
/// Dostlar ekranının tək sorğuluq mənzərəsi. İki siyahı bir yerdədir ki,
/// mobil klient ekranı açanda iki dəfə şəbəkəyə çıxmasın.
/// </summary>
public class FriendsViewDto
{
    public List<FriendDto> Friends { get; set; } = new();
    public List<PendingFriendDto> Pending { get; set; } = new();
}

/// <summary>
/// Uşağın gələn dostluq sorğusuna cavabı. Sorğunu göndərən marşrutda gedir,
/// ona görə gövdədə yalnız qərar qalır.
/// </summary>
public class FriendRequestDecision
{
    /// <summary><c>true</c> — dostluq işə düşür; <c>false</c> — sorğu silinir.</summary>
    public bool Approve { get; set; }
}

public class AddFriendRequest
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FriendCodeRequired))]
    [RegularExpression("^[A-Z0-9]{6}$", ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.FriendCodeFormat))]
    public string FriendCode { get; set; } = string.Empty;
}

public class TeamMissionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int Target { get; set; }
    public int Progress { get; set; }
    public int RewardStars { get; set; }
    public MissionStatus Status { get; set; }

    /// <summary>Bu uşağın həmin missiyadakı vəziyyəti — dəvət gözləyir, yoxsa qoşulub.</summary>
    public TeamMemberStatus MyStatus { get; set; }

    /// <summary>Missiyanı başladan uşağın adı — dəvət kartında «kim çağırır» sualı üçün.</summary>
    public string StartedBy { get; set; } = string.Empty;

    public List<TeamMemberDto> Members { get; set; } = new();
}

public class TeamMemberDto
{
    public Guid ChildId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarKey { get; set; } = string.Empty;
    public int Contribution { get; set; }
    public TeamMemberStatus Status { get; set; }
}
