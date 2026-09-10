using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Hekayə rəsminin SAF qatı: səhnə təsviri, prompt və bayt yoxlayıcısı.
///
/// <para>Ən vacib iddia budur: <b>rəsm tapmacaya heç nə edə bilmir</b>. Model
/// nə mexanikanı, nə düyünləri, nə enerjini, nə həlli, nə də doğruluğu
/// dəyişir — o, yalnız fondur. İkinci iddia: <b>modelə uşağa aid heç nə
/// getmir</b> və bu, sahə səviyyəsində təmin olunur (göndəriləsi sahə
/// ümumiyyətlə mövcud deyil).</para>
/// </summary>
public class PetBrainIllustrationTests
{
    private static readonly Guid AylinId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RunId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static PuzzleSceneSpec MarsSpec(string language = "az") =>
        PuzzleSceneSpec.For(
            PuzzleBlueprintCatalog.Find(PuzzleBlueprintCatalog.MarsSignalRouteKey)!,
            language,
            ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!,
            species: "fox");

    // ==================== Səhnə təsviri ====================

    /// <summary>Eyni run → eyni səhnə, eyni hash, eyni prompt (test 15).</summary>
    [Fact]
    public void SehneVePrompt_EyniRunUcunSabitdir()
    {
        var first = MarsSpec();
        var second = MarsSpec();

        Assert.Equal(first.Hash(), second.Hash());
        Assert.Equal(
            SafePuzzleIllustrationPromptBuilder.Build(first),
            SafePuzzleIllustrationPromptBuilder.Build(second));
    }

    /// <summary>
    /// Prompt uşağa aid HEÇ NƏ daşımır və həlli sızdırmır (test 15).
    ///
    /// <para>Yoxlama iki qatlıdır: mətndə qadağan olunmuş parçalar axtarılır və
    /// AYRICA marşrutun düyün adlarının sıralı şəkildə görünmədiyi yoxlanılır.
    /// İkincisi vacibdir, çünki "antenna" sözü səhnədə olmalıdır — qadağan olan
    /// onun HƏLL SIRASI ilə birlikdə verilməsidir.</para>
    /// </summary>
    [Fact]
    public void Prompt_UsaqMelumatiVeHelliDasimir()
    {
        var prompt = SafePuzzleIllustrationPromptBuilder.Build(MarsSpec());

        foreach (var forbidden in new[]
                 {
                     "Aylin", "Mia", AylinId.ToString(), RunId.ToString(),
                     "childId", "pin", "email", "answer route", "correct route"
                 })
        {
            Assert.DoesNotContain(forbidden, prompt, StringComparison.OrdinalIgnoreCase);
        }

        // Modelə "cavabı çəkmə" bəndi HƏMİŞƏ gedir.
        Assert.Contains("Do not reveal a solution", prompt, StringComparison.Ordinal);
        Assert.Contains("no text, letters, numbers, arrows, route lines", prompt, StringComparison.Ordinal);

        // Marşrutun özü sıralı şəkildə görünmür.
        Assert.DoesNotContain("lander, ridge, solar, antenna, robo", prompt, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Dil promptu DƏYİŞMİR: modelə göndərilən mətn ingiliscədir və
    /// nəzarətlidir; uşağın dili yalnız ALT MƏTNƏ təsir edir.
    /// </summary>
    [Fact]
    public void Dil_PromptuDeyismir_AltMetniDeyisir()
    {
        var az = MarsSpec("az");
        var en = MarsSpec("en");

        Assert.Equal(
            SafePuzzleIllustrationPromptBuilder.Build(az),
            SafePuzzleIllustrationPromptBuilder.Build(en));

        Assert.Equal(az.Hash(), en.Hash());
        Assert.NotEqual(az.AltText(), en.AltText());
    }

    /// <summary>Fərqli hekayə anı → fərqli hash, yəni ayrı rəsm və ayrı keş.</summary>
    [Fact]
    public void FerqliSehne_FerqliHashVerir()
    {
        var mars = MarsSpec();

        var dragon = PuzzleSceneSpec.For(
            PuzzleBlueprintCatalog.Find(PuzzleBlueprintCatalog.LightFragmentsKey)!,
            "az",
            ExperienceCatalog.Find(ExperienceCatalog.DragonLostColors)!,
            species: "dragon");

        Assert.NotEqual(mars.Hash(), dragon.Hash());
    }

    /// <summary>
    /// Naməlum pet növü prompta DÜŞMÜR — təsdiqlənmiş dəyərə çevrilir.
    ///
    /// <para>Bu sahə onsuz da bizim kataloqumuzdandır; yoxlama müdafiənin
    /// İKİNCİ qatıdır.</para>
    /// </summary>
    [Fact]
    public void NamelumNov_PrompteDusmur()
    {
        var spec = PuzzleSceneSpec.For(
            PuzzleBlueprintCatalog.Find(PuzzleBlueprintCatalog.MarsSignalRouteKey)!,
            "az",
            ExperienceCatalog.Find(ExperienceCatalog.MarsRoverRescue)!,
            species: "<script>alert(1)</script>");

        var prompt = SafePuzzleIllustrationPromptBuilder.Build(spec);

        Assert.DoesNotContain("script", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("fox", spec.PetSpecies);
    }

    // ==================== Baytların yoxlanması ====================

    /// <summary>
    /// Provayderin dediyi format QƏBUL EDİLMİR — baytlar özü oxunur (test 31).
    /// </summary>
    [Theory]
    [InlineData("empty")]
    [InlineData("html")]
    [InlineData("tiny")]
    [InlineData("landscape")]
    [InlineData("huge")]
    public void PozuqRəsm_RedEdilir(string kind)
    {
        var bytes = kind switch
        {
            "empty" => [],
            "html" => "<html><body>salam</body></html>"u8.ToArray(),
            "tiny" => Png(64, 64),
            "landscape" => Png(1536, 1024),
            "huge" => Oversized(),
            _ => Png(1024, 1536)
        };

        var check = PuzzleIllustrationValidator.Validate(bytes);

        Assert.False(check.IsValid, $"«{kind}» qəbul edildi — halbuki rədd olunmalıdır.");
    }

    [Fact]
    public void DuzgunPortretPng_QebulEdilir()
    {
        var check = PuzzleIllustrationValidator.Validate(Png(1024, 1536));

        Assert.True(check.IsValid, check.Reason);
        Assert.Equal(PuzzleIllustrationValidator.Png, check.ContentType);
        Assert.Equal(1024, check.Width);
        Assert.Equal(1536, check.Height);
    }

    /// <summary>JPEG də tanınır — format sehrli baytlardan oxunur.</summary>
    [Fact]
    public void PortretJpeg_QebulEdilir()
    {
        var check = PuzzleIllustrationValidator.Validate(Jpeg(1024, 1536));

        Assert.True(check.IsValid, check.Reason);
        Assert.Equal(PuzzleIllustrationValidator.Jpeg, check.ContentType);
    }

    // ==================== Rəsm tapmacanı dəyişmir ====================

    /// <summary>
    /// Rəsmin vəziyyəti tapmacaya, cavaba və mükafata TOXUNMUR (test 16).
    ///
    /// <para>Bu, memarlığın nəticəsidir, təsadüf deyil: rəsm ayrıca cədvəldədir
    /// və generatorun girişi deyil. Test məhz bunu sabitləyir.</para>
    /// </summary>
    [Fact]
    public void RəsminVeziyyeti_TapmacaniDeyismir()
    {
        var generator = new DeterministicPuzzleGenerator();
        var context = SolverContext();

        var puzzle = generator.Generate(context);

        // Üç vəziyyətin hər biri üçün EYNİ tapmaca, EYNİ həll.
        foreach (var status in Enum.GetValues<PetBrainIllustrationStatus>())
        {
            var again = generator.Generate(context);
            again.Public.Scene.IllustrationStatus = status;
            again.Public.Scene.AssetUrl = status == PetBrainIllustrationStatus.Ready ? "/api/x" : string.Empty;

            Assert.Equal(puzzle.Signature, again.Signature);
            Assert.Equal(puzzle.Solution.Ids, again.Solution.Ids);
            Assert.Equal(
                puzzle.Public.Nodes.Select(n => $"{n.Id}:{n.Kind}"),
                again.Public.Nodes.Select(n => $"{n.Id}:{n.Kind}"));

            // Qiymətləndirici səhnəyə ÜMUMİYYƏTLƏ baxmır.
            var result = PuzzleAnswerEvaluator.Evaluate(
                again.Blueprint, again.Public, again.Solution, puzzle.Solution.Ids);

            Assert.True(result.IsCorrect);
        }
    }

    /// <summary>Bağlı provayder heç bir sorğu ATMIR və "uğursuz" qaytarır.</summary>
    [Fact]
    public async Task BagliProvayder_SorguAtmir()
    {
        var provider = new DisabledPuzzleIllustrationProvider();

        Assert.False(provider.IsEnabled);

        var result = await provider.RenderAsync(MarsSpec(), "prompt");

        Assert.False(result.Succeeded);
        Assert.Equal("disabled", result.Reason);
    }

    // ==================== Köməkçilər ====================

    private static PuzzleGenerationContext SolverContext() => new(
        ChildId: AylinId,
        RunId: RunId,
        StageIndex: 2,
        Age: 9,
        Language: "az",
        ExperienceType: PetBrainExperienceType.Adventure,
        Theme: TraitKeys.Space,
        Interests: new Dictionary<string, int>
        {
            [TraitKeys.Space] = 85,
            [TraitKeys.Science] = 75,
            [TraitKeys.Puzzles] = 90
        },
        PlayStyles: new Dictionary<string, int>
        {
            [TraitKeys.ProblemSolver] = 85,
            [TraitKeys.Explorer] = 75
        },
        Difficulty: PetBrainDifficulty.Medium,
        MasteryTargetDifficulty: 5,
        Assisted: false,
        RecentSignatures: new HashSet<string>());

    /// <summary>Yalnız BAŞLIQ — yoxlayıcı ölçünü IHDR-dən oxuyur, şəkli açmır.</summary>
    private static byte[] Png(int width, int height)
    {
        byte[] bytes = new byte[64];

        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);

        WriteBigEndian(bytes, 16, width);
        WriteBigEndian(bytes, 20, height);

        return bytes;
    }

    private static byte[] Jpeg(int width, int height)
    {
        byte[] bytes = new byte[64];

        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;

        // SOF0 seqmenti: FF C0, uzunluq, dəqiqlik, hündürlük, en.
        bytes[3] = 0xFF;
        bytes[4] = 0xC0;
        bytes[5] = 0x00;
        bytes[6] = 0x11;
        bytes[7] = 0x08;
        bytes[8] = (byte)(height >> 8);
        bytes[9] = (byte)(height & 0xFF);
        bytes[10] = (byte)(width >> 8);
        bytes[11] = (byte)(width & 0xFF);

        // JpegSize marker axtarışını 2-ci baytdan başlayır.
        return [.. bytes[..2], .. bytes[3..]];
    }

    private static byte[] Oversized()
    {
        var bytes = new byte[PuzzleIllustrationValidator.MaxBytes + 1];

        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);

        return bytes;
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
