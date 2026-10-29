using System.Windows.Forms;
using Tralalero.Core.Interop;

namespace Tralalero.Tests;

public sealed class HotkeyDefinitionTests
{
    [Fact]
    public void TryParse_AcceptsTheDefaultHotkey()
    {
        var parsed = HotkeyDefinition.TryParse("Ctrl+Alt+Space", out var hotkey);

        Assert.True(parsed);
        Assert.True(hotkey.Control);
        Assert.True(hotkey.Alt);
        Assert.False(hotkey.Shift);
        Assert.False(hotkey.Win);
        Assert.Equal(Keys.Space, hotkey.Key);
    }

    [Fact]
    public void TryParse_RejectsAKeyWithoutAModifier()
    {
        Assert.False(HotkeyDefinition.TryParse("Space", out _));
    }

    [Fact]
    public void FromKeyData_ExtractsModifiersAndKey()
    {
        var hotkey = HotkeyDefinition.FromKeyData(Keys.Control | Keys.Shift | Keys.F5);

        Assert.True(hotkey.Control);
        Assert.False(hotkey.Alt);
        Assert.True(hotkey.Shift);
        Assert.Equal(Keys.F5, hotkey.Key);
        Assert.Equal("Ctrl+Shift+F5", hotkey.Display);
    }

    [Fact]
    public void Display_IsRoundTrippable()
    {
        var original = new HotkeyDefinition(Keys.D, Control: true, Alt: true, Shift: true, Win: true);

        Assert.True(HotkeyDefinition.TryParse(original.Display, out var parsed));
        Assert.Equal(original, parsed);
    }
}
