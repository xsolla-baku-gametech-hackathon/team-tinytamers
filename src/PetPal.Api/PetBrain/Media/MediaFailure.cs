namespace PetPal.Api.PetBrain.Media;

/// <summary>
/// Media uğursuzluğunun səbəbini TƏSNİF edir — üç sual üçün.
///
/// <para><b>Tapşırığı yaratmağı təkrarlamaq təhlükəsizdirmi?</b> Yalnız provayder
/// sorğunu İŞLƏMƏDİYİNİ açıq deyəndə: <c>429</c> (növbə doludur) və
/// <c>502/503/504</c> (şlüz sorğunu ötürmədi). Nəqliyyat xətası və <c>500</c>
/// QƏSDƏN kənardadır: sorğu serverə çatıb tapşırıq yarada bilərdi, təkrar isə
/// ikinci PULLU tapşırıq olardı.</para>
///
/// <para><b>Tapşırığın vəziyyətini yenidən soruşmaq təhlükəsizdirmi?</b> Oxuma
/// heç nə yaratmır, ona görə şəbəkə xətası, <c>429</c> və istənilən <c>5xx</c>
/// artıq ödənilmiş tapşırığı öldürməməlidir — növbəti addımda yenidən soruşulur,
/// ümumi vaxt həddi isə bunu sərhədləyir.</para>
///
/// <para><b>Sətir provayderə heç çatıbmı?</b> Bağlı konfiqurasiya, xərc
/// siyasətinin rəddi və kvota pul xərcləmir. Belə sətir «bu səhnə alınmadı»
/// demək deyil — sadəcə o an AI bağlı idi. Provayder açılanda sətir yenidən
/// açılır, əks halda sonradan qoşulan açar köhnə səhnələrə heç vaxt çatmazdı.</para>
/// </summary>
public static class MediaFailure
{
    private static readonly HashSet<string> RetryableCreate = new(StringComparer.Ordinal)
    {
        "http-429", "http-502", "http-503", "http-504"
    };

    private static readonly HashSet<string> NotAttempted = new(StringComparer.Ordinal)
    {
        "disabled", "not-configured", "paid-media-disabled", "circuit-open",
        "unknown-model", "model-not-in-profile", "model-modality-mismatch",
        "duration-out-of-contract", "ratio-not-supported", "over-credit-cap",
        "prompt-length", "daily-quota", "global-daily-quota"
    };

    public static bool IsRetryableCreate(string? reason) =>
        reason is not null && RetryableCreate.Contains(reason);

    public static bool IsRetryableRead(string? reason) =>
        reason is "transport" or "http-429" ||
        (reason is not null && reason.StartsWith("http-5", StringComparison.Ordinal));

    public static bool NeverReachedProvider(string? reason) =>
        reason is not null && NotAttempted.Contains(reason);
}
