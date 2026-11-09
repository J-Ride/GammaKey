using GammaKey.Hotkeys;
using Xunit;

namespace GammaKey.Tests;

public class HotkeyTableTests
{
    private static HotkeyCombo Combo(string text)
    {
        Assert.True(HotkeyCombo.TryParse(text, out var combo));
        return combo;
    }

    [Fact]
    public void MatchReturnsIdForSameModifiersAndKey()
    {
        var table = new HotkeyTable();
        table.Register(100, Combo("Ctrl+F1"));

        var id = table.Match(HotkeyCombo.ModControl, Keys.F1);

        Assert.Equal(100, id);
    }

    [Fact]
    public void MatchIsNullForDifferentModifiers()
    {
        var table = new HotkeyTable();
        table.Register(100, Combo("Ctrl+F1"));

        var id = table.Match(HotkeyCombo.ModControl | HotkeyCombo.ModShift, Keys.F1);

        Assert.Null(id);
    }

    [Fact]
    public void MatchIsNullAfterUnregisterAll()
    {
        var table = new HotkeyTable();
        table.Register(100, Combo("Ctrl+F1"));
        table.UnregisterAll();

        var id = table.Match(HotkeyCombo.ModControl, Keys.F1);

        Assert.Null(id);
    }

    [Fact]
    public void RegisterSameIdReplacesCombo()
    {
        var table = new HotkeyTable();
        table.Register(100, Combo("Ctrl+F1"));
        table.Register(100, Combo("Alt+F2"));

        var old = table.Match(HotkeyCombo.ModControl, Keys.F1);

        Assert.Null(old);
    }

    [Fact]
    public void PlainKeyMatchesWithNoModifiers()
    {
        var table = new HotkeyTable();
        table.Register(101, Combo("NumPad1"));

        var id = table.Match(0, Keys.NumPad1);

        Assert.Equal(101, id);
    }
}
