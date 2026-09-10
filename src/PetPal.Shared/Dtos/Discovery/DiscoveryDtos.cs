using System.ComponentModel.DataAnnotations;
using PetPal.Shared.Validation;

namespace PetPal.Shared.Dtos.Discovery;

/// <summary>
/// "Real Life Connect": uşaq ekrandan kənarda bir şey tapır (yarpaq, daş, həşərat),
/// app-də qeyd edir və ulduz qazanır.
/// </summary>
public class DiscoveryRequest
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.DiscoveryLabelRequired))]
    [StringLength(60, MinimumLength = 2, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.NameLength60))]
    public string Label { get; set; } = string.Empty;

    [StringLength(40)]
    public string CategoryKey { get; set; } = "nature";

    /// <summary>Şəkil data URI olmadan da göndərilə bilər — foto könüllüdür.</summary>
    public string? PhotoBase64 { get; set; }
}

public class DiscoveryResultDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int StarsEarned { get; set; }
    public bool IsFirstOfKind { get; set; }
    public int TotalDiscoveries { get; set; }
}

public class DiscoveryDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string CategoryKey { get; set; } = string.Empty;

    /// <summary>
    /// Şəklin ÖZÜ siyahıda gəlmir: 30 MB-lıq fayl hər sətrə qoşulsaydı,
    /// kolleksiyanı açmaq onlarla meqabayt çəkərdi. Klient burada yalnız şəklin
    /// olub-olmadığını görür və istəyəndə onu <c>…/photo</c> ünvanından alır.
    /// Serverdəki fayl yolu qəsdən verilmir — o, klientin işinə yaramır.
    /// </summary>
    public bool HasPhoto { get; set; }

    public int StarsEarned { get; set; }
    public DateTime CreatedAt { get; set; }
}
