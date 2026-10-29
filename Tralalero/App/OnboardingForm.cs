using System.Diagnostics;
using Tralalero.Core.Infrastructure;
using Tralalero.Core.OpenAI;
using Tralalero.Core.Security;
using Tralalero.Core.Settings;

namespace Tralalero.App;

internal sealed class OnboardingForm : Form
{
    private readonly SettingsService _settingsService;
    private readonly ApiKeyStore _keyStore;
    private readonly IAppLogger _logger;
    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;

    private readonly CheckBox _consent;
    private readonly TextBox _keyBox;
    private readonly Label _status;
    private readonly LinkLabel _keylessLink;
    private readonly ThemedButton _continueButton;
    private readonly ThemedButton _skipButton;

    public OnboardingForm(
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
        _settings = settings;

        Text = "Tralalero — Setup";
        Icon = AppIcon.Load();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Background;
        Font = Theme.Body;
        ClientSize = new Size(600, 520);
        Padding = new Padding(26);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 10,
            BackColor = Color.Transparent
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 8; i++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "👋 Welcome to Tralalero",
            Font = Theme.Title,
            ForeColor = Theme.Text,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };
        var subtitle = new Label
        {
            Text = "Near-live dictation for Windows, powered by your transcription endpoint.",
            Font = Theme.Subtitle,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 14)
        };

        var privacyCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Height = 128,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 0, 12)
        };
        var privacyTitle = new Label
        {
            Text = "🔐 Before we start",
            Font = Theme.BodyBold,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(16, 12)
        };
        var privacyText = new Label
        {
            Text = "Your microphone audio is sent to your configured transcription endpoint\n" +
                   "(OpenAI by default). Tralalero does not store your audio.\n" +
                   "You can change the endpoint and remove your API key in Settings.",
            Font = Theme.Body,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Location = new Point(16, 38),
            Size = new Size(520, 66)
        };
        privacyCard.Controls.Add(privacyTitle);
        privacyCard.Controls.Add(privacyText);

        _consent = new CheckBox
        {
            Text = "I understand and agree to send my audio to my configured transcription endpoint.",
            Font = Theme.Body,
            ForeColor = Theme.Text,
            BackColor = Theme.Background,
            AutoSize = true,
            Checked = settings.ConsentAcceptedAtUtc.HasValue,
            Margin = new Padding(0, 0, 0, 14)
        };

        var keyLabel = new Label
        {
            Text = "🔑 OpenAI API key",
            Font = Theme.BodyBold,
            ForeColor = Theme.Text,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        _keyBox = new TextBox
        {
            UseSystemPasswordChar = true,
            Font = Theme.Body,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Top,
            Height = 30,
            Text = _keyStore.Load() ?? string.Empty,
            Margin = new Padding(0, 0, 0, 4)
        };

        var keyLink = new LinkLabel
        {
            Text = "Get an API key ↗",
            Font = Theme.Small,
            LinkColor = Theme.Accent,
            ActiveLinkColor = Theme.AccentHover,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        keyLink.LinkClicked += (_, _) => OpenUrl("https://platform.openai.com/api-keys");

        _keylessLink = new LinkLabel
        {
            Text = "Using a local endpoint without a key? Continue without a key →",
            Font = Theme.Small,
            LinkColor = Theme.Accent,
            ActiveLinkColor = Theme.AccentHover,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        _keylessLink.LinkClicked += KeylessEndpointClicked;

        var keyLinks = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6)
        };
        keyLinks.Controls.Add(keyLink);
        keyLinks.Controls.Add(_keylessLink);

        _status = new Label
        {
            Text = "Your key is encrypted with Windows DPAPI and never written to logs.",
            Font = Theme.Small,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 0)
        };
        _continueButton = new ThemedButton
        {
            Text = "✅ Continue",
            Width = 132,
            Height = 42,
            BackColor = Theme.Accent,
            HoverColor = Theme.AccentHover,
            ForeColor = Color.White,
            Margin = new Padding(8, 0, 0, 0)
        };
        _continueButton.Click += ContinueClicked;
        _skipButton = new ThemedButton
        {
            Text = "Skip for now",
            Width = 118,
            Height = 42,
            BackColor = Theme.SurfaceMuted,
            HoverColor = Theme.Border,
            ForeColor = Theme.Text,
            Margin = new Padding(8, 0, 0, 0)
        };
        _skipButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        buttons.Controls.Add(_continueButton);
        buttons.Controls.Add(_skipButton);

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(subtitle, 0, 1);
        root.Controls.Add(privacyCard, 0, 2);
        root.Controls.Add(_consent, 0, 3);
        root.Controls.Add(keyLabel, 0, 4);
        root.Controls.Add(_keyBox, 0, 5);
        root.Controls.Add(keyLinks, 0, 6);
        root.Controls.Add(_status, 0, 7);
        root.Controls.Add(buttons, 0, 9);

        Controls.Add(root);
        AcceptButton = _continueButton;
        CancelButton = _skipButton;
    }

    private async void ContinueClicked(object? sender, EventArgs e)
    {
        if (!_consent.Checked)
        {
            SetStatus("⚠️ Please accept the privacy notice first.", Theme.Warning);
            return;
        }

        var key = _keyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            SetStatus("⚠️ Paste your OpenAI API key to continue.", Theme.Warning);
            return;
        }

        SetBusy(true);
        SetStatus("⏳ Checking your API key…", Theme.Muted);

        try
        {
            var client = new OpenAiClient(_httpClient, key, _settings.ApiBaseUrl, _logger);
            var valid = await client.ValidateApiKeyAsync();
            if (!valid)
            {
                SetStatus("❌ OpenAI rejected that key. Check it and try again.", Theme.Danger);
                return;
            }

            _keyStore.Save(key);
            _settings.ConsentAcceptedAtUtc = DateTimeOffset.UtcNow;
            _settingsService.Save(_settings);
            _logger.Info("Onboarding completed.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OpenAiException ex)
        {
            SetStatus($"⚠️ {ex.Message}", Theme.Warning);
        }
        catch (Exception ex)
        {
            _logger.Error("Onboarding key check failed.", ex);
            SetStatus("⚠️ Could not verify the key. Check your connection.", Theme.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void KeylessEndpointClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (!_consent.Checked)
        {
            SetStatus("⚠️ Please accept the privacy notice first.", Theme.Warning);
            return;
        }

        _settings.ConsentAcceptedAtUtc = DateTimeOffset.UtcNow;
        _settingsService.Save(_settings);
        _logger.Info("Onboarding completed without an API key; configure a custom endpoint.");
        DialogResult = DialogResult.OK;
        Close();
    }

    private void SetBusy(bool busy)
    {
        _continueButton.Enabled = !busy;
        _skipButton.Enabled = !busy;
        _keylessLink.Enabled = !busy;
        _keyBox.Enabled = !busy;
        _consent.Enabled = !busy;
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.ForeColor = color;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // The link is informational; not being able to open a browser is not fatal.
        }
    }
}
