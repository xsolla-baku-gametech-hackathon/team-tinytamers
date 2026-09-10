using PetPal.Shared.Dtos.Missions;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Dtos.Pets;
using PetPal.Shared.Dtos.Progress;
using PetPal.Shared.Dtos.Rewards;
using PetPal.Shared.Enums;

namespace PetPal.Shared.Dtos.Home;

/// <summary>
/// Ana ekranın tək çağırışla yığılmış vəziyyəti — mobil şəbəkədə hər açılışda
/// 5 ayrı sorğu göndərməmək üçün aqreqat DTO.
/// </summary>
public class HomeStateDto
{
    public Guid ChildId { get; set; }
    public string ChildDisplayName { get; set; } = string.Empty;
    public string AvatarKey { get; set; } = string.Empty;

    public PetDto Pet { get; set; } = new();
    public WalletDto Wallet { get; set; } = new();
    public DailyGoalDto DailyGoal { get; set; } = new();

    public WorldWeather Weather { get; set; }

    /// <summary>Ekran vaxtı vəziyyəti — bloklanıbsa app oyun ekranlarını bağlayır.</summary>
    public ScreenTimeStatusDto ScreenTime { get; set; } = new();

    /// <summary>Ana ekranda pet-in danışıq balonunda görünən mətn.</summary>
    public string PetMessage { get; set; } = string.Empty;

    /// <summary>
    /// Söhbət açıqdırmı. Ana ekran bunu bilməlidir: bağlıdırsa pet-in danışıq
    /// buludu toxunulan olmur və uşaq hər dəfə bağlı qapıya çatmır.
    /// </summary>
    public bool ChatEnabled { get; set; }

    /// <summary>Bu gün üçün ən aktual 3 missiya (world ekranına keçmədən görünür).</summary>
    public List<MissionDto> FeaturedMissions { get; set; } = new();

    /// <summary>
    /// Pet Brain-in növbəti macəra təklifi — ana ekrandakı üzən çip üçün.
    ///
    /// <para>Qəsdən BU aqreqatın içindədir: ayrıca sorğu olsaydı, ana ekranın
    /// açılışına ikinci şəbəkə gedişi əlavə olunardı. Özəllik bağlıdırsa və ya
    /// pet hələ yumurtadırsa boş qalır.</para>
    /// </summary>
    public PetBrainHomeChipDto? PetBrain { get; set; }
}
