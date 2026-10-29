using Tralalero.Core.Audio;
using Tralalero.Core.Infrastructure;
using Tralalero.Core.Settings;

namespace Tralalero.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "tralalero-tests",
        Guid.NewGuid().ToString("N"));

    public SettingsServiceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsAllValues()
    {
        var path = Path.Combine(_directory, "settings.json");
        var service = new SettingsService(path, NullAppLogger.Instance);
        var settings = new AppSettings
        {
            Model = "gpt-transcribe",
            Language = "auto",
            VadSensitivity = VadSensitivity.High,
            AutoPaste = false,
            MaxRecordingMinutes = 22,
            ConsentAcceptedAtUtc = DateTimeOffset.UtcNow
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(settings.Model, loaded.Model);
        Assert.Equal(settings.Language, loaded.Language);
        Assert.Equal(settings.VadSensitivity, loaded.VadSensitivity);
        Assert.False(loaded.AutoPaste);
        Assert.Equal(22, loaded.MaxRecordingMinutes);
        Assert.NotNull(loaded.ConsentAcceptedAtUtc);
    }

    [Fact]
    public void Load_WithCorruptJson_ReturnsDefaultsAndQuarantinesTheFile()
    {
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{ not valid json");
        var service = new SettingsService(path, NullAppLogger.Instance);

        var loaded = service.Load();

        Assert.Equal("gpt-transcribe", loaded.Model);
        Assert.False(File.Exists(path));
        Assert.NotEmpty(Directory.GetFiles(_directory, "settings.json.corrupt-*"));
    }

    [Fact]
    public void Normalize_ClampsUnsafeValues()
    {
        var settings = new AppSettings
        {
            MaxRecordingMinutes = 500,
            Language = " ",
            Model = " ",
            ApiBaseUrl = "https://example.test/v1/"
        };

        settings.Normalize();

        Assert.Equal(60, settings.MaxRecordingMinutes);
        Assert.Equal("es", settings.Language);
        Assert.Equal("gpt-transcribe", settings.Model);
        Assert.Equal("https://example.test/v1", settings.ApiBaseUrl);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // Best-effort test cleanup.
        }
    }
}
