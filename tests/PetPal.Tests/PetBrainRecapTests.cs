using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Media;
using PetPal.Api.PetBrain.Recap;
using PetPal.Shared.Enums;

namespace PetPal.Tests;

/// <summary>
/// Recap-ın SAF qatı: təsvir, storyboard, prompt və media yoxlayıcısı.
///
/// <para>İki iddia hər şeydən vacibdir: <b>storyboard uşağın HƏQİQİ seçimlərini
/// göstərir</b> (seçilməyən variant heç vaxt görünmür) və <b>modelə uşağa aid
/// heç nə getmir</b>.</para>
/// </summary>
public class PetBrainRecapTests
{
    private static readonly Guid RunId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ChildId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ==================== Təsvir və hash ====================

    /// <summary>Eyni kanonik təsvir → eyni hash (test 18).</summary>
    [Fact]
    public void EyniSecimler_EyniHashVerir()
    {
        var first = MarsSpec("canyon", "solar-panel");
        var second = MarsSpec("canyon", "solar-panel");

        Assert.Equal(first.Hash(), second.Hash());
    }

    /// <summary>
    /// MƏNALI seçim dəyişəndə hash da dəyişir — yəni yeni video yaraşır (test 18).
    /// </summary>
    [Theory]
    [InlineData("crater", "solar-panel")]
    [InlineData("canyon", "battery")]
    [InlineData("mountain", "carry-to-ship")]
    public void FerqliSecimler_FerqliHashVerir(string route, string rescue)
    {
        Assert.NotEqual(
            MarsSpec("canyon", "solar-panel").Hash(),
            MarsSpec(route, rescue).Hash());
    }

    /// <summary>
    /// Uşaq id-si, run id-si və dil hash-a DÜŞMÜR.
    ///
    /// <para>Bu, qəsdəndir: eyni seçimlərlə oynayan ikinci uşaq üçün pul
    /// XƏRCLƏNMƏMƏLİDİR, altyazılar isə onsuz da deterministik UI qatındadır —
    /// video hər iki dildə eynidir.</para>
    /// </summary>
    [Fact]
    public void Hash_UsaqdanVeDildenAsiliDeyil()
    {
        var mine = MarsSpec("canyon", "solar-panel");

        var theirs = mine with
        {
            ChildProfileId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            Language = "en"
        };

        Assert.Equal(mine.Hash(), theirs.Hash());
    }

    // ==================== Storyboard ====================

    /// <summary>
    /// Üç kadr, tam 10 saniyə, boşluqsuz (test 17).
    /// </summary>
    [Fact]
    public void Storyboard_UcKadrVeOnSaniyedir()
    {
        foreach (var spec in new[] { MarsSpec("canyon", "solar-panel"), DragonSpec("ocean", "crystal-cave") })
        {
            var shots = RecapStoryboard.Build(spec);

            Assert.Equal(3, shots.Count);
            Assert.Equal(0.0, shots[0].StartSeconds);
            Assert.Equal(RecapStoryboard.TotalSeconds, shots[^1].EndSeconds);

            // Kadrlar bitişikdir — ara boşluq və üst-üstə düşmə yoxdur.
            for (var i = 1; i < shots.Count; i++)
                Assert.Equal(shots[i - 1].EndSeconds, shots[i].StartSeconds);

            Assert.All(shots, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
            Assert.All(shots, s => Assert.False(string.IsNullOrWhiteSpace(s.Motion)));
        }
    }

    /// <summary>
    /// Storyboard uşağın HƏQİQİ seçimini göstərir və ƏKS seçimi heç vaxt
    /// göstərmir (test 17).
    /// </summary>
    [Fact]
    public void Storyboard_SecilmeyenVariantiGostermir()
    {
        var shots = RecapStoryboard.Build(MarsSpec("crater", "battery"));
        var text = string.Join(' ', shots.Select(s => s.Motion + " " + s.Caption));

        Assert.Contains("crater", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("battery", text, StringComparison.OrdinalIgnoreCase);

        // Seçilməyən variantlar YOXDUR.
        Assert.DoesNotContain("canyon", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mountain", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("solar panel", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Altyazılar uşağın dilində qurulur (test 50).</summary>
    [Fact]
    public void Altyazilar_UsaginDilindedir()
    {
        var az = RecapStoryboard.Build(MarsSpec("canyon", "solar-panel"));
        var en = RecapStoryboard.Build(MarsSpec("canyon", "solar-panel") with { Language = "en" });

        Assert.NotEqual(az[0].Caption, en[0].Caption);

        // Hərəkət ifadəsi isə HƏMİŞƏ ingiliscədir — o, modelə gedir.
        Assert.Equal(az[0].Motion, en[0].Motion);
    }

    /// <summary>Şablonda olmayan an UYDURULMUR — kadr neytral qalır.</summary>
    [Fact]
    public void EskikSecim_Uydurulmur()
    {
        var spec = MarsSpec("canyon", "solar-panel") with { Beats = [] };
        var shots = RecapStoryboard.Build(spec);

        Assert.Equal(3, shots.Count);
        Assert.All(shots, s => Assert.False(string.IsNullOrWhiteSpace(s.Caption)));
    }

    // ==================== Prompt ====================

    /// <summary>
    /// Prompt uşağa aid HEÇ NƏ və həll daşımır; seçimi KİLİDLƏYİR (test 17, 35).
    /// </summary>
    [Fact]
    public void Prompt_UsaqMelumatiDasimir_SecimiKilidleyir()
    {
        var spec = MarsSpec("canyon", "solar-panel");
        var prompt = SafeRecapPromptBuilder.Build(spec, RecapStoryboard.Build(spec));

        foreach (var forbidden in new[]
                 {
                     "Aylin", "Mia", ChildId.ToString(), RunId.ToString(),
                     "childId", "pin", "email", "lander", "antenna ->", "correct route"
                 })
        {
            Assert.DoesNotContain(forbidden, prompt, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("exactly 10-second", prompt, StringComparison.Ordinal);
        Assert.Contains("9:16", prompt, StringComparison.Ordinal);
        Assert.Contains("No children, speech, lip-sync", prompt, StringComparison.Ordinal);

        // Seçim KİLİDLƏNİR — model əks variantı çəkə bilməz.
        Assert.Contains("Do not change the selected", prompt, StringComparison.Ordinal);
        Assert.Contains("canyon", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("solar panel", prompt, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// İpucu istəmək UTANDIRICI dilə çevrilmir — model «çətinlik çəkdi»
    /// eşitmir, sadəcə pet-in köməyini görür.
    /// </summary>
    [Fact]
    public void Prompt_IpucunuUtandiriciDileCevirmir()
    {
        var spec = MarsSpec("canyon", "solar-panel") with { PuzzleOutcome = "several-hints" };
        var prompt = SafeRecapPromptBuilder.Build(spec, RecapStoryboard.Build(spec));

        Assert.Contains("points encouragingly", prompt, StringComparison.Ordinal);

        foreach (var shaming in new[] { "failed", "struggled", "wrong", "mistake", "could not" })
            Assert.DoesNotContain(shaming, prompt, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Eyni təsvir → eyni prompt (təkrarlanan iş yaranmasın).</summary>
    [Fact]
    public void Prompt_SabitQalir()
    {
        var spec = MarsSpec("canyon", "solar-panel");

        var first = SafeRecapPromptBuilder.Build(spec, RecapStoryboard.Build(spec));
        var second = SafeRecapPromptBuilder.Build(spec, RecapStoryboard.Build(spec));

        Assert.Equal(first, second);
        Assert.Equal(SafeRecapPromptBuilder.HashOf(first), SafeRecapPromptBuilder.HashOf(second));
    }

    // ==================== Media yoxlayıcısı ====================

    /// <summary>
    /// Müqaviləyə uymayan video RƏDD edilir — başlığa deyil, BAYTLARA baxılır
    /// (test 38).
    /// </summary>
    [Theory]
    [InlineData("empty", "empty")]
    [InlineData("html", "unsupported-container")]
    [InlineData("short", "duration-out-of-contract")]
    [InlineData("long", "duration-out-of-contract")]
    [InlineData("landscape", "not-portrait")]
    [InlineData("tiny", "too-small")]
    public void PozuqVideo_RedEdilir(string kind, string expected)
    {
        var bytes = kind switch
        {
            "empty" => [],
            "html" => "<html>salam</html>"u8.ToArray(),
            "short" => Mp4(720, 1280, 4.0),
            "long" => Mp4(720, 1280, 15.0),
            "landscape" => Mp4(1280, 720, 10.0),
            "tiny" => Mp4(160, 240, 10.0),
            _ => Mp4(720, 1280, 10.0)
        };

        var check = RecapVideoValidator.Validate(bytes);

        Assert.False(check.IsValid, $"«{kind}» qəbul edildi.");
        Assert.Equal(expected, check.Reason);
    }

    [Fact]
    public void DuzgunPortretVideo_QebulEdilir()
    {
        var check = RecapVideoValidator.Validate(Mp4(720, 1280, 10.0));

        Assert.True(check.IsValid, check.Reason);
        Assert.Equal(RecapVideoValidator.Mp4, check.ContentType);
        Assert.Equal(720, check.Width);
        Assert.Equal(1280, check.Height);
        Assert.Equal(10.0, check.Seconds, 2);
    }

    /// <summary>Dar dözümlülük: 10.0 ± 0.5 saniyə qəbul edilir.</summary>
    [Theory]
    [InlineData(9.6, true)]
    [InlineData(10.4, true)]
    [InlineData(9.2, false)]
    [InlineData(10.8, false)]
    public void Muddet_DarAraliqdaQebulEdilir(double seconds, bool expected)
    {
        var check = RecapVideoValidator.Validate(Mp4(720, 1280, seconds));

        Assert.Equal(expected, check.IsValid);
    }

    // ==================== Xərc siyasəti ====================

    /// <summary>Standart konfiqurasiya PULLU iş başlatmır.</summary>
    [Fact]
    public void StandartKonfiqurasiya_PulluIsBaslatmir()
    {
        var policy = Policy(new PetBrainMediaOptions());

        Assert.False(policy.Options.PaidMediaEnabled);
        Assert.False(policy.ForImage().Allowed);
        Assert.False(policy.ForVideo().Allowed);
        Assert.Equal("paid-media-disabled", policy.ForVideo().Reason);
    }

    /// <summary>
    /// <c>Budget</c> profili YALNIZ iki modelə icazə verir və qiyməti sabitdir.
    /// </summary>
    [Fact]
    public void BudgetProfili_YalnizIkiModeleIcazeVerir()
    {
        var policy = Policy(Budget());

        var image = policy.ForImage();
        var video = policy.ForVideo();

        Assert.True(image.Allowed, image.Reason);
        Assert.Equal(MediaModelCatalog.Gen4ImageTurbo, image.Model);
        Assert.Equal(2, image.Credits);
        Assert.Equal(0.02m, image.Usd);

        Assert.True(video.Allowed, video.Reason);
        Assert.Equal(MediaModelCatalog.Gen4Turbo, video.Model);
        Assert.Equal(50, video.Credits);
        Assert.Equal(0.50m, video.Usd);

        // Bir tamamlanmış run = $0.52.
        Assert.Equal(0.52m, image.Usd + video.Usd);
    }

    /// <summary>
    /// Naməlum model, profil kənarı model və tavanı aşan təxmin BAĞLI sınır.
    /// </summary>
    [Theory]
    [InlineData("uydurma-model", "unknown-model")]
    [InlineData(MediaModelCatalog.Gen45, "model-not-in-profile")]
    public void NamelumVeIcazesizModel_RedEdilir(string model, string reason)
    {
        var options = Budget();
        options.VideoModel = model;

        var decision = Policy(options).ForVideo();

        Assert.False(decision.Allowed);
        Assert.Equal(reason, decision.Reason);
    }

    [Fact]
    public void TavaniAsanTexmin_RedEdilir()
    {
        var options = Budget();
        options.MaxVideoCreditsPerRun = 40;

        var decision = Policy(options).ForVideo();

        Assert.False(decision.Allowed);
        Assert.Equal("over-credit-cap", decision.Reason);
    }

    /// <summary>Yataylıq və müqavilədən kənar müddət rədd edilir.</summary>
    [Theory]
    [InlineData("1280:720", 10, "ratio-not-supported")]
    [InlineData("720:1280", 15, "duration-out-of-contract")]
    public void YanlisNisbetVeMuddet_RedEdilir(string ratio, int duration, string reason)
    {
        var options = Budget();
        options.VideoRatio = ratio;
        options.VideoDurationSeconds = duration;

        var decision = Policy(options).ForVideo();

        Assert.False(decision.Allowed);
        Assert.Equal(reason, decision.Reason);
    }

    /// <summary>
    /// <c>QualityDemo</c> profili bahalıdır və bu, AÇIQ görünür — təxmin
    /// böyüklərin qərarı üçün dəqiq olmalıdır.
    /// </summary>
    [Fact]
    public void KeyfiyyetProfili_BahaOlduguGorunur()
    {
        var options = Budget();
        options.Profile = PetBrainMediaProfile.QualityDemo;
        options.VideoModel = MediaModelCatalog.Gen45;
        options.MaxVideoCreditsPerRun = 120;

        var policy = Policy(options);

        Assert.Equal(120, policy.ForVideo().Credits);
        Assert.Equal(1.20m, policy.ForVideo().Usd);
        Assert.Equal(1.22m, policy.ForImage().Usd + policy.ForVideo().Usd);
    }

    /// <summary>
    /// <c>FallbackOnly</c> profilində provayder seçilsə də xərc SIFIRDIR.
    /// </summary>
    [Fact]
    public void EhtiyatProfili_XercSifirdir()
    {
        var options = Budget();
        options.Profile = PetBrainMediaProfile.FallbackOnly;

        var policy = Policy(options);

        Assert.False(policy.Options.PaidMediaEnabled);
        Assert.False(policy.ForImage().Allowed);
        Assert.False(policy.ForVideo().Allowed);
    }

    /// <summary>
    /// Dövrə kəsicisi açılanda YENİ pullu iş başlamır — səssizcə artıq
    /// xərcləməkdənsə dayanmaq düzgündür.
    /// </summary>
    [Fact]
    public void AciqDovre_YeniIsiDayandirir()
    {
        var breaker = new MediaCircuitBreaker();
        var policy = new PetBrainMediaCostPolicy(Options(Budget()), breaker);

        Assert.True(policy.ForVideo().Allowed);

        breaker.Open("realized-over-estimate");

        Assert.False(policy.ForVideo().Allowed);
        Assert.Equal("circuit-open", policy.ForVideo().Reason);
        Assert.True(breaker.IsOpen);
    }

    /// <summary>Kataloq naməlum açarı heç yerdə tanımır.</summary>
    [Fact]
    public void Kataloq_NamelumModeliTanimir()
    {
        Assert.Null(MediaModelCatalog.Find("uydurma"));
        Assert.False(MediaModelCatalog.IsAllowed(PetBrainMediaProfile.Budget, "uydurma"));
        Assert.False(MediaModelCatalog.IsAllowed(PetBrainMediaProfile.FallbackOnly, MediaModelCatalog.Gen4Turbo));
        Assert.Equal(int.MaxValue, MediaModelCatalog.WorstCaseCredits("uydurma", 10));
    }

    // ==================== Köməkçilər ====================

    private static PetBrainMediaOptions Budget() => new()
    {
        Provider = PetBrainMediaProvider.Runway,
        Profile = PetBrainMediaProfile.Budget
    };

    private static PetBrainMediaCostPolicy Policy(PetBrainMediaOptions options) =>
        new(Options(options), new MediaCircuitBreaker());

    private static Microsoft.Extensions.Options.IOptions<PetBrainMediaOptions> Options(PetBrainMediaOptions value) =>
        Microsoft.Extensions.Options.Options.Create(value);

    private static AdventureRecapSpec MarsSpec(string route, string rescue) => new(
        RunId: RunId,
        ChildProfileId: ChildId,
        ExperienceKey: ExperienceCatalog.MarsRoverRescue,
        SpecVersion: AdventureRecapSpec.CurrentVersion,
        Language: "az",
        DurationSeconds: 10,
        PetSpecies: "fox",
        PetColor: "warm-orange",
        PetCosmetic: "helmet-mars",
        Beats: [new("route", route), new("rescue", rescue)],
        PuzzleMechanic: nameof(PetBrainPuzzleMechanic.OrderedRoute),
        PuzzleOutcome: "no-hints",
        Environment: "martian-canyon",
        Palette: "warm-orange",
        Mood: "hopeful-adventurous",
        CameraStyle: "gentle-storybook",
        SceneSpecHash: "abc123");

    private static AdventureRecapSpec DragonSpec(string palette, string habitat) => MarsSpec("canyon", "battery") with
    {
        ExperienceKey = ExperienceCatalog.DragonLostColors,
        PetCosmetic = "wings-rainbow",
        Beats = [new("palette", palette), new("habitat", habitat)],
        Environment = "crystal-garden-at-dusk",
        Palette = "violet-and-moonlight",
        Mood = "gentle-wonder"
    };

    /// <summary>
    /// Minimal MP4 başlığı: <c>ftyp</c> + <c>mvhd</c> + <c>tkhd</c>.
    ///
    /// <para>Yoxlayıcı tam dekoder deyil — o, müddəti və ölçünü qutulardan
    /// oxuyur, ona görə test də məhz həmin qutuları qurur.</para>
    /// </summary>
    private static byte[] Mp4(int width, int height, double seconds)
    {
        List<byte> bytes = [];

        // ftyp qutusu.
        bytes.AddRange([0, 0, 0, 16]);
        bytes.AddRange("ftyp"u8.ToArray());
        bytes.AddRange("isom"u8.ToArray());
        bytes.AddRange([0, 0, 0, 0]);

        // mvhd: version(1) flags(3) created(4) modified(4) timescale(4) duration(4)
        const int timescale = 1000;
        bytes.AddRange("mvhd"u8.ToArray());
        bytes.AddRange([0, 0, 0, 0]);
        bytes.AddRange(BigEndian(0));
        bytes.AddRange(BigEndian(0));
        bytes.AddRange(BigEndian(timescale));
        bytes.AddRange(BigEndian((uint)Math.Round(seconds * timescale)));

        // tkhd: version(1) flags(3) created(4) modified(4) trackId(4) reserved(4)
        //       duration(4) reserved(8) layer(2) group(2) volume(2) reserved(2)
        //       matrix(36) width(4) height(4)
        bytes.AddRange("tkhd"u8.ToArray());
        bytes.AddRange([0, 0, 0, 0]);
        bytes.AddRange(BigEndian(0));
        bytes.AddRange(BigEndian(0));
        bytes.AddRange(BigEndian(1));
        bytes.AddRange(BigEndian(0));
        bytes.AddRange(BigEndian((uint)Math.Round(seconds * timescale)));
        bytes.AddRange(new byte[8]);
        bytes.AddRange(new byte[8]);
        bytes.AddRange(new byte[36]);
        bytes.AddRange(BigEndian((uint)width << 16));
        bytes.AddRange(BigEndian((uint)height << 16));

        return [.. bytes];
    }

    private static byte[] BigEndian(uint value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
