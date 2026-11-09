using GammaKey.Hotkeys;
using Xunit;

namespace GammaKey.Tests;

public class HotkeyComboTests
{
    [Fact]
    public void ParsesPlainFunctionKey()
    {
        Assert.True(HotkeyCombo.TryParse("F9", out var combo));
        Assert.Equal(Keys.F9, combo.Key);
        Assert.Equal(0u, combo.Modifiers);
    }

    [Fact]
    public void ParsesModifiersInAnyOrderAndCase()
    {
        Assert.True(HotkeyCombo.TryParse("shift + ctrl + f10", out var combo));
        Assert.Equal(HotkeyCombo.ModControl | HotkeyCombo.ModShift, combo.Modifiers);
        Assert.Equal(Keys.F10, combo.Key);
    }

    [Fact]
    public void FormatsInCanonicalOrder()
    {
        var combo = new HotkeyCombo(HotkeyCombo.ModShift | HotkeyCombo.ModAlt | HotkeyCombo.ModControl, Keys.D5);

        Assert.Equal("Ctrl+Alt+Shift+D5", combo.ToString());
    }

    [Fact]
    public void RoundTripsThroughString()
    {
        var original = new HotkeyCombo(HotkeyCombo.ModWin, Keys.Home);

        Assert.True(HotkeyCombo.TryParse(original.ToString(), out var parsed));
        Assert.Equal(original, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Banana")]
    [InlineData("ControlKey")]
    public void RejectsIncompleteOrUnknown(string text)
    {
        Assert.False(HotkeyCombo.TryParse(text, out _));
    }
}
