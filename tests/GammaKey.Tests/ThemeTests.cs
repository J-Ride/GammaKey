using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class ThemeTests
{
    [Fact]
    public void ResolveFamilySkipsMissingFontAndReturnsFirstInstalled()
    {
        var family = Theme.ResolveFamily("No Such Font 9000", "Segoe UI");

        Assert.Equal("Segoe UI", family);
    }

    [Fact]
    public void ResolveFamilyFallsBackToGenericSansWhenNoneInstalled()
    {
        var family = Theme.ResolveFamily("No Such Font 9000");

        Assert.Equal(System.Drawing.FontFamily.GenericSansSerif.Name, family);
    }

    [Fact]
    public void ScaleIsIdentityAt96Dpi()
    {
        Assert.Equal(30, Theme.Scale(30, 96f));
    }

    [Fact]
    public void ScaleGrowsByHalfAt144Dpi()
    {
        Assert.Equal(45, Theme.Scale(30, 144f));
    }

    [Fact]
    public void ScaleRoundsHalvesAwayFromZero()
    {
        // 7 * 1.5 = 10.5; banker's rounding would give 10 and shave a pixel off every odd size.
        Assert.Equal(11, Theme.Scale(7, 144f));
    }
}
