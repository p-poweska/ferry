using Ferry.Core;

namespace Ferry.Core.Tests;

public sealed class KeyShortcutTests
{
    [Fact]
    public void WritesShortcutsTheWindowsWay()
    {
        Assert.Equal("Ctrl+Alt+F", KeyShortcut.Standard.ToDisplayString());
        Assert.Equal("Win+Ctrl+Shift+Space", new KeyShortcut(0x20, KeyModifiers.Windows | KeyModifiers.Control | KeyModifiers.Shift).ToDisplayString());
        Assert.Equal("F12", new KeyShortcut(0x7B, KeyModifiers.None).ToDisplayString());
        Assert.Equal("Alt+Num 5", new KeyShortcut(0x65, KeyModifiers.Alt).ToDisplayString());
    }

    [Fact]
    public void AsksForNamesOfKeysItDoesNotKnow()
    {
        var shortcut = new KeyShortcut(0xBA, KeyModifiers.Control);

        Assert.Equal("Ctrl+Ł", shortcut.ToDisplayString(key => key == 0xBA ? "Ł" : null));
        Assert.Equal("Ctrl+Key 186", shortcut.ToDisplayString());
    }

    [Fact]
    public void NeedsAModifierUnlessItIsAFunctionKey()
    {
        Assert.True(KeyShortcut.Standard.IsValid);
        Assert.True(new KeyShortcut('K', KeyModifiers.Windows | KeyModifiers.Alt).IsValid);
        Assert.True(new KeyShortcut(0x70, KeyModifiers.None).IsValid);
        Assert.False(new KeyShortcut('F', KeyModifiers.None).IsValid);
        Assert.False(new KeyShortcut('F', KeyModifiers.Shift).IsValid);
        Assert.False(new KeyShortcut(0x11, KeyModifiers.Control).IsValid);
    }

    [Fact]
    public void UsesTheFlagsRegisterHotKeyExpects()
    {
        // MOD_ALT, MOD_CONTROL, MOD_SHIFT and MOD_WIN.
        Assert.Equal(0x1, (int)KeyModifiers.Alt);
        Assert.Equal(0x2, (int)KeyModifiers.Control);
        Assert.Equal(0x4, (int)KeyModifiers.Shift);
        Assert.Equal(0x8, (int)KeyModifiers.Windows);
    }
}
