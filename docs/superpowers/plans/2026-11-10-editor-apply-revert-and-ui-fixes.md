# Editor Apply/Revert, Export Choice and UI Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix four UI defects from round 3 hand testing, then give the profile editor an unsaved working copy with Apply and Revert, an unsaved-changes guard, and an export dialog that can write one profile.

**Architecture:** The four fixes are local to `GkHotkeyBox.DrawChip`, `GkSlider`, `ProfileList` and the `MainForm` constructor. The flow change makes `MainForm._editing` a clone of the selected profile (`Profile.CopyForEditing()`), keeps the saved profile in `_saved`, and moves every save to one `ApplyEdits()` method. Two new small themed dialogs (`UnsavedChangesDialog`, `ExportChoiceDialog`) follow the `SettingsForm` pattern. Pure helpers (`Profile.HasSameContentAs`, `GkSlider.CentreLabelX`, `ProfileList.RowHeightFor`, `ProfileExport`) carry the unit tests.

**Tech Stack:** C# .NET 8 WinForms, x64, xUnit. No NuGet beyond xUnit.

**Spec:** `docs/superpowers/specs/2026-11-10-editor-apply-revert-and-ui-fixes-design.md`

## Global Constraints

- Branch `feature/editor-apply-revert`, already cut from `origin/master`. Commit on this branch only.
- No NuGet packages beyond xUnit. No new projects.
- Never run the app (`dotnet run` or the exe): it is a tray app and hangs the shell. Build and test only. Hand checks are Jordan's.
- Build: `dotnet build C:\Dev\Projects\GammaKey\GammaKey.sln`. Tests: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj --filter "FullyQualifiedName~<ClassName>"`. Format gate before each commit: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes`.
- Use `git -C C:\Dev\Projects\GammaKey ...` for every git command. Never `cd` into the repo.
- Commit messages are one descriptive line, no attribution trailers, no model names.
- Source files use CRLF line endings. Keep them.
- The test project has no `InternalsVisibleTo`, so any helper a test calls must be `public`.
- Tests: one assertion per test, Arrange-Act-Assert, names describe behaviour, no `if` or loops in tests.
- No em dashes in any user-visible text or comment. Copy strings are exact: "Apply", "Revert", "Unsaved changes", "<name> has unsaved changes.", "Save and Apply", "Discard changes", "Export", "This profile only", "All profiles", "Exported 1 profile.", "Exported N profiles.", heading suffix " *".
- Layout values are authored at 96 dpi and scaled with `Theme.Scale(logical, DeviceDpi)` in owner-drawn code. The form's `AutoScaleMode.Dpi` pass scales control bounds.
- Hotkeys and the reset key never touch the editor and are never guarded.
- Do not fix nearby code, refactor adjacent code, or add features beyond the task. Report out-of-scope findings instead.

---

## File map

| File | Change |
|---|---|
| `src/GammaKey/UI/GkHotkeyBox.cs` | `DrawChip` gains a `y` parameter (Task 1) |
| `src/GammaKey/UI/ProfileList.cs` | Pass `row.Top` to `DrawChip`, drop the transform (Task 1); font-derived row height (Task 3) |
| `src/GammaKey/UI/GkSlider.cs` | `CentreLabelX` helper, centre label drawn under the origin (Task 2) |
| `src/GammaKey/UI/MainForm.cs` | `MinimumSize` after the scale pass (Task 4); working copy, Apply, Revert (Task 6); guards (Task 7); export choice (Task 8) |
| `src/GammaKey/UI/Theme.cs` | `StylePrimary(Button)` for accent-filled text buttons (Task 6) |
| `src/GammaKey/Models/Profile.cs` | `CopyForEditing`, `CopyContentFrom`, `HasSameContentAs` (Task 5) |
| `src/GammaKey/UI/UnsavedChangesDialog.cs` | New (Task 7) |
| `src/GammaKey/UI/TrayIcon.cs` | Quit runs the guard (Task 7) |
| `src/GammaKey/UI/ExportChoiceDialog.cs` | New (Task 8) |
| `src/GammaKey/Storage/ProfileExport.cs` | New: single-profile `AppSettings` and suggested file name (Task 8) |
| `tests/GammaKey.Tests/GkSliderTests.cs` | New (Task 2) |
| `tests/GammaKey.Tests/ProfileListTests.cs` | New (Task 3) |
| `tests/GammaKey.Tests/ProfileTests.cs` | New (Task 5) |
| `tests/GammaKey.Tests/UnsavedChangesDialogTests.cs` | New (Task 7) |
| `tests/GammaKey.Tests/ProfileExportTests.cs` | New (Task 8) |

---

### Task 1: Hotkey chip text draws on every profile row

Bug: `ProfileList` translates the GDI+ `Graphics` by `row.Top` before calling `GkHotkeyBox.DrawChip`. The chip box is drawn with GDI+ (honours the transform) but the text is drawn with `TextRenderer` (GDI, ignores the transform), so every row's chip text lands on row 0.

**Files:**
- Modify: `src/GammaKey/UI/GkHotkeyBox.cs:92` (call) and `:101-114` (DrawChip)
- Modify: `src/GammaKey/UI/ProfileList.cs:59-65`

**Interfaces:**
- Produces: `internal static int DrawChip(Graphics g, string text, int x, int y, int lineHeight, float dpi, Color textColor, Color border)`. `y` is the top of the line the chip centres on, in device pixels. Returns the chip's right edge.

- [ ] **Step 1: Add the `y` parameter to `DrawChip`**

Replace lines 101-114 of `GkHotkeyBox.cs` with:

```csharp
    // A keycap: mono text in a box with a 2 px bottom edge, centred on a line that starts at y and
    // is lineHeight device pixels tall. Shared look with the profile list. The caller passes y
    // instead of translating the Graphics because TextRenderer draws with GDI and ignores GDI+
    // transforms, so a translated chip would keep its box but lose its text to row 0.
    internal static int DrawChip(Graphics g, string text, int x, int y, int lineHeight, float dpi, Color textColor, Color border)
    {
        var size = TextRenderer.MeasureText(g, text, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding);
        int h = size.Height + Theme.Scale(6, dpi);
        int w = size.Width + Theme.Scale(12, dpi);
        var chip = new Rectangle(x, y + (lineHeight - h) / 2 - 1, w, h);
        using (var b = new SolidBrush(Theme.Background)) Theme.FillRounded(g, b, chip, 3);
        using (var p = new Pen(border)) Theme.DrawRounded(g, p, chip, 3);
        using (var bottom = new Pen(border, 2f)) g.DrawLine(bottom, chip.Left + 3, chip.Bottom, chip.Right - 3, chip.Bottom);
        TextRenderer.DrawText(g, text, Theme.MonoSmall, chip, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        return chip.Right;
    }
```

- [ ] **Step 2: Update the call inside `GkHotkeyBox.OnPaint`**

Line 92 becomes:

```csharp
                x = DrawChip(g, part, x, 0, Height, DeviceDpi, chipText, _isRefused ? Theme.Warning : Theme.LineStrong) + S(Theme.Space1);
```

- [ ] **Step 3: Pass the row top from `ProfileList` and drop the transform**

Replace lines 59-65 of `ProfileList.cs` with:

```csharp
        // Chips centre on the row; DrawChip centres on the line it is given.
        int x = textRight;
        foreach (var part in parts)
            x = GkHotkeyBox.DrawChip(g, part, x, row.Top, row.Height, DeviceDpi, selected ? Theme.Accent : Theme.TextMuted, selected ? Theme.AccentDim : Theme.Line) + S(Theme.Space1);
```

- [ ] **Step 4: Build and run the full suite**

Run: `dotnet build C:\Dev\Projects\GammaKey\GammaKey.sln`
Expected: 0 warnings, 0 errors.

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj`
Expected: 111 passed.

- [ ] **Step 5: Format gate and commit**

Run: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes`
Expected: exit 0.

```
git -C C:\Dev\Projects\GammaKey add src/GammaKey/UI/GkHotkeyBox.cs src/GammaKey/UI/ProfileList.cs
git -C C:\Dev\Projects\GammaKey commit -m "Profile list draws hotkey chip text on its own row; DrawChip takes the line top instead of a Graphics transform"
```

---

### Task 2: Gamma scale label sits under the origin

Bug: the slider's centre label is drawn with `HorizontalCenter` over the whole width, but the Gamma origin (1.00) sits at 28 percent of the track (range 0.30 to 2.80). The spec chose option A: draw the centre label centred on the origin's true x. Sliders whose origin is at the minimum (Vibrance, Black floor) keep the middle value in the middle.

**Files:**
- Modify: `src/GammaKey/UI/GkSlider.cs:103-110`
- Create: `tests/GammaKey.Tests/GkSliderTests.cs`

**Interfaces:**
- Produces: `public static int CentreLabelX(int minimum, int maximum, int origin, Rectangle track, int width)`. Returns the x (device pixels, control coordinates) that the centre label centres on.

- [ ] **Step 1: Write the failing tests**

Create `tests/GammaKey.Tests/GkSliderTests.cs`:

```csharp
using System.Drawing;
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class GkSliderTests
{
    [Fact]
    public void CentreLabelSitsUnderOriginWhenOriginIsAboveMinimum()
    {
        // Gamma: 0.30 to 2.80 with origin 1.00 sits 70/250 of the way along a 250 px track.
        int x = GkSlider.CentreLabelX(30, 280, 100, new Rectangle(7, 8, 250, 4), 264);

        Assert.Equal(77, x);
    }

    [Fact]
    public void CentreLabelSitsInTheMiddleWhenOriginIsTheMinimum()
    {
        int x = GkSlider.CentreLabelX(50, 100, 50, new Rectangle(7, 8, 306, 4), 320);

        Assert.Equal(160, x);
    }

    [Fact]
    public void CentreLabelClampsAnOriginAboveMaximumToTheRightEnd()
    {
        int x = GkSlider.CentreLabelX(0, 100, 150, new Rectangle(7, 8, 306, 4), 320);

        Assert.Equal(313, x);
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj --filter "FullyQualifiedName~GkSliderTests"`
Expected: build error, `GkSlider` does not contain a definition for `CentreLabelX`.

- [ ] **Step 3: Add the helper and draw the centre label at that x**

In `GkSlider.cs`, add after `XToValue` (after line 61):

```csharp
    // Where the centre scale label sits. An origin at the minimum (Vibrance, Black floor) has no
    // label of its own, so the middle value is drawn in the middle. Any other origin is labelled
    // under its own position on the track, so Gamma's "1.00" sits where the thumb rests at 1.00.
    public static int CentreLabelX(int minimum, int maximum, int origin, Rectangle track, int width)
    {
        if (origin <= minimum || maximum == minimum) return width / 2;
        double fraction = (Math.Clamp(origin, minimum, maximum) - minimum) / (double)(maximum - minimum);
        return track.Left + (int)Math.Round(fraction * track.Width);
    }
```

Replace lines 103-110 (from `var formatter = ...` to the third `DrawText`) with:

```csharp
        var formatter = ScaleFormatter ?? (v => Format.Signed(v, 0));
        var scaleRect = new Rectangle(0, S(TrackRowHeight), Width, S(ScaleRowHeight));
        var scaleColor = Enabled ? Theme.TextDim : Theme.Raised;
        // An origin at the minimum (Vibrance) has no useful label of its own, so the middle value stands in.
        int centreValue = Origin <= _minimum ? (_minimum + _maximum) / 2 : Math.Clamp(Origin, _minimum, _maximum);
        string centreText = formatter(centreValue);
        int centreWidth = TextRenderer.MeasureText(g, centreText, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding).Width;
        int centreX = CentreLabelX(_minimum, _maximum, Origin, track, Width);
        var centreRect = new Rectangle(centreX - centreWidth / 2, scaleRect.Top, centreWidth, scaleRect.Height);
        TextRenderer.DrawText(g, formatter(_minimum), Theme.MonoSmall, scaleRect, scaleColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, centreText, Theme.MonoSmall, centreRect, scaleColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, formatter(_maximum), Theme.MonoSmall, scaleRect, scaleColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
```

- [ ] **Step 4: Run the tests to confirm they pass**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj --filter "FullyQualifiedName~GkSliderTests"`
Expected: 3 passed.

- [ ] **Step 5: Format gate and commit**

Run: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes`
Expected: exit 0.

```
git -C C:\Dev\Projects\GammaKey add src/GammaKey/UI/GkSlider.cs tests/GammaKey.Tests/GkSliderTests.cs
git -C C:\Dev\Projects\GammaKey commit -m "Slider centre label sits under the origin so Gamma 1.00 lines up with its thumb"
```

---

### Task 3: Profile row height comes from the fonts

Bug: `ProfileList.RowHeight` is a fixed 44 px and the two text rectangles are fixed 18 and 14 px. Segoe UI 9.5 pt and 8.5 pt need about 17 and 15 px at 96 dpi, so the summary line clips at 100 percent and both lines crowd at 125 and 150.

**Files:**
- Modify: `src/GammaKey/UI/ProfileList.cs`
- Create: `tests/GammaKey.Tests/ProfileListTests.cs`

**Interfaces:**
- Produces: `public static int RowHeightFor(float dpi)` on `ProfileList`. Removes `public const int RowHeight` (no other file uses it; confirm with `grep -rn "RowHeight" src tests` before deleting).

- [ ] **Step 1: Write the failing tests**

Create `tests/GammaKey.Tests/ProfileListTests.cs`:

```csharp
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class ProfileListTests
{
    [Fact]
    public void RowHeightAt96DpiFitsBothTextLinesPlusSpacing()
    {
        int needed = (int)Math.Ceiling(Theme.Label.GetHeight(96f)) + (int)Math.Ceiling(Theme.Caption.GetHeight(96f)) + 2 * Theme.Space2;

        Assert.True(ProfileList.RowHeightFor(96f) >= needed);
    }

    [Fact]
    public void RowHeightAt144DpiFitsBothTextLinesPlusScaledSpacing()
    {
        int needed = (int)Math.Ceiling(Theme.Label.GetHeight(144f)) + (int)Math.Ceiling(Theme.Caption.GetHeight(144f)) + 2 * Theme.Scale(Theme.Space2, 144f);

        Assert.True(ProfileList.RowHeightFor(144f) >= needed);
    }

    [Fact]
    public void RowHeightGrowsWithDpi()
    {
        Assert.True(ProfileList.RowHeightFor(144f) > ProfileList.RowHeightFor(96f));
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj --filter "FullyQualifiedName~ProfileListTests"`
Expected: build error, `ProfileList` does not contain a definition for `RowHeightFor`.

- [ ] **Step 3: Replace the fixed heights**

Replace the whole of `ProfileList.cs` with:

```csharp
using GammaKey.Models;

namespace GammaKey.UI;

public sealed class ProfileList : ListBox
{
    public ProfileList()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        // The form's scale pass does not touch ItemHeight, so set it in device pixels here.
        ItemHeight = RowHeightFor(DeviceDpi);
        BorderStyle = BorderStyle.None;
        IntegralHeight = false;
        BackColor = Theme.Panel;
        ForeColor = Theme.TextPrimary;
        Margin = Padding.Empty;
    }

    // Two text lines plus a Space2 band above and below, so the summary never clips at any scale.
    public static int RowHeightFor(float dpi) => NameLineHeight(dpi) + SummaryLineHeight(dpi) + 2 * Theme.Scale(Theme.Space2, dpi);

    private static int NameLineHeight(float dpi) => (int)Math.Ceiling(Theme.Label.GetHeight(dpi));
    private static int SummaryLineHeight(float dpi) => (int)Math.Ceiling(Theme.Caption.GetHeight(dpi));

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ItemHeight = RowHeightFor(DeviceDpi);
    }

    public void Bind(IReadOnlyList<Profile> profiles, int keepIndex)
    {
        BeginUpdate();
        Items.Clear();
        foreach (var p in profiles) Items.Add(p);
        if (keepIndex >= 0 && keepIndex < Items.Count) SelectedIndex = keepIndex;
        EndUpdate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || Items[e.Index] is not Profile profile) return;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        int S(int v) => Theme.Scale(v, DeviceDpi);
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var back = new SolidBrush(Theme.Panel)) g.FillRectangle(back, e.Bounds);
        var row = new Rectangle(e.Bounds.Left + S(Theme.Space2), e.Bounds.Top + 1, e.Bounds.Width - 2 * S(Theme.Space2) - 1, e.Bounds.Height - 3);
        if (selected)
        {
            using var fill = new SolidBrush(Theme.Surface);
            Theme.FillRounded(g, fill, row, Theme.Radius);
            using var border = new Pen(Theme.LineStrong);
            Theme.DrawRounded(g, border, row, Theme.Radius);
            using var bar = new SolidBrush(Theme.Accent);
            g.FillRectangle(bar, row.Left, row.Top + 3, 2, row.Height - 6);
        }

        var parts = GammaKey.UI.Format.HotkeyParts(profile.Hotkey);
        int chipsWidth = 0;
        foreach (var part in parts) chipsWidth += TextRenderer.MeasureText(g, part, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding).Width + S(12) + S(Theme.Space1);
        int textRight = row.Right - S(Theme.Space2) - chipsWidth;

        // Both lines are centred as a block inside the row so the Space2 bands stay equal.
        int nameHeight = NameLineHeight(DeviceDpi);
        int summaryHeight = SummaryLineHeight(DeviceDpi);
        int textTop = row.Top + (row.Height - nameHeight - summaryHeight) / 2;
        var nameRect = new Rectangle(row.Left + S(Theme.Space3), textTop, textRight - row.Left - S(Theme.Space3), nameHeight);
        TextRenderer.DrawText(g, profile.Name, Theme.Label, nameRect, Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        var subRect = new Rectangle(nameRect.Left, nameRect.Bottom, nameRect.Width, summaryHeight);
        TextRenderer.DrawText(g, GammaKey.UI.Format.ProfileSummary(profile), Theme.Caption, subRect, Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        // Chips centre on the row; DrawChip centres on the line it is given.
        int x = textRight;
        foreach (var part in parts)
            x = GkHotkeyBox.DrawChip(g, part, x, row.Top, row.Height, DeviceDpi, selected ? Theme.Accent : Theme.TextMuted, selected ? Theme.AccentDim : Theme.Line) + S(Theme.Space1);
    }
}
```

Then run `grep -rn "RowHeight\b" C:\Dev\Projects\GammaKey\src C:\Dev\Projects\GammaKey\tests` and fix any other reference to the removed constant (none is expected).

- [ ] **Step 4: Run the tests to confirm they pass, then the full suite**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj --filter "FullyQualifiedName~ProfileListTests"`
Expected: 3 passed.

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj`
Expected: 117 passed.

- [ ] **Step 5: Format gate and commit**

Run: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes`
Expected: exit 0.

```
git -C C:\Dev\Projects\GammaKey add src/GammaKey/UI/ProfileList.cs tests/GammaKey.Tests/ProfileListTests.cs
git -C C:\Dev\Projects\GammaKey commit -m "Profile rows size from the font heights so the summary line fits at every scale"
```

---

### Task 4: Minimum window size is enforced

Bug: `MainForm` sets `MinimumSize = new Size(860, 600)` at line 62, inside `SuspendLayout()` and before `AutoScaleMode = AutoScaleMode.Dpi`, yet the window can be dragged narrower than its content. This task is investigative: find the cause, apply the fix, and record what you found in your report. Jordan verifies by hand at 100, 125 and 150 percent.

Known facts to start from:

- WinForms answers `WM_GETMINMAXINFO` from `Form.MinimumSize`, so a non-empty `MinimumSize` on a shown form is normally enforced.
- `Form.ScaleControl` scales `MinimumSize` by the DPI factor during the scale pass at `ResumeLayout(true)`. Setting it before the pass is usually fine, but setting it after the pass in device pixels removes any doubt about the order.
- `WindowChrome.Apply` only sets DWM attributes (dark caption, rounded corners, border colour). It does not touch size.
- The reported cut-off may also mean the 860 by 600 floor is smaller than the content needs. Check the rail (272 logical) plus the editor's label column (92), the number column (72 plus Space3) and paddings, and the strip plus status bar heights. If the content needs more than 860 by 600 at 100 percent, report the measured need; do not change the floor without Jordan's decision.

**Files:**
- Modify: `src/GammaKey/UI/MainForm.cs:60-64` and `:89-92`

**Interfaces:** none.

- [ ] **Step 1: Read the WinForms source for `Form.MinimumSize` and `Form.ScaleControl` to confirm or rule out the order theory**

Use the .NET 8 `System.Windows.Forms` source on GitHub (dotnet/winforms, `src/System.Windows.Forms/src/System/Windows/Forms/Form.cs`). Note in your report: does `ScaleControl` scale `MinimumSize`, and does `WmGetMinMaxInfo` read `MinimumSize` directly? Do not spend more than a few minutes; the fix below is applied either way.

- [ ] **Step 2: Move `MinimumSize` after the scale pass, in device pixels**

In the constructor, delete line 62 (`MinimumSize = new Size(860, 600);`). Then change lines 89-92 to:

```csharp
        BuildLayout();
        Theme.Apply(this);
        // Every control now exists; a control added after this line would not be scaled.
        ResumeLayout(true);
        // Set after the scale pass, in device pixels, so the pass can neither skip nor double it.
        MinimumSize = new Size(Theme.Scale(860, DeviceDpi), Theme.Scale(600, DeviceDpi));
```

Add a DPI-change handler so a move between monitors re-sets the floor. Add this method after `ShowAndFocus()`:

```csharp
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        MinimumSize = new Size(Theme.Scale(860, e.DeviceDpiNew), Theme.Scale(600, e.DeviceDpiNew));
    }
```

- [ ] **Step 3: Build and run the full suite**

Run: `dotnet build C:\Dev\Projects\GammaKey\GammaKey.sln`
Expected: 0 warnings, 0 errors.

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj`
Expected: 117 passed.

- [ ] **Step 4: Format gate and commit**

Run: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes`
Expected: exit 0.

```
git -C C:\Dev\Projects\GammaKey add src/GammaKey/UI/MainForm.cs
git -C C:\Dev\Projects\GammaKey commit -m "Main window minimum size is set after the DPI scale pass and re-set on DPI change"
```

- [ ] **Step 5: Report**

In your report, state what you found in Step 1, the content width and height the layout needs at 100 percent (sum the fixed values named above), and that the hand check at 100, 125 and 150 percent is pending for Jordan.

---

### Task 5: Profile editing copies and the dirty check

Pure model work for the working-copy flow. `Profile.Clone()` exists but gives a new `Id` and clears the hotkey, which is right for Duplicate and wrong for an editor working copy.

**Files:**
- Modify: `src/GammaKey/Models/Profile.cs:50-57`
- Create: `tests/GammaKey.Tests/ProfileTests.cs`

**Interfaces:**
- Produces on `Profile`:
  - `public Profile CopyForEditing()`: same `Id`, `Name`, `Hotkey`, a new `MonitorTarget` with the same `Mode` and `MonitorName`, same `Values`.
  - `public void CopyContentFrom(Profile source)`: copies `Name`, `Hotkey`, `Target` (new instance) and `Values`; leaves `Id` alone.
  - `public bool HasSameContentAs(Profile other)`: true when `Name`, `Hotkey`, `Target.Mode`, `Target.MonitorName` and `Values` all match. `Id` is not compared.

- [ ] **Step 1: Write the failing tests**

Create `tests/GammaKey.Tests/ProfileTests.cs`:

```csharp
using GammaKey.Models;
using Xunit;

namespace GammaKey.Tests;

public class ProfileTests
{
    private static Profile Cave() => new()
    {
        Name = "Cave",
        Hotkey = "Ctrl+F9",
        Target = MonitorTarget.ForMonitor("LG ULTRAGEAR"),
        Values = new ColorValues(1.6, 20, -10, 35, 0, 65) { Temperature = 10, Red = -5, Green = 0, Blue = 5, BlackFloor = 20 },
    };

    [Fact]
    public void CopyForEditingKeepsTheId()
    {
        var saved = Cave();

        var copy = saved.CopyForEditing();

        Assert.Equal(saved.Id, copy.Id);
    }

    [Fact]
    public void CopyForEditingKeepsTheHotkey()
    {
        var copy = Cave().CopyForEditing();

        Assert.Equal("Ctrl+F9", copy.Hotkey);
    }

    [Fact]
    public void CopyForEditingCopiesEveryValue()
    {
        var saved = Cave();

        var copy = saved.CopyForEditing();

        Assert.Equal(saved.Values, copy.Values);
    }

    [Fact]
    public void CopyForEditingGivesTheCopyItsOwnTarget()
    {
        var saved = Cave();

        var copy = saved.CopyForEditing();

        Assert.NotSame(saved.Target, copy.Target);
    }

    [Fact]
    public void IdenticalCopyHasSameContent()
    {
        var saved = Cave();

        Assert.True(saved.CopyForEditing().HasSameContentAs(saved));
    }

    [Fact]
    public void ChangedNameIsDifferentContent()
    {
        var saved = Cave();
        var copy = saved.CopyForEditing();

        copy.Name = "Cave 2";

        Assert.False(copy.HasSameContentAs(saved));
    }

    [Fact]
    public void ChangedHotkeyIsDifferentContent()
    {
        var saved = Cave();
        var copy = saved.CopyForEditing();

        copy.Hotkey = "Ctrl+F10";

        Assert.False(copy.HasSameContentAs(saved));
    }

    [Fact]
    public void ChangedTargetModeIsDifferentContent()
    {
        var saved = Cave();
        var copy = saved.CopyForEditing();

        copy.Target = MonitorTarget.ForAll();

        Assert.False(copy.HasSameContentAs(saved));
    }

    [Fact]
    public void ChangedTargetMonitorIsDifferentContent()
    {
        var saved = Cave();
        var copy = saved.CopyForEditing();

        copy.Target = MonitorTarget.ForMonitor("DELL U2720Q");

        Assert.False(copy.HasSameContentAs(saved));
    }

    [Fact]
    public void ChangedColourValueIsDifferentContent()
    {
        var saved = Cave();
        var copy = saved.CopyForEditing();

        copy.Values = copy.Values with { BlackFloor = 21 };

        Assert.False(copy.HasSameContentAs(saved));
    }

    [Fact]
    public void DifferentIdWithSameFieldsIsStillSameContent()
    {
        var saved = Cave();
        var other = Cave();

        Assert.True(other.HasSameContentAs(saved));
    }

    [Fact]
    public void CopyContentFromReplacesEveryFieldButTheId()
    {
        var saved = Cave();
        var edited = saved.CopyForEditing();
        edited.Name = "Cave 2";
        edited.Hotkey = "Alt+F1";
        edited.Target = MonitorTarget.ForAll();
        edited.Values = ColorValues.Normal;

        saved.CopyContentFrom(edited);

        Assert.Equal((saved.Id, "Cave 2", "Alt+F1", MonitorTarget.All, ColorValues.Normal), (edited.Id, saved.Name, saved.Hotkey, saved.Target.Mode, saved.Values));
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj --filter "FullyQualifiedName~ProfileTests"`
Expected: build error, `Profile` does not contain a definition for `CopyForEditing`.

- [ ] **Step 3: Add the three methods**

In `Profile.cs`, after the existing `Clone()` method (line 57), add:

```csharp

    // The editor works on a copy so Revert is a reload. Unlike Clone, the copy keeps the Id (the
    // active tag and the list row match on it) and the Hotkey (an edit must not clear it).
    public Profile CopyForEditing() => new()
    {
        Id = Id,
        Name = Name,
        Hotkey = Hotkey,
        Target = new MonitorTarget { Mode = Target.Mode, MonitorName = Target.MonitorName },
        Values = Values,
    };

    public void CopyContentFrom(Profile source)
    {
        Name = source.Name;
        Hotkey = source.Hotkey;
        Target = new MonitorTarget { Mode = source.Target.Mode, MonitorName = source.Target.MonitorName };
        Values = source.Values;
    }

    // The editor's dirty check. Id is left out so a working copy compares equal to its source.
    public bool HasSameContentAs(Profile other) =>
        Name == other.Name
        && Hotkey == other.Hotkey
        && Target.Mode == other.Target.Mode
        && Target.MonitorName == other.Target.MonitorName
        && Values == other.Values;
```

- [ ] **Step 4: Run the tests to confirm they pass**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj --filter "FullyQualifiedName~ProfileTests"`
Expected: 12 passed. (Note: `--filter FullyQualifiedName~ProfileTests` also matches nothing else; `ProfileStoreTests` and `ProfileImportTests` do not contain the substring `ProfileTests`.)

- [ ] **Step 5: Format gate and commit**

Run: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes`
Expected: exit 0.

```
git -C C:\Dev\Projects\GammaKey add src/GammaKey/Models/Profile.cs tests/GammaKey.Tests/ProfileTests.cs
git -C C:\Dev\Projects\GammaKey commit -m "Profile gains CopyForEditing, CopyContentFrom and HasSameContentAs for the editor working copy"
```

---

### Task 6: Editor works on a copy with Apply and Revert

The biggest task. Every edit in the editor writes to a working copy and previews on screen; nothing is saved until Apply. Revert reloads the saved profile.

**Files:**
- Modify: `src/GammaKey/UI/Theme.cs` (add `StylePrimary`)
- Modify: `src/GammaKey/UI/MainForm.cs` (many places, listed per step)

**Interfaces:**
- Consumes: `Profile.CopyForEditing()`, `Profile.CopyContentFrom(Profile)`, `Profile.HasSameContentAs(Profile)` from Task 5.
- Produces (used by Tasks 7 and 8):
  - `MainForm._saved` (`Profile?`): the selected profile inside `Settings.Profiles`.
  - `MainForm._editing` (`Profile?`): the working copy.
  - `private bool IsDirty` property.
  - `private void ApplyEdits()` and `private void RevertEdits()`.
  - `public static void StylePrimary(Button button)` on `Theme`.

No new unit tests: the behaviour lives in event handlers on a Form that needs a live `AppController`. The pure parts were tested in Task 5.

- [ ] **Step 1: Add `Theme.StylePrimary`**

In `Theme.cs`, after `Apply(Control root)` (after line 71), add:

```csharp

    // An accent-filled text button, the text counterpart of GkIconButton.IsPrimary. Call after
    // Apply(root), which paints every Button with the standard surface first. Disabled falls back
    // to the standard look so a grey accent block never appears.
    public static void StylePrimary(Button button)
    {
        void Paint()
        {
            bool on = button.Enabled;
            button.BackColor = on ? Accent : Surface;
            button.ForeColor = on ? Background : TextDim;
            button.FlatAppearance.BorderColor = on ? Accent : Line;
            button.FlatAppearance.MouseOverBackColor = on ? AccentDim : Raised;
        }
        button.FlatStyle = FlatStyle.Flat;
        button.EnabledChanged += (_, _) => Paint();
        Paint();
    }
```

- [ ] **Step 2: Add the fields and buttons to `MainForm`**

Replace lines 48-50:

```csharp
    private Profile? _editing;
    private string? _activeProfileId;
    private bool _loading;
```

with:

```csharp
    // _saved is the profile inside Settings.Profiles; _editing is its working copy. Edits and
    // previews touch _editing only, so Revert is a fresh copy and nothing is written until Apply.
    private Profile? _saved;
    private Profile? _editing;
    private readonly Button _applyButton = new() { Text = "Apply", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = Padding.Empty, Enabled = false };
    private readonly Button _revertButton = new() { Text = "Revert", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = new Padding(0, 0, Theme.Space2, 0), Enabled = false };
    private string? _activeProfileId;
    private bool _loading;
```

- [ ] **Step 3: Place the button row under Hotkey and Monitors**

In `BuildLayout`, after `profileGroup.Content.Controls.Add(two);` (line 182) and before `editor.Controls.Add(profileGroup);`, insert:

```csharp
        // Right aligned: RightToLeft flow puts the first control at the right edge, so Apply
        // sits at the far right with Revert to its left. GkGroup widens the row to the group.
        var editActions = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = false, Height = Theme.ControlHeight + 4, Margin = new Padding(0, Theme.Space1, 0, 0), BackColor = Theme.Panel };
        editActions.Controls.Add(_applyButton);
        editActions.Controls.Add(_revertButton);
        _applyButton.Click += (_, _) => ApplyEdits();
        _revertButton.Click += (_, _) => RevertEdits();
        profileGroup.Content.Controls.Add(editActions);
```

In the constructor, after `Theme.Apply(this);` (line 90) add:

```csharp
        Theme.StylePrimary(_applyButton);
```

- [ ] **Step 4: Rewire the name, hotkey and target handlers**

Replace lines 228-230:

```csharp
        _nameBox.TextChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Name = _nameBox.Text; _heading.Text = _nameBox.Text; _controller.Save(); } };
        _hotkeyBox.ComboChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Hotkey = _hotkeyBox.Combo; SaveSettingsAndRegister(_hotkeyBox); } };
        _targetBox.SelectedIndexChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Target = TargetFromCombo(); _controller.Save(); _status.Text = _controller.ApplyProfile(_editing).Note ?? ""; } };
```

with:

```csharp
        _nameBox.TextChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Name = _nameBox.Text; RefreshDirty(); } };
        _hotkeyBox.ComboChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Hotkey = _hotkeyBox.Combo; _hotkeyBox.IsRefused = false; RefreshDirty(); } };
        // A new target previews the working copy there; it is saved and made active only by Apply.
        _targetBox.SelectedIndexChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Target = TargetFromCombo(); _status.Text = _controller.Preview(_editing.Values, _editing.Target).Note ?? ""; RefreshDirty(); } };
```

- [ ] **Step 5: Sliders preview only**

In `Slider(...)`, replace lines 283-305 (both `ValueChanged` handlers and the four save hooks) with:

```csharp
        bar.ValueChanged += (_, _) =>
        {
            if (_loading) return;
            _loading = true;
            number.Value = isGamma ? bar.Value / 100m : bar.Value;
            _loading = false;
            PushValuesToProfileAndPreview();
        };
        number.ValueChanged += (_, _) =>
        {
            if (_loading) return;
            _loading = true;
            bar.Value = isGamma ? (int)Math.Round(number.Value * 100) : (int)number.Value;
            _loading = false;
            PushValuesToProfileAndPreview();
        };
```

Delete the `SaveIfEditing()` method (lines 467-470).

Replace `PushValuesToProfileAndPreview` (lines 459-465) with:

```csharp
    private void PushValuesToProfileAndPreview()
    {
        if (_editing is null) return;
        _editing.Values = CurrentValues();
        var result = _controller.Preview(_editing.Values, _editing.Target);
        _status.Text = result.Note ?? "";
        RefreshDirty();
    }
```

- [ ] **Step 6: Load the working copy and split out `ShowEditing`**

Replace `LoadSelectedProfile` (lines 414-448) with:

```csharp
    private void LoadSelectedProfile(bool apply = true)
    {
        if (_loading) return;
        var index = _profileList.SelectedIndex;
        if (index < 0 || index >= _controller.Settings.Profiles.Count) { ClearEditor(); return; }
        _saved = _controller.Settings.Profiles[index];
        _editing = _saved.CopyForEditing();
        SetEditorEnabled(true);
        _activeTag.Visible = _editing.Id == _activeProfileId;
        ShowEditing();
        _hotkeyBox.IsRefused = false;
        RefreshDirty();
        if (!apply) return;
        // Clicking a profile is the same as pressing its hotkey: the screen changes at once.
        var result = _controller.ApplyProfile(_saved);
        _status.Text = result.Note ?? "";
    }

    // Pushes the working copy into every editor control without firing their change handlers.
    private void ShowEditing()
    {
        if (_editing is null) return;
        _loading = true;
        _nameBox.Text = _editing.Name;
        _hotkeyBox.Combo = _editing.Hotkey;
        SelectTargetInCombo(_editing.Target);
        var v = _editing.Values;
        _gamma.Value = (int)Math.Round(v.Gamma * 100);
        _brightness.Value = v.Brightness;
        _contrast.Value = v.Contrast;
        _shadows.Value = v.Shadows;
        _highlights.Value = v.Highlights;
        _vibrance.Value = Math.Clamp(v.Vibrance, 50, 100);
        _temperature.Value = v.Temperature;
        _red.Value = v.Red;
        _green.Value = v.Green;
        _blue.Value = v.Blue;
        _blackFloor.Value = v.BlackFloor;
        foreach (var (bar, number) in _numbers)
            number.Value = ReferenceEquals(bar, _gamma) ? bar.Value / 100m : bar.Value;
        _loading = false;
    }

    private void ClearEditor()
    {
        _saved = null;
        _editing = null;
        SetEditorEnabled(false);
        RefreshDirty();
    }

    private bool IsDirty => _saved is not null && _editing is not null && !_editing.HasSameContentAs(_saved);

    // The heading, Apply and Revert all follow the dirty state, so they change in one place.
    private void RefreshDirty()
    {
        bool dirty = IsDirty;
        _applyButton.Enabled = dirty;
        _revertButton.Enabled = dirty;
        _heading.Text = _editing is null ? "No profile selected" : (dirty ? _editing.Name + " *" : _editing.Name);
    }

    private void ApplyEdits()
    {
        if (_saved is null || _editing is null) return;
        _saved.CopyContentFrom(_editing);
        SaveSettingsAndRegister(_hotkeyBox);
        var result = _controller.ApplyProfile(_saved);
        if (result.Note is not null) _status.Text = string.Join(" ", new[] { _status.Text, result.Note }.Where(s => s.Length > 0));
        RefreshDirty();
    }

    private void RevertEdits()
    {
        if (_saved is null) return;
        _editing = _saved.CopyForEditing();
        ShowEditing();
        _hotkeyBox.IsRefused = false;
        // The screen followed the working copy while it was edited; put the saved look back.
        _status.Text = _controller.Preview(_saved.Values, _saved.Target).Note ?? "";
        RefreshDirty();
    }
```

In `SetEditorEnabled`, delete the line `if (!on) { _heading.Text = "No profile selected"; _activeTag.Visible = false; }` and replace it with:

```csharp
        if (!on) _activeTag.Visible = false;
```

In the constructor, replace `SetEditorEnabled(_editing is not null);` (line 105) with:

```csharp
        SetEditorEnabled(_editing is not null);
        RefreshDirty();
```

- [ ] **Step 7: Duplicate and Delete act on the saved profile**

Replace `DuplicateProfile` and `DeleteProfile` (lines 498-524) with:

```csharp
    private void DuplicateProfile()
    {
        if (_saved is null) return;
        var copy = _saved.Clone();
        copy.Name = _saved.Name + " copy";
        _controller.Settings.Profiles.Add(copy);
        SaveSettingsAndRegister();
        _profileList.SelectedIndex = _controller.Settings.Profiles.Count - 1;
    }

    private void DeleteProfile()
    {
        if (_saved is null) return;
        var index = _controller.Settings.Profiles.IndexOf(_saved);
        _controller.Settings.Profiles.Remove(_saved);
        SaveSettingsAndRegister();
        if (_profileList.Items.Count > 0)
        {
            // The save refreshes the list and keeps the old index. When the new index is the
            // same, the list raises no SelectedIndexChanged, so the editor is rebound here.
            _loading = true;
            _profileList.SelectedIndex = Math.Min(index, _profileList.Items.Count - 1);
            _loading = false;
            LoadSelectedProfile();
        }
        else ClearEditor();
    }
```

- [ ] **Step 8: Update the stale comment in `GkHotkeyBox`**

Lines 36-37 of `GkHotkeyBox.cs` say "Every edit auto-saves, so Escape must not clear a hotkey". Replace the two comment lines with:

```csharp
    // Escape must not clear a hotkey (Revert is the way back); it goes to the form so a dialog's
    // CancelButton still closes it.
```

- [ ] **Step 9: Build and run the full suite**

Run: `dotnet build C:\Dev\Projects\GammaKey\GammaKey.sln`
Expected: 0 warnings, 0 errors. If the compiler reports an unused `SaveIfEditing` or a missing symbol, fix the reference; do not leave dead code.

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj`
Expected: 129 passed.

- [ ] **Step 10: Self-check against the spec, then format gate and commit**

Walk `MainForm.cs` once and confirm: no call to `_controller.Save()` remains outside `SaveSettingsAndRegister`, `AddProfile`, `DuplicateProfile`, `DeleteProfile`, `ImportProfiles`; `_editing` is never placed in `Settings.Profiles`; `ApplyProfile` is called with `_saved`, never `_editing`.

Run: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes`
Expected: exit 0.

```
git -C C:\Dev\Projects\GammaKey add src/GammaKey/UI/MainForm.cs src/GammaKey/UI/Theme.cs src/GammaKey/UI/GkHotkeyBox.cs
git -C C:\Dev\Projects\GammaKey commit -m "Editor edits a working copy with live preview; Apply saves and applies, Revert reloads the saved profile"
```

---

### Task 7: Unsaved-changes guard

Before any action that leaves the current profile, a dirty editor asks: "Save and Apply" or "Discard changes". Either answer completes the action.

**Files:**
- Create: `src/GammaKey/UI/UnsavedChangesDialog.cs`
- Modify: `src/GammaKey/UI/MainForm.cs` (list selection, Add, Duplicate, Delete, Import, Export, Settings, close)
- Modify: `src/GammaKey/UI/TrayIcon.cs:88`
- Create: `tests/GammaKey.Tests/UnsavedChangesDialogTests.cs`

**Interfaces:**
- Consumes: `IsDirty`, `ApplyEdits()`, `RevertEdits()`, `_editing` from Task 6; `Theme.StylePrimary` from Task 6.
- Produces:
  - `public enum UnsavedChoice { SaveAndApply, Discard }`
  - `public sealed class UnsavedChangesDialog : Form` with `public static UnsavedChoice Ask(IWin32Window? owner, string profileName)` and `public static UnsavedChoice ChoiceFrom(DialogResult result)`.
  - `public void ResolveUnsavedEdits()` on `MainForm` (public so `TrayIcon` can call it).

- [ ] **Step 1: Write the failing tests**

Create `tests/GammaKey.Tests/UnsavedChangesDialogTests.cs`:

```csharp
using System.Windows.Forms;
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class UnsavedChangesDialogTests
{
    [Fact]
    public void OkMeansSaveAndApply()
    {
        Assert.Equal(UnsavedChoice.SaveAndApply, UnsavedChangesDialog.ChoiceFrom(DialogResult.OK));
    }

    [Fact]
    public void CancelMeansDiscard()
    {
        // Escape and the close box both end ShowDialog with Cancel.
        Assert.Equal(UnsavedChoice.Discard, UnsavedChangesDialog.ChoiceFrom(DialogResult.Cancel));
    }

    [Fact]
    public void DialogNamesTheProfileInItsMessage()
    {
        using var dialog = new UnsavedChangesDialog("Cave");

        Assert.Equal("Cave has unsaved changes.", dialog.Message);
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj --filter "FullyQualifiedName~UnsavedChangesDialogTests"`
Expected: build error, `UnsavedChangesDialog` could not be found.

- [ ] **Step 3: Create the dialog**

Create `src/GammaKey/UI/UnsavedChangesDialog.cs`:

```csharp
namespace GammaKey.UI;

public enum UnsavedChoice { SaveAndApply, Discard }

// Two choices and no Cancel: either one completes the action that raised the dialog, so the
// user is never left in a dead end. Escape and the close box count as Discard changes.
public sealed class UnsavedChangesDialog : Form
{
    private readonly Label _message = new() { AutoSize = true, Font = Theme.Label, ForeColor = Theme.TextPrimary, Margin = new Padding(0, 0, 0, Theme.Space4), MaximumSize = new Size(380, 0) };

    public UnsavedChangesDialog(string profileName)
    {
        // Setting AutoScaleMode or ClientSize on a live form runs the DPI scale pass at once, on
        // an empty form. Suspend first so the pass runs once, over the whole tree, at ResumeLayout.
        SuspendLayout();
        Text = "Unsaved changes";
        WindowChrome.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(420, 120);
        _message.Text = $"{profileName} has unsaved changes.";

        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(Theme.Space4), BackColor = Theme.Background };
        Controls.Add(stack);
        stack.Controls.Add(_message);

        var buttons = new FlowLayoutPanel { AutoSize = false, Height = Theme.ControlHeight, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty, BackColor = Theme.Background };
        var save = new Button { Text = "Save and Apply", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = Padding.Empty, DialogResult = DialogResult.OK };
        var discard = new Button { Text = "Discard changes", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = new Padding(0, 0, Theme.Space2, 0), DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save);
        buttons.Controls.Add(discard);
        stack.Controls.Add(buttons);
        // The button row stretches to the stack width; a TopDown FlowLayoutPanel does not do that itself.
        stack.Layout += (_, _) => buttons.Width = stack.ClientSize.Width - stack.Padding.Horizontal;

        AcceptButton = save;
        CancelButton = discard;
        Theme.Apply(this);
        Theme.StylePrimary(save);
        ResumeLayout(true);
    }

    public string Message => _message.Text;

    public static UnsavedChoice ChoiceFrom(DialogResult result) => result == DialogResult.OK ? UnsavedChoice.SaveAndApply : UnsavedChoice.Discard;

    // A null owner (the main window is hidden in the tray) centres the dialog on the screen.
    public static UnsavedChoice Ask(IWin32Window? owner, string profileName)
    {
        using var dialog = new UnsavedChangesDialog(profileName);
        if (owner is null) dialog.StartPosition = FormStartPosition.CenterScreen;
        return ChoiceFrom(owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner));
    }
}
```

- [ ] **Step 4: Run the tests to confirm they pass**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj --filter "FullyQualifiedName~UnsavedChangesDialogTests"`
Expected: 3 passed.

- [ ] **Step 5: Add the guard to `MainForm`**

Add this method after `ShowAndFocus()`:

```csharp
    // Runs before any action that leaves the current profile. Both answers finish the action:
    // Save and Apply writes the edits first, Discard changes drops them. Hotkeys never call this.
    public void ResolveUnsavedEdits()
    {
        if (!IsDirty || _editing is null) return;
        var choice = UnsavedChangesDialog.Ask(Visible ? this : null, _editing.Name);
        if (choice == UnsavedChoice.SaveAndApply) ApplyEdits();
        else RevertEdits();
    }
```

Change the list selection handler (`_profileList.SelectedIndexChanged += (_, _) => LoadSelectedProfile();`) to:

```csharp
        // The list has already moved, but _saved and _editing still point at the previous
        // profile, so the dialog's choice lands on that one before the new row loads.
        _profileList.SelectedIndexChanged += (_, _) => { if (_loading) return; ResolveUnsavedEdits(); LoadSelectedProfile(); };
```

Add `ResolveUnsavedEdits();` as the first line of `AddProfile()`, `DuplicateProfile()`, `DeleteProfile()`, `ImportProfiles()`, `ExportProfiles()` and `OpenSettings()`. In `DuplicateProfile` and `DeleteProfile` it goes before the `if (_saved is null) return;` line.

Replace `OnFormClosing` with:

```csharp
    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // Closing the window hides to the tray; Quit in the tray menu ends the app.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            ResolveUnsavedEdits();
            e.Cancel = true;
            Hide();
        }
    }
```

- [ ] **Step 6: Quit from the tray runs the guard**

In `TrayIcon.cs`, replace line 88:

```csharp
        _menu.Items.Add("Quit", null, (_, _) => Application.Exit());
```

with:

```csharp
        // Application.Exit closes the form with CloseReason.ApplicationExitCall, which skips
        // OnFormClosing's guard, so the guard runs here first.
        _menu.Items.Add("Quit", null, (_, _) => { _mainForm.ResolveUnsavedEdits(); Application.Exit(); });
```

- [ ] **Step 7: Build and run the full suite**

Run: `dotnet build C:\Dev\Projects\GammaKey\GammaKey.sln`
Expected: 0 warnings, 0 errors.

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj`
Expected: 132 passed.

- [ ] **Step 8: Format gate and commit**

Run: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes`
Expected: exit 0.

```
git -C C:\Dev\Projects\GammaKey add src/GammaKey/UI/UnsavedChangesDialog.cs src/GammaKey/UI/MainForm.cs src/GammaKey/UI/TrayIcon.cs tests/GammaKey.Tests/UnsavedChangesDialogTests.cs
git -C C:\Dev\Projects\GammaKey commit -m "Unsaved edits ask Save and Apply or Discard changes before switching profile, list actions, close and Quit"
```

---

### Task 8: Export one profile or all

Export first asks which to write. "This profile only" writes an `AppSettings` holding the saved copy of the selected profile and default settings, so Import reads it with no change.

**Files:**
- Create: `src/GammaKey/Storage/ProfileExport.cs`
- Create: `src/GammaKey/UI/ExportChoiceDialog.cs`
- Modify: `src/GammaKey/UI/MainForm.cs` (`ExportProfiles`)
- Create: `tests/GammaKey.Tests/ProfileExportTests.cs`

**Interfaces:**
- Consumes: `_saved` from Task 6; `ResolveUnsavedEdits()` from Task 7 (already the first line of `ExportProfiles`); `Theme.StylePrimary` from Task 6; `ProfileStore.JsonOptions` and `ProfileStore.TryParse`.
- Produces:
  - `public static class ProfileExport` with `public static AppSettings Single(Profile profile)` and `public static string SuggestedFileName(string profileName)`.
  - `public enum ExportScope { Selected, All }`
  - `public sealed class ExportChoiceDialog : Form` with `public static ExportScope? Ask(IWin32Window owner, bool hasSelection)`; null means the user closed the dialog without choosing.

- [ ] **Step 1: Write the failing tests**

Create `tests/GammaKey.Tests/ProfileExportTests.cs`:

```csharp
using System.Text.Json;
using GammaKey.Models;
using GammaKey.Storage;
using Xunit;

namespace GammaKey.Tests;

public class ProfileExportTests
{
    [Fact]
    public void SingleExportHoldsExactlyThatProfile()
    {
        var profile = new Profile { Name = "Cave", Hotkey = "Ctrl+F9" };

        var exported = ProfileExport.Single(profile);

        Assert.Equal(new[] { "Cave" }, exported.Profiles.Select(p => p.Name));
    }

    [Fact]
    public void SingleExportRoundTripsThroughTryParse()
    {
        var profile = new Profile { Name = "Cave", Hotkey = "Ctrl+F9", Values = new ColorValues(1.6, 20, -10, 35, 0, 65) { BlackFloor = 20 } };

        var json = JsonSerializer.Serialize(ProfileExport.Single(profile), ProfileStore.JsonOptions);
        var parsed = ProfileStore.TryParse(json);

        Assert.Equal(profile.Values, Assert.Single(parsed!.Profiles).Values);
    }

    [Fact]
    public void SingleExportUsesDefaultSettings()
    {
        var exported = ProfileExport.Single(new Profile());

        Assert.Equal("", exported.ResetHotkey);
    }

    [Fact]
    public void SuggestedFileNameIsTheProfileNameWithJson()
    {
        Assert.Equal("Cave.json", ProfileExport.SuggestedFileName("Cave"));
    }

    [Fact]
    public void SuggestedFileNameDropsCharactersWindowsRefuses()
    {
        Assert.Equal("Night Mode.json", ProfileExport.SuggestedFileName("Night: Mode?"));
    }

    [Fact]
    public void SuggestedFileNameFallsBackWhenNothingIsLeft()
    {
        Assert.Equal("profile.json", ProfileExport.SuggestedFileName("<>:\"/\\|?*"));
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj --filter "FullyQualifiedName~ProfileExportTests"`
Expected: build error, `ProfileExport` could not be found.

- [ ] **Step 3: Create `ProfileExport`**

Create `src/GammaKey/Storage/ProfileExport.cs`:

```csharp
using GammaKey.Models;

namespace GammaKey.Storage;

public static class ProfileExport
{
    // Same shape as profiles.json with one profile and default settings, so Import reads it as is.
    public static AppSettings Single(Profile profile) => new() { Profiles = { profile } };

    public static string SuggestedFileName(string profileName)
    {
        var kept = string.Concat(profileName.Split(Path.GetInvalidFileNameChars())).Trim();
        return (kept.Length == 0 ? "profile" : kept) + ".json";
    }
}
```

- [ ] **Step 4: Run the tests to confirm they pass**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj --filter "FullyQualifiedName~ProfileExportTests"`
Expected: 6 passed.

- [ ] **Step 5: Create the choice dialog**

Create `src/GammaKey/UI/ExportChoiceDialog.cs`:

```csharp
namespace GammaKey.UI;

public enum ExportScope { Selected, All }

public sealed class ExportChoiceDialog : Form
{
    private ExportScope? _choice;

    public ExportChoiceDialog(bool hasSelection)
    {
        // Setting AutoScaleMode or ClientSize on a live form runs the DPI scale pass at once, on
        // an empty form. Suspend first so the pass runs once, over the whole tree, at ResumeLayout.
        SuspendLayout();
        Text = "Export";
        WindowChrome.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(420, 120);

        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(Theme.Space4), BackColor = Theme.Background };
        Controls.Add(stack);
        stack.Controls.Add(new Label { Text = "What do you want to export?", AutoSize = true, Font = Theme.Label, ForeColor = Theme.TextPrimary, Margin = new Padding(0, 0, 0, Theme.Space4) });

        var buttons = new FlowLayoutPanel { AutoSize = false, Height = Theme.ControlHeight, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty, BackColor = Theme.Background };
        var selected = new Button { Text = "This profile only", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = Padding.Empty, Enabled = hasSelection };
        var all = new Button { Text = "All profiles", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = new Padding(0, 0, Theme.Space2, 0) };
        selected.Click += (_, _) => { _choice = ExportScope.Selected; DialogResult = DialogResult.OK; };
        all.Click += (_, _) => { _choice = ExportScope.All; DialogResult = DialogResult.OK; };
        buttons.Controls.Add(selected);
        buttons.Controls.Add(all);
        stack.Controls.Add(buttons);
        stack.Layout += (_, _) => buttons.Width = stack.ClientSize.Width - stack.Padding.Horizontal;

        // The single export is the default when a profile is selected; otherwise All is.
        AcceptButton = hasSelection ? selected : all;
        Theme.Apply(this);
        Theme.StylePrimary(hasSelection ? selected : all);
        ResumeLayout(true);
    }

    // Null when the user pressed Escape or the close box: nothing is exported.
    public static ExportScope? Ask(IWin32Window owner, bool hasSelection)
    {
        using var dialog = new ExportChoiceDialog(hasSelection);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._choice : null;
    }
}
```

- [ ] **Step 6: Rewrite `ExportProfiles`**

Replace the `ExportProfiles` method in `MainForm.cs` with:

```csharp
    private void ExportProfiles()
    {
        ResolveUnsavedEdits();
        var scope = ExportChoiceDialog.Ask(this, hasSelection: _saved is not null);
        if (scope is null) return;
        bool single = scope == ExportScope.Selected && _saved is not null;
        // The saved profile goes out, never the working copy; the guard above has settled it.
        AppSettings payload = single ? ProfileExport.Single(_saved!) : _controller.Settings;
        string fileName = single ? ProfileExport.SuggestedFileName(_saved!.Name) : "GammaKey-profiles.json";
        using var dialog = new SaveFileDialog { Filter = ProfileFileFilter, FileName = fileName, DefaultExt = "json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            // Same shape as profiles.json, so an export and a copied profiles.json both import.
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(payload, ProfileStore.JsonOptions));
            int count = payload.Profiles.Count;
            _status.Text = count == 1 ? "Exported 1 profile." : $"Exported {count} profiles.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status.Text = "Could not write that file.";
        }
    }
```

- [ ] **Step 7: Build and run the full suite**

Run: `dotnet build C:\Dev\Projects\GammaKey\GammaKey.sln`
Expected: 0 warnings, 0 errors.

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests\GammaKey.Tests.csproj`
Expected: 138 passed.

- [ ] **Step 8: Format gate and commit**

Run: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes`
Expected: exit 0.

```
git -C C:\Dev\Projects\GammaKey add src/GammaKey/Storage/ProfileExport.cs src/GammaKey/UI/ExportChoiceDialog.cs src/GammaKey/UI/MainForm.cs tests/GammaKey.Tests/ProfileExportTests.cs
git -C C:\Dev\Projects\GammaKey commit -m "Export asks for this profile only or all profiles; a single export is a one-profile settings file"
```

---

## Hand checks for Jordan (after the branch is published)

Gaming PC and laptop, at 100, 125 and 150 percent where the scale matters:

1. Drag the window to its smallest: it stops before content is cut, at every scale.
2. Every profile row shows both its name and its summary line, at every scale.
3. Gamma slider: "1.00" sits under the origin mark and the thumb when the value is 1.00.
4. Every profile row with a hotkey shows its chip text (NumPad1, NumPad2 and so on).
5. Drag a slider: the screen changes, the heading gains " *", Apply and Revert light up, nothing is saved (check profiles.json timestamp).
6. Revert: controls and screen go back to the saved profile.
7. Apply: saved, hotkey registered, screen matches, list row updates, " *" gone.
8. Edit, then click another row: dialog appears; try both buttons; the new row loads either way.
9. Edit, then Add, Duplicate, Delete, Import, Export, Settings, and the close box: dialog appears each time.
10. Edit, hide to tray, Quit from the tray: dialog appears centred on screen.
11. Export "This profile only": file named after the profile; import it on the other machine.
12. Export "All profiles": status says "Exported N profiles."
