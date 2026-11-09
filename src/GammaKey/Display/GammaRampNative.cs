using System.Runtime.InteropServices;

namespace GammaKey.Display;

internal static class GammaRampNative
{
    public const int RampLength = 256 * 3;

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateDC(string? driver, string device, string? output, IntPtr initData);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool SetDeviceGammaRamp(IntPtr hdc, ushort[] ramp);

    [DllImport("gdi32.dll")]
    private static extern bool GetDeviceGammaRamp(IntPtr hdc, [Out] ushort[] ramp);

    public static ushort[]? Get(string deviceName)
    {
        var hdc = CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero) return null;
        try
        {
            var ramp = new ushort[RampLength];
            return GetDeviceGammaRamp(hdc, ramp) ? ramp : null;
        }
        finally
        {
            DeleteDC(hdc);
        }
    }

    public static bool Set(string deviceName, ushort[] ramp)
    {
        if (ramp.Length != RampLength) throw new ArgumentException("Ramp must have 768 entries", nameof(ramp));
        var hdc = CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero) return false;
        try
        {
            return SetDeviceGammaRamp(hdc, ramp);
        }
        finally
        {
            DeleteDC(hdc);
        }
    }
}
