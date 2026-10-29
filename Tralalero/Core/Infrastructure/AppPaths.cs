using System.Diagnostics;

namespace Tralalero.Core.Infrastructure;

/// <summary>
/// Well-known locations used by Tralalero. Keeping them in one place makes
/// backup, troubleshooting, and tests predictable.
/// </summary>
public static class AppPaths
{
    public const string AppName = "Tralalero";

    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    public static string LocalDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

    public static string LogsDirectory => Path.Combine(LocalDataDirectory, "logs");

    public static string SettingsFile => Path.Combine(AppDataDirectory, "settings.json");

    public static string CredentialsFile => Path.Combine(AppDataDirectory, "credentials.bin");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(LocalDataDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    public static void OpenInExplorer(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }
}
