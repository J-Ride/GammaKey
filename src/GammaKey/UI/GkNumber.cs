namespace GammaKey.UI;

using System.Globalization;

// GammaKey.Color is a namespace, so the alias keeps Color meaning the drawing type here.
using Color = System.Drawing.Color;

public sealed class GkNumber : Control
{
    private const int SpinWidth = 16;
    private decimal _minimum;
    private decimal _maximum = 100;
    private decimal _value;
    private int _hoverSpin; // 0 none, 1 up, -1 down
    private decimal _valueBeforeEdit;

    // A real TextBox gives caret, selection, clipboard and IME for free. It is hidden until
    // the user clicks the text area or tabs in, so the painted look is unchanged at rest.
    private readonly TextBox _editor = new()
    {
        BorderStyle = BorderStyle.None,
        TextAlign = HorizontalAlignment.Center,
        Visible = false,
        TabStop = false,
    };

    public event EventHandler? ValueChanged;

    public GkNumber()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        Size = new Size(72, 28);
        TabStop = true;
        Controls.Add(_editor);
        _editor.KeyDown += OnEditorKeyDown;
        _editor.LostFocus += (_, _) => { if (IsEditing) CommitEdit(); };
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

    public bool IsEditing => _editor.Visible;

    public string EditText { get => _editor.Text; set => _editor.Text = value; }

    public void BeginEdit()
    {
        if (!Enabled || IsEditing) return;
        _valueBeforeEdit = _value;
        _editor.Text = _value.ToString("F" + DecimalPlaces, CultureInfo.InvariantCulture);
        PlaceEditor();
        _editor.Visible = true;
        _editor.Focus();
        _editor.SelectAll();
    }

    // Unparseable text (empty, letters, a lone minus) keeps the old value. The setter clamps
    // and fires ValueChanged only when the value actually moves.
    public void CommitEdit()
    {
        if (!IsEditing) return;
        if (decimal.TryParse(_editor.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var typed))
            Value = Math.Round(typed, DecimalPlaces, MidpointRounding.AwayFromZero);
        EndEdit();
    }

    public void CancelEdit()
    {
        if (!IsEditing) return;
        Value = _valueBeforeEdit;
        EndEdit();
    }

    private void EndEdit()
    {
        _editor.Visible = false;
        Invalidate();
    }

    private void PlaceEditor()
    {
        var fonts = Theme.FontsFor(DeviceDpi);
        _editor.Font = fonts.Mono;
        _editor.BackColor = Theme.Surface;
        _editor.ForeColor = Theme.TextPrimary;
        int textWidth = SpinRect.Left - 2;
        int top = Math.Max(1, (Height - _editor.PreferredHeight) / 2);
        _editor.SetBounds(2, top, textWidth - 2, _editor.PreferredHeight);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                CommitEdit();
                e.Handled = true; e.SuppressKeyPress = true;
                break;
            case Keys.Escape:
                CancelEdit();
                e.Handled = true; e.SuppressKeyPress = true;
                break;
            case Keys.Up:
            case Keys.Down:
                // Arrows apply at once, as they do outside edit mode, and the text follows.
                Step(e.KeyCode == Keys.Up ? +1 : -1);
                _valueBeforeEdit = _value;
                _editor.Text = _value.ToString("F" + DecimalPlaces, CultureInfo.InvariantCulture);
                _editor.SelectAll();
                e.Handled = true; e.SuppressKeyPress = true;
                break;
        }
    }

    private Rectangle SpinRect => new(Width - Theme.Scale(SpinWidth, DeviceDpi) - 1, 1, Theme.Scale(SpinWidth, DeviceDpi), Height - 2);

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
        float k = DeviceDpi / 96f;
        using (var pen = new Pen(arrow, 1.5f * k))
        {
            float cx = spin.Left + spin.Width / 2f;
            float uy = upRect.Top + upRect.Height / 2f;
            g.DrawLines(pen, new[] { new PointF(cx - 3 * k, uy + 1.5f * k), new PointF(cx, uy - 1.5f * k), new PointF(cx + 3 * k, uy + 1.5f * k) });
            float dy = downRect.Top + downRect.Height / 2f;
            g.DrawLines(pen, new[] { new PointF(cx - 3 * k, dy - 1.5f * k), new PointF(cx, dy + 1.5f * k), new PointF(cx + 3 * k, dy - 1.5f * k) });
        }

        var textRect = new Rectangle(0, 0, spin.Left, Height);
        if (!IsEditing)
            TextRenderer.DrawText(g, DisplayText, Theme.FontsFor(DeviceDpi).Mono, textRect, Enabled ? Theme.TextPrimary : Theme.TextDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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
            int s = SpinAt(e.Location);
            if (s != 0) { Focus(); Step(s); }
            else BeginEdit();
        }
        base.OnMouseDown(e);
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up) { Step(+1); e.Handled = true; }
        else if (e.KeyCode == Keys.Down) { Step(-1); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
        // Tab lands here; a mouse click already called BeginEdit from OnMouseDown.
        if (!IsEditing && MouseButtons == MouseButtons.None) BeginEdit();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (IsEditing) PlaceEditor();
    }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
}
