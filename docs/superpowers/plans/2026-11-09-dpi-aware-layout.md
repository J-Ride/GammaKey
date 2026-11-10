# DPI-aware layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the main window, the Settings dialog and the toast lay out and paint correctly at 150% display scaling without changing anything at 100%.

**Architecture:** Both forms already use `AutoScaleMode.Dpi`, but the WinForms scale pass fires the moment `AutoScaleMode` is set, while the form is still empty, so no child control is ever scaled. Each form constructor is wrapped in `SuspendLayout()` first and one `ResumeLayout(true)` after the tree is built, so the pass runs once over every control. The pass scales bounds, Padding, Margin, MinimumSize, MaximumSize and absolute TableLayoutPanel styles. It does not scale owner-drawn paint constants, ComboBox and ListBox `ItemHeight`, or a borderless form without auto-scale, so those read `DeviceDpi` through one helper, `Theme.Scale`.

**Tech Stack:** C# .NET 8 WinForms x64, xUnit. No new dependencies.

## Facts verified 2026-11-09 (probe console app, factor 1.5)

- `AutoScaleMode = Dpi` set on an unsuspended form runs the scale pass immediately and clears its "scaling needed" flag. Children added later are never scaled. This is the whole bug.
- With `SuspendLayout()` before the `AutoScale*` properties and `ResumeLayout(true)` after the tree is built, the pass scales: form size and MinimumSize, child Height/Width, Padding, Margin, Button MinimumSize, Label MaximumSize, TableLayoutPanel Absolute row and column styles.
- The pass does not scale `ComboBox.ItemHeight` or `ListBox.ItemHeight`.
- A control added after `ResumeLayout(true)` is not scaled. Every control must exist before it.
- At 96 dpi the factor is 1.0 and the pass changes nothing, so 100% layout is untouched.
- `Control.DeviceDpi` on a brand-new control equals the primary monitor's DPI at process start. `Graphics.DpiX` inside `OnPaint` equals the window's DPI.

## Global Constraints

- Repo `C:\Dev\Projects\GammaKey`. Run every command from the repo root.
- Branch `feature/dpi-layout`. Create it from `origin/master` once PR #5 (feature/v1-fixes) is merged. If PR #5 is not merged yet, create it from `origin/feature/v1-fixes` after `git fetch`.
- Build and test commands, always with these flags (a plain `dotnet build | tail` hangs past the tool timeout):
  - Build: `timeout 300 dotnet build -nodeReuse:false -p:UseSharedCompilation=false -v q < /dev/null`
  - Test: `timeout 500 dotnet test tests/GammaKey.Tests -nodeReuse:false -p:UseSharedCompilation=false -v q < /dev/null`
  - Format: `timeout 300 dotnet format --verify-no-changes -v q < /dev/null`
- 0 warnings, 0 errors, all tests green at every commit. 84 tests pass at the start.
- Only the files named in each task change. `Theme.Radius` corner radii stay unscaled on purpose (a 4 px corner reads fine at any DPI).
- `Theme.Scale(int logical, float dpi)` is the only scaling helper. Controls pass `DeviceDpi`; static paint helpers that only have a `Graphics` pass `g.DpiX`.
- Commit messages: descriptive subject and body. No attribution trailer of any kind. Write the message to a file and use `git commit -F <file>`. Stage by path.
- Comments explain WHY only. No dead code. `using Color = System.Drawing.Color;` stays after the namespace line where it already exists (`GammaKey.Color` is a namespace).
- Do not touch Jordan's uncommitted `docs/mockups/gammakey-ui-mockup-v2.html` or the untracked colour-balance spec.

---

### Task 1: Theme.Scale helper

**Files:**
- Modify: `src/GammaKey/UI/Theme.cs` (after `SliderThumbSize`, line 48)
- Test: `tests/GammaKey.Tests/ThemeTests.cs`

**Interfaces:**
- Produces: `public static int Theme.Scale(int logical, float dpi)` returning `logical * dpi / 96` rounded half away from zero. Every later task calls it.

- [ ] **Step 1: Write the failing tests**

Append inside the `ThemeTests` class in `tests/GammaKey.Tests/ThemeTests.cs`:

```csharp
    [Fact]
    public void ScaleIsIdentityAt96Dpi()
    {
        Assert.Equal(30, Theme.Scale(30, 96f));
    }

    [Fact]
    public void ScaleGrowsByHalfAt144Dpi()
    {
        Assert.Equal(45, Theme.Scale(30, 144f));
    }

    [Fact]
    public void ScaleRoundsHalvesAwayFromZero()
    {
        // 7 * 1.5 = 10.5; banker's rounding would give 10 and shave a pixel off every odd size.
        Assert.Equal(11, Theme.Scale(7, 144f));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `timeout 500 dotnet test tests/GammaKey.Tests -nodeReuse:false -p:UseSharedCompilation=false -v q < /dev/null`
Expected: build error `'Theme' does not contain a definition for 'Scale'`.

- [ ] **Step 3: Add the helper**

In `src/GammaKey/UI/Theme.cs`, after `public const int SliderThumbSize = 14;` add:

```csharp

    // Layout values are authored at 96 dpi. The form's auto-scale pass handles control bounds;
    // owner-drawn paint code and the toast call this for everything else.
    public static int Scale(int logical, float dpi) => (int)Math.Round(logical * dpi / 96f, MidpointRounding.AwayFromZero);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `timeout 500 dotnet test tests/GammaKey.Tests -nodeReuse:false -p:UseSharedCompilation=false -v q < /dev/null`
Expected: `Passed! - Failed: 0, Passed: 87`.

- [ ] **Step 5: Commit**

```bash
git add src/GammaKey/UI/Theme.cs tests/GammaKey.Tests/ThemeTests.cs
printf '%s\n' "Theme.Scale: one helper for DPI-scaled paint values" "" "Layout constants are authored at 96 dpi. The form auto-scale pass covers control bounds; owner-drawn paint and the toast need the same factor by hand. Rounds half away from zero so odd sizes do not lose a pixel at 150%." > .git/COMMIT_MSG
git commit -F .git/COMMIT_MSG
```

---

### Task 2: Forms run the scale pass over the whole tree

**Files:**
- Modify: `src/GammaKey/UI/MainForm.cs:47-96` (constructor)
- Modify: `src/GammaKey/UI/SettingsForm.cs:20-42` (constructor)

**Interfaces:**
- Consumes: nothing new.
- Produces: both forms are fully scaled before any later code reads a control size. `GkChip` (Task 3) relies on the pass calling `ScaleControl` on every chip.

There is no unit test for this task: building a `MainForm` needs an `AppController` that registers hotkeys and touches the display. The behaviour is checked by eye in Task 7, and the probe facts above are the evidence for the change.

- [ ] **Step 1: MainForm constructor**

In `src/GammaKey/UI/MainForm.cs` replace lines 47 to 58 (from `public MainForm(AppController controller)` through `StartPosition = FormStartPosition.CenterScreen;`) with:

```csharp
    public MainForm(AppController controller)
    {
        _controller = controller;
        // Setting AutoScaleMode on a live form runs the DPI scale pass at once, on an empty form.
        // Suspend first so the pass runs once, over the whole tree, at ResumeLayout below.
        SuspendLayout();
        Text = "GammaKey";
        WindowChrome.Apply(this);
        Width = 960;
        Height = 680;
        MinimumSize = new Size(860, 600);
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = TrayIcon.CreateIcon();
        StartPosition = FormStartPosition.CenterScreen;
```

Then replace lines 81 to 82 (`BuildLayout();` and `Theme.Apply(this);`) with:

```csharp
        BuildLayout();
        Theme.Apply(this);
        // Every control now exists; a control added after this line would not be scaled.
        ResumeLayout(true);
```

`LoadMonitorsIntoTarget()`, `RefreshProfileList()` and `RefreshStatus()` stay after the resume: they set chip text, and `GkChip` re-measures its width at the real DPI each time.

- [ ] **Step 2: SettingsForm constructor**

In `src/GammaKey/UI/SettingsForm.cs` replace lines 20 to 36 (from `public SettingsForm(AppController controller)` through `LoadSettingsIntoControls();`) with:

```csharp
    public SettingsForm(AppController controller)
    {
        _controller = controller;
        // Setting AutoScaleMode or ClientSize on a live form runs the DPI scale pass at once, on
        // an empty form. Suspend first so the pass runs once, over the whole tree, at ResumeLayout.
        SuspendLayout();
        Text = "GammaKey settings";
        WindowChrome.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(440, 500);

        BuildLayout();
        Theme.Apply(this);
        ResumeLayout(true);
        LoadSettingsIntoControls();
```

- [ ] **Step 3: Build and test**

Run: `timeout 500 dotnet test tests/GammaKey.Tests -nodeReuse:false -p:UseSharedCompilation=false -v q < /dev/null`
Expected: `Passed! - Failed: 0, Passed: 87`, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add src/GammaKey/UI/MainForm.cs src/GammaKey/UI/SettingsForm.cs
printf '%s\n' "Forms: suspend layout so the DPI scale pass covers every control" "" "AutoScaleMode.Dpi ran its pass the moment it was set, on an empty form, so nothing built afterwards was scaled and text clipped at 150%. SuspendLayout first and one ResumeLayout(true) after BuildLayout and Theme.Apply scales the whole tree once. At 96 dpi the factor is 1 and nothing changes." > .git/COMMIT_MSG
git commit -F .git/COMMIT_MSG
```

---

### Task 3: Controls that size themselves

**Files:**
- Modify: `src/GammaKey/UI/GkChip.cs`
- Modify: `src/GammaKey/UI/GkDropdown.cs:16` (ItemHeight)
- Modify: `src/GammaKey/UI/ProfileList.cs:12` (ItemHeight)
- Test: `tests/GammaKey.Tests/GkChipTests.cs` (create)

**Interfaces:**
- Consumes: `Theme.Scale(int, float)` from Task 1.
- Produces: `GkChip` keeps a device-pixel width through the form's scale pass. `ItemHeight` on the dropdown and the profile list is set in device pixels.

- [ ] **Step 1: Write the failing test**

Create `tests/GammaKey.Tests/GkChipTests.cs`:

```csharp
using System.Drawing;
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class GkChipTests
{
    [Fact]
    public void ScalePassGrowsHeightButKeepsMeasuredWidth()
    {
        var chip = new GkChip { Label = "HDR", Value = "off" };
        int measuredWidth = chip.Width;

        chip.Scale(new SizeF(1.5f, 1.5f));

        Assert.Equal((39, measuredWidth), (chip.Height, chip.Width));
    }
}
```

The width is measured from text at the control's real DPI, so the pass must not multiply it again. The height is a plain 26 px constant and must grow to 39.

- [ ] **Step 2: Run the test to verify it fails**

Run: `timeout 500 dotnet test tests/GammaKey.Tests -nodeReuse:false -p:UseSharedCompilation=false -v q < /dev/null`
Expected: one failure, the width tuple element is 1.5 times `measuredWidth`.

- [ ] **Step 3: GkChip**

Replace the whole of `src/GammaKey/UI/GkChip.cs` with:

```csharp
namespace GammaKey.UI;

public enum DotState { None, Ok, Dim }

public sealed class GkChip : Control
{
    private const int DotSize = 7;
    private string _label = "";
    private string _value = "";
    private DotState _dot;

    public GkChip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Height = 26;
        Margin = new Padding(0, 0, Theme.Space2, 0);
    }

    public string Label { get => _label; set { _label = value; FitWidth(); } }
    public string Value { get => _value; set { _value = value; FitWidth(); } }
    public DotState Dot { get => _dot; set { _dot = value; FitWidth(); } }

    // Width is measured from text at the real DPI, so the form's scale pass must not multiply it
    // again. Let the pass scale Height, then measure the width fresh.
    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        base.ScaleControl(factor, specified & ~BoundsSpecified.Width);
        FitWidth();
    }

    private void FitWidth()
    {
        using var g = CreateGraphics();
        int S(int v) => Theme.Scale(v, DeviceDpi);
        int w = S(Theme.Space2) * 2;
        if (_dot != DotState.None) w += S(DotSize) + S(Theme.Space2);
        if (_label.Length > 0) w += TextRenderer.MeasureText(g, _label, Theme.Caption, Size.Empty, TextFormatFlags.NoPadding).Width + (_value.Length > 0 ? S(Theme.Space1) : 0);
        if (_value.Length > 0) w += TextRenderer.MeasureText(g, _value, Theme.Mono, Size.Empty, TextFormatFlags.NoPadding).Width;
        Width = w;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Panel);
        int S(int v) => Theme.Scale(v, DeviceDpi);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var b = new SolidBrush(Theme.Surface)) Theme.FillRounded(g, b, r, Theme.Radius);
        using (var p = new Pen(Theme.Line)) Theme.DrawRounded(g, p, r, Theme.Radius);
        int x = S(Theme.Space2);
        if (_dot != DotState.None)
        {
            int dotSize = S(DotSize);
            using var dot = new SolidBrush(_dot == DotState.Ok ? Theme.Ok : Theme.TextDim);
            g.FillEllipse(dot, x, (Height - dotSize) / 2, dotSize, dotSize);
            x += dotSize + S(Theme.Space2);
        }
        if (_label.Length > 0)
        {
            var size = TextRenderer.MeasureText(g, _label, Theme.Caption, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, _label, Theme.Caption, new Rectangle(x, 0, size.Width, Height), Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            x += size.Width + S(Theme.Space1);
        }
        if (_value.Length > 0)
            TextRenderer.DrawText(g, _value, Theme.Mono, new Rectangle(x, 0, Width - x, Height), Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
```

- [ ] **Step 4: GkDropdown and ProfileList item heights**

In `src/GammaKey/UI/GkDropdown.cs` replace line 16:

```csharp
        ItemHeight = Theme.ControlHeight - 6;
```

with:

```csharp
        // The form's scale pass does not touch ItemHeight, so set it in device pixels here.
        ItemHeight = Theme.Scale(Theme.ControlHeight - 6, DeviceDpi);
```

In `src/GammaKey/UI/ProfileList.cs` replace line 12:

```csharp
        ItemHeight = RowHeight;
```

with:

```csharp
        // The form's scale pass does not touch ItemHeight, so set it in device pixels here.
        ItemHeight = Theme.Scale(RowHeight, DeviceDpi);
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `timeout 500 dotnet test tests/GammaKey.Tests -nodeReuse:false -p:UseSharedCompilation=false -v q < /dev/null`
Expected: `Passed! - Failed: 0, Passed: 88`, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add src/GammaKey/UI/GkChip.cs src/GammaKey/UI/GkDropdown.cs src/GammaKey/UI/ProfileList.cs tests/GammaKey.Tests/GkChipTests.cs
printf '%s\n' "Chip, dropdown and profile list size themselves in device pixels" "" "GkChip measures its width from text at the real DPI, so the scale pass now leaves Width alone and re-measures after scaling Height. ComboBox and ListBox ItemHeight are not scaled by the pass, so both are set through Theme.Scale." > .git/COMMIT_MSG
git commit -F .git/COMMIT_MSG
```

---

### Task 4: GkToggle and GkGroup paint at DPI

**Files:**
- Modify: `src/GammaKey/UI/GkToggle.cs:42-73` (OnPaint)
- Modify: `src/GammaKey/UI/GkGroup.cs:45-57` (OnPaint)

**Interfaces:**
- Consumes: `Theme.Scale`.
- Produces: nothing new. Constructor sizes and Padding stay in logical pixels because the form's pass scales them.

- [ ] **Step 1: GkToggle.OnPaint**

Replace the `OnPaint` method in `src/GammaKey/UI/GkToggle.cs` (lines 42 to 73) with:

```csharp
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Panel);
        int S(int v) => Theme.Scale(v, DeviceDpi);

        int switchWidth = S(SwitchWidth), switchHeight = S(SwitchHeight), inset = S(3);
        var sw = new Rectangle(Width - switchWidth - 1, (Height - switchHeight) / 2, switchWidth, switchHeight);
        using (var back = new SolidBrush(_checked ? Theme.AccentDim : Theme.Raised)) Theme.FillRounded(g, back, sw, 3);
        using (var border = new Pen(_checked ? Theme.Accent : (Focused || _hover ? Theme.TextMuted : Theme.LineStrong))) Theme.DrawRounded(g, border, sw, 3);
        int knob = switchHeight - 2 * inset;
        var knobRect = new Rectangle(_checked ? sw.Right - knob - inset : sw.Left + inset, sw.Top + inset, knob, knob);
        using (var kb = new SolidBrush(_checked ? Color.White : Theme.TextMuted)) Theme.FillRounded(g, kb, knobRect, 2);

        int textRight = sw.Left - S(Theme.Space3);
        bool hasFlag = !string.IsNullOrEmpty(Flag);
        var flagSize = hasFlag ? TextRenderer.MeasureText(g, Flag, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding) : Size.Empty;
        int flagWidth = hasFlag ? flagSize.Width + S(10) : 0;
        int titleMax = Math.Max(0, hasFlag ? textRight - flagWidth - S(Theme.Space2) : textRight);
        var titleSize = TextRenderer.MeasureText(g, Text, Theme.Label, new Size(titleMax, Height), TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        var titleRect = new Rectangle(0, S(Theme.Space1), titleMax, titleSize.Height);
        TextRenderer.DrawText(g, Text, Theme.Label, titleRect, Enabled ? Theme.TextPrimary : Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        if (hasFlag)
        {
            int flagLeft = Math.Min(titleSize.Width + S(Theme.Space2), textRight - flagWidth);
            var flagRect = new Rectangle(flagLeft, titleRect.Top + 1, flagWidth, flagSize.Height + S(3));
            using var fp = new Pen(Color.FromArgb(115, Theme.Warning));
            Theme.DrawRounded(g, fp, flagRect, 3);
            TextRenderer.DrawText(g, Flag, Theme.MonoSmall, flagRect, Theme.Warning, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        var descRect = new Rectangle(0, titleRect.Bottom + 1, textRight, Height - titleRect.Bottom - 1);
        TextRenderer.DrawText(g, Description, Theme.Caption, descRect, Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }
```

- [ ] **Step 2: GkGroup.OnPaint**

Replace the `OnPaint` method in `src/GammaKey/UI/GkGroup.cs` (lines 45 to 57) with:

```csharp
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var b = new SolidBrush(Theme.Panel)) Theme.FillRounded(g, b, r, Theme.Radius + 1);
        using (var p = new Pen(Theme.Line)) Theme.DrawRounded(g, p, r, Theme.Radius + 1);
        // Padding is scaled by the form's pass; the header band drawn here must match it.
        int S(int v) => Theme.Scale(v, DeviceDpi);
        var head = new Rectangle(S(Theme.Space3), S(Theme.Space2), Width - 2 * S(Theme.Space3), S(HeaderHeight) - S(Theme.Space2));
        TextRenderer.DrawText(g, Title.ToUpperInvariant(), Theme.Eyebrow, head, Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        if (Hint.Length > 0)
            TextRenderer.DrawText(g, Hint, Theme.MonoSmall, head, Theme.TextDim, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
```

- [ ] **Step 3: Build and test**

Run: `timeout 500 dotnet test tests/GammaKey.Tests -nodeReuse:false -p:UseSharedCompilation=false -v q < /dev/null`
Expected: `Passed! - Failed: 0, Passed: 88`, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add src/GammaKey/UI/GkToggle.cs src/GammaKey/UI/GkGroup.cs
printf '%s\n' "GkToggle and GkGroup paint their switch, flag and header at device DPI" "" "Control bounds and Padding now come scaled from the form's pass. The paint constants inside OnPaint were still 96 dpi values, so the switch stayed small and the group header band was shorter than its text at 150%." > .git/COMMIT_MSG
git commit -F .git/COMMIT_MSG
```

---

### Task 5: GkSlider, GkNumber and GkIcons paint at DPI

**Files:**
- Modify: `src/GammaKey/UI/GkSlider.cs:45` (TrackRect) and `:61-109` (OnPaint)
- Modify: `src/GammaKey/UI/GkNumber.cs:45` (SpinRect) and `:47-77` (OnPaint)
- Modify: `src/GammaKey/UI/GkIcons.cs:15-38` (Draw)

**Interfaces:**
- Consumes: `Theme.Scale`.
- Produces: `GkIcons.Draw(Graphics g, GkIcon icon, Rectangle bounds, Color color)` keeps its signature and reads `g.DpiX`. `GkIconButton` needs no change.

- [ ] **Step 1: GkSlider**

In `src/GammaKey/UI/GkSlider.cs` replace line 45:

```csharp
    private Rectangle TrackRect => new(Theme.SliderThumbSize / 2, (TrackRowHeight - Theme.SliderTrackHeight) / 2, Width - Theme.SliderThumbSize, Theme.SliderTrackHeight);
```

with:

```csharp
    private int S(int v) => Theme.Scale(v, DeviceDpi);

    private Rectangle TrackRect => new(S(Theme.SliderThumbSize) / 2, (S(TrackRowHeight) - S(Theme.SliderTrackHeight)) / 2, Width - S(Theme.SliderThumbSize), S(Theme.SliderTrackHeight));
```

In `OnPaint`, replace these three lines:

```csharp
        int half = Theme.SliderThumbSize / 2;
```
```csharp
        var scaleRect = new Rectangle(0, TrackRowHeight, Width, ScaleRowHeight);
```
```csharp
                g.DrawLine(tick, tx, track.Bottom + 2, tx, track.Bottom + 5);
```

with, in the same places:

```csharp
        int half = S(Theme.SliderThumbSize) / 2;
```
```csharp
        var scaleRect = new Rectangle(0, S(TrackRowHeight), Width, S(ScaleRowHeight));
```
```csharp
                g.DrawLine(tick, tx, track.Bottom + S(2), tx, track.Bottom + S(5));
```

The constructor `Height = TrackRowHeight + ScaleRowHeight + Theme.Space1;` stays in logical pixels: the form's pass scales it.

- [ ] **Step 2: GkNumber**

In `src/GammaKey/UI/GkNumber.cs` replace line 45:

```csharp
    private Rectangle SpinRect => new(Width - SpinWidth - 1, 1, SpinWidth, Height - 2);
```

with:

```csharp
    private Rectangle SpinRect => new(Width - Theme.Scale(SpinWidth, DeviceDpi) - 1, 1, Theme.Scale(SpinWidth, DeviceDpi), Height - 2);
```

In `OnPaint`, replace the arrow block:

```csharp
        using (var pen = new Pen(arrow, 1.5f))
        {
            float cx = spin.Left + spin.Width / 2f;
            float uy = upRect.Top + upRect.Height / 2f;
            g.DrawLines(pen, new[] { new PointF(cx - 3, uy + 1.5f), new PointF(cx, uy - 1.5f), new PointF(cx + 3, uy + 1.5f) });
            float dy = downRect.Top + downRect.Height / 2f;
            g.DrawLines(pen, new[] { new PointF(cx - 3, dy - 1.5f), new PointF(cx, dy + 1.5f), new PointF(cx + 3, dy - 1.5f) });
        }
```

with:

```csharp
        float k = DeviceDpi / 96f;
        using (var pen = new Pen(arrow, 1.5f * k))
        {
            float cx = spin.Left + spin.Width / 2f;
            float uy = upRect.Top + upRect.Height / 2f;
            g.DrawLines(pen, new[] { new PointF(cx - 3 * k, uy + 1.5f * k), new PointF(cx, uy - 1.5f * k), new PointF(cx + 3 * k, uy + 1.5f * k) });
            float dy = downRect.Top + downRect.Height / 2f;
            g.DrawLines(pen, new[] { new PointF(cx - 3 * k, dy - 1.5f * k), new PointF(cx, dy + 1.5f * k), new PointF(cx + 3 * k, dy - 1.5f * k) });
        }
```

- [ ] **Step 3: GkIcons.Draw**

In `src/GammaKey/UI/GkIcons.cs` replace lines 15 to 24 (the start of `Draw` through the `PointF P(...)` local function) with:

```csharp
    public static void Draw(Graphics g, GkIcon icon, Rectangle bounds, Color color)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        // Paint-time DPI: the button bounds arrive scaled, the glyph must grow with them.
        float k = g.DpiX / 96f;
        float size = Size * k;
        float x = bounds.Left + (bounds.Width - size) / 2f;
        float y = bounds.Top + (bounds.Height - size) / 2f;
        using var pen = new Pen(color, 2f * k) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        // Glyphs are authored on a 24-unit grid like the SVGs; scale to the device size.
        float s = size / 24f;
        PointF P(float gx, float gy) => new(x + gx * s, y + gy * s);
```

The rest of `Draw` already works in units of `s`, so it needs no change.

- [ ] **Step 4: Build and test**

Run: `timeout 500 dotnet test tests/GammaKey.Tests -nodeReuse:false -p:UseSharedCompilation=false -v q < /dev/null`
Expected: `Passed! - Failed: 0, Passed: 88`, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/GammaKey/UI/GkSlider.cs src/GammaKey/UI/GkNumber.cs src/GammaKey/UI/GkIcons.cs
printf '%s\n' "Slider, number readout and icons paint at device DPI" "" "Track, thumb, scale row, spinner column and the 16 px icon grid were fixed 96 dpi values inside controls whose bounds now arrive scaled. Each reads DeviceDpi (or Graphics.DpiX for the static icon drawer) so the drawing fills the scaled box." > .git/COMMIT_MSG
git commit -F .git/COMMIT_MSG
```

---

### Task 6: Keycap chips, profile rows and dropdown arrow at DPI

**Files:**
- Modify: `src/GammaKey/UI/GkHotkeyBox.cs:66-113` (OnPaint and DrawChip)
- Modify: `src/GammaKey/UI/ProfileList.cs:29-64` (OnDrawItem)
- Modify: `src/GammaKey/UI/GkDropdown.cs:33,60-63` (row text inset and arrow glyph)

**Interfaces:**
- Consumes: `Theme.Scale`.
- Produces: `internal static int GkHotkeyBox.DrawChip(Graphics g, string text, int x, int lineHeight, Color textColor, Color border)`. The new `lineHeight` parameter is the device-pixel height of the line the chip is centred on. `ProfileList` is the only other caller.

- [ ] **Step 1: GkHotkeyBox**

In `src/GammaKey/UI/GkHotkeyBox.cs` replace lines 80 to 93 (from `int x = Theme.Space2;` through the `else` block's closing brace) with:

```csharp
        int S(int v) => Theme.Scale(v, DeviceDpi);
        int x = S(Theme.Space2);
        var parts = Format.HotkeyParts(_combo);
        if (parts.Length == 0)
        {
            TextRenderer.DrawText(g, Focused ? "press a combination…" : "click, then press keys", Theme.Caption, new Rectangle(x, 0, Width - x, Height), Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        else
        {
            Color chipText = !Enabled ? Theme.TextDim : (_isRefused ? Theme.Warning : Theme.TextPrimary);
            foreach (var part in parts)
            {
                x = DrawChip(g, part, x, Height, chipText, _isRefused ? Theme.Warning : Theme.LineStrong) + S(Theme.Space1);
            }
        }
```

Replace line 97 (the hint `DrawText` with `Width - Theme.Space2`) so the right margin scales:

```csharp
            TextRenderer.DrawText(g, hint, Theme.Caption, new Rectangle(0, 0, Width - S(Theme.Space2), Height), _isRefused && !Focused ? Theme.Warning : Theme.TextDim, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
```

Replace the `DrawChip` comment and method (lines 100 to 113) with:

```csharp
    // A keycap: mono text in a box with a 2 px bottom edge, centred on a line of lineHeight device
    // pixels. Shared look with the profile list.
    internal static int DrawChip(Graphics g, string text, int x, int lineHeight, Color textColor, Color border)
    {
        var size = TextRenderer.MeasureText(g, text, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding);
        int h = size.Height + Theme.Scale(6, g.DpiX);
        int w = size.Width + Theme.Scale(12, g.DpiX);
        var chip = new Rectangle(x, (lineHeight - h) / 2 - 1, w, h);
        using (var b = new SolidBrush(Theme.Background)) Theme.FillRounded(g, b, chip, 3);
        using (var p = new Pen(border)) Theme.DrawRounded(g, p, chip, 3);
        using (var bottom = new Pen(border, 2f)) g.DrawLine(bottom, chip.Left + 3, chip.Bottom, chip.Right - 3, chip.Bottom);
        TextRenderer.DrawText(g, text, Theme.MonoSmall, chip, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        return chip.Right;
    }
```

- [ ] **Step 2: ProfileList.OnDrawItem**

Replace the `OnDrawItem` method in `src/GammaKey/UI/ProfileList.cs` (lines 29 to 64) with:

```csharp
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

        var nameRect = new Rectangle(row.Left + S(Theme.Space3), row.Top + S(5), textRight - row.Left - S(Theme.Space3), S(18));
        TextRenderer.DrawText(g, profile.Name, Theme.Label, nameRect, Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        var subRect = new Rectangle(nameRect.Left, nameRect.Bottom, nameRect.Width, S(14));
        TextRenderer.DrawText(g, GammaKey.UI.Format.ProfileSummary(profile), Theme.Caption, subRect, Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        // Chips centre on the row; DrawChip centres on the line height it is given.
        int x = textRight;
        var state = g.Save();
        g.TranslateTransform(0, row.Top);
        foreach (var part in parts)
            x = GkHotkeyBox.DrawChip(g, part, x, row.Height, selected ? Theme.Accent : Theme.TextMuted, selected ? Theme.AccentDim : Theme.Line) + S(Theme.Space1);
        g.Restore(state);
    }
```

- [ ] **Step 3: GkDropdown arrow**

In `src/GammaKey/UI/GkDropdown.cs` replace lines 60 to 63 (from `using var ap = new Pen(...)` to the `g.DrawLines(...)` call) with:

```csharp
        float k = DeviceDpi / 96f;
        using var ap = new Pen(Enabled ? Theme.Accent : Theme.TextDim, 1.5f * k);
        float cx = arrowBox.Left + arrowBox.Width / 2f;
        float cy = Height / 2f - 1;
        g.DrawLines(ap, new[] { new PointF(cx - 4 * k, cy - 2 * k), new PointF(cx, cy + 2 * k), new PointF(cx + 4 * k, cy - 2 * k) });
```

The `Theme.Space2` text inset in `OnDrawItem` (line 33) becomes `Theme.Scale(Theme.Space2, DeviceDpi)` in both places on that line.

- [ ] **Step 4: Build and test**

Run: `timeout 500 dotnet test tests/GammaKey.Tests -nodeReuse:false -p:UseSharedCompilation=false -v q < /dev/null`
Expected: `Passed! - Failed: 0, Passed: 88`, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/GammaKey/UI/GkHotkeyBox.cs src/GammaKey/UI/ProfileList.cs src/GammaKey/UI/GkDropdown.cs
printf '%s\n' "Keycap chips, profile rows and dropdown arrow paint at device DPI" "" "DrawChip takes the line height it centres on instead of assuming Theme.ControlHeight, so the hotkey box and the 44 px profile row both place chips correctly at any DPI. Row text offsets, chip padding and the arrow glyph scale from DeviceDpi." > .git/COMMIT_MSG
git commit -F .git/COMMIT_MSG
```

---

### Task 7: Toast at DPI

**Files:**
- Modify: `src/GammaKey/UI/ToastForm.cs`

**Interfaces:**
- Consumes: `Theme.Scale`.
- Produces: `ToastForm.Show(string title, string detail)` unchanged. Both `TrayIcon` callers stay as they are.

The toast is borderless with no `AutoScaleMode`, so nothing scales it. It is sized from measured text (already device pixels) plus constants that must be scaled by hand.

- [ ] **Step 1: Rewrite the sizing and painting**

In `src/GammaKey/UI/ToastForm.cs` replace lines 10 to 29 (from `private const int Pad = Theme.Space4;` through the end of the constructor) with:

```csharp
    private const int Pad = Theme.Space4;
    private const int Bar = 3;
    private const int Mark = 20;
    private const int BodyHeight = 56;
    private readonly string _title;
    private readonly string _detail;

    private ToastForm(string title, string detail)
    {
        _title = title;
        _detail = detail;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Panel;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        // No AutoScaleMode on a borderless form, so every constant is scaled here by hand.
        using var g = CreateGraphics();
        int titleW = TextRenderer.MeasureText(g, _title, Theme.Title, Size.Empty, TextFormatFlags.NoPadding).Width;
        int detailW = TextRenderer.MeasureText(g, _detail, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding).Width;
        ClientSize = new Size(S(Bar + Pad + Mark + Theme.Space3) + Math.Max(titleW, detailW) + S(Pad), S(BodyHeight));
        _timer.Tick += (_, _) => { _timer.Stop(); Close(); };
    }

    private int S(int v) => Theme.Scale(v, DeviceDpi);
```

Replace line 47 in `OnLoad`:

```csharp
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + 48);
```

with:

```csharp
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + S(48));
```

Replace the `OnPaint` method (lines 50 to 69) with:

```csharp
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float k = DeviceDpi / 96f;
        using (var border = new Pen(Theme.LineStrong)) g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using (var bar = new SolidBrush(Theme.Accent)) g.FillRectangle(bar, 0, 0, S(Bar), Height);
        // Crosshair mark, same drawing as the app icon, authored at 20 px.
        int mark = S(Mark);
        int mx = S(Bar + Pad), my = (Height - mark) / 2;
        using (var pen = new Pen(Theme.Accent, 2f * k))
        {
            g.DrawLine(pen, mx + mark / 2f, my, mx + mark / 2f, my + mark);
            g.DrawLine(pen, mx, my + mark / 2f, mx + mark, my + mark / 2f);
            using var hole = new SolidBrush(Theme.Panel);
            g.FillEllipse(hole, mx + 4 * k, my + 4 * k, 12 * k, 12 * k);
            g.DrawEllipse(pen, mx + 5 * k, my + 5 * k, 10 * k, 10 * k);
        }
        int tx = mx + mark + S(Theme.Space3);
        TextRenderer.DrawText(g, _title, Theme.Title, new Rectangle(tx, S(9), Width - tx - S(Pad), S(22)), Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, _detail, Theme.MonoSmall, new Rectangle(tx, S(32), Width - tx - S(Pad), S(16)), Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.NoPadding);
    }
```

- [ ] **Step 2: Build and test**

Run: `timeout 500 dotnet test tests/GammaKey.Tests -nodeReuse:false -p:UseSharedCompilation=false -v q < /dev/null`
Expected: `Passed! - Failed: 0, Passed: 88`, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/GammaKey/UI/ToastForm.cs
printf '%s\n' "Toast sizes and paints at device DPI" "" "The borderless toast has no AutoScaleMode, so its 56 px body, 20 px mark, text rows and top offset were fixed 96 dpi values while the measured text grew with DPI. Every constant now goes through Theme.Scale." > .git/COMMIT_MSG
git commit -F .git/COMMIT_MSG
```

---

### Task 8: Check by eye at 100% and 150%, then format and graph

**Files:**
- No source changes expected. If a check fails, fix it in the file that owns the drawing and commit with a message that names the symptom.

This task needs a person at the screen: Windows display scaling cannot be changed safely from an agent. The agent builds and launches; Jordan looks.

- [ ] **Step 1: Format gate**

Run: `timeout 300 dotnet format --verify-no-changes -v q < /dev/null`
Expected: exit code 0, no output. If it reports changes, run `timeout 300 dotnet format -v q < /dev/null`, re-run the tests, and commit `style: dotnet format`.

- [ ] **Step 2: Launch at the current scaling**

Run: `timeout 300 dotnet build -nodeReuse:false -p:UseSharedCompilation=false -v q < /dev/null`, then start `src\GammaKey\bin\Debug\net8.0-windows\win-x64\GammaKey.exe`. Open the window from the tray icon by the clock, then open Settings from the rail, then press a profile hotkey so the toast shows.

Checklist for Jordan, at the current Windows scale (Settings > System > Display > Scale):

- Main window: the three status chips show their full text with the dot centred. The Profile group's Hotkey and Monitors rows sit side by side with their inputs as tall as the Name box. Slider tracks, diamond thumbs and scale labels line up with the number readout. Group headers (PROFILE, CURVE, COLOR) are not cut off. The five icon buttons show full glyphs. The Settings and Restore monitor buttons show their full text.
- Settings dialog: every toggle shows its title and its description on two full lines. The ANTI-CHEAT RISK flag sits after the title and does not overlap the switch. The Close and Open profiles folder buttons show their full text. The dialog is not clipped at the bottom.
- Toast: accent bar, crosshair mark, title and mono detail line all inside the box with even margins.

- [ ] **Step 3: Switch scaling and repeat**

Jordan changes Windows scale to the other value (100% if it was 150%, 150% if it was 100%), signs out and back in if Windows asks, then restarts GammaKey and repeats the Step 2 checklist. Both results go in the task report.

- [ ] **Step 4: Refresh the code graph**

Run from the repo root: `graphify update .`
Expected: `graphify-out/graph.json` updated with no errors.

- [ ] **Step 5: Commit the graph if it is tracked**

Run: `git status --short graphify-out`. If files show as modified, commit them:

```bash
git add graphify-out
printf '%s\n' "graph: refresh after DPI-aware layout" > .git/COMMIT_MSG
git commit -F .git/COMMIT_MSG
```

If `git status` shows nothing, `graphify-out` is ignored and there is nothing to commit.

---

## Known limits (deliberate, outside this plan)

- `ItemHeight` on the dropdown and the profile list is set once from the DPI at construction. Dragging the window to a monitor with a different scale leaves those two heights at the old value until the app restarts. Everything else follows `DeviceDpi` live.
- Corner radii stay 4 px at every DPI.
- Settings copy ("1.5 s toast, bottom right" should read "1.2 s toast, top centre") is a separate parked item and is not changed here.
