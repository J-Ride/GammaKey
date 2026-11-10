using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace GammaKey.UI;

// GammaKey.Color is a namespace, so the alias keeps Color meaning the drawing type here.
using Color = System.Drawing.Color;

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

    // Both families ship on Windows 10 and 11, so the layout is the same on every machine.
    private static readonly string Sans = ResolveFamily("Segoe UI");
    private static readonly string MonoFamily = ResolveFamily("Consolas");

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

    // Layout values are authored at 96 dpi. The form's auto-scale pass handles control bounds;
    // owner-drawn paint code and the toast call this for everything else.
    public static int Scale(int logical, float dpi) => (int)Math.Round(logical * dpi / 96f, MidpointRounding.AwayFromZero);

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
            case System.Windows.Forms.Panel or FlowLayoutPanel or TableLayoutPanel:
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
