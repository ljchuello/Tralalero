using System.ComponentModel;

namespace Tralalero.App;

/// <summary>
/// A flat, theme-aware button. It intentionally relies on WinForms' native
/// painting instead of owner-drawing, which avoids transparent-corner artifacts
/// on high-DPI and remote-desktop sessions.
/// </summary>
internal sealed class ThemedButton : Button
{
    private Color _hoverColor = Theme.AccentHover;
    private Color _borderColor = Color.Transparent;

    public ThemedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Theme.AccentHover;
        FlatAppearance.MouseDownBackColor = Theme.AccentHover;
        BackColor = Theme.Accent;
        ForeColor = Color.White;
        Font = Theme.BodyBold;
        Height = 40;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverColor
    {
        get => _hoverColor;
        set
        {
            _hoverColor = value;
            FlatAppearance.MouseOverBackColor = value;
            FlatAppearance.MouseDownBackColor = value;
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            FlatAppearance.BorderColor = value;
            FlatAppearance.BorderSize = value == Color.Transparent ? 0 : 1;
        }
    }
}
