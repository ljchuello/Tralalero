using System.Text.Json;
using System.Text.Json.Serialization;
using Tralalero.Core.Infrastructure;

namespace Tralalero.Core.Settings;

/// <summary>Loads and atomically saves <see cref="AppSettings"/>.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();
    private readonly IAppLogger _logger;

    public SettingsService(string path, IAppLogger logger)
    {
        Path = path;
        _logger = logger;
    }

    public string Path { get; }

    public AppSettings Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(Path))
                {
                    return new AppSettings();
                }

                var json = File.ReadAllText(Path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                settings.Normalize();
                return settings;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                _logger.Warn($"Could not read settings from {Path}. Defaults will be used.", ex);
                QuarantineCorruptFile();
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();

        lock (_gate)
        {
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = Path + ".tmp";
            try
            {
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(temp, json);
                File.Move(temp, Path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    try
                    {
                        File.Delete(temp);
                    }
                    catch
                    {
                        // Best-effort cleanup.
                    }
                }
            }
        }
    }

    private void QuarantineCorruptFile()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return;
            }

            var quarantine = $"{Path}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(Path, quarantine, overwrite: false);
            _logger.Warn($"The invalid settings file was moved to {quarantine}.");
        }
        catch (Exception ex)
        {
            _logger.Warn("Could not quarantine the invalid settings file.", ex);
        }
    }
}
