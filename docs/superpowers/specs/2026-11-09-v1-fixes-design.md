# GammaKey v1 fixes: auto-save, instant hotkeys, select-to-apply, settings dialog, import and export

Date: 2026-11-09. Branch: `feature/v1-fixes`. Follows the v1 spec at `2026-11-08-gammakey-design.md`.

## 1. Problem

First hands-on test of v1 on the laptop found:

1. Hotkeys never fired. Hotkeys were registered with Windows only at startup and after the Save button. A hotkey typed into the box but not saved was never registered, and nothing told the user a Save was needed.
2. Clicking a profile in the list only loaded it into the editor. The screen did not change until a slider was dragged.
3. There was no settings dialog, no "show window on launch" option, no link to the profiles file, and no way to import or export profiles.
4. The "Duplicate" button label was clipped.

## 2. Decisions

- **Auto-save everything.** Every edit writes `profiles.json` at once. The Save and Apply buttons are removed.
- **Hotkeys register on change.** Any hotkey edit saves and re-registers all hotkeys with Windows immediately.
- **Selecting a profile applies it.** List click, hotkey and tray menu all do the same thing.
- **Settings move to a modal dialog** opened by a Settings button under the profile list.
- **Import and Export live in the profile action row**, not in Settings, because they manage presets.
- **Show window on launch** is a new setting, default off, so today's tray-only start is unchanged.
- **Import always adds, never overwrites.** Fresh ids, name suffix on clash, colliding hotkeys cleared.

## 3. Auto-save and instant hotkeys

All edits in `MainForm` go through `AppController.Save()`. Three kinds of edit:

| Edit | After the edit |
|---|---|
| Slider, name, monitor target | `Save()` |
| Profile hotkey, Reset hotkey | `Save()` then `RegisterHotkeys()` |
| Add, Duplicate, Delete, Import | `Save()` then `RegisterHotkeys()` |

- Sliders keep the live preview. Saving happens when a drag ends (mouse up, key up) and on each number box change, not on every tick of a drag.
- `RegisterHotkeys()` returns the names whose key Windows refused. The status line shows `Hotkey already used by another app: <names>. Pick another.` and the hotkey box for that profile uses `Theme.Warning` as its text colour until the hotkey changes.
- **No-modifier warning.** When the typed hotkey has none of Ctrl, Alt, Shift or Win, the status line shows `This key will be blocked in every app while GammaKey runs. Add Ctrl or Alt to be safe.` The hotkey is still accepted and registered.
- The action row under the editor keeps the Reset button only. No "Saved" message. Silence means saved.
- `ApplyCurrent`, `SaveCurrent` and `_applyButton` are removed from `MainForm`.

## 4. Selection applies the profile

`LoadSelectedProfile` in `MainForm` loads the editor as today and then calls `_controller.ApplyProfile(_editing)`. The status line shows the apply note, or stays empty. Hotkey, tray menu and list click now share one path. `RefreshProfileList(keepSelection: true)` must not re-apply: the `_loading` flag already suppresses `SelectedIndexChanged` during the refresh, and that behaviour is kept.

## 5. Settings dialog

### 5.1 Model

`AppSettings` gains `public bool ShowWindowOnLaunch { get; set; }` (default false). `ProfileStore` reads and writes it like the other flags. Files without the field load as false.

### 5.2 Launch

`Program.cs`: after `controller.Start()` and `new MainForm(controller)`, if `controller.Settings.ShowWindowOnLaunch` then `mainForm.ShowAndFocus()`.

### 5.3 Dialog

New `UI/SettingsForm.cs`, a `Form` with `FormBorderStyle.FixedDialog`, no minimise or maximise, `StartPosition.CenterParent`, styled with `Theme`. Opened modal from a Settings button at the bottom of the left column in `MainForm`. Contents, top to bottom:

- `CheckBox` Start with Windows: calls `_controller.SetStartWithWindows(checked)`.
- `CheckBox` Show window on launch: sets `Settings.ShowWindowOnLaunch`, `Save()`.
- `CheckBox` Show profile name on hotkey: sets `Settings.ShowToast`, `Save()`.
- `CheckBox` Re-apply if Windows resets the curve: sets `Settings.GuardEnabled`, `Save()`.
- Label "Reset hotkey" and a `HotkeyCaptureBox`: sets `Settings.ResetHotkey`, `Save()`, `RegisterHotkeys()`, with the same refused and no-modifier messages in a status label inside the dialog.
- `Button` Open profiles folder: `Process.Start` Explorer on `_controller.BaseDirectory`.
- `Button` Close.

Each control saves on change, exactly as the removed main window block did. The dialog subscribes to `SettingsChanged` while open so a tray toggle of Start with Windows updates its checkbox, and unsubscribes on close.

### 5.4 Main window

The "Settings" title, Reset hotkey box and three checkboxes leave `MainForm`. The profile list takes the full left column height above the Settings button.

## 6. Import and Export

### 6.1 Buttons

The profile action row becomes Add, Duplicate, Delete, Import, Export. Buttons use `AutoSize = true` with the existing 72 px minimum so "Duplicate" is never clipped; the row wraps to two lines if the column is too narrow (`FlowLayoutPanel` with `WrapContents = true`).

### 6.2 Export

`SaveFileDialog`, filter `GammaKey profiles (*.json)|*.json`, default name `GammaKey-profiles.json`. Writes the full `AppSettings` shape through `ProfileStore.JsonOptions`, with `Profiles` set to the current list and the other flags at their current values. Using the same shape as `profiles.json` means an exported file or a copied `profiles.json` both import. Status line: `Exported 3 profiles.` Any IO error: `Could not write that file.`

### 6.3 Import

`OpenFileDialog`, same filter. Steps:

1. Parse with `ProfileStore` loading rules (same clamping and validation as startup). A file that fails to parse or has no profiles: status `Could not read that file. Is it a GammaKey profiles file?`, nothing changes.
2. Merge with `ProfileImport.Merge(existing, incoming, resetHotkey)`, a pure function in new `Storage/ProfileImport.cs`:
   - every incoming profile gets a fresh id;
   - an incoming name equal to an existing name (ordinal, case-insensitive) gets ` (imported)` appended; if that name also exists, ` (imported 2)`, and so on;
   - an incoming hotkey equal to an existing profile hotkey, the reset hotkey, or an earlier incoming hotkey in the same import is set to `""` and counted;
   - returns the list to add and the number of hotkeys cleared.
3. Add the profiles, `Save()`, `RegisterHotkeys()`, `RefreshProfileList()`, select the first imported profile.
4. Status line: `Imported 3 profiles.` or `Imported 3 profiles. 1 hotkey cleared because it was in use.`

## 7. Tests

xUnit tests in `tests/GammaKey.Tests`:

- `ProfileImportTests`: fresh ids on every imported profile; name clash gets ` (imported)`; second clash gets ` (imported 2)`; hotkey clash with an existing profile is cleared and counted; hotkey clash with the reset hotkey is cleared; two incoming profiles with the same hotkey keep the first and clear the second; no clashes imports unchanged apart from ids.
- `ProfileStoreTests`: `ShowWindowOnLaunch` round-trips; a file without the field loads as false.
- `HotkeyCombo` already has tests; the no-modifier check is `combo.Modifiers == 0`, exercised through a small `HasModifier` helper test.

Hand-checked, not automated: hotkey fires right after typing it; list click changes the screen; Settings dialog toggles; Export then Import of the same file adds three "(imported)" copies with hotkeys cleared; Duplicate label fully visible.

## 8. Out of scope

- Undo or "discard changes". Auto-save has no draft state.
- Merge-by-id import for syncing between machines.
- Per-profile toast or guard settings.
- Any change to engines, natives or the hotkey listener.
