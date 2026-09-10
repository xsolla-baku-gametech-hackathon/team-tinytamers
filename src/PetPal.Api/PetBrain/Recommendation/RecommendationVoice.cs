using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Recommendation;

/// <summary>
/// Qərarın uşağın dilinə tərcüməsi — <b>saf funksiyalar</b>.
///
/// <para><b>Nə üçün ayrı sinif?</b> Siyasət SƏBƏB KODU qaytarır, cümlə yox.
/// Kod jurnala düşür, cümlə isə ekrana. İkisini bir yerdə saxlasaydıq,
/// jurnalda sərbəst mətn qalar və dil dəyişəndə köhnə qərarlar oxunmaz
/// olardı.</para>
///
/// <para><b>Uşaq dili, daxili bal yox.</b> Heç bir cümlədə «bal», «faiz»,
/// «model» və ya «alqoritm» keçmir: uşaq «kosmos macəralarını tez-tez
/// seçdiyin üçün» oxuyur, «topicFit 82» yox.</para>
/// </summary>
public static class RecommendationVoice
{
    /// <summary>Bir səbəb kodunun uşağa göstərilən cümləsi.</summary>
    public static string Reason(
        PetBrainWhyReason reason, string language, string topicLabel, string mechanicLabel, int minutes) =>
        reason switch
        {
            PetBrainWhyReason.StrongTopic => Localized.T(language,
                $"{topicLabel} macəralarını tez-tez seçirsən",
                $"You often choose {topicLabel.ToLowerInvariant()} adventures"),

            PetBrainWhyReason.LovedMechanic => Localized.T(language,
                $"Sevdiyin iş burada var: {mechanicLabel.ToLowerInvariant()}",
                $"Your favourite part is here: {mechanicLabel.ToLowerInvariant()}"),

            PetBrainWhyReason.LovedMechanicNewTopic => Localized.T(language,
                $"Bu dəfə sevdiyin «{mechanicLabel.ToLowerInvariant()}» yeni bir yerdədir",
                $"This time your favourite «{mechanicLabel.ToLowerInvariant()}» is somewhere new"),

            PetBrainWhyReason.ContinueStory => Localized.T(language,
                "Keçən dəfə qaldığımız yerin davamıdır",
                "It carries on from where we stopped"),

            PetBrainWhyReason.MatchesSessionLength => Localized.T(language,
                $"Bu macəra təxminən {minutes} dəqiqədir",
                $"This adventure takes about {minutes} minutes"),

            PetBrainWhyReason.MatchesChallenge => Localized.T(language,
                "Çətinlik sənin son nəticənə uyğundur",
                "The challenge matches how you did last time"),

            PetBrainWhyReason.SomethingNew => Localized.T(language,
                "Bu, hələ görmədiyin bir macəradır",
                "This is an adventure you have not tried yet"),

            PetBrainWhyReason.AskedForSurprise => Localized.T(language,
                "Yeni bir şey sınamaq istədiyin üçün",
                "Because you wanted to try something new"),

            PetBrainWhyReason.RewardMatch => Localized.T(language,
                "Sonda sevdiyin mükafat var",
                "There is a reward you like at the end"),

            PetBrainWhyReason.RecentlyPlayedSimilar => Localized.T(language,
                "Oxşarı bu yaxınlarda oldu — bu dəfə başqa yer",
                "Something similar happened recently — a different place this time"),

            PetBrainWhyReason.SupportReady => Localized.T(language,
                "Lazım olsa, kömək əlimin altındadır",
                "If you need it, my help is right here"),

            PetBrainWhyReason.StillLearning => Localized.T(language,
                "Hələ bir-birimizi tanıyırıq — bunu da sınayaq",
                "We are still getting to know each other — let us try this one"),

            _ => Localized.T(language,
                "Bu, bizim üçün yaxşı bir seçimdir",
                "This looks like a good pick for us")
        };

    /// <summary>Kartın rolunu uşağa bir sözlə deyir.</summary>
    public static string SlotLabel(PetBrainRecommendationSlot slot, string language) => slot switch
    {
        PetBrainRecommendationSlot.Continuity => Localized.T(language, "Davamı", "Continue"),
        PetBrainRecommendationSlot.NearbyDiscovery => Localized.T(language, "Yaxınlıqda", "Nearby"),
        PetBrainRecommendationSlot.SafeExploration => Localized.T(language, "Yeni", "Something new"),
        _ => Localized.T(language, "Sənin üçün", "For you")
    };

    /// <summary>
    /// Çətinliyin uşağa görünən adı.
    ///
    /// <para>«Asan» sözü qəsdən yoxdur: uşağa «bu sənin üçün asandır» demək
    /// onu kiçiltmək olardı. Ad təcrübəni təsvir edir, uşağı yox.</para>
    /// </summary>
    public static string ChallengeLabel(PetBrainDifficulty difficulty, string language) => difficulty switch
    {
        PetBrainDifficulty.Easy => Localized.T(language, "Rahat gediş", "Gentle pace"),
        PetBrainDifficulty.Hard => Localized.T(language, "Böyük sınaq", "Big challenge"),
        _ => Localized.T(language, "Tam sənə görə", "Just right")
    };

    /// <summary>Mükafatın uşağa görünən adı.</summary>
    public static string RewardLabel(PetBrainRewardPreference flavor, string language) => flavor switch
    {
        PetBrainRewardPreference.PetCosmetic => Localized.T(language, "Pet üçün geyim", "Something for your pet to wear"),
        PetBrainRewardPreference.RoomDecor => Localized.T(language, "Otaq üçün bəzək", "A decoration for the room"),
        PetBrainRewardPreference.Collection => Localized.T(language, "Kolleksiya üçün tapıntı", "A find for your collection"),
        PetBrainRewardPreference.CreativeTool => Localized.T(language, "Yeni yaradıcı alət", "A new creative tool"),
        _ => Localized.T(language, "Xatirə albomuna səhifə", "A page for your memory album")
    };

    /// <summary>İpucu formasının uşağa görünən adı.</summary>
    public static string SupportLabel(PetBrainHintStyle style, string language) => style switch
    {
        PetBrainHintStyle.StepByStep => Localized.T(language, "Addım-addım kömək", "Step-by-step help"),
        PetBrainHintStyle.Example => Localized.T(language, "Nümunə ilə kömək", "Help with an example"),
        PetBrainHintStyle.Rule => Localized.T(language, "Qayda ilə kömək", "Help with the rule"),
        _ => Localized.T(language, "Göstərərək kömək", "Help by showing")
    };

    /// <summary>
    /// Bal komponentinin valideyn/münsif üçün adı.
    ///
    /// <para>Bu, uşaq ekranına düşmür: uşağa cümlə göstərilir, valideynə isə
    /// hansı ölçünün nə qədər təsir etdiyi.</para>
    /// </summary>
    public static string FactorLabel(string key, string language) => key switch
    {
        "topic" => Localized.T(language, "Mövzu uyğunluğu", "Topic fit"),
        "mechanic" => Localized.T(language, "Mexanika uyğunluğu", "Mechanic fit"),
        "mastery" => Localized.T(language, "Çətinlik uyğunluğu", "Challenge fit"),
        "style" => Localized.T(language, "Oyun üslubu", "Play style"),
        "support" => Localized.T(language, "Dəstək", "Support"),
        "pace" => Localized.T(language, "Sessiya uzunluğu", "Session length"),
        "continuity" => Localized.T(language, "Davamlılıq", "Continuity"),
        "reward" => Localized.T(language, "Mükafat", "Reward"),
        "novelty" => Localized.T(language, "Yenilik", "Novelty"),
        "explicit" => Localized.T(language, "Sənin sözün", "What you told me"),
        _ => key
    };
}
