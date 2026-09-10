using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Pets;
using PetPal.Api.Security;
using PetPal.Shared.Dtos.Auth;
using PetPal.Shared.Enums;

namespace PetPal.Api.Auth;

public class AuthService : IAuthService
{
    private const int MaxChildrenPerParent = 6;

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public AuthService(AppDbContext db, UserManager<ApplicationUser> userManager, ITokenService tokenService)
    {
        _db = db;
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<ServiceResult<AuthResponse>> RegisterParentAsync(RegisterParentRequest request, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return ServiceResult<AuthResponse>.Conflict(Localized.T("Bu e-poçt artıq qeydiyyatdan keçib.", "This email is already registered."));

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName.Trim(),
            EmailConfirmed = true
        };

        var created = await _userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return ServiceResult<AuthResponse>.Fail(string.Join(" ", created.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, DbInitializer.ParentRole);

        return ServiceResult<AuthResponse>.Ok(await BuildResponseAsync(user, ProfileKind.Parent, null, ct));
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return ServiceResult<AuthResponse>.Fail(Localized.T("E-poçt və ya şifrə yanlışdır.", "The email or password is wrong."));

        return ServiceResult<AuthResponse>.Ok(await BuildResponseAsync(user, ProfileKind.Parent, null, ct));
    }

    public async Task<ServiceResult<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var token = await _tokenService.FindActiveRefreshTokenAsync(request.RefreshToken, ct);
        if (token is null)
            return ServiceResult<AuthResponse>.Forbidden(Localized.T("Sessiya bitib. Yenidən daxil olun.", "Your session has ended. Please sign in again."));

        // Rotasiya: köhnə token dərhal ləğv olunur, yenisi verilir.
        await _tokenService.RevokeAsync(token, ct);

        var kind = token.ChildProfileId.HasValue ? ProfileKind.Child : ProfileKind.Parent;
        return ServiceResult<AuthResponse>.Ok(await BuildResponseAsync(token.User, kind, token.ChildProfileId, ct));
    }

    public async Task<ServiceResult<bool>> LogoutAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var token = await _tokenService.FindActiveRefreshTokenAsync(request.RefreshToken, ct);
        if (token is not null)
            await _tokenService.RevokeAsync(token, ct);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<ChildSummaryDto>> CreateChildAsync(Guid parentId, CreateChildRequest request, CancellationToken ct = default)
    {
        var parent = await _db.Users.FirstOrDefaultAsync(u => u.Id == parentId, ct);
        if (parent is null)
            return ServiceResult<ChildSummaryDto>.NotFound(Localized.T("Valideyn hesabı tapılmadı.", "Parent account not found."));

        var childCount = await _db.ChildProfiles.CountAsync(c => c.ParentUserId == parentId, ct);
        if (childCount >= MaxChildrenPerParent)
            return ServiceResult<ChildSummaryDto>.Conflict(Localized.T(
                $"Bir hesaba ən çoxu {MaxChildrenPerParent} uşaq profili əlavə edilə bilər.",
                $"An account can hold at most {MaxChildrenPerParent} child profiles."));

        var child = new ChildProfile
        {
            ParentUserId = parentId,
            DisplayName = request.DisplayName.Trim(),
            AvatarKey = string.IsNullOrWhiteSpace(request.AvatarKey) ? "avatar-fox" : request.AvatarKey,
            Age = request.Age,
            PinHash = PinHasher.Hash(request.Pin),
            FriendCode = await CreateUniqueFriendCodeAsync(ct),
            LanguageCode = Localized.Normalize(request.LanguageCode),
            UtcOffsetMinutes = request.UtcOffsetMinutes,
            DailyGoalTarget = 5
        };

        child.Pet = new Pet
        {
            Name = request.PetName.Trim(),
            Species = string.IsNullOrWhiteSpace(request.PetSpecies) ? "fox" : request.PetSpecies
        };

        foreach (var skill in Enum.GetValues<SkillArea>())
            child.SkillMasteries.Add(new SkillMastery { Skill = skill });

        _db.ChildProfiles.Add(child);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<ChildSummaryDto>.Ok(ToSummary(child));
    }

    public async Task<ServiceResult<List<ChildSummaryDto>>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
    {
        var children = await _db.ChildProfiles
            .Include(c => c.Pet)
            .Where(c => c.ParentUserId == parentId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        return ServiceResult<List<ChildSummaryDto>>.Ok(children.Select(ToSummary).ToList());
    }

    public async Task<ServiceResult<AuthResponse>> ChildLoginAsync(Guid parentId, ChildLoginRequest request, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .Include(c => c.ParentUser)
            .Include(c => c.Pet)
            .FirstOrDefaultAsync(c => c.Id == request.ChildId, ct);

        if (child is null || child.ParentUserId != parentId)
            return ServiceResult<AuthResponse>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        if (!PinHasher.Verify(request.Pin, child.PinHash))
            return ServiceResult<AuthResponse>.Fail(Localized.T("PIN yanlışdır.", "That PIN is wrong."));

        return ServiceResult<AuthResponse>.Ok(await BuildResponseAsync(child.ParentUser, ProfileKind.Child, child.Id, ct));
    }

    private async Task<AuthResponse> BuildResponseAsync(ApplicationUser user, ProfileKind kind, Guid? childId, CancellationToken ct)
    {
        var (accessToken, expiresAt) = _tokenService.CreateAccessToken(user, kind, childId);
        var refreshToken = await _tokenService.IssueRefreshTokenAsync(user.Id, childId, ct);

        var children = await _db.ChildProfiles
            .Include(c => c.Pet)
            .Where(c => c.ParentUserId == user.Id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        // Uşaq sessiyasında yalnız öz profili qaytarılır — digər uşaqların məlumatı sızmasın.
        if (kind == ProfileKind.Child && childId.HasValue)
            children = children.Where(c => c.Id == childId.Value).ToList();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = expiresAt,
            ProfileKind = kind,
            ParentId = user.Id,
            ParentDisplayName = user.DisplayName,
            ChildId = childId,
            Children = children.Select(ToSummary).ToList(),

            // İnterfeysin dili: uşaq sessiyasında öz dili, valideyn sessiyasında
            // birinci uşağın dili. Beləliklə ailə ingilis dilində işləyirsə,
            // valideyn ekranları da ingiliscə açılır.
            LanguageCode = Localized.Normalize(
                (childId.HasValue
                    ? children.FirstOrDefault(c => c.Id == childId.Value)
                    : children.FirstOrDefault())?.LanguageCode)
        };
    }

    private async Task<string> CreateUniqueFriendCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = FriendCodeGenerator.Create();
            if (!await _db.ChildProfiles.AnyAsync(c => c.FriendCode == code, ct))
                return code;
        }

        throw new InvalidOperationException("Unikal dost kodu yaradıla bilmədi.");
    }

    private static ChildSummaryDto ToSummary(ChildProfile child) => new()
    {
        Id = child.Id,
        DisplayName = child.DisplayName,
        AvatarKey = child.AvatarKey,
        Age = child.Age,
        Stars = child.Stars,
        Gems = child.Gems,
        Level = child.Pet?.Level ?? 1,
        PetName = child.Pet?.Name ?? string.Empty,
        PetStage = child.Pet is null ? PetStage.Egg : PetProgression.StageFor(child.Pet),
        LanguageCode = Localized.Normalize(child.LanguageCode)
    };
}
