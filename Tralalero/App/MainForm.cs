using System.Text;
using Tralalero.Core.Audio;
using Tralalero.Core.Infrastructure;
using Tralalero.Core.Interop;
using Tralalero.Core.OpenAI;
using Tralalero.Core.Security;
using Tralalero.Core.Settings;
using Tralalero.Core.Transcription;

namespace Tralalero.App;

internal sealed class MainForm : Form
{
    private readonly SettingsService _settingsService;
    private readonly ApiKeyStore _keyStore;
    private readonly IAppLogger _logger;
    private readonly HttpClient _httpClient;
    private readonly TextInjector _textInjector;
    private readonly TrayController _tray;
    private readonly RecordingOverlay _overlay;
    private readonly System.Windows.Forms.Timer _uiTimer;
    private readonly ToolTip _toolTip = new();

    private AppSettings _settings;
    private string? _apiKey;
    private DictationSession? _session;
    private GlobalHotkey? _hotkey;
    private IntPtr _pasteTarget;
    private float _latestLevel;
    private float _displayLevel;
    private int _displayedSegments;
    private bool _exiting;
    private bool _hotkeyRegistered;
    private bool _onboardingShownOnce;
    private bool _setupOpen;
    private DateTimeOffset? _transientStatusUntil;

    private readonly ThemedButton _recordButton;
    private readonly Label _statusLabel;
    private readonly Label _timerLabel;
    private readonly Label _metaLabel;
    private readonly Label _countLabel;
    private readonly Label _bannerLabel;
    private readonly Panel _banner;
    private readonly LinkLabel _setupLink;
    private readonly LevelMeter _levelMeter;
    private readonly TextBox _transcriptBox;
    private readonly ThemedButton _copyButton;
    private readonly ThemedButton _saveButton;
    private readonly ThemedButton _clearButton;
    private readonly ThemedButton _settingsButton;

    public MainForm(
        SettingsService settingsService,
        ApiKeyStore keyStore,
        IAppLogger logger,
        HttpClient httpClient)
    {
        _settingsService = settingsService;
        _keyStore = keyStore;
        _logger = logger;
        _httpClient = httpClient;
        _textInjector = new TextInjector(logger);
        _settings = settingsService.Load();
        _apiKey = keyStore.Load();

        Text = UiText.WindowTitle;
        Icon = AppIcon.Load();
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(780, 680);
        MinimumSize = new Size(700, 620);
        BackColor = Theme.Background;
        Font = Theme.Body;
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;

        _tray = new TrayController();
        _tray.ToggleRequested += (_, _) => _ = ToggleRecordingAsync();
        _tray.ShowRequested += (_, _) => RestoreFromTray();
        _tray.SettingsRequested += (_, _) => OpenSettings();
        _tray.OpenLogsRequested += (_, _) => OpenLogs();
        _tray.AboutRequested += (_, _) => ShowAbout();
        _tray.ExitRequested += (_, _) => ExitApplication();

        _overlay = new RecordingOverlay();
        _uiTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _uiTimer.Tick += UiTimerTick;
        _uiTimer.Start();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(22),
            BackColor = Theme.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // header
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // banner
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // status
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // record
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // transcript
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // actions
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // footer

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.Controls.Add(new Label
        {
            Text = UiText.AppTitle,
            Font = Theme.Title,
            ForeColor = Theme.Text,
            AutoSize = true,
            Margin = new Padding(0)
        }, 0, 0);
        header.Controls.Add(new Label
        {
            Text = UiText.Subtitle,
            Font = Theme.Subtitle,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Margin = new Padding(2, 2, 0, 0)
        }, 0, 1);

        _banner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(255, 251, 235),
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0, 0, 0, 10),
            Visible = false
        };
        var bannerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        bannerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bannerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bannerLabel = new Label
        {
            Text = UiText.KeyRequired,
            Font = Theme.Body,
            ForeColor = Color.FromArgb(146, 64, 14),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0)
        };
        _setupLink = new LinkLabel
        {
            Text = UiText.SetupLink,
            Font = Theme.BodyBold,
            LinkColor = Color.FromArgb(146, 64, 14),
            ActiveLinkColor = Theme.Warning,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(10, 0, 0, 0)
        };
        _setupLink.LinkClicked += (_, _) => OpenSetup();
        bannerLayout.Controls.Add(_bannerLabel, 0, 0);
        bannerLayout.Controls.Add(_setupLink, 1, 0);
        _banner.Controls.Add(bannerLayout);

        var statusCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Height = 104,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(0)
        };
        _statusLabel = new Label
        {
            AutoSize = true,
            Location = new Point(18, 14),
            Font = Theme.Status,
            ForeColor = Theme.Success,
            Text = UiText.Ready
        };
        _timerLabel = new Label
        {
            AutoSize = false,
            Size = new Size(96, 28),
            Location = new Point(Width - 120, 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = Theme.Timer,
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleRight,
            Text = "00:00"
        };
        _metaLabel = new Label
        {
            AutoSize = true,
            Location = new Point(20, 48),
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            Text = string.Empty
        };
        _levelMeter = new LevelMeter
        {
            Location = new Point(20, 76),
            Size = new Size(Width - 150, 8),
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        statusCard.Controls.Add(_statusLabel);
        statusCard.Controls.Add(_timerLabel);
        statusCard.Controls.Add(_metaLabel);
        statusCard.Controls.Add(_levelMeter);
        statusCard.Resize += (_, _) =>
        {
            _timerLabel.Left = Math.Max(18, statusCard.ClientSize.Width - _timerLabel.Width - 18);
            _levelMeter.Width = Math.Max(40, statusCard.ClientSize.Width - 40);
        };

        var recordArea = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 12)
        };
        recordArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        recordArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        recordArea.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _recordButton = new ThemedButton
        {
            Text = UiText.Start,
            Size = new Size(238, 54),
            Anchor = AnchorStyles.None,
            BackColor = Theme.Accent,
            HoverColor = Theme.AccentHover,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Margin = new Padding(0, 2, 0, 6)
        };
        _recordButton.Click += (_, _) => _ = ToggleRecordingAsync();
        var hotkeyHint = new Label
        {
            Text = UiText.HotkeyHint,
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0)
        };
        recordArea.Controls.Add(_recordButton, 0, 0);
        recordArea.Controls.Add(hotkeyHint, 0, 1);

        var transcriptCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 0, 10)
        };
        var transcriptLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        transcriptLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        transcriptLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        transcriptLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var transcriptHeader = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 26,
            BackColor = Color.Transparent
        };
        transcriptHeader.Controls.Add(new Label
        {
            Text = "📝 TRANSCRIPT",
            Font = Theme.SmallBold,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(0, 4)
        });
        _countLabel = new Label
        {
            Text = "0 words · 0 chars",
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(200, 4),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        transcriptHeader.Controls.Add(_countLabel);
        transcriptHeader.Resize += (_, _) =>
        {
            _countLabel.Left = Math.Max(0, transcriptHeader.ClientSize.Width - _countLabel.Width);
        };
        _transcriptBox = new TextBox
        {
            Multiline = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            Font = Theme.Mono,
            ForeColor = Theme.Text,
            BackColor = Theme.Surface,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            PlaceholderText = UiText.TranscriptPlaceholder,
            Margin = new Padding(0, 6, 0, 0)
        };
        _transcriptBox.TextChanged += (_, _) => UpdateCount();
        transcriptLayout.Controls.Add(transcriptHeader, 0, 0);
        transcriptLayout.Controls.Add(_transcriptBox, 0, 1);
        transcriptCard.Controls.Add(transcriptLayout);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        _copyButton = CreateSecondaryButton(UiText.Copy, 100);
        _copyButton.Click += (_, _) => CopyTranscript();
        _saveButton = CreateSecondaryButton(UiText.Save, 100);
        _saveButton.Click += (_, _) => SaveTranscript();
        _clearButton = CreateSecondaryButton(UiText.Clear, 100);
        _clearButton.Click += (_, _) => ClearTranscript();
        _settingsButton = CreateSecondaryButton(UiText.Settings, 118);
        _settingsButton.Click += (_, _) => OpenSettings();
        actions.Controls.Add(_copyButton);
        actions.Controls.Add(_saveButton);
        actions.Controls.Add(_clearButton);
        actions.Controls.Add(_settingsButton);

        _toolTip.SetToolTip(_recordButton, "Start or stop listening (Ctrl+Alt+Space)");
        _toolTip.SetToolTip(_copyButton, "Copy the transcript to the clipboard");
        _toolTip.SetToolTip(_saveButton, "Save the transcript as a .txt file");
        _toolTip.SetToolTip(_clearButton, "Clear the transcript");
        _toolTip.SetToolTip(_settingsButton, "Open settings");

        var footer = new Label
        {
            Text = UiText.Privacy,
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(_banner, 0, 1);
        root.Controls.Add(statusCard, 0, 2);
        root.Controls.Add(recordArea, 0, 3);
        root.Controls.Add(transcriptCard, 0, 4);
        root.Controls.Add(actions, 0, 5);
        root.Controls.Add(footer, 0, 6);
        Controls.Add(root);

        Shown += OnShown;
        FormClosing += OnFormClosing;
        Resize += OnResize;
        UpdateMeta();
        UpdateReadiness();
        ApplyState(DictationState.Idle);
        UpdateCount();
    }

    private static ThemedButton CreateSecondaryButton(string text, int width) => new()
    {
        Text = text,
        Width = width,
        Height = 38,
        BackColor = Theme.Surface,
        HoverColor = Theme.SurfaceMuted,
        ForeColor = Theme.Text,
        BorderColor = Theme.Border,
        Font = Theme.BodyBold,
        Margin = new Padding(0, 0, 8, 0)
    };

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        _hotkey?.Dispose();
        _hotkey = new GlobalHotkey(Handle);
        _hotkey.Pressed += OnHotkeyPressed;

        var hotkey = HotkeyDefinition.TryParse(_settings.Hotkey, out var parsed)
            ? parsed
            : HotkeyDefinition.Default;
        _hotkeyRegistered = _hotkey.Register(hotkey);
        if (!_hotkeyRegistered)
        {
            _logger.Warn($"Global hotkey '{hotkey.Display}' could not be registered.");
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (_hotkey is not null && _hotkey.HandleMessage(ref m))
        {
            return;
        }

        base.WndProc(ref m);
    }

    private void OnShown(object? sender, EventArgs e)
    {
        if (_onboardingShownOnce)
        {
            return;
        }

        _onboardingShownOnce = true;
        _logger.Info(
            $"Main form shown. Consent={_settings.ConsentAcceptedAtUtc.HasValue}, " +
            $"KeyPresent={!string.IsNullOrWhiteSpace(_apiKey)}, Modal={Modal}.");

        if (!_settings.ConsentAcceptedAtUtc.HasValue || string.IsNullOrWhiteSpace(_apiKey))
        {
            try
            {
                BeginInvoke(new Action(OpenSetup));
            }
            catch (InvalidOperationException)
            {
                // The form is closing before setup can be shown.
            }
        }

        if (!_hotkeyRegistered)
        {
            _tray.ShowBalloon(
                "⚠️ Hotkey unavailable",
                "Ctrl+Alt+Space is already used by another app. Change it in Settings.",
                ToolTipIcon.Warning);
        }
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (Modal)
        {
            return;
        }

        _ = ToggleRecordingAsync();
    }

    private async Task ToggleRecordingAsync()
    {
        if (_session is { State: DictationState.Finalizing })
        {
            return;
        }

        if (_session is { State: DictationState.Listening })
        {
            await StopDictationAsync();
            return;
        }

        if (_session is { State: DictationState.Error })
        {
            CleanupSession();
        }

        if (!CanDictate())
        {
            OpenSetup();
            if (!CanDictate())
            {
                return;
            }
        }

        await StartDictationAsync();
    }

    private async Task StartDictationAsync()
    {
        if (_session is not null)
        {
            return;
        }

        var key = _keyStore.Load();
        if (string.IsNullOrWhiteSpace(key))
        {
            _apiKey = null;
            UpdateReadiness();
            OpenSetup();
            return;
        }

        var devices = AudioRecorder.GetDevices();
        if (devices.Count == 0)
        {
            ShowMessage(UiText.NoMicrophone, "Tralalero", MessageBoxIcon.Warning);
            return;
        }

        var device = devices.FirstOrDefault(item =>
                string.Equals(item.Name, _settings.InputDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? devices[0];

        var foreground = WindowHelper.GetForegroundWindow();
        _pasteTarget = foreground != IntPtr.Zero && !WindowHelper.IsOwnedByCurrentProcess(foreground)
            ? foreground
            : IntPtr.Zero;

        var client = new OpenAiClient(_httpClient, key, _settings.ApiBaseUrl, _logger);
        var transcriber = new OpenAiTranscriber(client);
        var session = new DictationSession(() => new AudioRecorder(), transcriber, _logger);
        session.TranscriptUpdated += OnTranscriptUpdated;
        session.StateChanged += OnStateChanged;
        session.Error += OnSessionError;
        session.Completed += OnSessionCompleted;
        session.LevelChanged += OnLevelChanged;

        _session = session;
        _displayedSegments = 0;
        _transcriptBox.Clear();
        _recordButton.Text = UiText.Starting;
        _recordButton.Enabled = false;

        try
        {
            await session.StartAsync(device.DeviceNumber, _settings);
            ApplyState(DictationState.Listening);
            _overlay.ShowListening(TimeSpan.Zero, 0);
            if (!Visible || WindowState == FormWindowState.Minimized)
            {
                _tray.ShowBalloon("🔴 Listening", "Press Ctrl+Alt+Space to stop.", ToolTipIcon.Info);
            }

            _logger.Info($"Dictation started with '{device.Name}'.");
        }
        catch (AudioCaptureException ex)
        {
            _logger.Warn("Could not start the microphone.", ex);
            ShowMessage(ex.Message, "Microphone", MessageBoxIcon.Warning);
            CleanupSession();
        }
        catch (Exception ex)
        {
            _logger.Error("Could not start dictation.", ex);
            ShowMessage($"Could not start dictation: {ex.Message}", "Tralalero", MessageBoxIcon.Error);
            CleanupSession();
        }
    }

    private async Task StopDictationAsync()
    {
        if (_session is null)
        {
            return;
        }

        var foreground = WindowHelper.GetForegroundWindow();
        if (foreground != IntPtr.Zero && !WindowHelper.IsOwnedByCurrentProcess(foreground))
        {
            _pasteTarget = foreground;
        }

        ApplyState(DictationState.Finalizing);
        _overlay.ShowFinishing(_displayedSegments);

        try
        {
            await _session.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Could not stop dictation cleanly.", ex);
            ShowMessage($"Could not finish dictation: {ex.Message}", "Tralalero", MessageBoxIcon.Warning);
            CleanupSession();
        }
    }

    private void OnTranscriptUpdated(object? sender, TranscriptUpdatedEventArgs e)
    {
        RunOnUi(() =>
        {
            _displayedSegments = e.SegmentCount;
            _transcriptBox.Text = e.Transcript;
            _transcriptBox.SelectionStart = _transcriptBox.TextLength;
            _transcriptBox.ScrollToCaret();
            UpdateCount();
            if (_overlay.Visible)
            {
                _overlay.UpdateListening(e.Elapsed, e.SegmentCount);
            }

            _timerLabel.Text = FormatElapsed(e.Elapsed);
        });
    }

    private void OnStateChanged(object? sender, DictationStateChangedEventArgs e)
    {
        RunOnUi(() => ApplyState(e.State));
    }

    private void OnSessionError(object? sender, DictationErrorEventArgs e)
    {
        RunOnUi(() =>
        {
            _logger.Warn("The dictation session reported an error.", e.Error);
            _statusLabel.Text = UiText.Error;
            _statusLabel.ForeColor = Theme.Danger;
            _tray.ShowBalloon("⚠️ Tralalero", e.Error.Message, ToolTipIcon.Warning);
            _ = StopAfterErrorAsync();
        });
    }

    private async Task StopAfterErrorAsync()
    {
        try
        {
            if (_session is not null)
            {
                await _session.StopAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Warn("Could not finalize the failed session.", ex);
        }
    }

    private void OnSessionCompleted(object? sender, DictationCompletedEventArgs e)
    {
        RunOnUi(() => _ = HandleCompletedAsync(e));
    }

    private async Task HandleCompletedAsync(DictationCompletedEventArgs e)
    {
        try
        {
            await HandleCompletedCoreAsync(e);
        }
        catch (Exception ex)
        {
            _logger.Error("The completed dictation could not be presented.", ex);
            ApplyState(DictationState.Error);
            ShowMessage($"The transcript could not be finalized: {ex.Message}", "Tralalero", MessageBoxIcon.Warning);
        }
    }

    private async Task HandleCompletedCoreAsync(DictationCompletedEventArgs e)
    {
        _overlay.HideOverlay();
        _timerLabel.Text = FormatElapsed(e.Duration);
        _displayedSegments = e.SegmentCount;
        _transcriptBox.Text = e.Transcript;
        _transcriptBox.ReadOnly = false;
        UpdateCount();

        var hadTarget = _pasteTarget;
        _pasteTarget = IntPtr.Zero;

        if (e.Error is not null)
        {
            ApplyState(DictationState.Error);
            ShowMessage(e.Error.Message, "Transcription error", MessageBoxIcon.Error);
        }
        else
        {
            ApplyState(DictationState.Idle);
        }

        if (!string.IsNullOrWhiteSpace(e.Transcript))
        {
            if (_settings.AutoPaste && hadTarget != IntPtr.Zero)
            {
                var result = await _textInjector.PasteTextAsync(hadTarget, e.Transcript);
                if (result.Pasted)
                {
                    SetTransientStatus("✅ Pasted into the active app", Theme.Success);
                    _tray.ShowBalloon("✅ Tralalero", string.IsNullOrWhiteSpace(result.TargetTitle)
                        ? "Transcript pasted."
                        : $"Pasted into {result.TargetTitle}.", ToolTipIcon.Info);
                }
                else if (result.Copied)
                {
                    SetTransientStatus(UiText.PasteFallback, Theme.Warning);
                    _tray.ShowBalloon("📋 Copied", "Auto-paste was not possible. Paste with Ctrl+V.", ToolTipIcon.Info);
                }
                else
                {
                    SetTransientStatus("⚠️ Could not copy or paste", Theme.Warning);
                }
            }
            else if (_settings.AutoCopy)
            {
                if (_textInjector.CopyText(e.Transcript))
                {
                    SetTransientStatus(UiText.Copied, Theme.Success);
                }
                else
                {
                    SetTransientStatus("⚠️ Clipboard was busy", Theme.Warning);
                }
            }
        }
        else if (e.Error is null)
        {
            SetTransientStatus("🎙️ No speech detected", Theme.Muted);
        }

        _logger.Info(
            $"Dictation finished: {e.Duration.TotalSeconds:0.0}s, {e.SegmentCount} phrases, " +
            $"{(e.Error is null ? "success" : "error")}.");

        var session = _session;
        _session = null;
        session?.Dispose();
        _recordButton.Enabled = true;
        _transcriptBox.ReadOnly = false;
        UpdateReadiness();
    }

    private void OnLevelChanged(object? sender, float level)
    {
        _latestLevel = Math.Max(_latestLevel, level);
    }

    private void UiTimerTick(object? sender, EventArgs e)
    {
        if (_transientStatusUntil is { } until && DateTimeOffset.UtcNow >= until)
        {
            _transientStatusUntil = null;
            UpdateStatusText();
        }

        if (_session is null)
        {
            _displayLevel = Math.Max(0, _displayLevel * 0.75f);
            _levelMeter.Level = _displayLevel;
            return;
        }

        _timerLabel.Text = FormatElapsed(_session.Elapsed);
        _displayLevel = Math.Max(_latestLevel, _displayLevel * 0.82f);
        _latestLevel *= 0.8f;
        _levelMeter.Level = _session.State == DictationState.Listening ? _displayLevel : 0;

        if (_session.State == DictationState.Listening && _overlay.Visible)
        {
            _overlay.UpdateListening(_session.Elapsed, _displayedSegments);
        }
    }

    private void ApplyState(DictationState state)
    {
        switch (state)
        {
            case DictationState.Listening:
                _recordButton.Text = UiText.Stop;
                _recordButton.BackColor = Theme.Danger;
                _recordButton.HoverColor = Color.FromArgb(185, 28, 28);
                _recordButton.Enabled = true;
                _transcriptBox.ReadOnly = true;
                break;
            case DictationState.Finalizing:
                _recordButton.Text = UiText.Finishing;
                _recordButton.BackColor = Theme.Warning;
                _recordButton.HoverColor = Theme.Warning;
                _recordButton.Enabled = false;
                _transcriptBox.ReadOnly = true;
                break;
            case DictationState.Error:
                _recordButton.Text = UiText.Start;
                _recordButton.BackColor = Theme.Accent;
                _recordButton.HoverColor = Theme.AccentHover;
                _recordButton.Enabled = true;
                _transcriptBox.ReadOnly = false;
                _overlay.HideOverlay();
                break;
            default:
                _recordButton.Text = UiText.Start;
                _recordButton.BackColor = Theme.Accent;
                _recordButton.HoverColor = Theme.AccentHover;
                _recordButton.Enabled = true;
                _transcriptBox.ReadOnly = false;
                _overlay.HideOverlay();
                break;
        }

        _tray.SetState(state);
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        var state = _session?.State ?? DictationState.Idle;
        _statusLabel.Text = state switch
        {
            DictationState.Listening => UiText.Listening,
            DictationState.Finalizing => UiText.Finishing,
            DictationState.Error => UiText.Error,
            _ => UiText.Ready
        };
        _statusLabel.ForeColor = state switch
        {
            DictationState.Listening => Theme.Danger,
            DictationState.Finalizing => Theme.Warning,
            DictationState.Error => Theme.Danger,
            _ => Theme.Success
        };
    }

    private void SetTransientStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
        _transientStatusUntil = DateTimeOffset.UtcNow.AddSeconds(2.5);
    }

    private void UpdateMeta()
    {
        var language = string.Equals(_settings.Language, "auto", StringComparison.OrdinalIgnoreCase)
            ? UiText.LanguageAuto
            : UiText.LanguageSpanish;
        var key = string.IsNullOrWhiteSpace(_apiKey) ? UiText.KeyMissing : UiText.KeyReady;
        _metaLabel.Text = $"🧠 {_settings.Model} · {language} · {key} · ☁️ OpenAI";
        _metaLabel.ForeColor = string.IsNullOrWhiteSpace(_apiKey) ? Theme.Warning : Theme.Muted;
    }

    private void UpdateReadiness()
    {
        var missingKey = string.IsNullOrWhiteSpace(_apiKey);
        var missingConsent = !_settings.ConsentAcceptedAtUtc.HasValue;
        _banner.Visible = missingKey || missingConsent;
        _bannerLabel.Text = missingConsent ? UiText.ConsentRequired : UiText.KeyRequired;
        _setupLink.Visible = missingKey || missingConsent;
        UpdateMeta();
    }

    private bool CanDictate() =>
        !string.IsNullOrWhiteSpace(_apiKey) && _settings.ConsentAcceptedAtUtc.HasValue;

    private void OpenSetup()
    {
        if (_setupOpen || Modal)
        {
            return;
        }

        _setupOpen = true;
        _logger.Info("Opening setup dialog.");
        try
        {
            using var form = new OnboardingForm(_settingsService, _keyStore, _logger, _httpClient, _settings);
            var result = form.ShowDialog(this);
            _logger.Info($"Setup dialog closed with {result}.");
            if (result != DialogResult.OK)
            {
                return;
            }

            _settings = _settingsService.Load();
            _apiKey = _keyStore.Load();
            UpdateReadiness();
            UpdateMeta();
            _logger.Info("Setup completed.");
        }
        catch (Exception ex)
        {
            _logger.Error("The setup dialog failed.", ex);
            ShowMessage($"Setup could not be opened: {ex.Message}", "Tralalero", MessageBoxIcon.Error);
        }
        finally
        {
            _setupOpen = false;
        }
    }

    private void OpenSettings()
    {
        if (Modal)
        {
            return;
        }

        var previous = _settings.Clone();
        using var form = new SettingsForm(_settingsService, _keyStore, _logger, _httpClient, _settings);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings = _settingsService.Load();
        _apiKey = _keyStore.Load();

        if (!TryApplyHotkey(_settings.Hotkey, out var hotkeyError))
        {
            _settings.Hotkey = previous.Hotkey;
            _settingsService.Save(_settings);
            TryApplyHotkey(previous.Hotkey, out _);
            ShowMessage(
                hotkeyError ?? "The new hotkey could not be registered.",
                "Hotkey",
                MessageBoxIcon.Warning);
        }

        try
        {
            StartupManager.SetEnabled(_settings.LaunchAtStartup);
        }
        catch (Exception ex)
        {
            _logger.Warn("Could not update the Windows startup setting.", ex);
        }

        UpdateReadiness();
        UpdateMeta();
    }

    private bool TryApplyHotkey(string hotkey, out string? error)
    {
        error = null;
        if (_hotkey is null)
        {
            return true;
        }

        if (!HotkeyDefinition.TryParse(hotkey, out var definition))
        {
            error = "The hotkey needs at least one modifier and a key. Example: Ctrl+Alt+Space.";
            return false;
        }

        if (!_hotkey.Register(definition))
        {
            error = "That hotkey is already in use by another application. Choose another one.";
            return false;
        }

        _hotkeyRegistered = true;
        return true;
    }

    private void CopyTranscript()
    {
        var text = _transcriptBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (_textInjector.CopyText(text))
        {
            SetTransientStatus(UiText.Copied, Theme.Success);
        }
        else
        {
            SetTransientStatus("⚠️ Clipboard was busy", Theme.Warning);
        }
    }

    private void SaveTranscript()
    {
        var text = _transcriptBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save transcript",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = "txt",
            AddExtension = true,
            FileName = $"tralalero-{DateTime.Now:yyyyMMdd-HHmm}.txt"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            SetTransientStatus(UiText.Saved, Theme.Success);
        }
        catch (Exception ex)
        {
            _logger.Warn("Could not save the transcript.", ex);
            ShowMessage($"Could not save the transcript: {ex.Message}", "Save", MessageBoxIcon.Warning);
        }
    }

    private void ClearTranscript()
    {
        if (_transcriptBox.TextLength == 0)
        {
            return;
        }

        _transcriptBox.Clear();
        _displayedSegments = 0;
        UpdateCount();
        SetTransientStatus(UiText.Cleared, Theme.Muted);
        _logger.Info("Transcript cleared.");
    }

    private void UpdateCount()
    {
        var text = _transcriptBox.Text;
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var phraseWord = _displayedSegments == 1 ? "phrase" : "phrases";
        _countLabel.Text = $"{_displayedSegments} {phraseWord} · {words} words · {text.Length} chars";
        _countLabel.Left = Math.Max(0, _countLabel.Parent?.ClientSize.Width - _countLabel.Width ?? 0);
        var hasText = !string.IsNullOrWhiteSpace(text);
        _copyButton.Enabled = hasText;
        _saveButton.Enabled = hasText;
        _clearButton.Enabled = hasText;
    }

    private void OpenLogs()
    {
        try
        {
            AppPaths.OpenInExplorer(AppPaths.LogsDirectory);
        }
        catch (Exception ex)
        {
            _logger.Warn("Could not open the logs folder.", ex);
            ShowMessage($"Could not open the logs folder: {ex.Message}", "Tralalero", MessageBoxIcon.Warning);
        }
    }

    private void ShowAbout()
    {
        var version = Application.ProductVersion;
        MessageBox.Show(
            this,
            "🎙️ Tralalero\n\n" +
            $"Version {version}\n" +
            "Near-live dictation for Windows, powered by OpenAI.\n\n" +
            "🔒 Audio is sent to OpenAI for transcription.\n" +
            "Tralalero stores no audio and keeps transcripts in memory.\n\n" +
            "Audiophile-grade silence detection. Questionable name.",
            "About Tralalero",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private void OnResize(object? sender, EventArgs e)
    {
        if (WindowState != FormWindowState.Minimized || !_settings.CloseToTray || _exiting)
        {
            return;
        }

        Hide();
        _tray.ShowBalloon("Tralalero", "Still running in the tray. Double-click the icon to return.", ToolTipIcon.Info);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_exiting && _settings.CloseToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            _tray.ShowBalloon("Tralalero", "Still running in the tray. Use the tray menu to exit.", ToolTipIcon.Info);
            return;
        }

        CleanupSession();
        _uiTimer.Stop();
        _uiTimer.Dispose();
        _hotkey?.Dispose();
        _hotkey = null;
        _overlay.Dispose();
        _tray.Dispose();
        _toolTip.Dispose();
    }

    private void ExitApplication()
    {
        _exiting = true;
        try
        {
            _session?.Dispose();
            _session = null;
        }
        catch (Exception ex)
        {
            _logger.Warn("Could not stop the active session while exiting.", ex);
        }

        Close();
        Application.Exit();
    }

    private void CleanupSession()
    {
        var session = _session;
        _session = null;
        if (session is not null)
        {
            try
            {
                session.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Warn("Could not dispose the dictation session.", ex);
            }
        }

        _overlay.HideOverlay();
        _displayLevel = 0;
        _latestLevel = 0;
        _levelMeter.Level = 0;
        _recordButton.Enabled = true;
        _transcriptBox.ReadOnly = false;
        ApplyState(DictationState.Idle);
        UpdateReadiness();
    }

    private void RunOnUi(Action action)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // The form is closing.
            }
        }
        else
        {
            action();
        }
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        var total = Math.Max(0, (int)elapsed.TotalSeconds);
        return $"{total / 60:00}:{total % 60:00}";
    }

    private void ShowMessage(string text, string caption, MessageBoxIcon icon)
    {
        MessageBox.Show(this, text, caption, MessageBoxButtons.OK, icon);
    }
}
