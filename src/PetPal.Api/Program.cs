using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PetPal.Api.Ai;
using PetPal.Api.Common;
using PetPal.Api.Auth;
using PetPal.Api.Data;
using PetPal.Api.Discoveries;
using PetPal.Api.Entities;
using PetPal.Api.Games;
using PetPal.Api.Home;
using PetPal.Api.Hosting;
using PetPal.Api.Learning;
using PetPal.Api.Learning.Arena;
using PetPal.Api.Missions;
using PetPal.Api.Notifications;
using PetPal.Api.Realtime;
using PetPal.Api.Parent;
using PetPal.Api.PetBrain;
using PetPal.Api.PetBrain.Mind;
using PetPal.Api.PetBrain.Media;
using PetPal.Api.PetBrain.Puzzles;
using PetPal.Api.PetBrain.Recap;
using PetPal.Api.PetBrain.Story;
using PetPal.Api.Pets;
using PetPal.Api.Progress;
using PetPal.Api.Rewards;
using PetPal.Api.Security;
using PetPal.Api.Social;
using PetPal.Shared.Dtos;
using PetPal.Shared.Enums;
using Scalar.AspNetCore;

var runMode = HostRunModeParser.Parse(args);
var builder = WebApplication.CreateBuilder(HostRunModeParser.StripFlags(args));

builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Konteyner platformaları (Railway, Render, Fly, Cloud Run) dinləniləcək portu PORT
// dəyişəni ilə verir və proxy-ni ona yönləndirir. ASPNETCORE_URLS açıq verilibsə,
// o üstündür — yəni bu yalnız boşluğu doldurur, mövcud ayarı əvəz etmir.
var listenPort = builder.Configuration["PORT"];
var explicitUrls = builder.Configuration["ASPNETCORE_URLS"] ?? builder.Configuration["urls"];
if (!string.IsNullOrWhiteSpace(listenPort) && string.IsNullOrWhiteSpace(explicitUrls))
    builder.WebHost.UseUrls($"http://+:{listenPort}");

// ---------- Verilənlər bazası ----------
var connectionString = DatabaseConnectionString.Resolve(builder.Configuration);
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Baza əlaqəsi təyin olunmalıdır: ConnectionStrings:DefaultConnection (appsettings.local.json və ya environment variable) və ya DATABASE_URL.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// ---------- Identity (yalnız valideyn hesabları) ----------
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ---------- JWT ----------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key konfiqurasiyada təyin olunmalı və ən azı 32 simvol olmalıdır.");

// Development üçün hazır açar repoda saxlanılır; produksiyada mütləq override olunmalıdır.
const string developmentJwtKey = "petpal-development-only-signing-key-do-not-use-in-production";
if (!builder.Environment.IsDevelopment() && jwtOptions.Key == developmentJwtKey)
    throw new InvalidOperationException("Produksiyada Jwt:Key environment variable ilə override olunmalıdır.");

var tokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = jwtOptions.Issuer,
    ValidateAudience = true,
    ValidAudience = jwtOptions.Audience,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
    ClockSkew = TimeSpan.FromSeconds(30)
};
builder.Services.AddSingleton(tokenValidationParameters);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = tokenValidationParameters;

        // Brauzerin WebSocket-i Authorization başlığı DAŞIYA BİLMİR, ona görə
        // SignalR tokeni sorğu sətrində göndərir. Bu qayda yalnız hub
        // marşrutlarına aiddir — adi API endpoint-ləri başlıq tələb etməyə davam edir.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.Child, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(PetPalClaims.ProfileKindClaim, ProfileKind.Child.ToString()))
    .AddPolicy(AuthorizationPolicies.Parent, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(PetPalClaims.ProfileKindClaim, ProfileKind.Parent.ToString()));

// ---------- Rate limiting ----------
var authPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:Auth:PermitLimit") ?? 20;
var authWindowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:Auth:WindowSeconds") ?? 60;
var socialPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:Social:PermitLimit") ?? 60;
var socialWindowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:Social:WindowSeconds") ?? 60;
// Valideyn qapısı: PIN dörd rəqəmdir, yəni 10 000 variant var. Sürət limiti
// olmasa uşaq (və ya skript) onları sadəcə sıra ilə yoxlayardı. Beş dəqiqədə
// beş cəhd səhv yazan valideynə mane olmur, sıra ilə yoxlamağı isə mənasız edir.
var gatePermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:ParentGate:PermitLimit") ?? 5;
var gateWindowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:ParentGate:WindowSeconds") ?? 300;
var uploadPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:Upload:PermitLimit") ?? 10;
var uploadWindowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:Upload:WindowSeconds") ?? 60;
var chatPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:Chat:PermitLimit") ?? 6;
var chatWindowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:Chat:WindowSeconds") ?? 60;
var arenaPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:Arena:PermitLimit") ?? 60;
var arenaWindowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:Arena:WindowSeconds") ?? 60;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromSeconds(authWindowSeconds),
                QueueLimit = 0
            }));

    options.AddPolicy("social", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst(PetPalClaims.ChildIdClaim)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = socialPermitLimit,
                Window = TimeSpan.FromSeconds(socialWindowSeconds),
                QueueLimit = 0
            }));

    // Bölgü HESAB başınadır, IP başına yox: qapını sındırmağa çalışan uşaq
    // valideynin öz cihazındadır, yəni IP eyni qalır və başqa ailələri
    // cəzalandırmamalıdır.
    //
    // Açara PROFİL TİPİ də qatılır. Səbəb ölçüldü: sürət limiti avtorizasiyadan
    // ƏVVƏL işləyir, uşaq sessiyasının `NameIdentifier`-i isə valideynin öz
    // id-sidir. Tək açarla uşaq endpoint-i döyəcləyib (özü 403 alsa da)
    // valideynin büdcəsini yandırır və onu 5 dəqiqəlik öz bölməsindən
    // kilidləyirdi. İndi uşaq sessiyası öz səbətindədir.
    options.AddPolicy("parent-gate", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            (httpContext.User.GetUserId()?.ToString()
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown")
                + ":" + httpContext.User.GetProfileKind(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = gatePermitLimit,
                Window = TimeSpan.FromSeconds(gateWindowSeconds),
                QueueLimit = 0
            }));

    // Söhbətin öz limiti var, çünki hər mesaj model çağırışıdır: pulsuz
    // səviyyədə darboğaz sorğu sayı deyil, DƏQİQƏLİK TOKEN həddidir. Gündəlik
    // hədd ayrıca yerdədir (PetChatOptions) — bu limit isə ard-arda basmağı
    // dayandırır. Bölgü uşaq başınadır, IP başına yox.
    options.AddPolicy("chat", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst(PetPalClaims.ChildIdClaim)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = chatPermitLimit,
                Window = TimeSpan.FromSeconds(chatWindowSeconds),
                QueueLimit = 0
            }));

    // Arenanın öz limiti var: duel uyğunlaşdırması bir neçə cədvələ toxunur və
    // "Yarış" düyməsi ard-arda basıla bilər. Bölgü uşaq başınadır, IP başına yox —
    // eyni evdəki ikinci uşaq birincinin limitindən əziyyət çəkməməlidir.
    options.AddPolicy("arena", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst(PetPalClaims.ChildIdClaim)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = arenaPermitLimit,
                Window = TimeSpan.FromSeconds(arenaWindowSeconds),
                QueueLimit = 0
            }));

    // Şəkil yükləmə ayrıca və daha sıx limitlənir: bir sorğu 30 MB fayl (JSON
    // gövdəsində ~40 MB) gətirə bilər, yəni ard-arda gələn bir neçə yükləmə
    // kiçik instansiyanın yaddaşını doldura bilər. Bölgü uşaq başınadır, IP
    // başına yox — eyni ailədəki ikinci uşaq birincinin limitindən əziyyət
    // çəkməməlidir.
    options.AddPolicy("upload", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst(PetPalClaims.ChildIdClaim)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = uploadPermitLimit,
                Window = TimeSpan.FromSeconds(uploadWindowSeconds),
                QueueLimit = 0
            }));
});

// ---------- Tətbiq servisləri ----------
builder.Services.Configure<DiscoveryOptions>(builder.Configuration.GetSection(DiscoveryOptions.SectionName));

// Kəşf şəkli JSON gövdəsinin içində base64 kimi gəlir: 30 MB fayl ≈ 40 MB gövdə.
// Kestrel-in defolt limiti isə ~28.6 MB-dır — yəni yalnız MaxPhotoBytes-i
// artırmaq kifayət etmir, sorğu app-in yoxlamasına ÇATMADAN 413 alardı və
// uşaq səbəbi görünməyən xəta ilə qalardı. Limit həmin dəyərdən hesablanır ki,
// ikisi bir-birindən ayrı düşməsin.
builder.WebHost.ConfigureKestrel(options =>
{
    var maxPhotoBytes = builder.Configuration
        .GetSection(DiscoveryOptions.SectionName)
        .GetValue<int?>(nameof(DiscoveryOptions.MaxPhotoBytes)) ?? new DiscoveryOptions().MaxPhotoBytes;

    // base64 həcmi ~4/3 böyüdür; 2 MB isə JSON sahələri və başlıqlar üçün ehtiyatdır.
    options.Limits.MaxRequestBodySize = maxPhotoBytes * 4L / 3 + 2 * 1024 * 1024;
});
builder.Services.Configure<ScreenTimeOptions>(builder.Configuration.GetSection(ScreenTimeOptions.SectionName));
builder.Services.Configure<ArenaOptions>(builder.Configuration.GetSection(ArenaOptions.SectionName));

// ---------- AI (könüllü) ----------
// Standart olaraq söndürülüdür: app model serveri olmadan da tam işləməlidir.
// Açmaq üçün appsettings.local.json-da Ai:Provider = "OpenAiCompatible".
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.AddMemoryCache();

var aiOptions = builder.Configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
if (aiOptions.IsEnabled)
    builder.Services.AddHttpClient<IPetVoiceGenerator, AiPetVoiceGenerator>();
else
    builder.Services.AddSingleton<IPetVoiceGenerator, RuleBasedPetVoiceGenerator>();

// Söhbət AI-dan asılı olmadan qeydiyyatdan keçir: model sönülü olsa da pet
// qayda əsaslı replika ilə cavab verməlidir. HttpClient yalnız AI açıq
// olanda ünvan alır — bax PetChatService konstruktoru.
builder.Services.Configure<PetChatOptions>(builder.Configuration.GetSection(PetChatOptions.SectionName));
builder.Services.AddHttpClient<IPetChatService, PetChatService>();

// Testlər saatı sabitləyə bilsin deyə bütün servislər TimeProvider-dən istifadə edir.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IRewardService, RewardService>();
builder.Services.AddScoped<MissionService>();
builder.Services.AddScoped<IMissionService>(sp => sp.GetRequiredService<MissionService>());
builder.Services.AddScoped<IMissionProgressTracker>(sp => sp.GetRequiredService<MissionService>());

builder.Services.AddScoped<IPetService, PetService>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IQuestionSelector, QuestionSelector>();
builder.Services.AddScoped<IDailyGoalService, DailyGoalService>();
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<ILearningService, LearningService>();
builder.Services.AddSignalR();

// Presence yaddaşdadır, ona görə SINGLETON: hər sorğuda yenidən qurulsa
// kimin onlayn olduğu heç vaxt bilinməzdi.
builder.Services.AddSingleton<IPresenceTracker, PresenceTracker>();
builder.Services.AddScoped<ILiveNotifier, LiveNotifier>();
builder.Services.AddScoped<IArenaStandings, ArenaStandings>();
builder.Services.AddScoped<IArenaService, ArenaService>();
// ---------- Push bildirişləri ----------
// Standart olaraq SÖNÜKDÜR: konfiqurasiya olmadan app tam işləyir, sadəcə
// bildiriş göndərmir — AI dialoqu və söhbət qatları ilə eyni qayda.
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));

var notificationOptions = builder.Configuration
    .GetSection(NotificationOptions.SectionName)
    .Get<NotificationOptions>() ?? new NotificationOptions();

if (notificationOptions.IsConfigured)
{
    builder.Services.AddSingleton(sp => new FirebaseAccessToken(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("fcm-oauth"),
        sp.GetRequiredService<TimeProvider>()));

    builder.Services.AddHttpClient<INotificationSender, FirebaseNotificationSender>();
}
else
{
    builder.Services.AddSingleton<INotificationSender, NullNotificationSender>();
}

builder.Services.AddHttpClient();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Kəşf şəkillərinin saxlanması. Obyekt saxlama YALNIZ konfiqurasiya olunubsa
// işə düşür — əks halda app diskə yazır və lokal iş üçün heç nə tələb olunmur.
// Konteynerdə disk müvəqqətidir: ya S3 (Discovery:S3:Bucket), ya da kalıcı
// volume (Discovery:StorageRoot) qoşulmalıdır, yoxsa şəkillər deploy-da itir.
var s3Options = builder.Configuration
    .GetSection(DiscoveryOptions.SectionName)
    .GetSection(nameof(DiscoveryOptions.S3))
    .Get<S3StorageOptions>() ?? new S3StorageOptions();

if (s3Options.IsConfigured)
{
    builder.Services.AddSingleton<IAmazonS3>(_ =>
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = s3Options.ForcePathStyle,
            AuthenticationRegion = s3Options.Region
        };

        // AWS-dən başqa provayderlər (R2, B2, MinIO) öz ünvanını istəyir.
        if (!string.IsNullOrWhiteSpace(s3Options.ServiceUrl))
            config.ServiceURL = s3Options.ServiceUrl;
        else
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(s3Options.Region);

        return new AmazonS3Client(s3Options.AccessKey, s3Options.SecretKey, config);
    });

    builder.Services.AddSingleton<IDiscoveryPhotoStore, S3DiscoveryPhotoStore>();
}
else
{
    builder.Services.AddSingleton<IDiscoveryPhotoStore, LocalDiscoveryPhotoStore>();
}

// ---------- Pet Brain (Adaptive Pet Director) ----------
// Deterministik qat HƏMİŞƏ qeydiyyatdadır: qərar, çətinlik, mükafat və mətn
// modelsiz də tam işləyir. Model yalnız BAŞLIQ və GİRİŞ cümləsini
// zənginləşdirir və o da ayrıca açarla açılır.
builder.Services.Configure<PetBrainOptions>(builder.Configuration.GetSection(PetBrainOptions.SectionName));
builder.Services.Configure<PetBrainV2Options>(builder.Configuration.GetSection(PetBrainV2Options.SectionName));

var petBrainOptions = builder.Configuration
    .GetSection(PetBrainOptions.SectionName)
    .Get<PetBrainOptions>() ?? new PetBrainOptions();

if (petBrainOptions.UseAiNarrative && aiOptions.IsEnabled)
    builder.Services.AddHttpClient<IExperienceNarrativeProvider, AiExperienceNarrativeProvider>();
else
    builder.Services.AddSingleton<IExperienceNarrativeProvider, TemplateNarrativeProvider>();

// Tapmaca generatoru SAFDIR (I/O, saat və şəbəkə yoxdur) — ona görə singleton.
builder.Services.AddSingleton<IPersonalizedPuzzleGenerator, DeterministicPuzzleGenerator>();

// ---------- Media (rəsm + recap videosu) ----------
// Standart PROVAYDERSİZDİR: heç bir xarici çağırış getmir, heç nə xərclənmir
// və hər şey deterministik ehtiyatla tam işləyir. Açar verilməsə də app
// qalxır — bu, sınaq deyil, məhsul qərarıdır.
builder.Services.Configure<PetBrainMediaOptions>(
    builder.Configuration.GetSection(PetBrainMediaOptions.SectionName));
builder.Services.Configure<RunwayOptions>(builder.Configuration.GetSection(RunwayOptions.SectionName));

var mediaOptions = builder.Configuration
    .GetSection(PetBrainMediaOptions.SectionName)
    .Get<PetBrainMediaOptions>() ?? new PetBrainMediaOptions();

builder.Services.AddSingleton<MediaCircuitBreaker>();
builder.Services.AddSingleton<PetBrainMediaCostPolicy>();

if (mediaOptions.Provider == PetBrainMediaProvider.Runway)
    builder.Services.AddHttpClient<IRunwayTaskClient, RunwayTaskClient>();

// Rəsm ATMOSFERDİR: düyünlər, qaydalar, toxunuş hədəfləri və cavab
// deterministik overlay-dədir. Provayder nə seçilirsə seçilsin, tapmaca
// dəyişmir.
switch (mediaOptions.Provider)
{
    case PetBrainMediaProvider.Runway:
        builder.Services.AddSingleton<IPuzzleIllustrationProvider, RunwayPuzzleIllustrationProvider>();
        break;

    // Köhnə OpenAI-uyğun adapter SİLİNMİR, amma yalnız AÇIQ seçiləndə işləyir.
    // Onun video qatı yoxdur — recap deterministik qalır.
    case PetBrainMediaProvider.OpenAiCompatibleImage
        when petBrainOptions.UseAiIllustration && !string.IsNullOrWhiteSpace(petBrainOptions.IllustrationModel):
        builder.Services.AddHttpClient<IPuzzleIllustrationProvider, AiPuzzleIllustrationProvider>();
        break;

    default:
        builder.Services.AddSingleton<IPuzzleIllustrationProvider, DisabledPuzzleIllustrationProvider>();
        break;
}

builder.Services.AddSingleton<IPuzzleIllustrationStore, LocalPuzzleIllustrationStore>();
builder.Services.AddSingleton<PuzzleIllustrationQueue>();
builder.Services.AddScoped<PuzzleIllustrationCoordinator>();
builder.Services.AddHostedService<PuzzleIllustrationWorker>();

// ---------- Macəranın 10 saniyəlik recap-ı ----------
// Video PREZENTASİYADIR: seçimləri, xassələri, mükafatı və növbəti tövsiyəni
// dəyişə bilmir. Altyazılar serverin saxladığı HƏQİQİ seçimlərdən qurulur.
if (mediaOptions.Provider == PetBrainMediaProvider.Runway)
    builder.Services.AddSingleton<IRecapVideoProvider, RunwayRecapVideoProvider>();
else
    builder.Services.AddSingleton<IRecapVideoProvider, DisabledRecapVideoProvider>();

builder.Services.AddSingleton<IRecapVideoStore, LocalRecapVideoStore>();
builder.Services.AddSingleton<RecapQueue>();
builder.Services.AddScoped<IRecapSpecFactory, RecapSpecFactory>();
builder.Services.AddScoped<RecapCoordinator>();
builder.Services.AddHostedService<RecapWorker>();

builder.Services.AddSingleton<DeclinedRecommendations>();
builder.Services.AddSingleton<PetBrainTelemetry>();
builder.Services.AddScoped<PetMindContextBuilder>();
builder.Services.AddScoped<TraitDailyLedger>();
builder.Services.AddScoped<IBehaviorTracker, BehaviorTracker>();
builder.Services.AddScoped<IPetBrainService, PetBrainService>();

builder.Services.AddScoped<IDiscoveryService, DiscoveryService>();
builder.Services.AddScoped<IParentService, ParentService>();
builder.Services.AddScoped<IParentGateService, ParentGateService>();
builder.Services.AddScoped<IHomeService, HomeService>();

builder.Services.AddScoped<SocialService>();
builder.Services.AddScoped<ISocialService>(sp => sp.GetRequiredService<SocialService>());
builder.Services.AddScoped<ITeamMissionTracker>(sp => sp.GetRequiredService<SocialService>());

// DTO-lardakı DataAnnotations qaydaları minimal API endpoint-lərində avtomatik yoxlanılır;
// bu olmasa [Required]/[Range] atributları yalnız sənəd olaraq qalır.
builder.Services.AddValidation();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

// Mobil app fərqli origin-dən gəlir; Blazor Hybrid WebView də daxil olmaqla.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    else
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

if (builder.Configuration.GetValue("Database:MigrateOnStartup", true) || runMode == HostRunMode.MigrateOnly)
    await DatabaseBootstrapper.InitializeAsync(app.Services);

if (runMode == HostRunMode.MigrateOnly)
    return;

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

    // Pozuq JSON serverin nasazlığı deyil, sorğunun nasazlığıdır. Bunu 500 kimi
    // qaytarmaq həm səhv siqnal verir, həm də monitorinqdə yalançı həyəcan yaradır.
    // Minimal API JSON parse xətasını BadHttpRequestException ilə bükür.
    var statusCode = exception switch
    {
        BadHttpRequestException bad => bad.StatusCode,
        JsonException => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError
    };

    context.Response.StatusCode = statusCode;

    await context.Response.WriteAsJsonAsync(new ApiErrorResponse
    {
        Message = statusCode == StatusCodes.Status500InternalServerError
            ? Localized.T("Gözlənilməz xəta baş verdi. Bir azdan yenidən cəhd edin.", "Something went wrong. Try again in a moment.")
            : Localized.T("Sorğunun formatı düzgün deyil.", "The request format is not valid.")
    });
}));

// İnterfeysin dili hər sorğuda Accept-Language başlığı ilə gəlir — klient onu
// özü göndərir. Uşaq profili məlum olmayan mesajlar (giriş, qeydiyyat, "profil
// tapılmadı") dilini məhz buradan alır.
//
// Yalnız UI mədəniyyəti dəyişir, formatlaşdırma invariant qalır: əks halda
// rəqəm və tarixlərin yazılışı da dilə görə sürüşərdi.
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture, new CultureInfo(Localized.Azerbaijani)),
    SupportedCultures = [CultureInfo.InvariantCulture],
    SupportedUICultures = [.. Localized.Supported.Select(code => new CultureInfo(code))]
});

app.UseCors();
app.UseAuthentication();

// Limiter autentifikasiyadan SONRA işləməlidir: "social" və "upload" siyasətləri
// bölgünü uşaq id-sinə görə aparır, HttpContext.User isə yalnız burada dolur.
// Əvvəl limiter birinci idi — claim boş qalırdı və hər iki siyasət səssizcə
// IP-yə görə bölürdü, yəni eyni evdəki (və ya eyni NAT arxasındakı) bütün
// uşaqlar bir səbəti bölüşürdü.
//
// Authorization-dan ƏVVƏLdir ki, tokensiz sel də limitə düşsün: authorization
// sorğunu 401 ilə kəssəydi, limiter heç işə düşməzdi.
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("AI Pets for Kids API"));
}

app.MapHealthChecks("/health");

// Container orkestratoru (Azure Container Apps) üçün iki ayrı siqnal:
//  • /health/live  — yalnız proses yoxlanılır. Baza qısa müddət əlçatmaz olanda
//    konteyner lazımsız yerə restart edilməsin deyə asılılıqlar bura daxil deyil.
//  • /health/ready — baza da hazırdır; hazır olmayan replica trafikdən çıxarılır.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

app.MapAuthEndpoints();
app.MapHomeEndpoints();
app.MapPetEndpoints();
app.MapGameEndpoints();
app.MapLearningEndpoints();
app.MapProgressEndpoints();
app.MapMissionEndpoints();
app.MapRewardEndpoints();
app.MapDiscoveryEndpoints();
app.MapSocialEndpoints();
app.MapNotificationEndpoints();
app.MapParentEndpoints();
app.MapPetBrainEndpoints();

// App-in tək real vaxt kanalı: arena, dostların onlayn vəziyyəti və komanda missiyaları.
app.MapHub<LiveHub>("/hubs/live");

// Köhnə ünvan saxlanılır — mağazadakı app yenilənənə qədər kanalsız qalmasın.
app.MapHub<LiveHub>("/hubs/arena");

app.Run();

/// <summary>Integration testlərin <c>WebApplicationFactory&lt;Program&gt;</c> ilə qoşula bilməsi üçün.</summary>
public partial class Program;

