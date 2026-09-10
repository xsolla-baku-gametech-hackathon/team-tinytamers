using PetPal.Api.Common;
using PetPal.Shared.Dtos.Pets;

namespace PetPal.Api.Ai;

public interface IPetChatService
{
    /// <summary>Söhbət otağı açılanda: açıqdırmı, neçə mesaj qalıb, son replikalar.</summary>
    Task<ServiceResult<PetChatStateDto>> GetStateAsync(Guid childId, CancellationToken ct = default);

    /// <summary>
    /// Uşağın mesajını qəbul edir və pet-in cavabını qaytarır.
    /// Model əlçatan olmasa da uğurlu nəticə qaytarır — cavab qayda əsaslı olur.
    /// </summary>
    Task<ServiceResult<PetChatReplyDto>> SendAsync(Guid childId, PetChatRequest request, CancellationToken ct = default);
}
