using System.Security.Cryptography;
using System.Text;
using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain.Recap;

/// <summary>
/// Bir mərhələdə uşağın ETDİYİ seçim.
/// </summary>
/// <param name="BeatKey">Mərhələnin açarı — <c>route</c>, <c>rescue</c>, <c>habitat</c>.</param>
/// <param name="ChoiceKey">Kataloqdan TƏSDİQLƏNMİŞ seçim açarı — <c>canyon</c>, <c>solar-panel</c>.</param>
public sealed record RecapBeat(string BeatKey, string ChoiceKey);

/// <summary>
/// Recap videosunun DƏYİŞMƏZ təsviri.
///
/// <para>Tamamlama tranzaksiyasında serverin öz vəziyyətindən qurulur və
/// yalnız sərhədli, enum-abənzər açarlardan ibarətdir. Uşağın adı, id-si,
/// şəkli, səsi, söhbəti, sərbəst cavabı, xatirə mətni, tapmacanın həlli,
/// autentifikasiya məlumatı, yeri və məktəbi <b>sahə səviyyəsində yoxdur</b> —
/// yəni səhvən göndərmək mümkün deyil.</para>
///
/// <para>Uşağın id-si burada VAR, amma o, <b>sahiblik üçündür</b> və prompta
/// düşmür: <see cref="Hash"/> onu qəsdən kənarda saxlayır, çünki eyni seçimlər
/// eyni videonu verməlidir.</para>
/// </summary>
public sealed record AdventureRecapSpec(
    Guid RunId,

    /// <summary>Sahiblik — yalnız endpoint və kvota üçün, prompta DÜŞMÜR.</summary>
    Guid ChildProfileId,

    string ExperienceKey,
    int SpecVersion,
    string Language,
    int DurationSeconds,

    /// <summary>Pet-in növü, rəngi və taxdığı kosmetik — qapalı siyahıdan.</summary>
    string PetSpecies,
    string PetColor,
    string PetCosmetic,

    /// <summary>Sıralı hekayə anları və uşağın HƏQİQİ seçimləri.</summary>
    IReadOnlyList<RecapBeat> Beats,

    /// <summary>Tapmacanın mexanika açarı.</summary>
    string PuzzleMechanic,

    /// <summary>Təhlükəsiz nəticə: <c>no-hints</c>, <c>one-hint</c>, <c>several-hints</c>.</summary>
    string PuzzleOutcome,

    string Environment,
    string Palette,
    string Mood,
    string CameraStyle,

    /// <summary>Hazır tapmaca rəsminin hash-ı — ilk kadr kimi işlənir.</summary>
    string SceneSpecHash)
{
    /// <summary>Müqavilə versiyası. Qaydalar dəyişəndə artır və hash-a düşür.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Sahə ayırıcısı — PuzzleSeed ilə eyni prinsip (0x1F).</summary>
    private const char Separator = (char)0x1F;

    /// <summary>
    /// İdempotentlik və keş açarı.
    ///
    /// <para><b>Uşaq id-si və run id-si QƏSDƏN kənardadır.</b> Səbəb: iki uşaq
    /// eyni seçimləri etsə, eyni video yaraşır və <b>ikinci dəfə pul
    /// xərclənməməlidir</b>. Eyni uşaq eyni run-ı təkrar açanda da keşdən
    /// gəlir.</para>
    ///
    /// <para>Dil də kənardadır: altyazılar deterministik UI qatındadır, videonun
    /// içində mətn yoxdur — deməli azərbaycanca və ingiliscə eyni videodur.</para>
    /// </summary>
    public string Hash()
    {
        var canonical = string.Join(Separator,
        [
            "v" + SpecVersion,
            ExperienceKey,
            DurationSeconds.ToString(),
            PetSpecies,
            PetColor,
            PetCosmetic,
            string.Join(',', Beats.Select(b => $"{b.BeatKey}={b.ChoiceKey}")),
            PuzzleMechanic,
            PuzzleOutcome,
            Environment,
            Palette,
            Mood,
            CameraStyle,
            SceneSpecHash
        ]);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// Tamamlanmış run-dan təsvir qurur.
    ///
    /// <para>Seçimlər <b>şablonun öz mərhələlərindən</b> oxunur, yəni kataloqda
    /// olmayan açar recap-a düşə bilmir. Uydurulmuş seçim də əlavə edilmir:
    /// şablonda olmayan an sadəcə BURAXILIR.</para>
    /// </summary>
    /// <summary>
    /// Budaqlanan macəranın xülasəsi — beat-lər REAL nəticə sətirlərindən.
    ///
    /// <para>Xətti versiyada beat-lər <c>Choices</c> siyahısını şablonun
    /// mərhələləri ilə indeks-indeks tutuşdururdu. Budaqlanan hekayədə bu
    /// mümkün deyil: iki uşaq eyni sayda addım atmır və eyni indeksdə eyni
    /// mərhələ olmur. Burada isə hər sətir hansı düyünə aid olduğunu özü
    /// deyir.</para>
    /// </summary>
    public static AdventureRecapSpec ForGraph(
        ExperienceRun run,
        ExperienceTemplate template,
        Pet pet,
        string language,
        string sceneSpecHash,
        string puzzleMechanic,
        IReadOnlyList<RunStageOutcome> outcomes)
    {
        List<RecapBeat> beats = [];

        foreach (var outcome in outcomes
                     .Where(o => o.Kind == PetBrainStageKind.Choice && o.SelectedOptionKeys.Count > 0)
                     .OrderBy(o => o.StageOrdinal))
            beats.Add(new RecapBeat(
                BeatKeyFor(template.Key, beats.Count), outcome.SelectedOptionKeys[0]));

        // Sonluq da bir beat-dir: uşağın hekayəsi məhz orada tamamlanır.
        if (!string.IsNullOrEmpty(run.EndingKey))
            beats.Add(new RecapBeat("ending", run.EndingKey));

        return Build(run, template, pet, language, sceneSpecHash, puzzleMechanic, beats);
    }

    public static AdventureRecapSpec For(
        ExperienceRun run,
        ExperienceTemplate template,
        Pet pet,
        string language,
        string sceneSpecHash,
        string puzzleMechanic)
    {
        List<RecapBeat> beats = [];

        // Run.Choices mərhələ sırası ilə yazılır; tapmaca mərhələsində ora
        // "solved" (və ya yaradıcı yolda parça açarları) düşür.
        var index = 0;

        foreach (var stage in template.Stages)
        {
            if (index >= run.Choices.Count)
                break;

            var chosen = run.Choices[index];
            index++;

            if (stage.Kind != PetBrainStageKind.Choice)
                continue;

            // Yalnız kataloqda MÖVCUD seçim qəbul edilir.
            if (stage.Options.All(o => !string.Equals(o.Key, chosen, StringComparison.Ordinal)))
                continue;

            beats.Add(new RecapBeat(BeatKeyFor(template.Key, beats.Count), chosen));
        }

        return Build(run, template, pet, language, sceneSpecHash, puzzleMechanic, beats);
    }

    private static AdventureRecapSpec Build(
        ExperienceRun run,
        ExperienceTemplate template,
        Pet pet,
        string language,
        string sceneSpecHash,
        string puzzleMechanic,
        IReadOnlyList<RecapBeat> beats)
    {
        return new AdventureRecapSpec(
            RunId: run.Id,
            ChildProfileId: run.ChildProfileId,
            ExperienceKey: template.Key,
            SpecVersion: CurrentVersion,
            Language: language,
            DurationSeconds: 10,
            PetSpecies: Approved(pet.Species),
            PetColor: ColorFor(Approved(pet.Species)),
            PetCosmetic: Cosmetic(template.RewardCode),
            Beats: beats,
            PuzzleMechanic: puzzleMechanic,
            PuzzleOutcome: Outcome(run.HintsUsed),
            Environment: EnvironmentFor(template.Key),
            Palette: PaletteFor(template.Key),
            Mood: MoodFor(template.Key),
            CameraStyle: "gentle-storybook",
            SceneSpecHash: sceneSpecHash);
    }

    /// <summary>Mərhələnin rolu — şablona görə sabit sıradadır.</summary>
    private static string BeatKeyFor(string templateKey, int order) => templateKey switch
    {
        ExperienceCatalog.MarsRoverRescue => order == 0 ? "route" : "rescue",
        ExperienceCatalog.DragonLostColors => order == 0 ? "palette" : "habitat",
        _ => order == 0 ? "first-choice" : "second-choice"
    };

    /// <summary>İpucu sayı ANİMASİYANI incələdir, amma uşağı utandırmır.</summary>
    private static string Outcome(int hints) => hints switch
    {
        0 => "no-hints",
        1 => "one-hint",
        _ => "several-hints"
    };

    private static string EnvironmentFor(string key) => key switch
    {
        ExperienceCatalog.MarsRoverRescue => "martian-canyon",
        ExperienceCatalog.DragonLostColors => "crystal-garden-at-dusk",
        _ => "storybook-landscape"
    };

    private static string PaletteFor(string key) => key switch
    {
        ExperienceCatalog.MarsRoverRescue => "warm-orange",
        ExperienceCatalog.DragonLostColors => "violet-and-moonlight",
        _ => "soft-daylight"
    };

    private static string MoodFor(string key) => key switch
    {
        ExperienceCatalog.MarsRoverRescue => "hopeful-adventurous",
        ExperienceCatalog.DragonLostColors => "gentle-wonder",
        _ => "calm-curious"
    };

    /// <summary>Kosmetik açarı QAPALI siyahıdandır.</summary>
    private static string Cosmetic(string? code) => code switch
    {
        ExperienceCatalog.HelmetMars => "helmet-mars",
        ExperienceCatalog.WingsRainbow => "wings-rainbow",
        _ => "none"
    };

    private static string Approved(string species) => species switch
    {
        "fox" or "cat" or "dragon" or "owl" or "bunny" => species,
        _ => "fox"
    };

    private static string ColorFor(string species) => species switch
    {
        "fox" => "warm-orange",
        "cat" => "soft-grey",
        "dragon" => "mint-green",
        "owl" => "dusk-blue",
        "bunny" => "cream-white",
        _ => "warm-orange"
    };
}
