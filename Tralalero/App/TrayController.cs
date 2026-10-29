using Tralalero.Core.Transcription;

namespace Tralalero.App;

internal sealed class TrayController : IDisposable
{
    private readonly Icon _icon;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleItem;
    private bool _disposed;

    public TrayController()
    {
        _icon = AppIcon.Load();
        _toggleItem = new ToolStripMenuItem("🎙️ Start dictation", null, (_, _) => ToggleRequested?.Invoke(this, EventArgs.Empty));

        var menu = new ContextMenuStrip
        {
            Font = Theme.Body,
            ShowImageMargin = false
        };
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("⚙️ Settings", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("📂 Open logs", null, (_, _) => OpenLogsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("ℹ️ About", null, (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("❌ Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty)));

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "Tralalero — Ready",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ToggleRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? OpenLogsRequested;
    public event EventHandler? AboutRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? ShowRequested;

    public void SetState(DictationState state)
    {
        if (_disposed)
        {
            return;
        }

        _toggleItem.Text = state == DictationState.Listening
            ? "⏹️ Stop dictation"
            : "🎙️ Start dictation";
        _toggleItem.Enabled = state != DictationState.Finalizing;
        _notifyIcon.Text = state switch
        {
            DictationState.Listening => "Tralalero — Listening…",
            DictationState.Finalizing => "Tralalero — Finishing…",
            DictationState.Error => "Tralalero — Error",
            _ => "Tralalero — Ready"
        };
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = text;
            _notifyIcon.BalloonTipIcon = icon;
            _notifyIcon.ShowBalloonTip(3500);
        }
        catch
        {
            // Balloons are best-effort and may be disabled by Focus Assist.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}
