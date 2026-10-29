using System.Text;

namespace Tralalero.Core.Interop;

/// <summary>A global hotkey described without depending on Win32 at the call site.</summary>
public readonly record struct HotkeyDefinition(Keys Key, bool Control, bool Alt, bool Shift, bool Win)
{
    public static HotkeyDefinition Default => new(Keys.Space, Control: true, Alt: true, Shift: false, Win: false);

    public uint Modifiers
    {
        get
        {
            var modifiers = 0u;
            if (Alt)
            {
                modifiers |= NativeMethods.ModAlt;
            }

            if (Control)
            {
                modifiers |= NativeMethods.ModControl;
            }

            if (Shift)
            {
                modifiers |= NativeMethods.ModShift;
            }

            if (Win)
            {
                modifiers |= NativeMethods.ModWin;
            }

            return modifiers;
        }
    }

    public string Display
    {
        get
        {
            var builder = new StringBuilder();
            if (Control)
            {
                builder.Append("Ctrl+");
            }

            if (Alt)
            {
                builder.Append("Alt+");
            }

            if (Shift)
            {
                builder.Append("Shift+");
            }

            if (Win)
            {
                builder.Append("Win+");
            }

            builder.Append(Key);
            return builder.ToString();
        }
    }

    public static HotkeyDefinition FromKeyData(Keys keyData)
    {
        var modifiers = keyData & Keys.Modifiers;
        var key = keyData & Keys.KeyCode;
        return new HotkeyDefinition(
            key,
            (modifiers & Keys.Control) != 0,
            (modifiers & Keys.Alt) != 0,
            (modifiers & Keys.Shift) != 0,
            Win: false);
    }

    public static bool TryParse(string? value, out HotkeyDefinition result)
    {
        result = Default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var control = false;
        var alt = false;
        var shift = false;
        var win = false;
        var key = Keys.None;

        foreach (var rawPart in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = rawPart.Trim();
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    control = true;
                    continue;
                case "alt":
                    alt = true;
                    continue;
                case "shift":
                    shift = true;
                    continue;
                case "win":
                case "windows":
                    win = true;
                    continue;
            }

            if (!Enum.TryParse<Keys>(part, ignoreCase: true, out var parsed) || parsed == Keys.None)
            {
                return false;
            }

            key = parsed;
        }

        if (key == Keys.None || IsModifierKey(key) || (!control && !alt && !shift && !win))
        {
            return false;
        }

        result = new HotkeyDefinition(key, control, alt, shift, win);
        return true;
    }

    public static bool IsModifierKey(Keys key) =>
        key is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
            or Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey
            or Keys.Menu or Keys.LMenu or Keys.RMenu
            or Keys.LWin or Keys.RWin;

    public override string ToString() => Display;
}
