# GammaKey UI restyle: tactical dark theme

Date: 2026-11-09. Branch: `feature/v1-fixes`. Mock: `docs/mockups/gammakey-ui-mockup-v2.html`
(reviewed by Jordan in orca over three rounds; this spec records the agreed state).

## 1. Purpose

Replace the purple "Gaming" palette and stock WinForms controls with a clean, detailed, dark
look in the style of competitive FPS tooling. Every control is owner-drawn from Theme tokens.
No new dependency. No new feature: every element on screen maps to data the app already has.

## 2. Non-goals (parked, see section 7)

- Curve graph panel. Editable curve with draggable points. Per-channel RGB curves.
- "Test 3 s" and "Apply" buttons. Guard and NVAPI chips in the status strip.

## 3. Decisions

| Decision | Choice | Why |
|---|---|---|
| UI library | None. Owner-drawn controls in `src/GammaKey/UI/` | CLAUDE.md: no NuGet beyond xUnit. ReaLTaiizor rejected 2026-11-09 (size, own palettes, DPI). |
| Fonts | Segoe UI Variable Text (fallback Segoe UI), Cascadia Mono (fallback Consolas) | Ship with Windows 11; no font files in the exe. |
| Accent | Crosshair cyan, one accent only | Jordan rejected purple. Amber for warnings, red only on Delete, green only for "ready" dots. |
| Shapes | 4 px radius, 1 px lines, diamond slider thumb, square toggle | Tool look, not web look. |
| Title bar | DWM immersive dark mode + rounded corners | Removes the white caption bar, the biggest "basic WinForms" tell. |
| Icons | One family: 16 px, 2 px stroke, round caps, drawn with GDI+ | Matches the mock; no icon font or image assets. |

## 4. Tokens (replace spec 2026-11-08 section 11)

| Token | Value | Use |
|---|---|---|
| Background | `#0B0D10` (11,13,16) | Window fill |
| Panel | `#111417` (17,20,23) | Rail, status strip, status bar, group cards, dialog groups, toast |
| Surface | `#171B20` (23,27,32) | Inputs, buttons, selected row, chips |
| Raised | `#1F242B` (31,36,43) | Hover, slider track, toggle off |
| Line | `#262C34` (38,44,52) | Borders and dividers |
| LineStrong | `#343C47` (52,60,71) | Button borders, kbd chip borders, selected row border |
| TextPrimary | `#E6EAEF` (230,234,239) | Body text, slider thumb |
| TextMuted | `#8B95A3` (139,149,163) | Labels, eyebrows, chip text (6.0:1 on Panel) |
| TextDim | `#5C6673` (92,102,115) | Captions, scale numbers, version (3.4:1, large or secondary only) |
| Accent | `#35D6F0` (53,214,240) | Active bar, fill, focus border, spin arrows, Add button |
| AccentDim | `#1AA6BD` (26,166,189) | Toggle on, kbd chip border when selected |
| AccentGlow | Accent at 18 % alpha | 3 px focus halo |
| Warning | `#F5B041` (245,176,65) | Status text, anti-cheat flag |
| Danger | `#EF5350` (239,83,80) | Delete hover only |
| Ok | `#5AD38A` (90,211,138) | Status dots, "applied" line |
| TextOnAccent | Background | Add button glyph |

| Type role | Font | Use |
|---|---|---|
| Title | Segoe UI Variable Text Semibold 12 pt | Editor heading (profile name) |
| Label | Segoe UI Variable Text 9.5 pt | Body and row labels |
| Eyebrow | Segoe UI Variable Text Semibold 8 pt, uppercase, +1.5 px tracking | Group headers, rail head |
| Mono | Cascadia Mono 9.5 pt | Numbers, hotkey chips, status bar |
| MonoSmall | Cascadia Mono 7.5 pt | Scale labels, version |
| Caption | Segoe UI Variable Text 8.5 pt | Toggle descriptions, notes |

Spacing stays on the 4 px scale (Space1..Space6). ControlHeight 30. Radius 4.

## 5. Screens

### Main window (960 x 680)
- Title bar: dark, window icon is a cyan crosshair, text "GammaKey".
- Status strip (Panel, 1 px bottom Line): chips for primary monitor name, `W×H · N Hz`,
  `HDR on/off`, `Hotkeys N armed`. Dot colour: Ok when true, Dim otherwise.
- Rail (272 px, Panel, 1 px right Line): eyebrow "PROFILES" with mono count; owner-drawn list,
  row 44 px: name (Label, Primary), summary line (Caption, Dim: `gamma 1.55 · shadows +36`),
  hotkey chip right (MonoSmall kbd chip; Accent border when selected); selected row Surface fill,
  LineStrong border, 2 px Accent left bar. Below: icon row of five 30 px buttons (Add primary,
  Duplicate, Delete danger-hover, Import, Export) then "Settings" ghost button and
  "Restore monitor" button with the reset hotkey as kbd chips.
- Editor: heading = profile name (Title) + "ACTIVE" tag when the profile is the one applied.
  Three group cards: Profile (Name full row; Hotkey and Monitors side by side), Curve
  (Gamma, Brightness, Contrast, Shadows, Highlights), Color (Vibrance + note). Each slider row:
  label 92 px, full-width track with scale labels (min, origin, max) under it, 72 px mono readout
  with stacked accent spin arrows. Status (Warning) under the groups.
- Status bar (28 px, Panel): `● applied HH:mm:ss` (Ok), `hook: normal|pass-through`,
  right: `profiles.json · saved`.

### Settings dialog (440 wide)
Groups Startup (Start with Windows, Show window on launch), Behaviour (Show profile name on
hotkey, Re-apply if Windows resets the curve, Let hotkeys pass through with inline
"ANTI-CHEAT RISK" flag), Restore monitor hotkey. Each toggle: title, one-line description,
square switch right. Footer: "Open profiles folder" ghost, "Close".

### Toast
Panel fill, LineStrong border, 3 px Accent left bar, crosshair mark, profile name (Title) and
mono detail line `CTRL ALT 1 · all monitors`.

## 6. Controls (all in `src/GammaKey/UI/`)

| Control | Replaces | API kept |
|---|---|---|
| `WindowChrome` (static) | none | `Apply(Form)` |
| `GkIcons` (static) + `GkIconButton` | Button with text | `Click` |
| `GkSlider` (restyle) | itself | `Minimum Maximum Value Origin ValueChanged`; new `ScaleFormatter` |
| `GkNumber` | `NumericUpDown` | `Minimum Maximum Value Increment DecimalPlaces ValueChanged` (decimal) |
| `GkToggle` | `CheckBox` | `Checked CheckedChanged Text`; new `Description`, `Flag` |
| `GkDropdown` | `ComboBox` | `Items SelectedIndex SelectedItem SelectedIndexChanged` (derives from ComboBox) |
| `GkHotkeyBox` | `HotkeyCaptureBox` (TextBox) | `Combo ComboChanged`; new `IsRefused` |
| `GkGroup` | none | `Title`, `Hint`, hosts a `FlowLayoutPanel` |
| `GkChip` | Label | `Text Value DotState` |
| `ProfileList` | `ListBox` owner draw | `SelectedIndex SelectedIndexChanged`; bound to `IReadOnlyList<Profile>` |

Pure helpers with unit tests: `ProfileSummary.For(Profile)`, `HotkeyChips.Split(string)`,
`NumberFormat.Signed(decimal, int places)`, `DisplayMode` record from `EnumDisplaySettings`.

## 7. Deferred (ledger)

- Read-only curve graph panel (draws `CurveBuilder.Build` output). Small; after the port.
- Editable curve with add, move, delete points; monotone cubic; reset to sliders.
- Per-channel RGB curves (ramp already has three tables).

## 8. Testing

xUnit for the pure helpers. Visual check by build, run and screenshot at 100 % and 150 % DPI.
`dotnet format --verify-no-changes` clean. Existing tests stay green.
