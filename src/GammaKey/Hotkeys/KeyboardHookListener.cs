using System.Runtime.InteropServices;

namespace GammaKey.Hotkeys;

/// Detects hotkeys with a low-level keyboard hook and lets every key through to the
/// active app. RegisterHotKey would swallow the key, which is what pass-through avoids.
public sealed class KeyboardHookListener : IHotkeyListener
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint LlkhfInjected = 0x10;

    private const int VkShift = 0x10, VkControl = 0x11, VkMenu = 0x12, VkLWin = 0x5B, VkRWin = 0x5C;

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    private readonly HotkeyTable _table = new();
    private readonly HashSet<uint> _down = new();
    // Held in a field so the GC cannot collect the delegate while Windows still calls it.
    private readonly HookProc _proc;
    private readonly SynchronizationContext _context;
    private IntPtr _hook;

    public event Action<int>? Pressed;

    public bool IsInstalled => _hook != IntPtr.Zero;

    public KeyboardHookListener()
    {
        // WinForms installs its context only once the first control exists. The fallback
        // still posts to this thread, which must pump messages anyway for the hook to run.
        _context = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _proc = Callback;
        _hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(null), 0);
    }

    public bool Register(int id, HotkeyCombo combo)
    {
        _table.Register(id, combo);
        // Hook install can fail; with a zero hook no hotkey will ever fire, so report refusal.
        return _hook != IntPtr.Zero;
    }

    public void UnregisterAll() => _table.UnregisterAll();

    private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        // An exception leaving a native callback kills the process, and CallNextHookEx must
        // always run, so every failure here is swallowed.
        try
        {
            if (code >= 0)
            {
                var info = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                int msg = wParam.ToInt32();
                bool injected = (info.Flags & LlkhfInjected) != 0;
                if (!injected && msg is WmKeyDown or WmSysKeyDown)
                {
                    // The LL hook has no repeat flag; a key already down is auto-repeat, unless
                    // its key-up was lost (e.g. on the secure desktop) and it is really up.
                    bool isFreshPress = _down.Add(info.VkCode) || !IsDown((int)info.VkCode);
                    if (isFreshPress && !IsModifierKey(info.VkCode))
                    {
                        var id = _table.Match(CurrentModifiers(), (Keys)info.VkCode);
                        // Post so the callback returns at once; a slow handler past the LL hook
                        // timeout makes Windows silently remove the hook.
                        if (id is int matched) _context.Post(_ => Pressed?.Invoke(matched), null);
                    }
                }
                else if (!injected && msg is WmKeyUp or WmSysKeyUp)
                {
                    _down.Remove(info.VkCode);
                }
            }
        }
        catch
        {
        }
        // Always pass the key on; that is the whole point of this listener.
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private static bool IsModifierKey(uint vk) => vk is VkShift or VkControl or VkMenu or VkLWin or VkRWin
        or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5; // left/right Shift, Ctrl, Alt

    private static uint CurrentModifiers()
    {
        uint mods = 0;
        if (IsDown(VkControl)) mods |= HotkeyCombo.ModControl;
        if (IsDown(VkMenu)) mods |= HotkeyCombo.ModAlt;
        if (IsDown(VkShift)) mods |= HotkeyCombo.ModShift;
        if (IsDown(VkLWin) || IsDown(VkRWin)) mods |= HotkeyCombo.ModWin;
        return mods;
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _table.UnregisterAll();
    }
}
