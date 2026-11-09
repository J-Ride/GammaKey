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
    private const uint WmQuit = 0x0012;
    private const uint PmNoRemove = 0x0000;
    private const int ReadyTimeoutMs = 2000;
    private const int StopTimeoutMs = 2000;

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PeekMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DispatchMessage(ref Msg lpMsg);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PtX;
        public int PtY;
        public uint Private;
    }

    // Locked because the hook thread matches while the UI thread registers.
    private readonly HotkeyTable _table = new();
    // Hook thread only.
    private readonly HashSet<uint> _down = new();
    // Held in a field so the GC cannot collect the delegate while Windows still calls it.
    private readonly HookProc _proc;
    private readonly SynchronizationContext _context;
    private readonly Thread _thread;
    private readonly object _lifetime = new();
    private readonly bool _isInstalled;
    private IntPtr _hook;
    private uint _threadId;
    private bool _isDisposed;

    public event Action<int>? Pressed;

    public bool IsInstalled => _isInstalled;

    public KeyboardHookListener()
    {
        // WinForms installs its context only once the first control exists; the fallback
        // still posts to the UI thread because this constructor runs there.
        _context = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _proc = Callback;
        var ready = new ManualResetEventSlim();
        // Windows calls an LL hook only while its installing thread pumps messages. On the UI
        // thread every key press on the machine would wait out gamma writes during a slider
        // drag, and a callback past the LL hook timeout gets the hook silently removed.
        _thread = new Thread(() => RunHookThread(ready)) { IsBackground = true, Name = "GammaKey hotkey hook" };
        _thread.Start();
        // A hook thread that never reports in counts as not installed, so the caller falls back.
        _isInstalled = ready.Wait(ReadyTimeoutMs) && _hook != IntPtr.Zero;
    }

    public bool Register(int id, HotkeyCombo combo)
    {
        lock (_table) _table.Register(id, combo);
        // Hook install can fail; with no hook no hotkey will ever fire, so report refusal.
        return _isInstalled;
    }

    public void UnregisterAll()
    {
        lock (_table) _table.UnregisterAll();
    }

    private void RunHookThread(ManualResetEventSlim ready)
    {
        _hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(null), 0);
        // PostThreadMessage fails on a thread without a message queue; peeking creates it, so
        // a Dispose right after the constructor still reaches this thread.
        PeekMessage(out _, IntPtr.Zero, 0, 0, PmNoRemove);
        bool isStopping;
        lock (_lifetime)
        {
            _threadId = GetCurrentThreadId();
            isStopping = _isDisposed;
        }
        ready.Set();
        if (_hook == IntPtr.Zero) return;
        if (!isStopping)
        {
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        UnhookWindowsHookEx(_hook);
    }

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
                        uint mods = CurrentModifiers();
                        int? id;
                        lock (_table) id = _table.Match(mods, (Keys)info.VkCode);
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
        uint threadId;
        lock (_lifetime)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            threadId = _threadId;
        }
        // A zero id means the thread has not reached its loop yet; it sees _isDisposed and
        // unhooks without waiting for a quit message.
        if (threadId != 0) PostThreadMessage(threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
        // Bounded so a stuck hook thread cannot hang quit; it is a background thread anyway.
        _thread.Join(StopTimeoutMs);
        UnregisterAll();
    }
}
