using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PetPal.Api.Entities;

namespace PetPal.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ChildProfile> ChildProfiles => Set<ChildProfile>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<SkillMastery> SkillMasteries => Set<SkillMastery>();
    public DbSet<LearningSession> LearningSessions => Set<LearningSession>();
    public DbSet<SessionAnswer> SessionAnswers => Set<SessionAnswer>();
    public DbSet<DailyGoal> DailyGoals => Set<DailyGoal>();
    public DbSet<RewardEntry> RewardEntries => Set<RewardEntry>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<ChildBadge> ChildBadges => Set<ChildBadge>();
    public DbSet<WorldZone> WorldZones => Set<WorldZone>();
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<ChildMission> ChildMissions => Set<ChildMission>();
    public DbSet<Discovery> Discoveries => Set<Discovery>();
    public DbSet<ChatTurn> ChatTurns => Set<ChatTurn>();
    public DbSet<GameResult> GameResults => Set<GameResult>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<TeamMission> TeamMissions => Set<TeamMission>();
    public DbSet<TeamMissionMember> TeamMissionMembers => Set<TeamMissionMember>();
    public DbSet<Duel> Duels => Set<Duel>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<DuelQuestion> DuelQuestions => Set<DuelQuestion>();
    public DbSet<DuelEntry> DuelEntries => Set<DuelEntry>();
    public DbSet<DuelAnswer> DuelAnswers => Set<DuelAnswer>();

    // ---------- Pet Brain (Adaptive Pet Director) ----------
    public DbSet<PlayerTrait> PlayerTraits => Set<PlayerTrait>();
    public DbSet<BehaviorEvent> BehaviorEvents => Set<BehaviorEvent>();
    public DbSet<PetMemory> PetMemories => Set<PetMemory>();
    public DbSet<ExperienceRun> ExperienceRuns => Set<ExperienceRun>();
    public DbSet<IssuedPuzzle> IssuedPuzzles => Set<IssuedPuzzle>();
    public DbSet<PuzzleIllustration> PuzzleIllustrations => Set<PuzzleIllustration>();
    public DbSet<AdventureRecap> AdventureRecaps => Set<AdventureRecap>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(60).IsRequired();
            e.Property(x => x.ParentGatePinHash).HasMaxLength(256);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChildProfile>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(40).IsRequired();
            e.Property(x => x.AvatarKey).HasMaxLength(40).IsRequired();
            e.Property(x => x.PinHash).HasMaxLength(256).IsRequired();
            e.Property(x => x.FriendCode).HasMaxLength(6).IsRequired();
            e.Property(x => x.LanguageCode).HasMaxLength(5).IsRequired();
            e.Property(x => x.UnlockedGames)
                .HasConversion(StringListConverter.Converter)
                .Metadata.SetValueComparer(StringListConverter.Comparer);
            // Miqrasiyadan ƏVVƏL yaradılmış profillər üçün də arena açıq olmalıdır:
            // sütun defoltu olmasa, mövcud uşaqlar özəlliyi bağlı görərdi.
            e.Property(x => x.ArenaEnabled).HasDefaultValue(true);
            e.Property(x => x.ArenaRating).HasDefaultValue(300);

            e.HasIndex(x => x.FriendCode).IsUnique();
            e.HasIndex(x => x.ParentUserId);

            e.HasOne(x => x.ParentUser)
                .WithMany(u => u.Children)
                .HasForeignKey(x => x.ParentUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Pet>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(24).IsRequired();
            e.Property(x => x.Species).HasMaxLength(24).IsRequired();

            // Miqrasiyadan ƏVVƏL yaradılmış pet-lər də eyni başlanğıc bağı almalıdır:
            // sütun defoltu olmasa, köhnə pet-lər 0 ilə qalar və uşaq "münasibətimiz
            // sıfırlandı" görərdi. Eyni qayda ArenaRating üçün də tətbiq olunub.
            e.Property(x => x.Bond).HasDefaultValue(10);
            e.Property(x => x.UnlockedAccessories)
                .HasConversion(StringListConverter.Converter)
                .Metadata.SetValueComparer(StringListConverter.Comparer);

            e.Property(x => x.EquippedAccessories)
                .HasConversion(StringListConverter.Converter)
                .Metadata.SetValueComparer(StringListConverter.Comparer);

            e.HasOne(x => x.ChildProfile)
                .WithOne(c => c.Pet)
                .HasForeignKey<Pet>(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Question>(e =>
        {
            e.Property(x => x.Prompt).HasMaxLength(400).IsRequired();
            e.Property(x => x.Explanation).HasMaxLength(400);
            e.Property(x => x.Hint).HasMaxLength(200);
            e.Property(x => x.LanguageCode).HasMaxLength(5).IsRequired();
            e.Property(x => x.Options)
                .HasConversion(StringListConverter.Converter)
                .Metadata.SetValueComparer(StringListConverter.Comparer);

            // Sual seçimi həmişə dil + bacarıq + çətinlik üzrə filtrləyir.
            e.HasIndex(x => new { x.LanguageCode, x.Skill, x.Difficulty, x.IsActive });
        });

        builder.Entity<SkillMastery>(e =>
        {
            e.HasIndex(x => new { x.ChildProfileId, x.Skill }).IsUnique();
            e.HasOne(x => x.ChildProfile)
                .WithMany(c => c.SkillMasteries)
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LearningSession>(e =>
        {
            e.HasIndex(x => new { x.ChildProfileId, x.StartedAt });
            e.HasOne(x => x.ChildProfile)
                .WithMany(c => c.Sessions)
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SessionAnswer>(e =>
        {
            e.HasIndex(x => new { x.LearningSessionId, x.QuestionId }).IsUnique();
            e.HasOne(x => x.LearningSession)
                .WithMany(s => s.Answers)
                .HasForeignKey(x => x.LearningSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DailyGoal>(e =>
        {
            e.HasIndex(x => new { x.ChildProfileId, x.Date }).IsUnique();
            e.HasOne(x => x.ChildProfile)
                .WithMany(c => c.DailyGoals)
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RewardEntry>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(120).IsRequired();
            e.HasIndex(x => new { x.ChildProfileId, x.CreatedAt });
            e.HasOne(x => x.ChildProfile)
                .WithMany(c => c.Rewards)
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Badge>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.Property(x => x.Title).HasMaxLength(60).IsRequired();
            e.Property(x => x.Description).HasMaxLength(200);
            e.Property(x => x.TitleAz).HasMaxLength(60);
            e.Property(x => x.DescriptionAz).HasMaxLength(200);
            e.Property(x => x.IconKey).HasMaxLength(40);
            e.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<ChildBadge>(e =>
        {
            e.HasIndex(x => new { x.ChildProfileId, x.BadgeId }).IsUnique();
            e.HasOne(x => x.ChildProfile)
                .WithMany(c => c.Badges)
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Badge)
                .WithMany(b => b.ChildBadges)
                .HasForeignKey(x => x.BadgeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WorldZone>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.Property(x => x.Description).HasMaxLength(240);
            e.Property(x => x.NameAz).HasMaxLength(60);
            e.Property(x => x.DescriptionAz).HasMaxLength(240);
            e.Property(x => x.IconKey).HasMaxLength(40);
            e.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<Mission>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.Property(x => x.Title).HasMaxLength(80).IsRequired();
            e.Property(x => x.Description).HasMaxLength(240);
            e.Property(x => x.TitleAz).HasMaxLength(80);
            e.Property(x => x.DescriptionAz).HasMaxLength(240);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.WorldZone)
                .WithMany(z => z.Missions)
                .HasForeignKey(x => x.WorldZoneId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChildMission>(e =>
        {
            e.HasIndex(x => new { x.ChildProfileId, x.MissionId }).IsUnique();
            e.HasOne(x => x.ChildProfile)
                .WithMany(c => c.Missions)
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Mission)
                .WithMany()
                .HasForeignKey(x => x.MissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Discovery>(e =>
        {
            e.Property(x => x.Label).HasMaxLength(60).IsRequired();
            e.Property(x => x.CategoryKey).HasMaxLength(40).IsRequired();
            e.Property(x => x.PhotoPath).HasMaxLength(260);
            e.HasIndex(x => new { x.ChildProfileId, x.CreatedAt });
            e.HasOne(x => x.ChildProfile)
                .WithMany(c => c.Discoveries)
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatTurn>(e =>
        {
            // 400 simvol: uşaq mesajı 200, pet cavabı 140 — ehtiyatla yuxarı yuvarlaqlaşdırılıb.
            e.Property(x => x.Text).HasMaxLength(400).IsRequired();
            e.Property(x => x.BlockedReason).HasMaxLength(60);
            e.HasIndex(x => new { x.ChildProfileId, x.Sequence });
            e.HasOne(x => x.ChildProfile)
                .WithMany(c => c.ChatTurns)
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GameResult>(e =>
        {
            e.Property(x => x.GameKey).HasMaxLength(40).IsRequired();
            e.HasIndex(x => new { x.ChildProfileId, x.CreatedAt });
            e.HasOne(x => x.ChildProfile)
                .WithMany()
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Friendship>(e =>
        {
            e.HasIndex(x => new { x.ChildProfileId, x.FriendChildProfileId }).IsUnique();
            e.HasOne(x => x.ChildProfile)
                .WithMany()
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.FriendChildProfile)
                .WithMany()
                .HasForeignKey(x => x.FriendChildProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DeviceToken>(e =>
        {
            // Bir token bir dəfə: eyni cihaz iki sətirdə olsa, bildiriş də ikiqat gedərdi.
            e.HasIndex(x => x.Token).IsUnique();
            e.Property(x => x.Token).HasMaxLength(4096).IsRequired();
            e.Property(x => x.Platform).HasMaxLength(16);

            e.HasOne(x => x.ChildProfile)
                .WithMany()
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.ParentUser)
                .WithMany()
                .HasForeignKey(x => x.ParentUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TeamMission>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(80).IsRequired();
            e.Property(x => x.Description).HasMaxLength(240);
            e.Property(x => x.TitleAz).HasMaxLength(80);
            e.Property(x => x.DescriptionAz).HasMaxLength(240);
        });

        builder.Entity<Duel>(e =>
        {
            // Uyğunlaşdırma sorğusunun oxuduğu ox: açıq duellər, ən köhnədən.
            e.HasIndex(x => new { x.Status, x.ExpiresAt, x.CreatedAt });
            e.HasOne(x => x.CreatedByChildProfile)
                .WithMany()
                .HasForeignKey(x => x.CreatedByChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Çağırılan uşaq silinsə duel də getməlidir, amma yaradanla eyni
            // profil ola bilməz — kaskad iki yoldan gəlməsin deyə Restrict.
            e.HasOne(x => x.TargetChildProfile)
                .WithMany()
                .HasForeignKey(x => x.TargetChildProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DuelQuestion>(e =>
        {
            e.HasIndex(x => new { x.DuelId, x.Order }).IsUnique();
            e.HasOne(x => x.Duel)
                .WithMany(d => d.Questions)
                .HasForeignKey(x => x.DuelId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DuelEntry>(e =>
        {
            // Bir uşaq eyni duelə iki dəfə qoşula bilməz.
            e.HasIndex(x => new { x.DuelId, x.ChildProfileId }).IsUnique();
            e.HasIndex(x => new { x.ChildProfileId, x.StartedAt });
            e.HasOne(x => x.Duel)
                .WithMany(d => d.Entries)
                .HasForeignKey(x => x.DuelId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ChildProfile)
                .WithMany(c => c.DuelEntries)
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DuelAnswer>(e =>
        {
            e.HasIndex(x => new { x.DuelEntryId, x.QuestionId }).IsUnique();
            e.HasOne(x => x.DuelEntry)
                .WithMany(entry => entry.Answers)
                .HasForeignKey(x => x.DuelEntryId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Pet Brain ----------

        builder.Entity<PlayerTrait>(e =>
        {
            e.Property(x => x.Key).HasMaxLength(40).IsRequired();

            // Bir uşaqda bir kateqoriya + açar cütü YALNIZ BİR DƏFƏ olur. Bu, təkcə
            // səliqə deyil: iki eyni vaxtlı hadisə eyni xassəni yaratmağa çalışsa,
            // ikincisi bazada dayanır və bal ikiqat artmır.
            e.HasIndex(x => new { x.ChildProfileId, x.Category, x.Key }).IsUnique();

            e.HasOne(x => x.ChildProfile)
                .WithMany(c => c.Traits)
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BehaviorEvent>(e =>
        {
            e.Property(x => x.Source).HasMaxLength(60).IsRequired();
            e.Property(x => x.Detail).HasMaxLength(200).IsRequired();
            e.Property(x => x.IdempotencyKey).HasMaxLength(120);

            // Direktorun oxuduğu ox: bu uşağın son hadisələri.
            e.HasIndex(x => new { x.ChildProfileId, x.OccurredAt });

            // Təkrarın qarşısını BAZA alır, yaddaşdakı yoxlama yox. Açar uşaq
            // başına unikaldır; NULL açarlar indeksə düşmür, yəni təkrarı
            // mümkün olmayan hadisələr limitsiz yazıla bilir.
            e.HasIndex(x => new { x.ChildProfileId, x.IdempotencyKey })
                .IsUnique()
                .HasFilter(null);

            e.HasOne(x => x.ChildProfile)
                .WithMany(c => c.BehaviorEvents)
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PetMemory>(e =>
        {
            e.Property(x => x.FactKey).HasMaxLength(60).IsRequired();
            e.Property(x => x.ValueKey).HasMaxLength(60).IsRequired();
            e.Property(x => x.Tags)
                .HasConversion(StringListConverter.Converter)
                .Metadata.SetValueComparer(StringListConverter.Comparer);

            e.HasIndex(x => new { x.ChildProfileId, x.CreatedAt });

            // Eyni fakt iki dəfə xatırlanmır: "Ay macərasını bitirdin" bir sətirdir,
            // təkrar oynanışda yalnız vacibliyi yenilənir.
            e.HasIndex(x => new { x.ChildProfileId, x.Kind, x.FactKey, x.ValueKey }).IsUnique();

            e.HasOne(x => x.ChildProfile)
                .WithMany(c => c.Memories)
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ExperienceRun>(e =>
        {
            e.Property(x => x.TemplateKey).HasMaxLength(60).IsRequired();
            e.Property(x => x.Theme).HasMaxLength(40).IsRequired();
            e.Property(x => x.Choices)
                .HasConversion(StringListConverter.Converter)
                .Metadata.SetValueComparer(StringListConverter.Comparer);

            e.HasIndex(x => new { x.ChildProfileId, x.StartedAt });

            // "Bu şablon artıq tamamlanıbmı" sualı hər tövsiyədə verilir.
            e.HasIndex(x => new { x.ChildProfileId, x.TemplateKey, x.Status });

            e.HasOne(x => x.ChildProfile)
                .WithMany(c => c.ExperienceRuns)
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IssuedPuzzle>(e =>
        {
            e.Property(x => x.BlueprintKey).HasMaxLength(40).IsRequired();
            e.Property(x => x.Seed).HasMaxLength(64).IsRequired();
            e.Property(x => x.ContentSignature).HasMaxLength(64).IsRequired();

            // Səhnə hash-ı tapmacanı rəsm sətri ilə bağlayır. Rəsm PAYLAŞILA
            // bilər (səhnə təsvirində uşağa aid heç nə yoxdur), ona görə əlaqə
            // xarici açar deyil, hash-dır.
            e.Property(x => x.SceneSpecHash).HasMaxLength(64).IsRequired();

            // Məzmun və həll JSON mətnidir; həll HEÇ BİR DTO-ya düşmür.
            e.Property(x => x.PublicPayload).HasMaxLength(4000).IsRequired();
            e.Property(x => x.PrivateSolution).HasMaxLength(1000).IsRequired();

            // Bir run-ın bir mərhələsinə YALNIZ BİR tapmaca verilir. Bu, təkcə
            // səliqə deyil: iki eyni vaxtlı sorğu ikinci tapmaca yaratmağa
            // çalışsa, bazada dayanır və uşaq sualın dəyişdiyini görmür.
            e.HasIndex(x => new { x.ExperienceRunId, x.StageIndex }).IsUnique();

            // Təkrar yoxlaması bu oxu oxuyur: uşağın son tapmacaları.
            e.HasIndex(x => new { x.ChildProfileId, x.IssuedAt });

            e.HasOne(x => x.ChildProfile)
                .WithMany()
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Run silinsə tapmaca da getməlidir, amma uşaq üzərindən ikinci
            // kaskad yolu yaranmasın deyə Restrict.
            e.HasOne(x => x.ExperienceRun)
                .WithMany(r => r.Puzzles)
                .HasForeignKey(x => x.ExperienceRunId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PuzzleIllustration>(e =>
        {
            e.Property(x => x.SceneSpecHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.BlueprintKey).HasMaxLength(40).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(40).IsRequired();
            e.Property(x => x.Model).HasMaxLength(80).IsRequired();
            e.Property(x => x.PromptHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.AssetKey).HasMaxLength(120).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(40).IsRequired();
            e.Property(x => x.FailureReason).HasMaxLength(60).IsRequired();

            // BİR SƏHNƏ → BİR PULLU SORĞU. Təminat yaddaşda deyil, məhz
            // buradadır: iki eyni vaxtlı sorğudan ikincisi bazada dayanır.
            e.HasIndex(x => x.SceneSpecHash).IsUnique();
        });

        builder.Entity<AdventureRecap>(e =>
        {
            e.Property(x => x.RecapSpecHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.ExperienceKey).HasMaxLength(60).IsRequired();
            e.Property(x => x.ProviderJobId).HasMaxLength(120).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(40).IsRequired();
            e.Property(x => x.Model).HasMaxLength(80).IsRequired();
            e.Property(x => x.PromptHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.AssetKey).HasMaxLength(120).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(40).IsRequired();
            e.Property(x => x.FailureReason).HasMaxLength(60).IsRequired();

            // BİR SEÇİM DƏSTİ → BİR PULLU VİDEO. Eyni seçimlərlə oynayan ikinci
            // uşaq da, eyni run-ı təkrar açan uşaq da keşdən gəlir.
            e.HasIndex(x => x.RecapSpecHash).IsUnique();

            // Sahiblik yoxlaması və gündəlik kvota bu oxu oxuyur.
            e.HasIndex(x => new { x.ChildProfileId, x.RequestedAt });

            // Bir run-ın recap-ı endpoint tərəfindən run id ilə tapılır.
            e.HasIndex(x => x.ExperienceRunId);

            e.HasOne(x => x.ChildProfile)
                .WithMany()
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TeamMissionMember>(e =>
        {
            e.HasIndex(x => new { x.TeamMissionId, x.ChildProfileId }).IsUnique();
            e.HasOne(x => x.TeamMission)
                .WithMany(m => m.Members)
                .HasForeignKey(x => x.TeamMissionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ChildProfile)
                .WithMany()
                .HasForeignKey(x => x.ChildProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
