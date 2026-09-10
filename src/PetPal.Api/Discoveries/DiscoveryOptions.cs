namespace PetPal.Api.Discoveries;

public class DiscoveryOptions
{
    public const string SectionName = "Discovery";

    public int BaseStars { get; set; } = 10;

    /// <summary>Uşaq eyni şeyi təkrar qeyd edəndə deyil, yeni şey tapanda daha çox ulduz alır.</summary>
    public int FirstOfKindStars { get; set; } = 15;

    /// <summary>Gündə maksimum neçə kəşf ulduz gətirir — sistem "spam" ilə doldurulmasın.</summary>
    public int MaxRewardedPerDay { get; set; } = 5;

    /// <summary>
    /// Şəkil JSON gövdəsində base64 kimi gəlir, yəni şəbəkədən ~4/3 qədər çox
    /// bayt keçir. Kestrel-in gövdə limiti bu dəyərdən hesablanır (Program.cs) —
    /// yalnız bu rəqəmi artırmaq kifayət etmir, sorğu daha əvvəl 413 alardı.
    /// </summary>
    public int MaxPhotoBytes { get; set; } = 30 * 1024 * 1024;

    public string StorageFolder { get; set; } = "uploads/discoveries";

    /// <summary>
    /// Diskə yazanda kök qovluq. Boş olanda app-in öz qovluğu işlədilir —
    /// konteynerdə bu, HƏR DEPLOY-DA SİLİNİR. Kalıcı volume mount edildikdə
    /// onun yolu buraya yazılır (məsələn <c>/data</c>).
    ///
    /// <para>Obyekt saxlama qoşulubsa (<see cref="S3"/>) bu dəyər oxunmur.</para>
    /// </summary>
    public string StorageRoot { get; set; } = string.Empty;

    public S3StorageOptions S3 { get; set; } = new();
}

/// <summary>
/// S3-uyğun obyekt saxlama. <see cref="Bucket"/> boş olduqda saxlama
/// TAMAMİLƏ SÖNÜKDÜR və app diskə yazır — yəni lokal iş üçün heç bir
/// konfiqurasiya tələb olunmur.
/// </summary>
public class S3StorageOptions
{
    public string Bucket { get; set; } = string.Empty;

    /// <summary>
    /// AWS-dən başqa provayderlər üçün ünvan, məsələn
    /// <c>https://&lt;account&gt;.r2.cloudflarestorage.com</c>.
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;

    public string Region { get; set; } = "auto";

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Bir bucket-i bir neçə mühitlə paylaşmaq üçün könüllü prefiks.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// R2 və MinIO kimi provayderlər üçün <c>true</c>: onlar bucket adını
    /// alt-domendə deyil, yolda gözləyir.
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// Cloudflare R2 <c>STREAMING-AWS4-HMAC-SHA256-PAYLOAD</c> imzasını qəbul
    /// etmir — R2 üçün bu açar <c>true</c> olmalıdır.
    /// </summary>
    public bool DisablePayloadSigning { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Bucket);
}

