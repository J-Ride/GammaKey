using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class GkNumberTests
{
    [Fact]
    public void ValueIsClampedToRange()
    {
        var n = new GkNumber { Minimum = -100, Maximum = 100 };

        n.Value = 250;

        Assert.Equal(100m, n.Value);
    }

    [Fact]
    public void StepUpAddsIncrement()
    {
        var n = new GkNumber { Minimum = 0.3m, Maximum = 2.8m, Increment = 0.01m, DecimalPlaces = 2, Value = 1.55m };

        n.Step(+1);

        Assert.Equal(1.56m, n.Value);
    }

    [Fact]
    public void ValueChangedFiresOnceForOneChange()
    {
        var n = new GkNumber { Minimum = 0, Maximum = 10 };
        int fired = 0;
        n.ValueChanged += (_, _) => fired++;

        n.Value = 3;

        Assert.Equal(1, fired);
    }

    [Fact]
    public void DisplayTextUsesSignedFormat()
    {
        var n = new GkNumber { Minimum = -100, Maximum = 100, Value = 24 };

        Assert.Equal("+24", n.DisplayText);
    }
}
