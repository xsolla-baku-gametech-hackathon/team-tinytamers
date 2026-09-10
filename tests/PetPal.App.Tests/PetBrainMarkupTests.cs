using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PetPal.App.Tests;

/// <summary>
/// Pet Brain ekranlarının mənbə səviyyəsindəki qaydaları.
///
/// <para>Bu qaydaların hamısı pozulanda app KOMPİLYASİYA OLUNUR və işləyir —
/// səhv yalnız başqa telefonda, başqa dildə və ya ekran oxuyucusu ilə görünür.
/// Ona görə mətnin özündə qorunur.</para>
/// </summary>
public class PetBrainMarkupTests
{
    private static readonly string[] Components =
    [
        "ExperienceShell", "ExperienceScene", "StageChoices", "RunSummary", "BrainDebugPanel",
        "PuzzleBoard", "OrderedRoutePuzzle", "SequenceOrderPuzzle", "RouteLogicPuzzle", "LightFragmentsPuzzle",
        "RecapPlayer"
    ];

    /// <summary>
    /// Öz sabit mətni olan komponentlər — onlar mütləq <c>Loc.T</c> işlətməlidir.
    ///
    /// <para>Siyahıda olmayan ikisi qəsdən kənardadır: <c>ExperienceScene</c>
    /// tamamilə dekorativdir (<c>aria-hidden</c>), <c>StageChoices</c> isə bütün
    /// mətni SERVERDƏN alır — hər ikisində tərcümə ediləsi sabit mətn yoxdur.</para>
    /// </summary>
    private static readonly string[] ComponentsWithOwnText =
    [
        "ExperienceShell", "RunSummary", "BrainDebugPanel", "PuzzleBoard", "OrderedRoutePuzzle",
        "LightFragmentsPuzzle", "RecapPlayer"
    ];

    /// <summary>Hər QAPALI mexanikanın öz təqdimat komponenti.</summary>
    private static readonly string[] PuzzleComponents =
    [
        "OrderedRoutePuzzle", "SequenceOrderPuzzle", "RouteLogicPuzzle", "LightFragmentsPuzzle",
        "SignalPatternPuzzle", "ObservationRecallPuzzle", "MatchingPairsPuzzle"
    ];

    // ==================== Marşrut və klient ====================

    [Fact]
    public void PetBrain_OzMarsrutunaSahibdir()
    {
        Assert.Contains("@page \"/pet-brain\"", ReadPage("PetBrain.razor"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Ekran TİPLİ klient metodlarını çağırır — çılpaq <c>HttpClient</c> yoxdur.
    /// Əks halda marşrut və gövdə iki yerdə saxlanardı.
    /// </summary>
    [Fact]
    public void PetBrain_TipliKlientMetodlariniIsledir()
    {
        var page = ReadPage("PetBrain.razor");

        foreach (var method in new[]
                 {
                     "GetPetBrainAsync", "StartPetBrainRunAsync", "SubmitPetBrainChoiceAsync",
                     "RequestPetBrainHintAsync", "CompletePetBrainRunAsync", "AbandonPetBrainRunAsync"
                 })
            Assert.Contains(method, page, StringComparison.Ordinal);

        Assert.DoesNotContain("new HttpClient", page, StringComparison.Ordinal);
    }

    /// <summary>Klientdə hər metod mövcud olmalıdır — ekran ona istinad edir.</summary>
    [Fact]
    public void GameApiClient_PetBrainMetodlariniDasiyir()
    {
        var client = ReadService("GameApiClient.cs");

        Assert.Contains("api/pet-brain", client, StringComparison.Ordinal);
        Assert.Contains("/complete", client, StringComparison.Ordinal);
        Assert.Contains("/abandon", client, StringComparison.Ordinal);
        Assert.Contains("/choices", client, StringComparison.Ordinal);
    }

    /// <summary>
    /// Klient keşi AKTİV UŞAĞA bağlıdır: profil dəyişəndə bir an da olsa
    /// başqa uşağın tövsiyəsi göstərilə bilməz.
    /// </summary>
    [Fact]
    public void PetBrainState_AktivUsagaBaglidir()
    {
        var state = ReadService("PetBrainState.cs");

        Assert.Contains("ActiveChildId", state, StringComparison.Ordinal);
        Assert.Contains("_session.Changed", state, StringComparison.Ordinal);
    }

    // ==================== Hər iki macəra oynanandır ====================

    /// <summary>
    /// Səhnə komponenti hər iki macərəni AYRICA çəkir — ikisi eyni görünsəydi
    /// "fərqli təcrübə" iddiası boş qalardı.
    /// </summary>
    [Fact]
    public void Sehne_MarsVeEjdahaniAyricaCekir()
    {
        var scene = ReadComponent("ExperienceScene");

        Assert.Contains("case \"mars\":", scene, StringComparison.Ordinal);
        Assert.Contains("case \"dragon\":", scene, StringComparison.Ordinal);

        // Əjdaha uşağın seçdiyi palitraya və naxışa görə dəyişir.
        Assert.Contains("pbx-dragon--@Palette", scene, StringComparison.Ordinal);
        Assert.Contains("pbx-dragon__pattern--@Pattern", scene, StringComparison.Ordinal);

        // Mars isə seçilən əraziyə və xilas üsuluna görə.
        Assert.Contains("solar-panel", scene, StringComparison.Ordinal);
        Assert.Contains("crater", scene, StringComparison.Ordinal);
    }

    /// <summary>
    /// Hər üç ekran ailəsinin qarşılığı var: seçim, tapmaca və variantsız.
    ///
    /// <para>Variantsız ekranlar (giriş, nəticə, hekayə, qurma…) BİR yolla
    /// emal olunur və fərqi <c>NodePresentation</c> reyestrindən alır — ona
    /// görə burada ayrı-ayrı növ adları axtarılmır.</para>
    /// </summary>
    [Fact]
    public void PetBrain_ButunMerheleNovleriniCizir()
    {
        var page = ReadPage("PetBrain.razor");

        Assert.Contains("<StageChoices", page, StringComparison.Ordinal);

        // Tapmaca mərhələsi ayrıca lövhəyə gedir — ipucu da oradadır.
        Assert.Contains("<PuzzleBoard", page, StringComparison.Ordinal);
        Assert.Contains("Stage.Puzzle is", page, StringComparison.Ordinal);

        Assert.Contains("NodePresentation.ContinueLabel", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// QAPALI mexanika açarlarının HƏR BİRİ tanınan Razor komponentinə bağlıdır.
    ///
    /// <para>Açarlar kataloqun MƏNBƏYİNDƏN oxunur: yeni mexanika əlavə edib
    /// komponent yazmamaq səssiz xətadır — lövhə heç nə çəkməz və uşaq boş
    /// ekran görər.</para>
    /// </summary>
    [Fact]
    public void HerTapmacaMexanikasi_TaninanKomponenteBaglidir()
    {
        // Yalnız MEXANİKA enum-u oxunur: fayldakı digər enum-lar (cavab sxemi,
        // vəziyyət) komponentə bağlanmır və siyahını çirkləndirməməlidir.
        var source = ReadEnums();
        var start = source.IndexOf("enum PetBrainPuzzleMechanic", StringComparison.Ordinal);
        Assert.True(start > 0, "PetBrainPuzzleMechanic tapılmadı — test köhnəlib.");

        var end = source.IndexOf('}', start);
        var block = source[start..end];

        var mechanics = Regex.Matches(block, @"^\s{4}(\w+) = \d+,?$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.True(mechanics.Count >= 3, $"Yalnız {mechanics.Count} mexanika oxundu — test köhnəlib.");

        var board = ReadComponent("PuzzleBoard");

        foreach (var mechanic in mechanics)
            Assert.Contains($"PetBrainPuzzleMechanic.{mechanic}", board, StringComparison.Ordinal);

        foreach (var component in PuzzleComponents)
            Assert.Contains($"<{component}", board, StringComparison.Ordinal);
    }

    /// <summary>
    /// Server HAZIR HTML göndərmir — komponentlər yalnız mətn və id işlədir.
    ///
    /// <para><c>MarkupString</c> serverdən gələn məzmunu olduğu kimi DOM-a
    /// buraxardı; uşaq tətbiqində bu, qəbuledilməzdir.</para>
    /// </summary>
    [Fact]
    public void TapmacaKomponentleri_ServerHtmlRenderEtmir()
    {
        foreach (var component in PuzzleComponents.Append("PuzzleBoard"))
        {
            var source = ReadComponent(component);

            Assert.DoesNotContain("MarkupString", source, StringComparison.Ordinal);
            Assert.DoesNotContain("@((MarkupString)", source, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Rəsmin vəziyyəti overlay HƏNDƏSƏSİNİ dəyişmir (test 46).
    ///
    /// <para>Bu, sadəcə səliqə deyil: rəsm gec gəlirsə və toxunuş hədəfləri
    /// yerini dəyişsə, uşaq məhz basmaq üzrə olduğu düyünü itirərdi. Ona görə
    /// düyünlərin, yuvaların və xətlərin mövqeyi YALNIZ serverin normallaşdırılmış
    /// <c>x</c>/<c>y</c> dəyərlərindən gəlməlidir — <c>SceneImage</c>-dən asılı
    /// olan tək şey <c>&lt;img&gt;</c> teqidir.</para>
    /// </summary>
    [Fact]
    public void SehneninVeziyyeti_ToxunusHedeflerimiTerpetmir()
    {
        foreach (var component in new[] { "OrderedRoutePuzzle", "LightFragmentsPuzzle" })
        {
            var source = StripComments(ReadComponent(component));

            // Mövqe həmişə serverin normallaşdırılmış dəyərindəndir.
            Assert.Contains("style=\"left:@(", source, StringComparison.Ordinal);

            // SceneImage yalnız BİR şərtdə işlənir: fon şəklinin olub-olmaması.
            var uses = Regex.Matches(source, @"SceneImage").Count;
            var guarded = Regex.Matches(source, @"IsNullOrEmpty\(SceneImage\)").Count;

            // Bir yoxlama, bir <img src>, bir [Parameter] — başqa yerdə yox.
            Assert.True(uses <= 3, $"{component}: SceneImage {uses} yerdə işlənir — həndəsəyə sızma riski.");
            Assert.Equal(1, guarded);

            // Şərtin İÇİNDƏ yalnız <img> olmalıdır: düyünlər kənarda qalır.
            var block = Regex.Match(source, @"IsNullOrEmpty\(SceneImage\)\s*\)\s*\{(.*?)\}", RegexOptions.Singleline);

            Assert.True(block.Success, $"{component}: rəsm şərti tapılmadı.");
            Assert.DoesNotContain("<button", block.Groups[1].Value, StringComparison.Ordinal);
            Assert.DoesNotContain("pbx-node", block.Groups[1].Value, StringComparison.Ordinal);
            Assert.DoesNotContain("pbx-slot", block.Groups[1].Value, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Tapmaca rəsmsiz də ANLAŞILANDIR (test 47).
    ///
    /// <para>Rəsm <c>aria-hidden</c>-dır və boş <c>alt</c> daşıyır — yəni ekran
    /// oxuyucusu onu heç görmür. Deməli mənanı daşıyan hər şey mətndə olmalıdır:
    /// göstəriş, düyün adları və enerji rəqəmi.</para>
    /// </summary>
    [Fact]
    public void Tapmaca_ResmsizDeAnlasiliandir()
    {
        var route = StripComments(ReadComponent("OrderedRoutePuzzle"));

        // Fon rəsmi ekran oxuyucusuna GÖRÜNMÜR.
        Assert.Contains("alt=\"\" aria-hidden=\"true\"", route, StringComparison.Ordinal);

        // Düyünün adı və rolu mətnlə verilir.
        Assert.Contains("aria-label=\"@AriaFor(node, step)\"", route, StringComparison.Ordinal);

        // Enerji həm zolaq, HƏM DƏ rəqəmdir — zolaq tək daşıyıcı deyil.
        Assert.Contains("pbx-route__energy-value", route, StringComparison.Ordinal);

        // Göstəriş və hekayə cümləsi lövhədə həmişə görünür.
        var board = StripComments(ReadComponent("PuzzleBoard"));
        Assert.Contains("Puzzle.StoryPrompt", board, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tapmacadakı hər SEÇİM düyməsi oxunan ad və vəziyyət daşıyır.
    ///
    /// <para>Yoxlama mətn axtarışı deyil, quruluş yoxlamasıdır: hər
    /// <c>aria-pressed</c> daşıyan düymənin eyni zamanda <c>aria-label</c>-i
    /// olmalıdır. Səbəb odur ki, düymələrin görünən mətni yalnız emoji ola
    /// bilər (marşrut düyünləri belədir) — ekran oxuyucusu onu oxuya bilmir.</para>
    /// </summary>
    [Fact]
    public void TapmacaElementleri_ElcatanliqIsaresiDasiyir()
    {
        foreach (var component in PuzzleComponents)
        {
            var source = ReadComponent(component);

            // Bəzəyi ekran oxuyucusundan gizlədən işarə hər lövhədə olmalıdır.
            Assert.Contains("aria-hidden=\"true\"", source, StringComparison.Ordinal);

            // Teq atributlarını regex ilə kəsmək OLMAZ: Razor ifadələri
            // (`@(step >= 0 ? …)`) teqin içində `>` daşıyır. Ona görə bütöv
            // element — açılışdan bağlanışa qədər — götürülür.
            var selectable = Regex
                .Matches(source, "<button.*?</button>", RegexOptions.Singleline)
                .Where(b => b.Value.Contains("aria-pressed", StringComparison.Ordinal))
                .ToList();

            Assert.True(selectable.Count > 0, $"{component}: seçim düyməsi tapılmadı.");

            foreach (var button in selectable)
            {
                Assert.True(
                    button.Value.Contains("aria-label=", StringComparison.Ordinal),
                    $"{component}: seçim düyməsinin oxunan adı yoxdur.");
            }
        }

        // Seçimin vəziyyəti canlı sahədə elan olunur.
        Assert.Contains("aria-live=\"polite\"", ReadComponent("PuzzleBoard"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Recap-ın DÖRD vəziyyətində də mükafat hərəkətləri işlək qalır (test 48).
    ///
    /// <para>Video gec gəlsə, rədd olunsa və ya heç gəlməsə də «Əla!» düyməsi
    /// yerindədir və altyazılar görünür: uşaq spinner-də ilişib qalmır.</para>
    /// </summary>
    [Fact]
    public void Recap_ButunVeziyyetlerdeMukafatIslekQalir()
    {
        var summary = StripComments(ReadComponent("RunSummary"));

        // Oynadıcı ŞƏRTSİZ render olunur — vəziyyətdən asılı deyil.
        Assert.Contains("<RecapPlayer Recap=\"Summary.Recap\"", summary, StringComparison.Ordinal);

        var player = StripComments(ReadComponent("RecapPlayer"));

        // Altyazılar HƏR vəziyyətdə çəkilir (şərtin kənarındadır).
        Assert.Contains("pbx-recap__captions", player, StringComparison.Ordinal);
        Assert.Contains("shot.Caption", player, StringComparison.Ordinal);

        // Video yalnız HAZIR olanda görünür; qalan hallarda storyboard qalır.
        Assert.Contains("PetBrainRecapStatus.Ready", player, StringComparison.Ordinal);

        // «Əla!» düyməsi oynadıcıdan SONRA və şərtsizdir.
        var playerAt = summary.IndexOf("<RecapPlayer", StringComparison.Ordinal);
        var doneAt = summary.IndexOf("pbx-summary__done", StringComparison.Ordinal);

        Assert.True(playerAt > 0 && doneAt > playerAt,
            "«Əla!» düyməsi recap-dan sonra və şərtsiz olmalıdır.");
    }

    /// <summary>
    /// Recap dövrə vurmur, avtomatik başlamır və idarəedicilərin oxunan adı var
    /// (test 49).
    /// </summary>
    [Fact]
    public void Recap_DovreVurmur_VeAvtomatikBaslamir()
    {
        var player = StripComments(ReadComponent("RecapPlayer"));

        // Brauzerin ÖZ idarəediciləri — klaviatura və ekran oxuyucusu ilə işləyir.
        Assert.Contains("controls", player, StringComparison.Ordinal);

        // Sonsuz dövrə və avtomatik başlatma YOXDUR.
        Assert.DoesNotContain("loop", player, StringComparison.Ordinal);
        Assert.DoesNotContain("autoplay", player, StringComparison.Ordinal);

        // Deterministik storyboard-ın öz düymələri oxunan ad daşıyır.
        Assert.Contains("aria-label=\"@(_playing ?", player, StringComparison.Ordinal);
        Assert.Contains("Yenidən oynat", player, StringComparison.Ordinal);

        // Hərəkət həssaslığı: kadrlar özbaşına sürüşmür.
        Assert.Contains("ReducedMotion", player, StringComparison.Ordinal);
    }

    /// <summary>Macəranın sonunda YEKUN ekranı var — nəticəsiz bitən axın olmaz.</summary>
    [Fact]
    public void PetBrain_YekunEkraniniGosterir()
    {
        var page = ReadPage("PetBrain.razor");
        var summary = ReadComponent("RunSummary");

        Assert.Contains("<RunSummary", page, StringComparison.Ordinal);
        Assert.Contains("_run.Summary", page, StringComparison.Ordinal);

        // Yekunda mükafatın hər üç hissəsi görünür.
        Assert.Contains("XpEarned", summary, StringComparison.Ordinal);
        Assert.Contains("BondEarned", summary, StringComparison.Ordinal);
        Assert.Contains("UnlockedAccessoryCode", summary, StringComparison.Ordinal);
        Assert.Contains("NewMemories", summary, StringComparison.Ordinal);
    }

    /// <summary>Şəbəkə xətasında boş ekran qalmır — mesaj və yenidən cəhd var.</summary>
    [Fact]
    public void PetBrain_XetadaBosEkranQoymur()
    {
        var page = ReadPage("PetBrain.razor");

        Assert.Contains("<LoadingState", page, StringComparison.Ordinal);
        Assert.Contains("OnRetry=\"LoadAsync\"", page, StringComparison.Ordinal);
    }

    /// <summary>Yumurta macəraya çıxmır — ekran bunu nəzakətlə deyir.</summary>
    [Fact]
    public void PetBrain_YumurtaHaliniAyricaGosterir()
    {
        var page = ReadPage("PetBrain.razor");

        Assert.Contains("PetIsHatched: false", page, StringComparison.Ordinal);
    }

    // ==================== Lokalizasiya ====================

    /// <summary>
    /// Görünən hər mətn iki dillidir. Layihənin qaydası budur: tərcümə mətnin
    /// ÖZ YANINDADIR (bax docs/ARCHITECTURE.md), ona görə ayrıca açar faylı yoxdur.
    /// </summary>
    [Fact]
    public void ButunEkranlar_IkiDilliMetnIsledir()
    {
        Assert.Contains("Loc.T(", ReadPage("PetBrain.razor"), StringComparison.Ordinal);

        foreach (var component in ComponentsWithOwnText)
            Assert.Contains("Loc.T(", ReadComponent(component), StringComparison.Ordinal);

        // Hər çağırışda İKİ arqument olmalıdır — biri unudulsa ekran bir dildə
        // boş qalar. Boş və ya tək arqumentli çağırış qəbul edilmir.
        foreach (var (name, source) in AllPetBrainSources())
        {
            Assert.DoesNotContain("Loc.T()", source, StringComparison.Ordinal);

            var calls = Regex.Matches(StripComments(source), @"Loc\.T\(");
            var complete = Regex.Matches(StripComments(source), @"Loc\.T\(\s*\$?""");

            Assert.Equal(calls.Count, complete.Count);

            if (calls.Count > 0)
                Assert.True(
                    Regex.IsMatch(StripComments(source), @"Loc\.T\(\s*\$?""[^""]*""\s*,"),
                    $"{name} — Loc.T çağırışında ikinci dil yoxdur.");
        }
    }

    /// <summary>
    /// Dekorativ səhnə ekran oxuyucusundan gizlədilir: onun içində oxunası
    /// heç nə yoxdur, oxunanların hamısı üstündəki qatdadır.
    /// </summary>
    [Fact]
    public void DekorativSehne_EkranOxuyucusundanGizlidir()
    {
        Assert.Contains("aria-hidden=\"true\"", ReadComponent("ExperienceScene"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Ekranda sabit yazılmış Azərbaycan mətni qalmamalıdır — o, ingilis
    /// dilində də göstərilərdi.
    /// </summary>
    [Fact]
    public void Ekranlar_TercumesizAzerbaycanMetniSaxlamir()
    {
        // Yalnız uşağa GÖRÜNƏN mətn axtarılır: şərhlər və atribut adları yox.
        var azOnlyLetters = new Regex(@">\s*[^<>@{}]*[əƏğĞışŞçÇöÖüÜ][^<>@{}]*<");

        foreach (var (name, source) in AllPetBrainSources())
        {
            var withoutComments = StripComments(source);

            foreach (Match match in azOnlyLetters.Matches(withoutComments))
            {
                var text = match.Value.Trim('>', '<').Trim();

                if (text.Length == 0)
                    continue;

                Assert.Fail($"{name} — tərcüməsiz mətn: «{text}». Loc.T(...) işlədin.");
            }
        }
    }

    // ==================== Kətan qaydaları ====================

    /// <summary>
    /// Pet Brain CSS-i kətan müqaviləsini pozmur: nə <c>position: fixed</c>,
    /// nə də miqyaslanmamış ekran vahidi.
    ///
    /// <para>Ümumi qoruyucu <see cref="ResponsiveLayoutTests"/>-dədir; bu test
    /// isə məhz `pbx` bloklarını hədəfləyir ki, səhv baş verəndə səbəb dərhal
    /// görünsün.</para>
    /// </summary>
    [Fact]
    public void PetBrainStilleri_KetanQaydasiniPozmur()
    {
        var css = PetBrainCss();

        Assert.DoesNotContain("position: fixed", css, StringComparison.Ordinal);
        Assert.False(Regex.IsMatch(css, @"\d\s*(vw|vh|dvh|svh|lvh)\b"),
            "Pet Brain stillərində ekran vahidi var — zoom ölçünü ikinci dəfə kiçildəcək.");

        // Tam ekran qat `absolute` olmalıdır.
        Assert.Contains("position: absolute", css, StringComparison.Ordinal);
    }

    /// <summary>Toxunulan hər element ən azı 56px — 6 yaşlı barmaq üçün.</summary>
    [Fact]
    public void SecimDuymeleri_ToxunusHeddiniOdeyir()
    {
        var css = PetBrainCss();

        var choice = RuleBody(css, ".pbx-choice {");
        Assert.Contains("min-height: var(--pp-tap)", choice, StringComparison.Ordinal);
    }

    /// <summary>Beş bölmə BEŞ qalır — Pet Brain altıncı element əlavə etmir.</summary>
    [Fact]
    public void AltNaviqasiya_BesElementQalir()
    {
        var nav = ReadLayout("BottomNav.razor");

        Assert.Equal(5, Regex.Matches(nav, @"<NavLink\b").Count);
        Assert.DoesNotContain("pet-brain", nav, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ana ekrandakı macəra çipi mövcud üzən çiplə EYNİ yeri tutur və
    /// <c>HasFloatingChip</c>-ə daxildir — yoxsa səhnənin hündürlük büdcəsi
    /// pozulur və kafellər naviqasiyanın altında qalır.
    /// </summary>
    [Fact]
    public void AnaEkranCipi_UzenCipBudcesineTabedir()
    {
        var home = ReadPage("Home.razor");

        Assert.Contains("sprint--brain", home, StringComparison.Ordinal);
        Assert.Contains("_home.PetBrain", home, StringComparison.Ordinal);

        // Çip HasFloatingChip hesabına daxildir.
        var flag = home[home.IndexOf("private bool HasFloatingChip", StringComparison.Ordinal)..];
        Assert.Contains("PetBrain", flag[..160], StringComparison.Ordinal);

        // Üzən çiplər eyni budaqdadır (else if) — ikisi eyni anda görünmür.
        var chipBranch = home.IndexOf("else if (_home.PetBrain is { } brain)", StringComparison.Ordinal);
        Assert.True(chipBranch > 0, "Macəra çipi ayrıca budaqda deyil — sprintlə eyni anda çıxa bilər.");
    }

    /// <summary>Ana ekran ƏLAVƏ sorğu göndərmir — təklif aqreqatın içindədir.</summary>
    [Fact]
    public void AnaEkran_TeklifUcunElaveSorguGondermir()
    {
        var home = ReadPage("Home.razor");

        Assert.DoesNotContain("GetPetBrainAsync", home, StringComparison.Ordinal);
    }

    // ==================== Əlçatanlıq ====================

    /// <summary>Tam ekran qatların adı və canlı sahələri var.</summary>
    [Fact]
    public void TamEkranKomponentler_ElcatanAdDasiyir()
    {
        var shell = ReadComponent("ExperienceShell");

        Assert.Contains("role=\"dialog\"", shell, StringComparison.Ordinal);
        Assert.Contains("aria-modal=\"true\"", shell, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"@Title\"", shell, StringComparison.Ordinal);

        // Çıxış düyməsinin oxunan adı var (ikon tək başına oxunmur).
        Assert.Contains("aria-label=\"@Loc.T(\"Macərədən çıx\"", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void DeyisenMetnler_CanliSahedeElanOlunur()
    {
        var page = ReadPage("PetBrain.razor");
        var summary = ReadComponent("RunSummary");

        // Pet-in replikası və ipucu dəyişəndə ekran oxuyucusu xəbər tutmalıdır.
        Assert.Contains("aria-live=\"polite\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", summary, StringComparison.Ordinal);
    }

    /// <summary>Hər variantın oxunan adı var — ikon aria-hidden-dir.</summary>
    [Fact]
    public void Variantlar_OxunanAdDasiyir()
    {
        var choices = ReadComponent("StageChoices");

        Assert.Contains("aria-label=\"@AriaLabelFor(option)\"", choices, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", choices, StringComparison.Ordinal);
    }

    /// <summary>
    /// Rəng TƏK məlumat daşıyıcısı deyil: seçilmiş variantda və seçilmiş
    /// namizəd sətrində nişan da var (bax docs/DESIGN_SYSTEM.md, 3-cü qayda).
    /// </summary>
    [Fact]
    public void Veziyyet_YalnizRengleBildirilmir()
    {
        Assert.Contains("pbx-choice__mark", ReadComponent("StageChoices"), StringComparison.Ordinal);
        Assert.Contains("✓", ReadComponent("BrainDebugPanel"), StringComparison.Ordinal);
        Assert.Contains("@DifficultyLabel", ReadComponent("ExperienceShell"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Bütün animasiyalar qlobal <c>prefers-reduced-motion</c> qaydasına
    /// tabedir: onlar `animation`/`transition` işlədir, `!important` YOX.
    ///
    /// <para>Elan blokun SONUNCUSU ola bilər, yəni nöqtəli vergül olmadan.
    /// Regex əvvəllər yalnız <c>;</c> ilə bitəni tuturdu və bir elanı nöqtəli
    /// vergülsüz yazmaq qaydadan yayınmağın hazır yolu idi — indi bağlayan
    /// mötərizə də sayılır.</para>
    /// </summary>
    [Fact]
    public void Animasiyalar_ReducedMotionQaydasindanQacmir()
    {
        var css = PetBrainCss();

        foreach (Match match in Regex.Matches(css, @"(animation|transition)\s*:[^;}]*[;}]"))
            Assert.DoesNotContain("!important", match.Value, StringComparison.Ordinal);
    }

    // ==================== Nümayiş paneli ====================

    /// <summary>
    /// İzah paneli YALNIZ nümayiş rejimində çəkilir — uşaq adi axında bal və
    /// namizəd cədvəli görməməlidir.
    /// </summary>
    [Fact]
    public void IzahPaneli_YalnizNumayisRejimindeCizilir()
    {
        var page = ReadPage("PetBrain.razor");

        var guard = page.IndexOf("DemoMode: true", StringComparison.Ordinal);
        var panel = page.IndexOf("<BrainDebugPanel", StringComparison.Ordinal);

        Assert.True(guard > 0, "Nümayiş rejimi şərti yoxdur.");
        Assert.True(panel > guard, "İzah paneli şərtin içində deyil.");
        Assert.True(panel - guard < 200, "Panel şərtdən çox uzaqdır — şərt başqa bloka aiddir.");
    }

    /// <summary>Panel mətnin mənbəyini DÜRÜST göstərir — yalançı AI iddiası olmamalıdır.</summary>
    [Fact]
    public void IzahPaneli_MetninMenbeyiniDurustGosterir()
    {
        var panel = ReadComponent("BrainDebugPanel");

        Assert.Contains("NarrativeSource", panel, StringComparison.Ordinal);
        Assert.Contains("deterministik şablon", panel, StringComparison.Ordinal);
    }

    // ==================== Köməkçilər ====================

    private static IEnumerable<(string Name, string Source)> AllPetBrainSources()
    {
        yield return ("PetBrain.razor", ReadPage("PetBrain.razor"));

        foreach (var component in Components)
            yield return ($"{component}.razor", ReadComponent(component));
    }

    /// <summary>
    /// Yalnız Pet Brain bloku və yalnız QAYDALAR — şərhlər silinir.
    ///
    /// <para>Şərh silinməsə blokun öz başlığındakı «`position: fixed` YOXDUR»
    /// izahı qaydanın özü kimi oxunurdu.</para>
    /// </summary>
    private static string PetBrainCss()
    {
        var css = ReadCss("app.css");
        var start = css.IndexOf("PET BRAIN — ADAPTIVE PET DIRECTOR", StringComparison.Ordinal);

        Assert.True(start > 0, "Pet Brain CSS bloku tapılmadı — test köhnəlib.");

        // Nişan blokun ÖZ başlıq şərhinin içindədir, ona görə əvvəlcə həmin
        // şərhin sonuna keçirik — yoxsa yarımçıq şərh regex-ə uyğun gəlmir və
        // izahatdakı «position: fixed YOXDUR» cümləsi qayda kimi oxunur.
        var headerEnd = css.IndexOf("*/", start, StringComparison.Ordinal);
        Assert.True(headerEnd > start, "Pet Brain CSS başlığı bağlanmayıb.");

        return Regex.Replace(css[(headerEnd + 2)..], @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
    }

    private static string RuleBody(string css, string opener)
    {
        var start = css.IndexOf(opener, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{opener} qaydası tapılmadı — test köhnəlib.");

        var open = start + opener.Length;
        return css[open..css.IndexOf('}', open)];
    }

    /// <summary>
    /// Şərhlər uşağa GÖRÜNMÜR, ona görə mətn yoxlamalarından çıxarılır.
    ///
    /// <para>Üç növü də silinir: Razor (<c>@* … *@</c>), C# sənəd/sətir şərhi
    /// (<c>///</c>, <c>//</c>) və blok şərhi (<c>/* … */</c>). Bu olmasa
    /// testlər öz izahatlarımızı "tərcüməsiz mətn" sanırdı.</para>
    /// </summary>
    private static string StripComments(string source)
    {
        var text = Regex.Replace(source, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        text = Regex.Replace(text, @"^\s*///.*$", string.Empty, RegexOptions.Multiline);
        text = Regex.Replace(text, @"^\s*//.*$", string.Empty, RegexOptions.Multiline);

        return text;
    }

    private static string ReadPage(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Pages", name));

    private static string ReadComponent(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui",
            "Components", "PetBrain", name + ".razor"));

    private static string ReadLayout(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Components", "Layout", name));

    private static string ReadService(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "Services", name));

    /// <summary>
    /// Mexanika açarları KATALOQUN mənbəyindən oxunur, əl ilə sadalanmır.
    ///
    /// <para>Sətir sonları NORMALLAŞDIRILIR. Fayl Windows-da <c>CRLF</c> ilə
    /// yazılır, .NET-in çoxsətirli <c>$</c> lövbəri isə yalnız <c>\n</c>-dən
    /// əvvəl uyğunlaşır — yəni sətrin sonundakı <c>\r</c> nümunəni pozur və
    /// test kataloqu BOŞ oxuyub səhvən "köhnəlib" deyirdi.</para>
    /// </summary>
    private static string ReadEnums([CallerFilePath] string path = "") =>
        Normalize(File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.Shared", "Enums", "PetBrainPuzzleEnums.cs")));

    /// <summary>Sətir sonlarını <c>\n</c>-ə gətirir — nümunələr platformadan asılı olmasın.</summary>
    private static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private static string ReadCss(string name, [CallerFilePath] string path = "") =>
        File.ReadAllText(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "src", "PetPal.App.Ui", "wwwroot", "css", name));
}
