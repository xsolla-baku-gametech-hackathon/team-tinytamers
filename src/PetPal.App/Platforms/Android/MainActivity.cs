using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using PetPal.App.Ui.Services;

namespace PetPal.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]

// Dəvət linki: petpal://friend/AB12CD
//
// Link dostluq QURMUR — sadəcə kodu dostlar ekranındakı formaya yazır.
// Sorğu yenə uşağın öz düyməsi ilə göndərilir və qarşı tərəf onu özü qəbul
// edir. Belə olmasaydı, linki tapan hər kəs uşağın dostuna çevrilərdi.
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "petpal",
    DataHost = "friend")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Barmaq izi dialoqu Android-də AKTİVLİK tələb edir (AndroidX.Biometric
        // fraqment açır). Plugin özü aktivliyi tapa bilmir, ona görə burada
        // verilir — yoxsa valideyn bölməsindəki qısa yol səssizcə uğursuz olar.
        Plugin.Fingerprint.CrossFingerprint.SetCurrentActivityResolver(() => this);

        // App bağlı ikən linkə toxunulub: niyyət elə burada gəlir.
        CaptureInvite(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        // App arxa fondadırsa yeni niyyət bu yolla gəlir.
        CaptureInvite(intent);
    }

    /// <summary>
    /// Linkdəki kodu yaddaşda saxlayır. UI hələ qurulmamış ola bilər, ona görə
    /// kod birbaşa ekrana verilmir — <see cref="PendingInvite"/> onu dostlar
    /// ekranı açılana qədər saxlayır.
    /// </summary>
    private static void CaptureInvite(Intent? intent)
    {
        var data = intent?.Data;

        if (data is null || !string.Equals(data.Scheme, "petpal", StringComparison.OrdinalIgnoreCase))
            return;

        // petpal://friend/AB12CD → son seqment koddur.
        var code = data.LastPathSegment;

        if (string.IsNullOrWhiteSpace(code))
            return;

        IPlatformApplication.Current?.Services
            .GetService<PendingInvite>()?
            .Set(code);
    }
}
