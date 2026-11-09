using GammaKey.Color;
using GammaKey.Models;
using Xunit;

namespace GammaKey.Tests;

public class CurveBuilderTests
{
    [Fact]
    public void NormalValuesProduceExactIdentityTable()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal);

        for (int i = 0; i < 256; i++)
            Assert.Equal((ushort)(i * 257), ramp[i]);
    }

    [Fact]
    public void TableHasThreeEqualChannels()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Gamma = 1.7 });

        for (int i = 0; i < 256; i++)
        {
            Assert.Equal(ramp[i], ramp[256 + i]);
            Assert.Equal(ramp[i], ramp[512 + i]);
        }
    }

    [Fact]
    public void GammaAboveOneBrightensMidtones()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Gamma = 2.0 });

        Assert.True(ramp[128] > 128 * 257);
    }

    [Fact]
    public void PositiveBrightnessLiftsBlack()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Brightness = 50 });

        Assert.True(ramp[0] > 0);
    }

    [Fact]
    public void PositiveContrastDarkensBelowMidAndBrightensAbove()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Contrast = 50 });

        Assert.True(ramp[64] < 64 * 257);
        Assert.True(ramp[192] > 192 * 257);
    }

    [Fact]
    public void ShadowsOnlyChangeTheDarkHalf()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Shadows = 100 });

        Assert.True(ramp[0] > 0);
        Assert.Equal((ushort)(200 * 257), ramp[200]);
    }

    [Fact]
    public void HighlightsOnlyChangeTheBrightHalf()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Highlights = -100 });

        Assert.True(ramp[255] < 65535);
        Assert.Equal((ushort)(60 * 257), ramp[60]);
    }

    [Theory]
    [InlineData(0.3, 100, 100, 100, 100)]
    [InlineData(2.8, -100, -100, -100, -100)]
    [InlineData(0.3, 0, 0, 100, 0)]
    [InlineData(2.8, 0, -100, 0, 100)]
    public void ExtremeValuesStayNonDecreasing(double gamma, int brightness, int contrast, int shadows, int highlights)
    {
        var ramp = CurveBuilder.Build(new ColorValues(gamma, brightness, contrast, shadows, highlights, 50));

        for (int i = 1; i < 256; i++)
            Assert.True(ramp[i] >= ramp[i - 1], $"entry {i} decreased");
    }

    [Fact]
    public void OutOfRangeInputIsClampedNotThrown()
    {
        var ramp = CurveBuilder.Build(new ColorValues(99, 999, -999, 999, 999, 999));

        Assert.Equal(768, ramp.Length);
    }
}
