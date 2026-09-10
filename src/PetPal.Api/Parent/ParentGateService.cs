using Microsoft.AspNetCore.Identity;
using PetPal.Api.Common;
using PetPal.Api.Entities;
using PetPal.Api.Security;
using PetPal.Shared.Dtos.Parent;

namespace PetPal.Api.Parent;

/// <summary>
/// Valideyn bölməsinin qapısı.
///
/// <para>Əvvəl qapı vurma sualı idi və yalnız KLİENTDƏ yoxlanılırdı. İki
/// səbəbdən işləmirdi: sual app-in özünün öyrətdiyi şeydir (Öyrən bölməsində
/// uşaq elə vurma məşq edir), və valideyn tokeni uşaq oynayarkən yaddaşda
/// qalır — yəni həmin lokal `bool` bütün valideyn API-sinin qarşısında dururdu.</para>
///
/// <para>İndi qapı PIN-dir və yoxlama SERVERDƏDİR: uşaq nə təxmin edə bilər,
/// nə də kodda cavabı görə bilər. Açılış qısa müddətlidir və yalnız uğurlu
/// PIN-dən sonra verilir.</para>
///
/// <para>PIN hash-i <see cref="ApplicationUser.ParentGatePinHash"/>-dədir —
/// sahə ilk miqrasiyadan bəri var idi, sadəcə qoşulmamışdı.</para>
/// </summary>
public interface IParentGateService
{
    Task<ServiceResult<ParentGateStatusDto>> GetStatusAsync(Guid parentId, CancellationToken ct = default);

    /// <summary>
    /// PIN qoyur və ya dəyişir. İlk dəfə qoyanda hesab PAROLU tələb olunur;
    /// dəyişəndə isə ya cari PIN, ya da parol kifayətdir — valideyn PIN-i
    /// unudursa, hesabına girişi ilə bərpa edə bilməlidir.
    /// </summary>
    Task<ServiceResult<ParentGateStatusDto>> SetPinAsync(
        Guid parentId, SetParentPinRequest request, CancellationToken ct = default);

    /// <summary>PIN-i yoxlayır. Sürət limiti endpoint səviyyəsindədir.</summary>
    Task<ServiceResult<bool>> UnlockAsync(
        Guid parentId, ParentGateUnlockRequest request, CancellationToken ct = default);
}

public class ParentGateService : IParentGateService
{
    private readonly UserManager<ApplicationUser> _users;

    public ParentGateService(UserManager<ApplicationUser> users)
    {
        _users = users;
    }

    public async Task<ServiceResult<ParentGateStatusDto>> GetStatusAsync(
        Guid parentId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(parentId.ToString());
        if (user is null)
            return ServiceResult<ParentGateStatusDto>.NotFound(Localized.T("Hesab tapılmadı.", "Account not found."));

        return ServiceResult<ParentGateStatusDto>.Ok(new ParentGateStatusDto
        {
            HasPin = !string.IsNullOrEmpty(user.ParentGatePinHash)
        });
    }

    public async Task<ServiceResult<ParentGateStatusDto>> SetPinAsync(
        Guid parentId, SetParentPinRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(parentId.ToString());
        if (user is null)
            return ServiceResult<ParentGateStatusDto>.NotFound(Localized.T("Hesab tapılmadı.", "Account not found."));

        var hasPin = !string.IsNullOrEmpty(user.ParentGatePinHash);

        // Kimliyin təsdiqi: parol həmişə işləyir, cari PIN isə yalnız PIN
        // varsa. İkisindən heç biri verilməyibsə dəyişikliyə icazə yoxdur —
        // əks halda açıq sessiyada PIN-i sadəcə üzərinə yazmaq olardı.
        var verified = false;

        if (!string.IsNullOrWhiteSpace(request.Password))
            verified = await _users.CheckPasswordAsync(user, request.Password);
        else if (hasPin && !string.IsNullOrWhiteSpace(request.CurrentPin))
            verified = PinHasher.Verify(request.CurrentPin, user.ParentGatePinHash!);

        if (!verified)
            return ServiceResult<ParentGateStatusDto>.Fail(
                Localized.T("Hesab parolu və ya cari PIN düzgün deyil.", "The account password or current PIN is not right."));

        user.ParentGatePinHash = PinHasher.Hash(request.NewPin);

        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded)
            return ServiceResult<ParentGateStatusDto>.Fail(
                Localized.T("PIN yadda saxlanılmadı.", "The PIN could not be saved."));

        return ServiceResult<ParentGateStatusDto>.Ok(new ParentGateStatusDto { HasPin = true });
    }

    public async Task<ServiceResult<bool>> UnlockAsync(
        Guid parentId, ParentGateUnlockRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(parentId.ToString());
        if (user is null)
            return ServiceResult<bool>.NotFound(Localized.T("Hesab tapılmadı.", "Account not found."));

        // PIN hələ qoyulmayıbsa qapı BAĞLI sayılır: köhnə hesablar da təhlükəsiz
        // vəziyyətdə qalır və klient istifadəçini PIN qurmağa yönləndirir.
        if (string.IsNullOrEmpty(user.ParentGatePinHash))
            return ServiceResult<bool>.Fail(
                Localized.T("Əvvəlcə valideyn PIN-i qurulmalıdır.", "A parent PIN must be set first."));

        if (!PinHasher.Verify(request.Pin, user.ParentGatePinHash))
            return ServiceResult<bool>.Fail(Localized.T("PIN düzgün deyil.", "That PIN is not right."));

        return ServiceResult<bool>.Ok(true);
    }
}
