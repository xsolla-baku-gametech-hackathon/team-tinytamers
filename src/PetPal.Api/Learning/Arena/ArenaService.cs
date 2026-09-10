using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.Pets;
using PetPal.Api.Progress;
using PetPal.Api.Realtime;
using PetPal.Api.Rewards;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Dtos.Rewards;
using PetPal.Shared.Enums;

namespace PetPal.Api.Learning.Arena;

/// <summary>
/// Bilik Arenası — Öyrən bölməsindəki uşaq-uşaq yarışı (docs/LEARN_ARENA.md).
///
/// <para>Dörd qərar bütün sinfi izah edir:</para>
/// <list type="number">
///   <item><b>Sinxron duel.</b> Yarış rəqibsiz BAŞLAMIR: dəst snapshot edilir,
///   duel rəqib gözləyir və ikinci uşaq qoşulan kimi geri sayım işə düşür —
///   dəst hər ikisi üçün eyni andan sayılır (<see cref="Duel.StartedAt"/>).
///   Buna görə açıq duel saatlarla yox, dəqiqələrlə yaşayır: gözləyən duel
///   "hazırda ekran qarşısındakı uşaq" deməkdir.</item>
///   <item><b>Ayrıca reytinq.</b> <see cref="ChildProfile.ArenaRating"/> yarışın
///   öz reytinqidir — <see cref="SkillMastery.Rating"/>-ə TOXUNULMUR, yoxsa
///   yarışdakı tələsik səhvlər növbəti günün adaptiv seçimini korlayardı.</item>
///   <item><b>Bot yoxdur.</b> Rəqib tapılmasa duel gözləyir; süni rəqib əsl uşaq
///   kimi göstərilmir.</item>
///   <item><b>Hovuz hamıdır.</b> Rəqib bütün istifadəçilər arasından seçilir;
///   <see cref="ChildProfile.ArenaFriendsOnly"/> valideynin könüllü daralmasıdır.
///   Bu, təhlükəsizdir, çünki arenada sərbəst mətn yoxdur və tanımadığı rəqibin
///   əsl adı göstərilmir.</item>
/// </list>
/// </summary>
public class ArenaService : IArenaService
{
    /// <summary>
    /// Bir sualın maksimum hesablanan müddəti. Uşaq telefonu yerə qoyub gedə
    /// bilər — belə fasilə bütün duelin nəticəsini qərəzləndirməməlidir.
    /// </summary>
    private const int MaxQuestionMilliseconds = 120_000;

    /// <summary>Uyğunlaşdırma sorğusunun bir dəfəyə oxuduğu açıq duel sayı.</summary>
    private const int CandidateBatchSize = 50;

    /// <summary>
    /// Geri sayımın sonunda qəbul edilən irəliləmə. Klientin sayğacı serverin
    /// saatından bir neçə yüz millisaniyə qabaqda bitə bilər — uşağın ilk cavabı
    /// buna görə rədd olunmamalıdır. Belə cavabın müddəti onsuz da sıfıra yığılır.
    /// </summary>
    private static readonly TimeSpan AnswerGrace = TimeSpan.FromMilliseconds(1_500);

    /// <summary>Məşq rəqibinin bir suala sərf etdiyi vaxtın aralığı (ms).</summary>
    private const int PracticeMinMilliseconds = 2_500;
    private const int PracticeMaxMilliseconds = 9_000;

    private readonly AppDbContext _db;
    private readonly IQuestionSelector _selector;
    private readonly IRewardService _rewards;
    private readonly IDailyGoalService _dailyGoals;
    private readonly IArenaStandings _standings;
    private readonly ILiveNotifier _notifier;
    private readonly TimeProvider _clock;
    private readonly ArenaOptions _options;
    private readonly ScreenTimeOptions _screenTime;

    public ArenaService(
        AppDbContext db,
        IQuestionSelector selector,
        IRewardService rewards,
        IDailyGoalService dailyGoals,
        IArenaStandings standings,
        ILiveNotifier notifier,
        TimeProvider clock,
        IOptions<ArenaOptions> options,
        IOptions<ScreenTimeOptions> screenTime)
    {
        _db = db;
        _selector = selector;
        _rewards = rewards;
        _dailyGoals = dailyGoals;
        _standings = standings;
        _notifier = notifier;
        _clock = clock;
        _options = options.Value;
        _screenTime = screenTime.Value;
    }

    public async Task<ServiceResult<ArenaStatusDto>> GetStatusAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == childId, ct);
        if (child is null)
            return ServiceResult<ArenaStatusDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var now = _clock.GetUtcNow().UtcDateTime;

        var entries = await _db.DuelEntries
            .AsNoTracking()
            .Include(e => e.Duel)
            .Where(e => e.ChildProfileId == childId)
            .ToListAsync(ct);

        // Yalnız BAŞLAMIŞ duelə qayıdılır. Rəqib gözləyən duel "davam edən oyun"
        // deyil — o, axtarışdır və uşaq arenaya qayıdanda yenidən axtarır.
        var active = entries.FirstOrDefault(e => e.FinishedAt == null && e.Duel.Status == DuelStatus.Live);

        // Məşq duelləri nə statistikaya, nə də gündəlik həddə girir — rəqib sünidir.
        var real = entries.Where(e => !e.Duel.IsPractice).ToList();

        // Çağırışlar STATUS ilə gəlir, yalnız real vaxt kanalı ilə yox: kanal
        // qopubsa da uşaq dostunun çağırışını görməlidir.
        var challenges = await _db.Duels
            .AsNoTracking()
            .Where(d => d.TargetChildProfileId == childId
                        && d.Status == DuelStatus.WaitingOpponent
                        && d.ExpiresAt > now)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new ArenaChallengeDto
            {
                DuelId = d.Id,
                FromName = d.CreatedByChildProfile.DisplayName,
                AvatarKey = d.CreatedByChildProfile.AvatarKey,
                ExpiresAt = d.ExpiresAt
            })
            .ToListAsync(ct);

        return ServiceResult<ArenaStatusDto>.Ok(new ArenaStatusDto
        {
            IncomingChallenges = challenges,
            Enabled = child.ArenaEnabled,
            FriendsOnly = child.ArenaFriendsOnly,
            Rating = child.ArenaRating,
            DuelsPerDay = _options.DuelsPerDay,

            // Hədd yoxdursa (DuelsPerDay <= 0) qalıq say mənasızdır — ekran
            // ona baxmır, "∞" göstərir. Hədd varsa: ləğv olunmuş duel SAYILMIR,
            // çünki rəqib tapılmayıb və uşaq heç nə oynamayıb.
            DuelsLeftToday = _options.DuelsPerDay <= 0
                ? 0
                : Math.Max(0, _options.DuelsPerDay
                    - real.Count(e => e.StartedAt >= now.Date && e.Duel.Status != DuelStatus.Expired)),
            ActiveDuelId = active?.DuelId,
            PendingResultDuelIds = real
                .Where(e => e.Duel.Status == DuelStatus.Complete && e.ResultSeenAt == null)
                .OrderBy(e => e.Duel.CompletedAt)
                .Select(e => e.DuelId)
                .ToList(),
            Wins = real.Count(e => e.Outcome == DuelOutcome.Win),
            Draws = real.Count(e => e.Outcome == DuelOutcome.Draw),
            Losses = real.Count(e => e.Outcome == DuelOutcome.Loss)
        });
    }

    public async Task<ServiceResult<ArenaDuelDto>> StartDuelAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .Include(c => c.SkillMasteries)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child is null)
            return ServiceResult<ArenaDuelDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        if (!child.ArenaEnabled)
            return ServiceResult<ArenaDuelDto>.Forbidden(PetVoice.ArenaDisabled(child.LanguageCode));

        var now = _clock.GetUtcNow().UtcDateTime;
        await ExpireStaleDuelsAsync(now, ct);

        // Bitməmiş MƏŞQ dueli əsl yarışa mane olmur: onun rəqibi süni olduğuna
        // görə yarımçıq qalması heç kimi gözlətmir.
        await CancelUnfinishedPracticeAsync(childId, ct);

        // Yarımçıq ƏSL duel varsa yenisi yaradılmır: şəbəkə kəsilsə və ya uşaq
        // ekranı bağlasa, geri qayıdanda elə həmin dəstə düşməlidir — rəqib də
        // onun cavablarını gözləyir.
        var resumed = await LoadUnfinishedAsync(childId, ct);

        if (resumed is not null)
            return ServiceResult<ArenaDuelDto>.Ok(await BuildDuelViewAsync(child, resumed, ct));

        // Ekran vaxtı və gündəlik hədd — çağırış axını ilə eyni qayda.
        // Ləğv olunmuş (rəqib tapılmayan) duel sayılmır, bax GetStatusAsync.
        var blocked = await CheckArenaGuardsAsync(child, now, ct);
        if (blocked is not null)
            return blocked;

        EnsureMasteries(child);

        var entry = await JoinOpenDuelAsync(child, now, ct)
                    ?? await CreateDuelAsync(child, now, isPractice: false, ct);

        if (entry is null)
            return ServiceResult<ArenaDuelDto>.NotFound(
                Localized.T("Yarış üçün uyğun sual tapılmadı.", "No suitable question was found for a duel."));

        await _db.SaveChangesAsync(ct);

        // Açıq duelə qoşulduq: gözləyən uşağa rəqibin tapıldığını dərhal
        // bildiririk — o, ekranı yeniləmək üçün sorğu döyəcləməməlidir.
        if (entry.Duel.CreatedByChildProfileId != childId)
            await _notifier.DuelMatchedAsync(entry.Duel.CreatedByChildProfileId, entry.DuelId, ct);

        return ServiceResult<ArenaDuelDto>.Ok(await BuildDuelViewAsync(child, entry, ct));
    }

    /// <summary>
    /// Dostu birbaşa yarışa çağırır.
    ///
    /// <para>Yaranan duel adi açıq dueldən yalnız BİR şeylə fərqlənir: onun
    /// ünvanı var, ona görə hovuza düşmür və təsadüfi rəqib ona qoşula bilmir.
    /// Dəst, geri sayım, reytinq və mükafat tamamilə eynidir — «dostla oyna»
    /// ayrıca yarış rejimi deyil, sadəcə rəqibin necə tapılmasıdır.</para>
    ///
    /// <para>Valideyn açarları toxunulmaz qalır: arena bağlıdırsa çağırış nə
    /// göndərilir, nə qəbul edilir.</para>
    /// </summary>
    public async Task<ServiceResult<ArenaDuelDto>> ChallengeFriendAsync(
        Guid childId, Guid friendChildId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .Include(c => c.SkillMasteries)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child is null)
            return ServiceResult<ArenaDuelDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        if (!child.ArenaEnabled)
            return ServiceResult<ArenaDuelDto>.Forbidden(PetVoice.ArenaDisabled(child.LanguageCode));

        // Yalnız TƏSDİQLƏNMİŞ dost çağırıla bilər — gözləyən sorğu hələ dostluq deyil.
        var isFriend = await _db.Friendships
            .AnyAsync(f => f.ChildProfileId == childId
                           && f.FriendChildProfileId == friendChildId
                           && f.Status == FriendshipStatus.Active, ct);

        if (!isFriend)
            return ServiceResult<ArenaDuelDto>.Forbidden(
                Localized.T("Yalnız dostunu yarışa çağıra bilərsən.", "You can only challenge a friend."));

        // Dostun VALİDEYNİ arenanı bağlayıbsa çağırış göndərilmir. Əks halda
        // çağırış ölü doğulurdu: dostun ekranında kart görünürdü, qəbul etmək
        // isə mümkün deyildi — uşaq üçün izahsız nasazlıq kimi görünərdi.
        var rivalArenaEnabled = await _db.ChildProfiles
            .Where(c => c.Id == friendChildId)
            .Select(c => c.ArenaEnabled)
            .FirstOrDefaultAsync(ct);

        if (!rivalArenaEnabled)
            return ServiceResult<ArenaDuelDto>.Forbidden(
                Localized.T("Dostunun yarışı bağlıdır.", "Your friend's arena is switched off."));

        var now = _clock.GetUtcNow().UtcDateTime;
        await ExpireStaleDuelsAsync(now, ct);
        await CancelUnfinishedPracticeAsync(childId, ct);

        // Yalnız BAŞLAMIŞ duel çağırışa mane olur — orada rəqib uşağın
        // cavablarını gözləyir. Öz gözləyən dueli isə oyun deyil, axtarışdır.
        var live = await LoadLiveDuelAsync(childId, ct);
        if (live is not null)
            return ServiceResult<ArenaDuelDto>.Ok(await BuildDuelViewAsync(child, live, ct));

        var blocked = await CheckArenaGuardsAsync(child, now, ct);
        if (blocked is not null)
            return blocked;

        // Köhnə axtarış/çağırış söndürülür: uşaq indi KONKRET dostu seçib.
        // Bu olmasa ikinci dosta çağırış göndərmək mümkün olmazdı — funksiya
        // birinci duelin özünü qaytarardı.
        await CancelOwnWaitingDuelsAsync(childId, ct);

        EnsureMasteries(child);

        var entry = await CreateDuelAsync(child, now, isPractice: false, ct, targetChildId: friendChildId);

        if (entry is null)
            return ServiceResult<ArenaDuelDto>.NotFound(
                Localized.T("Yarış üçün uyğun sual tapılmadı.", "No suitable question was found for a duel."));

        await _db.SaveChangesAsync(ct);
        await _notifier.DuelChallengedAsync(friendChildId, entry.DuelId, child.DisplayName, ct);

        return ServiceResult<ArenaDuelDto>.Ok(await BuildDuelViewAsync(child, entry, ct));
    }

    /// <summary>
    /// Dostun çağırışını qəbul edir. Qəbul adi «açıq duelə qoşulma»nın eynisidir:
    /// dəst başlayır, geri sayım hər ikisi üçün eyni andan gedir.
    /// </summary>
    public async Task<ServiceResult<ArenaDuelDto>> AcceptChallengeAsync(
        Guid childId, Guid duelId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .Include(c => c.SkillMasteries)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child is null)
            return ServiceResult<ArenaDuelDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        if (!child.ArenaEnabled)
            return ServiceResult<ArenaDuelDto>.Forbidden(PetVoice.ArenaDisabled(child.LanguageCode));

        var now = _clock.GetUtcNow().UtcDateTime;

        var duel = await _db.Duels
            .Include(d => d.Questions)
            .Include(d => d.Entries)
            .FirstOrDefaultAsync(d => d.Id == duelId, ct);

        // Vaxtı keçmiş çağırış «tapılmadı» sayılır: uşağa «gec qaldın» demək
        // əvəzinə ekran sadəcə çağırışı siyahıdan silir.
        if (duel is null
            || duel.TargetChildProfileId != childId
            || duel.Status != DuelStatus.WaitingOpponent
            || duel.ExpiresAt <= now)
            return ServiceResult<ArenaDuelDto>.NotFound(
                Localized.T("Bu çağırış artıq keçərli deyil.", "That challenge is no longer valid."));

        // Dostluq QƏBUL ANINDA da yoxlanılır: çağırış göndəriləndən sonra
        // dostluq pozula bilər və o zaman duel dostluqdan sağ çıxmamalıdır.
        var stillFriends = await _db.Friendships
            .AnyAsync(f => f.ChildProfileId == childId
                           && f.FriendChildProfileId == duel.CreatedByChildProfileId
                           && f.Status == FriendshipStatus.Active, ct);

        if (!stillFriends)
            return ServiceResult<ArenaDuelDto>.NotFound(
                Localized.T("Bu çağırış artıq keçərli deyil.", "That challenge is no longer valid."));

        await CancelUnfinishedPracticeAsync(childId, ct);

        // Yalnız BAŞLAMIŞ duel qəbula mane olur. Uşağın öz axtarışı gedirsə,
        // dostun çağırışını seçmək onun haqqıdır — əks halda axtarışdakı uşaq
        // çağırışı heç vaxt qəbul edə bilməzdi və çağıran boş yerə gözləyərdi.
        var live = await LoadLiveDuelAsync(childId, ct);
        if (live is not null)
            return ServiceResult<ArenaDuelDto>.Ok(await BuildDuelViewAsync(child, live, ct));

        var blocked = await CheckArenaGuardsAsync(child, now, ct);
        if (blocked is not null)
            return blocked;

        await CancelOwnWaitingDuelsAsync(childId, ct);

        EnsureMasteries(child);

        var entry = AddEntry(duel, child, now);
        duel.Status = DuelStatus.Live;
        duel.StartedAt = now.AddSeconds(_options.CountdownSeconds);

        await _db.SaveChangesAsync(ct);
        await _notifier.DuelMatchedAsync(duel.CreatedByChildProfileId, duel.Id, ct);

        return ServiceResult<ArenaDuelDto>.Ok(await BuildDuelViewAsync(child, entry, ct));
    }

    /// <summary>
    /// Çağırışdan imtina. Duel dərhal söndürülür — çağıran uşaq ekran qarşısında
    /// gözləyir və ona «yox» demək cavabsız saxlamaqdan yaxşıdır.
    /// </summary>
    public async Task<ServiceResult<bool>> DeclineChallengeAsync(
        Guid childId, Guid duelId, CancellationToken ct = default)
    {
        var duel = await _db.Duels
            .FirstOrDefaultAsync(d => d.Id == duelId
                                      && d.TargetChildProfileId == childId
                                      && d.Status == DuelStatus.WaitingOpponent, ct);

        if (duel is null)
            return ServiceResult<bool>.NotFound(
                Localized.T("Bu çağırış artıq keçərli deyil.", "That challenge is no longer valid."));

        duel.Status = DuelStatus.Expired;
        await _db.SaveChangesAsync(ct);

        await _notifier.ChallengeCancelledAsync(duel.CreatedByChildProfileId, duel.Id, ct);

        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>
    /// Ekran vaxtı və gündəlik hədd yoxlaması — yeni duel açan hər üç yolda
    /// (adi axtarış, çağırış göndərmək, çağırışı qəbul etmək) eynidir.
    /// Qayda keçilibsə <c>null</c>, əks halda hazır xəta nəticəsi qaytarır.
    /// </summary>
    private async Task<ServiceResult<ArenaDuelDto>?> CheckArenaGuardsAsync(
        ChildProfile child, DateTime now, CancellationToken ct)
    {
        // Yuxu rejimi və gündəlik limit YENİ dueli bloklayır — duel də ekran vaxtıdır.
        var todayGoal = await _dailyGoals.GetOrCreateTodayAsync(child, ct);
        var screenTime = ScreenTimeGuard.Evaluate(child, todayGoal, now, _screenTime.Enforced);

        if (screenTime != ScreenTimeState.Allowed)
        {
            await _db.SaveChangesAsync(ct);
            return ServiceResult<ArenaDuelDto>.Forbidden(ScreenTimeGuard.MessageFor(screenTime, child.LanguageCode, child));
        }

        // Gündəlik hədd yalnız qoşulubsa yoxlanılır (DuelsPerDay > 0).
        if (_options.DuelsPerDay > 0)
        {
            var playedToday = await _db.DuelEntries
                .CountAsync(e => e.ChildProfileId == child.Id
                                 && e.StartedAt >= now.Date
                                 && !e.Duel.IsPractice
                                 && e.Duel.Status != DuelStatus.Expired, ct);

            if (playedToday >= _options.DuelsPerDay)
            {
                await _db.SaveChangesAsync(ct);
                return ServiceResult<ArenaDuelDto>.Forbidden(
                    PetVoice.ArenaDailyLimit(child.LanguageCode, _options.DuelsPerDay));
            }
        }

        return null;
    }

    /// <summary>
    /// Məşq dueli. Rəqib SÜNİDİR və ekranda açıq şəkildə belə işarələnir —
    /// süni rəqibi əsl uşaq kimi göstərmək uşağa yalan danışmaqdır.
    ///
    /// <para>Nə reytinq dəyişir, nə ulduz verilir, nə də gündəlik hədd yeyilir:
    /// süni rəqibdən qazanılan mükafat əsl yarışı dəyərsizləşdirərdi. Ekran
    /// vaxtı qaydası isə burada da işləyir.</para>
    /// </summary>
    public async Task<ServiceResult<ArenaDuelDto>> StartPracticeAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .Include(c => c.SkillMasteries)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child is null)
            return ServiceResult<ArenaDuelDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        if (!child.ArenaEnabled)
            return ServiceResult<ArenaDuelDto>.Forbidden(PetVoice.ArenaDisabled(child.LanguageCode));

        // Yarımçıq duel varsa (əsl və ya məşq) uşaq ona qayıdır — eyni anda iki
        // açıq dəst qalsaydı, hansına cavab verdiyi qarışardı.
        var resumed = await LoadUnfinishedAsync(childId, ct);
        if (resumed is not null)
            return ServiceResult<ArenaDuelDto>.Ok(await BuildDuelViewAsync(child, resumed, ct));

        var now = _clock.GetUtcNow().UtcDateTime;
        var todayGoal = await _dailyGoals.GetOrCreateTodayAsync(child, ct);
        var screenTime = ScreenTimeGuard.Evaluate(child, todayGoal, now, _screenTime.Enforced);
        if (screenTime != ScreenTimeState.Allowed)
        {
            await _db.SaveChangesAsync(ct);
            return ServiceResult<ArenaDuelDto>.Forbidden(ScreenTimeGuard.MessageFor(screenTime, child.LanguageCode, child));
        }

        EnsureMasteries(child);

        var entry = await CreateDuelAsync(child, now, isPractice: true, ct);
        if (entry is null)
            return ServiceResult<ArenaDuelDto>.NotFound(
                Localized.T("Yarış üçün uyğun sual tapılmadı.", "No suitable question was found for a duel."));

        await _db.SaveChangesAsync(ct);

        return ServiceResult<ArenaDuelDto>.Ok(await BuildDuelViewAsync(child, entry, ct));
    }

    /// <summary>
    /// Axtarışı dayandırır. Duel hovuzdan ÇIXARILMALIDIR: ekran qarşısında
    /// olmayan uşağın açıq dueli başqasını heç vaxt gəlməyəcək rəqiblə
    /// üz-üzə qoyardı — sinxron yarışda bu, ən pis haldır.
    ///
    /// <para>Başlamış duel ləğv olunmur: orada rəqib var və o, oynayır.</para>
    /// </summary>
    public async Task<ServiceResult<bool>> CancelSearchAsync(
        Guid childId, Guid duelId, CancellationToken ct = default)
    {
        var duel = await _db.Duels
            .Include(d => d.Entries)
            .FirstOrDefaultAsync(d => d.Id == duelId, ct);

        if (duel is null || duel.Entries.All(e => e.ChildProfileId != childId))
            return ServiceResult<bool>.NotFound(Localized.T("Duel tapılmadı.", "Duel not found."));

        // Rəqib bu arada qoşulubsa ləğv etmirik — yarış artıq başlayıb.
        if (duel.Status != DuelStatus.WaitingOpponent)
            return ServiceResult<bool>.Ok(false);

        duel.Status = DuelStatus.Expired;
        await _db.SaveChangesAsync(ct);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<ArenaDuelDto>> GetDuelAsync(Guid childId, Guid duelId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == childId, ct);
        if (child is null)
            return ServiceResult<ArenaDuelDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var entry = await _db.DuelEntries
            .AsNoTracking()
            .Include(e => e.Duel).ThenInclude(d => d.Questions)
            .Include(e => e.Answers)
            .FirstOrDefaultAsync(e => e.DuelId == duelId && e.ChildProfileId == childId, ct);

        if (entry is null)
            return ServiceResult<ArenaDuelDto>.NotFound(Localized.T("Duel tapılmadı.", "Duel not found."));

        return ServiceResult<ArenaDuelDto>.Ok(await BuildDuelViewAsync(child, entry, ct));
    }

    public async Task<ServiceResult<DuelAnswerResultDto>> SubmitAnswerAsync(
        Guid childId, SubmitDuelAnswerRequest request, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .Include(c => c.Pet)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child is null)
            return ServiceResult<DuelAnswerResultDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        if (!child.ArenaEnabled)
            return ServiceResult<DuelAnswerResultDto>.Forbidden(PetVoice.ArenaDisabled(child.LanguageCode));

        var entry = await _db.DuelEntries
            .Include(e => e.Answers)
            .Include(e => e.Duel).ThenInclude(d => d.Entries)
            .Include(e => e.Duel).ThenInclude(d => d.Questions)
            .FirstOrDefaultAsync(e => e.DuelId == request.DuelId && e.ChildProfileId == childId, ct);

        if (entry is null)
            return ServiceResult<DuelAnswerResultDto>.NotFound(Localized.T("Duel tapılmadı.", "Duel not found."));

        if (entry.IsFinished)
            return ServiceResult<DuelAnswerResultDto>.Conflict(
                Localized.T("Bu duel artıq tamamlanıb.", "This duel is already finished."));

        // Yarış rəqibsiz OYNANMIR. Duel başlamayıbsa (rəqib hələ tapılmayıb və
        // ya geri sayım getmir) cavab qəbul edilmir — server tərəfdə yoxlanılır,
        // çünki bu, yarışın ədalətinin şərtidir, ekranın nəzakəti deyil.
        if (entry.Duel.StartedAt is not { } duelStart)
            return ServiceResult<DuelAnswerResultDto>.Conflict(
                PetVoice.ArenaNotStarted(child.LanguageCode));

        var startsIn = duelStart - _clock.GetUtcNow().UtcDateTime;
        if (startsIn > AnswerGrace)
            return ServiceResult<DuelAnswerResultDto>.Conflict(
                PetVoice.ArenaNotStarted(child.LanguageCode));

        var slot = entry.Answers.FirstOrDefault(a => a.QuestionId == request.QuestionId);
        if (slot is null)
            return ServiceResult<DuelAnswerResultDto>.NotFound(
                Localized.T("Bu sual duelə aid deyil.", "This question does not belong to the duel."));

        if (slot.IsAnswered)
            return ServiceResult<DuelAnswerResultDto>.Conflict(
                Localized.T("Bu sual artıq cavablandırılıb.", "This question has already been answered."));

        var question = await _db.Questions.AsNoTracking().FirstAsync(q => q.Id == request.QuestionId, ct);
        if (request.ChosenIndex < 0 || request.ChosenIndex >= question.Options.Count)
            return ServiceResult<DuelAnswerResultDto>.Fail(
                Localized.T("Cavab variantı düzgün deyil.", "That answer choice is not valid."));

        var now = _clock.GetUtcNow().UtcDateTime;
        var isCorrect = request.ChosenIndex == question.CorrectIndex;

        // Müddət SERVERDƏ ölçülür: klientin göndərdiyi vaxt qəbul edilmir, çünki
        // bərabərliyi məhz sürət həll edir. Baza nöqtəsi əvvəlki cavabın anıdır,
        // birinci sualda isə DUELİN başlama anı — o, hər iki uşaq üçün eynidir.
        // Qeydin öz `StartedAt`-ı burada işə yaramır: yaradan uşaq duelə rəqibdən
        // əvvəl qoşulub və onun gözləmə saniyələri yarışa yazılmamalıdır.
        var previous = entry.Answers.Where(a => a.AnsweredAt != null).Max(a => a.AnsweredAt) ?? duelStart;
        var elapsed = (int)Math.Clamp((now - previous).TotalMilliseconds, 0, MaxQuestionMilliseconds);

        slot.ChosenIndex = request.ChosenIndex;
        slot.IsCorrect = isCorrect;
        slot.ElapsedMs = elapsed;
        slot.AnsweredAt = now;

        entry.TotalMilliseconds += elapsed;
        if (isCorrect)
            entry.CorrectCount++;

        var answered = entry.Answers.Count(a => a.IsAnswered);
        var finished = answered == entry.Answers.Count;

        if (finished)
        {
            entry.FinishedAt = now;

            if (entry.Duel.IsPractice)
                CompletePractice(entry, now);
            else
                await TryCompleteDuelAsync(entry.Duel, now, ct);
        }

        await _db.SaveChangesAsync(ct);
        await NotifyRivalAsync(entry, answered, ct);

        var result = new DuelAnswerResultDto
        {
            IsCorrect = isCorrect,
            CorrectIndex = question.CorrectIndex,
            Explanation = question.Explanation,
            AnsweredCount = answered,
            TotalCount = entry.Answers.Count,
            Finished = finished,
            PetReaction = PetVoice.AnswerReaction(
                child.LanguageCode, isCorrect, entry.CorrectCount, question.Skill, child.Pet?.Name ?? "Pet")
        };

        // Rəqib artıq oynayıbsa nəticə DƏRHAL görünür — uşaq gözləmir.
        if (finished && entry.Duel.Status == DuelStatus.Complete)
            result.Result = await BuildResultAsync(child, entry.DuelId, ct);

        return ServiceResult<DuelAnswerResultDto>.Ok(result);
    }

    public async Task<ServiceResult<ArenaDuelResultDto>> GetResultAsync(
        Guid childId, Guid duelId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .Include(c => c.Pet)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child is null)
            return ServiceResult<ArenaDuelResultDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var exists = await _db.DuelEntries.AnyAsync(e => e.DuelId == duelId && e.ChildProfileId == childId, ct);
        if (!exists)
            return ServiceResult<ArenaDuelResultDto>.NotFound(Localized.T("Duel tapılmadı.", "Duel not found."));

        return ServiceResult<ArenaDuelResultDto>.Ok(await BuildResultAsync(child, duelId, ct));
    }

    /// <summary>
    /// Həftəlik liqa. Cədvəl ayrıca saxlanılmır — hər dəfə duel qeydlərindən
    /// hesablanır (bax <see cref="ArenaLeague"/>), yəni "həftə sonu sıfırlanma"
    /// heç bir fon işi tələb etmir.
    /// </summary>
    public async Task<ServiceResult<ArenaLeagueDto>> GetLeagueAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == childId, ct);
        if (child is null)
            return ServiceResult<ArenaLeagueDto>.NotFound(Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        var now = _clock.GetUtcNow().UtcDateTime;
        var standings = await _standings.GetCurrentWeekAsync(ct);
        var myIndex = standings.FindIndex(r => r.ChildProfileId == childId);

        var shown = standings.Take(ArenaLeague.TopCount).ToList();

        // Uşaq ilk 20-dən kənardadırsa öz sətri sona əlavə olunur: cədvəldə
        // özünü görməmək 6–10 yaş üçün ən pis nəticədir.
        if (myIndex >= ArenaLeague.TopCount)
            shown.Add(standings[myIndex]);

        var ids = shown.Select(r => r.ChildProfileId).ToList();

        var profiles = await _db.ChildProfiles
            .AsNoTracking()
            .Include(c => c.Pet)
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var friendIds = (await _db.Friendships
                .AsNoTracking()
                .Where(f => f.ChildProfileId == childId && f.Status == FriendshipStatus.Active)
                .Select(f => f.FriendChildProfileId)
                .ToListAsync(ct))
            .ToHashSet();

        var rankById = standings
            .Select((row, index) => (row.ChildProfileId, Rank: index + 1))
            .ToDictionary(x => x.ChildProfileId, x => x.Rank);

        var rows = new List<ArenaLeagueRowDto>();

        foreach (var row in shown)
        {
            if (!profiles.TryGetValue(row.ChildProfileId, out var profile))
                continue;

            var isMe = row.ChildProfileId == childId;
            var isFriend = friendIds.Contains(row.ChildProfileId);

            rows.Add(new ArenaLeagueRowDto
            {
                Rank = rankById[row.ChildProfileId],

                // Ümumi siyahıda əsl ad GÖRÜNMÜR — yalnız dostda və öz sətrində.
                DisplayName = isMe || isFriend ? profile.DisplayName : null,
                PetName = profile.Pet?.Name ?? string.Empty,
                AvatarKey = profile.AvatarKey,
                Level = profile.Pet?.Level ?? 1,
                Points = row.Points,
                Wins = row.Wins,
                Draws = row.Draws,
                Losses = row.Losses,
                IsMe = isMe,
                IsFriend = isFriend
            });
        }

        return ServiceResult<ArenaLeagueDto>.Ok(new ArenaLeagueDto
        {
            WeekStart = ArenaLeague.WeekStart(now),
            WeekEnd = ArenaLeague.WeekEnd(now),
            MyRank = myIndex < 0 ? 0 : myIndex + 1,
            MyPoints = myIndex < 0 ? 0 : standings[myIndex].Points,
            MyDuels = myIndex < 0 ? 0 : standings[myIndex].Duels,
            Rows = rows
        });
    }

    /// <summary>
    /// Rəqibə real vaxt xəbəri. Duelin özü bundan ASILI DEYİL — bütün məlumat
    /// adi endpoint-lərdən də alınır; kanal sadəcə onu tez çatdırır.
    /// </summary>
    private async Task NotifyRivalAsync(DuelEntry entry, int answered, CancellationToken ct)
    {
        // Məşq duelində rəqib sünidir — xəbər göndəriləcək kimsə yoxdur.
        if (entry.Duel.IsPractice)
            return;

        var rival = entry.Duel.Entries.FirstOrDefault(e => e.ChildProfileId != entry.ChildProfileId);
        if (rival is null)
            return;

        if (entry.Duel.Status == DuelStatus.Complete)
        {
            // Cavabı indi verən uşaq nəticəni onsuz da sorğunun cavabında alır.
            await _notifier.DuelCompletedAsync([rival.ChildProfileId], entry.DuelId, ct);
            return;
        }

        await _notifier.OpponentProgressAsync(rival.ChildProfileId, entry.DuelId, answered, entry.Answers.Count, ct);
    }

    // ---------- Uyğunlaşdırma ----------

    /// <summary>
    /// Açıq duel axtarır. Hovuz standart olaraq HAMIDIR — filtrlər yalnız
    /// valideyn açarından və reytinq/çətinlik zolağından gəlir.
    /// </summary>
    private async Task<DuelEntry?> JoinOpenDuelAsync(ChildProfile child, DateTime now, CancellationToken ct)
    {
        var candidates = await _db.Duels
            .Include(d => d.Questions)
            .Where(d => d.Status == DuelStatus.WaitingOpponent
                        && !d.IsPractice
                        // Ünvanlanmış duel hovuza DÜŞMÜR: onu yalnız çağırılan
                        // uşaq qəbul edə bilər, təsadüfi rəqib yox.
                        && d.TargetChildProfileId == null
                        && d.ExpiresAt > now
                        && d.CreatedByChildProfileId != child.Id
                        && d.Entries.Count < 2
                        && !d.Entries.Any(e => e.ChildProfileId == child.Id))
            .OrderBy(d => d.CreatedAt)
            .Take(CandidateBatchSize)
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return null;

        // Dostluq sorğusu yalnız məhdudiyyət varsa işə düşür — hovuz standart
        // olaraq açıqdır və adi halda əlavə sorğuya ehtiyac qalmır.
        var friendIds = child.ArenaFriendsOnly || candidates.Any(d => d.FriendsOnly)
            ? (await _db.Friendships
                .Where(f => f.ChildProfileId == child.Id && f.Status == FriendshipStatus.Active)
                .Select(f => f.FriendChildProfileId)
                .ToListAsync(ct))
                .ToHashSet()
            : new HashSet<Guid>();

        foreach (var duel in candidates)
        {
            // Məhdudiyyət hər iki tərəfə baxır: mənim açarım da, yaradanınkı da.
            // Dostluq qarşılıqlı yazıldığına görə bir yoxlama hər ikisi üçün kifayətdir.
            if ((child.ArenaFriendsOnly || duel.FriendsOnly) && !friendIds.Contains(duel.CreatedByChildProfileId))
                continue;

            var mastery = child.SkillMasteries.First(m => m.Skill == duel.Skill);
            var waitingSeconds = (int)(now - duel.CreatedAt).TotalSeconds;

            var matches = ArenaMatchmaker.Matches(
                waitingSeconds,
                duel.CreatorArenaRating,
                child.ArenaRating,
                duel.Difficulty,
                AdaptiveEngine.TargetDifficulty(mastery.Rating),
                _options);

            if (!matches)
                continue;

            var entry = AddEntry(duel, child, now);

            // RƏQİB TAPILDI — dəst məhz burada başlayır və hər iki uşaq üçün
            // eyni andan sayılır. Başlanğıc gələcəkdədir: geri sayım ekranı
            // birinci sualın vaxtına yazılmasın deyə.
            duel.Status = DuelStatus.Live;
            duel.StartedAt = now.AddSeconds(_options.CountdownSeconds);

            return entry;
        }

        return null;
    }

    /// <summary>
    /// Yalnız BAŞLAMIŞ duel — rəqib qoşulub və oyun gedir.
    ///
    /// <para><see cref="LoadUnfinishedAsync"/>-dən fərqi budur ki, o, gözləyən
    /// dueli də «yarımçıq» sayır. Adi axtarışda bu doğrudur (uşaq öz axtarışına
    /// qayıdır), çağırış axınında isə YANLIŞDIR: gözləyən duel oyun deyil və
    /// yeni seçim onu əvəz etməlidir.</para>
    /// </summary>
    private Task<DuelEntry?> LoadLiveDuelAsync(Guid childId, CancellationToken ct) =>
        _db.DuelEntries
            .Include(e => e.Duel).ThenInclude(d => d.Questions)
            .Include(e => e.Answers)
            .FirstOrDefaultAsync(e => e.ChildProfileId == childId
                                      && e.FinishedAt == null
                                      && e.Duel.Status == DuelStatus.Live, ct);

    /// <summary>
    /// Uşağın ÖZ yaratdığı və hələ rəqib gözləyən duellərini söndürür.
    ///
    /// <para>Çağırış idisə, ünvanlanan uşağa xəbər gedir: onun ekranındakı
    /// çağırış kartı özü yox olmalıdır, yoxsa artıq mövcud olmayan dueli
    /// qəbul etməyə çalışardı.</para>
    /// </summary>
    private async Task CancelOwnWaitingDuelsAsync(Guid childId, CancellationToken ct)
    {
        var waiting = await _db.Duels
            .Where(d => d.CreatedByChildProfileId == childId && d.Status == DuelStatus.WaitingOpponent)
            .ToListAsync(ct);

        if (waiting.Count == 0)
            return;

        foreach (var duel in waiting)
            duel.Status = DuelStatus.Expired;

        await _db.SaveChangesAsync(ct);

        foreach (var duel in waiting.Where(d => d.TargetChildProfileId is not null))
            await _notifier.ChallengeCancelledAsync(duel.TargetChildProfileId!.Value, duel.Id, ct);
    }

    /// <summary>Yarımçıq qalmış duel qeydi — uşaq həmişə ona qayıdır.</summary>
    private Task<DuelEntry?> LoadUnfinishedAsync(Guid childId, CancellationToken ct) =>
        _db.DuelEntries
            .Include(e => e.Duel).ThenInclude(d => d.Questions)
            .Include(e => e.Answers)
            .FirstOrDefaultAsync(e => e.ChildProfileId == childId
                                      && e.FinishedAt == null
                                      && e.Duel.Status != DuelStatus.Expired, ct);

    /// <summary>
    /// Yarımçıq məşq duelini bağlayır. Əsl duel yarımçıq atılmır (rəqib onu
    /// oynayır), məşqdə isə gözləyən yoxdur — uşaq istədiyi an əsl yarışa
    /// keçə bilməlidir.
    /// </summary>
    private async Task CancelUnfinishedPracticeAsync(Guid childId, CancellationToken ct)
    {
        var practice = await _db.Duels
            .Where(d => d.IsPractice
                        && d.Status != DuelStatus.Complete
                        && d.Status != DuelStatus.Expired
                        && d.Entries.Any(e => e.ChildProfileId == childId && e.FinishedAt == null))
            .ToListAsync(ct);

        if (practice.Count == 0)
            return;

        foreach (var duel in practice)
            duel.Status = DuelStatus.Expired;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<DuelEntry?> CreateDuelAsync(
        ChildProfile child, DateTime now, bool isPractice, CancellationToken ct,
        Guid? targetChildId = null)
    {
        var mastery = await PickSkillAsync(child, ct);

        var questions = await _selector.SelectAsync(
            child.Id, mastery.Skill, child.LanguageCode, mastery.Rating, child.Age, _options.QuestionCount, ct);

        if (questions.Count == 0)
            return null;

        var duel = new Duel
        {
            Skill = mastery.Skill,
            Difficulty = (int)Math.Round(questions.Average(q => q.Difficulty)),

            // Məşqin rəqibi hazırdır (süni), ona görə dəst DƏRHAL başlayır.
            // Əsl duel isə rəqib qoşulana qədər gözləyir və cavab qəbul etmir.
            Status = isPractice ? DuelStatus.Live : DuelStatus.WaitingOpponent,
            StartedAt = isPractice ? now : null,

            CreatedByChildProfileId = child.Id,
            CreatorArenaRating = child.ArenaRating,
            FriendsOnly = child.ArenaFriendsOnly,
            TargetChildProfileId = targetChildId,
            CreatedAt = now,

            // Gözləyən duel DƏQİQƏLƏR sonra ölür: açıq duel "hazırda ekran
            // qarşısında gözləyən uşaq" deməkdir, saatlarla açıq qalan duelə
            // qoşulan uşaq isə heç vaxt oynamayacaq rəqiblə qalardı.
            //
            // Çağırış daha tez sönür: o, konkret adama ünvanlanıb və cavabsız
            // qalarsa uşaq adi rəqib axtarmağa keçməlidir.
            ExpiresAt = targetChildId is null
                ? now.AddMinutes(_options.WaitingExpireMinutes)
                : now.AddSeconds(_options.ChallengeExpireSeconds),
            IsPractice = isPractice
        };

        // Məşq rəqibinin cavabları İNDİ, uşaq oynamazdan ƏVVƏL hesablanır —
        // sonra hesablansaydı, rəqib uşağın nəticəsinə uyğunlaşardı.
        var rival = isPractice ? new Random() : null;

        for (var i = 0; i < questions.Count; i++)
        {
            var slot = new DuelQuestion { QuestionId = questions[i].Id, Order = i };

            if (rival is not null)
            {
                // Süni rəqibin gücü uşağın arena reytinqinə bağlıdır: nə əlçatmaz,
                // nə də mənasız asan olur.
                var chance = AdaptiveEngine.ExpectedScore(child.ArenaRating, questions[i].Difficulty);
                slot.PracticeCorrect = rival.NextDouble() < chance;
                slot.PracticeElapsedMs = rival.Next(PracticeMinMilliseconds, PracticeMaxMilliseconds);
            }

            duel.Questions.Add(slot);
        }

        _db.Duels.Add(duel);

        return AddEntry(duel, child, now);
    }

    /// <summary>
    /// Duelin bacarıq sahəsini seçir. Öyrənmə sessiyasından FƏRQLİ qayda işləyir:
    /// orada həmişə ən zəif sahə götürülür, burada isə sahə NÖVBƏ İLƏ dəyişir.
    ///
    /// <para>Səbəb arenanın öz qərarından gəlir: duel <see cref="SkillMastery.Rating"/>-ə
    /// toxunmur, yəni "ən zəif sahə" yarışdan asılı olaraq heç vaxt dəyişmir.
    /// Nəticədə hər duel eyni sahənin eyni çətinlik zolağından gəlirdi və uşaq
    /// eyni sualları təkrar-təkrar görürdü. Arena zəif sahəni düzəltmək üçün
    /// deyil — bunu öyrənmə sessiyası edir; arena yarışdır və müxtəlif olmalıdır.</para>
    ///
    /// <para>Sonuncu duelin sahəsi kənarda qalır ki, ard-arda iki eyni mövzu
    /// düşməsin.</para>
    /// </summary>
    private async Task<SkillMastery> PickSkillAsync(ChildProfile child, CancellationToken ct)
    {
        var lastSkill = await _db.DuelEntries
            .AsNoTracking()
            .Where(e => e.ChildProfileId == child.Id)
            .OrderByDescending(e => e.StartedAt)
            .Select(e => (SkillArea?)e.Duel.Skill)
            .FirstOrDefaultAsync(ct);

        var candidates = child.SkillMasteries.Where(m => m.Skill != lastSkill).ToList();

        // Yalnız bir sahə varsa (və ya hamısı sonuncu ilə eynidirsə) təkrardan
        // qaçmaq mümkün deyil — boş dəst qaytarmaqdansa eyni sahə yaxşıdır.
        if (candidates.Count == 0)
            candidates = child.SkillMasteries.ToList();

        return candidates[Random.Shared.Next(candidates.Count)];
    }

    /// <summary>Uşağın duel qeydi — sual dəsti cavabsız sətirlər kimi açılır.</summary>
    private DuelEntry AddEntry(Duel duel, ChildProfile child, DateTime now)
    {
        var entry = new DuelEntry
        {
            Duel = duel,
            ChildProfileId = child.Id,
            StartedAt = now,
            ArenaRatingBefore = child.ArenaRating
        };

        foreach (var question in duel.Questions.OrderBy(q => q.Order))
            entry.Answers.Add(new DuelAnswer { QuestionId = question.QuestionId, Order = question.Order });

        duel.Entries.Add(entry);
        _db.DuelEntries.Add(entry);

        return entry;
    }

    /// <summary>
    /// Rəqib tapılmayan duellər ləğv olunur. Ayrıca fon işi yoxdur — sweep
    /// arenaya hər girişdə işləyir və bu, bir cədvəl yeniləməsidir.
    /// </summary>
    private async Task ExpireStaleDuelsAsync(DateTime now, CancellationToken ct)
    {
        var stale = await _db.Duels
            .Where(d => d.Status == DuelStatus.WaitingOpponent && d.ExpiresAt <= now)
            .Take(CandidateBatchSize)
            .ToListAsync(ct);

        if (stale.Count == 0)
            return;

        foreach (var duel in stale)
            duel.Status = DuelStatus.Expired;

        await _db.SaveChangesAsync(ct);
    }

    // ---------- Tamamlanma ----------

    /// <summary>
    /// Hər iki tərəf bitiribsə duel bağlanır: nəticə, arena reytinqi və ulduzlar
    /// eyni anda hər iki uşaq üçün yazılır. Birinci oynayan uşaq bu anda onlayn
    /// olmaya bilər — ona görə mükafat onun nəticəyə baxmasını gözləmir.
    /// </summary>
    private async Task TryCompleteDuelAsync(Duel duel, DateTime now, CancellationToken ct)
    {
        var entries = duel.Entries.ToList();
        if (entries.Count < 2 || entries.Any(e => !e.IsFinished))
            return;

        var first = entries[0];
        var second = entries[1];

        var outcome = ArenaRatingEngine.Decide(
            first.CorrectCount, first.TotalMilliseconds, second.CorrectCount, second.TotalMilliseconds);

        var children = await _db.ChildProfiles
            .Where(c => c.Id == first.ChildProfileId || c.Id == second.ChildProfileId)
            .ToDictionaryAsync(c => c.Id, ct);

        await SettleAsync(
            first, children[first.ChildProfileId], outcome,
            second.ArenaRatingBefore, second.TotalMilliseconds, ct);

        await SettleAsync(
            second, children[second.ChildProfileId], ArenaRatingEngine.Opposite(outcome),
            first.ArenaRatingBefore, first.TotalMilliseconds, ct);

        duel.Status = DuelStatus.Complete;
        duel.CompletedAt = now;
    }

    /// <summary>
    /// Məşq dueli bir tərəflidir: rəqibin cavabları duel yaradılanda hesablanıb.
    /// Nəticə göstərilir, amma REYTİNQ VƏ ULDUZ toxunulmur — süni rəqibdən
    /// mükafat qazanmaq əsl yarışı dəyərsizləşdirərdi.
    /// </summary>
    private static void CompletePractice(DuelEntry entry, DateTime now)
    {
        var duel = entry.Duel;

        entry.Outcome = ArenaRatingEngine.Decide(
            entry.CorrectCount,
            entry.TotalMilliseconds,
            duel.Questions.Count(q => q.PracticeCorrect),
            duel.Questions.Sum(q => q.PracticeElapsedMs));

        entry.ArenaRatingAfter = entry.ArenaRatingBefore;
        entry.StarsEarned = 0;
        entry.SpeedBonusStars = 0;

        // Məşqin nəticəsi dərhal görünür — "nəticə hazırdır" nişanına düşmür.
        entry.ResultSeenAt = now;

        duel.Status = DuelStatus.Complete;
        duel.CompletedAt = now;
    }

    private async Task SettleAsync(
        DuelEntry entry,
        ChildProfile child,
        DuelOutcome outcome,
        int opponentRating,
        int opponentMilliseconds,
        CancellationToken ct)
    {
        entry.Outcome = outcome;
        entry.ArenaRatingAfter = ArenaRatingEngine.UpdateRating(entry.ArenaRatingBefore, opponentRating, outcome);

        // Yalnız ARENA reytinqi dəyişir — SkillMastery.Rating-ə toxunulmur.
        child.ArenaRating = entry.ArenaRatingAfter;

        // Rəqibi nə qədər tez qabaqlayıbsa, ulduz da bir o qədər çoxdur. Bonus
        // cəmin İÇİNDƏDİR, ona görə ayrıca da yazılır: nəticə ekranı ayrımı
        // sonradan yenidən hesablamamalıdır.
        entry.SpeedBonusStars = ArenaRatingEngine.SpeedBonusFor(
            outcome, entry.TotalMilliseconds, opponentMilliseconds, _options);

        entry.StarsEarned = ArenaRatingEngine.StarsFor(outcome, _options) + entry.SpeedBonusStars;
        await _rewards.GrantStarsAsync(child, entry.StarsEarned, "Arena duel", ct);
    }

    // ---------- Görünüşlər ----------

    private async Task<ArenaDuelDto> BuildDuelViewAsync(ChildProfile child, DuelEntry entry, CancellationToken ct)
    {
        var duel = entry.Duel;
        var questionIds = entry.Answers.Select(a => a.QuestionId).ToList();

        var byId = await _db.Questions
            .AsNoTracking()
            .Where(q => questionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, ct);

        var opponentEntry = duel.IsPractice
            ? null
            : await LoadOpponentEntryAsync(entry.DuelId, child.Id, ct);

        return new ArenaDuelDto
        {
            DuelId = duel.Id,
            Skill = duel.Skill,
            Difficulty = duel.Difficulty,
            Status = duel.Status,
            IsPractice = duel.IsPractice,
            AnsweredCount = entry.Answers.Count(a => a.IsAnswered),
            Finished = entry.IsFinished,

            // Geri sayım MİLLİSANİYƏ ilə göndərilir, mütləq vaxt kimi yox: uşağın
            // cihazının saatı serverin saatı ilə üst-üstə düşməyə bilər. Hər iki
            // uşaq öz qalıq vaxtını alır, ona görə şəbəkə gecikməsi kiminsə
            // hesabına düşmür — sayğaclar fərqli rəqəmdən başlayıb eyni anda bitir.
            StartsInMilliseconds = StartsInMilliseconds(duel),

            // Məşq rəqibi "artıq oynayıb": cavabları duel yaradılanda hesablanıb.
            OpponentFinished = duel.IsPractice || (opponentEntry?.IsFinished ?? false),

            // Çağırışda rəqib duelə qoşulmamışdan ƏVVƏL də məlumdur: ünvan var.
            // Ona görə çağıranın ekranı «axtarılır» yox, «filankəs gözlənilir»
            // deyə bilir. Çağırılan uşaq özünü rəqib kimi görməməlidir.
            Opponent = duel.IsPractice
                ? PracticeOpponent(child)
                : await BuildOpponentAsync(
                    child.Id,
                    opponentEntry?.ChildProfileId
                        ?? (duel.TargetChildProfileId == child.Id ? null : duel.TargetChildProfileId),
                    ct),
            Questions = entry.Answers
                .OrderBy(a => a.Order)
                .Where(a => byId.ContainsKey(a.QuestionId))
                .Select(a => ToDto(byId[a.QuestionId]))
                .ToList()
        };
    }

    /// <summary>
    /// Dəstin başlamasına neçə millisaniyə qalıb. Duel hələ rəqib gözləyirsə
    /// <c>null</c> — bu, ekran üçün "axtarış davam edir" siqnalıdır.
    /// </summary>
    private int? StartsInMilliseconds(Duel duel)
    {
        if (duel.StartedAt is not { } startedAt)
            return null;

        var remaining = (startedAt - _clock.GetUtcNow().UtcDateTime).TotalMilliseconds;
        return (int)Math.Max(0, remaining);
    }

    private async Task<ArenaDuelResultDto> BuildResultAsync(ChildProfile child, Guid duelId, CancellationToken ct)
    {
        var duel = await _db.Duels
            .Include(d => d.Questions)
            .Include(d => d.Entries).ThenInclude(e => e.Answers)
            .FirstAsync(d => d.Id == duelId, ct);

        var mine = duel.Entries.First(e => e.ChildProfileId == child.Id);
        var opponent = duel.Entries.FirstOrDefault(e => e.ChildProfileId != child.Id);

        var outcome = duel.Status switch
        {
            DuelStatus.Expired => DuelOutcome.Expired,
            DuelStatus.Complete => mine.Outcome,
            _ => DuelOutcome.Pending
        };

        // Rəqibin cavabları YALNIZ duel bağlananda açılır: əks halda sonra
        // oynayan uşaq nəticə ekranını açıb rəqibin seçimlərini görərdi.
        var reveal = duel.Status == DuelStatus.Complete && (duel.IsPractice || opponent is not null);

        var questionIds = mine.Answers.Select(a => a.QuestionId).ToList();
        var byId = await _db.Questions
            .AsNoTracking()
            .Where(q => questionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, ct);

        var opponentAnswers = ReadOpponentAnswers(duel, opponent, reveal);

        var rows = mine.Answers
            .OrderBy(a => a.Order)
            .Where(a => byId.ContainsKey(a.QuestionId))
            .Select(a =>
            {
                var question = byId[a.QuestionId];
                var known = opponentAnswers.TryGetValue(a.Order, out var theirs);

                return new DuelQuestionResultDto
                {
                    Order = a.Order,
                    Prompt = question.Prompt,
                    Options = question.Options.ToList(),
                    CorrectIndex = question.CorrectIndex,
                    MyChosenIndex = a.ChosenIndex,
                    MyCorrect = a.IsCorrect,
                    MyElapsedMs = a.ElapsedMs,
                    OpponentChosenIndex = known ? theirs.ChosenIndex : -1,
                    OpponentCorrect = known && theirs.IsCorrect,
                    OpponentElapsedMs = known ? theirs.ElapsedMs : 0
                };
            })
            .ToList();

        var newBadges = new List<BadgeDto>();

        // Nişanlar nəticə ilk dəfə açılanda yoxlanılır — hər iki uşaq öz növbəsində.
        // Məşq duelində <c>ResultSeenAt</c> onsuz da dolu olur, yəni bura düşmür.
        if (duel.Status == DuelStatus.Complete && !duel.IsPractice && mine.ResultSeenAt is null)
        {
            mine.ResultSeenAt = _clock.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync(ct);
            newBadges = await _rewards.EvaluateBadgesAsync(child.Id, ct);
        }

        var opponentScore = OpponentScore(duel, opponent, reveal);

        // Hesab bərabər, qalib isə var — deməli qərarı VAXT verib. Ekranda
        // 3–3 yazılanda uşaq udduğunu anlamır, ona görə bu, ayrıca bayraqdır.
        var decidedBySpeed = duel.Status == DuelStatus.Complete
                             && outcome is DuelOutcome.Win or DuelOutcome.Loss
                             && mine.CorrectCount == opponentScore;

        return new ArenaDuelResultDto
        {
            DuelId = duel.Id,
            Skill = duel.Skill,
            Status = duel.Status,
            Outcome = outcome,
            TotalCount = mine.Answers.Count,
            MyCorrectCount = mine.CorrectCount,
            MyTotalMilliseconds = mine.TotalMilliseconds,
            IsPractice = duel.IsPractice,
            OpponentCorrectCount = opponentScore,
            OpponentTotalMilliseconds = OpponentMilliseconds(duel, opponent, reveal),
            Opponent = duel.IsPractice
                ? PracticeOpponent(child)
                : await BuildOpponentAsync(child.Id, opponent?.ChildProfileId, ct),
            StarsEarned = mine.StarsEarned,
            SpeedBonusStars = mine.SpeedBonusStars,
            DecidedBySpeed = decidedBySpeed,
            RatingBefore = mine.ArenaRatingBefore,
            RatingAfter = duel.Status == DuelStatus.Complete ? mine.ArenaRatingAfter : mine.ArenaRatingBefore,
            Questions = rows,
            NewBadges = newBadges,
            PetMessage = PetVoice.DuelSummary(
                child.LanguageCode,
                outcome,
                mine.CorrectCount,
                opponentScore,
                child.Pet?.Name ?? "Pet",
                decidedBySpeed)
        };
    }

    /// <summary>
    /// Rəqibin sual-sual cavabları, SIRA üzrə. Əsl rəqibdə onlar öz qeydindədir,
    /// məşqdə isə duelin sual sətrində — hər ikisi eyni sıranı daşıyır.
    /// </summary>
    private static Dictionary<int, (int ChosenIndex, bool IsCorrect, int ElapsedMs)> ReadOpponentAnswers(
        Duel duel, DuelEntry? opponent, bool reveal)
    {
        if (!reveal)
            return new Dictionary<int, (int, bool, int)>();

        if (duel.IsPractice)
            return duel.Questions.ToDictionary(
                q => q.Order,
                q => (ChosenIndex: -1, IsCorrect: q.PracticeCorrect, ElapsedMs: q.PracticeElapsedMs));

        return opponent!.Answers.ToDictionary(
            a => a.Order,
            a => (a.ChosenIndex, a.IsCorrect, a.ElapsedMs));
    }

    private static int OpponentScore(Duel duel, DuelEntry? opponent, bool reveal)
    {
        if (!reveal)
            return 0;

        return duel.IsPractice ? duel.Questions.Count(q => q.PracticeCorrect) : opponent!.CorrectCount;
    }

    private static int OpponentMilliseconds(Duel duel, DuelEntry? opponent, bool reveal)
    {
        if (!reveal)
            return 0;

        return duel.IsPractice ? duel.Questions.Sum(q => q.PracticeElapsedMs) : opponent!.TotalMilliseconds;
    }

    /// <summary>
    /// Məşq rəqibinin üzü. Adı AÇIQ şəkildə "Məşq rəqibi"dir və
    /// <see cref="ArenaOpponentDto.IsPractice"/> qoyulur — uşaq qarşısındakının
    /// əsl uşaq olmadığını bilməlidir.
    /// </summary>
    private static ArenaOpponentDto PracticeOpponent(ChildProfile child) => new()
    {
        DisplayName = Localized.T(child.LanguageCode, "Məşq rəqibi", "Practice rival"),
        PetName = "Robo",
        AvatarKey = "avatar-robot",
        Level = 1,
        IsPractice = true
    };

    private async Task<DuelEntry?> LoadOpponentEntryAsync(Guid duelId, Guid childId, CancellationToken ct) =>
        await _db.DuelEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.DuelId == duelId && e.ChildProfileId != childId, ct);

    /// <summary>
    /// Rəqibin uşağa göstərilən üzü. Əsl ad YALNIZ dost olan rəqibdə açılır —
    /// tanımadığı uşaq həmişə pet adı + avatar + səviyyə ilə qalır.
    /// </summary>
    private async Task<ArenaOpponentDto?> BuildOpponentAsync(Guid childId, Guid? opponentId, CancellationToken ct)
    {
        if (opponentId is null)
            return null;

        var opponent = await _db.ChildProfiles
            .AsNoTracking()
            .Include(c => c.Pet)
            .FirstOrDefaultAsync(c => c.Id == opponentId, ct);

        if (opponent is null)
            return null;

        var isFriend = await _db.Friendships
            .AnyAsync(f => f.ChildProfileId == childId
                           && f.FriendChildProfileId == opponent.Id
                           && f.Status == FriendshipStatus.Active, ct);

        return new ArenaOpponentDto
        {
            DisplayName = isFriend ? opponent.DisplayName : null,
            PetName = opponent.Pet?.Name ?? string.Empty,
            AvatarKey = opponent.AvatarKey,
            Level = opponent.Pet?.Level ?? 1,
            IsFriend = isFriend
        };
    }

    private static void EnsureMasteries(ChildProfile child)
    {
        foreach (var skill in Enum.GetValues<SkillArea>())
        {
            if (child.SkillMasteries.All(m => m.Skill != skill))
                child.SkillMasteries.Add(new SkillMastery { Skill = skill, Rating = AdaptiveEngine.StartingRating });
        }
    }

    private static QuestionDto ToDto(Question question) => new()
    {
        Id = question.Id,
        Skill = question.Skill,
        Difficulty = question.Difficulty,
        Prompt = question.Prompt,
        Options = question.Options.ToList(),
        Hint = question.Hint
    };
}
