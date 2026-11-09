namespace GammaKey.UI;

// Token values come from the ui-ux-pro-max "Gaming" palette; see design spec section 11.
public static class Theme
{
    public static readonly System.Drawing.Color Background = System.Drawing.Color.FromArgb(15, 15, 35);
    public static readonly System.Drawing.Color Surface = System.Drawing.Color.FromArgb(30, 28, 53);
    public static readonly System.Drawing.Color SurfaceRaised = System.Drawing.Color.FromArgb(39, 39, 59);
    public static readonly System.Drawing.Color Border = System.Drawing.Color.FromArgb(76, 29, 149);
    public static readonly System.Drawing.Color TextPrimary = System.Drawing.Color.FromArgb(226, 232, 240);
    public static readonly System.Drawing.Color TextMuted = System.Drawing.Color.FromArgb(148, 163, 184);
    public static readonly System.Drawing.Color Accent = System.Drawing.Color.FromArgb(167, 139, 250);
    public static readonly System.Drawing.Color AccentHover = System.Drawing.Color.FromArgb(196, 181, 253);
    public static readonly System.Drawing.Color FocusRing = System.Drawing.Color.FromArgb(124, 58, 237);
    public static readonly System.Drawing.Color Warning = System.Drawing.Color.FromArgb(255, 176, 76);
    public static readonly System.Drawing.Color Disabled = System.Drawing.Color.FromArgb(70, 74, 86);

    public static readonly Font Title = new("Segoe UI Semibold", 15f);
    public static readonly Font Label = new("Segoe UI", 10f);
    public static readonly Font Value = new("Segoe UI Semibold", 10f);
    public static readonly Font Caption = new("Segoe UI", 9f);

    public const int Space1 = 4;
    public const int Space2 = 8;
    public const int Space3 = 12;
    public const int Space4 = 16;
    public const int Space6 = 24;
    public const int ControlHeight = 32;
    public const int LabelColumnWidth = 100;
    public const int SliderTrackHeight = 6;
    public const int SliderThumbSize = 18;

    public static void Apply(Control root)
    {
        // Font and ForeColor are ambient, so setting them on the root reaches every child
        // that has no explicit value, and keeps explicit title, muted and warning styling.
        // Edit controls do not inherit ForeColor, so ApplyRecursive sets theirs.
        root.BackColor = Background;
        root.ForeColor = TextPrimary;
        root.Font = Label;
        foreach (Control child in root.Controls) ApplyRecursive(child);
    }

    private static void ApplyRecursive(Control control)
    {
        switch (control)
        {
            case Button b:
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderColor = Border;
                b.FlatAppearance.MouseOverBackColor = SurfaceRaised;
                b.BackColor = Surface;
                b.Height = ControlHeight;
                b.Padding = Padding.Empty;
                break;
            case TextBox t:
                t.BackColor = Surface;
                t.ForeColor = TextPrimary;
                t.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox c:
                c.BackColor = Surface;
                c.ForeColor = TextPrimary;
                c.FlatStyle = FlatStyle.Flat;
                break;
            case NumericUpDown n:
                n.BackColor = Surface;
                n.ForeColor = TextPrimary;
                n.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ListBox l:
                l.BackColor = Surface;
                l.BorderStyle = BorderStyle.None;
                l.ItemHeight = ControlHeight;
                l.DrawMode = DrawMode.OwnerDrawFixed;
                l.DrawItem += DrawListItem;
                break;
            case CheckBox cb:
                cb.FlatStyle = FlatStyle.Flat;
                break;
            default:
                control.BackColor = Background;
                break;
        }
        foreach (Control child in control.Controls) ApplyRecursive(child);
    }

    private static void DrawListItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox list || e.Index < 0) return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using var back = new SolidBrush(selected ? SurfaceRaised : Surface);
        e.Graphics.FillRectangle(back, e.Bounds);
        if (selected)
        {
            using var bar = new SolidBrush(Accent);
            e.Graphics.FillRectangle(bar, e.Bounds.Left, e.Bounds.Top, 3, e.Bounds.Height);
        }
        var textBounds = new Rectangle(e.Bounds.Left + Space3, e.Bounds.Top, e.Bounds.Width - Space3, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, list.Items[e.Index]?.ToString(), Label, textBounds, TextPrimary, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }
}
