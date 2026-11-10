# Editor Apply/Revert, export choice and UI fixes design

Date: 2026-11-10
Status: approved by Jordan in brainstorming, ready for implementation plan
Extends: `2026-11-08-gammakey-design.md` (sections 4.5, 7), `2026-11-09-ui-tactical-restyle-design.md`

## 1. Purpose

Round 3 hand testing of the published build found four UI defects and two flow gaps.

Flow gaps:

1. **No way back from an edit.** Every slider drag saves to disk, so a user who experiments and
   forgets the starting values has lost them. The editor must work on an unsaved copy with
   explicit Apply and Revert.
2. **Export is all or nothing.** A user cannot share one profile without sharing the whole
   library.

UI defects:

3. The window can be dragged narrower than its content (the minimum size is set but not
   enforced).
4. Profile rows in the list clip the summary line at 100 percent scale and crowd at 125 and 150.
5. The gamma slider's centre label "1.00" sits in the middle of the track while the thumb at
   1.00 sits 28 percent along it.
6. Hotkey chips on the second and later profile rows draw an empty box; the chip text of every
   row lands on the first row.

## 2. Non-goals

- No temp file for unsaved edits. Unsaved edits live in memory only; a crash keeps the saved
  profile intact, which is the point of the feature.
- No Cancel in the unsaved-changes dialog. Two choices only.
- No undo history. One saved state, one working copy.
- Hotkeys never use the working copy. They apply the saved profile.
- No change to the profile file format.

## 3. Decisions

| Decision | Choice | Why |
|---|---|---|
| Working copy | The editor binds to a `Profile` clone; the saved profile in `Settings.Profiles` is untouched until Apply | Simplest model that makes Revert a reload |
| Scope of Apply | Everything in the editor: name, hotkey, monitor target, all sliders | One rule for the whole editor (Jordan) |
| Live preview | Sliders still preview on screen from the working copy | Real-time feedback was the reason for auto-save; keep it |
| Dialog wording | "Save and Apply" and "Discard changes"; editor buttons "Apply" and "Revert" | "Cancel" is ambiguous about which action it cancels; "Discard changes" names what happens to the edits |
| Dialog outcome | Either button completes the interrupted action | One click, never a dead end (Jordan) |
| Export choice | Dialog with "This profile only" and "All profiles" | Share one profile without the library |
| Gamma scale | Draw the centre label under the origin's true position (option A) | Honest with a 0.30 floor; the linear track stays |
| Row height | Derived from font heights at the running DPI | Fixed 44 px clipped the summary at 100 percent |
| Chip text | Pass the row's top into `DrawChip`; no Graphics transform | GDI `TextRenderer` ignores GDI+ transforms |

## 4. Working copy and dirty state (MainForm)

- `_editing` becomes the working copy: `Profile.Clone()` of the selected profile, keeping the
  same `Id` and `Hotkey` (the current `Clone()` gives a new id and clears the hotkey; the editor
  uses a new `Profile.CopyForEditing()` that keeps both).
- `_saved` is the selected profile inside `Settings.Profiles`.
- `IsDirty` is true when the working copy differs from the saved one in name, hotkey, target
  mode, target monitor name, or any of the eleven colour values.
- Name, hotkey and monitor edits write to the working copy only. Slider and number edits write
  to the working copy and preview it on screen (`Preview(values, target)`), as today, but no
  longer save.
- Changing the monitor dropdown previews the working copy on the new target. It no longer
  applies and saves.
- The Apply and Revert buttons sit in a row under the Hotkey and Monitors row inside the
  Profile group, right aligned: Revert (standard button) then Apply (primary, accent fill like
  Add). Both are disabled while `IsDirty` is false and when no profile is selected.
- The heading shows the working name live, with " *" appended while dirty.

### Apply

1. Copy name, hotkey, target and values from the working copy into the saved profile.
2. `Save()`, then `RegisterHotkeys()` (the hotkey may have changed; refusals show as today via
   `SaveSettingsAndRegister`).
3. `ApplyProfile(saved)` so the screen and the active profile match the saved state.
4. Refresh the list row and clear dirty.

### Revert

1. Replace the working copy with a fresh copy of the saved profile.
2. Reload the editor controls under `_loading`.
3. `Preview(saved.Values, saved.Target)` so the screen matches the saved profile again.
4. Clear dirty.

### Guarded actions

Before each of these, if `IsDirty`, show the unsaved-changes dialog and run its choice first:
selecting another list row, Add, Duplicate, Delete, Import, Export, Settings, closing the
window (hide to tray), and Quit from the tray menu. Hotkeys and the reset key are not guarded;
they never touch the editor.

- List selection: the guard runs in `SelectedIndexChanged` before `LoadSelectedProfile`. The
  dialog's choice is applied to the previously selected profile, then the new row loads.
- Window close: `OnFormClosing` with `CloseReason.UserClosing` runs the guard, then hides.
- Quit: `TrayIcon` asks the main form to run the guard before `Application.Exit()`.

### Unsaved-changes dialog

Owner-drawn small form in the app theme, modal, title "Unsaved changes", text
"<name> has unsaved changes." Buttons: **Save and Apply** (primary) and **Discard changes**.
Escape and the close box count as Discard changes. Returns which was chosen; the caller runs
Apply or Revert and then continues its action.

## 5. Export choice

`ExportProfiles()` first shows a themed dialog "Export" with two options:

- **This profile only** (default when a profile is selected; disabled when none is)
- **All profiles**

Then the existing SaveFileDialog. "This profile only" writes an `AppSettings` with a single
profile (the saved one, not the working copy) and default settings, so Import reads it with no
change. Suggested file name: `<profile name>.json` for one, `GammaKey-profiles.json` for all.
Status text: "Exported 1 profile." or "Exported N profiles."

## 6. UI fixes

### 6.1 Minimum window size

Keep `MinimumSize` 860 by 600 logical. Investigate why it is not enforced (the DPI scale pass
runs after it is set; the chrome attributes may also matter). Set it after `ResumeLayout` in
device pixels via `Theme.Scale`, and verify by hand at 100, 125 and 150 percent that the
window cannot be dragged narrower or shorter than the content.

### 6.2 Profile row height

`ProfileList` computes its row height from the fonts: `Label.Height + Caption.Height + 2 *
Space2`, measured at the control's DPI, and lays out the name and summary rectangles from the
same heights. `ItemHeight` is set from that in the constructor and again on `DpiChangedAfterParent`.
No fixed 18 or 14.

### 6.3 Gamma scale label

`GkSlider` draws the centre label centred on `ValueToX(Origin)` instead of the track's centre
when `Origin` is above `Minimum`. Sliders whose origin is at the minimum (Black floor,
Vibrance) keep the middle value in the middle. The left and right labels are unchanged.

### 6.4 Chip text on every row

`GkHotkeyBox.DrawChip` gains a `y` parameter (the top of the line to centre on). `ProfileList`
passes `row.Top` and drops the `TranslateTransform`. `GkHotkeyBox` passes 0.

## 7. Error handling

- Apply failing to write the file: existing `Save()` behaviour; the editor stays dirty.
- A refused hotkey on Apply: the existing refusal highlight; the profile is still saved.
- Export write failure: existing "Could not write that file." text.

## 8. Testing

Unit tests (xUnit):

- `Profile.CopyForEditing` keeps Id and Hotkey and copies every value.
- A dirty check helper returns false for an identical copy and true for each changed field
  (name, hotkey, target, one colour value).
- Single-profile export produces an `AppSettings` with one profile that `ProfileStore.TryParse`
  reads back.
- `GkSlider` centre label position: a pure helper that returns the label x for a given origin.
- `ProfileList` row height is at least the two font heights plus spacing at 96 and 144 dpi.

Hand checks (gaming PC and laptop): drag the window to its minimum at 100, 125 and 150
percent; rows show both lines at each scale; gamma "1.00" sits under the thumb at 1.00; every
row shows its chip; edit then switch rows, both dialog choices; edit then Quit; export one
profile and import it on the other machine.

## 9. Build order

Bug fixes first (6.4, 6.3, 6.2, 6.1), each its own commit, then the working copy and dirty
state, then Apply and Revert, then the guard and dialog, then the export choice.
