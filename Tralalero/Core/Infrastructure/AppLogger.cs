using System.Text;
using System.Text.RegularExpressions;

namespace Tralalero.Core.Infrastructure;

/// <summary>
/// Daily rolling file logger. Transcript content is never passed here by the
/// application; this class additionally redacts anything that looks like a key.
/// </summary>
public sealed class AppLogger : IAppLogger
{
    private const int RetentionDays = 14;
    private const string InfoIcon = "ℹ️";
    private const string WarnIcon = "⚠️";
    private const string ErrorIcon = "❌";
    private const string DebugIcon = "🐞";

    private static readonly Regex KeyPattern = new(
        @"(sk-[A-Za-z0-9_\-]{8,}|Bearer\s+[A-Za-z0-9._\-]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly object _gate = new();
    private readonly string _directory;

    public AppLogger(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
        DeleteOldLogs();
    }

    public void Info(string message) => Write(InfoIcon, "INFO", message, null);

    public void Warn(string message, Exception? exception = null) => Write(WarnIcon, "WARN", message, exception);

    public void Error(string message, Exception? exception = null) => Write(ErrorIcon, "ERROR", message, exception);

    public void Debug(string message) => Write(DebugIcon, "DEBUG", message, null);

    /// <summary>Exposed for tests and diagnostics; never logs the input.</summary>
    public static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return KeyPattern.Replace(value, match =>
            match.Value.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase) ? "Bearer ***" : "sk-***");
    }

    private void Write(string icon, string level, string message, Exception? exception)
    {
        try
        {
            var line = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
                .Append(' ')
                .Append(icon)
                .Append(' ')
                .Append(level)
                .Append(' ')
                .Append(Sanitize(message));

            if (exception is not null)
            {
                line.AppendLine();
                line.Append(Sanitize(exception.ToString()));
            }

            lock (_gate)
            {
                var file = Path.Combine(_directory, $"tralalero-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(file, line.ToString() + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never take the application down.
        }
    }

    private void DeleteOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (var file in Directory.EnumerateFiles(_directory, "tralalero-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Cleanup is best-effort.
        }
    }
}
