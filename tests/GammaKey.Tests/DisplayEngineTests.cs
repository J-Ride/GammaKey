using GammaKey.Display;
using Xunit;

namespace GammaKey.Tests;

public class DisplayEngineTests
{
    private static MonitorInfo Mon(string device, bool primary = false) => new(device, device, primary, false);

    [Fact]
    public void OtherDevicesReturnsMonitorsNotInTargets()
    {
        var all = new[] { Mon(@"\\.\DISPLAY1", true), Mon(@"\\.\DISPLAY2"), Mon(@"\\.\DISPLAY3") };
        var targets = new[] { Mon(@"\\.\DISPLAY1", true) };

        var others = DisplayEngine.OtherDevices(all, targets);

        Assert.Equal(new[] { @"\\.\DISPLAY2", @"\\.\DISPLAY3" }, others);
    }

    [Fact]
    public void OtherDevicesIsEmptyWhenAllTargeted()
    {
        var all = new[] { Mon(@"\\.\DISPLAY1", true), Mon(@"\\.\DISPLAY2") };

        var others = DisplayEngine.OtherDevices(all, all);

        Assert.Empty(others);
    }

    [Fact]
    public void OtherDevicesComparesDeviceNamesIgnoringCase()
    {
        var all = new[] { Mon(@"\\.\DISPLAY1", true), Mon(@"\\.\DISPLAY2") };
        var targets = new[] { Mon(@"\\.\display1", true) };

        var others = DisplayEngine.OtherDevices(all, targets);

        Assert.Equal(new[] { @"\\.\DISPLAY2" }, others);
    }
}
