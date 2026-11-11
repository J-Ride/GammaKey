# GammaKey

Switch your monitor's gamma, brightness, contrast, shadows, highlights, colour balance and NVIDIA
vibrance with a hotkey. Made for Escape from Tarkov's dark corners, works anywhere on the desktop.

![Main window](docs/screenshots/main-window.png)

## What it does

- **Profiles.** Each profile holds a full set of picture values, a hotkey and a monitor target
  (primary, all, or one named monitor). Press the hotkey anywhere and the picture changes.
- **Press again to restore.** Pressing the hotkey of the profile that is already active puts the
  original picture back. A separate **Restore monitor** hotkey does the same from any state.
- **Lives in the tray.** Closing the window hides it by the clock. Right-click the icon for the
  profile list, Restore, Start with Windows and Quit.
- **Share profiles.** Export one profile or all of them to a JSON file. Import a file from a friend.
- **Safe by design.** The original picture is saved when the app starts and put back when you
  restore, quit, or start again after a crash.

## Requirements

- Windows 10 or 11, 64-bit.
- Nothing else to install. The download is a single self-contained exe (about 70 MB) that
  carries its own .NET runtime.
- An NVIDIA graphics card for the **Vibrance** slider. On AMD and Intel cards that slider is
  greyed out and everything else works.
- No administrator rights.

## Install and run

1. Download `GammaKey.exe` from the [latest release](https://github.com/J-Ride/GammaKey/releases/latest).
2. Put it in its own folder. It saves its files beside itself (see Files below).
3. Double-click it. Windows SmartScreen may warn because the exe is unsigned: click **More info**,
   then **Run anyway**.
4. An icon appears by the clock. Left-click it to open the window.

Only one copy runs at a time. Starting a second one shows a note and quits.

## Using it

### The window

- **Left rail:** the profile list with each profile's hotkey. Below it: add, duplicate, delete,
  import and export buttons, then **Settings** and **Restore monitor**.
- **Top strip:** the primary monitor, its resolution and refresh rate, whether HDR is on, and how
  many hotkeys are armed.
- **Editor:** the selected profile's name, hotkey and monitor target, then the sliders.

### Make a profile

1. Click **+** to add a profile, or pick one of the three starter profiles (Dark Room, Bright
   Outdoors, Normal).
2. Drag the sliders. The screen changes as you drag so you can see the result.
3. Click the **Hotkey** box and press your key combination. Backspace clears it.
4. Choose which **Monitors** the profile targets.
5. Click **Apply** to save. **Revert** throws the edits away and reloads the saved profile.

Selecting a profile in the list applies it to the screen. If you move to another profile with
unsaved edits, a dialog asks whether to save them or discard them.

### Sliders

| Group | Sliders | Range |
|---|---|---|
| Curve | Gamma | 0.30 to 2.80 (1.00 is normal) |
| Curve | Brightness, Contrast, Shadows, Highlights | -100 to +100 |
| Balance | Warm/cool, Red, Green, Blue | -100 to +100 |
| Balance | Black floor | 0 to 100 |
| Color | Vibrance (NVIDIA only) | 50 to 100 (50 is normal) |

Curve and Balance change the colour table the graphics card applies to the whole screen. Vibrance
is set in the NVIDIA driver and survives alt-tab.

If you push the sliders to an extreme, Windows may refuse the curve. The status line then says
"Windows refused this curve, reduce the sliders" and the previous picture stays.

### Hotkeys

- Hotkeys are global. They work in games and on the desktop while GammaKey runs.
- Set a **Restore monitor** hotkey in Settings so you can always get back to normal.
- A hotkey without Ctrl, Alt or Shift swallows its key, so a profile on `F9` stops `F9` reaching
  other apps. Use a modifier, or turn on pass-through (see Settings).
- If another program already owns a combination, the hotkey box shows "in use by another app"
  and the status line says "Hotkey already used by another app" with the profile names.

### Settings

| Setting | What it does |
|---|---|
| Start with Windows | Runs in the tray at sign-in. Adds a Run entry for the current user that points at the exe, so if you move the exe, turn this off and on again. |
| Show window on launch | Off means tray only. |
| Show profile name on hotkey | A short toast at the bottom right when a hotkey fires. |
| Re-apply if Windows resets the curve | Every 3 seconds, checks that the active curve is still on screen and puts it back if a game or the driver cleared it. On by default. |
| Let hotkeys pass through to other apps | Uses a system-wide keyboard hook instead of Windows hotkeys, so the key also reaches the game. See the anti-cheat warning under Games. |
| Restore monitor hotkey | The key that puts the original picture back. |
| Open profiles folder | Opens the folder that holds the exe and its files. |

## Files

Everything is saved beside `GammaKey.exe`. Nothing is written anywhere else except the optional
Start with Windows entry in the registry.

| File | Purpose |
|---|---|
| `profiles.json` | Your profiles and settings. Plain JSON, safe to copy to another PC. Written to a temp file first, then swapped in, so a crash cannot leave it half written. |
| `profiles.json.corrupt` | Made only if `profiles.json` could not be read. Your old file is kept here and the app starts with the defaults. |
| `baseline.json` | The monitor's original colour table and vibrance, captured at start. |
| `running.lock` | Exists while the app runs. If it is still there at the next start, the last run died with a curve on screen, so the saved baseline is restored first. |

Export writes the same JSON shape as `profiles.json`, so a copied `profiles.json` and an exported
file both import. Imported profiles whose hotkey is already taken arrive with the hotkey cleared.

## How it works, and why it is safe

GammaKey changes the colour curve your graphics card applies to the whole screen, the same control
the NVIDIA Control Panel's "Adjust desktop color settings" page uses. It talks only to Windows and
the graphics driver. It never opens, reads, hooks or injects anything into a game. The one
exception is the optional pass-through setting, which installs a system-wide keyboard hook; see
Games.

## Games

Run games in borderless windowed mode. In exclusive fullscreen many games write their own gamma
curve when they gain focus, which overwrites GammaKey until the next hotkey press or the next
guard check.

**Anti-cheat.** The pass-through setting uses a system-wide keyboard hook. Some anti-cheat systems
flag that. The safer choice is to leave it off and use a Ctrl, Alt or Shift modifier on your
hotkeys. Pass-through also does not see keys sent by macro software or a Stream Deck, and gets
nothing while an elevated (admin) window has focus.

## Notes

- **HDR.** Tune the sliders with HDR on if you game with HDR on; what you see is what is saved.
- **Flicker in Discord or Chrome** with a curve active is a known NVIDIA driver quirk with custom
  curves. Update the driver, or press Restore monitor while not gaming.
- **Monitor unplugged.** A profile that targets a monitor that is not connected falls back to
  the primary monitor and says so in the status line.

## Build from source

Needs the .NET 8 SDK.

    dotnet build
    dotnet test
    .\publish.ps1

`publish.ps1` runs the tests, then builds the single self-contained exe into `publish\GammaKey.exe`.
