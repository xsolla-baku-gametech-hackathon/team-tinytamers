using PetPal.Api.PetBrain.Puzzles;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Story;

/// <summary>
/// <b>Ay Kristalı — V2.</b> Yeni qraf arxitekturasının ilk tam nümunəsi.
///
/// <para>Köhnə xətti versiyada uşaq üç krater arasından seçim edirdi, sonra
/// hamı EYNİ tapmacaya və EYNİ sona gedirdi: seçim yalnız siyahıya yazılırdı.
/// Burada isə hər başlanğıc seçimi ayrı nəticə düyününə, ayrı səhnə variantına
/// və ayrı hekayə bayrağına aparır; bayraq isə sonrakı düyünlərdə oxunur.</para>
///
/// <para><b>Budaqlar:</b> şimal kraterindən getmək «işıq yolu»nu (light-path)
/// açır, dərin kraterdən getmək «dərinlik»i (deep-path), parlaq kraterdən
/// getmək isə «əks-səda»nı (echo-path). İki əsas budaq tapmacadan sonra
/// birləşir, amma kristalı DAŞIMAQ seçimi hansı budaqdan gəldiyinə görə
/// dəyişir — yəni ilk seçim sona qədər yaşayır.</para>
///
/// <para><b>Üç sonluq:</b> kəşfiyyatçı (explorer-summit), alim/həlledici
/// (scientist-signal) və qayğıkeş/yaradıcı (caring-lantern). Hər üçü
/// çatılandır və testlə qorunur.</para>
/// </summary>
public static class MoonCrystalHunt
{
    public const string NorthFlag = "moon-north";
    public const string DeepFlag = "moon-deep";
    public const string BrightFlag = "moon-bright";

    /// <summary>Tapmaca ilk cəhddə həll olundu — sonrakı düyün dəyişir.</summary>
    public const string CleanSolveFlag = "moon-clean-solve";

    public const string ExplorerEnding = "explorer-summit";
    public const string ScientistEnding = "scientist-signal";
    public const string CaringEnding = "caring-lantern";

    private static ExperienceOption Option(
        string key, string labelAz, string labelEn, string icon,
        string detailAz, string detailEn, params TraitDelta[] traits) =>
        new(key, labelAz, labelEn, icon, detailAz, detailEn, traits);

    private static ExperienceTransition On(string option, string target, int priority = 10) =>
        new(target, RequiredOptionKey: option, Priority: priority);

    private static ExperienceTransition IfFlag(string flag, string target, int priority = 20) =>
        new(target, RequiredFlag: flag, Priority: priority);

    private static ExperienceTransition Fallback(string target) =>
        new(target, Priority: 1000, IsFallback: true);

    public static ExperienceDefinition Definition { get; } = new(
        Key: ExperienceCatalog.MoonCrystalRescue,
        Version: 2,
        StartNodeId: "intro",
        AllowedPuzzleFamilies: [PuzzleBlueprintCatalog.MoonCrystalRouteKey],
        Nodes:
        [
            // ---------- Giriş ----------
            new ExperienceNode(
                Id: "intro",
                Kind: PetBrainStageKind.Intro,
                PromptAz: "Ayda bir işıq sönür",
                PromptEn: "A light is fading on the Moon",
                PetLineAz: "Ayın kristalı sönür. Onu tapıb işığa qaytara bilərik — hazırsan?",
                PetLineEn: "The Moon's crystal is going dark. We can find it and bring it back to light — ready?",
                Options: [],
                Transitions: [Fallback("choose-crater")],
                Effects: [],
                SceneVariant: "moon-arrival"),

            // ---------- Üç başlanğıc seçimi ----------
            new ExperienceNode(
                Id: "choose-crater",
                Kind: PetBrainStageKind.Choice,
                PromptAz: "Hansı kraterdən başlayaq?",
                PromptEn: "Which crater do we start from?",
                PetLineAz: "Üç krater görürəm. Sən seç — mən arxanca gəlirəm!",
                PetLineEn: "I can see three craters. You choose — I am right behind you!",
                Options:
                [
                    Option("north-crater", "Şimal krateri", "The north crater", "🧭",
                        "Soyuq, amma işıq zolağı var", "Cold, but a ribbon of light runs through it",
                        new TraitDelta(TraitKeys.Explorer, 2), new TraitDelta(TraitKeys.Space, 1)),

                    Option("deep-crater", "Dərin krater", "The deep crater", "🕳️",
                        "Qaranlıq və dərin", "Dark and deep",
                        new TraitDelta(TraitKeys.ProblemSolver, 2), new TraitDelta(TraitKeys.Science, 1)),

                    Option("bright-crater", "Parlaq krater", "The bright crater", "✨",
                        "Divarları səs qaytarır", "Its walls give the echo back",
                        new TraitDelta(TraitKeys.Creative, 2), new TraitDelta(TraitKeys.Stories, 1))
                ],
                Transitions:
                [
                    On("north-crater", "north-result"),
                    On("deep-crater", "deep-result"),
                    On("bright-crater", "bright-result"),
                    Fallback("north-result")
                ],
                Effects: [],
                SceneVariant: "moon-three-craters"),

            // ---------- Üç FƏRQLİ nəticə düyünü ----------
            new ExperienceNode(
                Id: "north-result",
                Kind: PetBrainStageKind.Consequence,
                PromptAz: "İşıq zolağı bizi apardı",
                PromptEn: "The ribbon of light led us on",
                PetLineAz: "Şimalda incə bir işıq zolağı var idi — o, bizi düz güzgü sahəsinə apardı!",
                PetLineEn: "There was a thin ribbon of light in the north — it led us straight to the mirror field!",
                Options: [],
                Transitions: [Fallback("crystal-puzzle")],
                Effects: [new ExperienceEffect(ExperienceEffectKind.SetFlag, NorthFlag)],
                SceneVariant: "moon-light-ribbon"),

            new ExperienceNode(
                Id: "deep-result",
                Kind: PetBrainStageKind.Consequence,
                PromptAz: "Dərinlikdə soyuq bir səs var",
                PromptEn: "Something cold hums in the deep",
                PetLineAz: "Aşağıda kristalın uğultusunu eşitdim. Yol uzundur, amma biz onu tapdıq.",
                PetLineEn: "I heard the crystal humming down below. The way is long, but we found it.",
                Options: [],
                Transitions: [Fallback("crystal-puzzle")],
                Effects: [new ExperienceEffect(ExperienceEffectKind.SetFlag, DeepFlag)],
                SceneVariant: "moon-deep-hum"),

            new ExperienceNode(
                Id: "bright-result",
                Kind: PetBrainStageKind.Consequence,
                PromptAz: "Divarlar səsimizi qaytardı",
                PromptEn: "The walls gave our voices back",
                PetLineAz: "Sən danışdın, krater cavab verdi. Əks-səda bizə yolu göstərdi!",
                PetLineEn: "You spoke and the crater answered. The echo showed us the way!",
                Options: [],
                Transitions: [Fallback("crystal-puzzle")],
                Effects: [new ExperienceEffect(ExperienceEffectKind.SetFlag, BrightFlag)],
                SceneVariant: "moon-echo-walls"),

            // ---------- Budağa uyğun tapmaca ----------
            new ExperienceNode(
                Id: "crystal-puzzle",
                Kind: PetBrainStageKind.Puzzle,
                PromptAz: "Kristalı işığa qovuşdur",
                PromptEn: "Bring the crystal into the light",
                PetLineAz: "İndi roveri düzgün yolla apar — kristal işıqsız oyanmır.",
                PetLineEn: "Now take the rover the right way — the crystal will not wake without light.",
                Options: [],
                Transitions:
                [
                    // İlk cəhdən həll edən uşaq ayrı düyünə gedir: hekayə onun
                    // necə çatdığını da danışmalıdır, yalnız çatdığını yox.
                    new ExperienceTransition("crystal-awake-clean",
                        RequiredResult: PetBrainStageResult.Solved,
                        RequiredFlag: CleanSolveFlag,
                        Priority: 5),

                    new ExperienceTransition("crystal-awake",
                        RequiredResult: PetBrainStageResult.Solved,
                        Priority: 10),

                    Fallback("crystal-awake")
                ],
                Effects: [],
                SceneVariant: "moon-mirror-field",
                PuzzleFamily: PuzzleBlueprintCatalog.MoonCrystalRouteKey),

            new ExperienceNode(
                Id: "crystal-awake-clean",
                Kind: PetBrainStageKind.Consequence,
                PromptAz: "Kristal bir anda oyandı",
                PromptEn: "The crystal woke at once",
                PetLineAz: "Bir dəfəyə tapdın! Kristal elə parladı ki, krater gündüz kimi oldu.",
                PetLineEn: "You found it in one go! The crystal flared so bright the crater turned to daylight.",
                Options: [],
                Transitions: [Fallback("carry-choice")],
                Effects: [],
                SceneVariant: "moon-crystal-bright"),

            new ExperienceNode(
                Id: "crystal-awake",
                Kind: PetBrainStageKind.Consequence,
                PromptAz: "Kristal yavaş-yavaş oyanır",
                PromptEn: "The crystal wakes slowly",
                PetLineAz: "İşıq güzgüdən keçdi və kristal yavaşca isindi. Alındı!",
                PetLineEn: "The light passed through the mirror and the crystal warmed up. We did it!",
                Options: [],
                Transitions: [Fallback("carry-choice")],
                Effects: [],
                SceneVariant: "moon-crystal-glow"),

            // ---------- Daşıma seçimi — ƏVVƏLKİ budaqdan asılıdır ----------
            new ExperienceNode(
                Id: "carry-choice",
                Kind: PetBrainStageKind.Choice,
                PromptAz: "Kristalı necə aparaq?",
                PromptEn: "How do we carry the crystal?",
                PetLineAz: "İndi onu gəmiyə çatdırmalıyıq. Sənin planın nədir?",
                PetLineEn: "Now we need to get it to the ship. What is your plan?",
                Options:
                [
                    Option("summit-route", "Zirvədən aparaq", "Over the summit", "⛰️",
                        "Ən qısa, amma ən sərt yol", "The shortest way, and the steepest",
                        new TraitDelta(TraitKeys.Explorer, 2), new TraitDelta(TraitKeys.Space, 1)),

                    Option("signal-relay", "Siqnalla çağıraq", "Call it in by signal", "📶",
                        "Gəmi özü bizə gəlsin", "Let the ship come to us",
                        new TraitDelta(TraitKeys.ProblemSolver, 2), new TraitDelta(TraitKeys.Science, 1)),

                    Option("lantern-cradle", "Fənər beşiyi düzəldək", "Build a lantern cradle", "🏮",
                        "Kristal yolda da işıq versin", "So the crystal lights the way too",
                        new TraitDelta(TraitKeys.Creative, 2), new TraitDelta(TraitKeys.Caring, 1))
                ],
                Transitions:
                [
                    // İLK seçim burada, sonda özünü göstərir: dərin kraterdən
                    // gələn uşaq kristalı zirvəyə qaldıra bilmir, çünki yol
                    // yuxarı deyil, AŞAĞI gedirdi. Eyni düymə, başqa nəticə —
                    // budaq bəzək deyil.
                    new ExperienceTransition("carry-too-steep",
                        RequiredOptionKey: "summit-route",
                        RequiredFlag: DeepFlag,
                        Priority: 5),

                    On("summit-route", "ending-explorer"),
                    On("signal-relay", "ending-scientist"),
                    On("lantern-cradle", "ending-caring"),
                    Fallback("ending-caring")
                ],
                Effects: [],
                SceneVariant: "moon-carry",
                MemoryCallbackKey: "moon-first-choice"),

            new ExperienceNode(
                Id: "carry-too-steep",
                Kind: PetBrainStageKind.Consequence,
                PromptAz: "Yoxuş kristal üçün çox dikdir",
                PromptEn: "The climb is too steep for the crystal",
                PetLineAz: "Biz dərinlikdən gəlmişdik — zirvə çox uzaqdadır. Gəl ona beşik düzəldək!",
                PetLineEn: "We came up from the deep — the summit is too far. Let us build it a cradle instead!",
                Options: [],
                Transitions: [Fallback("ending-caring")],
                Effects: [],
                SceneVariant: "moon-carry"),

            // ---------- Üç sonluq ----------
            new ExperienceNode(
                Id: "ending-explorer",
                Kind: PetBrainStageKind.Ending,
                PromptAz: "Zirvədən enən işıq",
                PromptEn: "A light coming down from the summit",
                PetLineAz: "Zirvəyə qalxdıq və kristal bütün Ayı işıqlandırdı. Yolu sən tapdın!",
                PetLineEn: "We climbed to the summit and the crystal lit up the whole Moon. You found the way!",
                Options: [],
                Transitions: [],
                Effects: [new ExperienceEffect(ExperienceEffectKind.RememberChoice, "carry")],
                SceneVariant: "moon-summit-finale",
                EndingKey: ExplorerEnding),

            new ExperienceNode(
                Id: "ending-scientist",
                Kind: PetBrainStageKind.Ending,
                PromptAz: "Gəmi siqnalı tutdu",
                PromptEn: "The ship caught the signal",
                PetLineAz: "Siqnal güzgüdən sıçradı və gəmi düz yanımıza endi. Ağıllı plan idi!",
                PetLineEn: "The signal bounced off the mirror and the ship landed right beside us. Clever plan!",
                Options: [],
                Transitions: [],
                Effects: [new ExperienceEffect(ExperienceEffectKind.RememberChoice, "carry")],
                SceneVariant: "moon-signal-finale",
                EndingKey: ScientistEnding),

            new ExperienceNode(
                Id: "ending-caring",
                Kind: PetBrainStageKind.Ending,
                PromptAz: "Fənər kimi evə",
                PromptEn: "Home like a lantern",
                PetLineAz: "Kristalı beşikdə apardıq və o, bütün yol boyu bizə işıq verdi. Çox yumşaq fikir idi.",
                PetLineEn: "We carried the crystal in its cradle and it lit our whole path home. Such a gentle idea.",
                Options: [],
                Transitions: [],
                Effects: [new ExperienceEffect(ExperienceEffectKind.RememberChoice, "carry")],
                SceneVariant: "moon-lantern-finale",
                EndingKey: CaringEnding)
        ]);
}
