using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace PetPal.Launcher;

/// <summary>
/// AI Pets for Kids-in lokal yığını üç mərtəbəlidir: PostgreSQL (Docker) → API → app.
/// İstifadəçi tək ikonaya basdığına görə bu ardıcıllığı launcher qurur: hər
/// mərtəbənin həqiqətən cavab verdiyini yoxlayır (sadəcə prosesi başladıb
/// keçmir), sonuncuda app-i açır və app bağlananda özünün qaldırdığı
/// prosesləri söndürür.
///
/// Artıq işləyən komponentə toxunulmur — developer <c>start-api.ps1</c> ilə
/// API-ni əvvəlcədən qaldırıbsa, launcher ikinci nüsxəni başlatmır.
/// </summary>
internal sealed class StartupSequence(string baseDirectory)
{
    private const string DatabaseHost = "localhost";
    private const int DatabasePort = 5433;
    private const string DatabaseContainer = "petpal-postgres";
    private const string ComposeProject = "petpal";
    private const string ApiBaseUrl = "http://localhost:5180/";
    private const string HealthUrl = ApiBaseUrl + "health";

    private const string OllamaContainer = "petpal-ollama";
    private const string OllamaUrl = "http://localhost:11434";
    private const int OllamaPort = 11434;

    private static readonly TimeSpan DockerEngineTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan DatabaseReadyTimeout = TimeSpan.FromSeconds(90);

    /// <summary>İlk açılışda API migrasiyaları tətbiq edir və sual bankını seed edir — bu uzun sürür.</summary>
    private static readonly TimeSpan ApiReadyTimeout = TimeSpan.FromMinutes(3);

    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(1);

    private string ApiExecutable => Path.Combine(baseDirectory, "api", "PetPal.Api.exe");
    private string AppExecutable => Path.Combine(baseDirectory, "app", "PetPal.App.exe");
    private string ComposeFile => Path.Combine(baseDirectory, "docker-compose.yml");

    /// <summary>API-ni məhz bu launcher qaldırıbsa saxlanılır — yalnız onu söndürmək üçün.</summary>
    private Process? _apiProcess;

    private readonly StringBuilder _apiLog = new();

    public async Task RunAsync(IProgress<string> progress, Action onAppLaunched)
    {
        LauncherLog.Write($"Paket qovluğu: {baseDirectory}");
        VerifyPackage();

        try
        {
            await EnsureDatabaseAsync(progress);
            await EnsureApiAsync(progress);

            progress.Report(LauncherText.T("AI Pets for Kids açılır…", "AI Pets for Kids is opening…"));
            using var app = StartApp();
            LauncherLog.Write($"App başladı (pid {app.Id}).");
            onAppLaunched();
            await app.WaitForExitAsync();
            LauncherLog.Write($"App bağlandı (exit code {app.ExitCode}).");
        }
        finally
        {
            StopApi();
        }
    }

    private void VerifyPackage()
    {
        if (!File.Exists(ApiExecutable) || !File.Exists(AppExecutable))
            throw new LauncherException(
                LauncherText.T(
                    $"Paket natamamdır — {Path.GetFileName(ApiExecutable)} və ya {Path.GetFileName(AppExecutable)} tapılmadı.\n\n" +
                    $"Qovluq: {baseDirectory}\n\n" +
                    "\"AI Pets for Kids.exe\" faylını öz qovluğundan çıxarmayın; masaüstünə qısayol (shortcut) düzəldin.",
                    $"The package is incomplete — {Path.GetFileName(ApiExecutable)} or {Path.GetFileName(AppExecutable)} is missing.\n\n" +
                    $"Folder: {baseDirectory}\n\n" +
                    "Do not move \"AI Pets for Kids.exe\" out of its folder; create a desktop shortcut instead."));
    }

    // ---------- 1. Verilənlər bazası ----------

    private async Task EnsureDatabaseAsync(IProgress<string> progress)
    {
        if (await IsPortOpenAsync(DatabaseHost, DatabasePort))
        {
            LauncherLog.Write($"PostgreSQL artıq {DatabaseHost}:{DatabasePort} ünvanında işləyir.");
            return;
        }

        if (!File.Exists(ComposeFile))
            throw new LauncherException(
                LauncherText.T(
                    $"PostgreSQL {DatabaseHost}:{DatabasePort} ünvanında cavab vermir və docker-compose.yml paketdə yoxdur.",
                    $"PostgreSQL is not answering on {DatabaseHost}:{DatabasePort} and docker-compose.yml is missing from the package."));

        progress.Report(LauncherText.T("Docker yoxlanılır…", "Checking Docker…"));
        await EnsureDockerEngineAsync(progress);

        progress.Report(LauncherText.T("Verilənlər bazası qaldırılır…", "Starting the database…"));
        await StartDatabaseContainerAsync();

        // Konteyner qalxsa da Postgres bir neçə saniyə ərzində qoşulma qəbul etmir.
        progress.Report(LauncherText.T("Verilənlər bazası gözlənilir…", "Waiting for the database…"));
        if (!await WaitUntilAsync(() => IsPortOpenAsync(DatabaseHost, DatabasePort), DatabaseReadyTimeout))
            throw new LauncherException(
                LauncherText.T(
                    $"PostgreSQL {(int)DatabaseReadyTimeout.TotalSeconds} saniyə ərzində {DatabaseHost}:{DatabasePort} ünvanında hazır olmadı.",
                    $"PostgreSQL was not ready on {DatabaseHost}:{DatabasePort} within {(int)DatabaseReadyTimeout.TotalSeconds} seconds."));

        LauncherLog.Write("PostgreSQL hazırdır.");
    }

    /// <summary>
    /// Eyni adlı konteyner artıq varsa sadəcə başladılır. <c>compose up</c> yalnız
    /// konteyner ümumiyyətlə yoxdursa çağırılır: mövcud <c>petpal-postgres</c>
    /// compose etiketləri olmadan yaradılıbsa (məsələn əl ilə <c>docker run</c> və
    /// ya başqa qovluqdakı compose layihəsi ilə), <c>compose up</c> onu tanımır və
    /// "container name is already in use" xətası ilə dayanır. Mövcud konteyneri
    /// silmək isə variant deyil — içindəki məlumat itə bilər.
    /// </summary>
    private async Task StartDatabaseContainerAsync()
    {
        if (await DatabaseContainerExistsAsync())
        {
            LauncherLog.Write($"Mövcud '{DatabaseContainer}' konteyneri başladılır.");
            var (startExitCode, startOutput) = await RunProcessAsync("docker", $"start {DatabaseContainer}", TimeSpan.FromMinutes(2));
            if (startExitCode != 0)
                throw new LauncherException(LauncherText.T(
                    $"'{DatabaseContainer}' konteyneri başladıla bilmədi.\n\n{startOutput.TrimEnd()}",
                    $"The '{DatabaseContainer}' container could not be started.\n\n{startOutput.TrimEnd()}"));

            return;
        }

        // -p ilə layihə adı sabitlənir; əks halda compose onu qovluq adından götürür
        // və paketin yeri dəyişəndə ayrı-ayrı konteynerlər yaranardı.
        LauncherLog.Write("compose up ilə yeni konteyner yaradılır.");
        var (exitCode, output) = await RunProcessAsync(
            "docker",
            $"compose -p {ComposeProject} -f \"{ComposeFile}\" up -d postgres",
            TimeSpan.FromMinutes(5));

        if (exitCode != 0)
            throw new LauncherException(LauncherText.T(
                $"PostgreSQL konteyneri qaldırıla bilmədi.\n\n{output.TrimEnd()}",
                $"The PostgreSQL container could not be started.\n\n{output.TrimEnd()}"));
    }

    /// <summary>
    /// Tətbiqin öz Ollama konteyneri varsa başladır. Konteyner yoxdursa heç nə
    /// edilmir — model olmadan boş konteyner qaldırmağın mənası yoxdur və
    /// gigabaytlıq endirməni açılış ekranında başlatmaq düzgün olmazdı.
    /// </summary>
    private async Task<bool> TryStartOllamaContainerAsync()
    {
        if (!await ContainerExistsAsync(OllamaContainer))
            return false;

        LauncherLog.Write($"'{OllamaContainer}' konteyneri başladılır.");
        var (exitCode, _) = await RunProcessAsync("docker", $"start {OllamaContainer}", TimeSpan.FromMinutes(2));
        if (exitCode != 0)
            return false;

        return await WaitUntilAsync(() => IsPortOpenAsync(DatabaseHost, OllamaPort), TimeSpan.FromSeconds(30));
    }

    private static Task<bool> DatabaseContainerExistsAsync() => ContainerExistsAsync(DatabaseContainer);

    private static async Task<bool> ContainerExistsAsync(string name)
    {
        var (exitCode, output) = await RunProcessAsync(
            "docker",
            $"ps -a --filter name=^{name}$ --format {{{{.Names}}}}",
            TimeSpan.FromSeconds(30));

        return exitCode == 0 && output.Contains(name, StringComparison.Ordinal);
    }

    private async Task EnsureDockerEngineAsync(IProgress<string> progress)
    {
        if (await IsDockerEngineRunningAsync())
        {
            LauncherLog.Write("Docker mühərriki işləyir.");
            return;
        }

        var dockerDesktop = FindDockerDesktop()
            ?? throw new LauncherException(
                LauncherText.T(
                    "PostgreSQL işləmir və Docker Desktop tapılmadı.\n\n" +
                    "Docker Desktop quraşdırın (https://www.docker.com/products/docker-desktop) və ya\n" +
                    $"Bazanı əl ilə qaldırın:\n\n    docker compose -f \"{ComposeFile}\" up -d postgres",
                    "PostgreSQL is not running and Docker Desktop was not found.\n\n" +
                    "Install Docker Desktop (https://www.docker.com/products/docker-desktop) or\n" +
                    $"start the database yourself:\n\n    docker compose -f \"{ComposeFile}\" up -d postgres"));

        progress.Report(LauncherText.T("Docker Desktop başladılır…", "Starting Docker Desktop…"));
        LauncherLog.Write($"Docker mühərriki işləmir, başladılır: {dockerDesktop}");
        try
        {
            Process.Start(new ProcessStartInfo(dockerDesktop) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            throw new LauncherException(LauncherText.T(
                $"Docker Desktop başladıla bilmədi: {ex.Message}",
                $"Docker Desktop could not be started: {ex.Message}"));
        }

        var started = Stopwatch.StartNew();
        var ready = await WaitUntilAsync(
            IsDockerEngineRunningAsync,
            DockerEngineTimeout,
            onTick: () => progress.Report($"Docker Desktop başladılır… ({(int)started.Elapsed.TotalSeconds} san.)"));

        if (!ready)
            throw new LauncherException(
                LauncherText.T(
                    $"Docker Desktop {(int)DockerEngineTimeout.TotalMinutes} dəqiqə ərzində hazır olmadı.\n\n" +
                    "Docker Desktop-ı əl ilə açıb tam yüklənməsini gözləyin, sonra AI Pets for Kids-i yenidən başladın.",
                    $"Docker Desktop was not ready within {(int)DockerEngineTimeout.TotalMinutes} minutes.\n\n" +
                    "Open Docker Desktop yourself, wait until it has fully started, then launch AI Pets for Kids again."));

        LauncherLog.Write($"Docker mühərriki {(int)started.Elapsed.TotalSeconds} saniyəyə hazır oldu.");
    }

    private static async Task<bool> IsDockerEngineRunningAsync()
    {
        var (exitCode, _) = await RunProcessAsync("docker", "info --format {{.ServerVersion}}", TimeSpan.FromSeconds(20));
        return exitCode == 0;
    }

    private static string? FindDockerDesktop()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker", "Docker Desktop.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Docker", "Docker Desktop.exe"),
        ];

        return Array.Find(candidates, File.Exists);
    }

    // ---------- 2. API ----------

    private async Task EnsureApiAsync(IProgress<string> progress)
    {
        if (await IsApiHealthyAsync())
        {
            LauncherLog.Write($"API artıq {ApiBaseUrl} ünvanında işləyir — yenisi başladılmır.");
            return;
        }

        progress.Report(LauncherText.T("AI yoxlanılır…", "Checking the AI…"));
        var ai = await AiDetector.DetectAsync(TryStartOllamaContainerAsync);

        progress.Report(LauncherText.T("API başladılır…", "Starting the API…"));

        var startInfo = new ProcessStartInfo(ApiExecutable)
        {
            WorkingDirectory = Path.GetDirectoryName(ApiExecutable)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.Environment["ASPNETCORE_URLS"] = ApiBaseUrl.TrimEnd('/');
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        // AI konfiqurasiyası environment dəyişəni ilə ötürülür — masaüstü paketində
        // istifadəçi appsettings faylını redaktə etmir. ASP.NET Core "__" ayırıcısını
        // konfiqurasiya iyerarxiyası kimi oxuyur.
        if (ai is not null)
        {
            startInfo.Environment["Ai__Provider"] = "OpenAiCompatible";
            startInfo.Environment["Ai__BaseUrl"] = ai.BaseUrl;
            startInfo.Environment["Ai__Model"] = ai.Model;
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => CaptureApiLine(e.Data);
        process.ErrorDataReceived += (_, e) => CaptureApiLine(e.Data);

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            process.Dispose();
            throw new LauncherException(LauncherText.T(
                $"API başladıla bilmədi: {ex.Message}",
                $"The API could not be started: {ex.Message}"));
        }

        _apiProcess = process;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        LauncherLog.Write($"API prosesi başladı (pid {process.Id}).");

        var started = Stopwatch.StartNew();
        var ready = await WaitUntilAsync(
            IsApiHealthyAsync,
            ApiReadyTimeout,
            onTick: () =>
            {
                // API çökübsə gözləməyin mənası yoxdur — logu dərhal göstərmək daha faydalıdır.
                if (process.HasExited)
                    throw new LauncherException(
                        LauncherText.T(
                            $"API işə düşən kimi dayandı (exit code {process.ExitCode}).\n\n{ReadApiLog()}",
                            $"The API stopped right after starting (exit code {process.ExitCode}).\n\n{ReadApiLog()}"));

                progress.Report($"API hazırlanır… ({(int)started.Elapsed.TotalSeconds} san.)");
            });

        if (!ready)
            throw new LauncherException(
                LauncherText.T(
                    $"API {(int)ApiReadyTimeout.TotalMinutes} dəqiqə ərzində {HealthUrl} ünvanında cavab vermədi.\n\n{ReadApiLog()}",
                    $"The API did not answer on {HealthUrl} within {(int)ApiReadyTimeout.TotalMinutes} minutes.\n\n{ReadApiLog()}"));

        LauncherLog.Write($"API {(int)started.Elapsed.TotalSeconds} saniyəyə hazır oldu.");
    }

    private void CaptureApiLine(string? line)
    {
        if (line is null)
            return;

        lock (_apiLog)
        {
            // Xəta pəncərəsinə yalnız son sətirlər lazımdır; loq sonsuz böyüməsin.
            if (_apiLog.Length > 8000)
                _apiLog.Remove(0, _apiLog.Length - 4000);

            _apiLog.AppendLine(line);
        }
    }

    private string ReadApiLog()
    {
        lock (_apiLog)
        {
            var text = _apiLog.ToString().TrimEnd();
            return text.Length == 0 ? "(API heç nə yazmadı)" : text;
        }
    }

    private static async Task<bool> IsApiHealthyAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            var response = await http.GetAsync(HealthUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void StopApi()
    {
        var process = _apiProcess;
        _apiProcess = null;
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Proses onsuz da bitibsə problem yoxdur.
        }
        finally
        {
            process.Dispose();
        }
    }

    // ---------- 3. App ----------

    private Process StartApp()
    {
        var startInfo = new ProcessStartInfo(AppExecutable)
        {
            WorkingDirectory = Path.GetDirectoryName(AppExecutable)!,
            UseShellExecute = false,
        };
        // Release build-i standart olaraq produksiya API-sinə baxır; masaüstü
        // paketində backend lokal olduğuna görə ünvanı burada ötürürük.
        startInfo.Environment["PETPAL_API_URL"] = ApiBaseUrl;

        try
        {
            return Process.Start(startInfo)
                ?? throw new LauncherException(LauncherText.T(
                    "Tətbiq prosesi başladıla bilmədi.",
                    "The app process could not be started."));
        }
        catch (Exception ex) when (ex is not LauncherException)
        {
            throw new LauncherException(LauncherText.T(
                $"Tətbiq başladıla bilmədi: {ex.Message}",
                $"The app could not be started: {ex.Message}"));
        }
    }

    // ---------- Köməkçilər ----------

    private static async Task<bool> IsPortOpenAsync(string host, int port)
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port).WaitAsync(TimeSpan.FromSeconds(2));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> probe, TimeSpan timeout, Action? onTick = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            onTick?.Invoke();

            if (await probe())
                return true;

            await Task.Delay(ProbeInterval);
        }

        return await probe();
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(string fileName, string arguments, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            // Məsələn `docker` PATH-də yoxdur.
            return (-1, ex.Message);
        }

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* onsuz da bitib */ }
            return (-1, $"'{fileName} {arguments}' {(int)timeout.TotalSeconds} saniyə ərzində bitmədi.");
        }

        return (process.ExitCode, string.Concat(await stdout, await stderr));
    }
}

/// <summary>İstifadəçiyə olduğu kimi göstərilə bilən xəta — stack trace lazım deyil.</summary>
internal sealed class LauncherException(string message) : Exception(message);
