using GammaKey.Models;
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class FormatTests
{
    [Fact]
    public void SignedAddsPlusForPositiveIntegers()
    {
        Assert.Equal("+24", Format.Signed(24m, 0));
    }

    [Fact]
    public void SignedLeavesZeroBare()
    {
        Assert.Equal("0", Format.Signed(0m, 0));
    }

    [Fact]
    public void SignedKeepsDecimalPlacesWithoutPlus()
    {
        Assert.Equal("1.55", Format.Signed(1.55m, 2));
    }

    [Fact]
    public void HotkeyPartsSplitsModifiersAndStripsDigitPrefix()
    {
        Assert.Equal(new[] { "Ctrl", "Alt", "1" }, Format.HotkeyParts("Ctrl+Alt+D1"));
    }

    [Fact]
    public void HotkeyPartsOfEmptyIsEmpty()
    {
        Assert.Empty(Format.HotkeyParts(""));
    }

    [Fact]
    public void ProfileSummaryListsOnlyChangedValues()
    {
        var p = new Profile { Gamma = 1.55, Shadows = 36 };

        Assert.Equal("gamma 1.55 · shadows +36", Format.ProfileSummary(p));
    }

    [Fact]
    public void ProfileSummaryOfNormalIsNeutral()
    {
        Assert.Equal("neutral curve", Format.ProfileSummary(new Profile()));
    }
}
