using System.Drawing;
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class GkChipTests
{
    [Fact]
    public void ScalePassGrowsHeightButKeepsMeasuredWidth()
    {
        var chip = new GkChip { Label = "HDR", Value = "off" };
        int measuredWidth = chip.Width;

        chip.Scale(new SizeF(1.5f, 1.5f));

        Assert.Equal((39, measuredWidth), (chip.Height, chip.Width));
    }
}
