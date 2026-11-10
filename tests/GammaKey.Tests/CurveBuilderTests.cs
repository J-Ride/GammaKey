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

    [Fact]
    public void WarmTemperatureRaisesRedMidGrey()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Temperature = 100 });

        Assert.True(ramp[128] > 128 * 257);
    }

    [Fact]
    public void WarmTemperatureLowersBlueMidGrey()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Temperature = 100 });

        Assert.True(ramp[512 + 128] < 128 * 257);
    }

    [Fact]
    public void WarmTemperatureLeavesGreenUnchanged()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Temperature = 100 });

        Assert.Equal((ushort)(128 * 257), ramp[256 + 128]);
    }

    [Fact]
    public void RedGainRaisesTheRedChannel()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Red = 50 });

        Assert.True(ramp[128] > 128 * 257);
    }

    [Fact]
    public void RedGainLeavesGreenUnchanged()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Red = 50 });

        Assert.Equal((ushort)(128 * 257), ramp[256 + 128]);
    }

    [Fact]
    public void RedGainLeavesBlueUnchanged()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Red = 50 });

        Assert.Equal((ushort)(128 * 257), ramp[512 + 128]);
    }

    [Fact]
    public void GreenGainLowersTheGreenChannel()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Green = -50 });

        Assert.True(ramp[256 + 128] < 128 * 257);
    }

    [Fact]
    public void GreenGainLeavesRedUnchanged()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Green = -50 });

        Assert.Equal((ushort)(128 * 257), ramp[128]);
    }

    [Fact]
    public void GreenGainLeavesBlueUnchanged()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Green = -50 });

        Assert.Equal((ushort)(128 * 257), ramp[512 + 128]);
    }

    [Fact]
    public void BlackFloorAtMaxLiftsBlackToHalfScale()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { BlackFloor = 100 });

        Assert.Equal((ushort)32768, ramp[0]);
    }

    [Fact]
    public void BlackFloorHoldsWhenBrightnessPushesBlackBelowZero()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Brightness = -100, BlackFloor = 100 });

        Assert.Equal((ushort)32768, ramp[0]);
    }

    [Fact]
    public void BlackFloorKeepsWhiteAtFullScale()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { BlackFloor = 100 });

        Assert.Equal((ushort)65535, ramp[255]);
    }

    [Fact]
    public void MaxGainNeverPushesMidGreyAboveLimit()
    {
        // Red 100 plus Temperature 100 would be gain 1.6 unclamped; the limit is 1.6, so the
        // 30 percent rule holds: entry 128 (128/255 of full scale) times 1.6.
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Red = 100, Temperature = 100 });

        Assert.Equal((ushort)Math.Round(128 / 255.0 * 1.6 * 65535), ramp[128]);
    }

    [Theory]
    [InlineData(100, 100, 100, 100, 100)]
    [InlineData(-100, -100, -100, -100, 100)]
    [InlineData(100, -100, 100, -100, 0)]
    public void ExtremeBalanceValuesStayNonDecreasingPerChannel(int temperature, int red, int green, int blue, int floor)
    {
        var ramp = CurveBuilder.Build(new ColorValues(0.3, 100, 100, 100, 100, 50)
        {
            Temperature = temperature,
            Red = red,
            Green = green,
            Blue = blue,
            BlackFloor = floor,
        });

        for (int c = 0; c < 3; c++)
            for (int i = 1; i < 256; i++)
                Assert.True(ramp[c * 256 + i] >= ramp[c * 256 + i - 1], $"channel {c} entry {i} decreased");
    }
}
