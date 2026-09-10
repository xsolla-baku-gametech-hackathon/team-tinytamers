using Microsoft.EntityFrameworkCore;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Missions;
using PetPal.Api.PetBrain;
using PetPal.Api.Rewards;
using PetPal.Shared.Dtos.Pets;
using PetPal.Shared.Enums;

namespace PetPal.Api.Pets;

public class PetService : IPetService
{
    private readonly AppDbContext _db;
    private readonly IRewardService _rewards;
    private readonly IMissionProgressTracker _missions;
    private readonly IBehaviorTracker _behavior;
    private readonly TimeProvider _clock;

    public PetService(
        AppDbContext db,
        IRewardService rewards,
        IMissionProgressTracker missions,
        IBehaviorTracker behavior,
        TimeProvider clock)
    {
        _db = db;
        _rewards = rewards;
        _missions = missions;
        _behavior = behavior;
        _clock = clock;
    }

    public async Task<ServiceResult<PetDto>> GetAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return ServiceResult<PetDto>.NotFound(Localized.T("Pet tapılmadı.", "Pet not found."));

        PetProgression.Refresh(child.Pet, _clock.GetUtcNow().UtcDateTime);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<PetDto>.Ok(ToDto(child.Pet, child.DisplayName, child.LanguageCode));
    }

    public async Task<ServiceResult<CarePetResultDto>> CareAsync(Guid childId, CarePetRequest request, CancellationToken ct = default)
    {
        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return ServiceResult<CarePetResultDto>.NotFound(Localized.T("Pet tapılmadı.", "Pet not found."));

        var pet = child.Pet;
        var now = _clock.GetUtcNow().UtcDateTime;
        PetProgression.ApplyDecay(pet, now);

        var starsSpent = 0;
        var language = child.LanguageCode;
        PetFood? servedFood = null;

        // Yumurtaya qulluq etmək olmaz — əvvəlcə açılmalıdır.
        if (pet.HatchedAt is null)
            return ServiceResult<CarePetResultDto>.Fail(PetVoice.StillAnEgg(language));

        switch (request.Action)
        {
            case CareAction.Feed:
                // Naməlum kod sükutla başqa yeməyə çevrilmir — qiymət və təsir
                // uşağın gördüyü ilə eyni olmalıdır.
                var food = PetFoods.Resolve(request.Food);
                if (food is null)
                    return ServiceResult<CarePetResultDto>.Fail(PetVoice.UnknownFood(language));

                if (pet.Fullness >= 95)
                    return Refused(language, request.Action, pet.Name);
                if (child.Stars < food.StarCost)
                    return ServiceResult<CarePetResultDto>.Fail(PetVoice.NotEnoughStars(language, food.StarCost));

                starsSpent = food.StarCost;
                servedFood = food;

                pet.Fullness = PetProgression.Clamp(pet.Fullness + food.Fullness);
                pet.Happiness = PetProgression.Clamp(pet.Happiness + food.Happiness);

                if (food.Energy != 0)
                    pet.Energy = PetProgression.Clamp(pet.Energy + food.Energy);

                // Şirniyyat pet-i bulaşdırır: seçimin görünən nəticəsi olsun deyə
                // sonra çimizdirmə lazım gəlir.
                if (food.Cleanliness != 0)
                    pet.Cleanliness = PetProgression.Clamp(pet.Cleanliness + food.Cleanliness);
                break;

            case CareAction.Play:
                if (pet.Energy < 15)
                    return Refused(language, request.Action, pet.Name);

                pet.Happiness = PetProgression.Clamp(pet.Happiness + 25);
                pet.Energy = PetProgression.Clamp(pet.Energy - 10);
                pet.Cleanliness = PetProgression.Clamp(pet.Cleanliness - 5);
                break;

            case CareAction.Clean:
                if (pet.Cleanliness >= 95)
                    return Refused(language, request.Action, pet.Name);

                pet.Cleanliness = PetProgression.Clamp(pet.Cleanliness + 40);
                pet.Happiness = PetProgression.Clamp(pet.Happiness + 5);
                break;

            case CareAction.Sleep:
                if (pet.Energy >= 95)
                    return Refused(language, request.Action, pet.Name);

                pet.Energy = PetProgression.Clamp(pet.Energy + 40);
                pet.Happiness = PetProgression.Clamp(pet.Happiness + 5);
                pet.LastSleptAt = now;
                break;

            default:
                return ServiceResult<CarePetResultDto>.Fail(Localized.T("Bu qulluq əməliyyatı dəstəklənmir.", "That care action is not supported."));
        }

        var message = servedFood is null
            ? PetVoice.CareResult(language, request.Action, pet.Name)
            : PetVoice.FedFood(language, PetFoods.NameOf(servedFood, language), servedFood.IsTreat, pet.Name);

        const int careXp = 5;
        PetProgression.AddXp(pet, careXp);
        child.CareActionCount++;

        // Qulluqdan sonra yoxlanılır: bəzi əşyalar məhz xoşbəxtlik həddi ilə açılır,
        // yəni "oyna" düyməsi elə həmin an mükafat verə bilər.
        var newlyUnlocked = PetAccessories.UnlockEarned(pet);

        if (starsSpent > 0)
        {
            // Valideyn panelində "Feed — Max" yox, alınan yeməyin adı görünsün.
            var reason = servedFood is null
                ? $"{request.Action} — {pet.Name}"
                : $"{PetFoods.NameOf(servedFood, language)} — {pet.Name}";

            await _rewards.SpendStarsAsync(child, starsSpent, reason, ct);
        }

        await _missions.TrackAsync(childId, MissionType.CareForPet, null, 1, ct);

        // Qulluq bağı artırır — amma GÜNDƏ ÜÇ DƏFƏ. Limitsiz olsaydı, uşaq
        // düyməni basmaqla bağı doldurardı və "birlikdə yaşanan an" ölçüsü
        // sadəcə klik sayğacına çevrilərdi.
        var caresToday = await _db.BehaviorEvents.CountAsync(
            e => e.ChildProfileId == childId
                 && e.Type == PetBrainEventType.PetCared
                 && e.OccurredAt >= now.Date, ct);

        BondRules.Grant(pet, BondRules.ForCare(caresToday));

        await _behavior.TrackAsync(
            childId,
            PetBrainEventType.PetCared,
            new PetBrainEventData(request.Action.ToString(), string.Empty, ProfileLearningRules.ForCare()),
            null,
            ct);

        await _db.SaveChangesAsync(ct);

        return ServiceResult<CarePetResultDto>.Ok(new CarePetResultDto
        {
            Pet = ToDto(pet, child.DisplayName, language),
            Message = message,
            XpEarned = careXp,
            StarsSpent = starsSpent,
            NewAccessories = [.. PetAccessories.Describe(pet, language).Where(a => newlyUnlocked.Contains(a.Code))]
        });
    }

    /// <summary>
    /// Yumurtanı ulduzla açır. Oyunun ilk əsl mübadiləsidir: uşaq dərs həll edib
    /// topladığı ulduzu sərf edir və əvəzində peti alır.
    /// </summary>
    public async Task<ServiceResult<HatchPetResultDto>> HatchAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return ServiceResult<HatchPetResultDto>.NotFound(Localized.T("Pet tapılmadı.", "Pet not found."));

        var pet = child.Pet;
        var language = child.LanguageCode;

        if (pet.HatchedAt is not null)
            return ServiceResult<HatchPetResultDto>.Fail(PetVoice.AlreadyHatched(language, pet.Name));

        if (child.Stars < PetProgression.HatchStarCost)
            return ServiceResult<HatchPetResultDto>.Fail(
                PetVoice.NotEnoughStarsToHatch(language, PetProgression.HatchStarCost - child.Stars));

        var now = _clock.GetUtcNow().UtcDateTime;
        await _rewards.SpendStarsAsync(child, PetProgression.HatchStarCost, $"Yumurta açıldı — {pet.Name}", ct);

        pet.HatchedAt = now;
        pet.LastDecayAt = now;

        // Yumurtadan çıxan pet dincdir və toxdur — ilk təəssürat müsbət olmalıdır.
        pet.Happiness = 90;
        pet.Energy = 90;
        pet.Fullness = 80;
        pet.Cleanliness = 100;

        PetAccessories.UnlockEarned(pet);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<HatchPetResultDto>.Ok(new HatchPetResultDto
        {
            Pet = ToDto(pet, child.DisplayName, language),
            Message = PetVoice.JustHatched(language, pet.Name, child.DisplayName),
            StarsSpent = PetProgression.HatchStarCost
        });
    }

    /// <summary>
    /// Pet-in görünüşünü yeniləyir. Sorğu tam siyahıdır: içində olmayan əşya
    /// çıxarılır, boş siyahı isə "heç nə taxma" deməkdir.
    /// </summary>
    public async Task<ServiceResult<PetDto>> EquipAccessoriesAsync(
        Guid childId, EquipAccessoriesRequest request, CancellationToken ct = default)
    {
        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return ServiceResult<PetDto>.NotFound(Localized.T("Pet tapılmadı.", "Pet not found."));

        var pet = child.Pet;
        var language = child.LanguageCode;

        if (pet.HatchedAt is null)
            return ServiceResult<PetDto>.Fail(PetVoice.StillAnEgg(language));

        if (!PetAccessories.TryEquip(pet, request.Codes))
            return ServiceResult<PetDto>.Fail(PetVoice.AccessoryLocked(language));

        // Görünüş qurmaq yaradıcı üslubun zəif işarəsidir.
        await _behavior.TrackAsync(
            childId,
            PetBrainEventType.AccessoryEquipped,
            new PetBrainEventData("closet", $"count:{pet.EquippedAccessories.Count}",
                ProfileLearningRules.ForAccessoryEquipped()),
            null,
            ct);

        await _db.SaveChangesAsync(ct);

        return ServiceResult<PetDto>.Ok(ToDto(pet, child.DisplayName, language));
    }

    public async Task<ServiceResult<List<PetFoodDto>>> GetFoodsAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles.FirstOrDefaultAsync(c => c.Id == childId, ct);
        if (child is null)
            return ServiceResult<List<PetFoodDto>>.NotFound("Profil tapılmadı.");

        return ServiceResult<List<PetFoodDto>>.Ok(PetFoods.Describe(child.LanguageCode));
    }

    public async Task<ServiceResult<PetDto>> RenameAsync(Guid childId, RenamePetRequest request, CancellationToken ct = default)
    {
        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return ServiceResult<PetDto>.NotFound(Localized.T("Pet tapılmadı.", "Pet not found."));

        child.Pet.Name = request.Name.Trim();
        await _db.SaveChangesAsync(ct);

        return ServiceResult<PetDto>.Ok(ToDto(child.Pet, child.DisplayName, child.LanguageCode));
    }

    public async Task<ServiceResult<PetDto>> ChangeSpeciesAsync(
        Guid childId, ChangeSpeciesRequest request, CancellationToken ct = default)
    {
        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return ServiceResult<PetDto>.NotFound(Localized.T("Pet tapılmadı.", "Pet not found."));

        // Yalnız görünüş dəyişir: yaş, XP, statlar və açılmış əşyalar qalır.
        child.Pet.Species = request.Species;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<PetDto>.Ok(ToDto(child.Pet, child.DisplayName, child.LanguageCode));
    }

    public async Task<Pet?> LoadCurrentAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await LoadChildAsync(childId, ct);
        if (child?.Pet is null)
            return null;

        PetProgression.Refresh(child.Pet, _clock.GetUtcNow().UtcDateTime);
        return child.Pet;
    }

    public PetDto ToDto(Pet pet, string childName, string languageCode, string? message = null) => new()
    {
        Id = pet.Id,
        Name = pet.Name,
        Species = pet.Species,
        Stage = PetProgression.StageFor(pet),
        IsHatched = pet.HatchedAt is not null,
        HatchStarCost = PetProgression.HatchStarCost,
        Level = pet.Level,
        Xp = pet.Xp,
        XpToNextLevel = PetProgression.XpToNextLevel(pet.Level),
        Happiness = pet.Happiness,
        Energy = pet.Energy,
        Fullness = pet.Fullness,
        Cleanliness = pet.Cleanliness,
        Bond = PetBrain.BondRules.Clamp(pet.Bond),
        Mood = PetProgression.MoodFor(pet),
        Message = message ?? PetVoice.Idle(languageCode, PetProgression.MoodFor(pet), childName, pet.Name),
        UnlockedAccessories = pet.UnlockedAccessories.ToList(),
        EquippedAccessories = pet.EquippedAccessories.ToList(),
        Accessories = PetAccessories.Describe(pet, languageCode)
    };

    private static ServiceResult<CarePetResultDto> Refused(string language, CareAction action, string petName) =>
        ServiceResult<CarePetResultDto>.Fail(PetVoice.CareRefused(language, action, petName));

    private Task<ChildProfile?> LoadChildAsync(Guid childId, CancellationToken ct) =>
        _db.ChildProfiles.Include(c => c.Pet).FirstOrDefaultAsync(c => c.Id == childId, ct);
}
