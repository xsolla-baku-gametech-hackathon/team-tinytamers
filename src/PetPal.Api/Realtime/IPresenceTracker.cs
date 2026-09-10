namespace PetPal.Api.Realtime;

/// <summary>
/// Kimin hazırda app-də olduğunu saxlayır.
///
/// <para><b>Yaddaşdadır, bazada deyil.</b> Presence ən çox bir neçə dəqiqəlik
/// həqiqətdir və server yenidən qalxanda onsuz da sıfırlanmalıdır — bunu
/// bazaya yazmaq hər qoşulma/qopmada yazı əməliyyatı deməkdir və heç bir
/// sual «kim keçən çərşənbə onlayn idi?» deyə soruşmur.</para>
///
/// <para><b>Məhdudiyyət:</b> yaddaş tək prosesə aiddir. API bir neçə instansda
/// işləyəndə hər instans yalnız öz qoşulmalarını görər — o zaman Redis
/// backplane lazımdır. Hazırkı deploy tək instansdır.</para>
/// </summary>
public interface IPresenceTracker
{
    /// <summary>
    /// Qoşulmanı qeyd edir. Uşağın BİRİNCİ cihazı qoşulubsa <c>true</c> qaytarır —
    /// yəni «indi onlayn oldu» xəbəri yalnız bir dəfə göndərilir.
    /// </summary>
    bool Connect(Guid childId, string connectionId);

    /// <summary>
    /// Qoşulmanı silir. Uşağın SON cihazı qopubsa <c>true</c> qaytarır.
    /// </summary>
    bool Disconnect(Guid childId, string connectionId);

    bool IsOnline(Guid childId);

    /// <summary>Verilən siyahıdan onlayn olanlar — dost siyahısını bir keçidə işarələmək üçün.</summary>
    IReadOnlySet<Guid> OnlineAmong(IEnumerable<Guid> childIds);
}
