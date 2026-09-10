namespace PetPal.Api.Ai;

/// <summary>
/// Pet-in ekranda dediyi replikanı hazırlayır.
///
/// İki implementasiya var: qayda əsaslı (standart) və AI. Çağıran tərəf hansının
/// işlədiyini bilmir — AI sıradan çıxsa belə interfeys həmişə mətn qaytarır.
/// </summary>
public interface IPetVoiceGenerator
{
    Task<string> IdleAsync(PetVoiceContext context, CancellationToken ct = default);
}
