namespace PetPal.Api.Notifications;

/// <summary>
/// Push bildirişlərinin ayarları.
///
/// <para><b>Standart olaraq SÖNÜKDÜR.</b> Konfiqurasiya olmadan app tam işləyir —
/// bildiriş qatı sadəcə heç nə göndərmir. Bu, layihənin digər könüllü
/// qatları (AI dialoqu, söhbət) ilə eyni qaydadır: xarici xidmət yoxdursa
/// özəllik itir, app sınmır.</para>
/// </summary>
public class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Açar bağlıdırsa heç bir bildiriş göndərilmir.</summary>
    public bool Enabled { get; set; }

    /// <summary>Firebase layihəsinin id-si (FCM HTTP v1 ünvanında işlədilir).</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Xidmət hesabının JSON açarı (bütöv məzmun). Environment variable ilə
    /// verilir — repoda saxlanılmır.
    /// </summary>
    public string ServiceAccountJson { get; set; } = string.Empty;

    /// <summary>
    /// Uşağa gedən bildirişlər yuxu rejimində saxlanılır.
    ///
    /// <para>Bu, məhsul qərarıdır, texniki deyil: 6–10 yaşlı uşağı gecə
    /// telefon bildirişi ilə oyatmaq app-in ekran vaxtı vədini pozardı.
    /// Valideynə gedən bildirişlər isə bu qaydadan KƏNARDIR — qərar verməli
    /// olan odur və o, böyükdür.</para>
    /// </summary>
    public bool RespectBedtime { get; set; } = true;

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(ProjectId)
        && !string.IsNullOrWhiteSpace(ServiceAccountJson);
}
