namespace GammaKey.UI;

// GammaKey.Color is a namespace, so the alias keeps Color meaning the drawing type here.
using Color = System.Drawing.Color;

// The text counterpart of GkIconButton: same surface, border, hover and focus halo, so every
// button in the app looks like one family. IButtonControl lets a form use it as AcceptButton
// or CancelButton and lets DialogResult close a dialog, like a stock Button.
public sealed class GkButton : Control, IButtonControl
{
    private const int MinWidth = 72;
    private bool _hover;
    private bool _pressed;
    private bool _isKeyDown;
    private bool _autoWidth = true;

    public GkButton(string text)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
        TabStop = true;
        // Logical height: the form's scale pass sizes it like every other control.
        Height = Theme.ControlHeight;
        Text = text;
        FitWidth();
    }

    public bool IsPrimary { get; set; }
    public DialogResult DialogResult { get; set; }

    // Off for a button whose width the layout owns.
    public bool AutoWidth
    {
        get => _autoWidth;
        set { _autoWidth = value; FitWidth(); }
    }

    public void NotifyDefault(bool value) { }

    public void PerformClick()
    {
        if (Enabled) OnClick(EventArgs.Empty);
    }

    // Width is measured from text at the real DPI, so the form's scale pass must not multiply it
    // again. DeviceDpi is still the old value during the pass, so the width is measured once
    // more after the parent has moved to its new DPI.
    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        base.ScaleControl(factor, _autoWidth ? specified & ~BoundsSpecified.Width : specified);
        FitWidth();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); FitWidth(); }
    protected override void OnTextChanged(EventArgs e) { FitWidth(); base.OnTextChanged(e); }

    private void FitWidth()
    {
        if (!_autoWidth) return;
        int S(int v) => Theme.Scale(v, DeviceDpi);
        int text = TextRenderer.MeasureText(Text, Theme.FontsFor(DeviceDpi).Label, Size.Empty, TextFormatFlags.NoPadding).Width;
        Width = Math.Max(S(MinWidth), text + 2 * S(Theme.Space4));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Panel);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        bool active = _pressed || _hover;
        Color back = IsPrimary ? (active ? Theme.AccentDim : Theme.Accent) : (active ? Theme.Raised : Theme.Surface);
        Color border = IsPrimary ? back : (_hover || Focused ? Theme.LineStrong : Theme.Line);
        Color text = IsPrimary ? Theme.Background : Theme.TextPrimary;
        // Disabled keeps the muted text, not the dim one, so the label stays readable.
        if (!Enabled) { back = Theme.Surface; border = Theme.Line; text = Theme.TextMuted; }
        using (var b = new SolidBrush(back)) Theme.FillRounded(g, b, r, Theme.Radius);
        using (var p = new Pen(border)) Theme.DrawRounded(g, p, r, Theme.Radius);
        // ShowFocusCues is on only after keyboard navigation, so focus handed back by a closing
        // dialog does not leave the button glowing until the next click.
        if (Focused && ShowFocusCues && !IsPrimary)
        {
            using var halo = new Pen(Theme.AccentGlow, 3f);
            Theme.DrawRounded(g, halo, Rectangle.Inflate(r, -1, -1), Theme.Radius);
        }
        TextRenderer.DrawText(g, Text, Theme.FontsFor(DeviceDpi).Label, ClientRectangle, text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    protected override void OnClick(EventArgs e)
    {
        if (DialogResult != DialogResult.None && FindForm() is { } form) form.DialogResult = DialogResult;
        base.OnClick(e);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { _isKeyDown = false; Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    // Act on key release, like a stock Button, so holding the key does not click once per repeat.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) { _isKeyDown = true; e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (_isKeyDown && e.KeyCode is Keys.Space or Keys.Enter)
        {
            _isKeyDown = false;
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyUp(e);
    }
}
