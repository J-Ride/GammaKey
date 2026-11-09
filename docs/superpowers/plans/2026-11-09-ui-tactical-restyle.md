# UI Tactical Restyle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the reviewed mock `docs/mockups/gammakey-ui-mockup-v2.html` into the WinForms app: graphite palette, cyan accent, owner-drawn controls, status strip and status bar, grouped settings, restyled toast.

**Architecture:** All visuals derive from `Theme` tokens. Each stock control that still looks like WinForms is replaced by a small owner-drawn `Gk*` control in `src/GammaKey/UI/` that keeps the API the forms already call. Pure string and number helpers are split out so they get unit tests; painting code is checked by build-run-screenshot.

**Tech Stack:** C# .NET 8 WinForms, GDI+ (`System.Drawing`), DWM via P/Invoke, xUnit 2.5.3.

## Global Constraints

- No NuGet beyond xUnit (`CLAUDE.md`). No image or font assets in the exe.
- Spec: `docs/superpowers/specs/2026-11-09-ui-tactical-restyle-design.md`. Token values in its section 4 are exact.
- Every colour and font comes from `Theme`. No literal colours in controls.
- Keep public APIs listed in spec section 6 so `MainForm` and `SettingsForm` wiring stays small.
- Tests: one assertion per test, Arrange-Act-Assert, no mocks. Run only the touched test file: `dotnet test tests/GammaKey.Tests --filter "FullyQualifiedName~<ClassName>"`.
- Commit after each task. Descriptive messages. No model attribution lines.
- `dotnet format --verify-no-changes` must be clean before the final commit.
- Build/run check: `dotnet build` then `dotnet run --project src/GammaKey`. The app starts in the tray when `ShowWindowOnLaunch` is false; left-click the tray icon to open it.

---

### Task 1: Theme tokens and fonts

**Files:**
- Modify: `src/GammaKey/UI/Theme.cs` (whole file)
- Modify: `src/GammaKey/UI/GkSlider.cs:60-80` (token names), `src/GammaKey/UI/ToastForm.cs:18,55`, `src/GammaKey/UI/TrayIcon.cs:50`, `src/GammaKey/UI/MainForm.cs:181`
- Test: `tests/GammaKey.Tests/ThemeTests.cs`

**Interfaces:**
- Produces: `Theme.Background, Panel, Surface, Raised, Line, LineStrong, TextPrimary, TextMuted, TextDim, Accent, AccentDim, AccentGlow, Warning, Danger, Ok` (Color); `Theme.Title, Label, Eyebrow, Mono, MonoSmall, Caption` (Font); `Theme.Space1..Space6, ControlHeight=30, Radius=4, LabelColumnWidth=92, SliderTrackHeight=4, SliderThumbSize=14`; `Theme.ResolveFamily(params string[] candidates) : string`; `Theme.Apply(Control)`; `Theme.FillRounded(Graphics, Brush, Rectangle, int)`; `Theme.DrawRounded(Graphics, Pen, Rectangle, int)`.
- Removed: `SurfaceRaised, Border, AccentHover, FocusRing, Disabled, Value`.

- [ ] **Step 1: Write the failing test**

```csharp
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class ThemeTests
{
    [Fact]
    public void ResolveFamilySkipsMissingFontAndReturnsFirstInstalled()
    {
        var family = Theme.ResolveFamily("No Such Font 9000", "Segoe UI");

        Assert.Equal("Segoe UI", family);
    }

    [Fact]
    public void ResolveFamilyFallsBackToGenericSansWhenNoneInstalled()
    {
        var family = Theme.ResolveFamily("No Such Font 9000");

        Assert.Equal(System.Drawing.FontFamily.GenericSansSerif.Name, family);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/GammaKey.Tests --filter "FullyQualifiedName~ThemeTests"`
Expected: build error `'Theme' does not contain a definition for 'ResolveFamily'`.

- [ ] **Step 3: Replace Theme.cs**

```csharp
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace GammaKey.UI;

// Token values come from the restyle spec 2026-11-09 section 4. Every control paints from here.
public static class Theme
{
    public static readonly Color Background = Color.FromArgb(11, 13, 16);
    public static readonly Color Panel = Color.FromArgb(17, 20, 23);
    public static readonly Color Surface = Color.FromArgb(23, 27, 32);
    public static readonly Color Raised = Color.FromArgb(31, 36, 43);
    public static readonly Color Line = Color.FromArgb(38, 44, 52);
    public static readonly Color LineStrong = Color.FromArgb(52, 60, 71);
    public static readonly Color TextPrimary = Color.FromArgb(230, 234, 239);
    public static readonly Color TextMuted = Color.FromArgb(139, 149, 163);
    public static readonly Color TextDim = Color.FromArgb(92, 102, 115);
    public static readonly Color Accent = Color.FromArgb(53, 214, 240);
    public static readonly Color AccentDim = Color.FromArgb(26, 166, 189);
    public static readonly Color AccentGlow = Color.FromArgb(46, 53, 214, 240);
    public static readonly Color Warning = Color.FromArgb(245, 176, 65);
    public static readonly Color Danger = Color.FromArgb(239, 83, 80);
    public static readonly Color Ok = Color.FromArgb(90, 211, 138);

    private static readonly string Sans = ResolveFamily("Segoe UI Variable Text", "Segoe UI");
    private static readonly string MonoFamily = ResolveFamily("Cascadia Mono", "Consolas");

    public static readonly Font Title = new(Sans, 12f, FontStyle.Bold);
    public static readonly Font Label = new(Sans, 9.5f);
    public static readonly Font Eyebrow = new(Sans, 8f, FontStyle.Bold);
    public static readonly Font Mono = new(MonoFamily, 9.5f);
    public static readonly Font MonoSmall = new(MonoFamily, 7.5f);
    public static readonly Font Caption = new(Sans, 8.5f);

    public const int Space1 = 4;
    public const int Space2 = 8;
    public const int Space3 = 12;
    public const int Space4 = 16;
    public const int Space6 = 24;
    public const int ControlHeight = 30;
    public const int Radius = 4;
    public const int LabelColumnWidth = 92;
    public const int SliderTrackHeight = 4;
    public const int SliderThumbSize = 14;

    // GDI+ silently substitutes Microsoft Sans Serif for a missing family, so pick the first
    // installed name here and fall back to the generic sans instead.
    public static string ResolveFamily(params string[] candidates)
    {
        using var installed = new InstalledFontCollection();
        var names = installed.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
            if (names.Contains(candidate)) return candidate;
        return FontFamily.GenericSansSerif.Name;
    }

    public static void Apply(Control root)
    {
        root.BackColor = Background;
        root.ForeColor = TextPrimary;
        root.Font = Label;
        foreach (Control child in root.Controls) ApplyRecursive(child);
    }

    // Only the few stock controls that remain are styled here; the Gk* controls paint themselves.
    private static void ApplyRecursive(Control control)
    {
        switch (control)
        {
            case TextBox t:
                t.BackColor = Surface;
                t.ForeColor = TextPrimary;
                t.BorderStyle = BorderStyle.FixedSingle;
                break;
            case Button b:
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderColor = LineStrong;
                b.FlatAppearance.MouseOverBackColor = Raised;
                b.BackColor = Surface;
                b.Height = ControlHeight;
                b.Padding = Padding.Empty;
                break;
            case Panel or FlowLayoutPanel or TableLayoutPanel:
                if (control.BackColor == SystemColors.Control) control.BackColor = Background;
                break;
        }
        foreach (Control child in control.Controls) ApplyRecursive(child);
    }

    public static GraphicsPath RoundedPath(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Max(1, radius * 2);
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, Brush brush, Rectangle r, int radius)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        using var path = RoundedPath(r, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRounded(Graphics g, Pen pen, Rectangle r, int radius)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        using var path = RoundedPath(r, radius);
        g.DrawPath(pen, path);
    }
}
```

- [ ] **Step 4: Fix the renamed tokens in the other files so the solution builds**

In `GkSlider.cs` replace `Theme.Border` with `Theme.Raised`, `Theme.Disabled` with `Theme.TextDim`, `Theme.AccentHover` with `Theme.Accent`, `Theme.FocusRing` with `Theme.Accent`. Delete the private `FillRounded` method in `GkSlider.cs` and call `Theme.FillRounded(...)` instead. (Task 5 rewrites the paint code; this step only keeps the build green.)

In `ToastForm.cs` line 18 `Theme.SurfaceRaised` becomes `Theme.Panel`; line 55 `Theme.Border` becomes `Theme.LineStrong`.
In `TrayIcon.cs` line 50 `Theme.SurfaceRaised` becomes `Theme.Panel`.
In `MainForm.cs` line 181 `Font = Theme.Value` becomes `Font = Theme.Mono`.

In `MainForm.cs` and `SettingsForm.cs`, `Theme.Apply` no longer styles `ListBox`, `ComboBox`, `NumericUpDown`, `CheckBox`. They render stock until Tasks 6 to 9 replace them. That is expected.

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/GammaKey.Tests`
Expected: all pass, including the two new ones.

- [ ] **Step 6: Commit**

```bash
git add src/GammaKey/UI/Theme.cs src/GammaKey/UI/GkSlider.cs src/GammaKey/UI/ToastForm.cs src/GammaKey/UI/TrayIcon.cs src/GammaKey/UI/MainForm.cs tests/GammaKey.Tests/ThemeTests.cs
git commit -m "Theme: graphite and cyan tokens, Windows-native font resolution with fallback"
```

---

### Task 2: Window chrome and crosshair icon

**Files:**
- Create: `src/GammaKey/UI/WindowChrome.cs`
- Modify: `src/GammaKey/UI/TrayIcon.cs:45-56` (`CreateIcon`)
- Modify: `src/GammaKey/UI/MainForm.cs:37-45` and `src/GammaKey/UI/SettingsForm.cs:23-31` (call `WindowChrome.Apply(this)` after `Text = ...`)

**Interfaces:**
- Produces: `WindowChrome.Apply(Form form)`. Dark caption and rounded corners; silently no-op on Windows 10 builds that lack the attributes.

- [ ] **Step 1: Create WindowChrome.cs**

```csharp
using System.Runtime.InteropServices;

namespace GammaKey.UI;

// DWM paints the caption bar, so the only way to get a dark one is to ask it. Both attributes
// return an error on older Windows 10 builds and the window then keeps the default chrome.
public static class WindowChrome
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int CornerRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Form form)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return;
        form.HandleCreated += (_, _) => Set(form.Handle);
        if (form.IsHandleCreated) Set(form.Handle);
    }

    private static void Set(IntPtr hwnd)
    {
        int on = 1;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref on, sizeof(int));
        int corner = CornerRound;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));
        int border = ColorTranslator.ToWin32(Theme.Line);
        DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(int));
    }
}
```

- [ ] **Step 2: Call it from both forms**

In `MainForm` constructor, directly after `Text = "GammaKey";` add `WindowChrome.Apply(this);`.
In `SettingsForm` constructor, directly after `Text = "GammaKey settings";` add `WindowChrome.Apply(this);`.

- [ ] **Step 3: Replace the tray icon drawing with the crosshair mark**

```csharp
    internal static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var back = new SolidBrush(Theme.Background);
        g.FillRectangle(back, 0, 0, 32, 32);
        using var pen = new Pen(Theme.Accent, 3f);
        // Crosshair: ring with a gap, plus the four hairs. Reads at 16 px in the tray.
        g.DrawLine(pen, 16, 2, 16, 30);
        g.DrawLine(pen, 2, 16, 30, 16);
        using var hole = new SolidBrush(Theme.Background);
        g.FillEllipse(hole, 7, 7, 18, 18);
        g.DrawEllipse(pen, 8, 8, 16, 16);
        return Icon.FromHandle(bmp.GetHicon());
    }
```

- [ ] **Step 4: Build and run**

Run: `dotnet build` then `dotnet run --project src/GammaKey`. Open the window from the tray.
Expected: dark title bar, rounded corners, cyan crosshair in the title bar and tray.

- [ ] **Step 5: Commit**

```bash
git add src/GammaKey/UI/WindowChrome.cs src/GammaKey/UI/TrayIcon.cs src/GammaKey/UI/MainForm.cs src/GammaKey/UI/SettingsForm.cs
git commit -m "Dark DWM title bar with rounded corners; crosshair app icon"
```

---

### Task 3: Pure display helpers

**Files:**
- Create: `src/GammaKey/UI/Format.cs`
- Test: `tests/GammaKey.Tests/FormatTests.cs`

**Interfaces:**
- Produces: `Format.Signed(decimal value, int places) : string` ("+24", "0", "-12", "1.55"); `Format.HotkeyParts(string combo) : string[]` ("Ctrl+Alt+D1" to ["Ctrl","Alt","1"]); `Format.ProfileSummary(Profile p) : string` ("gamma 1.55 · shadows +36", "neutral curve" when all Normal).

- [ ] **Step 1: Write the failing tests**

```csharp
using GammaKey.Models;
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class FormatTests
{
    [Fact]
    public void SignedAddsPlusForPositiveIntegers()
    {
        Assert.Equal("+24", Format.Signed(24m, 0));
    }

    [Fact]
    public void SignedLeavesZeroBare()
    {
        Assert.Equal("0", Format.Signed(0m, 0));
    }

    [Fact]
    public void SignedKeepsDecimalPlacesWithoutPlus()
    {
        Assert.Equal("1.55", Format.Signed(1.55m, 2));
    }

    [Fact]
    public void HotkeyPartsSplitsModifiersAndStripsDigitPrefix()
    {
        Assert.Equal(new[] { "Ctrl", "Alt", "1" }, Format.HotkeyParts("Ctrl+Alt+D1"));
    }

    [Fact]
    public void HotkeyPartsOfEmptyIsEmpty()
    {
        Assert.Empty(Format.HotkeyParts(""));
    }

    [Fact]
    public void ProfileSummaryListsOnlyChangedValues()
    {
        var p = new Profile { Gamma = 1.55, Shadows = 36 };

        Assert.Equal("gamma 1.55 · shadows +36", Format.ProfileSummary(p));
    }

    [Fact]
    public void ProfileSummaryOfNormalIsNeutral()
    {
        Assert.Equal("neutral curve", Format.ProfileSummary(new Profile()));
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/GammaKey.Tests --filter "FullyQualifiedName~FormatTests"`
Expected: build error, `Format` not found.

- [ ] **Step 3: Implement Format.cs**

```csharp
using System.Globalization;
using GammaKey.Models;

namespace GammaKey.UI;

public static class Format
{
    public static string Signed(decimal value, int places)
    {
        var text = value.ToString("F" + places, CultureInfo.InvariantCulture);
        return places == 0 && value > 0 ? "+" + text : text;
    }

    // "D1" is how Keys names the digit row; the chip should read "1".
    public static string[] HotkeyParts(string combo)
    {
        if (combo.Length == 0) return Array.Empty<string>();
        var parts = combo.Split('+');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length == 2 && parts[i][0] == 'D' && char.IsDigit(parts[i][1])) parts[i] = parts[i][1..];
        return parts;
    }

    public static string ProfileSummary(Profile p)
    {
        var parts = new List<string>(3);
        if (p.Gamma != 1.0) parts.Add("gamma " + Signed((decimal)p.Gamma, 2));
        if (p.Brightness != 0) parts.Add("brightness " + Signed(p.Brightness, 0));
        if (p.Contrast != 0) parts.Add("contrast " + Signed(p.Contrast, 0));
        if (p.Shadows != 0) parts.Add("shadows " + Signed(p.Shadows, 0));
        if (p.Highlights != 0) parts.Add("highlights " + Signed(p.Highlights, 0));
        if (p.Vibrance != ColorValues.VibranceMin) parts.Add("vibrance " + p.Vibrance);
        return parts.Count == 0 ? "neutral curve" : string.Join(" · ", parts);
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/GammaKey.Tests --filter "FullyQualifiedName~FormatTests"`
Expected: 7 passed.

- [ ] **Step 5: Commit**

```bash
git add src/GammaKey/UI/Format.cs tests/GammaKey.Tests/FormatTests.cs
git commit -m "Add Format helpers for signed readouts, hotkey chips and profile summaries"
```

---

### Task 4: Icon family and icon button

**Files:**
- Create: `src/GammaKey/UI/GkIcons.cs`, `src/GammaKey/UI/GkIconButton.cs`

**Interfaces:**
- Produces: `enum GkIcon { Add, Duplicate, Delete, Import, Export }`; `GkIcons.Draw(Graphics g, GkIcon icon, Rectangle bounds, Color color)` (16 px glyph centred in bounds, 2 px stroke, round caps); `GkIconButton : Control` with `Icon`, `IsPrimary`, `IsDanger`, `Click`, 30 x 30, `AccessibleName` = tooltip text.

- [ ] **Step 1: Create GkIcons.cs**

```csharp
using System.Drawing.Drawing2D;

namespace GammaKey.UI;

public enum GkIcon { Add, Duplicate, Delete, Import, Export }

// One family: 16 px box, 2 px stroke, round caps, drawn from the mock's SVG paths.
public static class GkIcons
{
    public const int Size = 16;

    public static void Draw(Graphics g, GkIcon icon, Rectangle bounds, Color color)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float x = bounds.Left + (bounds.Width - Size) / 2f;
        float y = bounds.Top + (bounds.Height - Size) / 2f;
        using var pen = new Pen(color, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        // Glyphs are authored on a 24-unit grid like the SVGs; scale to 16.
        float s = Size / 24f;
        PointF P(float gx, float gy) => new(x + gx * s, y + gy * s);

        switch (icon)
        {
            case GkIcon.Add:
                g.DrawLine(pen, P(12, 5), P(12, 19));
                g.DrawLine(pen, P(5, 12), P(19, 12));
                break;
            case GkIcon.Delete:
                g.DrawLine(pen, P(6, 6), P(18, 18));
                g.DrawLine(pen, P(18, 6), P(6, 18));
                break;
            case GkIcon.Duplicate:
                using (var front = Theme.RoundedPath(Rectangle.Round(new RectangleF(x + 8 * s, y + 4 * s, 13 * s, 13 * s)), (int)(2.5f * s)))
                    g.DrawPath(pen, front);
                // Back sheet: open L behind the front square.
                g.DrawLines(pen, new[] { P(4, 8), P(4, 17), P(7, 20), P(16, 20) });
                g.DrawLine(pen, P(14.5f, 8), P(14.5f, 13));
                g.DrawLine(pen, P(12, 10.5f), P(17, 10.5f));
                break;
            case GkIcon.Import:
                DrawDoor(g, pen, P);
                g.DrawLine(pen, P(21, 12), P(9, 12));
                g.DrawLines(pen, new[] { P(13, 8), P(9, 12), P(13, 16) });
                break;
            case GkIcon.Export:
                DrawDoor(g, pen, P);
                g.DrawLine(pen, P(9, 12), P(21, 12));
                g.DrawLines(pen, new[] { P(17, 8), P(21, 12), P(17, 16) });
                break;
        }
        g.SmoothingMode = old;
    }

    // The rounded "door" frame shared by Import and Export: open on the right where the arrow passes.
    private static void DrawDoor(Graphics g, Pen pen, Func<float, float, PointF> P)
    {
        g.DrawLines(pen, new[] { P(13, 9), P(13, 6), P(11, 4), P(7, 4), P(5, 6), P(5, 18), P(7, 20), P(11, 20), P(13, 18), P(13, 15) });
    }
}
```

- [ ] **Step 2: Create GkIconButton.cs**

```csharp
namespace GammaKey.UI;

public sealed class GkIconButton : Control
{
    private bool _hover;
    private bool _pressed;

    public GkIconButton(GkIcon icon, string name)
    {
        Icon = icon;
        AccessibleName = name;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        Size = new Size(Theme.ControlHeight, Theme.ControlHeight);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public GkIcon Icon { get; }
    public bool IsPrimary { get; set; }
    public bool IsDanger { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Panel);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        Color back = IsPrimary ? Theme.Accent : (_pressed || _hover ? Theme.Raised : Theme.Surface);
        Color border = IsPrimary ? Theme.Accent : (IsDanger && _hover ? Theme.Danger : (_hover || Focused ? Theme.LineStrong : Theme.Line));
        Color glyph = IsPrimary ? Theme.Background : (IsDanger && _hover ? Theme.Danger : (_hover ? Theme.TextPrimary : Theme.TextMuted));
        if (!Enabled) { back = Theme.Surface; border = Theme.Line; glyph = Theme.TextDim; }
        using (var b = new SolidBrush(back)) Theme.FillRounded(g, b, r, Theme.Radius);
        using (var p = new Pen(border)) Theme.DrawRounded(g, p, r, Theme.Radius);
        if (Focused && !IsPrimary)
        {
            using var halo = new Pen(Theme.AccentGlow, 3f);
            Theme.DrawRounded(g, halo, Rectangle.Inflate(r, -1, -1), Theme.Radius);
        }
        GkIcons.Draw(g, Icon, ClientRectangle, glyph);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: success, no warnings about the new files.

- [ ] **Step 4: Commit**

```bash
git add src/GammaKey/UI/GkIcons.cs src/GammaKey/UI/GkIconButton.cs
git commit -m "Add GDI+ icon family and owner-drawn icon button"
```

---

### Task 5: GkSlider restyle with scale row

**Files:**
- Modify: `src/GammaKey/UI/GkSlider.cs` (constructor, `TrackRect`, `OnPaint`; add `ScaleFormatter`)

**Interfaces:**
- Keeps: `Minimum Maximum Value Origin ValueChanged`.
- Produces: `Func<int, string>? ScaleFormatter` (maps a raw value to its scale label; default `Format.Signed(v, 0)`); control height 40 (20 px track row + 16 px scale row + padding).

- [ ] **Step 1: Replace the constructor, TrackRect and OnPaint**

```csharp
    public const int TrackRowHeight = 20;
    public const int ScaleRowHeight = 16;

    public GkSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Height = TrackRowHeight + ScaleRowHeight + Theme.Space1;
        Width = 320;
        TabStop = true;
    }

    // Label text under the track for minimum, origin and maximum.
    public Func<int, string>? ScaleFormatter { get; set; }

    private Rectangle TrackRect => new(Theme.SliderThumbSize / 2, (TrackRowHeight - Theme.SliderTrackHeight) / 2, Width - Theme.SliderThumbSize, Theme.SliderTrackHeight);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Panel);

        var track = TrackRect;
        using (var trackBrush = new SolidBrush(Theme.Raised)) Theme.FillRounded(g, trackBrush, track, track.Height / 2);

        // Ten tick marks under the track, like a scale on a tool.
        using (var tick = new Pen(Theme.LineStrong))
            for (int i = 0; i <= 10; i++)
            {
                int tx = track.Left + (int)Math.Round(track.Width * i / 10.0);
                g.DrawLine(tick, tx, track.Bottom + 2, tx, track.Bottom + 5);
            }

        int originX = ValueToX(Math.Clamp(Origin, _minimum, _maximum));
        int valueX = ValueToX(_value);
        var fill = Rectangle.FromLTRB(Math.Min(originX, valueX), track.Top, Math.Max(originX, valueX), track.Bottom);
        if (fill.Width > 0)
        {
            using var fillBrush = new SolidBrush(Enabled ? Theme.Accent : Theme.TextDim);
            g.FillRectangle(fillBrush, fill);
        }
        using (var originPen = new Pen(Theme.TextMuted)) g.DrawLine(originPen, originX, track.Top - 3, originX, track.Bottom + 3);

        // Diamond thumb: a rotated square, white with an accent outline.
        int half = Theme.SliderThumbSize / 2;
        var thumbCentre = new PointF(valueX, track.Top + track.Height / 2f);
        var diamond = new[]
        {
            new PointF(thumbCentre.X, thumbCentre.Y - half),
            new PointF(thumbCentre.X + half, thumbCentre.Y),
            new PointF(thumbCentre.X, thumbCentre.Y + half),
            new PointF(thumbCentre.X - half, thumbCentre.Y),
        };
        using (var thumbBrush = new SolidBrush(Enabled ? Theme.TextPrimary : Theme.TextDim)) g.FillPolygon(thumbBrush, diamond);
        using (var outline = new Pen(Enabled ? (_dragging || Focused ? Theme.Accent : Theme.Background) : Theme.TextDim, 2f)) g.DrawPolygon(outline, diamond);

        var formatter = ScaleFormatter ?? (v => Format.Signed(v, 0));
        var scaleTop = TrackRowHeight;
        var scaleRect = new Rectangle(0, scaleTop, Width, ScaleRowHeight);
        Color scaleColor = Enabled ? Theme.TextDim : Theme.Raised;
        TextRenderer.DrawText(g, formatter(_minimum), Theme.MonoSmall, scaleRect, scaleColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, formatter(Math.Clamp(Origin, _minimum, _maximum)), Theme.MonoSmall, scaleRect, scaleColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, formatter(_maximum), Theme.MonoSmall, scaleRect, scaleColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
```

The origin label is drawn centred in the control. When Origin is not the midpoint (Gamma: origin 100 in 30..280; Vibrance: origin 50 at the left end) the mock still shows the middle value, so compute the centre label as `formatter((_minimum + _maximum) / 2)` when `Origin == _minimum`, else `formatter(Origin)`. Add that branch before the three DrawText calls:

```csharp
        int centreValue = Origin <= _minimum ? (_minimum + _maximum) / 2 : Math.Clamp(Origin, _minimum, _maximum);
```
and use `formatter(centreValue)` for the centred label.

- [ ] **Step 2: Remove the now-unused `FillRounded` leftover and `OnMouseUp` thumb colour dependency**

`OnMouseUp`, `OnMouseCaptureChanged`, key handling and mouse handling stay as they are. Make sure no reference to `Theme.FocusRing` or `Theme.AccentHover` remains.

- [ ] **Step 3: Build and run**

Run: `dotnet build` then run the app. Expected: 4 px track, ticks, diamond thumb, accent outline on focus or drag, scale numbers under each slider. The number boxes still overlap the taller slider until Task 11 rebuilds the rows; that is expected.

- [ ] **Step 4: Commit**

```bash
git add src/GammaKey/UI/GkSlider.cs
git commit -m "GkSlider: thin track with ticks, diamond thumb and scale labels"
```

---

### Task 6: GkNumber readout

**Files:**
- Create: `src/GammaKey/UI/GkNumber.cs`
- Test: `tests/GammaKey.Tests/GkNumberTests.cs`

**Interfaces:**
- Produces: `GkNumber : Control` with `decimal Minimum, Maximum, Value, Increment`, `int DecimalPlaces`, `event EventHandler ValueChanged`, `Width 72`, `Height 28`. Up and down arrows (right 16 px column), mouse wheel, Up/Down keys, typed digits are not supported (the slider and arrows are the input paths; same as the mock).

- [ ] **Step 1: Write the failing tests**

```csharp
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class GkNumberTests
{
    [Fact]
    public void ValueIsClampedToRange()
    {
        var n = new GkNumber { Minimum = -100, Maximum = 100 };

        n.Value = 250;

        Assert.Equal(100m, n.Value);
    }

    [Fact]
    public void StepUpAddsIncrement()
    {
        var n = new GkNumber { Minimum = 0.3m, Maximum = 2.8m, Increment = 0.01m, DecimalPlaces = 2, Value = 1.55m };

        n.Step(+1);

        Assert.Equal(1.56m, n.Value);
    }

    [Fact]
    public void ValueChangedFiresOnceForOneChange()
    {
        var n = new GkNumber { Minimum = 0, Maximum = 10 };
        int fired = 0;
        n.ValueChanged += (_, _) => fired++;

        n.Value = 3;

        Assert.Equal(1, fired);
    }

    [Fact]
    public void DisplayTextUsesSignedFormat()
    {
        var n = new GkNumber { Minimum = -100, Maximum = 100, Value = 24 };

        Assert.Equal("+24", n.DisplayText);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/GammaKey.Tests --filter "FullyQualifiedName~GkNumberTests"`
Expected: build error, `GkNumber` not found.

- [ ] **Step 3: Create GkNumber.cs**

```csharp
namespace GammaKey.UI;

public sealed class GkNumber : Control
{
    private const int SpinWidth = 16;
    private decimal _minimum;
    private decimal _maximum = 100;
    private decimal _value;
    private int _hoverSpin; // 0 none, 1 up, -1 down

    public event EventHandler? ValueChanged;

    public GkNumber()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        Size = new Size(72, 28);
        TabStop = true;
    }

    public decimal Minimum { get => _minimum; set { _minimum = value; Value = _value; } }
    public decimal Maximum { get => _maximum; set { _maximum = value; Value = _value; } }
    public decimal Increment { get; set; } = 1m;
    public int DecimalPlaces { get; set; }

    public decimal Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, _minimum, _maximum);
            if (clamped == _value) { Invalidate(); return; }
            _value = clamped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string DisplayText => Format.Signed(_value, DecimalPlaces);

    public void Step(int direction) => Value = _value + direction * Increment;

    private Rectangle SpinRect => new(Width - SpinWidth - 1, 1, SpinWidth, Height - 2);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Panel);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var b = new SolidBrush(Theme.Surface)) Theme.FillRounded(g, b, r, Theme.Radius);
        using (var p = new Pen(Focused ? Theme.Accent : Theme.Line)) Theme.DrawRounded(g, p, r, Theme.Radius);

        var spin = SpinRect;
        using (var divider = new Pen(Theme.Line)) g.DrawLine(divider, spin.Left, 1, spin.Left, Height - 2);
        var upRect = new Rectangle(spin.Left + 1, spin.Top, spin.Width - 1, spin.Height / 2);
        var downRect = new Rectangle(spin.Left + 1, spin.Top + spin.Height / 2, spin.Width - 1, spin.Height - spin.Height / 2);
        if (Enabled && _hoverSpin != 0)
        {
            using var hover = new SolidBrush(Theme.Raised);
            g.FillRectangle(hover, _hoverSpin > 0 ? upRect : downRect);
        }
        Color arrow = Enabled ? Theme.Accent : Theme.TextDim;
        using (var pen = new Pen(arrow, 1.5f))
        {
            float cx = spin.Left + spin.Width / 2f;
            float uy = upRect.Top + upRect.Height / 2f;
            g.DrawLines(pen, new[] { new PointF(cx - 3, uy + 1.5f), new PointF(cx, uy - 1.5f), new PointF(cx + 3, uy + 1.5f) });
            float dy = downRect.Top + downRect.Height / 2f;
            g.DrawLines(pen, new[] { new PointF(cx - 3, dy - 1.5f), new PointF(cx, dy + 1.5f), new PointF(cx + 3, dy - 1.5f) });
        }

        var textRect = new Rectangle(0, 0, spin.Left, Height);
        TextRenderer.DrawText(g, DisplayText, Theme.Mono, textRect, Enabled ? Theme.TextPrimary : Theme.TextDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private int SpinAt(Point p)
    {
        var spin = SpinRect;
        if (!spin.Contains(p)) return 0;
        return p.Y < spin.Top + spin.Height / 2 ? 1 : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int s = SpinAt(e.Location);
        if (s != _hoverSpin) { _hoverSpin = s; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hoverSpin = 0; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (Enabled && e.Button == MouseButtons.Left)
        {
            Focus();
            int s = SpinAt(e.Location);
            if (s != 0) Step(s);
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e) { if (Enabled) Step(Math.Sign(e.Delta)); base.OnMouseWheel(e); }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up) { Step(+1); e.Handled = true; }
        else if (e.KeyCode == Keys.Down) { Step(-1); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/GammaKey.Tests --filter "FullyQualifiedName~GkNumberTests"`
Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add src/GammaKey/UI/GkNumber.cs tests/GammaKey.Tests/GkNumberTests.cs
git commit -m "Add GkNumber owner-drawn readout to replace NumericUpDown"
```

---

### Task 7: GkToggle

**Files:**
- Create: `src/GammaKey/UI/GkToggle.cs`
- Test: `tests/GammaKey.Tests/GkToggleTests.cs`

**Interfaces:**
- Produces: `GkToggle : Control` with `bool Checked`, `event EventHandler CheckedChanged`, `Text` (title), `string Description`, `string? Flag` (uppercase warning tag, e.g. "ANTI-CHEAT RISK"), height 40, width set by parent (Dock Top or explicit). Click anywhere toggles; Space toggles.

- [ ] **Step 1: Write the failing tests**

```csharp
using GammaKey.UI;
using Xunit;

namespace GammaKey.Tests;

public class GkToggleTests
{
    [Fact]
    public void ToggleFlipsChecked()
    {
        var t = new GkToggle { Checked = false };

        t.Toggle();

        Assert.True(t.Checked);
    }

    [Fact]
    public void SettingSameValueDoesNotRaiseCheckedChanged()
    {
        var t = new GkToggle { Checked = true };
        int fired = 0;
        t.CheckedChanged += (_, _) => fired++;

        t.Checked = true;

        Assert.Equal(0, fired);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/GammaKey.Tests --filter "FullyQualifiedName~GkToggleTests"`
Expected: build error, `GkToggle` not found.

- [ ] **Step 3: Create GkToggle.cs**

```csharp
namespace GammaKey.UI;

public sealed class GkToggle : Control
{
    private const int SwitchWidth = 34;
    private const int SwitchHeight = 18;
    private bool _checked;
    private bool _hover;

    public event EventHandler? CheckedChanged;

    public GkToggle()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Height = 40;
        Width = 380;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string Description { get; set; } = "";
    public string? Flag { get; set; }

    public void Toggle() => Checked = !Checked;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Panel);

        var sw = new Rectangle(Width - SwitchWidth - 1, (Height - SwitchHeight) / 2, SwitchWidth, SwitchHeight);
        using (var back = new SolidBrush(_checked ? Theme.AccentDim : Theme.Raised)) Theme.FillRounded(g, back, sw, 3);
        using (var border = new Pen(_checked ? Theme.Accent : (Focused || _hover ? Theme.TextMuted : Theme.LineStrong))) Theme.DrawRounded(g, border, sw, 3);
        int knob = SwitchHeight - 6;
        var knobRect = new Rectangle(_checked ? sw.Right - knob - 3 : sw.Left + 3, sw.Top + 3, knob, knob);
        using (var kb = new SolidBrush(_checked ? Color.White : Theme.TextMuted)) Theme.FillRounded(g, kb, knobRect, 2);

        int textRight = sw.Left - Theme.Space3;
        var titleSize = TextRenderer.MeasureText(g, Text, Theme.Label, new Size(textRight, Height), TextFormatFlags.NoPadding);
        var titleRect = new Rectangle(0, Theme.Space1, textRight, titleSize.Height);
        TextRenderer.DrawText(g, Text, Theme.Label, titleRect, Enabled ? Theme.TextPrimary : Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        if (!string.IsNullOrEmpty(Flag))
        {
            var flagSize = TextRenderer.MeasureText(g, Flag, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding);
            var flagRect = new Rectangle(titleSize.Width + Theme.Space2, titleRect.Top + 1, flagSize.Width + 10, flagSize.Height + 3);
            using var fp = new Pen(Color.FromArgb(115, Theme.Warning));
            Theme.DrawRounded(g, fp, flagRect, 3);
            TextRenderer.DrawText(g, Flag, Theme.MonoSmall, flagRect, Theme.Warning, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        var descRect = new Rectangle(0, titleRect.Bottom + 1, textRight, Height - titleRect.Bottom - 1);
        TextRenderer.DrawText(g, Description, Theme.Caption, descRect, Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (Enabled && e.Button == MouseButtons.Left) { Focus(); Toggle(); } base.OnMouseDown(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { Toggle(); e.Handled = true; } base.OnKeyDown(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/GammaKey.Tests --filter "FullyQualifiedName~GkToggleTests"`
Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
git add src/GammaKey/UI/GkToggle.cs tests/GammaKey.Tests/GkToggleTests.cs
git commit -m "Add GkToggle switch with title, description and warning flag"
```

---

### Task 8: GkDropdown and GkHotkeyBox

**Files:**
- Create: `src/GammaKey/UI/GkDropdown.cs`, `src/GammaKey/UI/GkHotkeyBox.cs`
- Delete: `src/GammaKey/UI/HotkeyCaptureBox.cs`
- Modify: `src/GammaKey/UI/MainForm.cs:19,330-345` and `src/GammaKey/UI/SettingsForm.cs:15,120-128` (type rename and `IsRefused` instead of `ForeColor`)

**Interfaces:**
- Produces: `GkDropdown : ComboBox` (DropDownList, owner-drawn items and painted arrow; same `Items`, `SelectedIndex`, `SelectedItem`, `SelectedIndexChanged`).
- Produces: `GkHotkeyBox : Control` with `string Combo`, `event EventHandler ComboChanged`, `bool IsRefused` (paints chips in Warning), height 30. Keyboard rules identical to `HotkeyCaptureBox` (Backspace or Delete clears; modifiers alone ignored; Escape passes to the form; Tab moves focus).

- [ ] **Step 1: Create GkDropdown.cs**

```csharp
namespace GammaKey.UI;

// ComboBox keeps its popup and keyboard handling; this paints the closed box and the rows.
public sealed class GkDropdown : ComboBox
{
    private const int WmPaint = 0x000F;

    public GkDropdown()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        ItemHeight = Theme.ControlHeight - 6;
        BackColor = Theme.Surface;
        ForeColor = Theme.TextPrimary;
        Font = Theme.Label;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool inList = (e.State & DrawItemState.ComboBoxEdit) == 0;
        using (var back = new SolidBrush(selected && inList ? Theme.Raised : Theme.Surface)) e.Graphics.FillRectangle(back, e.Bounds);
        if (selected && inList)
        {
            using var bar = new SolidBrush(Theme.Accent);
            e.Graphics.FillRectangle(bar, e.Bounds.Left, e.Bounds.Top, 2, e.Bounds.Height);
        }
        var text = new Rectangle(e.Bounds.Left + Theme.Space2, e.Bounds.Top, e.Bounds.Width - Theme.Space2, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, Items[e.Index]?.ToString(), Theme.Label, text, Enabled ? Theme.TextPrimary : Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    // The closed box is painted by the system after OnDrawItem, so repaint its border and arrow
    // once WM_PAINT has finished.
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != WmPaint) return;
        using var g = Graphics.FromHwnd(Handle);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var pen = new Pen(Focused ? Theme.Accent : Theme.Line)) Theme.DrawRounded(g, pen, r, Theme.Radius);
        var arrowBox = new Rectangle(Width - 20, 0, 18, Height);
        using (var back = new SolidBrush(Theme.Surface)) g.FillRectangle(back, arrowBox);
        using var ap = new Pen(Enabled ? Theme.Accent : Theme.TextDim, 1.5f);
        float cx = arrowBox.Left + arrowBox.Width / 2f;
        float cy = Height / 2f - 1;
        g.DrawLines(ap, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
    }
}
```

- [ ] **Step 2: Create GkHotkeyBox.cs**

```csharp
using GammaKey.Hotkeys;

namespace GammaKey.UI;

public sealed class GkHotkeyBox : Control
{
    private string _combo = "";
    private bool _isRefused;

    public event EventHandler? ComboChanged;

    public GkHotkeyBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Height = Theme.ControlHeight;
        Width = 320;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public string Combo
    {
        get => _combo;
        set { _combo = value; Invalidate(); }
    }

    public bool IsRefused
    {
        get => _isRefused;
        set { _isRefused = value; Invalidate(); }
    }

    // Every edit auto-saves, so Escape must not clear a hotkey; it goes to the form so a
    // dialog's CancelButton still closes it.
    protected override bool IsInputKey(Keys keyData) =>
        keyData is not (Keys.Tab or (Keys.Tab | Keys.Shift)) && (keyData & Keys.KeyCode) != Keys.Escape;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
        if (e.KeyCode is Keys.Back or Keys.Delete) { SetCombo(""); return; }
        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin or Keys.Escape) return;
        uint mods = 0;
        if (e.Control) mods |= HotkeyCombo.ModControl;
        if (e.Alt) mods |= HotkeyCombo.ModAlt;
        if (e.Shift) mods |= HotkeyCombo.ModShift;
        SetCombo(new HotkeyCombo(mods, e.KeyCode).ToString());
    }

    private void SetCombo(string combo)
    {
        _combo = combo;
        Invalidate();
        ComboChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseDown(MouseEventArgs e) { if (Enabled) Focus(); base.OnMouseDown(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Panel);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var b = new SolidBrush(Theme.Surface)) Theme.FillRounded(g, b, r, Theme.Radius);
        if (Focused)
        {
            using var halo = new Pen(Theme.AccentGlow, 3f);
            Theme.DrawRounded(g, halo, Rectangle.Inflate(r, -1, -1), Theme.Radius);
        }
        using (var p = new Pen(Focused ? Theme.Accent : (_isRefused ? Theme.Warning : Theme.Line))) Theme.DrawRounded(g, p, r, Theme.Radius);

        int x = Theme.Space2;
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
                x = DrawChip(g, part, x, chipText, _isRefused ? Theme.Warning : Theme.LineStrong) + Theme.Space1;
            }
        }

        string hint = Focused ? "Esc to cancel · Backspace clears" : (_isRefused ? "in use by another app" : "");
        if (hint.Length > 0)
            TextRenderer.DrawText(g, hint, Theme.Caption, new Rectangle(0, 0, Width - Theme.Space2, Height), _isRefused && !Focused ? Theme.Warning : Theme.TextDim, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    // A keycap: mono text in a box with a 2 px bottom edge. Shared look with the profile list and the toast.
    internal static int DrawChip(Graphics g, string text, int x, Color textColor, Color border)
    {
        var size = TextRenderer.MeasureText(g, text, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding);
        int h = size.Height + 6;
        int w = size.Width + 12;
        var chip = new Rectangle(x, (Theme.ControlHeight - h) / 2 - 1, w, h);
        using (var b = new SolidBrush(Theme.Background)) Theme.FillRounded(g, b, chip, 3);
        using (var p = new Pen(border)) Theme.DrawRounded(g, p, chip, 3);
        using (var bottom = new Pen(border, 2f)) g.DrawLine(bottom, chip.Left + 3, chip.Bottom, chip.Right - 3, chip.Bottom);
        TextRenderer.DrawText(g, text, Theme.MonoSmall, chip, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        return chip.Right;
    }
}
```

- [ ] **Step 3: Swap the forms over and delete the old box**

`MainForm.cs` line 19: `private readonly GkHotkeyBox _hotkeyBox = new() { Width = WideInputWidth };`
`MainForm.cs` line 330: signature `SaveSettingsAndRegister(GkHotkeyBox? changedBox = null)`; inside, replace `changedBox.ForeColor = isRefused ? Theme.Warning : Theme.TextPrimary;` with `changedBox.IsRefused = isRefused;`. Line 308 `_hotkeyBox.ForeColor = Theme.TextPrimary;` becomes `_hotkeyBox.IsRefused = false;`.
`SettingsForm.cs` line 15: `private readonly GkHotkeyBox _resetHotkeyBox = new() { Width = 320 };` and line 126 becomes `_resetHotkeyBox.IsRefused = isRefused;`.
Delete `src/GammaKey/UI/HotkeyCaptureBox.cs`.

- [ ] **Step 4: Build and run tests**

Run: `dotnet build` then `dotnet test tests/GammaKey.Tests`
Expected: build clean, all tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A src/GammaKey/UI
git commit -m "Add GkDropdown and GkHotkeyBox with keycap chips; retire HotkeyCaptureBox"
```

---

### Task 9: Group card, status chip and profile list

**Files:**
- Create: `src/GammaKey/UI/GkGroup.cs`, `src/GammaKey/UI/GkChip.cs`, `src/GammaKey/UI/ProfileList.cs`

**Interfaces:**
- Produces: `GkGroup : Panel` with `Title`, `Hint` (right-aligned mono), and `Content` (a `FlowLayoutPanel`, TopDown, no wrap, AutoSize) that callers add rows to. Paints Panel fill, Line border, radius 4, eyebrow.
- Produces: `GkChip : Control` with `Label`, `Value`, `DotState` (`None`, `Ok`, `Dim`), AutoSize width, height 26.
- Produces: `ProfileList : ListBox` owner-drawn (ItemHeight 44) that takes `Profile` items; `Bind(IReadOnlyList<Profile> profiles, int keepIndex)`. Row paints name, `Format.ProfileSummary`, hotkey chips; selected row Surface fill, LineStrong border, 2 px Accent left bar.

- [ ] **Step 1: Create GkGroup.cs**

```csharp
namespace GammaKey.UI;

public sealed class GkGroup : Panel
{
    private const int HeaderHeight = 22;

    public GkGroup(string title, string hint = "")
    {
        Title = title;
        Hint = hint;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Panel;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(Theme.Space3, HeaderHeight + Theme.Space2, Theme.Space3, Theme.Space2);
        Content = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, BackColor = Theme.Panel, Margin = Padding.Empty };
        Controls.Add(Content);
    }

    public string Title { get; }
    public string Hint { get; }
    public FlowLayoutPanel Content { get; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var b = new SolidBrush(Theme.Panel)) Theme.FillRounded(g, b, r, Theme.Radius + 1);
        using (var p = new Pen(Theme.Line)) Theme.DrawRounded(g, p, r, Theme.Radius + 1);
        var head = new Rectangle(Theme.Space3, Theme.Space2, Width - 2 * Theme.Space3, HeaderHeight - Theme.Space2);
        TextRenderer.DrawText(g, Title.ToUpperInvariant(), Theme.Eyebrow, head, Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        if (Hint.Length > 0)
            TextRenderer.DrawText(g, Hint, Theme.MonoSmall, head, Theme.TextDim, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
```

Letter-spacing is not available in GDI text; the eyebrow uses uppercase bold 8 pt and reads close enough to the mock.

- [ ] **Step 2: Create GkChip.cs**

```csharp
namespace GammaKey.UI;

public enum DotState { None, Ok, Dim }

public sealed class GkChip : Control
{
    private string _label = "";
    private string _value = "";
    private DotState _dot;

    public GkChip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Height = 26;
        Margin = new Padding(0, 0, Theme.Space2, 0);
    }

    public string Label { get => _label; set { _label = value; Resize(); } }
    public string Value { get => _value; set { _value = value; Resize(); } }
    public DotState Dot { get => _dot; set { _dot = value; Resize(); } }

    private void Resize()
    {
        using var g = CreateGraphics();
        int w = Theme.Space2 * 2;
        if (_dot != DotState.None) w += 7 + Theme.Space2;
        if (_label.Length > 0) w += TextRenderer.MeasureText(g, _label, Theme.Caption, Size.Empty, TextFormatFlags.NoPadding).Width + (_value.Length > 0 ? Theme.Space1 : 0);
        if (_value.Length > 0) w += TextRenderer.MeasureText(g, _value, Theme.Mono, Size.Empty, TextFormatFlags.NoPadding).Width;
        Width = w;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Panel);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var b = new SolidBrush(Theme.Surface)) Theme.FillRounded(g, b, r, Theme.Radius);
        using (var p = new Pen(Theme.Line)) Theme.DrawRounded(g, p, r, Theme.Radius);
        int x = Theme.Space2;
        if (_dot != DotState.None)
        {
            using var dot = new SolidBrush(_dot == DotState.Ok ? Theme.Ok : Theme.TextDim);
            g.FillEllipse(dot, x, Height / 2 - 3, 7, 7);
            x += 7 + Theme.Space2;
        }
        if (_label.Length > 0)
        {
            var size = TextRenderer.MeasureText(g, _label, Theme.Caption, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, _label, Theme.Caption, new Rectangle(x, 0, size.Width, Height), Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            x += size.Width + Theme.Space1;
        }
        if (_value.Length > 0)
            TextRenderer.DrawText(g, _value, Theme.Mono, new Rectangle(x, 0, Width - x, Height), Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
```

- [ ] **Step 3: Create ProfileList.cs**

```csharp
using GammaKey.Models;

namespace GammaKey.UI;

public sealed class ProfileList : ListBox
{
    public const int RowHeight = 44;

    public ProfileList()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = RowHeight;
        BorderStyle = BorderStyle.None;
        IntegralHeight = false;
        BackColor = Theme.Panel;
        ForeColor = Theme.TextPrimary;
        Margin = Padding.Empty;
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
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var back = new SolidBrush(Theme.Panel)) g.FillRectangle(back, e.Bounds);
        var row = new Rectangle(e.Bounds.Left + Theme.Space2, e.Bounds.Top + 1, e.Bounds.Width - 2 * Theme.Space2 - 1, e.Bounds.Height - 3);
        if (selected)
        {
            using var fill = new SolidBrush(Theme.Surface);
            Theme.FillRounded(g, fill, row, Theme.Radius);
            using var border = new Pen(Theme.LineStrong);
            Theme.DrawRounded(g, border, row, Theme.Radius);
            using var bar = new SolidBrush(Theme.Accent);
            g.FillRectangle(bar, row.Left, row.Top + 3, 2, row.Height - 6);
        }

        var parts = Format.HotkeyParts(profile.Hotkey);
        int chipsWidth = 0;
        foreach (var part in parts) chipsWidth += TextRenderer.MeasureText(g, part, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding).Width + 12 + Theme.Space1;
        int textRight = row.Right - Theme.Space2 - chipsWidth;

        var nameRect = new Rectangle(row.Left + Theme.Space3, row.Top + 5, textRight - row.Left - Theme.Space3, 18);
        TextRenderer.DrawText(g, profile.Name, Theme.Label, nameRect, Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        var subRect = new Rectangle(nameRect.Left, nameRect.Bottom, nameRect.Width, 14);
        TextRenderer.DrawText(g, Format.ProfileSummary(profile), Theme.Caption, subRect, Theme.TextDim, TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        // Chips sit on the row's vertical centre; DrawChip assumes a ControlHeight-tall line, so offset.
        int x = textRight;
        var state = g.Save();
        g.TranslateTransform(0, row.Top + (row.Height - Theme.ControlHeight) / 2f);
        foreach (var part in parts)
            x = GkHotkeyBox.DrawChip(g, part, x, selected ? Theme.Accent : Theme.TextMuted, selected ? Theme.AccentDim : Theme.Line) + Theme.Space1;
        g.Restore(state);
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: success.

- [ ] **Step 5: Commit**

```bash
git add src/GammaKey/UI/GkGroup.cs src/GammaKey/UI/GkChip.cs src/GammaKey/UI/ProfileList.cs
git commit -m "Add group card, status chip and owner-drawn profile list"
```

---

### Task 10: Display mode and armed hotkey count

**Files:**
- Create: `src/GammaKey/Display/DisplayMode.cs`
- Modify: `src/GammaKey/App/AppController.cs:77-95` (`RegisterHotkeys` sets `ArmedHotkeys`), add property near line 24

**Interfaces:**
- Produces: `record DisplayMode(int Width, int Height, int RefreshHz)`; `DisplayModeNative.Get(string deviceName) : DisplayMode?` via `EnumDisplaySettings(ENUM_CURRENT_SETTINGS)`.
- Produces: `AppController.ArmedHotkeys : int` (profiles plus reset successfully registered in the last `RegisterHotkeys`).

- [ ] **Step 1: Create DisplayMode.cs**

```csharp
using System.Runtime.InteropServices;

namespace GammaKey.Display;

public sealed record DisplayMode(int Width, int Height, int RefreshHz);

public static class DisplayModeNative
{
    private const int EnumCurrentSettings = -1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Devmode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public int dmPositionX, dmPositionY;
        public uint dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref Devmode devMode);

    public static DisplayMode? Get(string deviceName)
    {
        var mode = new Devmode { dmSize = (ushort)Marshal.SizeOf<Devmode>() };
        if (!EnumDisplaySettings(deviceName, EnumCurrentSettings, ref mode)) return null;
        return new DisplayMode((int)mode.dmPelsWidth, (int)mode.dmPelsHeight, (int)mode.dmDisplayFrequency);
    }
}
```

- [ ] **Step 2: Add ArmedHotkeys to AppController**

After `public AppSettings Settings { get; private set; }` add:
```csharp
    public int ArmedHotkeys { get; private set; }
```
In `RegisterHotkeys`, change the body so each successful `Register` counts:
```csharp
        int armed = 0;
        if (HotkeyCombo.TryParse(Settings.ResetHotkey, out var resetCombo))
        {
            if (_hotkeys.Register(ResetHotkeyId, resetCombo)) armed++;
            else failed.Add("Reset");
        }

        int nextId = FirstProfileHotkeyId;
        foreach (var profile in Settings.Profiles)
        {
            if (!HotkeyCombo.TryParse(profile.Hotkey, out var combo)) continue;
            if (_hotkeys.Register(nextId, combo)) { _profileIdsByHotkeyId[nextId] = profile.Id; armed++; }
            else failed.Add(profile.Name);
            nextId++;
        }
        ArmedHotkeys = armed;
        return failed;
```

- [ ] **Step 3: Build and run existing tests**

Run: `dotnet test tests/GammaKey.Tests`
Expected: all pass (no behaviour change to registration).

- [ ] **Step 4: Commit**

```bash
git add src/GammaKey/Display/DisplayMode.cs src/GammaKey/App/AppController.cs
git commit -m "Expose current display mode and armed hotkey count for the status strip"
```

---

### Task 11: MainForm layout rebuild

**Files:**
- Modify: `src/GammaKey/UI/MainForm.cs` (fields lines 12-31, constructor lines 33-66, `BuildLayout` through `Slider` lines 76-219, `SetEditorEnabled` 258-271, `RefreshProfileList` 273-282, `LoadSelectedProfile` 284-310)

**Interfaces:**
- Consumes: everything from Tasks 1 to 10.
- Keeps every private method after line 310 unchanged (`CurrentValues`, `PushValuesToProfileAndPreview`, `SaveIfEditing`, `SaveSettingsAndRegister`, Add/Duplicate/Delete/Import/Export, closing, `OpenSettings`).

- [ ] **Step 1: Replace the fields (lines 12-31)**

```csharp
    private readonly AppController _controller;
    private readonly ProfileList _profileList = new() { Dock = DockStyle.Fill };
    private readonly Label _profileCount = new() { AutoSize = true, Font = Theme.MonoSmall, ForeColor = Theme.TextDim, Margin = Padding.Empty };
    private const string ProfileFileFilter = "GammaKey profiles (*.json)|*.json";

    private readonly TextBox _nameBox = new() { Font = Theme.Label };
    private readonly GkHotkeyBox _hotkeyBox = new();
    private readonly GkDropdown _targetBox = new();
    private readonly Label _heading = new() { AutoSize = true, Font = Theme.Title, Margin = new Padding(0, 0, Theme.Space2, 0) };
    private readonly Label _activeTag = new() { AutoSize = true, Font = Theme.MonoSmall, ForeColor = Theme.Accent, Text = "ACTIVE", Padding = new Padding(5, 2, 5, 2), BorderStyle = BorderStyle.FixedSingle, Visible = false, Margin = new Padding(0, 4, 0, 0) };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Theme.Warning, Font = Theme.Caption, MaximumSize = new Size(600, 0), Margin = new Padding(0, Theme.Space2, 0, 0) };
    private readonly Label _vibranceNote = new() { AutoSize = true, ForeColor = Theme.TextDim, Font = Theme.Caption, Margin = new Padding(Theme.LabelColumnWidth + Theme.Space3, 0, 0, Theme.Space1), Text = "Vibrance is set in the driver and survives alt-tab. Needs an NVIDIA GPU." };

    private readonly GkChip _monitorChip = new() { Dot = DotState.Ok };
    private readonly GkChip _modeChip = new();
    private readonly GkChip _hdrChip = new() { Label = "HDR" };
    private readonly GkChip _hotkeysChip = new() { Label = "Hotkeys", Dot = DotState.Ok };
    private readonly Label _appliedLine = new() { AutoSize = true, Font = Theme.MonoSmall, ForeColor = Theme.Ok, Text = "● not applied yet", Margin = new Padding(0, 0, Theme.Space4, 0) };
    private readonly Label _hookLine = new() { AutoSize = true, Font = Theme.MonoSmall, ForeColor = Theme.TextDim };
    private readonly Label _savedLine = new() { AutoSize = true, Font = Theme.MonoSmall, ForeColor = Theme.TextDim, Text = "profiles.json · saved" };

    private readonly GkSlider _gamma = new() { Minimum = 30, Maximum = 280, Origin = 100, ScaleFormatter = v => Format.Signed(v / 100m, 2) };
    private readonly GkSlider _brightness = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _contrast = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _shadows = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _highlights = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _vibrance = new() { Minimum = 50, Maximum = 100, Origin = 50, ScaleFormatter = v => v.ToString() };
    private readonly Dictionary<GkSlider, GkNumber> _numbers = new();
    private readonly List<GkIconButton> _profileButtons = new();

    private Profile? _editing;
    private string? _activeProfileId;
    private bool _loading;
```

- [ ] **Step 2: Replace the constructor (lines 33-66)**

```csharp
    public MainForm(AppController controller)
    {
        _controller = controller;
        Text = "GammaKey";
        WindowChrome.Apply(this);
        Width = 960;
        Height = 680;
        MinimumSize = new Size(860, 600);
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = TrayIcon.CreateIcon();
        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += OnFormClosing;
        _controller.SettingsChanged += () =>
        {
            if (IsDisposed) return;
            RefreshProfileList(keepSelection: true);
            RefreshStatus();
        };
        _controller.ProfileApplied += (p, _) =>
        {
            if (IsDisposed) return;
            _activeProfileId = p.Id;
            _appliedLine.Text = $"● applied {DateTime.Now:HH:mm:ss}";
            _activeTag.Visible = _editing is not null && _editing.Id == _activeProfileId;
        };
        _controller.ResetApplied += _ =>
        {
            if (IsDisposed) return;
            _activeProfileId = null;
            _appliedLine.Text = $"● restored {DateTime.Now:HH:mm:ss}";
            _activeTag.Visible = false;
        };

        BuildLayout();
        Theme.Apply(this);
        LoadMonitorsIntoTarget();
        RefreshProfileList();
        RefreshStatus();
        if (_profileList.Items.Count > 0)
        {
            _loading = true;
            _profileList.SelectedIndex = 0;
            _loading = false;
            LoadSelectedProfile(apply: false);
        }
        SetEditorEnabled(_editing is not null);
    }
```

- [ ] **Step 3: Replace BuildLayout, Button, Row, FormRow, Slider (lines 76-219)**

```csharp
    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = Theme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(root);

        // Status strip
        var strip = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, BackColor = Theme.Panel, Padding = new Padding(Theme.Space4, Theme.Space2, Theme.Space4, Theme.Space2), Margin = Padding.Empty };
        strip.Paint += (_, e) => { using var p = new Pen(Theme.Line); e.Graphics.DrawLine(p, 0, strip.Height - 1, strip.Width, strip.Height - 1); };
        strip.Controls.AddRange(new Control[] { _monitorChip, _modeChip, _hdrChip, _hotkeysChip });
        root.Controls.Add(strip, 0, 0);

        // Body: rail + editor
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty, BackColor = Theme.Background };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 272));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(body, 0, 1);

        var rail = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = Theme.Panel };
        rail.Paint += (_, e) => { using var p = new Pen(Theme.Line); e.Graphics.DrawLine(p, rail.Width - 1, 0, rail.Width - 1, rail.Height); };
        rail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rail.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var railHead = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, BackColor = Theme.Panel, Padding = new Padding(Theme.Space4, Theme.Space3, Theme.Space4, Theme.Space1), Margin = Padding.Empty };
        railHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        railHead.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        railHead.Controls.Add(new Label { Text = "PROFILES", Font = Theme.Eyebrow, ForeColor = Theme.TextMuted, AutoSize = true, Margin = Padding.Empty }, 0, 0);
        railHead.Controls.Add(_profileCount, 1, 0);
        rail.Controls.Add(railHead, 0, 0);
        rail.Controls.Add(_profileList, 0, 1);

        var railActions = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Bottom, BackColor = Theme.Panel, Padding = new Padding(Theme.Space3, Theme.Space2, Theme.Space3, Theme.Space3), Margin = Padding.Empty };
        railActions.Paint += (_, e) => { using var p = new Pen(Theme.Line); e.Graphics.DrawLine(p, 0, 0, railActions.Width, 0); };
        var iconRow = new TableLayoutPanel { ColumnCount = 5, AutoSize = true, Width = 272 - 2 * Theme.Space3 - 1, Height = Theme.ControlHeight, Margin = new Padding(0, 0, 0, Theme.Space2), BackColor = Theme.Panel };
        for (int i = 0; i < 5; i++) iconRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        iconRow.Controls.Add(IconButton(GkIcon.Add, "Add profile", (_, _) => AddProfile(), isPrimary: true), 0, 0);
        iconRow.Controls.Add(IconButton(GkIcon.Duplicate, "Duplicate profile", (_, _) => DuplicateProfile()), 1, 0);
        iconRow.Controls.Add(IconButton(GkIcon.Delete, "Delete profile", (_, _) => DeleteProfile(), isDanger: true), 2, 0);
        iconRow.Controls.Add(IconButton(GkIcon.Import, "Import profiles", (_, _) => ImportProfiles()), 3, 0);
        iconRow.Controls.Add(IconButton(GkIcon.Export, "Export profiles", (_, _) => ExportProfiles()), 4, 0);
        railActions.Controls.Add(iconRow);
        railActions.Controls.Add(WideButton("Settings", (_, _) => OpenSettings()));
        var restore = WideButton("Restore monitor", (_, _) => _controller.Reset());
        restore.Margin = new Padding(0, Theme.Space2, 0, 0);
        railActions.Controls.Add(restore);
        rail.Controls.Add(railActions, 0, 2);
        body.Controls.Add(rail, 0, 0);

        // Editor
        var editor = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(Theme.Space4, Theme.Space3, Theme.Space4, Theme.Space4), BackColor = Theme.Background };
        var head = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, Theme.Space2), BackColor = Theme.Background };
        head.Controls.Add(_heading);
        head.Controls.Add(_activeTag);
        editor.Controls.Add(head);

        var profileGroup = new GkGroup("Profile");
        profileGroup.Content.Controls.Add(FormRow(new Label { Text = "Name" }, _nameBox));
        var two = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = Padding.Empty, BackColor = Theme.Panel };
        two.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        two.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        two.Controls.Add(FormRow(new Label { Text = "Hotkey" }, _hotkeyBox), 0, 0);
        two.Controls.Add(FormRow(new Label { Text = "Monitors" }, _targetBox), 1, 0);
        profileGroup.Content.Controls.Add(two);
        editor.Controls.Add(profileGroup);

        var curveGroup = new GkGroup("Curve", "GDI gamma ramp");
        curveGroup.Content.Controls.Add(Slider("Gamma", _gamma, isGamma: true));
        curveGroup.Content.Controls.Add(Slider("Brightness", _brightness));
        curveGroup.Content.Controls.Add(Slider("Contrast", _contrast));
        curveGroup.Content.Controls.Add(Slider("Shadows", _shadows));
        curveGroup.Content.Controls.Add(Slider("Highlights", _highlights));
        editor.Controls.Add(curveGroup);

        var colorGroup = new GkGroup("Color", "NVIDIA only");
        colorGroup.Content.Controls.Add(Slider("Vibrance", _vibrance));
        colorGroup.Content.Controls.Add(_vibranceNote);
        editor.Controls.Add(colorGroup);

        editor.Controls.Add(_status);
        body.Controls.Add(editor, 1, 0);

        // Groups stretch to the editor width; FlowLayoutPanel does not do that itself.
        editor.Resize += (_, _) =>
        {
            int w = editor.ClientSize.Width - editor.Padding.Horizontal;
            foreach (Control c in editor.Controls) if (c is GkGroup) c.Width = w;
            two.Width = w - profileGroup.Padding.Horizontal;
        };

        // Status bar
        var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Theme.Panel, Padding = new Padding(Theme.Space3, 0, Theme.Space3, 0), Margin = Padding.Empty };
        bar.Paint += (_, e) => { using var p = new Pen(Theme.Line); e.Graphics.DrawLine(p, 0, 0, bar.Width, 0); };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach (var l in new[] { _appliedLine, _hookLine, _savedLine }) l.Anchor = AnchorStyles.Left;
        bar.Controls.Add(_appliedLine, 0, 0);
        bar.Controls.Add(_hookLine, 1, 0);
        bar.Controls.Add(_savedLine, 2, 0);
        root.Controls.Add(bar, 0, 2);

        _nameBox.TextChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Name = _nameBox.Text; _heading.Text = _nameBox.Text; _controller.Save(); } };
        _hotkeyBox.ComboChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Hotkey = _hotkeyBox.Combo; SaveSettingsAndRegister(_hotkeyBox); } };
        _targetBox.SelectedIndexChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Target = TargetFromCombo(); _controller.Save(); _status.Text = _controller.ApplyProfile(_editing).Note ?? ""; } };
        _profileList.SelectedIndexChanged += (_, _) => LoadSelectedProfile();

        if (!_controller.Vibrance.IsAvailable)
        {
            _vibrance.Enabled = false;
            _vibranceNote.Text = _controller.Vibrance.UnavailableReason ?? "Vibrance needs an NVIDIA graphics card";
        }
    }

    private GkIconButton IconButton(GkIcon icon, string name, EventHandler onClick, bool isPrimary = false, bool isDanger = false)
    {
        var b = new GkIconButton(icon, name) { IsPrimary = isPrimary, IsDanger = isDanger, Anchor = AnchorStyles.None, Margin = Padding.Empty };
        b.Click += onClick;
        _profileButtons.Add(b);
        return b;
    }

    private static Button WideButton(string text, EventHandler onClick)
    {
        var b = new Button { Text = text, Width = 272 - 2 * Theme.Space3 - 1, Height = Theme.ControlHeight, Margin = Padding.Empty, Font = Theme.Label };
        b.Click += onClick;
        return b;
    }

    // One grid per row so labels share a column and the input stretches to the row's right edge.
    private static TableLayoutPanel FormRow(Label label, Control input)
    {
        var panel = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 2, 0, 2), Dock = DockStyle.Top, Height = Theme.ControlHeight + 6, BackColor = Theme.Panel };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.LabelColumnWidth));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        label.AutoSize = true;
        label.ForeColor = Theme.TextMuted;
        label.Anchor = AnchorStyles.Left;
        label.Margin = Padding.Empty;
        panel.Controls.Add(label, 0, 0);
        input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        input.Margin = new Padding(0, 0, Theme.Space3, 0);
        panel.Controls.Add(input, 1, 0);
        return panel;
    }

    private TableLayoutPanel Slider(string label, GkSlider bar, bool isGamma = false)
    {
        var number = new GkNumber
        {
            Minimum = isGamma ? 0.3m : bar.Minimum,
            Maximum = isGamma ? 2.8m : bar.Maximum,
            Increment = isGamma ? 0.01m : 1m,
            DecimalPlaces = isGamma ? 2 : 0,
        };
        _numbers[bar] = number;

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
            SaveIfEditing();
        };
        // Saving on every drag tick would write the file many times a second, so save when
        // the drag or key press ends. A capture loss (Alt+Tab mid-drag) also ends the drag.
        bar.MouseUp += (_, _) => SaveIfEditing();
        bar.KeyUp += (_, _) => SaveIfEditing();
        bar.MouseCaptureChanged += (_, _) => SaveIfEditing();
        bar.MouseWheel += (_, _) => SaveIfEditing();

        var panel = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 2, 0, 2), Dock = DockStyle.Top, Height = bar.Height + 4, BackColor = Theme.Panel };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.LabelColumnWidth));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72 + Theme.Space3));
        var text = new Label { Text = label, AutoSize = true, ForeColor = Theme.TextMuted, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 4, 0, 0) };
        panel.Controls.Add(text, 0, 0);
        bar.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        bar.Margin = new Padding(0, 0, Theme.Space3, 0);
        panel.Controls.Add(bar, 1, 0);
        number.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        number.Margin = new Padding(Theme.Space3, 0, 0, 0);
        panel.Controls.Add(number, 2, 0);
        return panel;
    }

    private void RefreshStatus()
    {
        var monitors = _controller.Display.GetMonitors();
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
        if (primary is null)
        {
            _monitorChip.Value = "no monitor";
            _monitorChip.Dot = DotState.Dim;
            _modeChip.Visible = false;
        }
        else
        {
            _monitorChip.Value = primary.FriendlyName;
            _monitorChip.Label = "primary";
            var mode = DisplayModeNative.Get(primary.DeviceName);
            _modeChip.Visible = mode is not null;
            if (mode is not null) _modeChip.Value = $"{mode.Width}×{mode.Height} · {mode.RefreshHz} Hz";
            _hdrChip.Value = primary.IsHdrOn ? "on" : "off";
            _hdrChip.Dot = primary.IsHdrOn ? DotState.Ok : DotState.Dim;
        }
        _hotkeysChip.Value = $"{_controller.ArmedHotkeys} armed";
        _hotkeysChip.Dot = _controller.ArmedHotkeys > 0 ? DotState.Ok : DotState.Dim;
        _hookLine.Text = _controller.Settings.HotkeyPassthrough ? "hook: pass-through" : "hook: normal";
    }
```

The chip `Label` for the monitor chip is set after `Value` so the first `Resize` measures both; `GkChip.Resize` runs on each property set, which is fine at this size.

- [ ] **Step 4: Replace SetEditorEnabled, RefreshProfileList and the start of LoadSelectedProfile**

```csharp
    private void SetEditorEnabled(bool on)
    {
        _nameBox.Enabled = on;
        _hotkeyBox.Enabled = on;
        _targetBox.Enabled = on;
        foreach (var (bar, number) in _numbers)
        {
            bar.Enabled = on;
            number.Enabled = on;
        }
        if (on && !_controller.Vibrance.IsAvailable)
        {
            _vibrance.Enabled = false;
            _numbers[_vibrance].Enabled = false;
        }
        // Duplicate and Delete need a selection; Add, Import and Export never do.
        _profileButtons[1].Enabled = on;
        _profileButtons[2].Enabled = on;
        if (!on) { _heading.Text = "No profile selected"; _activeTag.Visible = false; }
    }

    private void RefreshProfileList(bool keepSelection = false)
    {
        var selected = _profileList.SelectedIndex;
        _loading = true;
        _profileList.Bind(_controller.Settings.Profiles, keepSelection ? selected : -1);
        _loading = false;
        _profileCount.Text = _controller.Settings.Profiles.Count.ToString();
    }
```

In `LoadSelectedProfile`, after `SetEditorEnabled(true);` add:
```csharp
        _heading.Text = _editing.Name;
        _activeTag.Visible = _editing.Id == _activeProfileId;
```
and replace `_hotkeyBox.ForeColor = Theme.TextPrimary;` with `_hotkeyBox.IsRefused = false;` (if Task 8 did not already).

The profile list previously formatted item text itself; `ProfileList.Bind` now takes the `Profile` objects, and `TrayIcon` is untouched because it reads `Settings.Profiles` directly.

- [ ] **Step 5: Build, run, screenshot**

Run: `dotnet build` then `dotnet run --project src/GammaKey`.
Check: status strip chips show the primary monitor, resolution and refresh, HDR state and armed hotkeys; rail rows show name, summary and chips; icon row has five icons; groups stretch to the editor width; sliders show scales; number boxes align right; status bar shows hook mode. Resize the window: groups follow. Switch profiles: heading and ACTIVE tag update after a hotkey press or list click (list click applies, so the tag appears).

- [ ] **Step 6: Run all tests and commit**

```bash
dotnet test tests/GammaKey.Tests
git add src/GammaKey/UI/MainForm.cs
git commit -m "MainForm: status strip, profile rail, grouped editor and status bar on the new theme"
```

---

### Task 12: SettingsForm rebuild

**Files:**
- Modify: `src/GammaKey/UI/SettingsForm.cs` (fields lines 10-16, constructor size line 31, `BuildLayout` lines 44-98)

**Interfaces:**
- Consumes: `GkToggle`, `GkGroup`, `GkHotkeyBox`, `WindowChrome`.
- Keeps: `LoadSettingsIntoControls`, `SaveAndRegister`, every `CheckedChanged` handler body (they read `.Checked`, which `GkToggle` provides).

- [ ] **Step 1: Replace the five CheckBox fields**

```csharp
    private readonly GkToggle _startWithWindows = new() { Text = "Start with Windows", Description = "Runs in the tray at sign-in" };
    private readonly GkToggle _showWindowOnLaunch = new() { Text = "Show window on launch", Description = "Off means tray only" };
    private readonly GkToggle _showToast = new() { Text = "Show profile name on hotkey", Description = "1.5 s toast, bottom right" };
    private readonly GkToggle _guard = new() { Text = "Re-apply if Windows resets the curve", Description = "Guards against fullscreen and driver resets" };
    private readonly GkToggle _passthrough = new() { Text = "Let hotkeys pass through to other apps", Description = "System-wide keyboard hook. Prefer Ctrl or Alt combos instead.", Flag = "ANTI-CHEAT RISK" };
```

- [ ] **Step 2: Set the dialog size and replace the layout part of BuildLayout (lines 44-67)**

Constructor: `ClientSize = new Size(440, 500);`

```csharp
    private void BuildLayout()
    {
        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(Theme.Space4), BackColor = Theme.Background };
        Controls.Add(stack);
        int groupWidth = ClientSize.Width - 2 * Theme.Space4;

        var startup = new GkGroup("Startup") { Width = groupWidth, Margin = new Padding(0, 0, 0, Theme.Space3) };
        AddToggles(startup, _startWithWindows, _showWindowOnLaunch);
        stack.Controls.Add(startup);

        var behaviour = new GkGroup("Behaviour") { Width = groupWidth, Margin = new Padding(0, 0, 0, Theme.Space3) };
        AddToggles(behaviour, _showToast, _guard, _passthrough);
        stack.Controls.Add(behaviour);

        var reset = new GkGroup("Restore monitor hotkey") { Width = groupWidth, Margin = new Padding(0, 0, 0, Theme.Space3) };
        _resetHotkeyBox.Width = groupWidth - reset.Padding.Horizontal;
        _resetHotkeyBox.Margin = new Padding(0, 2, 0, 4);
        reset.Content.Controls.Add(_resetHotkeyBox);
        stack.Controls.Add(reset);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Width = groupWidth, Margin = Padding.Empty, BackColor = Theme.Background };
        var close = new Button { Text = "Close", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = Padding.Empty, DialogResult = DialogResult.OK };
        var openFolder = new Button { Text = "Open profiles folder", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = new Padding(0, 0, Theme.Space2, 0) };
        openFolder.Click += (_, _) => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_controller.BaseDirectory}\"") { UseShellExecute = true });
        buttons.Controls.Add(close);
        buttons.Controls.Add(openFolder);
        stack.Controls.Add(buttons);
        stack.Controls.Add(_status);
        CancelButton = close;
```

Keep the existing handler wiring from line 68 (`_startWithWindows.CheckedChanged += ...`) to the end of the method as it is. Add this helper after `BuildLayout`:

```csharp
    private static void AddToggles(GkGroup group, params GkToggle[] toggles)
    {
        int width = group.Width - group.Padding.Horizontal;
        for (int i = 0; i < toggles.Length; i++)
        {
            toggles[i].Width = width;
            toggles[i].Margin = Padding.Empty;
            group.Content.Controls.Add(toggles[i]);
            if (i < toggles.Length - 1)
                group.Content.Controls.Add(new Panel { Width = width, Height = 1, BackColor = Theme.Line, Margin = Padding.Empty });
        }
    }
```

The `openFolder` and `close` buttons are stock `Button`s styled by `Theme.Apply` (flat, Surface, LineStrong border). `Theme.Apply` runs after `BuildLayout` in the constructor already.

- [ ] **Step 3: Build and run**

Open Settings from the rail. Expected: three group cards, toggles with descriptions and dividers, the anti-cheat flag beside the pass-through title, the restore hotkey as keycaps, buttons right-aligned. Toggling still saves (watch `profiles.json` timestamp) and the pass-through confirmation box still appears.

- [ ] **Step 4: Commit**

```bash
git add src/GammaKey/UI/SettingsForm.cs
git commit -m "SettingsForm: grouped toggles with descriptions and inline anti-cheat flag"
```

---

### Task 13: Toast restyle

**Files:**
- Modify: `src/GammaKey/UI/ToastForm.cs` (constructor, `OnPaint`, `Show`)
- Modify: `src/GammaKey/UI/TrayIcon.cs:31-38` (callers)

**Interfaces:**
- Produces: `ToastForm.Show(string title, string detail)`; the old single-argument `Show(string)` is removed.

- [ ] **Step 1: Rewrite the drawing**

Replace the `_label` field, constructor body that touches `_label`, and `OnPaint` with an owner-painted form:

```csharp
    private const int Pad = Theme.Space4;
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
        using var g = CreateGraphics();
        int titleW = TextRenderer.MeasureText(g, _title, Theme.Title, Size.Empty, TextFormatFlags.NoPadding).Width;
        int detailW = TextRenderer.MeasureText(g, _detail, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding).Width;
        ClientSize = new Size(3 + Pad + 20 + Theme.Space3 + Math.Max(titleW, detailW) + Pad, 56);
        _timer.Tick += (_, _) => { _timer.Stop(); Close(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var border = new Pen(Theme.LineStrong)) g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using (var bar = new SolidBrush(Theme.Accent)) g.FillRectangle(bar, 0, 0, 3, Height);
        // Crosshair mark, same drawing as the app icon at 20 px.
        int mx = 3 + Pad, my = (Height - 20) / 2;
        using (var pen = new Pen(Theme.Accent, 2f))
        {
            g.DrawLine(pen, mx + 10, my, mx + 10, my + 20);
            g.DrawLine(pen, mx, my + 10, mx + 20, my + 10);
            using var hole = new SolidBrush(Theme.Panel);
            g.FillEllipse(hole, mx + 4, my + 4, 12, 12);
            g.DrawEllipse(pen, mx + 5, my + 5, 10, 10);
        }
        int tx = mx + 20 + Theme.Space3;
        TextRenderer.DrawText(g, _title, Theme.Title, new Rectangle(tx, 9, Width - tx - Pad, 22), Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, _detail, Theme.MonoSmall, new Rectangle(tx, 32, Width - tx - Pad, 16), Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.NoPadding);
    }

    public static void Show(string title, string detail)
    {
        _current?.Close();
        var toast = new ToastForm(title, detail);
        toast.FormClosed += (_, _) => { if (ReferenceEquals(_current, toast)) _current = null; };
        _current = toast;
        toast.Show();
    }
```

Remove `Controls.Add(_label)`, `AutoSize`, `AutoSizeMode` and the `_label` field. Keep `ShowWithoutActivation`, `CreateParams`, `OnLoad`, `OnShown`, `OnFormClosed` as they are.

- [ ] **Step 2: Update TrayIcon callers**

```csharp
        _onProfileApplied = (p, note) =>
        {
            if (!_controller.Settings.ShowToast) return;
            var detail = string.Join(" · ", new[] { string.Join(" ", Format.HotkeyParts(p.Hotkey)).ToUpperInvariant(), TargetText(p.Target), note ?? "" }.Where(s => s.Length > 0));
            ToastForm.Show(p.Name, detail);
        };
        _onResetApplied = text =>
        {
            if (_controller.Settings.ShowToast) ToastForm.Show(text, string.Join(" ", Format.HotkeyParts(_controller.Settings.ResetHotkey)).ToUpperInvariant());
        };
```
Add to `TrayIcon`:
```csharp
    private static string TargetText(MonitorTarget target) => target.Mode switch
    {
        MonitorTarget.All => "all monitors",
        MonitorTarget.Monitor => target.MonitorName ?? "monitor",
        _ => "primary monitor",
    };
```

- [ ] **Step 3: Build, run, press a hotkey**

Expected: toast with accent edge, crosshair, name on top and `CTRL ALT 1 · all monitors` under it.

- [ ] **Step 4: Commit**

```bash
git add src/GammaKey/UI/ToastForm.cs src/GammaKey/UI/TrayIcon.cs
git commit -m "Toast: accent edge, crosshair mark, title with mono detail line"
```

---

### Task 14: Docs, format, final check

**Files:**
- Modify: `docs/superpowers/specs/2026-11-08-gammakey-design.md:172-199` (section 11 becomes a pointer)
- Modify: `README.md` only if it shows a screenshot or describes the purple theme (check with `grep -n -i "purple\|screenshot" README.md`; if nothing, skip)

- [ ] **Step 1: Point the old spec's section 11 at the new spec**

Replace the body of section 11 (table rows included) with:

```markdown
## 11. UI tokens

Superseded 2026-11-09 by `2026-11-09-ui-tactical-restyle-design.md` section 4 (graphite palette,
cyan accent, Windows-native fonts). Constants live in `src/GammaKey/UI/Theme.cs`.
```

- [ ] **Step 2: Format and full test run**

Run: `dotnet format` then `dotnet format --verify-no-changes` then `dotnet test tests/GammaKey.Tests`
Expected: no changes reported on the second format call; all tests pass.

- [ ] **Step 3: Visual check at 150 % DPI**

Set Windows display scale to 150 %, run the app, open the window and Settings. Check: no clipped scale labels, chips fully visible, slider thumbs centred on the track, toggle text not overlapping the switch. Fix any clipped measurement by widening the affected column constant in that control, not by changing fonts. Return scale to normal.

- [ ] **Step 4: Update the code graph and commit**

```bash
graphify update .
git add -A docs src tests graphify-out
git commit -m "docs: restyle spec supersedes the purple token table; graph refreshed"
```

---

## Self-review

- Spec coverage: section 4 tokens → Task 1. Title bar → Task 2. Status strip chips → Tasks 9, 10, 11. Rail rows and icon row → Tasks 4, 9, 11. Editor groups, sliders with scales, readouts → Tasks 5, 6, 9, 11. Hotkey keycaps and dropdown → Task 8. Status bar → Tasks 10, 11. Settings groups and toggles with flag → Tasks 7, 12. Toast → Task 13. Deferred list stays in the spec; no task builds it.
- Type consistency: `Format.Signed(decimal,int)`, `Format.HotkeyParts(string)`, `Format.ProfileSummary(Profile)` used in Tasks 5, 8, 9, 11, 13 match Task 3. `GkHotkeyBox.DrawChip` is `internal static` and used by `ProfileList`. `GkChip.Dot` property name matches `DotState` uses in Task 11. `GkGroup.Content` matches Tasks 11, 12. `AppController.ArmedHotkeys` matches Task 11. `GkSlider.ScaleFormatter` matches Task 11.
- Placeholders: none. Every code step has the code.
