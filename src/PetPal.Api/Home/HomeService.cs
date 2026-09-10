using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using PetPal.Api.Ai;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Missions;
using PetPal.Api.PetBrain;
using PetPal.Api.Pets;
using PetPal.Api.Progress;
using PetPal.Api.Rewards;
using PetPal.Shared.Dtos.Home;
using PetPal.Shared.Dtos.Progress;

namespace PetPal.Api.Home;

/// <summary>
/// Ana ekranın bütün vəziyyətini bir sorğuda yığır — mobil şəbəkədə
/// açılış sürəti üçün ən vacib optimallaşdırma.
/// </summary>
public class HomeService : IHomeService
{
    private readonly AppDbContext _db;
    private readonly IPetService _pets;
    private readonly IRewardService _rewards;
    private readonly IProgressService _progress;
    private readonly IMissionService _missions;
    private readonly IDailyGoalService _dailyGoals;
    private readonly IPetVoiceGenerator _voice;
    private readonly IPetBrainService _petBrain;
    private readonly TimeProvider _clock;
    private readonly ScreenTimeOptions _screenTime;

    public HomeService(
        AppDbContext db,
        IPetService pets,
        IRewardService rewards,
        IProgressService progress,
        IMissionService missions,
        IDailyGoalService dailyGoals,
        IPetVoiceGenerator voice,
        IPetBrainService petBrain,
        TimeProvider clock,
        IOptions<ScreenTimeOptions> screenTime)
    {
        _db = db;
        _pets = pets;
        _rewards = rewards;
        _progress = progress;
        _missions = missions;
        _dailyGoals = dailyGoals;
        _voice = voice;
        _petBrain = petBrain;
        _clock = clock;
        _screenTime = screenTime.Value;
    }

    public async Task<ServiceResult<HomeStateDto>> GetAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .Include(c => c.Pet)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child?.Pet is null)
            return ServiceResult<HomeStateDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var now = _clock.GetUtcNow().UtcDateTime;
        PetProgression.Refresh(child.Pet, now);

        var todayGoal = await _dailyGoals.GetOrCreateTodayAsync(child, ct);
        await _db.SaveChangesAsync(ct);

        // Salamlama replikası yeganə yerdir ki, AI işə düşür. Qulluq nəticələri
        // ("yem verdin, sağ ol") qəsdən qayda əsaslı qalır — onlar baş verən
        // hadisəni dəqiq təsvir etməlidir, model isə onu təhrif edə bilər.
        var petMessage = await _voice.IdleAsync(new PetVoiceContext(
            Language: child.LanguageCode,
            ChildName: child.DisplayName,
            PetName: child.Pet.Name,
            Mood: PetProgression.MoodFor(child.Pet),
            Level: child.Pet.Level,
            Happiness: child.Pet.Happiness,
            Energy: child.Pet.Energy,
            Fullness: child.Pet.Fullness,
            Cleanliness: child.Pet.Cleanliness,
            StreakDays: child.StreakDays,
            GoalCompleted: todayGoal.Completed,
            GoalTarget: todayGoal.Target), ct);

        var petDto = _pets.ToDto(child.Pet, child.DisplayName, child.LanguageCode, petMessage);

        var screenTimeState = ScreenTimeGuard.Evaluate(child, todayGoal, now, _screenTime.Enforced);
        var screenTime = new ScreenTimeStatusDto
        {
            State = screenTimeState,
            MinutesUsed = todayGoal.MinutesSpent,
            MinutesLimit = child.DailyMinutesLimit,
            BedtimeStartHour = child.BedtimeStartHour,
            BedtimeEndHour = child.BedtimeEndHour,
            Message = ScreenTimeGuard.MessageFor(screenTimeState, child.LanguageCode, child)
        };

        var today = DateOnly.FromDateTime(now);
        var weekStart = today.AddDays(-6);
        var week = await _db.DailyGoals
            .AsNoTracking()
            .Where(g => g.ChildProfileId == childId && g.Date >= weekStart && g.Date <= today)
            .Select(g => new { g.Completed, g.Target })
            .ToListAsync(ct);

        var score = WorldStateCalculator.EngagementScore(week.Select(g => (g.Completed, g.Completed >= g.Target)));

        return ServiceResult<HomeStateDto>.Ok(new HomeStateDto
        {
            ChildId = child.Id,
            ChildDisplayName = child.DisplayName,
            AvatarKey = child.AvatarKey,
            Pet = petDto,
            Wallet = await _rewards.GetWalletAsync(childId, ct),
            DailyGoal = await _progress.GetTodayGoalAsync(childId, ct),
            Weather = WorldStateCalculator.WeatherFor(score),
            ScreenTime = screenTime,
            PetMessage = petDto.Message,
            ChatEnabled = child.ChatEnabled,
            FeaturedMissions = await _missions.GetFeaturedAsync(childId, 3, ct),

            // Pet Brain təklifi ana ekranın ÖZ sorğusunda gəlir — açılışa ikinci
            // şəbəkə gedişi əlavə olunmur (bax HomeStateDto.PetBrain).
            PetBrain = await _petBrain.GetHomeChipAsync(childId, ct)
        });
    }
}
