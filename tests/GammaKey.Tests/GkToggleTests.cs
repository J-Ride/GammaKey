using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class GkToggleTests
{
    [Fact]
    public void ToggleFlipsChecked()
    {
        var t = new GkToggle { Checked = false };

        t.Toggle();

        Assert.True(t.Checked);
    }

    [Fact]
    public void SettingSameValueDoesNotRaiseCheckedChanged()
    {
        var t = new GkToggle { Checked = true };
        int fired = 0;
        t.CheckedChanged += (_, _) => fired++;

        t.Checked = true;

        Assert.Equal(0, fired);
    }
}
