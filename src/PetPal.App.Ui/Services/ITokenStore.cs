namespace PetPal.App.Ui.Services;

/// <summary>
/// Token-lərin platformaya uyğun saxlanması. UI kitabxanası MAUI-dən asılı olmasın deyə
/// interfeys burada, konkret implementasiya (SecureStorage) host layihəsindədir.
/// </summary>
public interface ITokenStore
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    Task RemoveAsync(string key);
}

public static class TokenStoreKeys
{
    public const string ParentRefreshToken = "petpal.parent.refresh";
    public const string LastChildId = "petpal.last-child";

    /// <summary>Son daxil olan valideynin e-poçtu — giriş forması onu özü doldurur.</summary>
    public const string LastEmail = "petpal.last-email";

    /// <summary>Sonuncu interfeys dili — giriş ekranı uşaq seçilməmişdən əvvəl də düzgün dildə açılsın deyə.</summary>
    public const string Language = "petpal.language";

    /// <summary>
    /// Pet söhbətdə ucadan danışsınmı ("1" / "0"). Cihazda saxlanılır, çünki bu,
    /// uşağın hesabına yox, məkanına aid qərardır: sinifdə səs bağlanır, evdə açıq qalır.
    /// </summary>
    public const string PetVoice = "petpal.pet-voice";

    /// <summary>
    /// Valideyn qapısının PIN-i — YALNIZ barmaq izi qısa yolu üçün və yalnız
    /// valideyn onu açıq şəkildə qoşanda saxlanılır.
    ///
    /// <para>Niyə saxlanılır: biometrika tək başına qapı ola bilməz, çünki
    /// cihazın barmaq izini SERVER yoxlaya bilmir — ailə planşetində uşağın
    /// barmağı qeydiyyatda ola bilər. Ona görə barmaq izi qapını açmır, saxlanan
    /// PIN-i açır; PIN isə yenə serverə göndərilir və orada yoxlanılır. Beləliklə
    /// qərar həmişə serverdədir, biometrika sadəcə yazmaqdan azad edir.</para>
    ///
    /// <para>Yalnız təhlükəsiz anbarı olan hostda (MAUI → Keystore/Keychain)
    /// yazılır: veb hostda biometrika yoxdur, ona görə bu açar da heç vaxt
    /// doldurulmur.</para>
    /// </summary>
    public const string ParentGatePin = "petpal.parent.gate-pin";
}
