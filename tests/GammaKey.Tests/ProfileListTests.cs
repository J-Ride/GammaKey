using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class ProfileListTests
{
    [Fact]
    public void RowHeightAt96DpiFitsTheNameLinePlusSpacing()
    {
        int needed = Theme.FontsFor(96f).Label.Height + 2 * Theme.Space2;

        Assert.True(ProfileList.RowHeightFor(96f) >= needed);
    }

    [Fact]
    public void RowHeightAt144DpiFitsTheNameLinePlusScaledSpacing()
    {
        int needed = Theme.FontsFor(144f).Label.Height + 2 * Theme.Scale(Theme.Space2, 144f);

        Assert.True(ProfileList.RowHeightFor(144f) >= needed);
    }

    [Fact]
    public void RowHeightGrowsWithDpi()
    {
        Assert.True(ProfileList.RowHeightFor(144f) > ProfileList.RowHeightFor(96f));
    }
}
