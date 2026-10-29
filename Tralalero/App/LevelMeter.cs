using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Tralalero.App;

internal sealed class LevelMeter : Control
{
    private float _level;

    public LevelMeter()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Height = 8;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Level
    {
        get => _level;
        set
        {
            var next = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(next - _level) < 0.002f)
            {
                return;
            }

            _level = next;
            Invalidate();
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var track = Theme.RoundedRectangle(rect, Height / 2);
        using (var brush = new SolidBrush(Theme.SurfaceMuted))
        {
            e.Graphics.FillPath(brush, track);
        }

        var width = (int)Math.Round(rect.Width * _level);
        if (width <= 1)
        {
            return;
        }

        var fillRect = new Rectangle(rect.X, rect.Y, Math.Max(2, width), rect.Height);
        using var fill = Theme.RoundedRectangle(fillRect, Height / 2);
        using var fillBrush = new SolidBrush(Theme.Accent);
        e.Graphics.FillPath(fillBrush, fill);
    }
}
