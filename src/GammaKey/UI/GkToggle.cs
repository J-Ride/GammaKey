namespace GammaKey.UI;

// GammaKey.Color is a namespace, so the alias keeps Color meaning the drawing type here.
using Color = System.Drawing.Color;

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
