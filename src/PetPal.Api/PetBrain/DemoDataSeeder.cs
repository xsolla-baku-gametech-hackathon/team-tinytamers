using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Security;
using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Nümayiş profilləri: <b>Aylin</b> (kosmos/tapmaca) və <b>Mia</b> (nağıl/yaradıcı).
///
/// <para>Məqsəd bir cümlədə göstərilə bilən bir şeydir: <b>eyni build, eyni pet
/// adı, eyni növ və eyni statlar — fərqli uşaq, fərqli macəra.</b> Ona görə hər
/// iki pet "Luna" adlanır, eyni növdədir, eyni yaşdadır və eyni baza statları
/// ilə başlayır. Yeganə fərq PROFİLDİR.</para>
///
/// <para><b>Təhlükəsizlik.</b> Toxum üç qapıdan keçir:</para>
/// <list type="number">
///   <item><c>PetBrain:SeedDemoData</c> açıq olmalıdır (standart: bağlı).</item>
///   <item>Mühit Development olmalıdır — VƏ YA
///   <c>PetBrain:AllowSeedOutsideDevelopment</c> açıq şəkildə verilməlidir.</item>
///   <item>Toxum idempotentdir: mövcud nümayiş valideyni varsa heç nə yaratmır.</item>
/// </list>
///
/// <para>Heç bir addımda autentifikasiya zəifləmir: nümayiş uşaqlarına da adi
/// PIN qoyulur və valideyn adi parol ilə daxil olur.</para>
/// </summary>
public static class DemoDataSeeder
{
    /// <summary>Hər iki pet EYNİ görünüşlə başlayır — fərq yalnız profildə olsun deyə.</summary>
    private const string SharedPetName = "Luna";
    private const string SharedPetSpecies = "fox";
    private const int SharedPetLevel = 4;

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var options = provider.GetRequiredService<IOptions<PetBrainOptions>>().Value;
        var environment = provider.GetRequiredService<IHostEnvironment>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DemoDataSeeder));

        if (!options.Enabled || !options.SeedDemoData)
            return;

        if (!environment.IsDevelopment() && !options.AllowSeedOutsideDevelopment)
        {
            logger.LogWarning(
                "PetBrain:SeedDemoData açıqdır, amma mühit {Environment}-dır. Nümayiş məlumatı YAZILMADI — " +
                "produksiyada nümayiş hesabı yaranmamalıdır. Bilərəkdən istəyirsinizsə " +
                "PetBrain:AllowSeedOutsideDevelopment açarını qoşun.",
                environment.EnvironmentName);
            return;
        }

        var db = provider.GetRequiredService<AppDbContext>();
        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var clock = provider.GetRequiredService<TimeProvider>();
        var now = clock.GetUtcNow().UtcDateTime;

        // İdempotentlik: nümayiş valideyni varsa toxum bir daha atılmır.
        var existing = await users.FindByEmailAsync(options.DemoParentEmail);
        if (existing is not null)
        {
            logger.LogInformation("Pet Brain nümayiş profilləri artıq mövcuddur — toxum atılmadı.");
            return;
        }

        var parent = new ApplicationUser
        {
            UserName = options.DemoParentEmail,
            Email = options.DemoParentEmail,
            EmailConfirmed = true,
            DisplayName = "PetPal Demo",
            CreatedAt = now
        };

        var created = await users.CreateAsync(parent, options.DemoParentPassword);
        if (!created.Succeeded)
        {
            logger.LogWarning("Nümayiş valideyni yaradılmadı: {Errors}",
                string.Join("; ", created.Errors.Select(e => e.Description)));
            return;
        }

        await users.AddToRoleAsync(parent, DbInitializer.ParentRole);

        var aylin = BuildChild(parent.Id, "Aylin", age: 9, options.DemoChildPin, now);
        var mia = BuildChild(parent.Id, "Mia", age: 8, options.DemoChildPin, now);

        db.ChildProfiles.AddRange(aylin, mia);
        await db.SaveChangesAsync(ct);

        // ---- Aylin: kosmos, elm, tapmaca; kəşfiyyatçı və həlledici ----
        AddTraits(db, aylin.Id, now,
            interests: new()
            {
                [TraitKeys.Space] = 85,
                [TraitKeys.Science] = 75,
                [TraitKeys.Puzzles] = 90,
                [TraitKeys.Animals] = 10,
                [TraitKeys.Fantasy] = 10,
                [TraitKeys.Stories] = 10,
                [TraitKeys.Nature] = 12,
                [TraitKeys.Ocean] = 10
            },
            playStyles: new()
            {
                [TraitKeys.Explorer] = 75,
                [TraitKeys.ProblemSolver] = 85,
                [TraitKeys.Creative] = 10,
                [TraitKeys.Caring] = 25,
                [TraitKeys.Playful] = 25
            });

        // Aylin Ay macərasını ARTIQ oynayıb: yenilik qaydası onu Marsa itələyir,
        // amma kosmos marağı olduğu kimi qalır (bax AdaptivePetDirector).
        var moonRun = new ExperienceRun
        {
            ChildProfileId = aylin.Id,
            TemplateKey = ExperienceCatalog.MoonCrystalRescue,
            ExperienceType = PetBrainExperienceType.Adventure,
            Theme = TraitKeys.Space,
            Difficulty = PetBrainDifficulty.Medium,
            Status = PetBrainRunStatus.Completed,
            CurrentStage = 4,
            Choices = ["continue", "deep-crater", "solved", "magnet-glove"],

            // Nəticə qəsdən "yaxşı, amma mükəmməl deyil": çətinlik OLDUĞU KİMİ
            // qalır, yəni nümayişdə Orta pillə görünür.
            ScorePercent = 80,
            HintsUsed = 0,
            Mistakes = 1,
            StartedAt = now.AddDays(-2),
            CompletedAt = now.AddDays(-2).AddMinutes(5),
            RewardApplied = true
        };

        db.ExperienceRuns.Add(moonRun);

        db.PetMemories.Add(new PetMemory
        {
            ChildProfileId = aylin.Id,
            Kind = PetBrainMemoryKind.FirstAdventure,
            FactKey = ExperienceCatalog.MoonCrystalRescue,
            ValueKey = string.Empty,
            Importance = MemoryPolicy.FirstAdventureImportance,
            CreatedAt = now.AddDays(-2),
            Tags = [TraitKeys.Space, "adventure"]
        });

        db.PetMemories.Add(new PetMemory
        {
            ChildProfileId = aylin.Id,
            Kind = PetBrainMemoryKind.ChoiceMade,
            FactKey = ExperienceCatalog.MoonCrystalRescue,
            ValueKey = "magnet-glove",
            Importance = MemoryPolicy.ChoiceImportance,
            CreatedAt = now.AddDays(-2),
            Tags = [TraitKeys.Space]
        });

        // ---- Mia: nağıl, heyvan, yaradıcılıq, hekayə ----
        AddTraits(db, mia.Id, now,
            interests: new()
            {
                [TraitKeys.Fantasy] = 90,
                [TraitKeys.Animals] = 85,
                [TraitKeys.Stories] = 82,
                [TraitKeys.Nature] = 30,
                [TraitKeys.Ocean] = 25,
                [TraitKeys.Space] = 15,
                [TraitKeys.Science] = 15,
                [TraitKeys.Puzzles] = 10
            },
            playStyles: new()
            {
                [TraitKeys.Creative] = 88,
                [TraitKeys.Caring] = 60,
                [TraitKeys.Playful] = 40,
                [TraitKeys.Explorer] = 30,
                [TraitKeys.ProblemSolver] = 20
            });

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Pet Brain nümayiş profilləri yazıldı: {Email} → Aylin (kosmos) və Mia (nağıl).",
            options.DemoParentEmail);
    }

    private static ChildProfile BuildChild(Guid parentId, string name, int age, string pin, DateTime now) =>
        new()
        {
            ParentUserId = parentId,
            DisplayName = name,
            Age = age,
            AvatarKey = "avatar-fox",
            LanguageCode = Common.Localized.Azerbaijani,
            PinHash = PinHasher.Hash(pin),
            FriendCode = FriendCodeGenerator.Create(),

            // Yumurta açmaq nümayişin mövzusu deyil — hər iki pet hazırdır.
            Stars = 120,
            CreatedAt = now,
            Pet = new Pet
            {
                // EYNİ pet: ad, növ, yaş və statlar hər iki uşaqda eynidir.
                // Fərq yalnız profildən gəlməlidir.
                Name = SharedPetName,
                Species = SharedPetSpecies,
                Level = SharedPetLevel,
                Xp = 40,
                Happiness = 80,
                Energy = 80,
                Fullness = 80,
                Cleanliness = 90,
                Bond = 24,
                HatchedAt = now.AddDays(-6),
                LastDecayAt = now,
                CreatedAt = now.AddDays(-6)
            }
        };

    private static void AddTraits(
        AppDbContext db,
        Guid childId,
        DateTime now,
        Dictionary<string, int> interests,
        Dictionary<string, int> playStyles)
    {
        foreach (var (key, score) in interests)
            db.PlayerTraits.Add(new PlayerTrait
            {
                ChildProfileId = childId,
                Category = PetBrainTraitCategory.Interest,
                Key = key,
                Score = TraitKeys.Clamp(score),
                UpdatedAt = now
            });

        foreach (var (key, score) in playStyles)
            db.PlayerTraits.Add(new PlayerTrait
            {
                ChildProfileId = childId,
                Category = PetBrainTraitCategory.PlayStyle,
                Key = key,
                Score = TraitKeys.Clamp(score),
                UpdatedAt = now
            });
    }
}
