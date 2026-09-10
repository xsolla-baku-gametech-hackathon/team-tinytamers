using PetPal.Api.Entities;
using PetPal.Shared.Dtos.Rewards;

namespace PetPal.Api.Rewards;

/// <summary>
/// Ulduz/gem balansının tək giriş nöqtəsi. Grant/Spend metodları
/// <c>SaveChanges</c> çağırmır — çağıran əməliyyat öz tranzaksiyasını özü bağlayır.
/// </summary>
public interface IRewardService
{
    Task GrantStarsAsync(ChildProfile child, int amount, string reason, CancellationToken ct = default);

    Task GrantGemsAsync(ChildProfile child, int amount, string reason, CancellationToken ct = default);

    Task<bool> SpendStarsAsync(ChildProfile child, int amount, string reason, CancellationToken ct = default);

    Task<WalletDto> GetWalletAsync(Guid childId, CancellationToken ct = default);

    Task<List<RewardEntryDto>> GetLedgerAsync(Guid childId, int take = 50, CancellationToken ct = default);

    Task<List<BadgeDto>> GetBadgesAsync(Guid childId, CancellationToken ct = default);

    /// <summary>Bütün nişan qaydalarını yoxlayır və yeni qazanılanları qaytarır.</summary>
    Task<List<BadgeDto>> EvaluateBadgesAsync(Guid childId, CancellationToken ct = default);
}
