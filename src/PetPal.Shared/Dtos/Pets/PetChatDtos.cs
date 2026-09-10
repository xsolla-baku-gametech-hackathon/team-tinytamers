using System.ComponentModel.DataAnnotations;
using PetPal.Shared.Validation;

namespace PetPal.Shared.Dtos.Pets;

/// <summary>Uşağın pet-ə yazdığı mesaj.</summary>
public class PetChatRequest
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.ChatMessageRequired))]
    [StringLength(200, MinimumLength = 1, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.ChatMessageLength))]
    public string Message { get; set; } = string.Empty;
}

/// <summary>Pet-in cavabı.</summary>
public class PetChatReplyDto
{
    public string Reply { get; set; } = string.Empty;

    /// <summary>
    /// Cavab modeldən gəldi (<c>true</c>), yoxsa qayda əsaslı replikadır (<c>false</c>).
    /// Uşaq bunu görmür — diaqnostika və valideyn paneli üçündür.
    /// </summary>
    public bool FromAi { get; set; }

    /// <summary>Bu gün qalan mesaj sayı. Sıfır olanda yazı sahəsi bağlanır.</summary>
    public int MessagesLeftToday { get; set; }
}

public class PetChatTurnDto
{
    public Guid Id { get; set; }
    public bool FromChild { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Söhbət otağı açılanda yüklənən vəziyyət.</summary>
public class PetChatStateDto
{
    /// <summary>Valideyn söhbəti açıbmı. Bağlıdırsa uşaq otağa girə bilmir.</summary>
    public bool Enabled { get; set; }

    public int MessagesLeftToday { get; set; }
    public int MessagesPerDay { get; set; }

    /// <summary>Ən köhnədən ən yeniyə — ekranda elə də göstərilir.</summary>
    public List<PetChatTurnDto> Turns { get; set; } = [];
}
