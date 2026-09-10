using PetPal.Api.Common;
using PetPal.Api.Entities;
using PetPal.Shared.Enums;
using static PetPal.Api.Data.Questions.QuestionBank;

namespace PetPal.Api.Data.Questions;

/// <summary>
/// Azərbaycan dilində sual bankı: hər bacarıqda 50 sual, hər çətinlikdə 5.
///
/// Ölçü qəsdən belədir: bir sessiya 5 sualdır və seçim hədəf çətinliyin ±1
/// aralığından edilir, yəni hər çətinlikdə ən azı 15 namizəd olur. Əvvəl
/// oxu bacarığında cəmi 7 sual vardı — uşaq ikinci sessiyada eyni sualları
/// görürdü.
///
/// Söz ehtiyatı bölməsi ingilis bankının tərcüməsi DEYİL: hecalama, şəkilçi
/// və deyimlər dilin özünə aiddir. Məntiq, oxu və elm isə paraleldir.
/// </summary>
internal static class AzQuestionBank
{
    private const string Az = Localized.Azerbaijani;

    public static IEnumerable<Question> All() =>
        [.. Vocabulary(), .. Logic(), .. Reading(), .. Science()];

    // ---------------- Söz ehtiyatı ----------------

    private static IEnumerable<Question> Vocabulary() =>
    [
        // Çətinlik 1 — əks məna, sadə qruplar
        Choice(Az, SkillArea.Vocabulary, 1, "«Böyük» sözünün mənası hansına yaxındır?", ["nəhəng", "balaca", "yavaş", "soyuq"], 0, "«Nəhəng» də böyük ölçünü bildirir.", "Ölçü haqqında düşün."),
        Choice(Az, SkillArea.Vocabulary, 1, "«İsti» sözünün əksi hansıdır?", ["ilıq", "soyuq", "günəşli", "quru"], 1, "İstinin əksi soyuqdur.", "Buz haqqında düşün."),
        Choice(Az, SkillArea.Vocabulary, 1, "«Gecə» sözünün əksi hansıdır?", ["səhər", "axşam", "gündüz", "sabah"], 2, "Gecənin əksi gündüzdür.", "Günəş nə vaxt görünür?"),
        Choice(Az, SkillArea.Vocabulary, 1, "Hansı söz heyvan adıdır?", ["stol", "qaşıq", "pəncərə", "dovşan"], 3, "Dovşan heyvandır, qalanları əşyadır.", "Hansı canlıdır?"),
        Choice(Az, SkillArea.Vocabulary, 1, "«Sürətli» sözünün əksi hansıdır?", ["uzun", "yavaş", "ağır", "isti"], 1, "Sürətlinin əksi yavaşdır.", "Tısbağa necə gedir?"),

        // Çətinlik 2 — heyvan balaları, sadə qruplaşdırma
        Choice(Az, SkillArea.Vocabulary, 2, "Pişiyin balası necə adlanır?", ["küçük", "bala pişik", "cücə", "quzu"], 1, "Pişiyin balası bala pişikdir.", "Pişiklər haqqında düşün."),
        Choice(Az, SkillArea.Vocabulary, 2, "«Sevincli» sözünün mənası nədir?", ["şad", "hirsli", "yorğun", "qorxmuş"], 0, "«Şad» sevinc bildirir.", "Hansı hiss xoşdur?"),
        Choice(Az, SkillArea.Vocabulary, 2, "İtin balası necə adlanır?", ["dayça", "quzu", "küçük", "cücə"], 2, "İtin balası küçükdür.", "İtlər haqqında düşün."),
        Choice(Az, SkillArea.Vocabulary, 2, "Hansı söz rəng bildirir?", ["dəftər", "tez", "qaçmaq", "yaşıl"], 3, "«Yaşıl» rəng adıdır.", "Hansını gözünlə görürsən?"),
        Choice(Az, SkillArea.Vocabulary, 2, "«Təmiz» sözünün əksi hansıdır?", ["quru", "çirkli", "isti", "dar"], 1, "Təmizin əksi çirklidir.", "Palçıq haqqında düşün."),

        // Çətinlik 3 — söz növləri ilə ilk tanışlıq
        Choice(Az, SkillArea.Vocabulary, 3, "Hansı söz feldir (iş bildirir)?", ["masa", "sürətlə", "qaçmaq", "mavi"], 2, "«Qaçmaq» iş bildirir, deməli feldir.", "Hansını edə bilərsən?"),
        Choice(Az, SkillArea.Vocabulary, 3, "«Kiçicik» sözü nə deməkdir?", ["çox böyük", "çox kiçik", "çox ağır", "çox uca"], 1, "«Kiçicik» çox kiçik deməkdir.", "Qarışqanı təsəvvür et."),
        Choice(Az, SkillArea.Vocabulary, 3, "Toyuğun balası necə adlanır?", ["cücə", "quzu", "küçük", "dayça"], 0, "Toyuğun balası cücədir.", "Yumurtadan kim çıxır?"),
        Choice(Az, SkillArea.Vocabulary, 3, "Hansı söz birdən çox əşya bildirir?", ["alma", "almanı", "almalar", "almadan"], 2, "«-lar» şəkilçisi çoxluq bildirir.", "Şəkilçiyə bax."),
        Choice(Az, SkillArea.Vocabulary, 3, "«Yüngül» sözünün əksi hansıdır?", ["dar", "uzun", "ağır", "isti"], 2, "Yüngülün əksi ağırdır.", "Daşı qaldırmağı düşün."),

        // Çətinlik 4 — yazılış və söz növü
        Choice(Az, SkillArea.Vocabulary, 4, "Düzgün yazılışı seç.", ["qayitmaq", "qayıtmaq", "qaytmaq", "qayıdmaq"], 1, "Düzgün yazılış «qayıtmaq»dır.", "Sözü hecalara böl."),
        Choice(Az, SkillArea.Vocabulary, 4, "Hansı söz isimdir (əşyanın adıdır)?", ["qaçmaq", "sakitcə", "yaşıl", "çay"], 3, "«Çay» əşya adıdır, deməli isimdir.", "Hansı bir şeyin adıdır?"),
        Choice(Az, SkillArea.Vocabulary, 4, "«Kitablar» sözündə cəm şəkilçisi hansıdır?", ["-kit", "-lar", "-ab", "-ki"], 1, "«-lar» şəkilçisi çoxluq bildirir.", "Sözün sonuna bax."),
        Choice(Az, SkillArea.Vocabulary, 4, "Hansı yazılış düzgündür?", ["şekil", "şəkl", "şəkil", "şəkill"], 2, "Düzgün yazılış «şəkil»dir.", "İki hecaya böl: şə-kil."),
        Choice(Az, SkillArea.Vocabulary, 4, "«Dərin» sözünün əksi hansıdır?", ["dayaz", "geniş", "uzun", "hündür"], 0, "Dərinin əksi dayazdır.", "Çayın kənarını düşün."),

        // Çətinlik 5 — cümlə içində məna
        Choice(Az, SkillArea.Vocabulary, 5, "«Cəsur» nə deməkdir?", ["qorxmayan", "çox yorğun", "çox varlı", "çox sakit"], 0, "Cəsur adam qorxmur.", "Qəhrəman haqqında düşün."),
        Choice(Az, SkillArea.Vocabulary, 5, "Cümləni tamamla: «Şorba o qədər ___ idi ki, dilimi yandırdım.»", ["sərin", "şirin", "isti", "boş"], 2, "Yalnız isti şey dili yandırır.", "Nə yandırır?"),
        Choice(Az, SkillArea.Vocabulary, 5, "Hansı söz peşə adıdır?", ["dünən", "həkim", "sürətli", "kitab"], 1, "Həkim bir peşədir.", "Kim işləyir?"),
        Choice(Az, SkillArea.Vocabulary, 5, "«Baxmaq» felinin əmr forması hansıdır?", ["baxdı", "baxır", "bax", "baxacaq"], 2, "Əmr forması «bax»dır.", "Birinə necə deyirsən?"),
        Choice(Az, SkillArea.Vocabulary, 5, "«Boş» sözünün əksi hansıdır?", ["dolu", "açıq", "yüngül", "təmiz"], 0, "Boşun əksi doludur.", "Stəkanı təsəvvür et."),

        // Çətinlik 6 — sifət, zərf, şəkilçi
        Choice(Az, SkillArea.Vocabulary, 6, "Hansı söz sifətdir (əlamət bildirir)?", ["parlaq", "oxumaq", "məktəb", "yavaşca"], 0, "«Parlaq» əşyanın əlamətini bildirir.", "Hansı söz təsvir edir?"),
        Choice(Az, SkillArea.Vocabulary, 6, "«Qədim» nə deməkdir?", ["çox yeni", "çox sürətli", "çox köhnə", "çox kiçik"], 2, "«Qədim» çox köhnə deməkdir.", "Piramidalar haqqında düşün."),
        Choice(Az, SkillArea.Vocabulary, 6, "«Gülməli» sözünün kökü hansıdır?", ["məli", "gül", "ülmə", "gülm"], 1, "Söz «gül» kökündən düzəlib.", "Şəkilçini ayır."),
        Choice(Az, SkillArea.Vocabulary, 6, "Hansı söz zərfdir (necə? sualına cavab verir)?", ["avtomobil", "qırmızı", "uşaq", "sürətlə"], 3, "«Sürətlə» işin necə görüldüyünü bildirir.", "Necə? sualını ver."),
        Choice(Az, SkillArea.Vocabulary, 6, "«Susuz» sözündəki «-suz» şəkilçisi nə bildirir?", ["çoxluq", "yoxluq", "kiçiklik", "sahiblik"], 1, "«-suz» bir şeyin olmadığını bildirir.", "«Susuz» = suyu olmayan."),

        // Çətinlik 7 — sinonim, deyim, söz quruluşu
        Choice(Az, SkillArea.Vocabulary, 7, "Hansı cütlük eyni mənalıdır?", ["başlamaq – start etmək", "başlamaq – dayanmaq", "başlamaq – yavaşımaq", "başlamaq – bağlamaq"], 0, "Hər ikisi eyni mənanı verir.", "Hansı ikisi eynidir?"),
        Choice(Az, SkillArea.Vocabulary, 7, "«Ağzıaçıq» sözü neçə sözdən düzəlib?", ["bir", "iki", "üç", "dörd"], 1, "«Ağız» və «açıq» sözlərindən düzəlib.", "Sözü ikiyə böl."),
        Choice(Az, SkillArea.Vocabulary, 7, "«Gözü su içmir» ifadəsi nə deməkdir?", ["susuzdur", "inanmır", "xəstədir", "sevinir"], 1, "Bu deyim inanmamağı bildirir.", "Deyimlər sözbəsöz oxunmur."),
        Choice(Az, SkillArea.Vocabulary, 7, "Hansı söz düzəltmə sözdür (kök + şəkilçi)?", ["ağac", "daş", "dəmirçi", "dəmir"], 2, "«Dəmirçi» = dəmir + çi.", "Hansında şəkilçi var?"),
        Choice(Az, SkillArea.Vocabulary, 7, "«Nadir» nə deməkdir?", ["az rast gəlinən", "çox olan", "çox böyük", "çox ucuz"], 0, "Nadir şey nadir hallarda tapılır.", "Tapılması çətin nədir?"),

        // Çətinlik 8 — xarakter sözləri və söz ailəsi
        Choice(Az, SkillArea.Vocabulary, 8, "«Maraqlı» (öyrənməyi sevən) insan necə olur?", ["həmişə hirsli", "öyrənmək istəyən", "heç danışmayan", "sürətlə qaçan"], 1, "Maraqlı insan öyrənməyi sevir.", "Sual verməyi düşün."),
        Choice(Az, SkillArea.Vocabulary, 8, "«Səbirli» nə deməkdir?", ["tez hirslənən", "çox danışan", "gözləməyi bacaran", "yorğun"], 2, "Səbirli adam gözləməyi bacarır.", "Növbədə durmağı düşün."),
        Choice(Az, SkillArea.Vocabulary, 8, "«Əl-ələ vermək» ifadəsi nə deməkdir?", ["birlikdə işləmək", "əl yumaq", "salamlaşmaq", "ayrılmaq"], 0, "Bu deyim birlikdə çalışmağı bildirir.", "Komanda haqqında düşün."),
        Choice(Az, SkillArea.Vocabulary, 8, "Hansı söz «yazmaq» felindən düzəlib?", ["yaxın", "yaşıl", "yazıçı", "yastı"], 2, "«Yazıçı» yazmaq sözündən düzəlib.", "Kim yazır?"),
        Choice(Az, SkillArea.Vocabulary, 8, "«Təvazökar» insan necə olur?", ["özünü öyməyən", "çox danışan", "hirsli", "tənbəl"], 0, "Təvazökar adam özünü öymür.", "Öyünməyin əksi nədir?"),

        // Çətinlik 9 — mücərrəd anlayışlar
        Choice(Az, SkillArea.Vocabulary, 9, "«Təkmilləşdirmək» nə deməkdir?", ["gözardı etmək", "daha yaxşı etmək", "azaltmaq", "təkrarlamaq"], 1, "«Təkmilləşdirmək» daha yaxşı etməkdir.", "İnkişaf haqqında düşün."),
        Choice(Az, SkillArea.Vocabulary, 9, "«Nəticə» sözünün mənası nədir?", ["başlanğıc", "səbəb", "sonda alınan", "sual"], 2, "Nəticə işin sonunda alınandır.", "Matçın sonunu düşün."),
        Choice(Az, SkillArea.Vocabulary, 9, "«Fərziyyə» nə deməkdir?", ["hələ yoxlanmamış fikir", "sübut olunmuş qanun", "rəsmi hesabat", "riyazi düstur"], 0, "Fərziyyə yoxlanmalı olan fikirdir.", "Alim əvvəlcə nə edir?"),
        Choice(Az, SkillArea.Vocabulary, 9, "«Etibarlı» insan necə olur?", ["sözünə əməl edən", "sürətli qaçan", "gülməli", "varlı"], 0, "Etibarlı adama arxalanmaq olar.", "Kimə söz vermək olar?"),
        Choice(Az, SkillArea.Vocabulary, 9, "«Diqqətsizlik» sözündə neçə şəkilçi var?", ["bir", "iki", "üç", "dörd"], 1, "Diqqət + siz + lik: iki şəkilçi.", "Kökü tap, qalanını say."),

        // Çətinlik 10 — incə fərqlər
        Choice(Az, SkillArea.Vocabulary, 10, "«Tərəddüdlü» nə deməkdir?", ["həyəcanlı", "ac", "qərar verə bilməyən", "çaşqın"], 2, "Tərəddüdlü adam qərar verməkdə çətinlik çəkir.", "Duruxmağı düşün."),
        Choice(Az, SkillArea.Vocabulary, 10, "«Mübahisə» nə deməkdir?", ["fikir ayrılığı", "tam razılıq", "uzun səyahət", "böyük hədiyyə"], 0, "Mübahisədə tərəflərin fikri fərqlidir.", "Razılığın əksi nədir?"),
        Choice(Az, SkillArea.Vocabulary, 10, "«Qənaətcil» insan necə olur?", ["israfçı", "xərcini ölçüb-biçən", "tənbəl", "hirsli"], 1, "Qənaətcil adam artıq xərcləmir.", "İsrafın əksi nədir?"),
        Choice(Az, SkillArea.Vocabulary, 10, "«Əvvəl-axır» ifadəsi nə bildirir?", ["heç vaxt", "dərhal", "nə vaxtsa mütləq", "təsadüfən"], 2, "Bu ifadə «gec-tez mütləq» deməkdir.", "Vaxt haqqında düşün."),
        Choice(Az, SkillArea.Vocabulary, 10, "Hansı anlayış mücərrəddir (əl ilə tutula bilmir)?", ["stol", "daş", "ağac", "sevgi"], 3, "Sevgini görmək və tutmaq olmur.", "Hansını əlinə ala bilməzsən?"),
    ];

    // ---------------- Məntiq ----------------

    private static IEnumerable<Question> Logic() =>
    [
        // Çətinlik 1 — sadə sıra və qrup
        Choice(Az, SkillArea.Logic, 1, "Növbəti hansıdır? 2, 4, 6, ___", ["7", "8", "9", "10"], 1, "Hər dəfə 2 əlavə olunur.", "Ədədlər arasındakı fərqə bax."),
        Choice(Az, SkillArea.Logic, 1, "Hansı fərqlidir?", ["alma", "banan", "kök", "armud"], 2, "Kök tərəvəzdir, qalanları meyvədir.", "Üçü meyvədir."),
        Choice(Az, SkillArea.Logic, 1, "Növbəti hansıdır? 1, 2, 3, ___", ["4", "5", "6", "7"], 0, "Ədədlər bir-bir artır.", "Sadəcə saymağa davam et."),
        Choice(Az, SkillArea.Logic, 1, "Hansı biri qalanlarından fərqlidir?", ["stol", "it", "pişik", "at"], 0, "Stol canlı deyil, qalanları heyvandır.", "Üçü nəfəs alır."),
        Choice(Az, SkillArea.Logic, 1, "Hansı fiqurun küncü yoxdur?", ["kvadrat", "üçbucaq", "dairə", "düzbucaqlı"], 2, "Dairənin küncü yoxdur.", "Barmağını fiqurun kənarı ilə gəzdir."),

        // Çətinlik 2 — sıra addımı, fiqurlar
        Choice(Az, SkillArea.Logic, 2, "Növbəti hansıdır? 5, 10, 15, ___", ["20", "22", "25", "30"], 0, "Hər dəfə 5 əlavə olunur.", "Beş-beş say."),
        Choice(Az, SkillArea.Logic, 2, "Hansı fiqurun 3 tərəfi var?", ["kvadrat", "üçbucaq", "dairə", "altıbucaq"], 1, "Üçbucağın düz 3 tərəfi var.", "Adına diqqət et."),
        Choice(Az, SkillArea.Logic, 2, "Növbəti hansıdır? 10, 9, 8, ___", ["7", "8", "9", "11"], 0, "Ədədlər bir-bir azalır.", "Geriyə say."),
        Choice(Az, SkillArea.Logic, 2, "Hansı söz qalanlarına uyğun gəlmir?", ["qırmızı", "mavi", "yaşıl", "kvadrat"], 3, "Kvadrat fiqurdur, qalanları rəngdir.", "Üçü rəngdir."),
        Choice(Az, SkillArea.Logic, 2, "Hansı fiqurun 4 bərabər tərəfi var?", ["üçbucaq", "dairə", "kvadrat", "oval"], 2, "Kvadratın dörd tərəfi bərabərdir.", "Tərəfləri say."),

        // Çətinlik 3 — günlər, hərflər
        Choice(Az, SkillArea.Logic, 3, "Bu gün bazar ertəsidirsə, sabah hansı gündür?", ["bazar", "çərşənbə axşamı", "cümə", "bazar ertəsi"], 1, "Bazar ertəsindən sonra çərşənbə axşamı gəlir.", "Günləri sıra ilə de."),
        Choice(Az, SkillArea.Logic, 3, "Növbəti hansıdır? A, C, E, ___", ["F", "G", "H", "I"], 1, "Hər dəfə bir hərf buraxılır.", "Hərfləri iki-iki say."),
        Choice(Az, SkillArea.Logic, 3, "Növbəti hansıdır? 10, 20, 30, ___", ["35", "40", "45", "50"], 1, "Hər dəfə 10 əlavə olunur.", "On-on say."),
        Choice(Az, SkillArea.Logic, 3, "Həftədə neçə gün var?", ["5", "6", "7", "8"], 2, "Həftədə 7 gün var.", "Günləri sadala."),
        Choice(Az, SkillArea.Logic, 3, "Dünən çərşənbə idisə, bu gün hansı gündür?", ["cümə axşamı", "çərşənbə axşamı", "cümə", "bazar"], 0, "Çərşənbədən sonra cümə axşamı gəlir.", "Bir gün irəli get."),

        // Çətinlik 4 — müqayisə və qat
        Choice(Az, SkillArea.Logic, 4, "Əli Saradan hündürdür. Sara Ninadan hündürdür. Ən hündür kimdir?", ["Əli", "Sara", "Nina", "Bilinmir"], 0, "Əli Saradan, Sara isə Ninadan hündürdür.", "Onları sıraya düz."),
        Choice(Az, SkillArea.Logic, 4, "Növbəti hansıdır? 1, 2, 4, 8, ___", ["16", "18", "20", "24"], 0, "Hər ədəd iki dəfə artır.", "2-yə vurmağı yoxla."),
        Choice(Az, SkillArea.Logic, 4, "Növbəti hansıdır? 3, 6, 9, ___", ["12", "13", "15", "18"], 0, "Hər dəfə 3 əlavə olunur.", "Üç-üç say."),
        Choice(Az, SkillArea.Logic, 4, "Kamil Aydan yaşlıdır. Ay Nurdan yaşlıdır. Ən kiçik kimdir?", ["Kamil", "Ay", "Nur", "Bilinmir"], 2, "Nur hamıdan kiçikdir.", "Sıranın sonuna bax."),
        Choice(Az, SkillArea.Logic, 4, "Hansı ədəd sıraya uyğun gəlmir? 2, 4, 6, 9, 8", ["2", "6", "8", "9"], 3, "Sıra cüt ədədlərdən ibarətdir, 9 isə təkdir.", "Cüt ədədləri tap."),

        // Çətinlik 5 — qayda tətbiqi
        Choice(Az, SkillArea.Logic, 5, "Bütün pişiklərin quyruğu var. Mia pişikdir. Deməli Mia…", ["quyruqludur", "qanadlıdır", "itdir", "bilinmir"], 0, "Qayda bütün pişiklərə aiddir.", "Qaydanı izlə."),
        Choice(Az, SkillArea.Logic, 5, "Hansı ədəd uyğun gəlmir? 3, 5, 8, 7", ["3", "5", "8", "7"], 2, "8 yeganə cüt ədəddir.", "Cüt və tək ədədlərə bax."),
        Choice(Az, SkillArea.Logic, 5, "Bütün quşların lələyi var. Tuti quşdur. Deməli Tutinin…", ["lələyi var", "pulcuğu var", "dörd ayağı var", "quyruğu yoxdur"], 0, "Qayda bütün quşlara aiddir.", "Qaydanı Tutiyə tətbiq et."),
        Choice(Az, SkillArea.Logic, 5, "Növbəti hansıdır? 20, 17, 14, ___", ["10", "11", "12", "13"], 1, "Hər dəfə 3 azalır.", "Fərqi hesabla."),
        Choice(Az, SkillArea.Logic, 5, "Hansı əşya qalanlarından fərqlidir?", ["çəkmə", "corab", "papaq", "qaşıq"], 3, "Qaşıq geyim deyil.", "Üçünü geyinirsən."),

        // Çətinlik 6 — mürəkkəb sıra
        Choice(Az, SkillArea.Logic, 6, "Növbəti hansıdır? 3, 6, 12, 24, ___", ["26", "30", "36", "48"], 3, "Hər ədəd iki dəfə artır.", "2-yə vurmağı yoxla."),
        Choice(Az, SkillArea.Logic, 6, "Növbəti hansıdır? 1, 4, 9, 16, ___", ["17", "20", "24", "25"], 3, "Bunlar 1×1, 2×2, 3×3, 4×4, 5×5-dir.", "Hər ədədi özünə vur."),
        Choice(Az, SkillArea.Logic, 6, "Bir qutuda 4 cüt corab var. Cəmi neçə corab var?", ["4", "6", "8", "12"], 2, "Bir cüt 2 corabdır, 4 cüt isə 8.", "«Cüt» ikidir."),
        Choice(Az, SkillArea.Logic, 6, "Növbəti hansıdır? 2, 3, 5, 8, 12, ___", ["13", "15", "16", "17"], 3, "Fərqlər 1, 2, 3, 4, 5 kimi artır.", "Fərqləri yaz."),
        Choice(Az, SkillArea.Logic, 6, "Hansı ifadə həmişə doğrudur?", ["Bütün kvadratlar dördbucaqlıdır", "Bütün dördbucaqlılar kvadratdır", "Bütün dairələr kvadratdır", "Bütün üçbucaqlar bərabərdir"], 0, "Kvadrat dördbucaqlının bir növüdür.", "Hansı hər zaman doğrudur?"),

        // Çətinlik 7 — ehtimal və sıralama
        Choice(Az, SkillArea.Logic, 7, "Qutuda 3 qırmızı, 2 mavi top var. Baxmadan 1 top götürürsən. Hansı rəng daha ehtimallıdır?", ["qırmızı", "mavi", "eyni", "bilinmir"], 0, "Qırmızı toplar daha çoxdur.", "Hər rəngi say."),
        Choice(Az, SkillArea.Logic, 7, "Zərin 6 üzü var. 7 gəlmə ehtimalı necədir?", ["çox böyük", "kiçik", "mümkün deyil", "yarı-yarı"], 2, "Zərdə 7 yoxdur, ona görə mümkün deyil.", "Zərin üzlərini say."),
        Choice(Az, SkillArea.Logic, 7, "Növbəti hansıdır? 1, 3, 6, 10, ___", ["11", "13", "14", "15"], 3, "Fərqlər 2, 3, 4, 5 kimi artır.", "Fərqləri yaz."),
        Choice(Az, SkillArea.Logic, 7, "Bir kisədə yalnız qırmızı toplar var. Bir top götürsən, rəngi nə olacaq?", ["qırmızı", "mavi", "bilinmir", "yaşıl"], 0, "Kisədə başqa rəng yoxdur.", "Kisədə nə var?"),
        Choice(Az, SkillArea.Logic, 7, "Ayla Rəşiddən sürətlidir. Rəşid Cəmilədən sürətlidir. Kim ən yavaşdır?", ["Ayla", "Rəşid", "Cəmilə", "Bilinmir"], 2, "Cəmilə hamıdan yavaşdır.", "Sıranın sonuna bax."),

        // Çətinlik 8 — çıxarış
        Choice(Az, SkillArea.Logic, 8, "Növbəti hansıdır? 1, 1, 2, 3, 5, ___", ["8", "9", "10", "12"], 0, "Hər ədəd özündən əvvəlki ikisinin cəmidir.", "Son iki ədədi topla."),
        Choice(Az, SkillArea.Logic, 8, "Növbəti hansıdır? 64, 32, 16, ___", ["4", "6", "8", "12"], 2, "Hər ədəd yarıya bölünür.", "Yarısını götür."),
        Choice(Az, SkillArea.Logic, 8, "Beş dostdan üçü futbol, ikisi şahmat oynayır. Futbol oynayanlar neçə nəfər çoxdur?", ["bir", "iki", "üç", "beş"], 0, "3 − 2 = 1.", "Fərqi hesabla."),
        Choice(Az, SkillArea.Logic, 8, "«Bütün balıqlar üzür» doğrudursa, «bütün üzənlər balıqdır» ifadəsi…", ["doğrudur", "yanlışdır", "eyni şeydir", "bilinmir"], 1, "İnsan da üzür, amma balıq deyil.", "Başqa kim üzür?"),
        Choice(Az, SkillArea.Logic, 8, "Bütün gülləri suvardım, amma bir gül soldu. Deməli…", ["su tək başına kifayət etmir", "heç gül suvarmadım", "bütün güllər soldu", "su zərərlidir"], 0, "Başqa səbəb də ola bilər — işıq, torpaq.", "Başqa nə lazımdır?"),

        // Çətinlik 9 — tərs qayda
        Choice(Az, SkillArea.Logic, 9, "Yağış yağsa, yer islanır. Yer qurudur. Deməli…", ["yağış yağıb", "yağış yağmayıb", "yağış yağacaq", "bilinmir"], 1, "Quru yer yağışın yağmadığını göstərir.", "Qaydanı tərsinə oxu."),
        Choice(Az, SkillArea.Logic, 9, "Hər kitab oxunanda bir stiker verilir. Aydında 4 stiker var. Neçə kitab oxuyub?", ["2", "3", "4", "8"], 2, "Hər stiker bir kitab deməkdir.", "Bir-bir uyğunlaşdır."),
        Choice(Az, SkillArea.Logic, 9, "Növbəti hansıdır? 2, 6, 12, 20, ___", ["21", "24", "28", "30"], 3, "Fərqlər 4, 6, 8, 10 kimi artır.", "Fərqləri yaz."),
        Choice(Az, SkillArea.Logic, 9, "Bütün mavi qutularda oyuncaq var. Bu qutuda oyuncaq yoxdur. Deməli qutu…", ["mavi deyil", "mütləq mavidir", "həm mavi, həm boşdur", "bilinmir"], 0, "Mavi olsaydı, içində oyuncaq olmalıydı.", "Qaydanı tərsinə oxu."),
        Choice(Az, SkillArea.Logic, 9, "Aygün Kamildən əvvəl, Kamil Nurdan əvvəl oynayır. İkinci kimdir?", ["Aygün", "Kamil", "Nur", "Bilinmir"], 1, "Sıra: Aygün, Kamil, Nur.", "Üçünü sıraya düz."),

        // Çətinlik 10 — tapmacalar
        Choice(Az, SkillArea.Logic, 10, "5 maşın 5 dəqiqəyə 5 oyuncaq düzəldir. 10 maşın 10 oyuncağa neçə dəqiqə sərf edir?", ["5 dəqiqə", "10 dəqiqə", "20 dəqiqə", "1 dəqiqə"], 0, "Hər maşın 5 dəqiqəyə 1 oyuncaq düzəldir, vaxt dəyişmir.", "Bir maşını düşün."),
        Choice(Az, SkillArea.Logic, 10, "Göldə zanbaqlar hər gün iki dəfə artır və 20-ci gün gölü tam örtür. Gölün yarısı neçənci gün örtülür?", ["10-cu gün", "15-ci gün", "18-ci gün", "19-cu gün"], 3, "Son gün iki dəfə artdığına görə bir gün əvvəl yarısıdır.", "Sonuncu günü geri get."),
        Choice(Az, SkillArea.Logic, 10, "3 səhifə oxumaq 6 dəqiqə çəkir. 10 səhifə neçə dəqiqə çəkər?", ["12", "16", "20", "30"], 2, "Bir səhifə 2 dəqiqədir, 10 səhifə isə 20.", "Əvvəlcə bir səhifəni tap."),
        Choice(Az, SkillArea.Logic, 10, "İki ata və iki oğul balıq tutdu, cəmi 3 balıq — hərəyə bir. Necə ola bilər?", ["Biri həm ata, həm oğuldur", "Biri balıq tutmadı", "Biri iki balıq tutdu", "Mümkün deyil"], 0, "Baba, ata və oğul — üç nəfər.", "Bir nəfər iki rolda ola bilər."),
        Choice(Az, SkillArea.Logic, 10, "Bir kərpicin ağırlığı 1 kq və yarım kərpicdir. Kərpic neçə kq-dır?", ["1", "1.5", "2", "3"], 2, "Yarım kərpic 1 kq-dırsa, tam kərpic 2 kq-dır.", "Yarısını tap, sonra ikiqat et."),
    ];

    // ---------------- Oxu ----------------

    private static IEnumerable<Question> Reading() =>
    [
        // Çətinlik 1 — bir cümlə, birbaşa cavab
        Choice(Az, SkillArea.Reading, 1, "«Ayanın balaca ağ pişiyi var. Onun adı Qardır.» Pişiyin adı nədir?", ["Aya", "Balaca", "Ağ", "Qar"], 3, "Mətndə pişiyin adının Qar olduğu deyilir.", "Cümləni yenidən oxu."),
        Choice(Az, SkillArea.Reading, 1, "«Elvin qırmızı velosipedini sürür.» Velosiped hansı rəngdədir?", ["mavi", "yaşıl", "qırmızı", "sarı"], 2, "Mətndə velosipedin qırmızı olduğu yazılıb.", "Rəng bildirən sözü tap."),
        Choice(Az, SkillArea.Reading, 1, "«Nərmin bağçada gül əkdi.» Nərmin harada gül əkdi?", ["məktəbdə", "bağçada", "evdə", "dükanda"], 1, "Cümlədə «bağçada» yazılıb.", "Yer bildirən sözü tap."),
        Choice(Az, SkillArea.Reading, 1, "«Balaca it stolun altında yatır.» İt harada yatır?", ["stolun üstündə", "stolun altında", "çarpayıda", "həyətdə"], 1, "İt stolun altındadır.", "«Altında» sözünə diqqət et."),
        Choice(Az, SkillArea.Reading, 1, "«Səhər Kamran süd içdi.» Kamran nə içdi?", ["su", "çay", "süd", "şirə"], 2, "Mətndə süd içdiyi deyilir.", "İçdiyi şeyi tap."),

        // Çətinlik 2 — iki cümlə, sadə səbəb
        Choice(Az, SkillArea.Reading, 2, "«Tülkü Max çayın yanında parlaq bir yarpaq tapdı.» Yarpaq harada idi?", ["mağarada", "damda", "çayın yanında", "çarpayının altında"], 2, "Mətndə yarpağın çayın yanında olduğu deyilir.", "Cümləni yenidən oxu."),
        Choice(Az, SkillArea.Reading, 2, "«Leyla top oynamağı sevir. Hər axşam həyətə düşür.» Leyla nə oynayır?", ["şahmat", "top", "kart", "gizlənqaç"], 1, "Birinci cümlədə top yazılıb.", "Birinci cümləyə bax."),
        Choice(Az, SkillArea.Reading, 2, "«Yağış yağırdı, ona görə uşaqlar içəridə oynadılar.» Uşaqlar niyə içəridə oynadılar?", ["soyuq idi", "gec idi", "top yox idi", "yağış yağırdı"], 3, "Səbəb mətndə birbaşa deyilir.", "«Ona görə» sözündən əvvələ bax."),
        Choice(Az, SkillArea.Reading, 2, "«Babam mənə ağac ev düzəltdi. O, çox möhkəmdir.» Ağac evi kim düzəltdi?", ["atam", "babam", "əmim", "qonşumuz"], 1, "Birinci cümlədə babası deyilir.", "Kim sözünü tap."),
        Choice(Az, SkillArea.Reading, 2, "«Səma qaralanda ulduzlar göründü.» Ulduzlar nə vaxt göründü?", ["səhər", "günorta", "səma qaralanda", "yağışda"], 2, "Ulduzlar səma qaralanda göründü.", "Vaxt bildirən hissəni tap."),

        // Çətinlik 3 — ardıcıllıq
        Choice(Az, SkillArea.Reading, 3, "«Lina çantasını yığdı, çəkmələrini geydi və məktəbə getdi.» Lina əvvəlcə nə etdi?", ["çəkmə geydi", "məktəbə getdi", "səhər yeməyi yedi", "çanta yığdı"], 3, "Çanta yığmaq birinci deyilir.", "Hərəkətlərin ardıcıllığına bax."),
        Choice(Az, SkillArea.Reading, 3, "«Aysel əvvəlcə əllərini yudu, sonra xəmiri yoğurdu, axırda peçenye bişirdi.» Axırda nə etdi?", ["əl yudu", "xəmir yoğurdu", "peçenye bişirdi", "süfrə açdı"], 2, "«Axırda» sözü sonuncu hərəkəti göstərir.", "«Axırda» sözünü tap."),
        Choice(Az, SkillArea.Reading, 3, "«Zəng çalındı, uşaqlar sinfə girdi, müəllim salam verdi.» Zəngdən dərhal sonra nə oldu?", ["müəllim salam verdi", "uşaqlar sinfə girdi", "dərs bitdi", "zəng yenə çaldı"], 1, "Sıra: zəng → sinfə girmək → salam.", "Sıranı barmağınla izlə."),
        Choice(Az, SkillArea.Reading, 3, "«Toxumu əkdik, suvardıq və bir həftə sonra yaşıl cücərti gördük.» Cücərti nə vaxt göründü?", ["dərhal", "ertəsi gün", "bir həftə sonra", "bir ay sonra"], 2, "Mətndə «bir həftə sonra» yazılıb.", "Vaxt bildirən sözü tap."),
        Choice(Az, SkillArea.Reading, 3, "«Rəşid velosipedi yudu, sonra yağladı, sonra sürdü.» Sürməzdən əvvəl sonuncu nə etdi?", ["yudu", "yağladı", "sürdü", "saxladı"], 1, "Sürmədən əvvəl yağladı.", "Bir addım geri get."),

        // Çətinlik 4 — nəticə çıxarma
        Choice(Az, SkillArea.Reading, 4, "«Səma boz oldu və hamı içəri qaçdı.» Yəqin ki, nə baş verdi?", ["günəş çıxdı", "qum yağdı", "şənlik başladı", "yağış başladı"], 3, "Boz səma və içəri qaçmaq yağışa işarədir.", "İpuclarından istifadə et."),
        Choice(Az, SkillArea.Reading, 4, "«Nərmin çətiri götürmədi. Evə gələndə saçları islanmışdı.» Nə baş verib?", ["qar yağıb", "yağış yağıb", "çətir itib", "külək əsib"], 1, "İslanmış saç yağışı göstərir.", "Nə islanmağa səbəb olur?"),
        Choice(Az, SkillArea.Reading, 4, "«Kamil qapını açanda tortun üstündə şamlar yanırdı və hamı gizlənmişdi.» Bu, nə gecəsidir?", ["ad günü", "Novruz", "yeni il", "məzuniyyət"], 0, "Şamlı tort və sürpriz ad gününü göstərir.", "Tortun üstündə şam nə deməkdir?"),
        Choice(Az, SkillArea.Reading, 4, "«Aylin dəftərini açdı, amma qələmi yox idi. Qonşusundan xahiş etdi.» Aylin nə istədi?", ["dəftər", "qələm", "silgi", "kitab"], 1, "Yalnız qələmi yox idi.", "Nəyin olmadığını tap."),
        Choice(Az, SkillArea.Reading, 4, "«Küçədə hamı qalın gödəkçə geyinmişdi, nəfəsdən buxar çıxırdı.» Hava necə idi?", ["isti", "soyuq", "yağışlı", "küləkli"], 1, "Qalın gödəkçə və buxar soyuğu göstərir.", "Nəfəsdən buxar nə vaxt çıxır?"),

        // Çətinlik 5 — hiss və səbəb
        Choice(Az, SkillArea.Reading, 5, "«Sam matçdan sonra bütün yol boyu gülümsədi.» Sam özünü necə hiss edirdi?", ["hirsli", "darıxmış", "qorxmuş", "xoşbəxt"], 3, "Gülümsəmək xoşbəxtliyi göstərir.", "Təbəssüm nə deməkdir?"),
        Choice(Az, SkillArea.Reading, 5, "«Aygün səhnəyə çıxanda əlləri titrəyirdi.» Aygün nə hiss edirdi?", ["həyəcan", "acgözlük", "yuxu", "hirs"], 0, "Titrəyən əllər həyəcanı göstərir.", "Səhnəyə çıxmaq necə hissdir?"),
        Choice(Az, SkillArea.Reading, 5, "«Nurun şarı partladı və o, ağladı.» Nur niyə ağladı?", ["şarı partladığı üçün", "acdığı üçün", "yorulduğu üçün", "qaçdığı üçün"], 0, "Səbəb elə birinci hissədə deyilir.", "Ağlamazdan əvvəl nə oldu?"),
        Choice(Az, SkillArea.Reading, 5, "«Elvin bütün həftə məşq etdi və yarışı uddu.» Elvin niyə uddu?", ["bəxti gətirdi", "çox məşq etdi", "rəqibi gəlmədi", "qaydanı pozdu"], 1, "Mətn məşqi qələbənin səbəbi kimi göstərir.", "Yarışdan əvvəl nə etdi?"),
        Choice(Az, SkillArea.Reading, 5, "«Leyla dostuna öz peçenyesini verdi.» Bu, Leylanın necə olduğunu göstərir?", ["xəsis", "hirsli", "comərd", "qorxaq"], 2, "Paylaşmaq comərdlikdir.", "Paylaşan adam necə olur?"),

        // Çətinlik 6 — sözün mətndəki mənası
        Choice(Az, SkillArea.Reading, 6, "«Köhnə körpü ağır yük maşınının altında iniltilə səsləndi.» «İnilti» bizə deyir ki, körpü…", ["alçaq səs çıxardı", "təzə tikilib", "rənglənib", "çox qısadır"], 0, "«İnilti» alçaq, cırıltılı səsi bildirir.", "Sözün səsini düşün."),
        Choice(Az, SkillArea.Reading, 6, "«Uşaq həyəcanla pıçıldadı.» «Pıçıldamaq» nə deməkdir?", ["qışqırmaq", "alçaq səslə danışmaq", "susmaq", "gülmək"], 1, "Pıçıltı çox alçaq səsdir.", "Sirri necə deyirsən?"),
        Choice(Az, SkillArea.Reading, 6, "«Nəhəng dalğa qayığı yellədi.» «Nəhəng» sözü nəyi bildirir?", ["çox kiçik", "çox soyuq", "çox sürətli", "çox böyük"], 3, "«Nəhəng» çox böyük deməkdir.", "Ölçü haqqında düşün."),
        Choice(Az, SkillArea.Reading, 6, "«Pişik səssizcə süzülüb otağa girdi.» «Süzülmək» burada nə deməkdir?", ["yumşaq və səssiz hərəkət etmək", "qaçmaq", "yıxılmaq", "dayanmaq"], 0, "«Süzülmək» yumşaq hərəkəti bildirir.", "«Səssizcə» sözü ipucudur."),
        Choice(Az, SkillArea.Reading, 6, "«Bulanıq su ilə heç nə görünmürdü.» «Bulanıq» nə deməkdir?", ["duru", "təmiz", "çirkli və qeyri-şəffaf", "isti"], 2, "Bulanıq suyun içi görünmür.", "Niyə heç nə görünmür?"),

        // Çətinlik 7 — mətnin növü və əsas fikir
        Choice(Az, SkillArea.Reading, 7, "Heyvanlar vasitəsilə dərs verən hekayə necə adlanır?", ["təmsil", "resept", "gündəlik", "hesabat"], 0, "Təmsillərdə heyvanlar vasitəsilə ibrət verilir.", "Tısbağa və dovşanı xatırla."),
        Choice(Az, SkillArea.Reading, 7, "«Əvvəlcə unu ələyin, sonra yumurta əlavə edin, 20 dəqiqə bişirin.» Bu mətn nədir?", ["hekayə", "resept", "şeir", "məktub"], 1, "Addım-addım hazırlanma resept deməkdir.", "Mətn nə etməyi öyrədir?"),
        Choice(Az, SkillArea.Reading, 7, "Bir mətnin ƏSAS FİKRİ nə deməkdir?", ["mətnin ən uzun cümləsi", "mətnin nədən bəhs etdiyi", "ilk söz", "müəllifin adı"], 1, "Əsas fikir mətnin nədən bəhs etdiyidir.", "Mətn ümumilikdə nə haqdadır?"),
        Choice(Az, SkillArea.Reading, 7, "«Dünən zooparka getdik. Ən çox zürafə xoşuma gəldi.» Bu mətn hansı növdəndir?", ["gündəlik qeydi", "elmi məqalə", "reklam", "resept"], 0, "Şəxsi təəssürat gündəlikdir.", "Kim danışır?"),
        Choice(Az, SkillArea.Reading, 7, "Şeiri nəsr mətnindən fərqləndirən nədir?", ["misralara bölünməsi", "uzunluğu", "başlığı", "şəkli"], 0, "Şeir misralara bölünür.", "Səhifədə necə görünür?"),

        // Çətinlik 8 — bağlayıcılar
        Choice(Az, SkillArea.Reading, 8, "«Yağışa baxmayaraq, komanda oynamağa davam etdi.» «Baxmayaraq» nə göstərir?", ["səbəb", "zaman", "ziddiyyət", "yer"], 2, "«Baxmayaraq» gözləniləndən fərqli nəticəni bildirir.", "Yağış adətən oyunu dayandırır."),
        Choice(Az, SkillArea.Reading, 8, "«Gec yatdığı üçün səhər qalxa bilmədi.» «Üçün» burada nə bildirir?", ["zaman", "səbəb", "yer", "məqsəd"], 1, "«Üçün» səbəbi göstərir.", "Niyə qalxa bilmədi?"),
        Choice(Az, SkillArea.Reading, 8, "«Əvvəlcə dərsi bitirdi, sonra oynamağa çıxdı.» «Sonra» sözü nəyi bildirir?", ["zaman ardıcıllığını", "səbəbi", "yeri", "sayı"], 0, "«Sonra» hərəkətlərin sırasını göstərir.", "Hansı əvvəl oldu?"),
        Choice(Az, SkillArea.Reading, 8, "«Nə kitab, nə də dəftər gətirmişdi.» Bu cümlə nə bildirir?", ["hər ikisini gətirib", "heç birini gətirməyib", "yalnız kitab gətirib", "yalnız dəftər gətirib"], 1, "«Nə… nə də» hər ikisinin yoxluğunu bildirir.", "İnkarı oxu."),
        Choice(Az, SkillArea.Reading, 8, "«O, yorğun idi, lakin işini bitirdi.» «Lakin» sözü nəyi göstərir?", ["gözləniləndən fərqli nəticəni", "səbəbi", "zamanı", "yeri"], 0, "Yorğun adamdan işi yarımçıq qoymaq gözlənilir.", "Gözlənilən nə idi?"),

        // Çətinlik 9 — niyyət və dərin nəticə
        Choice(Az, SkillArea.Reading, 9, "«Kamran cavabı bilirdi, amma əlini qaldırmadı. Sonra peşman oldu.» Kamran əvvəl nə hiss edib?", ["sevinib", "acıb", "hirslənib", "utanıb"], 3, "Bilib də susmaq utancaqlığı göstərir.", "Nə onu saxladı?"),
        Choice(Az, SkillArea.Reading, 9, "«Aysel dostunun şəklini gizlətdi ki, sürpriz pozulmasın.» Aysel niyə belə etdi?", ["şəkli bəyənmədi", "sürprizi qorumaq üçün", "dostuna acığı tutdu", "şəkil pis idi"], 1, "Səbəb «ki» sözündən sonra deyilir.", "Məqsədi tap."),
        Choice(Az, SkillArea.Reading, 9, "«Qonşu qapını döydü: “Sizin qapı açıq qalıb”, — dedi.» Qonşu nə etməyə gəlmişdi?", ["xəbərdarlıq etməyə", "qonaq olmağa", "açar istəməyə", "borc almağa"], 0, "Qonşu diqqətsizliyi xəbər verir.", "Nə dedi?"),
        Choice(Az, SkillArea.Reading, 9, "«Müəllim dəftəri qaytardı və “bu dəfə daha diqqətli oxu”, — dedi.» Bu, nəyi göstərir?", ["işdə səhv olub", "iş əladır", "dəftər itib", "dərs bitib"], 0, "«Daha diqqətli» sözü səhvə işarədir.", "Niyə belə deyər?"),
        Choice(Az, SkillArea.Reading, 9, "«Nur hər gün eyni yolla gedirdi, bu gün başqa küçəyə döndü və gec çatdı.» Niyə gec çatdı?", ["yolu dəyişdiyi üçün", "tez çıxdığı üçün", "qaçdığı üçün", "yaxın yol tapdığı üçün"], 0, "Dəyişən yeganə şey yol idi.", "Bu gün nə fərqli oldu?"),

        // Çətinlik 10 — müəllifin məqsədi
        Choice(Az, SkillArea.Reading, 10, "«Dişlərinizi gündə iki dəfə fırçalayın — bu, diş ağrısının qarşısını alır.» Müəllifin məqsədi nədir?", ["məsləhət vermək", "əyləndirmək", "hekayə danışmaq", "satmaq"], 0, "Mətn oxucuya nə etməyi məsləhət görür.", "Mətn səndən nə istəyir?"),
        Choice(Az, SkillArea.Reading, 10, "«Ən yaxşı velosiped bizdədir! İndi al, 50% endirim!» Bu mətnin məqsədi nədir?", ["öyrətmək", "satmaq", "hekayə danışmaq", "xəbər vermək"], 1, "Endirim və çağırış reklamdır.", "Mətn nəyi təklif edir?"),
        Choice(Az, SkillArea.Reading, 10, "Uzun mətni bir-iki cümlə ilə demək necə adlanır?", ["xülasə", "başlıq", "təsvir", "dialoq"], 0, "Xülasə mətnin qısa məzmunudur.", "Qısaca danışmaq necə adlanır?"),
        Choice(Az, SkillArea.Reading, 10, "«Dünən şəhərdə güclü külək əsdi, iki ağac aşdı, xəsarət alan olmadı.» Bu mətn hansı növdəndir?", ["nağıl", "şeir", "resept", "xəbər"], 3, "Baş vermiş hadisənin qısa təsviri xəbərdir.", "Mətn nə vaxtdan bəhs edir?"),
        Choice(Az, SkillArea.Reading, 10, "Mətndə «lakin», «ancaq» sözləri çox işlənirsə, müəllif nə edir?", ["iki fikri qarşılaşdırır", "sayır", "təsvir edir", "sual verir"], 0, "Bu sözlər ziddiyyət bildirir.", "Bu sözlər nəyi birləşdirir?"),
    ];

    // ---------------- Elm ----------------

    private static IEnumerable<Question> Science() =>
    [
        // Çətinlik 1 — ətraf aləm
        Choice(Az, SkillArea.Science, 1, "Hansı heyvan uça bilir?", ["it", "balıq", "pişik", "quş"], 3, "Quşların qanadı var və uça bilirlər.", "Qanad axtar."),
        Choice(Az, SkillArea.Science, 1, "Gecə səmada nə görünür?", ["günəş", "ay və ulduzlar", "göy qurşağı", "qar"], 1, "Gecə ay və ulduzlar görünür.", "Gecə yuxarı bax."),
        Choice(Az, SkillArea.Science, 1, "Balıq harada yaşayır?", ["ağacda", "havada", "suda", "qumda"], 2, "Balıqlar suda yaşayır.", "Üzmək üçün nə lazımdır?"),
        Choice(Az, SkillArea.Science, 1, "Hansı isti verir?", ["buz", "günəş", "daş", "külək"], 1, "Günəş istilik və işıq verir.", "Yayda nə isidir?"),
        Choice(Az, SkillArea.Science, 1, "İnsanın neçə gözü var?", ["bir", "iki", "üç", "dörd"], 1, "İnsanın iki gözü var.", "Üzünə toxun və say."),

        // Çətinlik 2 — canlılar
        Choice(Az, SkillArea.Science, 2, "Bitkilərin böyüməsi üçün nə lazımdır?", ["qum və şüşə", "plastik", "metal", "günəş işığı və su"], 3, "Bitkilərə günəş işığı və su lazımdır.", "Bağçanı düşün."),
        Choice(Az, SkillArea.Science, 2, "Hansı canlıdır?", ["daş", "ağac", "stol", "qaşıq"], 1, "Ağac böyüyür, deməli canlıdır.", "Hansı böyüyür?"),
        Choice(Az, SkillArea.Science, 2, "Süd hansı heyvandan alınır?", ["toyuqdan", "inəkdən", "balıqdan", "arıdan"], 1, "Süd inəkdən alınır.", "Fermanı düşün."),
        Choice(Az, SkillArea.Science, 2, "Bal hansı canlı düzəldir?", ["qarışqa", "arı", "kəpənək", "milçək"], 1, "Balı arılar düzəldir.", "Pətəyi düşün."),
        Choice(Az, SkillArea.Science, 2, "Hansı hava hadisəsidir?", ["yağış", "kitab", "stol", "ayaqqabı"], 0, "Yağış hava hadisəsidir.", "Hansı göydən gəlir?"),

        // Çətinlik 3 — işıq, materiallar, fəsillər
        Choice(Az, SkillArea.Science, 3, "Kölgə nə vaxt yaranır?", ["hava soyuyanda", "işıq əşyaya düşəndə", "külək əsəndə", "gecə düşəndə"], 1, "İşıq əşya tərəfindən kəsiləndə kölgə yaranır.", "Günəşdə əlini qaldır."),
        Choice(Az, SkillArea.Science, 3, "Gündüz niyə işıqlı olur?", ["ay işıq verir", "ulduzlar yanır", "buludlar parlayır", "günəş işıq verir"], 3, "Gündüz işığı Günəşdən gəlir.", "Ən böyük işıq mənbəyi nədir?"),
        Choice(Az, SkillArea.Science, 3, "Hansı material şəffafdır (arxası görünür)?", ["şüşə", "taxta", "dəmir", "karton"], 0, "Şüşədən işıq keçir.", "Pəncərəyə bax."),
        Choice(Az, SkillArea.Science, 3, "Qar hansı fəsildə yağır?", ["yayda", "payızda", "qışda", "yazda"], 2, "Qar qışda yağır.", "Ən soyuq fəsil hansıdır?"),
        Choice(Az, SkillArea.Science, 3, "Buz nədən əmələ gəlir?", ["qumdan", "sudan", "torpaqdan", "havadan"], 1, "Buz donmuş sudur.", "Dondurucunu düşün."),

        // Çətinlik 4 — maddə və hava
        Choice(Az, SkillArea.Science, 4, "Su nə vaxt buza çevrilir?", ["qızanda", "səslənəndə", "çirklənəndə", "soyuyanda"], 3, "Su kifayət qədər soyuyanda donur.", "Dondurucunu düşün."),
        Choice(Az, SkillArea.Science, 4, "Termometr nəyi ölçür?", ["uzunluğu", "ağırlığı", "temperaturu", "vaxtı"], 2, "Termometr temperaturu ölçür.", "Xəstələnəndə nə ölçülür?"),
        Choice(Az, SkillArea.Science, 4, "Yağış haradan yağır?", ["torpaqdan", "buludlardan", "dənizdən", "ağaclardan"], 1, "Yağış buludlardan yağır.", "Yağışdan əvvəl göyə bax."),
        Choice(Az, SkillArea.Science, 4, "İldə neçə fəsil var?", ["iki", "üç", "dörd", "beş"], 2, "Dörd fəsil var: yaz, yay, payız, qış.", "Fəsilləri sadala."),
        Choice(Az, SkillArea.Science, 4, "Hansı maddə mayedir?", ["daş", "su", "dəmir", "taxta"], 1, "Su mayedir — qabın formasını alır.", "Hansı axır?"),

        // Çətinlik 5 — Yer və kosmos
        Choice(Az, SkillArea.Science, 5, "Biz hansı planetdə yaşayırıq?", ["Mars", "Venera", "Yer", "Yupiter"], 2, "Biz Yer planetində yaşayırıq.", "Qlobusa bax."),
        Choice(Az, SkillArea.Science, 5, "Ay Yerin nəyidir?", ["ulduzu", "peyki", "planeti", "buludu"], 1, "Ay Yerin təbii peykidir.", "Ay Yerin ətrafında nə edir?"),
        Choice(Az, SkillArea.Science, 5, "Günəş sistemində neçə planet var?", ["altı", "yeddi", "səkkiz", "doqquz"], 2, "Səkkiz planet var.", "Merkuridən Neptuna qədər say."),
        Choice(Az, SkillArea.Science, 5, "Gecə və gündüzün növbələşməsinin səbəbi nədir?", ["Yerin öz oxu ətrafında fırlanması", "Yerin dayanması", "Ayın böyüməsi", "buludlar"], 0, "Yer fırlandıqca gecə və gündüz növbələşir.", "Yer nə edir?"),
        Choice(Az, SkillArea.Science, 5, "Kompasın oxu hansı istiqaməti göstərir?", ["şərqi", "qərbi", "şimalı", "aşağını"], 2, "Kompas şimalı göstərir.", "Səyyahlar nə ilə yol tapır?"),

        // Çətinlik 6 — canlıların qrupları
        Choice(Az, SkillArea.Science, 6, "Yerə ən yaxın ulduz hansıdır?", ["Ay", "Qütb ulduzu", "Mars", "Günəş"], 3, "Günəş bizə ən yaxın ulduzdur.", "Gündüzü işıqlandırır."),
        Choice(Az, SkillArea.Science, 6, "Hansı heyvan soyuqqanlıdır?", ["it", "quş", "kərtənkələ", "pişik"], 2, "Kərtənkələ soyuqqanlıdır — bədəni ətraf mühitə görə isinir.", "Kim günəşdə isinir?"),
        Choice(Az, SkillArea.Science, 6, "Hansı heyvan məməlidir?", ["balıq", "ilan", "yarasa", "qurbağa"], 2, "Yarasa uçsa da məməlidir.", "Balasını südlə kim bəsləyir?"),
        Choice(Az, SkillArea.Science, 6, "Kəpənək əvvəlcə nə olur?", ["tırtıl", "quş", "balıq", "qarışqa"], 0, "Kəpənək tırtıldan inkişaf edir.", "Barama haqqında düşün."),
        Choice(Az, SkillArea.Science, 6, "Hansı heyvan qışda yuxuya gedir?", ["ayı", "at", "inək", "dovşan"], 0, "Ayı qış yuxusuna gedir.", "Qışda kim yatır?"),

        // Çətinlik 7 — bitkilər və qida zənciri
        Choice(Az, SkillArea.Science, 7, "Bitkinin hansı hissəsi torpaqdan su çəkir?", ["yarpaq", "çiçək", "kök", "toxum"], 2, "Köklər torpaqdan su çəkir.", "Yerin altına bax."),
        Choice(Az, SkillArea.Science, 7, "Bitkilər havaya hansı qazı buraxır?", ["oksigen", "helium", "azot", "hidrogen"], 0, "Bitkilər oksigen buraxır.", "Meşədə nəfəs almaq niyə rahatdır?"),
        Choice(Az, SkillArea.Science, 7, "Qida zəncirində yalnız bitki yeyən heyvan necə adlanır?", ["yırtıcı", "otyeyən", "parazit", "göbələk"], 1, "Otyeyən heyvanlar bitki ilə qidalanır.", "İnək nə yeyir?"),
        Choice(Az, SkillArea.Science, 7, "Yarpaqlar niyə yaşıldır?", ["xlorofil olduğu üçün", "su olduğu üçün", "günəş isti olduğu üçün", "torpaq yaşıl olduğu üçün"], 0, "Yaşıl rəngi xlorofil verir.", "Yarpaqda hansı maddə var?"),
        Choice(Az, SkillArea.Science, 7, "Toxumdan nə çıxır?", ["daş", "yeni bitki", "su", "hava"], 1, "Toxumdan yeni bitki cücərir.", "Nə əkirik?"),

        // Çətinlik 8 — insan bədəni
        Choice(Az, SkillArea.Science, 8, "İnsan nəfəs almaq üçün hansı qaza ehtiyac duyur?", ["helium", "yalnız azot", "karbon qazı", "oksigen"], 3, "İnsan oksigenlə nəfəs alır.", "Hava haqqında düşün."),
        Choice(Az, SkillArea.Science, 8, "Ürək nə edir?", ["yeməyi həzm edir", "havanı təmizləyir", "qanı bədəndə hərəkət etdirir", "sümüyü möhkəmləndirir"], 2, "Ürək bir nasos kimi qanı hərəkət etdirir.", "Döş qəfəsində nə döyünür?"),
        Choice(Az, SkillArea.Science, 8, "Skelet nə üçündür?", ["bədəni saxlayır və qoruyur", "yalnız qidalandırır", "havanı təmizləyir", "isti verir"], 0, "Sümüklər bədənə dayaq olur və orqanları qoruyur.", "Sümük olmasa nə olardı?"),
        Choice(Az, SkillArea.Science, 8, "Hansı orqanla eşidirik?", ["göz", "qulaq", "burun", "dil"], 1, "Səsi qulaqla eşidirik.", "Səsi hansı orqan tutur?"),
        Choice(Az, SkillArea.Science, 8, "Ağciyər nə edir?", ["havanı alır və verir", "qan süzür", "yemək həzm edir", "sümük düzəldir"], 0, "Ağciyər nəfəs almağı təmin edir.", "Nəfəs alanda nə şişir?"),

        // Çətinlik 9 — su dövranı və hadisələr
        Choice(Az, SkillArea.Science, 9, "Suyun buxara çevrilməsi necə adlanır?", ["eroziya", "cazibə", "əks olunma", "buxarlanma"], 3, "Buxarlanma mayeni buxara çevirir.", "Qaynayan çaydanı düşün."),
        Choice(Az, SkillArea.Science, 9, "Su dövranında yağışdan əvvəl nə baş verir?", ["buxarlanma və buludların yaranması", "zəlzələ", "qar əriməsi", "gecənin düşməsi"], 0, "Su buxarlanır, bulud olur, sonra yağış yağır.", "Su göyə necə qalxır?"),
        Choice(Az, SkillArea.Science, 9, "Səs necə yayılır?", ["havada dalğa kimi", "yalnız suda", "boşluqda", "işıq şüası ilə"], 0, "Səs havada dalğa şəklində yayılır.", "Səsə nə lazımdır?"),
        Choice(Az, SkillArea.Science, 9, "Maqnit hansı əşyanı çəkir?", ["dəmir qapağı", "plastik qaşığı", "taxta xətkeşi", "kağızı"], 0, "Maqnit dəmiri çəkir.", "Hansı metaldır?"),
        Choice(Az, SkillArea.Science, 9, "Kölgə nə vaxt uzun olur?", ["günəş alçaq olanda", "günəş baş üstündə olanda", "gecə", "yağışda"], 0, "Günəş üfüqə yaxın olanda kölgə uzanır.", "Axşam kölgənə bax."),

        // Çətinlik 10 — qüvvələr və enerji
        Choice(Az, SkillArea.Science, 10, "Hansı qüvvə əşyaları Yerə doğru çəkir?", ["sürtünmə", "maqnetizm", "təzyiq", "cazibə"], 3, "Cazibə qüvvəsi əşyaları Yerə çəkir.", "Top niyə aşağı düşür?"),
        Choice(Az, SkillArea.Science, 10, "Sürtünmə nəyə səbəb olur?", ["hərəkəti yavaşladır", "hərəkəti sürətləndirir", "ağırlığı artırır", "işıq yaradır"], 0, "Sürtünmə hərəkətə müqavimət göstərir.", "Buzda niyə sürüşürsən?"),
        Choice(Az, SkillArea.Science, 10, "Enerji hansı formalarda olur?", ["yalnız istilik", "istilik, işıq və hərəkət", "yalnız işıq", "yalnız səs"], 1, "Enerjinin bir neçə forması var.", "Lampa nə verir?"),
        Choice(Az, SkillArea.Science, 10, "Ay tutulması nə vaxt baş verir?", ["Yer Günəşlə Ay arasına düşəndə", "Ay Günəşin içinə girəndə", "gecə uzun olanda", "qışda"], 0, "Yerin kölgəsi Ayın üzərinə düşür.", "Kölgəni kim salır?"),
        Choice(Az, SkillArea.Science, 10, "Niyə kosmosda səs eşidilmir?", ["hava (mühit) olmadığı üçün", "çox soyuq olduğu üçün", "çox uzaq olduğu üçün", "qaranlıq olduğu üçün"], 0, "Səsin yayılması üçün mühit lazımdır.", "Səs nədə yayılır?"),
    ];
}
