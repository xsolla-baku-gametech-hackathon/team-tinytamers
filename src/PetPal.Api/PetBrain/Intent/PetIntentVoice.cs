using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Intent;

/// <summary>
/// Niyyətin uşağa DEYİLƏN cümləsi — təsdiqlənmiş açarlardan qurulur.
///
/// <para><b>Ən vacib qayda budur: pet uşağı GÜNAHLANDIRMIR.</b> «Sən yoxkən
/// darıxdım», «məni tək qoydun», «niyə gəlmədin» tipli cümlələr burada
/// ümumiyyətlə mövcud deyil — nə şablon kimi, nə də variant kimi. Uşaq
/// məhsulunda emosional təzyiq ən asan və ən zərərli qısayoldur.</para>
///
/// <para>Cümlələr həmişə pet-in ÖZ təcrübəsindən danışır: «bunu tapdım»,
/// «bunu çəkdim», «yatdım». Uşağın yoxluğu heç bir cümlədə qiymətləndirilmir.
/// Qayda <c>PetIntentVoiceTests</c> ilə qorunur.</para>
/// </summary>
public static class PetIntentVoice
{
    /// <summary>Pet indi nə edir — niyyət DAVAM EDƏNDƏ görünür.</summary>
    public static string Doing(PetBrainIntentType type, string language, string petName) => type switch
    {
        PetBrainIntentType.Rest => Localized.T(language,
            $"{petName} bir az dincəlir.", $"{petName} is having a little rest."),

        PetBrainIntentType.Explore => Localized.T(language,
            $"{petName} ətrafı gəzir.", $"{petName} is wandering around."),

        PetBrainIntentType.Create => Localized.T(language,
            $"{petName} nəsə qurur.", $"{petName} is building something."),

        PetBrainIntentType.Help => Localized.T(language,
            $"{petName} kiməsə kömək edir.", $"{petName} is helping someone."),

        PetBrainIntentType.Learn => Localized.T(language,
            $"{petName} yeni bir şey öyrənir.", $"{petName} is learning something new."),

        PetBrainIntentType.Play => Localized.T(language,
            $"{petName} oynayır.", $"{petName} is playing."),

        _ => Localized.T(language,
            $"{petName} öz qayğısına qalır.", $"{petName} is looking after itself.")
    };

    /// <summary>
    /// Niyyət BİTƏNDƏ deyilən cümlə — pet nə etdiyini və nə tapdığını danışır.
    ///
    /// <para>Cümlə uşağın yoxluğuna deyil, pet-in işinə baxır. «Sən yoxkən»
    /// ifadəsi belə işlədilmir: o, uşağın olmamasını hadisəyə çevirir.</para>
    /// </summary>
    public static string Outcome(
        PetBrainIntentType type, string outcomeKey, string language, string petName) =>
        outcomeKey switch
        {
            "nap" => Localized.T(language,
                $"{petName} gözəl bir yuxu aldı.", $"{petName} had a lovely nap."),
            "stargazing" => Localized.T(language,
                "Ulduzlara baxdım — biri lap parlaq idi!", "I watched the stars — one was extra bright!"),
            "daydream" => Localized.T(language,
                "Xəyala daldım və gülməli bir şey düşündüm.", "I drifted off and thought of something funny."),

            "pebble" => Localized.T(language,
                "Bax, hamar bir daş tapdım!", "Look, I found a smooth pebble!"),
            "feather" => Localized.T(language,
                "Yumşaq bir lələk tapdım.", "I found a soft feather."),
            "map-corner" => Localized.T(language,
                "Köhnə xəritənin bir küncünü tapdım.", "I found the corner of an old map."),

            "sketch" => Localized.T(language,
                "Bir şəkil çəkdim — sənə göstərəcəyəm!", "I drew a picture — I will show you!"),
            "pattern" => Localized.T(language,
                "Yeni bir naxış düzəltdim.", "I made up a new pattern."),
            "tiny-tower" => Localized.T(language,
                "Balaca bir qüllə qurdum.", "I built a tiny tower."),

            "tidy-room" => Localized.T(language,
                "Otağı bir az səliqəyə saldım.", "I tidied up the room a little."),
            "watered-plant" => Localized.T(language,
                "Gülü sulamağı unutmadım.", "I remembered to water the plant."),
            "mission-note" => Localized.T(language,
                "Missiyamız üçün bir qeyd hazırladım.", "I made a note for our mission."),

            "new-word" => Localized.T(language,
                "Yeni bir söz öyrəndim!", "I learned a new word!"),
            "star-fact" => Localized.T(language,
                "Ulduzlar haqqında maraqlı bir şey öyrəndim.", "I learned something neat about stars."),
            "leaf-shape" => Localized.T(language,
                "Yarpaqların formalarını saydım.", "I counted the shapes of leaves."),

            "bubble-game" => Localized.T(language,
                "Baloncuqlarla oynadım.", "I played with bubbles."),
            "hide-and-seek" => Localized.T(language,
                "Gizlənpaç oynadım — özümü yaxşı gizlətdim!", "I played hide and seek — I hid well!"),
            "ball-toss" => Localized.T(language,
                "Topu atıb tutdum.", "I tossed a ball and caught it."),

            "snack" => Localized.T(language,
                "Kiçik bir qəlyanaltı yedim.", "I had a small snack."),
            "warm-bath" => Localized.T(language,
                "İsti su ilə yuyundum.", "I had a warm wash."),
            "brushed-fur" => Localized.T(language,
                "Tüklərimi darağladım.", "I brushed my fur."),

            _ => Doing(type, language, petName)
        };

    /// <summary>Niyyətin işarəsi — ekranda yanında görünür.</summary>
    public static string Icon(PetBrainIntentType type) => type switch
    {
        PetBrainIntentType.Rest => "😴",
        PetBrainIntentType.Explore => "🧭",
        PetBrainIntentType.Create => "🎨",
        PetBrainIntentType.Help => "🤝",
        PetBrainIntentType.Learn => "📚",
        PetBrainIntentType.Play => "🎈",
        _ => "🧼"
    };
}
