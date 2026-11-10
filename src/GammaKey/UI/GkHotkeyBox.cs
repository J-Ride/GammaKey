using GammaKey.Hotkeys;

namespace GammaKey.UI;

// GammaKey.Color is a namespace, so the alias keeps Color meaning the drawing type here.
using Color = System.Drawing.Color;

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
                x = DrawChip(g, part, x, Height, DeviceDpi, chipText, _isRefused ? Theme.Warning : Theme.LineStrong) + S(Theme.Space1);
            }
        }

        string hint = Focused ? "Esc to cancel · Backspace clears" : (_isRefused ? "in use by another app" : "");
        if (hint.Length > 0)
            TextRenderer.DrawText(g, hint, Theme.Caption, new Rectangle(0, 0, Width - S(Theme.Space2), Height), _isRefused && !Focused ? Theme.Warning : Theme.TextDim, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    // A keycap: mono text in a box with a 2 px bottom edge, centred on a line of lineHeight device
    // pixels. Shared look with the profile list.
    internal static int DrawChip(Graphics g, string text, int x, int lineHeight, float dpi, Color textColor, Color border)
    {
        var size = TextRenderer.MeasureText(g, text, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding);
        int h = size.Height + Theme.Scale(6, dpi);
        int w = size.Width + Theme.Scale(12, dpi);
        var chip = new Rectangle(x, (lineHeight - h) / 2 - 1, w, h);
        using (var b = new SolidBrush(Theme.Background)) Theme.FillRounded(g, b, chip, 3);
        using (var p = new Pen(border)) Theme.DrawRounded(g, p, chip, 3);
        using (var bottom = new Pen(border, 2f)) g.DrawLine(bottom, chip.Left + 3, chip.Bottom, chip.Right - 3, chip.Bottom);
        TextRenderer.DrawText(g, text, Theme.MonoSmall, chip, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        return chip.Right;
    }
}
