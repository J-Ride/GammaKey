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
}
