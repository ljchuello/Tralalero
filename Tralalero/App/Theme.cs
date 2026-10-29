using System.Drawing.Drawing2D;

namespace Tralalero.App;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(248, 250, 252);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceMuted = Color.FromArgb(241, 245, 249);
    public static readonly Color Text = Color.FromArgb(15, 23, 42);
    public static readonly Color Muted = Color.FromArgb(100, 116, 139);
    public static readonly Color Accent = Color.FromArgb(79, 70, 229);
    public static readonly Color AccentHover = Color.FromArgb(67, 56, 202);
    public static readonly Color AccentSoft = Color.FromArgb(238, 242, 255);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);
    public static readonly Color Warning = Color.FromArgb(217, 119, 6);
    public static readonly Color Border = Color.FromArgb(226, 232, 240);

    public static readonly Font Body = new("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font BodyBold = new("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Small = new("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font SmallBold = new("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Title = new("Segoe UI", 22f, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Subtitle = new("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font Status = new("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Timer = new("Segoe UI", 16f, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Mono = new("Consolas", 10.5f, FontStyle.Regular, GraphicsUnit.Point);

    public static GraphicsPath RoundedRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        var arc = new Rectangle(rect.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
