using System.Runtime.InteropServices;
using System.Windows.Forms;
using Tralalero.Core.Infrastructure;

namespace Tralalero.Core.Interop;

public sealed record PasteResult(bool Pasted, bool Copied, string? TargetTitle);

/// <summary>Copies text and optionally pastes it into a previously captured window.</summary>
public sealed class TextInjector
{
    private readonly IAppLogger _logger;

    public TextInjector(IAppLogger logger)
    {
        _logger = logger;
    }

    public bool CopyText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (ExternalException ex)
            {
                _logger.Warn($"Clipboard was busy (attempt {attempt}/3).", ex);
                Thread.Sleep(60);
            }
            catch (ThreadStateException ex)
            {
                _logger.Warn("Clipboard access requires an STA thread.", ex);
                return false;
            }
        }

        return false;
    }

    public async Task<PasteResult> PasteTextAsync(IntPtr targetWindow, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new PasteResult(false, false, null);
        }

        var copied = CopyText(text);
        if (targetWindow == IntPtr.Zero || !WindowHelper.IsAlive(targetWindow))
        {
            return new PasteResult(false, copied, null);
        }

        var title = WindowHelper.GetTitle(targetWindow);
        try
        {
            var restored = WindowHelper.RestoreForeground(targetWindow);
            if (!restored)
            {
                _logger.Warn($"Could not restore foreground window '{title}'.");
                return new PasteResult(false, copied, title);
            }

            await Task.Delay(120, cancellationToken).ConfigureAwait(true);
            var pasted = SendCtrlV();
            if (!pasted)
            {
                _logger.Warn($"Could not send Ctrl+V to '{title}'.");
            }

            return new PasteResult(pasted, copied, title);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warn($"Auto-paste failed for '{title}'.", ex);
            return new PasteResult(false, copied, title);
        }
    }

    private static bool SendCtrlV()
    {
        var inputs = new[]
        {
            KeyInput(NativeMethods.VkControl, keyUp: false),
            KeyInput(NativeMethods.VkV, keyUp: false),
            KeyInput(NativeMethods.VkV, keyUp: true),
            KeyInput(NativeMethods.VkControl, keyUp: true)
        };

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        return sent == inputs.Length;
    }

    private static NativeMethods.INPUT KeyInput(ushort virtualKey, bool keyUp) => new()
    {
        Type = NativeMethods.InputKeyboard,
        U = new NativeMethods.InputUnion
        {
            Ki = new NativeMethods.KEYBDINPUT
            {
                WVk = virtualKey,
                WScan = 0,
                DwFlags = keyUp ? NativeMethods.KeyeventfKeyup : 0,
                Time = 0,
                DwExtraInfo = IntPtr.Zero
            }
        }
    };
}
