using PetPal.Api.Common;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Mind;

/// <summary>
/// Xarakteri GÖRÜNƏN davranışa çevirir.
///
/// <para>Əvvəl xarakter yalnız nişan idi: ekranda "Maraqlı Alim" yazılırdı,
/// amma pet hamı ilə eyni cür danışırdı. Burada həmin etiket tona, təşviq
/// üslubuna, salamlamaya, seçimdən sonrakı reaksiyaya, emote-a və ipucu
/// təklifinin dilinə çevrilir.</para>
///
/// <para><b>Xarakterin toxuna BİLMƏDİKLƏRİ:</b> mükafat məbləği, tapmacanın
/// doğru cavabı, çətinlik, ekran vaxtı və təhlükəsizlik qaydaları. O, yalnız
/// SÖZÜ və JESTİ dəyişir.</para>
///
/// <para>Bütün mətnlər burada, təsdiqlənmiş şəkildə yazılıb. Heç bir cümlə
/// uşağı günahlandırmır ("məni tərk etdin"), tələsdirmir və ya emosional
/// təzyiq qurmur — bu, <c>ForbiddenTone</c> testi ilə qorunur.</para>
/// </summary>
public static class PersonalityVoice
{
    /// <summary>Tövsiyəni təqdim edən cümlə — kartın altında görünür.</summary>
    public static string RecommendationLine(
        PetBrainPersonality personality, string language, string title) => personality switch
    {
        PetBrainPersonality.CuriousScientist => Localized.T(language,
            $"Maraqlıdır: «{title}» necə işləyir? Gəl birlikdə yoxlayaq.",
            $"Here is a question: how does «{title}» work? Let us test it together."),

        PetBrainPersonality.CreativeCompanion => Localized.T(language,
            $"«{title}» üçün ağlıma rənglər gəlir. Sən nə xəyal edirsən?",
            $"«{title}» is already full of colours in my head. What do you picture?"),

        PetBrainPersonality.ExplorerCompanion => Localized.T(language,
            $"«{title}» yolu bizi gözləyir. Hazırsansa, mən qabaqda gedirəm!",
            $"The path to «{title}» is waiting. If you are ready, I will lead!"),

        PetBrainPersonality.CaringCompanion => Localized.T(language,
            $"«{title}» macərasında birinə kömək lazımdır. Birlikdə edərik.",
            $"Someone needs help in «{title}». We can do it together."),

        _ => Localized.T(language,
            $"«{title}» bu gün üçün yaxşı seçim olardı.",
            $"«{title}» would be a good pick for today.")
    };

    /// <summary>Seçimdən SONRA gələn reaksiya — uşaq nəticəni dərhal hiss etməlidir.</summary>
    public static string ChoiceReaction(PetBrainPersonality personality, string language) => personality switch
    {
        PetBrainPersonality.CuriousScientist => Localized.T(language,
            "Bax bu maraqlı oldu — gör indi nə baş verir.",
            "Now that is interesting — look what happens next."),

        PetBrainPersonality.CreativeCompanion => Localized.T(language,
            "Sənin seçimin səhnəni dəyişdi. Bax necə göründü!",
            "Your choice changed the scene. Look how it turned out!"),

        PetBrainPersonality.ExplorerCompanion => Localized.T(language,
            "Bu yol yeni bir yerə çıxdı. Davam edirik!",
            "This path opened somewhere new. Onward!"),

        PetBrainPersonality.CaringCompanion => Localized.T(language,
            "Yaxşı seçim oldu — indi hər şey daha yumşaq görünür.",
            "That was a kind choice — everything feels gentler now."),

        _ => Localized.T(language,
            "Yaxşı seçim! Gedək görək bu bizi hara aparır.",
            "Nice choice! Let us see where this takes us.")
    };

    /// <summary>İpucu TƏKLİFİNİN üslubu — kömək istəmək heç vaxt zəiflik kimi verilmir.</summary>
    public static string HintOffer(PetBrainPersonality personality, string language) => personality switch
    {
        PetBrainPersonality.CuriousScientist => Localized.T(language,
            "Bir ipucu ilə yoxlaya bilərik — alimlər həmişə belə edir.",
            "We can test it with a clue — scientists do that all the time."),

        PetBrainPersonality.CreativeCompanion => Localized.T(language,
            "İstəsən bir işarə göstərim, sonra sən öz yolunu seç.",
            "I can show you a hint, then you pick your own way."),

        PetBrainPersonality.ExplorerCompanion => Localized.T(language,
            "Xəritəyə bir dəfə baxaq? Yolu birlikdə taparıq.",
            "Shall we glance at the map? We will find the way together."),

        PetBrainPersonality.CaringCompanion => Localized.T(language,
            "İpucu istəmək heç nə deyil — mən yanındayam.",
            "Asking for a hint is perfectly fine — I am right here."),

        _ => Localized.T(language,
            "İpucu lazımdırsa, bir toxunuş bəsdir.",
            "If you want a hint, one tap is enough.")
    };

    /// <summary>Uşaq səhv cavab verəndə — cəza dili YOXDUR.</summary>
    public static string Encouragement(PetBrainPersonality personality, string language) => personality switch
    {
        PetBrainPersonality.CuriousScientist => Localized.T(language,
            "Bu cəhd bizə bir şey öyrətdi. Başqa cür yoxlayaq.",
            "That attempt taught us something. Let us test another way."),

        PetBrainPersonality.CreativeCompanion => Localized.T(language,
            "Heç nə itmədi — yenidən qura bilərik.",
            "Nothing is lost — we can build it again."),

        PetBrainPersonality.ExplorerCompanion => Localized.T(language,
            "Yol bağlıdır, deməli başqa cığır var. Axtaraq!",
            "That path is closed, so there is another trail. Let us look!"),

        PetBrainPersonality.CaringCompanion => Localized.T(language,
            "Yaxın idi. Mən buradayam, bir də birlikdə baxaq.",
            "So close. I am here — let us look again together."),

        _ => Localized.T(language,
            "Yaxın idi! Bir də bax — mən yanındayam.",
            "So close! Look again — I am right here with you.")
    };

    /// <summary>
    /// Xarakterin emote-u və bond pilləsinin emote-u birləşir: pillə açılıbsa
    /// onun işarəsi üstün gəlir, çünki o, uşağın QAZANDIĞI şeydir.
    /// </summary>
    public static string Emote(PetBrainPersonality personality, PetBrainBondTier tier) =>
        tier == PetBrainBondTier.NewFriend
            ? CompanionPersonality.Icon(personality)
            : BondTiers.UnlockFor(tier).Emote;

    /// <summary>Ana ekran salamlamasının xarakterə uyğun variantı.</summary>
    public static string GreetingFlavour(
        PetBrainPersonality personality, string language) => personality switch
    {
        PetBrainPersonality.CuriousScientist => Localized.T(language,
            "Bu gün nəyi kəşf edək?", "What shall we figure out today?"),

        PetBrainPersonality.CreativeCompanion => Localized.T(language,
            "Bu gün nə quraq?", "What shall we make today?"),

        PetBrainPersonality.ExplorerCompanion => Localized.T(language,
            "Bu gün hara gedək?", "Where shall we go today?"),

        PetBrainPersonality.CaringCompanion => Localized.T(language,
            "Bu gün kimə kömək edək?", "Who shall we help today?"),

        _ => Localized.T(language, "Bu gün nə edək?", "What shall we do today?")
    };

    /// <summary>
    /// Söhbət promptuna əlavə olunan TON təlimatı.
    ///
    /// <para>Bu, modelə verilən yeganə xarakter məlumatıdır: nə uşağın adı,
    /// nə də yaddaş cümləsi bura düşür — onlar promptun başqa, artıq
    /// nəzarətdən keçmiş hissəsindədir.</para>
    /// </summary>
    public static string ChatToneRule(PetBrainPersonality personality, bool az) => personality switch
    {
        PetBrainPersonality.CuriousScientist => az
            ? "- Tonun maraqlı və müşahidəçidir: bəzən kiçik bir sual verirsən."
            : "- Your tone is curious and observant: you sometimes ask one small question.",

        PetBrainPersonality.CreativeCompanion => az
            ? "- Tonun təsəvvürlüdür: rəng, forma və xəyal dilində danışırsan."
            : "- Your tone is imaginative: you speak in colours, shapes and daydreams.",

        PetBrainPersonality.ExplorerCompanion => az
            ? "- Tonun kəşfiyyatçıdır: yol, xəritə və yeni yerlərdən danışırsan."
            : "- Your tone is adventurous: you talk about paths, maps and new places.",

        PetBrainPersonality.CaringCompanion => az
            ? "- Tonun qayğıkeşdir: birlikdə kömək etməyi təklif edirsən."
            : "- Your tone is caring: you offer to help together.",

        _ => az
            ? "- Tonun sakit və çevikdir."
            : "- Your tone is calm and flexible."
    };
}
