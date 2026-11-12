namespace GammaKey.UI;

// The stock ToolTip keeps the hover timing and cursor placement; owner drawing swaps the white
// Windows balloon for the dark theme so the popup matches the rest of the editor.
public sealed class GkTooltip : IDisposable
{
    public const int WrapWidth = 280;
    public const int PadX = 8;
    public const int PadY = 6;

    private readonly ToolTip _tip = new()
    {
        OwnerDraw = true,
        InitialDelay = 700,
        ReshowDelay = 200,
        AutoPopDelay = 20000,
    };

    public GkTooltip()
    {
        _tip.Popup += OnPopup;
        _tip.Draw += OnDraw;
    }

    public int InitialDelay => _tip.InitialDelay;

    public void Attach(Control control, string text) => _tip.SetToolTip(control, text);

    public string TextFor(Control control) => _tip.GetToolTip(control);

    public static Size Measure(string text, float dpi)
    {
        var font = Theme.FontsFor(dpi).Label;
        var measured = TextRenderer.MeasureText(text, font, new Size(Theme.Scale(WrapWidth, dpi), int.MaxValue), Flags);
        return new Size(measured.Width + Theme.Scale(PadX, dpi) * 2, measured.Height + Theme.Scale(PadY, dpi) * 2);
    }

    public void Dispose() => _tip.Dispose();

    private const TextFormatFlags Flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

    private void OnPopup(object? sender, PopupEventArgs e)
    {
        e.ToolTipSize = Measure(_tip.GetToolTip(e.AssociatedControl), DpiOf(e.AssociatedControl));
    }

    private void OnDraw(object? sender, DrawToolTipEventArgs e)
    {
        float dpi = DpiOf(e.AssociatedControl);
        var g = e.Graphics;
        var r = new Rectangle(0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
        using (var b = new SolidBrush(Theme.Raised)) Theme.FillRounded(g, b, r, Theme.Radius);
        using (var p = new Pen(Theme.Line)) Theme.DrawRounded(g, p, r, Theme.Radius);
        var inner = Rectangle.Inflate(e.Bounds, -Theme.Scale(PadX, dpi), -Theme.Scale(PadY, dpi));
        TextRenderer.DrawText(g, e.ToolTipText, Theme.FontsFor(dpi).Label, inner, Theme.TextPrimary, Flags);
    }

    private static float DpiOf(Control? control) => control?.DeviceDpi ?? 96f;
}
