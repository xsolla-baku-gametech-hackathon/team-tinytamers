using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>
/// Bir xassə siqnalının HARADAN gəldiyi.
///
/// <para>Mənbə saxlanılır, çünki <b>müxtəlifliyin özü məlumatdır</b>: yalnız
/// macərəni bitirmiş uşaq ilə həm macərəni bitirən, həm mini oyunu seçən, həm
/// də dərsdə həmin mövzuya toxunan uşaq eyni bal alsa da, ikincinin marağı
/// haqqında daha çox şey bilirik.</para>
///
/// <para>Siyahı QAPALIDIR: klient mənbə göndərə bilmir, domen endpoint-ləri
/// isə yalnız buradakı dəyərləri işlədir.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainEvidenceSource
{
    /// <summary>Naməlum/köhnə sətir — miqrasiyadan əvvəlki məlumat.</summary>
    Unknown = 0,

    /// <summary>Macəra tamamlandı.</summary>
    Adventure = 1,

    /// <summary>Macərada bir variant seçildi.</summary>
    Choice = 2,

    /// <summary>Tapmaca həll olundu.</summary>
    Puzzle = 3,

    /// <summary>Mini oyun oynanıldı.</summary>
    MiniGame = 4,

    /// <summary>Dərsdə düzgün cavab verildi.</summary>
    Learning = 5,

    /// <summary>Pet-ə qulluq edildi.</summary>
    Care = 6,

    /// <summary>Missiya tamamlandı.</summary>
    Mission = 7,

    /// <summary>Kəşf edildi (dünya, kolleksiya).</summary>
    Discovery = 8,

    /// <summary>Şkafda görünüş quruldu.</summary>
    Cosmetic = 9,

    /// <summary>
    /// Uşağın AÇIQ sözü: «bəyənirəm» / «daha az göstər».
    ///
    /// <para>Ən güclü mənbədir və qəsdən belədir: uşaq birbaşa deyəndə sistem
    /// onun davranışını təfsir etməməlidir.</para>
    /// </summary>
    Explicit = 10,

    /// <summary>İlk tanışlıqdakı seçim — PRIOR, yüksək əminlikli həqiqət deyil.</summary>
    Onboarding = 11,

    /// <summary>Mükafat seçimi və ya geyindirilməsi.</summary>
    Reward = 12
}
