namespace PetPal.App.Ui.Services;

/// <summary>
/// Cihazın öz kilidi (barmaq izi / üz / cihaz PIN-i) ilə valideynin kimliyini
/// təsdiqləyir.
///
/// <para>Bu, PIN-in ƏVƏZİ deyil, QISA YOLUdur: PIN həmişə qurulu olmalıdır,
/// çünki biometrika hər cihazda yoxdur (veb hostda ümumiyyətlə yoxdur), cihaz
/// dəyişə bilər və barmaq izi silinə bilər. Biometrika uğursuz olanda və ya
/// mövcud olmayanda ekran sadəcə PIN soruşur.</para>
///
/// <para><b>Diqqət:</b> cihazda uşağın barmaq izi qeydiyyatdadırsa, biometrika
/// onu da tanıyacaq. Ona görə bu qısa yol istifadəçinin öz seçimidir və
/// valideyn bölməsində söndürülə bilir.</para>
/// </summary>
public interface IBiometricGate
{
    /// <summary>Cihazda biometrika var və ən azı bir barmaq izi/üz qeydiyyatdadır.</summary>
    Task<bool> IsAvailableAsync();

    /// <summary><paramref name="reason"/> istifadəçiyə göstərilən izahdır.</summary>
    Task<bool> AuthenticateAsync(string reason);
}

/// <summary>
/// Biometrikası olmayan host üçün (veb, masaüstü). Həmişə «yoxdur» deyir, yəni
/// ekran birbaşa PIN soruşur.
/// </summary>
public class NullBiometricGate : IBiometricGate
{
    public Task<bool> IsAvailableAsync() => Task.FromResult(false);

    public Task<bool> AuthenticateAsync(string reason) => Task.FromResult(false);
}
