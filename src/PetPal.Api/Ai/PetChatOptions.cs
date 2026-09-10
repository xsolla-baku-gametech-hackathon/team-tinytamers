namespace PetPal.Api.Ai;

/// <summary>
/// Söhbət özəlliyinin hədləri. AI provider ayarları <see cref="AiOptions"/>-dadır —
/// bunlar isə provider-dən asılı olmayan məhsul qərarlarıdır.
/// </summary>
public class PetChatOptions
{
    public const string SectionName = "PetChat";

    /// <summary>
    /// Uşağın gündəlik mesaj həddi. Həm ekran vaxtı, həm də pulsuz API kvotası
    /// üçündür: pulsuz səviyyədə gündə ~1000 sorğu var, yəni bir uşaq bütün
    /// kvotanı yeyə bilməməlidir.
    /// </summary>
    public int MessagesPerDay { get; set; } = 30;

    /// <summary>
    /// Modelə verilən keçmiş replikaların sayı (uşaq + pet birlikdə).
    /// Qısa saxlanılır: hər replika token yeyir, pulsuz səviyyədə isə
    /// dəqiqəlik token limiti əsl darboğazdır.
    /// </summary>
    public int HistoryTurns { get; set; } = 6;

    /// <summary>Uşağın bir mesajının maksimum uzunluğu.</summary>
    public int MaxMessageLength { get; set; } = 200;

    /// <summary>Valideyn panelində göstərilən son replika sayı.</summary>
    public int ParentHistoryLimit { get; set; } = 200;
}
