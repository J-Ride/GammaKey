using GammaKey.Models;
using Xunit;

namespace GammaKey.Tests;

public class ProfileTests
{
    private static Profile Cave() => new()
    {
        Name = "Cave",
        Hotkey = "Ctrl+F9",
        Target = MonitorTarget.ForMonitor("LG ULTRAGEAR"),
        Values = new ColorValues(1.6, 20, -10, 35, 0, 65) { Temperature = 10, Red = -5, Green = 0, Blue = 5, BlackFloor = 20 },
    };

    [Fact]
    public void CopyForEditingKeepsTheId()
    {
        var saved = Cave();

        var copy = saved.CopyForEditing();

        Assert.Equal(saved.Id, copy.Id);
    }

    [Fact]
    public void CopyForEditingKeepsTheHotkey()
    {
        var copy = Cave().CopyForEditing();

        Assert.Equal("Ctrl+F9", copy.Hotkey);
    }

    [Fact]
    public void CopyForEditingCopiesEveryValue()
    {
        var saved = Cave();

        var copy = saved.CopyForEditing();

        Assert.Equal(saved.Values, copy.Values);
    }

    [Fact]
    public void CopyForEditingGivesTheCopyItsOwnTarget()
    {
        var saved = Cave();

        var copy = saved.CopyForEditing();

        Assert.NotSame(saved.Target, copy.Target);
    }

    [Fact]
    public void IdenticalCopyHasSameContent()
    {
        var saved = Cave();

        Assert.True(saved.CopyForEditing().HasSameContentAs(saved));
    }

    [Fact]
    public void ChangedNameIsDifferentContent()
    {
        var saved = Cave();
        var copy = saved.CopyForEditing();

        copy.Name = "Cave 2";

        Assert.False(copy.HasSameContentAs(saved));
    }

    [Fact]
    public void ChangedHotkeyIsDifferentContent()
    {
        var saved = Cave();
        var copy = saved.CopyForEditing();

        copy.Hotkey = "Ctrl+F10";

        Assert.False(copy.HasSameContentAs(saved));
    }

    [Fact]
    public void ChangedTargetModeIsDifferentContent()
    {
        var saved = Cave();
        var copy = saved.CopyForEditing();

        copy.Target = MonitorTarget.ForAll();

        Assert.False(copy.HasSameContentAs(saved));
    }

    [Fact]
    public void ChangedTargetMonitorIsDifferentContent()
    {
        var saved = Cave();
        var copy = saved.CopyForEditing();

        copy.Target = MonitorTarget.ForMonitor("DELL U2720Q");

        Assert.False(copy.HasSameContentAs(saved));
    }

    [Fact]
    public void ChangedColourValueIsDifferentContent()
    {
        var saved = Cave();
        var copy = saved.CopyForEditing();

        copy.Values = copy.Values with { BlackFloor = 21 };

        Assert.False(copy.HasSameContentAs(saved));
    }

    [Fact]
    public void DifferentIdWithSameFieldsIsStillSameContent()
    {
        var saved = Cave();
        var other = Cave();

        Assert.True(other.HasSameContentAs(saved));
    }

    [Fact]
    public void CopyContentFromReplacesEveryFieldButTheId()
    {
        var saved = Cave();
        var edited = saved.CopyForEditing();
        edited.Name = "Cave 2";
        edited.Hotkey = "Alt+F1";
        edited.Target = MonitorTarget.ForAll();
        edited.Values = ColorValues.Normal;

        saved.CopyContentFrom(edited);

        Assert.Equal((saved.Id, "Cave 2", "Alt+F1", MonitorTarget.All, ColorValues.Normal), (edited.Id, saved.Name, saved.Hotkey, saved.Target.Mode, saved.Values));
    }
}
