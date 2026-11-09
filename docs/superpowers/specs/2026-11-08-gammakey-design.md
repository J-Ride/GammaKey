# GammaKey design spec

Date: 2026-11-08
Status: approved by Jordan in brainstorming, ready for implementation plan

## 1. Purpose

GammaKey is a small Windows tray app that switches display colour profiles by global hotkey.
A profile sets gamma, brightness, contrast, shadows, highlights and (on NVIDIA) digital vibrance
for one or more monitors. It replaces the RivaTuner plus NVIDIA Control Panel hotkey workflow
that stopped working after recent Windows updates. Main use is Escape from Tarkov, but it works
anywhere: it never detects or touches a game.

Users: Jordan and friends. Shared as one self-contained exe plus a copyable profile file.

## 2. Non-goals

- No game detection, no per-game automation.
- No injection, hooks, overlays, DLLs or anything inside a game process. The tool only calls the
  Windows display API and the NVIDIA driver library, the same channels the NVIDIA Control Panel uses.
- No installer, no admin rights.
- No AMD vibrance in v1 (gamma, brightness, contrast, shadows, highlights still work on AMD and Intel).

## 3. Decisions

| Decision | Choice | Why |
|---|---|---|
| Stack | C# .NET 8, WinForms, single self-contained x64 exe | Direct access to Win32 and NVAPI without extra downloads; one file to share |
| Colour engine | Per-monitor GPU gamma curve via Windows gamma table, plus re-apply guard | Same curve the NVIDIA Control Panel sliders drive; works on every GPU; HDR Gamma Fix and Gloam prove it works with HDR on in practice |
| Vibrance | NVAPI digital vibrance per display, optional | Only driver path for saturation; PrismHDR uses it with HDR on |
| Hotkeys | One global hotkey per profile plus one reset hotkey, user mapped | Jump straight to a look; reset avoids being stuck |
| Monitor target | Per profile: primary, all, or a selected monitor | Jordan's choice |
| UI | Tray icon plus one main window, designed with the ui-ux-pro-max skill | Jordan requested a high quality, uniform UI |
| Proof test | None. Build the full app; Jordan tests it on the gaming PC when done | Jordan is hands-off and wants to test the finished app |

## 4. Architecture

Five units. Each has one job and a small interface.

### 4.1 DisplayEngine
- Lists monitors (device name, friendly name, primary flag, HDR on or off).
- `Apply(profileValues, targets)`: builds a 256 by 3 gamma table from slider values and sets it
  on each target monitor.
- `CaptureBaseline()`: on startup saves each monitor's current table to `baseline.json`.
- `RestoreBaseline()`: puts the saved tables back.
- Guard: a timer (default 3 s) that re-applies the active profile if Windows cleared the table
  (after sleep, sign-in, Display Settings, resolution change). Compares the read-back table to the
  expected one before writing, so idle cost is one read.
- HDR awareness: reads the HDR state per monitor for display in the UI. The curve is applied the
  same way in both modes. The user tunes sliders with their real HDR state and saves what they see.

### 4.2 VibranceEngine (optional)
- On startup tries to load the NVAPI library. If it fails, `IsAvailable = false` and the UI greys
  the vibrance slider with the note "NVIDIA driver not found".
- `Get(display)` and `Set(display, level 0..100)`. Baseline captured and restored like the curve.
- Never required by any other unit.

### 4.3 HotkeyListener
- Registers each profile hotkey and the reset hotkey with Windows as global hotkeys
  (RegisterHotKey). Raises an event with the profile id. No keyboard hook, no key logging.
- Reports a registration failure (key already taken) back to the UI.

### 4.4 ProfileStore
- Reads and writes `profiles.json` beside the exe. Atomic write (write temp, then rename).
- Validates ranges on load and clamps out-of-range values.

### 4.5 UI
- Tray icon. Left click opens the main window. Right click menu: one entry per profile (name,
  hotkey), Reset, Start with Windows (toggle), Quit.
- Main window. Left: profile list with Add, Duplicate, Delete. Right: name, hotkey capture box,
  target picker (Primary / All / named monitor), six sliders with number boxes, Apply, Save, Reset.
  Sliders preview live while dragging.
- Toast on hotkey press showing the profile name (can be turned off in settings).
- Visual design is produced with the ui-ux-pro-max skill during implementation: consistent
  spacing, type scale, dark theme suited to gaming desktops, clear disabled state for vibrance.

### 4.6 Data flow
Hotkey press or slider drag -> profile values -> DisplayEngine.Apply and VibranceEngine.Set on
the target monitors. Reset -> RestoreBaseline on both engines. Quit -> RestoreBaseline first.

## 5. Profile model

```json
{
  "version": 1,
  "resetHotkey": "F12",
  "startWithWindows": false,
  "guardEnabled": true,
  "showToast": true,
  "profiles": [
    {
      "id": "7f3c...",
      "name": "Dark Room",
      "hotkey": "F9",
      "target": { "mode": "primary" },
      "gamma": 1.6,
      "brightness": 20,
      "contrast": -10,
      "shadows": 35,
      "highlights": 0,
      "vibrance": 65
    }
  ]
}
```

`target.mode` is `primary`, `all`, or `monitor` with `"monitorName": "<friendly name>"`.

| Slider | Range | Normal | Effect on the curve |
|---|---|---|---|
| Gamma | 0.3 to 2.8 | 1.0 | Power curve on the whole range |
| Brightness | -100 to +100 | 0 | Shifts the whole curve up or down |
| Contrast | -100 to +100 | 0 | Stretches or flattens the curve around mid grey |
| Shadows | -100 to +100 | 0 | Lifts or lowers only the dark end (smooth weight, zero above mid grey) |
| Highlights | -100 to +100 | 0 | Lifts or lowers only the bright end (smooth weight, zero below mid grey) |
| Vibrance | 0 to 100 | 50 | NVIDIA digital vibrance level; greyed when NVAPI is absent |

Curve build order: gamma, then contrast, then brightness, then shadows and highlights, then clamp
to 0..65535. All sliders at normal must produce the identity table exactly.

## 6. Error handling

- Baseline saved before any write. Restored on Reset, on Quit, and at next launch if the previous
  run did not exit cleanly (a `running.lock` file still present).
- Windows may silently refuse an extreme table. The engine reads the table back after writing and
  shows a status line "Windows refused this curve, reduce the sliders" when it did not take.
- Hotkey taken: capture box shows "Already used by another app, pick another".
- Target monitor unplugged: profile falls back to primary and the toast says so.
- NVAPI missing: vibrance greyed, everything else unchanged.
- Known side effect: with a custom curve loaded on NVIDIA, some Chromium apps (Discord, Chrome)
  can flicker because of the driver's multi-plane overlay handling. Documented in README; Reset
  clears it; a driver update usually fixes it.

## 7. Testing

- Unit tests (xUnit) on the curve math: identity at normal values, each slider moves only its
  region, combined output stays in range, monotonic for positive gamma.
- Unit tests on ProfileStore: round trip, clamping, atomic write, missing file gives defaults.
- Display, vibrance and hotkey units are thin Win32 wrappers; verified by Jordan on the gaming PC
  (NVIDIA, HDR on, Tarkov offline) when the app is complete.

## 8. Build and delivery

- `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`.
- Output: `GammaKey.exe` (about 70 MB). First run creates `profiles.json` with three starter
  profiles (Dark Room, Bright Outdoors, Normal) and the reset hotkey, all unassigned until the
  user sets keys.
- Unsigned exe: README notes the SmartScreen "More info, Run anyway" step.

## 9. Logged fallback options (not built)

Use only if Jordan's test shows the curve is ignored while HDR is on.

1. Write the curve through the Argyll `dispwin` helper, as HDR Gamma Fix does. Ships one small
   extra exe beside GammaKey.
2. Apply the curve as an MHC2 ICC profile per monitor through Windows colour management. Heavier
   and slower to switch; last resort.
3. For HDR-specific brightness only: drive the Windows "SDR content brightness" per display.

Vibrance under HDR is reported working by PrismHDR but not independently verified. If it fails,
the slider stays greyed with a note while HDR is on.

## 10. References

- Microsoft SetDeviceGammaRamp: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-setdevicegammaramp
- HDR Gamma Fix: https://github.com/kaeldrin-gh/HDR-Gamma-Fix
- Gloam: https://getgloam.org/
- PrismHDR: https://github.com/Fennix36GH/PrismHDR
- chromashift HDR issue: https://github.com/designmatty/chromashift/issues/89
- dantmnf MHC2 notes: https://github.com/dantmnf/MHC2

## 11. UI tokens

Source: `ui-ux-pro-max` skill, `--design-system` query "gaming utility desktop tool dark mode
minimal" (style "Dark Mode (OLED)") and `--domain color "gaming dark utility"` (palette "Gaming",
neon purple). The skill targets web and mobile, so values that it does not give for WinForms keep
the Task 11 brief defaults. Constants live in `src/GammaKey/UI/Theme.cs`.

| Token | Value | Source | Use |
|---|---|---|---|
| Background | `#0F0F23` (15,15,35) | skill Background | Window and panel fill |
| Surface | `#1E1C35` (30,28,53) | skill Card | List, text boxes, buttons |
| SurfaceRaised | `#27273B` (39,39,59) | skill Muted | Selected list row, button hover, toast |
| Border | `#4C1D95` (76,29,149) | skill Border | Button outline, slider track, toast outline |
| TextPrimary | `#E2E8F0` (226,232,240) | skill Foreground | Body text, slider thumb (15.3:1 on Background) |
| TextMuted | `#94A3B8` (148,163,184) | skill Muted Foreground | Row labels, captions (7.4:1) |
| Accent | `#A78BFA` (167,139,250) | skill Secondary | Apply button, slider fill, selection bar, tray mark |
| AccentHover | `#C4B5FD` (196,181,253) | derived, one step lighter violet | Apply hover, thumb while dragging |
| FocusRing | `#7C3AED` (124,58,237) | skill Ring / Primary | 2 px ring 3 px outside the slider thumb (3.3:1) |
| Warning | `#FFB04C` (255,176,76) | brief default (skill has none) | Status line (10.4:1) |
| Disabled | `#464A56` (70,74,86) | brief default | Disabled slider track, fill and thumb |
| Text on Accent | Background `#0F0F23` | skill On Secondary | Apply label (6.9:1) |

| Type role | Font | Use |
|---|---|---|
| Title | Segoe UI Semibold 15 pt | "Profile", "Settings", toast |
| Label | Segoe UI 10 pt | Body and row labels |
| Value | Segoe UI Semibold 10 pt | Numeric readouts (right aligned) |
| Caption | Segoe UI 9 pt | Vibrance note |

The skill suggests Inter; Segoe UI is kept because it is the Windows system font and needs no
install.

| Layout token | Value |
|---|---|
| Spacing scale | 4 / 8 / 12 / 16 / 24 px (Space1, 2, 3, 4, 6) |
| Window padding | 24 px; column gap 24 px |
| Control height | 32 px (rows, buttons, list items) |
| Label column | 100 px |
| Button minimum width | 72 px |
| Slider | track 6 px rounded, thumb 18 px circle, fill grows from the neutral value |
| Disabled | whole control drawn in Disabled colour; slider and readout disabled; caption explains why |
| Focus | slider: FocusRing 2 px ring; other controls: Windows focus cues |
