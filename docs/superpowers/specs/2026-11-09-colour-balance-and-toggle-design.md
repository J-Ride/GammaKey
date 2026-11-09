# Colour balance and press-again-to-restore design

Date: 2026-11-09
Status: approved by Jordan in brainstorming, ready for implementation plan
Extends: `2026-11-08-gammakey-design.md` (sections 4.1, 5, 7)

## 1. Purpose

Two additions to GammaKey:

1. **Colour balance.** Five new sliders per profile: Warm/cool, Red, Green, Blue, Black floor.
   They give per-channel tint and a hard black level, the two things the current single-curve
   sliders cannot do. Works on every GPU because it is still the Windows gamma table.
2. **Press again to restore.** Pressing the hotkey of the profile that is already active restores
   the baseline, the same as the reset hotkey.

## 2. Non-goals

- No hue rotation. RGB gains give tint; hue is dropped.
- No sharpness, filters, texture or any driver 3D setting.
- No per-monitor values inside one profile (deferred to v2).
- No startup profile, no cycle hotkey.
- No new window or settings option. Press-again-to-restore is always on.

## 3. Decisions

| Decision | Choice | Why |
|---|---|---|
| Where the balance lives | Extend `ColorValues`, `Profile` and `CurveBuilder` in place | Smallest change, one test surface, old files load unchanged |
| Channel gain limit | Plus or minus 30 percent of full scale | A channel can never clip to white from gain alone |
| Black floor limit | Floor at most half scale | Keeps the table inside the Windows clamp with brightness at max |
| Toggle setting | None, always on | YAGNI; the guard already covers re-apply |
| File version | Stays 1, new fields default to normal | Shared profile files from v1 keep working |

## 4. Model

`ColorValues` gains five fields; `Profile` gains the matching properties and `Values` maps them.

| Field | Range | Normal | Meaning |
|---|---|---|---|
| Temperature | -100 to +100 | 0 | Positive is warm (red up, blue down), negative is cool |
| Red | -100 to +100 | 0 | Gain on the red channel |
| Green | -100 to +100 | 0 | Gain on the green channel |
| Blue | -100 to +100 | 0 | Gain on the blue channel |
| BlackFloor | 0 to 100 | 0 | Minimum output level; 100 means half scale |

`Clamped()` clamps the new fields to these ranges. `Normal` has all five at 0.
A JSON file missing any field reads as 0, in both `ProfileStore` and `ProfileImport`.

## 5. Curve

`CurveBuilder.Build` keeps the current order (gamma, contrast, brightness, shadows, highlights)
and adds a per-channel stage after it:

```
gainR = 1 + 0.3 * (Red/100   + Temperature/100)
gainG = 1 + 0.3 * (Green/100)
gainB = 1 + 0.3 * (Blue/100  - Temperature/100)
floor = BlackFloor/100 * 0.5

for each channel c:
  yc = y * gain_c
  yc = floor + yc * (1 - floor)
  clamp yc to 0..1, scale to 0..65535
  non-decreasing: value_c = max(value_c, previous_c)
```

The combined gain is clamped to the range 0.4 to 1.6 before use, so Red at +100 with
Temperature at +100 does not exceed the limit. `previous` becomes one running value per channel.
All sliders at normal must still produce the identity table exactly.

## 6. Press again to restore

`AppController` keeps `_activeProfileId` (null when nothing is applied).

- Profile hotkey or list click for a different profile: apply it, set active, toast as today.
- Profile hotkey for the active profile: call `Reset()`, toast "Restored".
- `Reset()` (reset hotkey, tray Reset, or the toggle above) clears active.
- A refused apply does not change active.
- Preview from the editor does not change active.

## 7. UI

Five new `GkSlider` rows in the existing slider column of `MainForm`, in this order after
Highlights and before Vibrance: Warm/cool, Red, Green, Blue, Black floor. Same control, same
number box, same reset-to-normal behaviour as the existing rows. `CurrentValues()` and the
load-from-profile path carry the new fields. Vibrance stays last with its NVIDIA note.

## 8. Error handling

Nothing new. Gamma table failures surface through the existing `ApplyResult`.

## 9. Testing

`CurveBuilder` (xUnit, one assertion per test):

- all normal produces the identity table
- Temperature +100 raises red mid grey and lowers blue mid grey
- Red +50 changes only the red channel
- BlackFloor 100 maps input 0 to 32768 (half scale)
- extreme values on every slider keep each channel non-decreasing

`AppController`:

- same profile hotkey twice restores the baseline
- a different profile hotkey switches the active profile
- reset clears the active profile so the next press applies

## 10. Build order

Model and curve first (pure, testable), then controller toggle, then UI rows last. Branch from
master after the UI theme branch merges, since the UI rows touch `MainForm`.
