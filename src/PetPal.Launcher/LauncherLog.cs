using System.Text;

namespace PetPal.Launcher;

/// <summary>
/// Launcher-in gündəliyi. Masaüstü paketində konsol yoxdur — nə API-nin, nə də
/// Docker-in çıxışı heç yerə düşmür. Nəsə səhv gedəndə "heç nə açılmadı"dan
/// başqa əlamət qalmasın deyə hər addım fayla yazılır.
///
/// Fayl hər açılışda sıfırlanır: maraq doğuran həmişə sonuncu cəhddir.
/// </summary>
internal static class LauncherLog
{
    private static readonly object Gate = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AI Pets for Kids",
        "launcher.log");

    public static void Start()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, $"AI Pets for Kids launcher — {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            // Loq yazıla bilmirsə də launcher işini davam etdirməlidir.
        }
    }

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
                File.AppendAllText(FilePath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            // Yuxarıdakı ilə eyni səbəb.
        }
    }
}
