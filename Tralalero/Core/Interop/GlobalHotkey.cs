using System.Windows.Forms;

namespace Tralalero.Core.Interop;

/// <summary>Registers a system-wide hotkey on an existing window handle.</summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int HotkeyId = 0x7A21;
    private readonly IntPtr _windowHandle;
    private bool _registered;

    public GlobalHotkey(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public event EventHandler? Pressed;

    public bool Register(HotkeyDefinition hotkey)
    {
        Unregister();

        if (_windowHandle == IntPtr.Zero)
        {
            return false;
        }

        var modifiers = hotkey.Modifiers | NativeMethods.ModNoRepeat;
        _registered = NativeMethods.RegisterHotKey(_windowHandle, HotkeyId, modifiers, (uint)hotkey.Key);
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);
        _registered = false;
    }

    /// <summary>Call from the host form's WndProc.</summary>
    public bool HandleMessage(ref Message message)
    {
        if (message.Msg != NativeMethods.WmHotkey || message.WParam.ToInt32() != HotkeyId)
        {
            return false;
        }

        Pressed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Dispose()
    {
        Unregister();
        Pressed = null;
    }
}
