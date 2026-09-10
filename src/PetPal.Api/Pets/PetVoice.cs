using PetPal.Api.Ai;
using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.Pets;

/// <summary>
/// Pet-in bütün replikaları. Mətnlər tək yerdə saxlanılır ki, ton vahid qalsın
/// və yeni dil əlavə etmək üçün kodun məntiqinə toxunmaq lazım gəlməsin.
///
/// Ton qaydası: pet heç vaxt uşağı danlamır. Səhv cavabda belə dəstəkləyici olur.
/// </summary>
public static class PetVoice
{
    public static string Idle(string language, PetMood mood, string childName, string petName) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? mood switch
            {
                PetMood.Hungry => $"Acmışam, {childName}! Nəsə yeyə bilərik?",
                PetMood.Sleepy => "Gözlərim ağırlaşır… bir az mürgüləsəm yaxşı olardı.",
                PetMood.Dirty => "Yenə palçığa bulaşmışam. Çimdirərsən?",
                PetMood.Sad => $"Səni darıxmışam, {childName}. Gəl birlikdə nəsə edək!",
                PetMood.Excited => $"Qayıtdın! Gəl maraqlı bir şey öyrənək, {childName}!",
                PetMood.Happy => "Bu gün əhvalım əladır. Bir çağırışa hazırsan?",
                _ => $"Salam, {childName}! Nədən başlayaq?"
            }
            : mood switch
            {
                PetMood.Hungry => $"I'm getting hungry, {childName}! Can we eat something?",
                PetMood.Sleepy => "My eyes are heavy… a little nap would be perfect.",
                PetMood.Dirty => "I rolled in the mud again. Bath time?",
                PetMood.Sad => $"I missed you, {childName}. Let's do something together!",
                PetMood.Excited => $"You're back! Let's learn something amazing, {childName}!",
                PetMood.Happy => "I feel great today. Ready for a challenge?",
                _ => $"Hi {childName}! What should we do first?"
            };

    public static string AnswerReaction(
        string language, bool isCorrect, int correctCount, SkillArea skill, string petName)
    {
        var az = Localized.Normalize(language) == Localized.Azerbaijani;

        if (!isCorrect)
            return az
                ? $"Az qalmışdı! {petName} sənə inanır — gəl birlikdə baxaq."
                : $"Almost! {petName} believes in you — let's look at it together.";

        return correctCount switch
        {
            1 => az ? $"Əla başlanğıc! {petName} sənə hey-hey deyir." : $"Great start! {petName} is cheering for you.",
            >= 3 => az
                ? $"Afərin! {SkillName(skill, true)} üzrə {correctCount} sual həll etdin!"
                : $"Great job! You solved {correctCount} {SkillName(skill, false)} questions!",
            _ => az ? $"Doğrudur! {petName} bir az da gücləndi." : $"Correct! {petName} just got a little stronger."
        };
    }

    public static string SessionSummary(string language, int correctCount, int totalCount, string petName)
    {
        var az = Localized.Normalize(language) == Localized.Azerbaijani;

        if (totalCount > 0 && correctCount == totalCount)
            return az ? $"Mükəmməl raund! {petName} sevincdən rəqs edir." : $"Perfect round! {petName} is doing a happy dance.";

        if (correctCount * 2 >= totalCount)
            return az ? $"Yaxşı iş! {petName} bu gün səninlə çox şey öyrəndi." : $"Nice work! {petName} learned a lot with you today.";

        return az
            ? $"Hər cəhd {petName}-i daha güclü edir. Tezliklə yenə məşq edək!"
            : $"Every try makes {petName} stronger. Let's practise again soon!";
    }

    public static string CareResult(string language, CareAction action, string petName) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? action switch
            {
                CareAction.Feed => $"Nuş olsun! {petName} bu yeməyi çox bəyəndi.",
                CareAction.Play => $"{petName} səninlə oynayarkən çox əyləndi!",
                CareAction.Clean => $"Tərtəmiz! {petName} çox gözəl görünür.",
                _ => $"{petName} rahat mürgülədi və özünü dincəlmiş hiss edir."
            }
            : action switch
            {
                CareAction.Feed => $"Yum! {petName} loved that snack.",
                CareAction.Play => $"{petName} had so much fun playing with you!",
                CareAction.Clean => $"Squeaky clean! {petName} looks great.",
                _ => $"{petName} took a cosy nap and feels refreshed."
            };

    public static string CareRefused(string language, CareAction action, string petName) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? action switch
            {
                CareAction.Feed => $"{petName} indi toxdur.",
                CareAction.Play => $"{petName} oynamaq üçün çox yorğundur. Əvvəlcə yatızdır.",
                CareAction.Clean => $"{petName} onsuz da tərtəmizdir.",
                _ => $"{petName} tamamilə oyaqdır."
            }
            : action switch
            {
                CareAction.Feed => $"{petName} is full right now.",
                CareAction.Play => $"{petName} is too tired to play. Try a nap first.",
                CareAction.Clean => $"{petName} is already sparkling clean.",
                _ => $"{petName} is wide awake."
            };

    /// <summary>
    /// Konkret yeməyə reaksiya. Yeməyin adı hal şəkilçisi almasın deyə tire ilə
    /// ayrılır — "Kök — bunu çox bəyəndim" hər söz üçün doğru səslənir.
    /// </summary>
    public static string FedFood(string language, string foodName, bool isTreat, string petName) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? isTreat
                ? $"{foodName} — nə şirin! {petName} sevindi, amma bir az bulaşdı."
                : $"{foodName} — nuş olsun! {petName} bunu çox bəyəndi."
            : isTreat
                ? $"{foodName} — so sweet! {petName} is happy, but a little sticky now."
                : $"{foodName} — yum! {petName} loved it.";

    public static string UnknownFood(string language) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? "Belə bir yemək tapılmadı. Qabdan birini seç!"
            : "I can't find that snack. Pick one from the bowl!";

    public static string NotEnoughStars(string language, int cost) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? $"Yemək almaq üçün {cost} ulduz lazımdır. Bir tapşırıq həll et və qazan!"
            : $"You need {cost} stars to buy a snack. Solve a task to earn some!";

    // ---------- Yumurta ----------

    public static string StillAnEgg(string language) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? "Hələ yumurtadayam! Əvvəlcə məni buradan çıxar."
            : "I'm still inside the egg! Get me out first.";

    public static string NotEnoughStarsToHatch(string language, int missing) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? $"Yumurtanı açmaq üçün daha {missing} ulduz lazımdır. Bir neçə sual həll et!"
            : $"You need {missing} more stars to open the egg. Solve a few questions!";

    /// <summary>Kilidli əşya taxılmaq istənəndə — uşaq nə etməli olduğunu bilməlidir.</summary>
    public static string AccessoryLocked(string language) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? "Bu əşya hələ açılmayıb. Şərtini tamamla və sonra tax!"
            : "That item isn't unlocked yet. Meet its goal first!";

    public static string AlreadyHatched(string language, string petName) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? $"{petName} artıq yumurtadan çıxıb!"
            : $"{petName} is already out of the egg!";

    public static string JustHatched(string language, string petName, string childName) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? $"Salam, {childName}! Mən {petName}. Nəhayət səni gördüm!"
            : $"Hi {childName}! I'm {petName}. I finally get to see you!";

    // ---------- Söhbət ----------

    /// <summary>
    /// AI əlçatan olmayanda söhbətdəki cavab. Salamlama replikasından ayrıdır:
    /// burada pet uşağın DANIŞDIĞINI qəbul etməlidir, sadəcə vəziyyətini
    /// bildirməməlidir. Əhvala görə seçilir — model sönülü olsa belə cavab
    /// təsadüfi yox, kontekstli görünür.
    /// </summary>
    public static string ChatFallback(string language, PetMood mood) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? mood switch
            {
                PetMood.Hungry => "Səni dinləyirəm, amma qarnım quruldayır! Əvvəlcə bir şey yeyək?",
                PetMood.Sleepy => "Hmm… gözlərim yumulur, amma səninləyəm.",
                PetMood.Dirty => "Danışmaq yaxşıdır! Sonra məni çimdirərsən?",
                PetMood.Sad => "Səsini eşitmək çox yaxşıdır. Bir az birlikdə oynayaq?",
                PetMood.Excited => "Nə yaxşı yazdın! Gəl bir oyun oynayaq!",
                PetMood.Happy => "Səninlə danışmaq xoşdur! Bu gün nə etmək istəyirsən?",
                _ => "Səni dinləyirəm! Gəl birlikdə nəsə edək."
            }
            : mood switch
            {
                PetMood.Hungry => "I'm listening, but my tummy is rumbling! Shall we eat first?",
                PetMood.Sleepy => "Mmm… my eyes are closing, but I'm here with you.",
                PetMood.Dirty => "I love chatting! Will you give me a bath after?",
                PetMood.Sad => "It's so good to hear from you. Shall we play a little?",
                PetMood.Excited => "What a great message! Let's play a game!",
                PetMood.Happy => "I love talking with you! What shall we do today?",
                _ => "I'm listening! Let's do something together."
            };

    /// <summary>
    /// Filtr uşağın mesajını saxlayanda pet-in dediyi. Ton qaydası burada
    /// xüsusilə vacibdir: uşaq bunu cəza kimi qəbul etməməlidir, ona görə heç
    /// bir variantda "olmaz", "səhv" və ya xəbərdarlıq tonu yoxdur.
    /// </summary>
    public static string ChatBlocked(string language, ChatBlockReason reason, string petName) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? reason switch
            {
                ChatBlockReason.Empty => "Nəsə yaz, mən buradayam!",
                ChatBlockReason.TooLong => "Bu çox uzundur! Qısa yaz ki, tez cavab verim.",
                ChatBlockReason.Repetition => "Bunu başa düşmədim. Bir söz yaz, cavab verim!",
                ChatBlockReason.ContactInfo =>
                    "Telefon və ünvan kimi şeyləri heç kimə yazmayaq — hətta mənə də. Gəl oyundan danışaq!",
                ChatBlockReason.Injection => $"Mən sadəcə sənin dostun {petName}-əm. Gəl başqa şeydən danışaq!",
                _ => "Gəl başqa şeydən danışaq!"
            }
            : reason switch
            {
                ChatBlockReason.Empty => "Type something, I'm right here!",
                ChatBlockReason.TooLong => "That's very long! Write a short one so I can answer quickly.",
                ChatBlockReason.Repetition => "I didn't get that. Write me a word and I'll reply!",
                ChatBlockReason.ContactInfo =>
                    "Let's not share phone numbers or addresses with anyone — not even me. Let's talk about the game!",
                ChatBlockReason.Injection => $"I'm just your friend {petName}. Let's talk about something else!",
                _ => "Let's talk about something else!"
            };

    /// <summary>Gündəlik mesaj həddi bitəndə. Uşaq limit deyil, dəvət eşitməlidir.</summary>
    public static string ChatLimitReached(string language) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? "Bu gün çox gözəl danışdıq! Sabah davam edərik — indi gəl oynayaq."
            : "We had a lovely chat today! Let's continue tomorrow — now let's play.";

    /// <summary>Valideyn söhbəti bağlı saxlayıb.</summary>
    public static string ChatDisabled(string language) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? "Söhbət hələ açılmayıb. Valideynindən xahiş et ki, onu açsın."
            : "Chat isn't switched on yet. Ask your grown-up to turn it on.";

    /// <summary>Valideyn arenanı bağlı saxlayıb.</summary>
    public static string ArenaDisabled(string language) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? "Yarış hələ açılmayıb. Valideynindən xahiş et ki, onu açsın."
            : "The arena isn't switched on yet. Ask your grown-up to turn it on.";

    /// <summary>Gündəlik duel həddi dolub — ton qadağa deyil, dəvətdir.</summary>
    public static string ArenaDailyLimit(string language, int duelsPerDay) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? $"Bu günlük {duelsPerDay} yarış tamamlandı 🏅 Sabah davam edərik!"
            : $"That's all {duelsPerDay} duels for today 🏅 Let's continue tomorrow!";

    /// <summary>
    /// Duel hələ başlamayıb: rəqib tapılmayıb və ya geri sayım getmir.
    /// Yarış tək başına oynanmır — ona görə cavab qəbul edilmir.
    /// </summary>
    public static string ArenaNotStarted(string language) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? "Yarış hələ başlamayıb — rəqibi gözləyirik!"
            : "The duel hasn't started yet — we're waiting for your rival!";

    /// <summary>Rəqib hələ oynamayıb — uşaq gözləmir, başqa işə keçir.</summary>
    public static string ArenaWaitingOpponent(string language) =>
        Localized.Normalize(language) == Localized.Azerbaijani
            ? "Rəqib axtarıram! Nəticə hazır olanda sənə xəbər verəcəyəm."
            : "I'm looking for an opponent! I'll let you know when the result is ready.";

    /// <summary>
    /// Duelin yekun replikası. Uduzanda da dəstəkləyici olur — arenada uşaq
    /// ulduz itirmir və pet heç vaxt danlamır.
    ///
    /// <para><paramref name="decidedBySpeed"/> — hesab bərabər olub, qalibi VAXT
    /// seçib. Belə halda adi "Qalib gəldin! 3–3" cümləsi uşağı çaşdırır: rəqəmlər
    /// eyni görünür, qalib isə var. Ona görə sürətlə qazanmağın ayrıca cümləsi var.</para>
    /// </summary>
    public static string DuelSummary(
        string language, DuelOutcome outcome, int myCorrect, int opponentCorrect, string petName, bool decidedBySpeed = false)
    {
        var az = Localized.Normalize(language) == Localized.Azerbaijani;

        if (decidedBySpeed && outcome is DuelOutcome.Win or DuelOutcome.Loss)
            return outcome == DuelOutcome.Win
                ? az
                    ? $"Hesab bərabər idi — {myCorrect}–{opponentCorrect}, amma sən DAHA TEZ cavabladın ⚡ Qalib sənsən!"
                    : $"The score was level — {myCorrect}–{opponentCorrect}, but you answered FASTER ⚡ You win!"
                : az
                    ? $"Hesab bərabər idi — {myCorrect}–{opponentCorrect}, rəqib sadəcə bir az tez cavabladı ⏱ Növbəti dəfə sürət səndə olsun!"
                    : $"The score was level — {myCorrect}–{opponentCorrect}, your rival was just a touch faster ⏱ Next time the speed is yours!";

        return outcome switch
        {
            DuelOutcome.Win => az
                ? $"Qalib gəldin! {myCorrect}–{opponentCorrect}. {petName} səninlə fəxr edir!"
                : $"You won! {myCorrect}–{opponentCorrect}. {petName} is so proud of you!",
            DuelOutcome.Draw => az
                ? $"Bərabərə! {myCorrect}–{opponentCorrect}. Nə yaxşı yarış idi!"
                : $"A draw! {myCorrect}–{opponentCorrect}. What a match!",
            DuelOutcome.Loss => az
                ? $"Bu dəfə rəqib qabaqladı — {myCorrect}–{opponentCorrect}. Ulduzların yerindədir, gəl bir də yoxlayaq!"
                : $"Your rival edged ahead this time — {myCorrect}–{opponentCorrect}. Your stars are safe, let's try again!",
            DuelOutcome.Expired => az
                ? "Bu dəfə rəqib tapılmadı. Gəl yenidən cəhd edək!"
                : "No opponent turned up this time. Let's try again!",
            _ => ArenaWaitingOpponent(language)
        };
    }

    private static string SkillName(SkillArea skill, bool az) => az
        ? skill switch
        {
            SkillArea.Math => "riyaziyyat",
            SkillArea.Vocabulary => "söz",
            SkillArea.Logic => "məntiq",
            SkillArea.Reading => "oxu",
            _ => "elm"
        }
        : skill.ToString().ToLowerInvariant();
}
