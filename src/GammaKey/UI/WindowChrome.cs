using System.Runtime.InteropServices;

namespace GammaKey.UI;

// DWM paints the caption bar, so the only way to get a dark one is to ask it. Both attributes
// return an error on older Windows 10 builds and the window then keeps the default chrome.
public static class WindowChrome
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int CornerRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Form form)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return;
        form.HandleCreated += (_, _) => Set(form.Handle);
        if (form.IsHandleCreated) Set(form.Handle);
    }

    private static void Set(IntPtr hwnd)
    {
        int on = 1;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref on, sizeof(int));
        int corner = CornerRound;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));
        int border = ColorTranslator.ToWin32(Theme.Line);
        DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(int));
    }
}
