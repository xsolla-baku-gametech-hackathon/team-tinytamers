using PetPal.Shared.Dtos.Discovery;
using PetPal.Shared.Dtos.Games;
using PetPal.Shared.Dtos.Home;
using PetPal.Shared.Dtos.Learning;
using PetPal.Shared.Dtos.Missions;
using PetPal.Shared.Dtos.Notifications;
using PetPal.Shared.Dtos.PetBrain;
using PetPal.Shared.Dtos.Pets;
using PetPal.Shared.Dtos.Progress;
using PetPal.Shared.Dtos.Rewards;
using PetPal.Shared.Dtos.Social;
using PetPal.Shared.Dtos.Wardrobe;
using PetPal.Shared.Enums;

namespace PetPal.App.Ui.Services;

/// <summary>
/// Uşaq sessiyasına aid bütün endpoint-lər. Ekranlar arasında paylaşıldığı üçün
/// bir klientdə saxlanılır — hər ekran üçün ayrıca DI qeydiyyatı lazım olmur.
/// </summary>
public class GameApiClient : ApiClientBase
{
    public GameApiClient(HttpClient http, AppSession session, Loc loc) : base(http, session, loc) { }

    // ---------- Home ----------
    public Task<ApiResult<HomeStateDto>> GetHomeAsync(CancellationToken ct = default) =>
        GetAsync<HomeStateDto>("api/home", ct);

    // ---------- Pet ----------
    public Task<ApiResult<PetDto>> GetPetAsync(CancellationToken ct = default) =>
        GetAsync<PetDto>("api/pet", ct);

    /// <param name="food">Yalnız <see cref="CareAction.Feed"/> üçün — yemək kodu.</param>
    public Task<ApiResult<CarePetResultDto>> CareAsync(
        CareAction action, string? food = null, CancellationToken ct = default) =>
        PostAsync<CarePetRequest, CarePetResultDto>(
            "api/pet/care", new CarePetRequest { Action = action, Food = food }, ct);

    public Task<ApiResult<List<PetFoodDto>>> GetPetFoodsAsync(CancellationToken ct = default) =>
        GetAsync<List<PetFoodDto>>("api/pet/foods", ct);

    public Task<ApiResult<HatchPetResultDto>> HatchPetAsync(CancellationToken ct = default) =>
        PostAsync<object, HatchPetResultDto>("api/pet/hatch", new object(), ct);

    /// <param name="codes">Taxılacaq əşyaların tam siyahısı — boş siyahı hamısını çıxarır.</param>
    public Task<ApiResult<PetDto>> EquipAccessoriesAsync(List<string> codes, CancellationToken ct = default) =>
        PutAsync<EquipAccessoriesRequest, PetDto>(
            "api/pet/accessories", new EquipAccessoriesRequest { Codes = codes }, ct);

    /// <summary>Növ yalnız görünüşdür — yaş, statlar və əşyalar dəyişmir.</summary>
    public Task<ApiResult<PetDto>> ChangeSpeciesAsync(string species, CancellationToken ct = default) =>
        PutAsync<ChangeSpeciesRequest, PetDto>(
            "api/pet/species", new ChangeSpeciesRequest { Species = species }, ct);

    public Task<ApiResult<PetDto>> RenamePetAsync(string name, CancellationToken ct = default) =>
        PutAsync<RenamePetRequest, PetDto>("api/pet/name", new RenamePetRequest { Name = name }, ct);


    // ---------- Söhbət ----------

    /// <summary>Söhbət otağı açılanda: açıqdırmı, neçə mesaj qalıb, son replikalar.</summary>
    public Task<ApiResult<PetChatStateDto>> GetChatAsync(CancellationToken ct = default) =>
        GetAsync<PetChatStateDto>("api/pet/chat", ct);

    public Task<ApiResult<PetChatReplyDto>> SendChatAsync(string message, CancellationToken ct = default) =>
        PostAsync<PetChatRequest, PetChatReplyDto>("api/pet/chat", new PetChatRequest { Message = message }, ct);

    // ---------- Pet Brain ----------

    /// <summary>Profil, yaddaş, bağ, xarakter və növbəti macəra tövsiyəsi — bir sorğuda.</summary>
    public Task<ApiResult<PetBrainStateDto>> GetPetBrainAsync(CancellationToken ct = default) =>
        GetAsync<PetBrainStateDto>("api/pet-brain", ct);

    public Task<ApiResult<PetBrainOnboardingDto>> GetPetBrainOnboardingAsync(CancellationToken ct = default) =>
        GetAsync<PetBrainOnboardingDto>("api/pet-brain/onboarding", ct);

    public Task<ApiResult<bool>> SubmitPetBrainOnboardingAsync(
        PetBrainOnboardingRequest request, CancellationToken ct = default) =>
        PostAsync<PetBrainOnboardingRequest, bool>("api/pet-brain/onboarding", request, ct);

    public Task<ApiResult<PetBrainSettingsDto>> UpdatePetBrainSettingsAsync(
        UpdatePetBrainSettingsRequest request, CancellationToken ct = default) =>
        PutAsync<UpdatePetBrainSettingsRequest, PetBrainSettingsDto>(
            "api/pet-brain/settings", request, ct);

    /// <param name="templateKey">
    /// Ekranda görünən şablon. Server onu ÖZ tövsiyəsi ilə tutuşdurur — klient
    /// kataloqdan istədiyini seçə bilmir.
    /// </param>
    /// <param name="decisionId">
    /// Ekranda görünən kartın qərar id-si. Server onu YOXLAYIR: yad və ya
    /// köhnəlmiş qərar rədd olunur, uyğun qərar isə məhz görünən kartı
    /// başladır.
    /// </param>
    public Task<ApiResult<PetBrainRunDto>> StartPetBrainRunAsync(
        string? templateKey = null, Guid? decisionId = null, CancellationToken ct = default) =>
        PostAsync<StartPetBrainRunRequest, PetBrainRunDto>(
            "api/pet-brain/runs",
            new StartPetBrainRunRequest { TemplateKey = templateKey, DecisionId = decisionId }, ct);

    /// <summary>
    /// «Başqa fikir» və «sonra».
    ///
    /// <para>Klient burada NƏ şablon, NƏ də bal dəyişikliyi göndərmir — yalnız
    /// serverin verdiyi qərar id-sini. Cavab yeni vəziyyəti qaytarır.</para>
    /// </summary>
    public Task<ApiResult<PetBrainStateDto>> SendPetBrainFeedbackAsync(
        Guid decisionId, PetBrainRecommendationFeedback feedback, CancellationToken ct = default) =>
        PostAsync<PetBrainFeedbackRequest, PetBrainStateDto>(
            "api/pet-brain/recommendation/feedback",
            new PetBrainFeedbackRequest { DecisionId = decisionId, Feedback = feedback }, ct);

    /// <summary>Yenilənmədən sonra macərəni bərpa edir.</summary>
    public Task<ApiResult<PetBrainRunDto>> GetPetBrainRunAsync(Guid runId, CancellationToken ct = default) =>
        GetAsync<PetBrainRunDto>($"api/pet-brain/runs/{runId}", ct);

    /// <param name="stageIndex">
    /// Klientin gördüyü mərhələ. Server öz sayğacı ilə tutuşdurur: uyğun
    /// gəlməsə <c>409</c> qayıdır, yəni iki dəfə basmaq mərhələ atlatmır.
    /// </param>
    /// <param name="revision">
    /// Klientin gördüyü vəziyyət nömrəsi. Serverinki fərqlidirsə <c>409</c>
    /// qayıdır — uşaq macərəni iki cihazda açıbsa, biri digərinin addımını
    /// səssizcə üstələmir.
    /// </param>
    /// <param name="idempotencyKey">
    /// Təkrar göndərilən sorğunun açarı. Zəif şəbəkədə eyni addım iki dəfə
    /// gedə bilər; server ikincisini tətbiq etmir və cari vəziyyəti qaytarır.
    /// </param>
    public Task<ApiResult<PetBrainRunDto>> SubmitPetBrainChoiceAsync(
        Guid runId, int stageIndex, string optionKey, string? nodeId = null,
        int? revision = null, string? idempotencyKey = null, CancellationToken ct = default) =>
        PostAsync<PetBrainChoiceRequest, PetBrainRunDto>(
            $"api/pet-brain/runs/{runId}/choices",
            new PetBrainChoiceRequest
            {
                StageIndex = stageIndex,
                NodeId = nodeId,
                OptionKey = optionKey,
                ClientRevision = revision,
                IdempotencyKey = idempotencyKey
            }, ct);

    /// <summary>Macərəni dayandırır — vəziyyət saxlanılır, mükafat hüququ qalır.</summary>
    public Task<ApiResult<PetBrainResumeDto>> PausePetBrainRunAsync(
        Guid runId, int playedSeconds, CancellationToken ct = default) =>
        PostAsync<PetBrainPauseRequest, PetBrainResumeDto>(
            $"api/pet-brain/runs/{runId}/pause",
            new PetBrainPauseRequest { PlayedSeconds = playedSeconds }, ct);

    /// <summary>Dayandırılmış macərəni son checkpoint-dən davam etdirir.</summary>
    public Task<ApiResult<PetBrainRunDto>> ResumePetBrainRunAsync(
        Guid runId, CancellationToken ct = default) =>
        PostAsync<PetBrainRunDto>($"api/pet-brain/runs/{runId}/resume", ct);

    /// <summary>Davam edilə bilən macəranın kartı; yoxdursa <c>null</c>.</summary>
    public Task<ApiResult<PetBrainResumeDto>> GetPetBrainResumeCardAsync(CancellationToken ct = default) =>
        GetAsync<PetBrainResumeDto>("api/pet-brain/resume", ct);

    /// <summary>
    /// Tapmaca cavabı — YALNIZ seçilmiş elementlərin id-ləri.
    ///
    /// <para>Sıra ardıcıllıq sxemində əhəmiyyətlidir. Doğruluğu server öz
    /// saxladığı həllə qarşı müəyyən edir: burada "düzdür", "bal" və ya
    /// "mükafat" sahəsi ümumiyyətlə yoxdur.</para>
    /// </summary>
    public Task<ApiResult<PetBrainRunDto>> SubmitPetBrainPuzzleAsync(
        Guid runId, int stageIndex, List<string> selectedIds,
        string? nodeId = null, CancellationToken ct = default) =>
        PostAsync<PetBrainChoiceRequest, PetBrainRunDto>(
            $"api/pet-brain/runs/{runId}/choices",
            new PetBrainChoiceRequest
            {
                StageIndex = stageIndex, NodeId = nodeId, SelectedIds = selectedIds
            }, ct);

    /// <summary>İpucu istəyi — mərhələ irəliləmir.</summary>
    public Task<ApiResult<PetBrainRunDto>> RequestPetBrainHintAsync(
        Guid runId, int stageIndex, string? nodeId = null, CancellationToken ct = default) =>
        PostAsync<PetBrainChoiceRequest, PetBrainRunDto>(
            $"api/pet-brain/runs/{runId}/choices",
            new PetBrainChoiceRequest { StageIndex = stageIndex, NodeId = nodeId, RequestHint = true }, ct);

    /// <summary>
    /// Pet-in «birlikdə bitirək» təklifinin qəbulu — ipucunun son pilləsi.
    ///
    /// <para>Server pilləni öz sayğacı ilə yoxlayır; vaxtından əvvəl
    /// göndərilən istək rədd olunur.</para>
    /// </summary>
    public Task<ApiResult<PetBrainRunDto>> AcceptPetBrainAssistAsync(
        Guid runId, int stageIndex, string? nodeId = null, CancellationToken ct = default) =>
        PostAsync<PetBrainChoiceRequest, PetBrainRunDto>(
            $"api/pet-brain/runs/{runId}/choices",
            new PetBrainChoiceRequest { StageIndex = stageIndex, NodeId = nodeId, AcceptAssist = true }, ct);

    /// <summary>Mükafat serverdə DƏQİQ BİR DƏFƏ verilir — təkrar çağırış təhlükəsizdir.</summary>
    public Task<ApiResult<PetBrainRunDto>> CompletePetBrainRunAsync(Guid runId, CancellationToken ct = default) =>
        PostAsync<PetBrainRunDto>($"api/pet-brain/runs/{runId}/complete", ct);

    public Task<ApiResult<PetBrainRunDto>> AbandonPetBrainRunAsync(Guid runId, CancellationToken ct = default) =>
        PostAsync<PetBrainRunDto>($"api/pet-brain/runs/{runId}/abandon", ct);

    /// <summary>
    /// Tapmacanın hekayə rəsmi.
    ///
    /// <para>Ünvan app-in ÖZ, sahiblik yoxlanan endpoint-idir — provayderin
    /// URL-i klientə heç vaxt çatmır. Bayt <c>data:</c> URI-yə çevrilir, çünki
    /// <c>&lt;img src&gt;</c> bearer başlığı daşıya bilmir.</para>
    /// </summary>
    public Task<ApiResult<string>> GetPetBrainIllustrationAsync(Guid puzzleId, CancellationToken ct = default) =>
        GetDataUrlAsync($"api/pet-brain/puzzles/{puzzleId}/illustration", ct);

    /// <summary>
    /// Macəranın cari arxa fonu və ya obrazı.
    ///
    /// <para>Ünvan serverin ÖZ cavabından gəlir (mərhələnin <c>Backdrop</c> və
    /// <c>Portrait</c> sahələri) və içində səhnənin hash-ı var: seçim dəyişəndə
    /// ünvan da dəyişir, ona görə klient köhnə rəsmi göstərmir. Rəsm hələ
    /// çəkilirsə sorğu <c>404</c> alır — ekran deterministik səhnə ilə işləməyə
    /// davam edir.</para>
    /// </summary>
    public Task<ApiResult<string>> GetPetBrainSceneAsync(string assetUrl, CancellationToken ct = default) =>
        GetDataUrlAsync(assetUrl.TrimStart('/'), ct);

    /// <summary>Paltar otağının dizayn studiyası — açıqdırmı, bu gün neçə dizayn qalıb, son dizaynlar.</summary>
    public Task<ApiResult<WardrobeStateDto>> GetWardrobeAsync(CancellationToken ct = default) =>
        GetAsync<WardrobeStateDto>("api/pet/wardrobe", ct);

    /// <summary>
    /// Uşağın paltar arzusu. Cavab dərhal gəlir: filtrin saxladığı arzu yumşaq
    /// mesajla, qəbul edilən isə «tikilir» vəziyyəti ilə — şəkil arxa fonda çəkilir.
    /// </summary>
    public Task<ApiResult<WardrobeDesignDto>> CreateWardrobeDesignAsync(string text, CancellationToken ct = default) =>
        PostAsync<CreateWardrobeDesignRequest, WardrobeDesignDto>(
            "api/pet/wardrobe/designs", new CreateWardrobeDesignRequest { Text = text }, ct);

    /// <summary>Dizaynın şəkli — app-in ÖZ, sahiblik yoxlanan endpoint-indən.</summary>
    public Task<ApiResult<string>> GetWardrobeImageAsync(Guid designId, CancellationToken ct = default) =>
        GetDataUrlAsync($"api/pet/wardrobe/designs/{designId}/image", ct);

    /// <summary>Dizaynı geyindirir; <c>null</c> adi görünüşə qaytarır.</summary>
    public Task<ApiResult<WardrobeStateDto>> EquipWardrobeDesignAsync(Guid? designId, CancellationToken ct = default) =>
        PutAsync<EquipWardrobeDesignRequest, WardrobeStateDto>(
            "api/pet/wardrobe/equipped", new EquipWardrobeDesignRequest { DesignId = designId }, ct);

    public Task<ApiResult<WardrobeStateDto>> DeleteWardrobeDesignAsync(Guid designId, CancellationToken ct = default) =>
        DeleteAsync<WardrobeStateDto>($"api/pet/wardrobe/designs/{designId}", ct);

    /// <summary>
    /// Tamamlanmış macəranın recap vəziyyəti. Video arxa fonda hazırlanır —
    /// ekran bunu seyrək soruşur və video hazır olanda storyboard-ı onunla
    /// əvəz edir.
    /// </summary>
    public Task<ApiResult<PetBrainRecapDto>> GetPetBrainRecapAsync(Guid runId, CancellationToken ct = default) =>
        GetAsync<PetBrainRecapDto>($"api/pet-brain/runs/{runId}/recap", ct);

    /// <summary>
    /// Hazır recap videosunun baytları — app-in ÖZ, sahiblik yoxlanan
    /// endpoint-indən. <c>&lt;video src&gt;</c> bearer başlığı daşıya bilmir,
    /// ona görə bayt adi sorğu ilə alınır.
    /// </summary>
    public Task<ApiResult<byte[]>> GetPetBrainRecapVideoAsync(Guid runId, CancellationToken ct = default) =>
        GetBytesAsync($"api/pet-brain/runs/{runId}/recap/video", ct);

    /// <summary>
    /// «Macəra videoları» rəfi — uşağın bitmiş macəraları, ən yenisi əvvəl.
    /// Rəfi açmaq yeni pullu iş başlatmır.
    /// </summary>
    public Task<ApiResult<List<PetBrainRecapEntryDto>>> GetPetBrainRecapsAsync(CancellationToken ct = default) =>
        GetAsync<List<PetBrainRecapEntryDto>>("api/pet-brain/recaps", ct);

    // ---------- Learning ----------
    public Task<ApiResult<LearningSessionDto>> StartSessionAsync(StartSessionRequest request, CancellationToken ct = default) =>
        PostAsync<StartSessionRequest, LearningSessionDto>("api/learn/sessions", request, ct);

    public Task<ApiResult<AnswerResultDto>> SubmitAnswerAsync(SubmitAnswerRequest request, CancellationToken ct = default) =>
        PostAsync<SubmitAnswerRequest, AnswerResultDto>("api/learn/answers", request, ct);

    public Task<ApiResult<SessionSummaryDto>> CompleteSessionAsync(Guid sessionId, CancellationToken ct = default) =>
        PostAsync<SessionSummaryDto>($"api/learn/sessions/{sessionId}/complete", ct);

    // ---------- Bilik Arenası ----------

    /// <summary>Arena girişi: reytinq, qalan duel sayı, hazır nəticələr.</summary>
    public Task<ApiResult<ArenaStatusDto>> GetArenaStatusAsync(CancellationToken ct = default) =>
        GetAsync<ArenaStatusDto>("api/learn/arena/status", ct);

    /// <summary>Rəqib axtarır; tapılmasa duel "rəqib gözləyir" vəziyyətində qalır.</summary>
    public Task<ApiResult<ArenaDuelDto>> StartDuelAsync(CancellationToken ct = default) =>
        PostAsync<ArenaDuelDto>("api/learn/arena/duels", ct);

    /// <summary>Məşq dueli — rəqib sünidir, reytinq və ulduz dəyişmir.</summary>
    public Task<ApiResult<ArenaDuelDto>> StartPracticeDuelAsync(CancellationToken ct = default) =>
        PostAsync<ArenaDuelDto>("api/learn/arena/practice", ct);

    /// <summary>Dostu birbaşa yarışa çağırır — duel hovuza düşmür.</summary>
    public Task<ApiResult<ArenaDuelDto>> ChallengeFriendAsync(Guid friendChildId, CancellationToken ct = default) =>
        PostAsync<ArenaDuelDto>($"api/learn/arena/challenge/{friendChildId}", ct);

    public Task<ApiResult<ArenaDuelDto>> AcceptChallengeAsync(Guid duelId, CancellationToken ct = default) =>
        PostAsync<ArenaDuelDto>($"api/learn/arena/challenge/{duelId}/accept", ct);

    public Task<ApiResult<bool>> DeclineChallengeAsync(Guid duelId, CancellationToken ct = default) =>
        PostAsync<bool>($"api/learn/arena/challenge/{duelId}/decline", ct);

    public Task<ApiResult<ArenaDuelDto>> GetDuelAsync(Guid duelId, CancellationToken ct = default) =>
        GetAsync<ArenaDuelDto>($"api/learn/arena/duels/{duelId}", ct);

    /// <summary>Rəqib axtarışını dayandırır — duel hovuzda qalmamalıdır.</summary>
    public Task<ApiResult<bool>> CancelDuelSearchAsync(Guid duelId, CancellationToken ct = default) =>
        PostAsync<bool>($"api/learn/arena/duels/{duelId}/cancel", ct);

    public Task<ApiResult<DuelAnswerResultDto>> SubmitDuelAnswerAsync(
        SubmitDuelAnswerRequest request, CancellationToken ct = default) =>
        PostAsync<SubmitDuelAnswerRequest, DuelAnswerResultDto>("api/learn/arena/answers", request, ct);

    /// <summary>Həftəlik liqa cədvəli — bazar ertəsi UTC 00:00-da sıfırlanır.</summary>
    public Task<ApiResult<ArenaLeagueDto>> GetArenaLeagueAsync(CancellationToken ct = default) =>
        GetAsync<ArenaLeagueDto>("api/learn/arena/league", ct);

    public Task<ApiResult<ArenaDuelResultDto>> GetDuelResultAsync(Guid duelId, CancellationToken ct = default) =>
        GetAsync<ArenaDuelResultDto>($"api/learn/arena/duels/{duelId}/result", ct);

    // ---------- Mini oyunlar ----------
    public Task<ApiResult<List<GameCatalogItemDto>>> GetGamesAsync(CancellationToken ct = default) =>
        GetAsync<List<GameCatalogItemDto>>("api/games", ct);

    public Task<ApiResult<UnlockGameResultDto>> UnlockGameAsync(string gameKey, CancellationToken ct = default) =>
        PostAsync<UnlockGameRequest, UnlockGameResultDto>("api/games/unlock", new UnlockGameRequest { GameKey = gameKey }, ct);

    public Task<ApiResult<GameResultDto>> SubmitGameResultAsync(
        SubmitGameResultRequest request, CancellationToken ct = default) =>
        PostAsync<SubmitGameResultRequest, GameResultDto>("api/games/results", request, ct);

    // ---------- Progress & rewards ----------
    public Task<ApiResult<ProgressSummaryDto>> GetProgressAsync(CancellationToken ct = default) =>
        GetAsync<ProgressSummaryDto>("api/progress", ct);

    public Task<ApiResult<List<SkillProgressDto>>> GetSkillsAsync(CancellationToken ct = default) =>
        GetAsync<List<SkillProgressDto>>("api/progress/skills", ct);

    public Task<ApiResult<WalletDto>> GetWalletAsync(CancellationToken ct = default) =>
        GetAsync<WalletDto>("api/rewards/wallet", ct);

    public Task<ApiResult<List<BadgeDto>>> GetBadgesAsync(CancellationToken ct = default) =>
        GetAsync<List<BadgeDto>>("api/rewards/badges", ct);

    // ---------- World & missions ----------
    public Task<ApiResult<WorldStateDto>> GetWorldAsync(CancellationToken ct = default) =>
        GetAsync<WorldStateDto>("api/world", ct);

    public Task<ApiResult<ClaimMissionResultDto>> ClaimMissionAsync(Guid missionId, CancellationToken ct = default) =>
        PostAsync<ClaimMissionResultDto>($"api/world/missions/{missionId}/claim", ct);

    // ---------- Discovery ----------
    public Task<ApiResult<DiscoveryResultDto>> CreateDiscoveryAsync(DiscoveryRequest request, CancellationToken ct = default) =>
        PostAsync<DiscoveryRequest, DiscoveryResultDto>("api/discoveries", request, ct);

    public Task<ApiResult<List<DiscoveryDto>>> GetDiscoveriesAsync(CancellationToken ct = default) =>
        GetAsync<List<DiscoveryDto>>("api/discoveries", ct);

    /// <summary>Şəkil siyahı ilə birlikdə gəlmir — yalnız açılan kəşf üçün alınır.</summary>
    public Task<ApiResult<string>> GetDiscoveryPhotoAsync(Guid discoveryId, CancellationToken ct = default) =>
        GetDataUrlAsync($"api/discoveries/{discoveryId}/photo", ct);

    // ---------- Bildirişlər ----------

    /// <summary>Cihazın push ünvanını qeyd edir — token cihazın öz SDK-sından gəlir.</summary>
    public Task<ApiResult<bool>> RegisterDeviceAsync(string token, string platform, CancellationToken ct = default) =>
        PostAsync<RegisterDeviceRequest, bool>("api/notifications/devices",
            new RegisterDeviceRequest { Token = token, Platform = platform }, ct);

    public Task<ApiResult<bool>> UnregisterDeviceAsync(string token, CancellationToken ct = default) =>
        DeleteAsync<bool>($"api/notifications/devices/{token}", ct);

    // ---------- Social ----------
    public Task<ApiResult<string>> GetFriendCodeAsync(CancellationToken ct = default) =>
        GetAsync<string>("api/social/friend-code", ct);

    /// <summary>Dostlar və gözləyən sorğular — bir sorğuda.</summary>
    public Task<ApiResult<FriendsViewDto>> GetFriendsAsync(CancellationToken ct = default) =>
        GetAsync<FriendsViewDto>("api/social/friends", ct);

    /// <summary>Dostluq SORĞUSU göndərir — dostluq qarşı tərəf qəbul edəndə işləyir.</summary>
    public Task<ApiResult<PendingFriendDto>> AddFriendAsync(string friendCode, CancellationToken ct = default) =>
        PostAsync<AddFriendRequest, PendingFriendDto>("api/social/friends", new AddFriendRequest { FriendCode = friendCode }, ct);

    /// <summary>Gələn sorğuya cavab. Qərar uşağın özünündür — valideyn qarışmır.</summary>
    public Task<ApiResult<bool>> RespondToFriendRequestAsync(
        Guid requesterChildId, bool approve, CancellationToken ct = default) =>
        PostAsync<FriendRequestDecision, bool>(
            $"api/social/friend-requests/{requesterChildId}",
            new FriendRequestDecision { Approve = approve }, ct);

    public Task<ApiResult<bool>> RemoveFriendAsync(Guid friendChildId, CancellationToken ct = default) =>
        DeleteAsync<bool>($"api/social/friends/{friendChildId}", ct);

    public Task<ApiResult<List<TeamMissionDto>>> GetTeamMissionsAsync(CancellationToken ct = default) =>
        GetAsync<List<TeamMissionDto>>("api/social/team-missions", ct);

    public Task<ApiResult<TeamMissionDto>> StartTeamMissionAsync(List<Guid> friendIds, CancellationToken ct = default) =>
        PostAsync<List<Guid>, TeamMissionDto>("api/social/team-missions", friendIds, ct);

    public Task<ApiResult<TeamMissionDto>> RespondToTeamMissionAsync(
        Guid missionId, bool join, CancellationToken ct = default) =>
        PostAsync<TeamMissionDto>($"api/social/team-missions/{missionId}/respond?join={(join ? "true" : "false")}", ct);
}
