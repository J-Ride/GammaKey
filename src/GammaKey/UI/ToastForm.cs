namespace GammaKey.UI;

public sealed class ToastForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private static ToastForm? _current;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1200 };
    private readonly Label _label = new() { AutoSize = true, Padding = new Padding(Theme.Space6, Theme.Space3, Theme.Space6, Theme.Space3) };

    private ToastForm(string text)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.SurfaceRaised;
        Padding = new Padding(1);
        _label.ForeColor = Theme.TextPrimary;
        _label.Font = Theme.Title;
        _label.Text = text;
        Controls.Add(_label);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Close();
        };
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
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
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

    public static void Show(string text)
    {
        _current?.Close();
        var toast = new ToastForm(text);
        toast.FormClosed += (_, _) => { if (ReferenceEquals(_current, toast)) _current = null; };
        _current = toast;
        toast.Show();
    }
}
