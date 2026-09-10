using System.Globalization;

namespace PetPal.App.Ui.Services;

/// <summary>
/// İnterfeysin dili və iki dilli mətn seçimi.
///
/// Resurs faylı (.resx) və açar sistemi qəsdən seçilmədi: dil ikidir, mətnlər
/// qısadır və hər birinin yeri elə koddadır. Açarlar olsaydı, hər sətir üçün
/// ad uydurmaq və iki fayl arasında gedib-gəlmək lazım gələrdi; belə isə
/// tərcümə mətnin ÖZ YANINDADIR — biri dəyişəndə o birinin köhnə qalması
/// dərhal gözə çarpır. API tərəfindəki <c>Localized</c> də eyni məntiqlədir.
///
/// Dil uşağın profilindən gəlir. Giriş və profil seçimi ekranları isə uşaq
/// seçilməmişdən ƏVVƏL açılır, ona görə sonuncu dil cihazda saxlanılır:
/// ailə ingilis dilində işləyirsə, app növbəti açılışda da ingiliscə açılır.
/// </summary>
public class Loc
{
    public const string Azerbaijani = "az";
    public const string English = "en";

    private readonly ITokenStore _tokenStore;

    public Loc(ITokenStore tokenStore)
    {
        _tokenStore = tokenStore;
        ApplyCulture();
    }

    /// <summary>Dil dəyişdi — ekranlar yenidən çəkilməlidir.</summary>
    public event Action? Changed;

    public string Language { get; private set; } = Azerbaijani;

    public bool IsEnglish => Language == English;

    /// <summary>
    /// İki dilli mətn. Sıra qəsdən "əvvəl az, sonra en"-dir: mənbə mətnlər
    /// Azərbaycancadır, tərcümə isə həmişə ikinci arqumentdə gəlir.
    /// </summary>
    public string T(string az, string en) => IsEnglish ? en : az;

    /// <summary>Cihazda saxlanmış dili oxuyur — app açılan kimi, girişdən əvvəl.</summary>
    public async Task RestoreAsync() => Apply(await _tokenStore.GetAsync(TokenStoreKeys.Language));

    /// <summary>Sessiyadan gələn dili tətbiq edir və cihazda saxlayır.</summary>
    public async Task UseAsync(string? languageCode)
    {
        Apply(languageCode);
        await _tokenStore.SetAsync(TokenStoreKeys.Language, Language);
    }

    /// <summary>Naməlum/boş kod Azərbaycan dilinə düşür — ekran heç vaxt boş qalmır.</summary>
    public static string Normalize(string? languageCode) =>
        string.Equals(languageCode, English, StringComparison.OrdinalIgnoreCase)
            ? English
            : Azerbaijani;

    private void Apply(string? languageCode)
    {
        var next = Normalize(languageCode);
        var changed = next != Language;

        Language = next;
        ApplyCulture();

        if (changed)
            Changed?.Invoke();
    }

    /// <summary>
    /// Forma yoxlamalarının mesajları (PetPal.Shared.ValidationMessages) dili
    /// mədəniyyətdən oxuyur, ona görə mədəniyyət dil DƏYİŞMƏSƏ DƏ qurulur:
    /// prosesin standart mədəniyyəti cihazın dilidir (məsələn en-US) və
    /// interfeys Azərbaycanca qalsa belə, mesajlar ingiliscə çıxardı.
    ///
    /// Yalnız UI mədəniyyəti dəyişir — rəqəm və tarix formatı toxunulmaz qalır.
    ///
    /// Qəsdən yalnız PROSESİN standart mədəniyyəti qurulur, cari axınınkı yox:
    /// <c>CultureInfo.CurrentUICulture</c> async axına bağlıdır və async metodun
    /// içində verilən dəyər çağırana qayıtmır — üstəlik bir dəfə açıq verilsə,
    /// həmin axın artıq standart dəyəri saymır və dil dəyişməz qalır.
    /// </summary>
    private void ApplyCulture()
    {
        try
        {
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(Language);
        }
        catch (CultureNotFoundException)
        {
            // Bəzi quraşdırmalarda (məsələn kəsilmiş qlobalizasiya məlumatı ilə
            // yığılmış WebAssembly) mədəniyyət tapılmaya bilər. Bu, yalnız forma
            // mesajlarına təsir edir — app-in açılmamasına dəyməz.
        }
    }
}
