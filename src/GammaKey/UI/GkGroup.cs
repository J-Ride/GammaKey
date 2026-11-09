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
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(Theme.Space3, HeaderHeight + Theme.Space2, Theme.Space3, Theme.Space2);
        Content = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, BackColor = Theme.Panel, Margin = Padding.Empty };
        Controls.Add(Content);
    }

    public string Title { get; }
    public string Hint { get; }
    public FlowLayoutPanel Content { get; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var b = new SolidBrush(Theme.Panel)) Theme.FillRounded(g, b, r, Theme.Radius + 1);
        using (var p = new Pen(Theme.Line)) Theme.DrawRounded(g, p, r, Theme.Radius + 1);
        var head = new Rectangle(Theme.Space3, Theme.Space2, Width - 2 * Theme.Space3, HeaderHeight - Theme.Space2);
        TextRenderer.DrawText(g, Title.ToUpperInvariant(), Theme.Eyebrow, head, Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        if (Hint.Length > 0)
            TextRenderer.DrawText(g, Hint, Theme.MonoSmall, head, Theme.TextDim, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
