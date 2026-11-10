namespace GammaKey.UI;

public sealed class GkGroup : Panel
{
    private const int HeaderHeight = 22;

    public GkGroup(string title, string hint = "")
    {
        Title = title;
        Hint = hint;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Panel;
        Padding = new Padding(Theme.Space3, HeaderHeight + Theme.Space2, Theme.Space3, Theme.Space2);
        Content = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, BackColor = Theme.Panel, Margin = Padding.Empty };
        Controls.Add(Content);
        Content.Layout += (_, _) => FitToContent();
    }

    public string Title { get; }
    public string Hint { get; }
    public FlowLayoutPanel Content { get; }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        FitToContent();
    }

    // Width comes from the owner; only the height follows the content. A TopDown flow panel does
    // not stretch its children, so each row is widened to the inner width here.
    private void FitToContent()
    {
        int inner = Width - Padding.Horizontal;
        // An AutoSize panel ignores Dock width, so pin its minimum width instead.
        if (Content.MinimumSize.Width != inner) Content.MinimumSize = new Size(inner, 0);
        foreach (Control c in Content.Controls)
        {
            int w = inner - c.Margin.Horizontal;
            if (!c.AutoSize && w > 0 && c.Width != w) c.Width = w;
        }
        int h = Content.Bottom + Padding.Bottom;
        if (Height != h) Height = h;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var b = new SolidBrush(Theme.Panel)) Theme.FillRounded(g, b, r, Theme.Radius + 1);
        using (var p = new Pen(Theme.Line)) Theme.DrawRounded(g, p, r, Theme.Radius + 1);
        // Padding is scaled by the form's pass; the header band drawn here must match it.
        int S(int v) => Theme.Scale(v, DeviceDpi);
        var head = new Rectangle(S(Theme.Space3), S(Theme.Space2), Width - 2 * S(Theme.Space3), S(HeaderHeight) - S(Theme.Space2));
        TextRenderer.DrawText(g, Title.ToUpperInvariant(), Theme.Eyebrow, head, Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        if (Hint.Length > 0)
            TextRenderer.DrawText(g, Hint, Theme.MonoSmall, head, Theme.TextDim, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
