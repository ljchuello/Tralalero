using System.Diagnostics;
using Tralalero.Core.Audio;
using Tralalero.Core.Infrastructure;
using Tralalero.Core.Interop;
using Tralalero.Core.OpenAI;
using Tralalero.Core.Security;
using Tralalero.Core.Settings;

namespace Tralalero.App;

internal sealed class SettingsForm : Form
{
    private sealed record LanguageOption(string Code, string Display)
    {
        public override string ToString() => Display;
    }

    private sealed record SensitivityOption(VadSensitivity Value, string Display)
    {
        public override string ToString() => Display;
    }

    private readonly SettingsService _settingsService;
    private readonly ApiKeyStore _keyStore;
    private readonly IAppLogger _logger;
    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;

    private readonly TextBox _keyBox;
    private readonly Label _keyStatus;
    private readonly ComboBox _modelCombo;
    private readonly ComboBox _languageCombo;
    private readonly ComboBox _deviceCombo;
    private readonly ComboBox _sensitivityCombo;
    private readonly TextBox _hotkeyBox;
    private readonly NumericUpDown _maxMinutes;
    private readonly CheckBox _autoCopy;
    private readonly CheckBox _autoPaste;
    private readonly CheckBox _closeToTray;
    private readonly CheckBox _launchAtStartup;
    private readonly ThemedButton _saveButton;
    private readonly ThemedButton _cancelButton;
    private bool _removeKey;

    public SettingsForm(
        SettingsService settingsService,
        ApiKeyStore keyStore,
        IAppLogger logger,
        HttpClient httpClient,
        AppSettings settings)
    {
        _settingsService = settingsService;
        _keyStore = keyStore;
        _logger = logger;
        _httpClient = httpClient;
        _settings = settings.Clone();

        Text = "Tralalero — Settings";
        Icon = AppIcon.Load();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Background;
        Font = Theme.Body;
        ClientSize = new Size(720, 880);
        AutoScroll = true;
        Padding = new Padding(24);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var apiGrid = CreateGrid();
        var apiCard = CreateCard("🔑 OpenAI account", apiGrid);

        _keyBox = new TextBox
        {
            UseSystemPasswordChar = true,
            Font = Theme.Body,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Text = _keyStore.Load() ?? string.Empty
        };
        _keyStatus = new Label
        {
            Text = string.IsNullOrWhiteSpace(_keyBox.Text)
                ? "🔑 No API key stored."
                : "🔒 Stored encrypted with Windows DPAPI.",
            Font = Theme.Small,
            ForeColor = _keyBox.Text.Length == 0 ? Theme.Warning : Theme.Success,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0)
        };
        AddRow(apiGrid, "API key", _keyBox);
        AddGridRow(apiGrid, _keyStatus);

        var keyButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0)
        };
        var testButton = CreateSecondaryButton("Test key");
        testButton.Click += TestKeyClicked;
        var removeButton = CreateSecondaryButton("Remove key");
        removeButton.Click += RemoveKeyClicked;
        keyButtons.Controls.Add(testButton);
        keyButtons.Controls.Add(removeButton);
        AddGridRow(apiGrid, keyButtons);

        _modelCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Theme.Body,
            Dock = DockStyle.Fill
        };
        foreach (var model in OpenAiModels.Known)
        {
            _modelCombo.Items.Add(model);
        }

        _modelCombo.SelectedItem = OpenAiModels.Known.FirstOrDefault(model => model.Id == _settings.Model)
            ?? OpenAiModels.Known[0];
        AddRow(apiGrid, "Transcription model", _modelCombo);

        _languageCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Theme.Body,
            Dock = DockStyle.Fill
        };
        _languageCombo.Items.Add(new LanguageOption("es", "🇪🇸 Spanish"));
        _languageCombo.Items.Add(new LanguageOption("auto", "🌎 Auto-detect"));
        _languageCombo.SelectedItem = _languageCombo.Items.Cast<LanguageOption>()
            .FirstOrDefault(option => option.Code == _settings.Language)
            ?? _languageCombo.Items[0];
        AddRow(apiGrid, "Language", _languageCombo);

        var dictationGrid = CreateGrid();
        var dictationCard = CreateCard("🎙️ Dictation", dictationGrid);

        _deviceCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Theme.Body,
            Dock = DockStyle.Fill
        };
        var devices = AudioRecorder.GetDevices();
        foreach (var device in devices)
        {
            _deviceCombo.Items.Add(device);
        }

        if (_deviceCombo.Items.Count == 0)
        {
            _deviceCombo.Items.Add(new AudioDeviceInfo(-1, "No microphone found", 0));
        }

        _deviceCombo.SelectedItem = _deviceCombo.Items.Cast<AudioDeviceInfo>()
            .FirstOrDefault(device => device.Name == _settings.InputDeviceName)
            ?? _deviceCombo.Items[0];
        AddRow(dictationGrid, "Microphone", _deviceCombo);

        _sensitivityCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Theme.Body,
            Dock = DockStyle.Fill
        };
        _sensitivityCombo.Items.Add(new SensitivityOption(VadSensitivity.Low, "Low — noisy rooms"));
        _sensitivityCombo.Items.Add(new SensitivityOption(VadSensitivity.Medium, "Medium — balanced"));
        _sensitivityCombo.Items.Add(new SensitivityOption(VadSensitivity.High, "High — quiet rooms"));
        _sensitivityCombo.SelectedItem = _sensitivityCombo.Items.Cast<SensitivityOption>()
            .FirstOrDefault(option => option.Value == _settings.VadSensitivity)
            ?? _sensitivityCombo.Items[1];
        AddRow(dictationGrid, "Voice sensitivity", _sensitivityCombo);

        _hotkeyBox = new TextBox
        {
            ReadOnly = true,
            ShortcutsEnabled = false,
            Font = Theme.Body,
            BorderStyle = BorderStyle.FixedSingle,
            Width = 220,
            Text = _settings.Hotkey
        };
        _hotkeyBox.KeyDown += HotkeyBoxKeyDown;
        var hotkeyPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };
        hotkeyPanel.Controls.Add(_hotkeyBox);
        var resetHotkey = CreateSecondaryButton("Reset");
        resetHotkey.Margin = new Padding(8, 0, 0, 0);
        resetHotkey.Click += (_, _) => _hotkeyBox.Text = HotkeyDefinition.Default.Display;
        hotkeyPanel.Controls.Add(resetHotkey);
        AddRow(dictationGrid, "Global hotkey", hotkeyPanel);
        var hotkeyHint = new Label
        {
            Text = "Click the field and press the new shortcut. At least one modifier is required.",
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0)
        };
        AddGridRow(dictationGrid, hotkeyHint);

        _maxMinutes = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 60,
            Value = _settings.MaxRecordingMinutes,
            Font = Theme.Body,
            Dock = DockStyle.Left,
            Width = 80
        };
        AddRow(dictationGrid, "Maximum recording (minutes)", _maxMinutes);

        var behaviorGrid = CreateGrid();
        var behaviorCard = CreateCard("⚙️ Behavior", behaviorGrid);

        _autoCopy = new CheckBox
        {
            Text = "📋 Copy the transcript to the clipboard when done",
            Checked = _settings.AutoCopy,
            AutoSize = true,
            Font = Theme.Body
        };
        _autoPaste = new CheckBox
        {
            Text = "⌨️ Paste into the app that was active when recording started",
            Checked = _settings.AutoPaste,
            AutoSize = true,
            Font = Theme.Body
        };
        _closeToTray = new CheckBox
        {
            Text = "🪟 Closing the window keeps Tralalero in the tray",
            Checked = _settings.CloseToTray,
            AutoSize = true,
            Font = Theme.Body
        };
        _launchAtStartup = new CheckBox
        {
            Text = "🚀 Start Tralalero with Windows",
            Checked = _settings.LaunchAtStartup,
            AutoSize = true,
            Font = Theme.Body
        };
        AddGridRow(behaviorGrid, _autoCopy);
        AddGridRow(behaviorGrid, _autoPaste);
        AddGridRow(behaviorGrid, _closeToTray);
        AddGridRow(behaviorGrid, _launchAtStartup);

        var linksGrid = CreateGrid();
        var linksCard = CreateCard("📂 Files and help", linksGrid);
        var links = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };
        links.Controls.Add(CreateLink("Open logs", () => AppPaths.OpenInExplorer(AppPaths.LogsDirectory)));
        links.Controls.Add(CreateLink("Open data folder", () => AppPaths.OpenInExplorer(AppPaths.AppDataDirectory)));
        links.Controls.Add(CreateLink("Get API key", () => OpenUrl("https://platform.openai.com/api-keys")));
        AddRow(linksGrid, "Folders", links);

        root.Controls.Add(apiCard);
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(dictationCard);
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(behaviorCard);
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(linksCard);
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Dock = DockStyle.Right,
            Margin = new Padding(0, 8, 0, 0)
        };
        _saveButton = new ThemedButton
        {
            Text = "💾 Save",
            Width = 118,
            Height = 42,
            BackColor = Theme.Accent,
            HoverColor = Theme.AccentHover,
            ForeColor = Color.White,
            Margin = new Padding(8, 0, 0, 0)
        };
        _saveButton.Click += SaveClicked;
        _cancelButton = new ThemedButton
        {
            Text = "Cancel",
            Width = 100,
            Height = 42,
            BackColor = Theme.SurfaceMuted,
            HoverColor = Theme.Border,
            ForeColor = Theme.Text,
            Margin = new Padding(8, 0, 0, 0)
        };
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        buttons.Controls.Add(_saveButton);
        buttons.Controls.Add(_cancelButton);
        root.Controls.Add(buttons);
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Controls.Add(root);
        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
    }

    private static TableLayoutPanel CreateGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return grid;
    }

    private CardPanel CreateCard(string title, TableLayoutPanel grid)
    {
        var card = new CardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 0, 12)
        };
        var titleLabel = new Label
        {
            Text = title,
            Font = Theme.BodyBold,
            ForeColor = Theme.Accent,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.Controls.Add(titleLabel, 0, 0);
        stack.Controls.Add(grid, 0, 1);
        card.Controls.Add(stack);
        return card;
    }

    private static void AddRow(TableLayoutPanel grid, string labelText, Control control)
    {
        var row = grid.RowCount;
        grid.RowCount = row + 1;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = labelText,
            Font = Theme.Body,
            ForeColor = Theme.Text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 8)
        };
        control.Margin = new Padding(0, 6, 0, 6);
        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private static void AddGridRow(TableLayoutPanel grid, Control control)
    {
        var row = grid.RowCount;
        grid.RowCount = row + 1;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        control.Margin = new Padding(0, 4, 0, 4);
        grid.Controls.Add(control, 1, row);
    }

    private ThemedButton CreateSecondaryButton(string text) => new()
    {
        Text = text,
        Width = 104,
        Height = 34,
        BackColor = Theme.SurfaceMuted,
        HoverColor = Theme.Border,
        ForeColor = Theme.Text,
        BorderColor = Theme.Border,
        Font = Theme.SmallBold,
        Margin = new Padding(0, 0, 8, 0)
    };

    private static LinkLabel CreateLink(string text, Action action)
    {
        var link = new LinkLabel
        {
            Text = text,
            Font = Theme.Body,
            LinkColor = Theme.Accent,
            ActiveLinkColor = Theme.AccentHover,
            AutoSize = true,
            Margin = new Padding(0, 0, 18, 0)
        };
        link.LinkClicked += (_, _) => action();
        return link;
    }

    private void HotkeyBoxKeyDown(object? sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;

        if (e.KeyCode == Keys.Escape)
        {
            _hotkeyBox.Text = HotkeyDefinition.Default.Display;
            return;
        }

        if (HotkeyDefinition.IsModifierKey(e.KeyCode))
        {
            return;
        }

        var definition = HotkeyDefinition.FromKeyData(e.KeyData);
        if (!HotkeyDefinition.TryParse(definition.Display, out var parsed))
        {
            _keyStatus.Text = "⚠️ Use at least one modifier (Ctrl, Alt, Shift, or Win).";
            _keyStatus.ForeColor = Theme.Warning;
            return;
        }

        _hotkeyBox.Text = parsed.Display;
        _keyStatus.Text = "⌨️ Hotkey will be applied when you save.";
        _keyStatus.ForeColor = Theme.Muted;
    }

    private async void TestKeyClicked(object? sender, EventArgs e)
    {
        var key = _keyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            key = _keyStore.Load() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            _keyStatus.Text = "🔑 Enter an API key first.";
            _keyStatus.ForeColor = Theme.Warning;
            return;
        }

        _keyStatus.Text = "⏳ Checking key…";
        _keyStatus.ForeColor = Theme.Muted;
        try
        {
            var client = new OpenAiClient(_httpClient, key, _settings.ApiBaseUrl, _logger);
            var valid = await client.ValidateApiKeyAsync();
            _keyStatus.Text = valid
                ? "✅ Key is valid."
                : "❌ OpenAI rejected this key.";
            _keyStatus.ForeColor = valid ? Theme.Success : Theme.Danger;
        }
        catch (OpenAiException ex)
        {
            _keyStatus.Text = $"⚠️ {ex.Message}";
            _keyStatus.ForeColor = Theme.Warning;
        }
        catch (Exception ex)
        {
            _logger.Error("Settings key check failed.", ex);
            _keyStatus.Text = "⚠️ Could not check the key.";
            _keyStatus.ForeColor = Theme.Warning;
        }
    }

    private void RemoveKeyClicked(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            this,
            "Remove the stored OpenAI API key?",
            "Tralalero",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (result != DialogResult.Yes)
        {
            return;
        }

        _keyStore.Delete();
        _keyBox.Clear();
        _removeKey = true;
        _keyStatus.Text = "🔑 API key removed.";
        _keyStatus.ForeColor = Theme.Warning;
        _logger.Info("Stored API key removed from Settings.");
    }

    private void SaveClicked(object? sender, EventArgs e)
    {
        if (!HotkeyDefinition.TryParse(_hotkeyBox.Text, out var hotkey))
        {
            MessageBox.Show(
                this,
                "The hotkey needs at least one modifier and a key. Example: Ctrl+Alt+Space.",
                "Tralalero",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (_modelCombo.SelectedItem is ModelOption model)
        {
            _settings.Model = model.Id;
        }

        if (_languageCombo.SelectedItem is LanguageOption language)
        {
            _settings.Language = language.Code;
        }

        if (_deviceCombo.SelectedItem is AudioDeviceInfo device && device.DeviceNumber >= 0)
        {
            _settings.InputDeviceName = device.Name;
        }

        if (_sensitivityCombo.SelectedItem is SensitivityOption sensitivity)
        {
            _settings.VadSensitivity = sensitivity.Value;
        }

        _settings.Hotkey = hotkey.Display;
        _settings.MaxRecordingMinutes = (int)_maxMinutes.Value;
        _settings.AutoCopy = _autoCopy.Checked;
        _settings.AutoPaste = _autoPaste.Checked;
        _settings.CloseToTray = _closeToTray.Checked;
        _settings.LaunchAtStartup = _launchAtStartup.Checked;

        var key = _keyBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(key))
        {
            _keyStore.Save(key);
            _removeKey = false;
        }
        else if (_removeKey)
        {
            _keyStore.Delete();
        }

        _settingsService.Save(_settings);
        _logger.Info("Settings saved.");
        DialogResult = DialogResult.OK;
        Close();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Informational link.
        }
    }
}
