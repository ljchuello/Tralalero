using System.Drawing.Drawing2D;

namespace Tralalero.App;

/// <summary>A compact, non-activating status pill shown while dictating in another app.</summary>
internal sealed class RecordingOverlay : Form
{
    private readonly Label _status;
    private readonly Label _timer;
    private readonly Label _detail;

    public RecordingOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(320, 72);
        BackColor = Color.FromArgb(15, 23, 42);
        DoubleBuffered = true;
        Padding = new Padding(16);

        _status = new Label
        {
            AutoSize = true,
            Location = new Point(16, 10),
            Font = Theme.BodyBold,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Text = "🔴 Listening"
        };
        _timer = new Label
        {
            AutoSize = false,
            Size = new Size(82, 24),
            Location = new Point(Width - 98, 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = Theme.Timer,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleRight,
            Text = "00:00"
        };
        _detail = new Label
        {
            AutoSize = false,
            Location = new Point(16, 39),
            Size = new Size(Width - 32, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = Theme.Small,
            ForeColor = Color.FromArgb(203, 213, 225),
            BackColor = Color.Transparent,
            Text = "Ctrl+Alt+Space to stop"
        };

        Controls.Add(_status);
        Controls.Add(_timer);
        Controls.Add(_detail);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            return parameters;
        }
    }

    public void ShowListening(TimeSpan elapsed, int phrases)
    {
        UpdateListening(elapsed, phrases);
        if (!Visible)
        {
            Show();
        }

        UpdatePosition();
    }

    public void UpdateListening(TimeSpan elapsed, int phrases)
    {
        _status.Text = "🔴 Listening";
        _timer.Text = FormatElapsed(elapsed);
        _detail.Text = phrases == 1
            ? "1 phrase · Ctrl+Alt+Space to stop"
            : $"{phrases} phrases · Ctrl+Alt+Space to stop";
    }

    public void ShowFinishing(int phrases)
    {
        _status.Text = "⏳ Finishing";
        _detail.Text = phrases == 1 ? "1 phrase left…" : $"{phrases} phrases left…";
        if (!Visible)
        {
            Show();
        }

        UpdatePosition();
    }

    public void HideOverlay()
    {
        if (Visible)
        {
            Hide();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = Theme.RoundedRectangle(new Rectangle(0, 0, Width, Height), 18);
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        var total = Math.Max(0, (int)elapsed.TotalSeconds);
        return $"{total / 60:00}:{total % 60:00}";
    }

    private void UpdatePosition()
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var area = screen.WorkingArea;
        Location = new Point(area.Right - Width - 18, area.Bottom - Height - 18);
    }
}
