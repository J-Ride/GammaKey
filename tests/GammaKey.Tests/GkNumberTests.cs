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

    [Fact]
    public void TypedValueCommitsOnce()
    {
        var n = new GkNumber { Minimum = -100, Maximum = 100, Value = 10 };
        int fired = 0;
        n.ValueChanged += (_, _) => fired++;

        n.BeginEdit();
        n.EditText = "65";
        n.CommitEdit();

        Assert.Equal(65m, n.Value);
        Assert.Equal(1, fired);
        Assert.False(n.IsEditing);
    }

    [Fact]
    public void TypedValueAboveMaximumClamps()
    {
        var n = new GkNumber { Minimum = -100, Maximum = 100, Value = 10 };

        n.BeginEdit();
        n.EditText = "999";
        n.CommitEdit();

        Assert.Equal(100m, n.Value);
    }

    [Fact]
    public void CancelKeepsTheOldValueAndFiresNothing()
    {
        var n = new GkNumber { Minimum = -100, Maximum = 100, Value = 10 };
        int fired = 0;
        n.ValueChanged += (_, _) => fired++;

        n.BeginEdit();
        n.EditText = "65";
        n.CancelEdit();

        Assert.Equal(10m, n.Value);
        Assert.Equal(0, fired);
        Assert.False(n.IsEditing);
    }

    [Fact]
    public void UnparseableTextKeepsTheOldValue()
    {
        var n = new GkNumber { Minimum = -100, Maximum = 100, Value = 10 };
        int fired = 0;
        n.ValueChanged += (_, _) => fired++;

        n.BeginEdit();
        n.EditText = "abc";
        n.CommitEdit();

        Assert.Equal(10m, n.Value);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void GammaBoxAcceptsTwoDecimals()
    {
        var n = new GkNumber { Minimum = 0.3m, Maximum = 2.8m, Increment = 0.01m, DecimalPlaces = 2, Value = 1m };

        n.BeginEdit();
        n.EditText = "1.25";
        n.CommitEdit();

        Assert.Equal(1.25m, n.Value);
    }

    [Fact]
    public void IntegerBoxRoundsATypedDecimal()
    {
        var n = new GkNumber { Minimum = -100, Maximum = 100, Value = 0 };

        n.BeginEdit();
        n.EditText = "12.7";
        n.CommitEdit();

        Assert.Equal(13m, n.Value);
    }

    [Fact]
    public void TypingDoesNotChangeTheValueUntilCommit()
    {
        var n = new GkNumber { Minimum = -100, Maximum = 100, Value = 10 };

        n.BeginEdit();
        n.EditText = "65";

        Assert.Equal(10m, n.Value);
        Assert.True(n.IsEditing);
    }

    [Fact]
    public void BeginEditShowsThePlainValue()
    {
        var n = new GkNumber { Minimum = -100, Maximum = 100, Value = 24 };

        n.BeginEdit();

        Assert.Equal("24", n.EditText);
    }
}
