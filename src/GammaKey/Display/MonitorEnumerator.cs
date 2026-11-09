using System.Runtime.InteropServices;
using static GammaKey.Display.DisplayConfigNative;

namespace GammaKey.Display;

public static class MonitorEnumerator
{
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var primaryName = FindPrimaryDeviceName();
        var result = new List<MonitorInfo>();
        var usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount) != 0)
            return FallbackFromGdi(primaryName);

        var paths = new PathInfo[pathCount];
        var modes = new ModeInfo[modeCount];
        if (QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
            return FallbackFromGdi(primaryName);

        for (int i = 0; i < pathCount; i++)
        {
            var path = paths[i];

            var source = new SourceDeviceName
            {
                Header = new DeviceInfoHeader
                {
                    Type = DeviceInfoGetSourceName,
                    Size = (uint)Marshal.SizeOf<SourceDeviceName>(),
                    AdapterId = path.SourceInfo.AdapterId,
                    Id = path.SourceInfo.Id,
                },
            };
            if (DisplayConfigGetDeviceInfo(ref source) != 0) continue;

            var target = new TargetDeviceName
            {
                Header = new DeviceInfoHeader
                {
                    Type = DeviceInfoGetTargetName,
                    Size = (uint)Marshal.SizeOf<TargetDeviceName>(),
                    AdapterId = path.TargetInfo.AdapterId,
                    Id = path.TargetInfo.Id,
                },
            };
            var friendly = DisplayConfigGetDeviceInfo(ref target) == 0 ? target.MonitorFriendlyDeviceName : "";
            if (string.IsNullOrWhiteSpace(friendly)) friendly = $"Display {result.Count + 1}";

            var color = new AdvancedColorInfo
            {
                Header = new DeviceInfoHeader
                {
                    Type = DeviceInfoGetAdvancedColorInfo,
                    Size = (uint)Marshal.SizeOf<AdvancedColorInfo>(),
                    AdapterId = path.TargetInfo.AdapterId,
                    Id = path.TargetInfo.Id,
                },
            };
            bool hdrOn = DisplayConfigGetDeviceInfo(ref color) == 0 && (color.Value & AdvancedColorEnabledBit) != 0;

            // Two identical monitors report the same friendly name; number the repeats so the
            // profile target can still name one of them.
            if (usedNames.TryGetValue(friendly, out var seen))
            {
                usedNames[friendly] = seen + 1;
                friendly = $"{friendly} ({seen + 1})";
            }
            else
            {
                usedNames[friendly] = 1;
            }

            var gdiName = source.ViewGdiDeviceName;
            result.Add(new MonitorInfo(gdiName, friendly, gdiName.Equals(primaryName, StringComparison.OrdinalIgnoreCase), hdrOn));
        }

        return result.Count > 0 ? result : FallbackFromGdi(primaryName);
    }

    private static string? FindPrimaryDeviceName()
    {
        var device = new DisplayDevice { Cb = Marshal.SizeOf<DisplayDevice>() };
        for (uint i = 0; EnumDisplayDevices(null, i, ref device, 0); i++)
        {
            if ((device.StateFlags & DisplayDevicePrimary) != 0) return device.DeviceName;
            device.Cb = Marshal.SizeOf<DisplayDevice>();
        }
        return null;
    }

    private static IReadOnlyList<MonitorInfo> FallbackFromGdi(string? primaryName)
    {
        var result = new List<MonitorInfo>();
        var device = new DisplayDevice { Cb = Marshal.SizeOf<DisplayDevice>() };
        for (uint i = 0; EnumDisplayDevices(null, i, ref device, 0); i++)
        {
            if ((device.StateFlags & DisplayDeviceActive) != 0)
                result.Add(new MonitorInfo(device.DeviceName, $"Display {result.Count + 1}", device.DeviceName == primaryName, false));
            device.Cb = Marshal.SizeOf<DisplayDevice>();
        }
        return result;
    }
}
