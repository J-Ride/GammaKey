namespace GammaKey.UI;

public sealed class GkSlider : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private bool _dragging;

    public event EventHandler? ValueChanged;

    public const int TrackRowHeight = 20;
    public const int ScaleRowHeight = 16;

    public GkSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Height = TrackRowHeight + ScaleRowHeight + Theme.Space1;
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

    // Label text under the track for minimum, origin and maximum.
    public Func<int, string>? ScaleFormatter { get; set; }

    private int S(int v) => Theme.Scale(v, DeviceDpi);

    private Rectangle TrackRect => new(S(Theme.SliderThumbSize) / 2, (S(TrackRowHeight) - S(Theme.SliderTrackHeight)) / 2, Width - S(Theme.SliderThumbSize), S(Theme.SliderTrackHeight));

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

    // Where the centre scale label sits. An origin at the minimum (Vibrance, Black floor) has no
    // label of its own, so the middle value is drawn in the middle. Any other origin is labelled
    // under its own position on the track, so Gamma's "1.00" sits where the thumb rests at 1.00.
    public static int CentreLabelX(int minimum, int maximum, int origin, Rectangle track, int width)
    {
        if (origin <= minimum || maximum == minimum) return width / 2;
        double fraction = (Math.Clamp(origin, minimum, maximum) - minimum) / (double)(maximum - minimum);
        return track.Left + (int)Math.Round(fraction * track.Width);
    }

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
                g.DrawLine(tick, tx, track.Bottom + S(2), tx, track.Bottom + S(5));
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
        int half = S(Theme.SliderThumbSize) / 2;
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
        var scaleRect = new Rectangle(0, S(TrackRowHeight), Width, S(ScaleRowHeight));
        var scaleColor = Enabled ? Theme.TextDim : Theme.Raised;
        // An origin at the minimum (Vibrance) has no useful label of its own, so the middle value stands in.
        int centreValue = Origin <= _minimum ? (_minimum + _maximum) / 2 : Math.Clamp(Origin, _minimum, _maximum);
        string centreText = formatter(centreValue);
        int centreWidth = TextRenderer.MeasureText(g, centreText, Theme.FontsFor(DeviceDpi).MonoSmall, Size.Empty, TextFormatFlags.NoPadding).Width;
        int centreX = CentreLabelX(_minimum, _maximum, Origin, track, Width);
        var centreRect = new Rectangle(centreX - centreWidth / 2, scaleRect.Top, centreWidth, scaleRect.Height);
        TextRenderer.DrawText(g, formatter(_minimum), Theme.FontsFor(DeviceDpi).MonoSmall, scaleRect, scaleColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, centreText, Theme.FontsFor(DeviceDpi).MonoSmall, centreRect, scaleColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, formatter(_maximum), Theme.FontsFor(DeviceDpi).MonoSmall, scaleRect, scaleColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (Enabled && e.Button == MouseButtons.Left)
        {
            Focus();
            _dragging = true;
            Value = XToValue(e.X);
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging) Value = XToValue(e.X);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _dragging = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    // Alt+Tab or a popup can take capture mid-drag without a mouse-up.
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        _dragging = false;
        Invalidate();
        base.OnMouseCaptureChanged(e);
    }

    // Mask modifiers so Shift+Arrow reaches OnKeyDown instead of moving focus.
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int step = e.Shift ? 10 : 1;
        switch (e.KeyCode)
        {
            case Keys.Left:
            case Keys.Down: Value -= step; e.Handled = true; break;
            case Keys.Right:
            case Keys.Up: Value += step; e.Handled = true; break;
            case Keys.Home: Value = _minimum; e.Handled = true; break;
            case Keys.End: Value = _maximum; e.Handled = true; break;
        }
        base.OnKeyDown(e);
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
