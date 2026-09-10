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
