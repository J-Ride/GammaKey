namespace GammaKey.UI;

public sealed class ToastForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private static ToastForm? _current;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1200 };
    private const int Pad = Theme.Space4;
    private readonly string _title;
    private readonly string _detail;

    private ToastForm(string title, string detail)
    {
        _title = title;
        _detail = detail;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Panel;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        using var g = CreateGraphics();
        int titleW = TextRenderer.MeasureText(g, _title, Theme.Title, Size.Empty, TextFormatFlags.NoPadding).Width;
        int detailW = TextRenderer.MeasureText(g, _detail, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding).Width;
        ClientSize = new Size(3 + Pad + 20 + Theme.Space3 + Math.Max(titleW, detailW) + Pad, 56);
        _timer.Tick += (_, _) => { _timer.Stop(); Close(); };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExNoActivate | WsExToolWindow;
            return cp;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + 48);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var border = new Pen(Theme.LineStrong)) g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using (var bar = new SolidBrush(Theme.Accent)) g.FillRectangle(bar, 0, 0, 3, Height);
        // Crosshair mark, same drawing as the app icon at 20 px.
        int mx = 3 + Pad, my = (Height - 20) / 2;
        using (var pen = new Pen(Theme.Accent, 2f))
        {
            g.DrawLine(pen, mx + 10, my, mx + 10, my + 20);
            g.DrawLine(pen, mx, my + 10, mx + 20, my + 10);
            using var hole = new SolidBrush(Theme.Panel);
            g.FillEllipse(hole, mx + 4, my + 4, 12, 12);
            g.DrawEllipse(pen, mx + 5, my + 5, 10, 10);
        }
        int tx = mx + 20 + Theme.Space3;
        TextRenderer.DrawText(g, _title, Theme.Title, new Rectangle(tx, 9, Width - tx - Pad, 22), Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, _detail, Theme.MonoSmall, new Rectangle(tx, 32, Width - tx - Pad, 16), Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.NoPadding);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _timer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _timer.Dispose();
    }

    public static void Show(string title, string detail)
    {
        _current?.Close();
        var toast = new ToastForm(title, detail);
        toast.FormClosed += (_, _) => { if (ReferenceEquals(_current, toast)) _current = null; };
        _current = toast;
        toast.Show();
    }
}
