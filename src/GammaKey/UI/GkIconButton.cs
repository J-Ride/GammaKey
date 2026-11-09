namespace GammaKey.UI;

// GammaKey.Color is a namespace, so the alias keeps Color meaning the drawing type here.
using Color = System.Drawing.Color;

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
