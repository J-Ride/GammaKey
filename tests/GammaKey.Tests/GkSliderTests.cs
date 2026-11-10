using System.Drawing;
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class GkSliderTests
{
    [Fact]
    public void CentreLabelSitsUnderOriginWhenOriginIsAboveMinimum()
    {
        // Gamma: 0.30 to 2.80 with origin 1.00 sits 70/250 of the way along a 250 px track.
        int x = GkSlider.CentreLabelX(30, 280, 100, new Rectangle(7, 8, 250, 4), 264);

        Assert.Equal(77, x);
    }

    [Fact]
    public void CentreLabelSitsInTheMiddleWhenOriginIsTheMinimum()
    {
        int x = GkSlider.CentreLabelX(50, 100, 50, new Rectangle(7, 8, 306, 4), 320);

        Assert.Equal(160, x);
    }

    [Fact]
    public void CentreLabelClampsAnOriginAboveMaximumToTheRightEnd()
    {
        int x = GkSlider.CentreLabelX(0, 100, 150, new Rectangle(7, 8, 306, 4), 320);

        Assert.Equal(313, x);
    }
}
