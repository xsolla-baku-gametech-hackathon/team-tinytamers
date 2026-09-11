namespace PetPal.Api.PetBrain.Scenery;

/// <summary>
/// Rəsm modelinə verilə bilən BİR səhnə təsviri.
///
/// <para>Tapmacanın səhnəsi, macəranın arxa fonu və yoldaşın obrazı eyni
/// boru xəttindən keçir: eyni keş cədvəli, eyni növbə, eyni işçi, eyni
/// provayder və eyni bayt yoxlaması. Ona görə üçü üçün ayrıca "gözlə, çək,
/// saxla, ehtiyata qayıt" məntiqi YAZILMIR — fərq yalnız təsvirdədir.</para>
///
/// <para>Müqavilənin özü də qoruyucudur: təsvir <b>modelə gedən mətni
/// ÖZÜ qurur</b>. Beləliklə "bu sahəni prompta əlavə etmək" yalnız
/// təsdiqlənmiş ifadə cədvəlindən keçir, çağıran qat isə sərbəst mətn
/// ötürə bilmir.</para>
/// </summary>
public interface IStoryScene
{
    /// <summary>
    /// Səhnənin NÖVÜ — baza sətrində saxlanılır (ən çox 40 simvol).
    /// Tapmacada şablonun açarı, arxa fonda və obrazda isə sabit açardır.
    /// </summary>
    string SceneKey { get; }

    /// <summary>Prompt şablonunun versiyası — bəndlər dəyişəndə artır və hash-a düşür.</summary>
    int PromptVersion { get; }

    /// <summary>
    /// Kanonik hash — idempotentlik və keş açarı.
    ///
    /// <para>Uşağa aid heç nə daxil olmur: eyni səhnəni iki uşaq paylaşır,
    /// deməli eyni təsvir üçün İKİNCİ pullu sorğu getmir.</para>
    /// </summary>
    string Hash();

    /// <summary>Modelə gedən YEGANƏ mətn — təsdiqlənmiş ifadələrdən yığılır.</summary>
    string BuildPrompt();

    /// <summary>Ekran oxuyucusu üçün mətn — modelin çıxışından DEYİL.</summary>
    string AltText();
}

/// <summary>
/// Promptun BARMAQ İZİ — nə göndərildiyini sonradan yoxlamaq üçün.
///
/// <para>Promptun özü saxlanmır: lazım deyil və sətri böyüdür. Barmaq izi isə
/// "bu səhnə hansı mətnlə çəkilib" sualına şablon versiyası ilə birlikdə cavab
/// verir.</para>
/// </summary>
public static class StoryScenePrompt
{
    public static string Fingerprint(string prompt) =>
        Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(prompt)));
}
