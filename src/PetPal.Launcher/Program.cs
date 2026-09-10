namespace PetPal.Launcher;

/// <summary>
/// Masaüstü giriş nöqtəsi: istifadəçi bir ikonaya basır, launcher isə bazanı,
/// API-ni və app-i lazımi ardıcıllıqla qaldırır.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        LauncherLog.Start();
        ApplicationConfiguration.Initialize();

        // Gözlənilməz nasazlıq da izsiz qalmasın — konsol olmadığına görə
        // stack trace-in yeganə gedəcəyi yer loq faylıdır.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LauncherLog.Write($"Tutulmamış xəta: {e.ExceptionObject}");

        var splash = new SplashWindow();

        // Progress<T> UI kontekstində yaradılır, ona görə hesabatlar avtomatik
        // UI thread-inə qayıdır — Invoke çağırışına ehtiyac qalmır.
        splash.Shown += async (_, _) =>
        {
            var progress = new Progress<string>(splash.SetStatus);
            var sequence = new StartupSequence(AppContext.BaseDirectory);

            try
            {
                await sequence.RunAsync(progress, splash.Hide);
                LauncherLog.Write("Normal bağlanış.");
            }
            catch (Exception ex)
            {
                LauncherLog.Write($"XƏTA: {ex}");
                splash.Hide();
                ShowError(ex);
            }

            Application.Exit();
        };

        Application.Run(splash);
    }

    private static void ShowError(Exception ex)
    {
        // Gözlənilən nasazlıqlar (Docker yoxdur, API çöküb) artıq oxunaqlı
        // mətnlə gəlir; qalanlar üçün ən azı tipi göstəririk.
        var message = ex is LauncherException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";

        MessageBox.Show(
            $"{message}\n\n{LauncherText.T("Ətraflı", "Details")}: {LauncherLog.FilePath}",
            LauncherText.T("AI Pets for Kids başladıla bilmədi", "AI Pets for Kids could not start"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
