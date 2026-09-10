using PetPal.Api.Common;
using PetPal.Shared.Dtos.Learning;

namespace PetPal.Api.Learning.Arena;

public interface IArenaService
{
    /// <summary>Arena girişinin vəziyyəti: reytinq, qalan duel sayı, hazır nəticələr.</summary>
    Task<ServiceResult<ArenaStatusDto>> GetStatusAsync(Guid childId, CancellationToken ct = default);

    /// <summary>
    /// Uyğun açıq duel tapır, tapılmasa yenisini yaradıb rəqib gözləyir.
    /// Yarımçıq duel varsa yenisi yaradılmır — uşaq həmişə başladığı dəstə qayıdır.
    ///
    /// <para>Qayıdan duel <c>WaitingOpponent</c> ola bilər: bu, "dəst hazırdır,
    /// amma hələ başlamayıb" deməkdir və cavab qəbul edilmir.</para>
    /// </summary>
    Task<ServiceResult<ArenaDuelDto>> StartDuelAsync(Guid childId, CancellationToken ct = default);

    /// <summary>
    /// Rəqib gözləyən dueli ləğv edir — uşaq axtarışdan çıxanda çağırılır.
    /// Başlamış duel ləğv OLUNMUR: rəqib onu oynayır.
    /// </summary>
    Task<ServiceResult<bool>> CancelSearchAsync(Guid childId, Guid duelId, CancellationToken ct = default);

    /// <summary>
    /// Məşq dueli: rəqib süni və açıq işarələnmişdir. Reytinq, ulduz və gündəlik
    /// hədd toxunulmaz qalır — bu, yarışın özü deyil, ona hazırlıqdır.
    /// </summary>
    Task<ServiceResult<ArenaDuelDto>> StartPracticeAsync(Guid childId, CancellationToken ct = default);

    /// <summary>
    /// Dostu birbaşa yarışa çağırır. Yaranan duel hovuza düşmür — ona yalnız
    /// çağırılan uşaq qoşula bilər.
    /// </summary>
    Task<ServiceResult<ArenaDuelDto>> ChallengeFriendAsync(
        Guid childId, Guid friendChildId, CancellationToken ct = default);

    /// <summary>Dostun çağırışını qəbul edir — dəst hər ikisi üçün eyni anda başlayır.</summary>
    Task<ServiceResult<ArenaDuelDto>> AcceptChallengeAsync(
        Guid childId, Guid duelId, CancellationToken ct = default);

    /// <summary>Çağırışdan imtina — duel dərhal sönür, çağıran gözlədilmir.</summary>
    Task<ServiceResult<bool>> DeclineChallengeAsync(
        Guid childId, Guid duelId, CancellationToken ct = default);

    Task<ServiceResult<ArenaDuelDto>> GetDuelAsync(Guid childId, Guid duelId, CancellationToken ct = default);

    Task<ServiceResult<DuelAnswerResultDto>> SubmitAnswerAsync(
        Guid childId, SubmitDuelAnswerRequest request, CancellationToken ct = default);

    /// <summary>Həftəlik liqa cədvəli — bazar ertəsi UTC 00:00-da sıfırlanır.</summary>
    Task<ServiceResult<ArenaLeagueDto>> GetLeagueAsync(Guid childId, CancellationToken ct = default);

    /// <summary>Nəticə ekranı. Rəqib hələ bitirməyibsə nəticə <c>Pending</c> qayıdır.</summary>
    Task<ServiceResult<ArenaDuelResultDto>> GetResultAsync(Guid childId, Guid duelId, CancellationToken ct = default);
}
