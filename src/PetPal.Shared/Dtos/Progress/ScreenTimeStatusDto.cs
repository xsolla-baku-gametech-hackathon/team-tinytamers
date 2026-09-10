using PetPal.Shared.Enums;

namespace PetPal.Shared.Dtos.Progress;

/// <summary>
/// Uşağın hazırda yeni fəaliyyət başlada bilib-bilməməsi.
/// App bunu ana ekranda alır və bloklanmış hallarda uyğun ekranı göstərir.
/// </summary>
public class ScreenTimeStatusDto
{
    public ScreenTimeState State { get; set; }
    public bool IsBlocked => State != ScreenTimeState.Allowed;

    public int MinutesUsed { get; set; }
    public int MinutesLimit { get; set; }
    public int MinutesLeft => Math.Max(0, MinutesLimit - MinutesUsed);

    public int BedtimeStartHour { get; set; }
    public int BedtimeEndHour { get; set; }

    /// <summary>Uşağa göstəriləcək mesaj (bloklanmayıbsa boş).</summary>
    public string Message { get; set; } = string.Empty;
}
