using PetPal.Shared.Enums;

namespace PetPal.Api.Missions;

/// <summary>
/// Missiya irəliləyişini yeniləyən kiçik interfeys. Learning/Pets/Discovery
/// slice-ları bir-birindən asılı olmasın deyə ayrıca saxlanılır.
/// Metodlar <c>SaveChanges</c> çağırmır — çağıran əməliyyat özü yazır.
/// </summary>
public interface IMissionProgressTracker
{
    Task TrackAsync(Guid childId, MissionType type, SkillArea? skill, int amount, CancellationToken ct = default);

    /// <summary>Mütləq dəyər təyin edir (streak kimi "sayğac deyil, vəziyyət" olan hallar üçün).</summary>
    Task SetProgressAsync(Guid childId, MissionType type, int value, CancellationToken ct = default);
}
