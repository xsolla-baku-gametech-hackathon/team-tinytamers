using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;
using PetPal.App.Ui.Services;

namespace PetPal.App.Services;

/// <summary>
/// Cihazın barmaq izi / üz tanıma kilidi (Plugin.Fingerprint).
///
/// <para>Valideyn PIN-inin QISA YOLUdur: PIN onsuz da qurulu olur, bu isə onu
/// hər dəfə yazmaqdan xilas edir. Uğursuz olanda ekran PIN-ə qayıdır — yəni
/// biometrika heç vaxt yeganə yol deyil.</para>
///
/// <para><c>AllowAlternativeAuthentication</c> QƏSDƏN sönülüdür: cihazın öz
/// PIN/naxış ekranını qəbul etsək, uşaq cihaz kilidini bilirsə (ailə
/// planşetində adətən bilir) qapı açılardı. Burada yalnız biometrika sayılır,
/// alternativ isə bizim öz valideyn PIN-imizdir.</para>
/// </summary>
public class MauiBiometricGate : IBiometricGate
{
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            return await CrossFingerprint.Current.IsAvailableAsync(allowAlternativeAuthentication: false);
        }
        catch
        {
            // Cihaz/platforma dəstəkləmirsə bu, xəta deyil — sadəcə qısa yol yoxdur.
            return false;
        }
    }

    public async Task<bool> AuthenticateAsync(string reason)
    {
        try
        {
            var request = new AuthenticationRequestConfiguration("PetPal", reason)
            {
                AllowAlternativeAuthentication = false
            };

            var result = await CrossFingerprint.Current.AuthenticateAsync(request);
            return result.Authenticated;
        }
        catch
        {
            return false;
        }
    }
}
