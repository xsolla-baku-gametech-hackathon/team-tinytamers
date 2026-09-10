using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PetPal.Api.Common;
using PetPal.Api.Data.Questions;
using PetPal.Api.Entities;
using PetPal.Shared.Enums;

namespace PetPal.Api.Data;

/// <summary>
/// Başlanğıc məlumatları: rollar, nişanlar, dünya bölgələri, missiyalar və sual bankı.
/// Idempotent-dir — mövcud sətirlərə toxunmur, yalnız çatışmayanları əlavə edir.
/// Sual bankı dil üzrə ayrıca seed olunur, ona görə yeni dil sonradan əlavə edilə bilər.
/// </summary>
public static class DbInitializer
{
    public const string ParentRole = "Parent";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        if (!await roleManager.RoleExistsAsync(ParentRole))
            await roleManager.CreateAsync(new ApplicationRole(ParentRole));

        await SeedBadgesAsync(db, ct);
        await SeedWorldAsync(db, ct);
        await SeedQuestionsAsync(db, ct);
        await RetireQuestionsAsync(db, ct);
    }

    /// <summary>
    /// Bankdan çıxarılan suallar. Bank faylından sətri silmək kifayət deyil: sual
    /// mövcud bazalara artıq yazılıb və seed yalnız ƏLAVƏ edir, silmir. Sətir
    /// burada da silinmir, yalnız passivləşdirilir — verilmiş cavabların
    /// tarixçəsi qalmalıdır.
    /// </summary>
    private static readonly string[] RetiredPrompts =
    [
        "Həşəratın neçə ayağı var?",
        "How many legs does an insect have?"
    ];

    private static async Task RetireQuestionsAsync(AppDbContext db, CancellationToken ct)
    {
        var retired = await db.Questions
            .Where(q => q.IsActive && RetiredPrompts.Contains(q.Prompt))
            .ToListAsync(ct);

        if (retired.Count == 0)
            return;

        foreach (var question in retired)
            question.IsActive = false;

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedBadgesAsync(AppDbContext db, CancellationToken ct)
    {
        var catalogue = new[]
        {
            new Badge { Code = "first-steps", Tier = BadgeTier.Bronze, IconKey = "badge-star",
                Title = "First Steps", Description = "Answer your very first question.",
                TitleAz = "İlk addımlar", DescriptionAz = "İlk sualını cavabla." },
            new Badge { Code = "perfect-round", Tier = BadgeTier.Silver, IconKey = "badge-target",
                Title = "Perfect Round", Description = "Finish a session with every answer correct.",
                TitleAz = "Mükəmməl raund", DescriptionAz = "Bir dəsti tam doğru tamamla." },
            new Badge { Code = "math-starter", Tier = BadgeTier.Bronze, IconKey = "badge-math",
                Title = "Number Friend", Description = "Answer 25 maths questions correctly.",
                TitleAz = "Ədəd dostu", DescriptionAz = "25 riyaziyyat sualını doğru cavabla." },
            new Badge { Code = "word-wizard", Tier = BadgeTier.Bronze, IconKey = "badge-book",
                Title = "Word Wizard", Description = "Answer 25 vocabulary questions correctly.",
                TitleAz = "Söz ustası", DescriptionAz = "25 söz sualını doğru cavabla." },
            new Badge { Code = "logic-hero", Tier = BadgeTier.Bronze, IconKey = "badge-puzzle",
                Title = "Logic Hero", Description = "Answer 25 logic questions correctly.",
                TitleAz = "Məntiq qəhrəmanı", DescriptionAz = "25 məntiq sualını doğru cavabla." },
            new Badge { Code = "streak-3", Tier = BadgeTier.Bronze, IconKey = "badge-flame",
                Title = "Three in a Row", Description = "Reach your daily goal 3 days in a row.",
                TitleAz = "Ardıcıl üç gün", DescriptionAz = "3 gün ardıcıl gündəlik hədəfə çat." },
            new Badge { Code = "streak-7", Tier = BadgeTier.Gold, IconKey = "badge-crown",
                Title = "Week Champion", Description = "Reach your daily goal 7 days in a row.",
                TitleAz = "Həftənin çempionu", DescriptionAz = "7 gün ardıcıl gündəlik hədəfə çat." },
            new Badge { Code = "explorer", Tier = BadgeTier.Silver, IconKey = "badge-leaf",
                Title = "Explorer", Description = "Make 5 real-world discoveries.",
                TitleAz = "Kəşfiyyatçı", DescriptionAz = "Real dünyada 5 kəşf et." },
            new Badge { Code = "pet-friend", Tier = BadgeTier.Silver, IconKey = "badge-heart",
                Title = "Best Friend", Description = "Care for your pet 20 times.",
                TitleAz = "Ən yaxşı dost", DescriptionAz = "Pet-inə 20 dəfə qulluq et." },
            new Badge { Code = "world-healer", Tier = BadgeTier.Gold, IconKey = "badge-globe",
                Title = "World Healer", Description = "Restore your first world zone.",
                TitleAz = "Dünyanın şəfaçısı", DescriptionAz = "İlk zonanı tam bərpa et." },
            new Badge { Code = "game-master", Tier = BadgeTier.Bronze, IconKey = "badge-game",
                Title = "Game Master", Description = "Finish 10 mini games.",
                TitleAz = "Oyun ustası", DescriptionAz = "10 mini oyun tamamla." },
            new Badge { Code = "arena-first", Tier = BadgeTier.Bronze, IconKey = "badge-sword",
                Title = "First Duel", Description = "Finish your first arena duel.",
                TitleAz = "İlk duel", DescriptionAz = "İlk arena duelini tamamla." },
            new Badge { Code = "arena-veteran", Tier = BadgeTier.Silver, IconKey = "badge-shield",
                Title = "Arena Regular", Description = "Finish 10 arena duels.",
                TitleAz = "Arena davamçısı", DescriptionAz = "10 arena dueli tamamla." },
            new Badge { Code = "arena-champion", Tier = BadgeTier.Gold, IconKey = "badge-trophy",
                Title = "Arena Champion", Description = "Win 5 arena duels.",
                TitleAz = "Arena çempionu", DescriptionAz = "5 arena duelində qalib gəl." },
            new Badge { Code = "arena-league-top3", Tier = BadgeTier.Gold, IconKey = "badge-medal",
                Title = "League Podium", Description = "Finish a week in the top three of the arena league.",
                TitleAz = "Liqanın ilk üçlüyü", DescriptionAz = "Həftəlik liqada ilk üçlüyə düş." }
        };

        var existing = await db.Badges.Select(b => b.Code).ToListAsync(ct);
        var missing = catalogue.Where(b => !existing.Contains(b.Code)).ToList();
        if (missing.Count == 0)
            return;

        db.Badges.AddRange(missing);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedWorldAsync(AppDbContext db, CancellationToken ct)
    {
        var zones = new[]
        {
            new WorldZone { Code = "meadow", IconKey = "zone-meadow", RequiredStars = 0, SortOrder = 1,
                Name = "Sunny Meadow", Description = "Where every journey begins.",
                NameAz = "Günəşli çəmən", DescriptionAz = "Hər səyahət buradan başlayır." },
            new WorldZone { Code = "forest", IconKey = "zone-forest", RequiredStars = 150, SortOrder = 2,
                Name = "Whisper Forest", Description = "The trees are waiting to wake up.",
                NameAz = "Pıçıltı meşəsi", DescriptionAz = "Ağaclar oyanmağı gözləyir." },
            new WorldZone { Code = "lake", IconKey = "zone-lake", RequiredStars = 400, SortOrder = 3,
                Name = "Crystal Lake", Description = "Clear water, clever puzzles.",
                NameAz = "Büllur göl", DescriptionAz = "Duru su, ağıllı tapmacalar." },
            new WorldZone { Code = "mountain", IconKey = "zone-mountain", RequiredStars = 800, SortOrder = 4,
                Name = "Cloud Peak", Description = "Only the boldest learners climb here.",
                NameAz = "Bulud zirvəsi", DescriptionAz = "Bura yalnız ən cəsurlar qalxır." },
            new WorldZone { Code = "castle", IconKey = "zone-castle", RequiredStars = 1500, SortOrder = 5,
                Name = "Star Castle", Description = "Home of the Guardian pets.",
                NameAz = "Ulduz qəsri", DescriptionAz = "Qoruyucu pet-lərin evi." }
        };

        var existingZones = await db.WorldZones.ToDictionaryAsync(z => z.Code, ct);
        foreach (var zone in zones)
        {
            if (existingZones.ContainsKey(zone.Code))
                continue;

            db.WorldZones.Add(zone);
            existingZones[zone.Code] = zone;
        }

        await db.SaveChangesAsync(ct);

        var missions = new (string Zone, Mission Mission)[]
        {
            ("meadow", new Mission { Code = "meadow-first-3", Type = MissionType.SolveQuestions, Target = 3, RewardStars = 30, SortOrder = 1,
                Title = "Wake the Meadow", Description = "Solve 3 questions to bring colour back to the grass.",
                TitleAz = "Çəməni oyat", DescriptionAz = "3 sual həll et ki, otlar yenidən yaşıllaşsın." }),
            ("meadow", new Mission { Code = "meadow-care-2", Type = MissionType.CareForPet, Target = 2, RewardStars = 20, SortOrder = 2,
                Title = "A Happy Friend", Description = "Care for your pet 2 times.",
                TitleAz = "Xoşbəxt dost", DescriptionAz = "Pet-inə 2 dəfə qulluq et." }),
            ("meadow", new Mission { Code = "meadow-math-5", Type = MissionType.SolveQuestions, Skill = SkillArea.Math, Target = 5, RewardStars = 40, SortOrder = 3,
                Title = "Counting Flowers", Description = "Solve 5 maths questions.",
                TitleAz = "Çiçəkləri say", DescriptionAz = "5 riyaziyyat sualı həll et." }),
            ("meadow", new Mission { Code = "meadow-discover-1", Type = MissionType.DiscoverRealWorld, Target = 1, RewardStars = 25, RewardGems = 1, SortOrder = 4,
                Title = "Step Outside", Description = "Discover 1 thing in the real world.",
                TitleAz = "Çölə çıx", DescriptionAz = "Real dünyada 1 şey kəşf et." }),

            ("forest", new Mission { Code = "forest-words-8", Type = MissionType.SolveQuestions, Skill = SkillArea.Vocabulary, Target = 8, RewardStars = 60, SortOrder = 1,
                Title = "Whispering Words", Description = "Solve 8 vocabulary questions.",
                TitleAz = "Pıçıldayan sözlər", DescriptionAz = "8 söz sualı həll et." }),
            ("forest", new Mission { Code = "forest-streak-3", Type = MissionType.KeepStreak, Target = 3, RewardStars = 70, RewardGems = 2, SortOrder = 2,
                Title = "Three Bright Days", Description = "Reach your daily goal 3 days in a row.",
                TitleAz = "Üç parlaq gün", DescriptionAz = "3 gün ardıcıl gündəlik hədəfə çat." }),
            ("forest", new Mission { Code = "forest-logic-8", Type = MissionType.SolveQuestions, Skill = SkillArea.Logic, Target = 8, RewardStars = 60, SortOrder = 3,
                Title = "Untangle the Path", Description = "Solve 8 logic questions.",
                TitleAz = "Cığırı aç", DescriptionAz = "8 məntiq sualı həll et." }),

            ("lake", new Mission { Code = "lake-solve-20", Type = MissionType.SolveQuestions, Target = 20, RewardStars = 120, RewardGems = 2, SortOrder = 1,
                Title = "Ripples of Knowledge", Description = "Solve 20 questions of any kind.",
                TitleAz = "Biliyin dalğaları", DescriptionAz = "İstənilən növdən 20 sual həll et." }),
            ("lake", new Mission { Code = "lake-discover-5", Type = MissionType.DiscoverRealWorld, Target = 5, RewardStars = 100, SortOrder = 2,
                Title = "Nature Collector", Description = "Make 5 real-world discoveries.",
                TitleAz = "Təbiət kolleksiyaçısı", DescriptionAz = "Real dünyada 5 kəşf et." }),
            ("lake", new Mission { Code = "lake-team-1", Type = MissionType.TeamChallenge, Target = 1, RewardStars = 90, RewardGems = 1, SortOrder = 3,
                Title = "Better Together", Description = "Finish a team mission with a friend.",
                TitleAz = "Birlikdə daha güclü", DescriptionAz = "Dostunla komanda missiyasını tamamla." }),

            ("mountain", new Mission { Code = "mountain-science-10", Type = MissionType.SolveQuestions, Skill = SkillArea.Science, Target = 10, RewardStars = 150, SortOrder = 1,
                Title = "Climb and Wonder", Description = "Solve 10 science questions.",
                TitleAz = "Qalx və öyrən", DescriptionAz = "10 elm sualı həll et." }),
            ("mountain", new Mission { Code = "mountain-streak-7", Type = MissionType.KeepStreak, Target = 7, RewardStars = 200, RewardGems = 3, SortOrder = 2,
                Title = "Seven Peaks", Description = "Reach your daily goal 7 days in a row.",
                TitleAz = "Yeddi zirvə", DescriptionAz = "7 gün ardıcıl gündəlik hədəfə çat." }),

            ("castle", new Mission { Code = "castle-solve-100", Type = MissionType.SolveQuestions, Target = 100, RewardStars = 400, RewardGems = 5, SortOrder = 1,
                Title = "Guardian Trial", Description = "Solve 100 questions in total.",
                TitleAz = "Qoruyucu sınağı", DescriptionAz = "Ümumilikdə 100 sual həll et." }),
            ("castle", new Mission { Code = "castle-care-30", Type = MissionType.CareForPet, Target = 30, RewardStars = 250, RewardGems = 2, SortOrder = 2,
                Title = "Loyal Heart", Description = "Care for your pet 30 times.",
                TitleAz = "Sadiq ürək", DescriptionAz = "Pet-inə 30 dəfə qulluq et." })
        };

        var existingMissions = await db.Missions.Select(m => m.Code).ToListAsync(ct);
        var added = false;
        foreach (var (zoneCode, mission) in missions)
        {
            if (existingMissions.Contains(mission.Code))
                continue;

            mission.WorldZoneId = existingZones[zoneCode].Id;
            db.Missions.Add(mission);
            added = true;
        }

        if (added)
            await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sual bankı. Mətnlər <c>Data/Questions/</c> altındadır — dörd bacarıq ×
    /// on çətinlik × iki dil bir faylda saxlanılsaydı, seed məntiqi itərdi.
    ///
    /// Əlavə etmə PROMPT-a görə edilir, dilə görə yox: bank böyüyəndə mövcud
    /// bazaya da yalnız yeni suallar düşür. Əvvəl "bu dildə sual varsa keç"
    /// şərti vardı — banka əlavə edilən sual heç vaxt işlək bazaya çatmırdı.
    /// </summary>
    private static async Task SeedQuestionsAsync(AppDbContext db, CancellationToken ct)
    {
        foreach (var language in Localized.Supported)
        {
            var known = await db.Questions
                .Where(q => q.LanguageCode == language)
                .Select(q => q.Prompt)
                .ToListAsync(ct);

            var seen = known.ToHashSet(StringComparer.Ordinal);

            var bank = MathQuestionFactory.Build(language)
                .Concat(language == Localized.Azerbaijani ? AzQuestionBank.All() : EnQuestionBank.All());

            // seen.Add həm bazadakı təkrarı, həm də bankın öz içindəki təkrarı kəsir.
            var fresh = bank.Where(q => seen.Add(q.Prompt)).ToList();

            if (fresh.Count == 0)
                continue;

            db.Questions.AddRange(fresh);
            await db.SaveChangesAsync(ct);
        }
    }
}
