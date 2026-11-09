# GammaKey v1 fixes, round 2: launch layout, monitor restore, Restore button, hotkey pass-through

Date: 2026-11-09. Branch: `feature/v1-fixes`. Follows `2026-11-09-v1-fixes-design.md`.

## 1. Problem

Hand test of round 1 on the laptop found:

1. With "Show window on launch" on, the main window opens with the profile list filling the whole left column. Add, Duplicate, Delete, Import, Export and Settings are missing, and reopening the window does not bring them back.
2. Applying a profile writes only to the monitors it targets. Monitors outside the target keep whatever curve was last written to them, forever, until Reset.
3. The Reset button sits in the profile editor and reads as "reset this preset". It actually restores the monitor to its startup state, the same thing the Reset hotkey does.
4. Hotkeys registered with Windows swallow the key. A profile on `Numpad1` stops `1` typing in every app. Jordan wants an option where the key still reaches the app and the profile still switches.
5. Nothing tells users to run games in borderless mode.

## 2. Decisions

- **Layout rows are explicit.** The left column table gets one percent row and two auto rows, and the button row stops asking for both fill and auto-size. The window is shown from the form's `Load` event, never before the message loop.
- **Apply restores the rest.** Applying a profile restores the startup baseline on every monitor not in the target set, then applies to the targets. Vibrance follows the same rule.
- **Reset becomes "Restore monitor".** It moves out of the editor to the left column, under the Settings button. The Settings dialog label becomes "Restore monitor hotkey". The toast text stays "Reset to original".
- **Pass-through is a setting, off by default.** `AppSettings.HotkeyPassthrough`. When on, hotkeys are detected by a low-level keyboard hook and the key is passed on to the active app. Turning it on shows a warning dialog with OK and Cancel. The no-modifier warning is skipped while pass-through is on, because the key is no longer blocked.
- **README says borderless.** One short section.

## 3. Launch layout (issue 1)

`MainForm.BuildLayout`, left column:

```csharp
left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
```

The button `FlowLayoutPanel` drops `Dock = DockStyle.Fill` and gets `Anchor = AnchorStyles.Left | AnchorStyles.Right` plus `AutoSize = true`, so its height comes from its content and its width from the column.

`Program.cs` keeps the setting check but defers the show until the message loop is running, with a one-shot `Application.Idle` handler:

```csharp
if (controller.Settings.ShowWindowOnLaunch)
{
    EventHandler? showOnce = null;
    showOnce = (_, _) => { Application.Idle -= showOnce; mainForm.ShowAndFocus(); };
    Application.Idle += showOnce;
}
```

`Idle` fires on the message loop after startup, so DPI scaling and layout run in the normal order.

Hand check: turn the setting on, relaunch, all five buttons and Settings are visible. Turn it off, relaunch, open from tray, same. 125 percent scaling if available.

## 4. Apply restores non-target monitors (issue 2)

`DisplayEngine` gains:

```csharp
public void RestoreBaseline(Baseline from, IEnumerable<string> deviceNames)
```

It writes the baseline ramp for each named device that has one and does not touch `_activeRamp` or `_activeMonitors` (the guard still protects the targets).

`VibranceEngine` gains the same overload for vibrance levels.

`AppController.Preview(values, target)` becomes:

```csharp
var monitors = Display.ResolveTargets(target, out var note);
var others = Display.GetMonitors().Where(m => !monitors.Contains(m)).Select(m => m.DeviceName).ToList();
Display.RestoreBaseline(_baseline, others);
if (Vibrance.IsAvailable) Vibrance.RestoreBaseline(_baseline, others);
var result = Display.Apply(values, monitors);
...
```

`MonitorInfo` equality: compare by `DeviceName`, so a static helper `DisplayEngine.OtherDevices(all, targets)` returns the device names not in targets, compared ordinal-ignore-case. This helper is pure and tested.

Slider preview during a drag also goes through `Preview`, so the restore runs on every tick for the other monitors. The baseline write is a single `SetDeviceGammaRamp` per other monitor and is cheap; the spec accepts it. If a drag feels slow, the follow-up is to restore others only when the target set changed.

Hand check: Dark Room on primary; switch to All; switch to Normal on primary; the other monitors go back to normal. Change a profile's Monitors from All to Primary; the other monitors go back to normal at once.

## 5. Restore monitor button (issue 3)

- `MainForm`: remove the `actions` panel with the Reset button from the editor. Add a "Restore monitor" button under Settings in the left column, same style as Settings (full width). It calls `_controller.Reset()`.
- `SettingsForm`: label "Reset hotkey" becomes "Restore monitor hotkey".
- `AppController.RegisterHotkeys` still reports the name `"Reset"` in the failed list; `SettingsForm` still matches on `"Reset"`. No change there.

Hand check: button under Settings restores the monitor; the editor has no Reset button; the dialog label reads "Restore monitor hotkey".

## 6. Hotkey pass-through

### 6.1 Model

`AppSettings.HotkeyPassthrough`, `bool`, default `false`. `ProfileStore` reads and writes it like the other flags.

### 6.2 Listener interface

New `Hotkeys/IHotkeyListener.cs`:

```csharp
public interface IHotkeyListener : IDisposable
{
    event Action<int>? Pressed;
    bool Register(int id, HotkeyCombo combo);
    void UnregisterAll();
}
```

`HotkeyListener` implements it unchanged.

### 6.3 Hook listener

New `Hotkeys/KeyboardHookListener.cs`, `IHotkeyListener`:

- `SetWindowsHookEx(WH_KEYBOARD_LL = 13, callback, moduleHandle, 0)` installed in the constructor on the calling (UI) thread. The delegate is kept in a field so the GC cannot collect it. `UnhookWindowsHookEx` in `Dispose`.
- The callback handles `WM_KEYDOWN (0x100)` and `WM_SYSKEYDOWN (0x104)`. It ignores injected input (`KBDLLHOOKSTRUCT.flags & 0x10`). It reads modifier state with `GetAsyncKeyState` for Ctrl (0x11), Alt (0x12), Shift (0x10), Win (0x5B, 0x5C) and builds a modifier mask using `HotkeyCombo.ModControl/ModAlt/ModShift/ModWin`.
- Auto-repeat: the hook keeps a `HashSet<uint>` of keys currently down. A key already in the set is a repeat and is ignored. `WM_KEYUP (0x101)` and `WM_SYSKEYUP (0x105)` remove the key.
- Matching uses `HotkeyTable`, a pure class: `Register(id, combo)`, `UnregisterAll()`, `int? Match(uint modifiers, Keys key)`. A match raises `Pressed(id)`.
- The callback always returns `CallNextHookEx`, so the key reaches the active app. That is the whole point.
- `Register` always returns true: a hook cannot be refused by another app. Collisions between GammaKey's own hotkeys are already cleared by import and are the user's own doing in the editor.

Modifier keys themselves (Ctrl, Alt, Shift, Win key-downs) are never matched as a hotkey key.

### 6.4 Controller

`AppController` holds `IHotkeyListener _hotkeys`. It is created in the constructor from the setting:

```csharp
_hotkeys = CreateListener(Settings.HotkeyPassthrough);
```

New `public void SetHotkeyPassthrough(bool on)`: if unchanged, return; `_hotkeys.Dispose()`; create the other listener; wire `Pressed`; set the setting; `Save()`; `RegisterHotkeys()`.

### 6.5 Settings dialog

A fifth checkbox after "Re-apply if Windows resets the curve": "Let hotkeys pass through to other apps". On check, before saving, show:

```
MessageBox.Show(this,
    "Pass-through watches every key press with a system-wide keyboard hook. " +
    "Some anti-cheat systems flag that.\n\n" +
    "Recommended: leave this off and give each hotkey a Ctrl or Alt modifier.\n\n" +
    "Turn it on anyway?",
    "GammaKey", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning)
```

Cancel puts the box back to unchecked under `_loading` and changes nothing. OK calls `_controller.SetHotkeyPassthrough(true)`. Uncheck calls `SetHotkeyPassthrough(false)` with no dialog.

### 6.6 No-modifier warning

`MainForm.SaveSettingsAndRegister` and `SettingsForm.SaveAndRegister` show `HotkeyCombo.NoModifierWarning` only when `!_controller.Settings.HotkeyPassthrough`.

### 6.7 Tests

`HotkeyTableTests`: match returns the id for the same modifiers and key; no match for a different modifier mask; no match after `UnregisterAll`; a later `Register` with the same id replaces the combo.
`ProfileStoreTests`: `HotkeyPassthrough` round-trips; missing field loads as false.

Hand check: pass-through on, profile on `Numpad1`, type `1` in Notepad; the digit appears and the profile switches. Pass-through off, the same key is swallowed as before. The warning dialog appears on enable and Cancel leaves it off.

## 7. README

Section "Games": run games in borderless windowed mode. In exclusive fullscreen many games write their own gamma curve when they gain focus and overwrite GammaKey. One sentence on pass-through and its anti-cheat caveat.

## 8. Out of scope

- Per-hotkey pass-through. It is one global setting.
- Restoring others only on target change (optimisation noted in section 4).
- Any change to toast, guard timing, or import rules.
