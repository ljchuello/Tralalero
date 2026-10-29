using Tralalero.Core.Audio;
using Tralalero.Core.OpenAI;

namespace Tralalero.Core.Settings;

/// <summary>User preferences persisted as JSON in %APPDATA%\Tralalero.</summary>
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    public string Model { get; set; } = OpenAiModels.Default;

    /// <summary>ISO 639-1 code, or "auto" to let the API detect it.</summary>
    public string Language { get; set; } = "es";

    public VadSensitivity VadSensitivity { get; set; } = VadSensitivity.Medium;

    /// <summary>Device product name. Empty means the first available device.</summary>
    public string InputDeviceName { get; set; } = string.Empty;

    public string Hotkey { get; set; } = "Ctrl+Alt+Space";

    public bool AutoCopy { get; set; } = true;

    public bool AutoPaste { get; set; } = true;

    public bool CloseToTray { get; set; } = true;

    public bool LaunchAtStartup { get; set; }

    public int MaxRecordingMinutes { get; set; } = 10;

    public DateTimeOffset? ConsentAcceptedAtUtc { get; set; }

    /// <summary>Optional override for proxies or local API-compatible test servers.</summary>
    public string ApiBaseUrl { get; set; } = "https://api.openai.com/v1";

    public AppSettings Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        Model = Model,
        Language = Language,
        VadSensitivity = VadSensitivity,
        InputDeviceName = InputDeviceName,
        Hotkey = Hotkey,
        AutoCopy = AutoCopy,
        AutoPaste = AutoPaste,
        CloseToTray = CloseToTray,
        LaunchAtStartup = LaunchAtStartup,
        MaxRecordingMinutes = MaxRecordingMinutes,
        ConsentAcceptedAtUtc = ConsentAcceptedAtUtc,
        ApiBaseUrl = ApiBaseUrl
    };

    /// <summary>Repairs values that may come from an older or hand-edited file.</summary>
    public void Normalize()
    {
        if (SchemaVersion <= 0)
        {
            SchemaVersion = 1;
        }

        Model = string.IsNullOrWhiteSpace(Model) ? OpenAiModels.Default : Model.Trim();
        Language = string.IsNullOrWhiteSpace(Language) ? "es" : Language.Trim();
        Hotkey = string.IsNullOrWhiteSpace(Hotkey) ? "Ctrl+Alt+Space" : Hotkey.Trim();
        MaxRecordingMinutes = Math.Clamp(MaxRecordingMinutes, 1, 60);
        ApiBaseUrl = string.IsNullOrWhiteSpace(ApiBaseUrl)
            ? "https://api.openai.com/v1"
            : ApiBaseUrl.Trim().TrimEnd('/');
        InputDeviceName ??= string.Empty;
    }
}
