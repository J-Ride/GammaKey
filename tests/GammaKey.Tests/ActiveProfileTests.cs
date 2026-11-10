using GammaKey.App;
using Xunit;

namespace GammaKey.Tests;

public class ActiveProfileTests
{
    [Fact]
    public void NothingIsActiveAtStart()
    {
        var active = new ActiveProfile();

        Assert.False(active.IsActive("a"));
    }

    [Fact]
    public void SameProfileTwiceIsActiveOnSecondPress()
    {
        var active = new ActiveProfile();
        active.Applied("a");

        Assert.True(active.IsActive("a"));
    }

    [Fact]
    public void DifferentProfileSwitchesTheActiveOne()
    {
        var active = new ActiveProfile();
        active.Applied("a");
        active.Applied("b");

        Assert.False(active.IsActive("a"));
    }

    [Fact]
    public void ClearedMeansTheNextPressApplies()
    {
        var active = new ActiveProfile();
        active.Applied("a");
        active.Cleared();

        Assert.False(active.IsActive("a"));
    }
}
