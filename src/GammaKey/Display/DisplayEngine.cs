using GammaKey.Color;
using GammaKey.Models;
using GammaKey.Storage;

namespace GammaKey.Display;

public sealed record ApplyResult(bool Accepted, string? Note);

public sealed class DisplayEngine
{
    // Drivers may round the table slightly on read-back; anything within this is "the same".
    private const int Tolerance = 512;

    private ushort[]? _activeRamp;
    private IReadOnlyList<MonitorInfo> _activeMonitors = Array.Empty<MonitorInfo>();

    public IReadOnlyList<MonitorInfo> GetMonitors() => MonitorEnumerator.GetMonitors();

    public IReadOnlyList<MonitorInfo> ResolveTargets(MonitorTarget target, out string? note)
    {
        note = null;
        var monitors = GetMonitors();
        if (monitors.Count == 0) return monitors;

        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];

        switch (target.Mode)
        {
            case MonitorTarget.All:
                return monitors;
            case MonitorTarget.Monitor:
                var match = monitors.FirstOrDefault(m => m.FriendlyName.Equals(target.MonitorName, StringComparison.OrdinalIgnoreCase));
                if (match is not null) return new[] { match };
                note = $"{target.MonitorName} not connected, used primary";
                return new[] { primary };
            default:
                return new[] { primary };
        }
    }

    public void CaptureBaseline(Baseline into)
    {
        foreach (var monitor in GetMonitors())
        {
            var ramp = GammaRampNative.Get(monitor.DeviceName);
            if (ramp is not null) into.Ramps[monitor.DeviceName] = ramp;
        }
    }

    public void RestoreBaseline(Baseline from)
    {
        foreach (var (deviceName, ramp) in from.Ramps)
            GammaRampNative.Set(deviceName, ramp);
        ClearActive();
    }

    public ApplyResult Apply(ColorValues values, IReadOnlyList<MonitorInfo> monitors)
    {
        var ramp = CurveBuilder.Build(values);
        bool anyAccepted = false;
        bool anyRefused = false;

        foreach (var monitor in monitors)
        {
            // Windows returns TRUE even when it silently refuses an extreme table, so the
            // read-back is the only honest signal.
            GammaRampNative.Set(monitor.DeviceName, ramp);
            var readBack = GammaRampNative.Get(monitor.DeviceName);
            if (readBack is not null && Matches(readBack, ramp)) anyAccepted = true;
            else anyRefused = true;
        }

        _activeRamp = ramp;
        _activeMonitors = monitors;

        if (anyRefused && !anyAccepted)
            return new ApplyResult(false, "Windows refused this curve, reduce the sliders");
        if (anyRefused)
            return new ApplyResult(true, "One monitor refused the curve");
        return new ApplyResult(true, null);
    }

    public void ClearActive()
    {
        _activeRamp = null;
        _activeMonitors = Array.Empty<MonitorInfo>();
    }

    public bool ReapplyIfCleared()
    {
        if (_activeRamp is null) return false;
        bool reapplied = false;
        foreach (var monitor in _activeMonitors)
        {
            var current = GammaRampNative.Get(monitor.DeviceName);
            if (current is null || Matches(current, _activeRamp)) continue;
            GammaRampNative.Set(monitor.DeviceName, _activeRamp);
            reapplied = true;
        }
        return reapplied;
    }

    private static bool Matches(ushort[] a, ushort[] b)
    {
        for (int i = 0; i < a.Length; i++)
            if (Math.Abs(a[i] - b[i]) > Tolerance) return false;
        return true;
    }
}
