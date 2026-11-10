using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class ProfileListTests
{
    [Fact]
    public void RowHeightAt96DpiFitsBothTextLinesPlusSpacing()
    {
        int needed = (int)Math.Ceiling(Theme.Label.GetHeight(96f)) + (int)Math.Ceiling(Theme.Caption.GetHeight(96f)) + 2 * Theme.Space2;

        Assert.True(ProfileList.RowHeightFor(96f) >= needed);
    }

    [Fact]
    public void RowHeightAt144DpiFitsBothTextLinesPlusScaledSpacing()
    {
        int needed = (int)Math.Ceiling(Theme.Label.GetHeight(144f)) + (int)Math.Ceiling(Theme.Caption.GetHeight(144f)) + 2 * Theme.Scale(Theme.Space2, 144f);

        Assert.True(ProfileList.RowHeightFor(144f) >= needed);
    }

    [Fact]
    public void RowHeightGrowsWithDpi()
    {
        Assert.True(ProfileList.RowHeightFor(144f) > ProfileList.RowHeightFor(96f));
    }
}
