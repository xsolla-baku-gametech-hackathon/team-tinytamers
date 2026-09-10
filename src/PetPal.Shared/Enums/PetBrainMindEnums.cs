using System.Text.Json.Serialization;

namespace PetPal.Shared.Enums;

/// <summary>
/// Bağın PİLLƏSİ — sadə faiz barından fərqli olaraq görünən bir mərtəbə.
///
/// <para>Pillə HEÇ VAXT geri düşmür, çünki bağın özü düşmür (bax <c>BondRules</c>).
/// Uşağı geri qaytarmaq üçün itki mexanikası qəsdən yoxdur.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainBondTier
{
    /// <summary>0–24.</summary>
    NewFriend = 0,

    /// <summary>25–49.</summary>
    TrustedFriend = 1,

    /// <summary>50–74.</summary>
    AdventurePartner = 2,

    /// <summary>75–99.</summary>
    BestCompanion = 3,

    /// <summary>100.</summary>
    LifelongTeam = 4
}

/// <summary>
/// Qulluq statının ZOLAĞI.
///
/// <para>Direktora dəqiq rəqəm verilmir: qərar "toxluq 43-dür" deyil, "acdır"
/// səviyyəsində olmalıdır — belə olanda balans dəyişikliyi qərar qatını
/// pozmur.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainCareBand
{
    /// <summary>Təcili — pet-in ehtiyacı macəra təklifindən öndə gələ bilər.</summary>
    Urgent = 0,

    Low = 1,
    Fine = 2,
    Great = 3
}

/// <summary>Ekran vaxtının QALIQ zolağı — dəqiqə sayı qərar qatına düşmür.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainScreenTimeBand
{
    /// <summary>Yeni fəaliyyət başlamaq olmur.</summary>
    Blocked = 0,

    /// <summary>Az qalıb — uzun macəra təklif edilməməlidir.</summary>
    Ending = 1,

    Plenty = 2
}

/// <summary>
/// Sessiyanın GÜN İÇİNDƏKİ yeri (uşağın öz saatına görə).
///
/// <para>Dəqiq vaxt deyil, zolaqdır: gecə saat 21:04 ilə 21:47 arasındakı fərq
/// qərar üçün əhəmiyyətsizdir, "axşamdır" faktı isə vacibdir.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainSessionBucket
{
    Morning = 0,
    Afternoon = 1,
    Evening = 2,

    /// <summary>Yuxu pəncərəsinə yaxın və ya onun içində.</summary>
    Night = 3
}

/// <summary>Bugünkü hədəfin vəziyyəti.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainDailyGoalBand
{
    NotStarted = 0,
    InProgress = 1,
    AlmostDone = 2,
    Reached = 3
}

/// <summary>
/// Uşağın tövsiyəyə verdiyi cavab.
///
/// <para>Bu, klientin SƏRBƏST siqnalı deyil: hər cavab serverin verdiyi
/// <c>DecisionId</c>-yə bağlanır, yəni klient nə şablon, nə də bal dəyişikliyi
/// göndərə bilir.</para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PetBrainRecommendationFeedback
{
    /// <summary>Kart göstərildi — bu, HƏLƏ üstünlük deyil.</summary>
    Shown = 0,

    /// <summary>Uşaq macərəni başlatdı.</summary>
    Selected = 1,

    /// <summary>"Başqa fikir" — yumşaq siqnal, qəti bəyənməmək deyil.</summary>
    ShowAnother = 2,

    /// <summary>"Sonra" — marağı AZALTMIR, yalnız indi uyğun deyil.</summary>
    NotNow = 3
}
