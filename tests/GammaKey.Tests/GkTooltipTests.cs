using System.Windows.Forms;
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class GkTooltipTests
{
    [Fact]
    public void WaitsSevenHundredMillisecondsBeforeShowing()
    {
        using var tips = new GkTooltip();

        Assert.Equal(700, tips.InitialDelay);
    }

    [Fact]
    public void AttachedTextRoundTrips()
    {
        using var tips = new GkTooltip();
        using var control = new Label();

        tips.Attach(control, "Lifts only the dark tones");

        Assert.Equal("Lifts only the dark tones", tips.TextFor(control));
    }

    [Fact]
    public void LongerTextMeasuresWider()
    {
        var shortOne = GkTooltip.Measure("Add profile", 96f);
        var longOne = GkTooltip.Measure("Shifts every tone lighter or darker", 96f);

        Assert.True(longOne.Width > shortOne.Width);
    }

    [Fact]
    public void VeryLongTextWrapsInsteadOfGrowingPastTheWrapWidth()
    {
        var size = GkTooltip.Measure(string.Join(' ', Enumerable.Repeat("word", 60)), 96f);

        Assert.True(size.Width <= GkTooltip.WrapWidth + GkTooltip.PadX * 2);
    }

    [Fact]
    public void WrappedTextIsTallerThanOneLine()
    {
        var oneLine = GkTooltip.Measure("Add profile", 96f);
        var manyLines = GkTooltip.Measure(string.Join(' ', Enumerable.Repeat("word", 60)), 96f);

        Assert.True(manyLines.Height > oneLine.Height);
    }

    [Fact]
    public void MeasureScalesWithDpi()
    {
        var at96 = GkTooltip.Measure("Add profile", 96f);
        var at192 = GkTooltip.Measure("Add profile", 192f);

        Assert.True(at192.Width > at96.Width);
    }
}
