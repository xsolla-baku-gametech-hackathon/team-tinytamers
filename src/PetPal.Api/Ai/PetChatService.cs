using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetPal.Api.Common;
using PetPal.Api.Data;
using PetPal.Api.Entities;
using PetPal.Api.PetBrain;
using PetPal.Api.Pets;
using PetPal.Api.Progress;
using PetPal.Shared.Dtos.Pets;
using PetPal.Shared.Enums;

namespace PetPal.Api.Ai;

/// <summary>
/// Pet ilə söhbət.
///
/// <para>Bu, layihədə uşağın yazdığı mətnin model serverinə çatdığı YEGANƏ yerdir.
/// Ona görə axın beş qatdan keçir:</para>
///
/// <list type="number">
///   <item><b>Valideyn açarı</b> — <see cref="ChildProfile.ChatEnabled"/> bağlıdırsa heç nə olmur.</item>
///   <item><b>Gündəlik hədd</b> — <see cref="PetChatOptions.MessagesPerDay"/>.</item>
///   <item><b>Determinist filtr</b> — <see cref="ChatGuard.InspectInput"/>, həmişə işləyir.</item>
///   <item><b>Model yoxlayıcısı</b> — könüllü, <see cref="AiOptions.GuardModel"/>.</item>
///   <item><b>Çıxış filtri</b> — <see cref="ChatGuard.SanitizeReply"/>.</item>
/// </list>
///
/// <para><b>Heç vaxt sındırmır.</b> Model işləmirsə, ləngiyirsə, kvota bitibsə və ya
/// filtr saxlayırsa — pet yenə də danışır, sadəcə qayda əsaslı replika ilə.
/// Uşaq xəta ekranı görmür.</para>
/// </summary>
public class PetChatService : IPetChatService
{
    private readonly AppDbContext _db;
    private readonly IDailyGoalService _dailyGoals;
    private readonly HttpClient _http;
    private readonly AiOptions _ai;
    private readonly PetChatOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<PetChatService> _logger;

    public PetChatService(
        AppDbContext db,
        IDailyGoalService dailyGoals,
        HttpClient http,
        IOptions<AiOptions> ai,
        IOptions<PetChatOptions> options,
        TimeProvider clock,
        ILogger<PetChatService> logger)
    {
        _db = db;
        _dailyGoals = dailyGoals;
        _http = http;
        _ai = ai.Value;
        _options = options.Value;
        _clock = clock;
        _logger = logger;

        // AI sönülü olanda HttpClient heç vaxt işlədilmir, ona görə ünvan da qurulmur.
        if (!_ai.IsEnabled)
            return;

        _http.BaseAddress = new Uri(_ai.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(1, _ai.TimeoutSeconds));

        if (!string.IsNullOrWhiteSpace(_ai.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _ai.ApiKey);
    }

    public async Task<ServiceResult<PetChatStateDto>> GetStateAsync(Guid childId, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .AsNoTracking()
            .Include(c => c.Pet)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child is null)
            return ServiceResult<PetChatStateDto>.NotFound(
                Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        // Yumurta danışmır: ekran «bağlı» vəziyyətini valideyn açarı ilə eyni
        // cür göstərsin deyə burada da qapalı qaytarılır (bax SendAsync).
        var hatched = child.Pet is { HatchedAt: not null };

        var used = await CountTodayAsync(childId, ct);

        var recent = await _db.ChatTurns
            .AsNoTracking()
            .Where(t => t.ChildProfileId == childId)
            .OrderByDescending(t => t.Sequence)
            .Take(_options.HistoryTurns * 4)
            .Select(t => new PetChatTurnDto
            {
                Id = t.Id,
                FromChild = t.FromChild,
                Text = t.Text,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(ct);

        // Baza ən yenidən verir, ekran isə ən köhnədən başlayır.
        recent.Reverse();

        return ServiceResult<PetChatStateDto>.Ok(new PetChatStateDto
        {
            Enabled = child.ChatEnabled && hatched,
            MessagesPerDay = _options.MessagesPerDay,
            MessagesLeftToday = Math.Max(0, _options.MessagesPerDay - used),
            Turns = recent
        });
    }

    public async Task<ServiceResult<PetChatReplyDto>> SendAsync(
        Guid childId, PetChatRequest request, CancellationToken ct = default)
    {
        var child = await _db.ChildProfiles
            .Include(c => c.Pet)
            .FirstOrDefaultAsync(c => c.Id == childId, ct);

        if (child?.Pet is null)
            return ServiceResult<PetChatReplyDto>.NotFound(
                Localized.T("Uşaq profili tapılmadı.", "Child profile not found."));

        // 1. Valideyn açarı.
        if (!child.ChatEnabled)
            return ServiceResult<PetChatReplyDto>.Forbidden(PetVoice.ChatDisabled(child.LanguageCode));

        // 1b. YUMURTA DANIŞMIR. Söhbət də qulluq kimi pet çıxandan sonra
        //     açılan özəllikdir. Ev ekranı balonu onsuz da toxunulmaz edir,
        //     amma /chat ünvanına birbaşa keçmək olur (veb hostda ünvan sətri
        //     var) — qayda serverdə də durmalıdır, yoxsa yumurta cavab verir.
        if (child.Pet.HatchedAt is null)
            return ServiceResult<PetChatReplyDto>.Forbidden(PetVoice.StillAnEgg(child.LanguageCode));

        var now = _clock.GetUtcNow().UtcDateTime;
        PetProgression.Refresh(child.Pet, now);

        // 2. Gündəlik hədd. Aşılıbsa mesaj SAXLANMIR — əks halda hədd özünü
        //    yeyərdi və valideyn panelində cavabsız sətirlər yığılardı.
        var used = await CountTodayAsync(childId, ct);
        if (used >= _options.MessagesPerDay)
            return ServiceResult<PetChatReplyDto>.Ok(new PetChatReplyDto
            {
                Reply = PetVoice.ChatLimitReached(child.LanguageCode),
                FromAi = false,
                MessagesLeftToday = 0
            });

        var message = (request.Message ?? string.Empty).Trim();

        // 3. Determinist filtr — açar, şəbəkə və kvota tələb etmir.
        var blocked = ChatGuard.InspectInput(message, _options.MaxMessageLength);
        if (blocked != ChatBlockReason.None)
        {
            var refusal = PetVoice.ChatBlocked(child.LanguageCode, blocked, child.Pet.Name);

            // Saxlanılan mətn kəsilir: filtr onu rədd etsə də valideyn nə
            // yazıldığını görməlidir, amma baza sütunu daşmamalıdır.
            var blockedAt = await NextSequenceAsync(childId, ct);
            Record(childId, blockedAt, fromChild: true, Clip(message), fromAi: false, now, blocked.ToString());
            Record(childId, blockedAt + 1, fromChild: false, refusal, fromAi: false, now, null);
            await _db.SaveChangesAsync(ct);

            return ServiceResult<PetChatReplyDto>.Ok(new PetChatReplyDto
            {
                Reply = refusal,
                FromAi = false,
                MessagesLeftToday = Math.Max(0, _options.MessagesPerDay - used - 1)
            });
        }

        var mood = PetProgression.MoodFor(child.Pet);
        var reply = await GenerateAsync(child, message, mood, ct);

        // Sıra nömrəsi model çağırışından SONRA alınır — oxumaqla yazmaq
        // arasındakı pəncərə mümkün qədər dar olsun deyə. Eyni uşaqdan paralel
        // sorğular praktikada olmur (bir cihaz + dəqiqəlik limit), olsa da
        // nəticə yalnız həmin anın daxilində sıranın qarışmasıdır.
        var sequence = await NextSequenceAsync(childId, ct);

        Record(childId, sequence, fromChild: true, message, fromAi: false, now, null);
        Record(childId, sequence + 1, fromChild: false, reply.Text, reply.FromAi, now, null);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<PetChatReplyDto>.Ok(new PetChatReplyDto
        {
            Reply = reply.Text,
            FromAi = reply.FromAi,
            MessagesLeftToday = Math.Max(0, _options.MessagesPerDay - used - 1)
        });
    }

    /// <summary>Cavabı yaradır; hər uğursuzluqda qayda əsaslı mətnə düşür.</summary>
    /// <summary>
    /// Söhbətə daşınan BİR xatirə cümləsi.
    ///
    /// <para>Yaddaş sətri strukturludur (açar + dəyər); cümlə render zamanı
    /// uşağın dilində qurulur. Ona görə burada model üçün sərbəst mətn yaranmır
    /// və dil dəyişəndə xatirə də tərcümə olunur.</para>
    ///
    /// <para>Yaddaş yoxdursa boş sətir qayıdır — pet uydurmur.</para>
    /// </summary>
    private async Task<string> MemoryLineAsync(ChildProfile child, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        var memories = await _db.PetMemories
            .AsNoTracking()
            .Where(m => m.ChildProfileId == child.Id)
            .ToListAsync(ct);

        var memory = MemoryPolicy.Select(memories, now, limit: 1).FirstOrDefault();

        return memory is null
            ? string.Empty
            : MemoryPolicy.Render(memory, child.LanguageCode, child.Pet!.Name);
    }

    private async Task<(string Text, bool FromAi)> GenerateAsync(
        ChildProfile child, string message, PetMood mood, CancellationToken ct)
    {
        var fallback = PetVoice.ChatFallback(child.LanguageCode, mood);

        if (!_ai.IsEnabled)
            return (fallback, false);

        // 4. Model yoxlayıcısı (könüllü).
        if (!await IsInputSafeByModelAsync(message, ct))
        {
            _logger.LogDebug("Söhbət mesajını model yoxlayıcısı saxladı.");
            return (PetVoice.ChatBlocked(child.LanguageCode, ChatBlockReason.Injection, child.Pet!.Name), false);
        }

        var goal = await _dailyGoals.GetOrCreateTodayAsync(child, ct);

        var context = new PetVoiceContext(
            Language: child.LanguageCode,
            ChildName: child.DisplayName,
            PetName: child.Pet!.Name,
            Mood: mood,
            Level: child.Pet.Level,
            Happiness: child.Pet.Happiness,
            Energy: child.Pet.Energy,
            Fullness: child.Pet.Fullness,
            Cleanliness: child.Pet.Cleanliness,
            StreakDays: child.StreakDays,
            GoalCompleted: goal.Completed,
            GoalTarget: goal.Target);

        var history = await RecentHistoryAsync(child.Id, ct);

        // Xarakter və yaddaş ORTAQ mənbədəndir: söhbətdəki pet ilə Pet Brain
        // ekranındakı pet eyni olmalıdır. Xatirə cümləsi serverin strukturlu
        // faktından qurulur — uşağın yazdığı mətn buraya heç vaxt düşmür.
        var memoryLine = await MemoryLineAsync(child, ct);

        var raw = await OpenAiChat.CompleteAsync(
            _http,
            _ai.EffectiveChatModel,
            PetChatPrompt.Build(context, child.Personality, memoryLine, history, message),
            _ai.ChatMaxOutputTokens,
            temperature: 0.7,
            _logger,
            ct,
            _ai.ReasoningEffort);

        // 5. Çıxış filtri.
        var clean = ChatGuard.SanitizeReply(raw);
        return clean is null ? (fallback, false) : (clean, true);
    }

    /// <summary>
    /// Uşağın mesajını təhlükəsizlik modelinə verir.
    ///
    /// <para>Prompt Guard mətn deyil, <b>ehtimal balı</b> qaytarır — məsələn
    /// <c>"0.00045"</c> (zərərsiz) və ya <c>"0.99957"</c> (hücum). Bal
    /// <see cref="AiOptions.GuardThreshold"/>-u keçəndə mesaj saxlanılır.</para>
    ///
    /// <para><b>Şübhə halında BURAXIR.</b> Model təyin olunmayıbsa, cavab vermirsə
    /// və ya cavab rəqəm deyilsə mesaj keçir — çünki determinist filtr onsuz da
    /// işləyib və yoxlayıcının nasazlığı söhbəti tamamilə dayandırmamalıdır.
    /// Əsl müdafiə qatlar toplusudur, tək model deyil.</para>
    ///
    /// <para><b>Bu qat ingilis dili üçündür.</b> Ölçmə göstərdi ki, model
    /// Azərbaycan dilindəki hücumların çoxunu görmür — bax
    /// <see cref="AiOptions.GuardModel"/> sənədi.</para>
    /// </summary>
    private async Task<bool> IsInputSafeByModelAsync(string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_ai.GuardModel))
            return true;

        var verdict = await OpenAiChat.CompleteAsync(
            _http,
            _ai.GuardModel,
            [OpenAiChat.Message.User(message)],
            maxTokens: 16,
            temperature: 0,
            _logger,
            ct);

        if (string.IsNullOrWhiteSpace(verdict))
            return true;

        if (!double.TryParse(verdict.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var score))
        {
            _logger.LogDebug("Yoxlayıcının cavabı rəqəm deyil: {Verdict}", verdict);
            return true;
        }

        return score < _ai.GuardThreshold;
    }

    private async Task<List<(bool FromChild, string Text)>> RecentHistoryAsync(Guid childId, CancellationToken ct)
    {
        var rows = await _db.ChatTurns
            .AsNoTracking()
            .Where(t => t.ChildProfileId == childId && t.BlockedReason == null)
            .OrderByDescending(t => t.Sequence)
            .Take(_options.HistoryTurns)
            .Select(t => new { t.FromChild, t.Text })
            .ToListAsync(ct);

        rows.Reverse();
        return [.. rows.Select(r => (r.FromChild, r.Text))];
    }

    /// <summary>Növbəti sıra nömrəsi. Sayğac deyil, MAKSİMUM+1 — silinmiş sətir sıranı pozmasın.</summary>
    private async Task<int> NextSequenceAsync(Guid childId, CancellationToken ct)
    {
        var last = await _db.ChatTurns
            .Where(t => t.ChildProfileId == childId)
            .MaxAsync(t => (int?)t.Sequence, ct);

        return (last ?? 0) + 1;
    }

    private Task<int> CountTodayAsync(Guid childId, CancellationToken ct)
    {
        // Gün UTC-yə görə hesablanır — DailyGoal ilə eyni konvensiya.
        var dayStart = _clock.GetUtcNow().UtcDateTime.Date;

        return _db.ChatTurns
            .Where(t => t.ChildProfileId == childId && t.FromChild && t.CreatedAt >= dayStart)
            .CountAsync(ct);
    }

    private void Record(
        Guid childId, int sequence, bool fromChild, string text, bool fromAi, DateTime now, string? blockedReason) =>
        _db.ChatTurns.Add(new ChatTurn
        {
            ChildProfileId = childId,
            Sequence = sequence,
            FromChild = fromChild,
            Text = text,
            FromAi = fromAi,
            BlockedReason = blockedReason,
            CreatedAt = now
        });

    /// <summary>Baza sütunu 400 simvoldur; filtrin saxladığı mətn ondan uzun ola bilər.</summary>
    private static string Clip(string text) => text.Length <= 400 ? text : text[..400];
}
