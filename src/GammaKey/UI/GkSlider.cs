namespace GammaKey.UI;

public sealed class GkSlider : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private bool _dragging;

    public event EventHandler? ValueChanged;

    public GkSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Height = Theme.ControlHeight;
        Width = 320;
        TabStop = true;
    }

    public int Minimum { get => _minimum; set { _minimum = value; Value = _value; } }
    public int Maximum { get => _maximum; set { _maximum = value; Value = _value; } }

    public int Value
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

    // Where "normal" sits so the fill can grow from it in both directions.
    public int Origin { get; set; }

    private Rectangle TrackRect => new(Theme.SliderThumbSize / 2, (Height - Theme.SliderTrackHeight) / 2, Width - Theme.SliderThumbSize, Theme.SliderTrackHeight);

    private int ValueToX(int v)
    {
        var t = TrackRect;
        double fraction = _maximum == _minimum ? 0 : (v - _minimum) / (double)(_maximum - _minimum);
        return t.Left + (int)Math.Round(fraction * t.Width);
    }

    private int XToValue(int x)
    {
        var t = TrackRect;
        double fraction = Math.Clamp((x - t.Left) / (double)t.Width, 0, 1);
        return _minimum + (int)Math.Round(fraction * (_maximum - _minimum));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background);

        var track = TrackRect;
        using var trackBrush = new SolidBrush(Enabled ? Theme.Border : Theme.Disabled);
        FillRounded(g, trackBrush, track, track.Height / 2);

        int originX = ValueToX(Math.Clamp(Origin, _minimum, _maximum));
        int valueX = ValueToX(_value);
        var fill = Rectangle.FromLTRB(Math.Min(originX, valueX), track.Top, Math.Max(originX, valueX), track.Bottom);
        if (fill.Width > 0)
        {
            using var fillBrush = new SolidBrush(Enabled ? Theme.Accent : Theme.Disabled);
            FillRounded(g, fillBrush, fill, track.Height / 2);
        }

        int thumb = Theme.SliderThumbSize;
        var thumbRect = new Rectangle(valueX - thumb / 2, (Height - thumb) / 2, thumb, thumb);
        using var thumbBrush = new SolidBrush(Enabled ? (_dragging ? Theme.AccentHover : Theme.TextPrimary) : Theme.Disabled);
        g.FillEllipse(thumbBrush, thumbRect);
        if (Focused && Enabled)
        {
            using var focus = new Pen(Theme.FocusRing, 2f);
            g.DrawEllipse(focus, Rectangle.Inflate(thumbRect, 3, 3));
        }
    }

    private static void FillRounded(Graphics g, Brush brush, Rectangle r, int radius)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = Math.Max(1, radius * 2);
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!Enabled || e.Button != MouseButtons.Left) return;
        Focus();
        _dragging = true;
        Value = XToValue(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging) Value = XToValue(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _dragging = false;
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int step = e.Shift ? 10 : 1;
        switch (e.KeyCode)
        {
            case Keys.Left: Value -= step; e.Handled = true; break;
            case Keys.Right: Value += step; e.Handled = true; break;
            case Keys.Home: Value = _minimum; e.Handled = true; break;
            case Keys.End: Value = _maximum; e.Handled = true; break;
        }
        base.OnKeyDown(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        Value += Math.Sign(e.Delta);
        base.OnMouseWheel(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }
}
