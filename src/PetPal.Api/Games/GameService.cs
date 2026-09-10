using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Missions;
using PetPal.Api.PetBrain;
using PetPal.Api.Pets;
using PetPal.Api.Progress;
using PetPal.Api.Rewards;
using PetPal.Shared.Dtos.Games;
using PetPal.Shared.Enums;

namespace PetPal.Api.Games;

public class GameService : IGameService
{
    private readonly AppDbContext _db;
    private readonly IPetService _pets;
    private readonly IRewardService _rewards;
    private readonly IMissionProgressTracker _missions;
    private readonly IDailyGoalService _dailyGoals;
    private readonly IBehaviorTracker _behavior;
    private readonly TimeProvider _clock;
    private readonly ScreenTimeOptions _screenTime;

    public GameService(
        AppDbContext db,
        IPetService pets,
        IRewardService rewards,
        IMissionProgressTracker missions,
        IDailyGoalService dailyGoals,
        IBehaviorTracker behavior,
        TimeProvider clock,
        IOptions<ScreenTimeOptions> screenTime)
    {
        _db = db;
        _pets = pets;
        _rewards = rewards;
        _missions = missions;
        _dailyGoals = dailyGoals;
        _behavior = behavior;
        _clock = clock;
        _screenTime = screenTime.Value;
    }

    public async Task<ServiceResult<List<GameCatalogItemDto>>> GetCatalogAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        return child is null
            ? ServiceResult<List<GameCatalogItemDto>>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."))
            : ServiceResult<List<GameCatalogItemDto>>.Ok(GameCatalog.For(child.LanguageCode, child.UnlockedGames));
    }

    /// <summary>
    /// Oyunu ulduzla açır. Açılmış oyun həmişəlik qalır — uşaq eyni şeyə iki dəfə
    /// ödəmir.
    /// </summary>
    public async Task<ServiceResult<UnlockGameResultDto>> UnlockAsync(
        Guid childId, UnlockGameRequest request, CancellationToken ct = default)
    {
        if (!GameCatalog.IsKnown(request.GameKey))
            return ServiceResult<UnlockGameResultDto>.NotFound(Localized.T("Belə oyun yoxdur.", "There is no such game."));

        var child = await _db.ChildProfiles.FirstOrDefaultAsync(c => c.Id == childId, ct);
        if (child is null)
            return ServiceResult<UnlockGameResultDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var cost = GameCatalog.UnlockCost(request.GameKey);
        if (cost == 0 || child.UnlockedGames.Contains(request.GameKey))
            return ServiceResult<UnlockGameResultDto>.Fail(Localized.T("Bu oyun artıq açıqdır.", "This game is already unlocked."));

        if (child.Stars < cost)
            return ServiceResult<UnlockGameResultDto>.Fail($"Bu oyunu açmaq üçün daha {cost - child.Stars} ulduz lazımdır.");

        await _rewards.SpendStarsAsync(child, cost, $"Oyun açıldı — {request.GameKey}", ct);
        child.UnlockedGames.Add(request.GameKey);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<UnlockGameResultDto>.Ok(new UnlockGameResultDto
        {
            GameKey = request.GameKey,
            StarsSpent = cost,
            Catalog = GameCatalog.For(child.LanguageCode, child.UnlockedGames)
        });
    }

    public async Task<ServiceResult<GameResultDto>> SubmitResultAsync(
        Guid childId, SubmitGameResultRequest request, CancellationToken ct = default)
    {
        if (!GameCatalog.IsKnown(request.GameKey))
            return ServiceResult<GameResultDto>.NotFound(Localized.T("Belə oyun yoxdur.", "There is no such game."));

        var child = await _db.ChildProfiles
            .Include(c => c.Pet)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child?.Pet is null)
            return ServiceResult<GameResultDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var now = _clock.GetUtcNow().UtcDateTime;
        var goal = await _dailyGoals.GetOrCreateTodayAsync(child, ct);

        // Yuxu rejimi / gündəlik limit yalnız YENİ fəaliyyəti bloklayır.
        var screenTime = ScreenTimeGuard.Evaluate(child, goal, now, _screenTime.Enforced);
        if (screenTime != ScreenTimeState.Allowed)
        {
            await _db.SaveChangesAsync(ct);
            return ServiceResult<GameResultDto>.Forbidden(
                ScreenTimeGuard.MessageFor(screenTime, child.LanguageCode, child));
        }

        var rewardedToday = await _db.GameResults
            .CountAsync(g => g.ChildProfileId == childId && g.CreatedAt >= now.Date && g.StarsEarned > 0, ct);

        var capped = rewardedToday >= GameCatalog.RewardedGamesPerDay;
        var (stars, xp) = capped ? (0, 2) : GameCatalog.Reward(request.Score);

        PetProgression.Refresh(child.Pet, now);
        PetProgression.AddXp(child.Pet, xp);

        // Oyun pet-i sevindirir, amma enerjisini də azaldır — care dövrəsi ilə bağlıdır.
        child.Pet.Happiness = PetProgression.Clamp(child.Pet.Happiness + 8);
        child.Pet.Energy = PetProgression.Clamp(child.Pet.Energy - 4);

        _db.GameResults.Add(new GameResult
        {
            ChildProfileId = childId,
            GameKey = request.GameKey,
            Score = Math.Clamp(request.Score, 0, 100),
            DurationMs = request.DurationMs,
            StarsEarned = stars,
            XpEarned = xp,
            CreatedAt = now
        });

        if (stars > 0)
            await _rewards.GrantStarsAsync(child, stars, $"Mini oyun: {request.GameKey}", ct);

        goal.MinutesSpent += Math.Clamp((int)Math.Round(request.DurationMs / 60_000.0), 0, 30);

        await _missions.TrackAsync(childId, MissionType.CareForPet, null, 0, ct);

        // Mini oyun Pet Brain üçün "şən oyun üslubu"nun zəif işarəsidir.
        //
        // Oyunun AİLƏSİ də sayılır (yaddaş/ardıcıllıq, hərəkət, sərbəst oyun),
        // amma bir pillə daha zəif: mini oyun mövzu seçimi deyil — uşaq orada
        // "kosmos" yox, "əylən" seçir (bax MiniGameFamilies).
        await _behavior.TrackAsync(
            childId,
            PetBrainEventType.MiniGameCompleted,
            new PetBrainEventData(request.GameKey, $"score:{Math.Clamp(request.Score, 0, 100)}",
                ProfileLearningRules.ForMiniGame(request.GameKey)),
            null,
            ct);

        await _db.SaveChangesAsync(ct);
        await _rewards.EvaluateBadgesAsync(childId, ct);

        return ServiceResult<GameResultDto>.Ok(new GameResultDto
        {
            GameKey = request.GameKey,
            Score = Math.Clamp(request.Score, 0, 100),
            StarsEarned = stars,
            XpEarned = xp,
            RewardCapped = capped,
            Message = capped
                ? GameCatalog.RewardCappedMessage(child.LanguageCode)
                : GameCatalog.ResultMessage(child.LanguageCode, request.Score, child.Pet.Name),
            Pet = _pets.ToDto(child.Pet, child.DisplayName, child.LanguageCode)
        });
    }
}
