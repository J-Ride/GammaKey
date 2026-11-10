namespace GammaKey.UI;

public sealed class ToastForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private static ToastForm? _current;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1200 };
    private const int Pad = Theme.Space4;
    private const int Bar = 3;
    private const int Mark = 20;
    private const int BodyHeight = 56;
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
        // No AutoScaleMode on a borderless form, so every constant is scaled here by hand.
        using var g = CreateGraphics();
        int titleW = TextRenderer.MeasureText(g, _title, Theme.Title, Size.Empty, TextFormatFlags.NoPadding).Width;
        int detailW = TextRenderer.MeasureText(g, _detail, Theme.MonoSmall, Size.Empty, TextFormatFlags.NoPadding).Width;
        ClientSize = new Size(S(Bar + Pad + Mark + Theme.Space3) + Math.Max(titleW, detailW) + S(Pad), S(BodyHeight));
        _timer.Tick += (_, _) => { _timer.Stop(); Close(); };
    }

    private int S(int v) => Theme.Scale(v, DeviceDpi);

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
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + S(48));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float k = DeviceDpi / 96f;
        using (var border = new Pen(Theme.LineStrong)) g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using (var bar = new SolidBrush(Theme.Accent)) g.FillRectangle(bar, 0, 0, S(Bar), Height);
        // Crosshair mark, same drawing as the app icon, authored at 20 px.
        int mark = S(Mark);
        int mx = S(Bar + Pad), my = (Height - mark) / 2;
        using (var pen = new Pen(Theme.Accent, 2f * k))
        {
            g.DrawLine(pen, mx + mark / 2f, my, mx + mark / 2f, my + mark);
            g.DrawLine(pen, mx, my + mark / 2f, mx + mark, my + mark / 2f);
            using var hole = new SolidBrush(Theme.Panel);
            g.FillEllipse(hole, mx + 4 * k, my + 4 * k, 12 * k, 12 * k);
            g.DrawEllipse(pen, mx + 5 * k, my + 5 * k, 10 * k, 10 * k);
        }
        int tx = mx + mark + S(Theme.Space3);
        TextRenderer.DrawText(g, _title, Theme.Title, new Rectangle(tx, S(9), Width - tx - S(Pad), S(22)), Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, _detail, Theme.MonoSmall, new Rectangle(tx, S(32), Width - tx - S(Pad), S(16)), Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.NoPadding);
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
