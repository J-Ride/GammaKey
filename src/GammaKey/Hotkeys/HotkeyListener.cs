using System.Runtime.InteropServices;

namespace GammaKey.Hotkeys;

public sealed class HotkeyListener : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private readonly HashSet<int> _registered = new();

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public event Action<int>? Pressed;

    public HotkeyListener()
    {
        // A message-only window: never shown, only receives WM_HOTKEY.
        CreateHandle(new CreateParams { Parent = new IntPtr(-3) });
    }

    public bool Register(int id, HotkeyCombo combo)
    {
        if (_registered.Contains(id)) UnregisterHotKey(Handle, id);
        bool ok = RegisterHotKey(Handle, id, combo.Modifiers | HotkeyCombo.ModNoRepeat, (uint)combo.Key);
        if (ok) _registered.Add(id);
        else _registered.Remove(id);
        return ok;
    }

    public void UnregisterAll()
    {
        foreach (var id in _registered) UnregisterHotKey(Handle, id);
        _registered.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey) Pressed?.Invoke(m.WParam.ToInt32());
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterAll();
        DestroyHandle();
    }
}
