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

    public string Label { get => _label; set { _label = value; FitWidth(); } }
    public string Value { get => _value; set { _value = value; FitWidth(); } }
    public DotState Dot { get => _dot; set { _dot = value; FitWidth(); } }

    private void FitWidth()
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
