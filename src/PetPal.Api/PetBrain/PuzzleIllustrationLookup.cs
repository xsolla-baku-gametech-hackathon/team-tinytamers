using PetPal.Shared.Enums;

namespace PetPal.Api.PetBrain;

/// <summary>
/// Uşağın ÖZ tapmacasının rəsm vəziyyəti. <see cref="AssetKey"/> yalnız rəsm
/// hazır olanda doludur.
/// </summary>
public sealed record PuzzleIllustrationLookup(PetBrainIllustrationStatus Status, string? AssetKey)
{
    /// <summary>Model hələ çəkir — klient bir az sonra yenidən soruşa bilər.</summary>
    public bool StillDrawing => Status == PetBrainIllustrationStatus.Pending;
}
