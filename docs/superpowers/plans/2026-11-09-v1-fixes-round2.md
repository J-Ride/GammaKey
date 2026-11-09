# GammaKey v1 fixes round 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the launch layout collapse, restore non-target monitors on apply, move Reset to a "Restore monitor" button, add an opt-in hotkey pass-through with an anti-cheat warning, and document borderless mode.

**Architecture:** Layout and launch timing fixes stay in `MainForm` and `Program`. The restore rule lives in `AppController.Preview` with per-device restore overloads on both engines. Pass-through adds an `IHotkeyListener` interface, a `KeyboardHookListener` built on a low-level keyboard hook, and a pure `HotkeyTable` for matching, chosen by a new setting.

**Tech Stack:** C# .NET 8 WinForms x64, xUnit. Spec: `docs/superpowers/specs/2026-11-09-v1-fixes-round2-design.md`.

## Global Constraints

- Branch `feature/v1-fixes`. Repo `C:\Dev\Projects\GammaKey`. Run commands from the repo root.
- `dotnet build` and `dotnet test` clean (0 warnings, 0 errors) at every commit.
- Only the files named in each task change. No edits to `Natives/`, `GkSlider.cs`, `Theme.cs`, `TrayIcon.cs`, `ToastForm.cs`, `CurveBuilder.cs`.
- Copy must match the spec exactly:
  - Button: `Restore monitor`. Settings label: `Restore monitor hotkey`. Checkbox: `Let hotkeys pass through to other apps`.
  - Warning dialog text: `Pass-through watches every key press with a system-wide keyboard hook. Some anti-cheat systems flag that.` blank line `Recommended: leave this off and give each hotkey a Ctrl or Alt modifier.` blank line `Turn it on anyway?` Title `GammaKey`, buttons OK and Cancel, warning icon.
- `HotkeyPassthrough` defaults to `false`.
- Commit messages: descriptive subject and body. No attribution trailer of any kind. Write the message to a file and use `git commit -F <file>`. Stage by path.
- Comments explain WHY only. No dead code.

---

### Task 1: Launch layout and deferred show

**Files:**
- Modify: `src/GammaKey/UI/MainForm.cs` (BuildLayout, left column)
- Modify: `src/GammaKey/Program.cs`

**Interfaces:** none new.

- [ ] **Step 1: Explicit row styles and a non-docked button row**

In `BuildLayout`, replace the left column setup. Before:

```csharp
var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Margin = Padding.Empty };
left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
left.Controls.Add(_profileList);
// A wrapping row so five buttons fit the 280 px column without clipping any label.
var leftButtons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Dock = DockStyle.Fill, Margin = new Padding(0, Theme.Space3, 0, 0) };
```

After:

```csharp
var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
// Explicit rows: a content-sized row holding a Dock=Fill AutoSize panel is circular and
// collapses to zero height when the form is laid out before the message loop runs.
left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
left.Controls.Add(_profileList);
// A wrapping row so five buttons fit the 280 px column without clipping any label.
var leftButtons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, Theme.Space3, 0, 0) };
```

- [ ] **Step 2: Defer the launch show to the message loop**

In `Program.cs` replace:

```csharp
if (controller.Settings.ShowWindowOnLaunch) mainForm.ShowAndFocus();
```

with:

```csharp
if (controller.Settings.ShowWindowOnLaunch)
{
    // Showing before Application.Run skips the normal DPI and layout pass and collapses
    // the auto-sized rows; Idle fires once the loop is up.
    EventHandler? showOnce = null;
    showOnce = (_, _) => { Application.Idle -= showOnce; mainForm.ShowAndFocus(); };
    Application.Idle += showOnce;
}
```

- [ ] **Step 3: Build and test**

Run: `dotnet build` then `dotnet test`. Expected: 0 warnings, 0 errors, all tests pass (59).

- [ ] **Step 4: Hand check**

Set `showWindowOnLaunch` true in `publish/profiles.json` or via the dialog, run `dotnet run --project src/GammaKey`. Add, Duplicate, Delete, Import, Export and Settings are visible. Turn it off, relaunch, open from tray, same.

- [ ] **Step 5: Commit**

`fix: explicit left-column rows and deferred launch show so buttons survive show-on-launch`

---

### Task 2: Apply restores non-target monitors

**Files:**
- Modify: `src/GammaKey/Display/DisplayEngine.cs`
- Modify: `src/GammaKey/Vibrance/VibranceEngine.cs`
- Modify: `src/GammaKey/App/AppController.cs` (Preview)
- Test: `tests/GammaKey.Tests/DisplayEngineTests.cs` (create)

**Interfaces:**
- Produces: `public static IReadOnlyList<string> DisplayEngine.OtherDevices(IReadOnlyList<MonitorInfo> all, IReadOnlyList<MonitorInfo> targets)`; `public void DisplayEngine.RestoreBaseline(Baseline from, IEnumerable<string> deviceNames)`; `public void VibranceEngine.RestoreBaseline(Baseline from, IEnumerable<string> deviceNames)`.

- [ ] **Step 1: Failing tests**

Create `tests/GammaKey.Tests/DisplayEngineTests.cs`:

```csharp
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
```

Run: `dotnet test --filter DisplayEngineTests`. Expected: build error, `OtherDevices` not defined.

- [ ] **Step 2: DisplayEngine**

Add after `RestoreBaseline(Baseline from)`:

```csharp
public void RestoreBaseline(Baseline from, IEnumerable<string> deviceNames)
{
    // Leaves the guard target alone: only the monitors outside the profile go back to original.
    foreach (var deviceName in deviceNames)
        if (from.Ramps.TryGetValue(deviceName, out var ramp)) GammaRampNative.Set(deviceName, ramp);
}

public static IReadOnlyList<string> OtherDevices(IReadOnlyList<MonitorInfo> all, IReadOnlyList<MonitorInfo> targets)
{
    var targeted = new HashSet<string>(targets.Select(m => m.DeviceName), StringComparer.OrdinalIgnoreCase);
    return all.Where(m => !targeted.Contains(m.DeviceName)).Select(m => m.DeviceName).ToList();
}
```

- [ ] **Step 3: VibranceEngine**

Add after `RestoreBaseline(Baseline from)`:

```csharp
public void RestoreBaseline(Baseline from, IEnumerable<string> deviceNames)
{
    foreach (var deviceName in deviceNames)
        if (from.VibranceLevels.TryGetValue(deviceName, out var level)) SetRaw(deviceName, level);
}
```

- [ ] **Step 4: Controller**

In `AppController.Preview`, replace the first two lines:

```csharp
var monitors = Display.ResolveTargets(target, out var note);
var result = Display.Apply(values, monitors);
```

with:

```csharp
var monitors = Display.ResolveTargets(target, out var note);
// A profile owns the whole desk: monitors it does not target go back to original, or they
// would keep the last curve written to them for ever.
var others = DisplayEngine.OtherDevices(Display.GetMonitors(), monitors);
Display.RestoreBaseline(_baseline, others);
if (Vibrance.IsAvailable) Vibrance.RestoreBaseline(_baseline, others);
var result = Display.Apply(values, monitors);
```

- [ ] **Step 5: Build and test**

Run: `dotnet test`. Expected: all pass (62).

- [ ] **Step 6: Hand check (needs two monitors)**

Dark Room on primary, then a profile on All, then Normal on primary: the other monitors return to normal. Change a profile's Monitors from All to Primary: the other monitors return to normal at once.

- [ ] **Step 7: Commit**

`fix: applying a profile restores the original curve on monitors it does not target`

---

### Task 3: Restore monitor button

**Files:**
- Modify: `src/GammaKey/UI/MainForm.cs`
- Modify: `src/GammaKey/UI/SettingsForm.cs` (label text)

- [ ] **Step 1: Remove the editor Reset row**

In `BuildLayout`, delete:

```csharp
var actions = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(Theme.LabelColumnWidth, Theme.Space2, 0, 0) };
actions.Controls.Add(Button("Reset", (_, _) => _controller.Reset()));
right.Controls.Add(actions);
```

Give `_status` the top margin the actions row carried, so the status line keeps its spacing: change `_status` Margin to `new Padding(Theme.LabelColumnWidth, Theme.Space2, 0, 0)`.

- [ ] **Step 2: Add the left-column button**

After the Settings button is added to `left`, add:

```csharp
var restoreButton = Button("Restore monitor", (_, _) => _controller.Reset());
restoreButton.AutoSize = false;
restoreButton.Dock = DockStyle.Fill;
restoreButton.Height = Theme.ControlHeight;
restoreButton.Margin = new Padding(0, Theme.Space3, 0, 0);
left.Controls.Add(restoreButton);
```

Set `left.RowCount = 4` and add a fourth `RowStyle(SizeType.AutoSize)`.

- [ ] **Step 3: Settings label**

In `SettingsForm`, change the label text `"Reset hotkey"` to `"Restore monitor hotkey"`.

- [ ] **Step 4: Build, test, hand check, commit**

`dotnet build`, `dotnet test` clean. Hand check: the editor has no Reset button; "Restore monitor" sits under Settings and restores the screen; the dialog label reads "Restore monitor hotkey".

`feat: move Reset out of the editor as a Restore monitor button under Settings`

---

### Task 4: HotkeyPassthrough setting and IHotkeyListener

**Files:**
- Modify: `src/GammaKey/Models/AppSettings.cs`
- Create: `src/GammaKey/Hotkeys/IHotkeyListener.cs`
- Modify: `src/GammaKey/Hotkeys/HotkeyListener.cs` (implement the interface)
- Test: `tests/GammaKey.Tests/ProfileStoreTests.cs`

**Interfaces:**
- Produces: `AppSettings.HotkeyPassthrough` (bool, default false); `IHotkeyListener`.

- [ ] **Step 1: Failing tests**

Append to `ProfileStoreTests`:

```csharp
[Fact]
public void HotkeyPassthroughRoundTrips()
{
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
    var store = new ProfileStore(path);
    var settings = ProfileStore.Defaults();
    settings.HotkeyPassthrough = true;

    store.Save(settings);

    Assert.True(store.Load().HotkeyPassthrough);
}

[Fact]
public void HotkeyPassthroughDefaultsToFalseWhenMissing()
{
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
    File.WriteAllText(path, "{\"version\":1,\"profiles\":[]}");

    var loaded = new ProfileStore(path).Load();

    Assert.False(loaded.HotkeyPassthrough);
}
```

Match the helper style already used in that file (the `Defaults()` and temp-path pattern used by the `ShowWindowOnLaunch` tests). Run: `dotnet test --filter HotkeyPassthrough`. Expected: build error.

- [ ] **Step 2: Setting**

In `AppSettings`, after `ShowWindowOnLaunch`: `public bool HotkeyPassthrough { get; set; }`

- [ ] **Step 3: Interface**

Create `src/GammaKey/Hotkeys/IHotkeyListener.cs`:

```csharp
namespace GammaKey.Hotkeys;

public interface IHotkeyListener : IDisposable
{
    event Action<int>? Pressed;
    bool Register(int id, HotkeyCombo combo);
    void UnregisterAll();
}
```

Change `HotkeyListener` declaration to `public sealed class HotkeyListener : NativeWindow, IHotkeyListener`. Body unchanged.

- [ ] **Step 4: Build, test, commit**

`dotnet test` all pass (64).

`feat: HotkeyPassthrough setting and IHotkeyListener interface`

---

### Task 5: HotkeyTable and KeyboardHookListener

**Files:**
- Create: `src/GammaKey/Hotkeys/HotkeyTable.cs`
- Create: `src/GammaKey/Hotkeys/KeyboardHookListener.cs`
- Test: `tests/GammaKey.Tests/HotkeyTableTests.cs` (create)

**Interfaces:**
- Consumes: `IHotkeyListener`, `HotkeyCombo` (`Modifiers`, `Key`, `ModControl/ModAlt/ModShift/ModWin`).
- Produces: `HotkeyTable` with `Register(int id, HotkeyCombo combo)`, `UnregisterAll()`, `int? Match(uint modifiers, Keys key)`; `KeyboardHookListener : IHotkeyListener`.

- [ ] **Step 1: Failing tests**

```csharp
using GammaKey.Hotkeys;
using Xunit;

namespace GammaKey.Tests;

public class HotkeyTableTests
{
    private static HotkeyCombo Combo(string text)
    {
        Assert.True(HotkeyCombo.TryParse(text, out var combo));
        return combo;
    }

    [Fact]
    public void MatchReturnsIdForSameModifiersAndKey()
    {
        var table = new HotkeyTable();
        table.Register(100, Combo("Ctrl+F1"));

        var id = table.Match(HotkeyCombo.ModControl, Keys.F1);

        Assert.Equal(100, id);
    }

    [Fact]
    public void MatchIsNullForDifferentModifiers()
    {
        var table = new HotkeyTable();
        table.Register(100, Combo("Ctrl+F1"));

        var id = table.Match(HotkeyCombo.ModControl | HotkeyCombo.ModShift, Keys.F1);

        Assert.Null(id);
    }

    [Fact]
    public void MatchIsNullAfterUnregisterAll()
    {
        var table = new HotkeyTable();
        table.Register(100, Combo("Ctrl+F1"));
        table.UnregisterAll();

        var id = table.Match(HotkeyCombo.ModControl, Keys.F1);

        Assert.Null(id);
    }

    [Fact]
    public void RegisterSameIdReplacesCombo()
    {
        var table = new HotkeyTable();
        table.Register(100, Combo("Ctrl+F1"));
        table.Register(100, Combo("Alt+F2"));

        var old = table.Match(HotkeyCombo.ModControl, Keys.F1);

        Assert.Null(old);
    }

    [Fact]
    public void PlainKeyMatchesWithNoModifiers()
    {
        var table = new HotkeyTable();
        table.Register(101, Combo("NumPad1"));

        var id = table.Match(0, Keys.NumPad1);

        Assert.Equal(101, id);
    }
}
```

Run: `dotnet test --filter HotkeyTableTests`. Expected: build error. If `HotkeyCombo.TryParse` rejects `"NumPad1"`, check `HotkeyComboTests` for the accepted spelling and use it.

- [ ] **Step 2: HotkeyTable**

```csharp
namespace GammaKey.Hotkeys;

/// Maps (modifiers, key) to a hotkey id for the keyboard-hook listener.
public sealed class HotkeyTable
{
    private readonly Dictionary<int, (uint Modifiers, Keys Key)> _byId = new();

    public void Register(int id, HotkeyCombo combo) => _byId[id] = (combo.Modifiers, combo.Key);

    public void UnregisterAll() => _byId.Clear();

    public int? Match(uint modifiers, Keys key)
    {
        foreach (var (id, combo) in _byId)
            if (combo.Modifiers == modifiers && combo.Key == key) return id;
        return null;
    }
}
```

- [ ] **Step 3: KeyboardHookListener**

```csharp
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
    private IntPtr _hook;

    public event Action<int>? Pressed;

    public KeyboardHookListener()
    {
        _proc = Callback;
        _hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(null), 0);
    }

    public bool Register(int id, HotkeyCombo combo)
    {
        _table.Register(id, combo);
        // A hook is never refused by another app, so registration cannot fail.
        return true;
    }

    public void UnregisterAll() => _table.UnregisterAll();

    private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var info = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            int msg = wParam.ToInt32();
            bool injected = (info.Flags & LlkhfInjected) != 0;
            if (!injected && msg is WmKeyDown or WmSysKeyDown)
            {
                // The LL hook has no repeat flag; a key already down is auto-repeat.
                if (_down.Add(info.VkCode) && !IsModifierKey(info.VkCode))
                {
                    var id = _table.Match(CurrentModifiers(), (Keys)info.VkCode);
                    if (id is not null) Pressed?.Invoke(id.Value);
                }
            }
            else if (msg is WmKeyUp or WmSysKeyUp)
            {
                _down.Remove(info.VkCode);
            }
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
```

The hook must be installed on a thread with a message loop. `AppController` is constructed on the UI thread in `Program.Main`, so constructing the listener there satisfies that.

- [ ] **Step 4: Build, test, commit**

`dotnet test` all pass (69).

`feat: HotkeyTable and KeyboardHookListener for pass-through hotkeys`

---

### Task 6: Controller switch, Settings checkbox with warning, no-modifier warning gate

**Files:**
- Modify: `src/GammaKey/App/AppController.cs`
- Modify: `src/GammaKey/UI/SettingsForm.cs`
- Modify: `src/GammaKey/UI/MainForm.cs` (SaveSettingsAndRegister)

**Interfaces:**
- Consumes: `IHotkeyListener`, `HotkeyListener`, `KeyboardHookListener`, `AppSettings.HotkeyPassthrough`.
- Produces: `public void AppController.SetHotkeyPassthrough(bool on)`.

- [ ] **Step 1: Controller**

Change the field `private readonly HotkeyListener _hotkeys = new();` to `private IHotkeyListener _hotkeys;`. In the constructor, after `Settings = _profileStore.Load();`:

```csharp
_hotkeys = CreateListener(Settings.HotkeyPassthrough);
_hotkeys.Pressed += OnHotkeyPressed;
```

Remove the old `_hotkeys.Pressed += OnHotkeyPressed;` line. Add:

```csharp
private static IHotkeyListener CreateListener(bool passthrough) =>
    passthrough ? new KeyboardHookListener() : new HotkeyListener();

public void SetHotkeyPassthrough(bool on)
{
    if (Settings.HotkeyPassthrough == on) return;
    _hotkeys.Pressed -= OnHotkeyPressed;
    _hotkeys.Dispose();
    _hotkeys = CreateListener(on);
    _hotkeys.Pressed += OnHotkeyPressed;
    Settings.HotkeyPassthrough = on;
    Save();
    RegisterHotkeys();
}
```

- [ ] **Step 2: Settings dialog**

Add a field `private readonly CheckBox _passthrough = new() { Text = "Let hotkeys pass through to other apps", AutoSize = true };` and place it after the guard checkbox in the layout. In `LoadSettingsIntoControls` set `_passthrough.Checked = _controller.Settings.HotkeyPassthrough;`. Handler:

```csharp
_passthrough.CheckedChanged += (_, _) =>
{
    if (_loading) return;
    if (_passthrough.Checked)
    {
        var answer = MessageBox.Show(this,
            "Pass-through watches every key press with a system-wide keyboard hook. Some anti-cheat systems flag that.\n\n" +
            "Recommended: leave this off and give each hotkey a Ctrl or Alt modifier.\n\n" +
            "Turn it on anyway?",
            "GammaKey", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
        if (answer != DialogResult.OK)
        {
            _loading = true;
            _passthrough.Checked = false;
            _loading = false;
            return;
        }
    }
    _controller.SetHotkeyPassthrough(_passthrough.Checked);
};
```

Grow `ClientSize` height by one checkbox row (`Theme.ControlHeight + Theme.Space2`) so the new box fits without clipping.

- [ ] **Step 3: No-modifier warning gate**

In `MainForm.SaveSettingsAndRegister` and `SettingsForm.SaveAndRegister`, add `&& !_controller.Settings.HotkeyPassthrough` to the condition that appends `HotkeyCombo.NoModifierWarning`.

- [ ] **Step 4: Build, test, hand check, commit**

`dotnet test` all pass. Hand check: enable pass-through, Cancel leaves it off; OK turns it on; profile on `Numpad1`, type `1` in Notepad: digit appears and profile switches; disable, key is swallowed again; the no-modifier warning shows only when pass-through is off.

`feat: opt-in hotkey pass-through with anti-cheat warning in Settings`

---

### Task 7: README games section

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add after "## Notes"**

```markdown
## Games

Run games in borderless windowed mode. In exclusive fullscreen many games write their own
gamma curve when they gain focus, which overwrites GammaKey until the next hotkey press.

Hotkeys swallow their key by default, so a profile on `Numpad1` stops `1` typing anywhere.
Settings has "Let hotkeys pass through to other apps", which uses a system-wide keyboard
hook instead. Some anti-cheat systems flag that; the safer choice is a Ctrl or Alt modifier.
```

- [ ] **Step 2: Commit**

`docs: games section, borderless mode and pass-through caveat`
