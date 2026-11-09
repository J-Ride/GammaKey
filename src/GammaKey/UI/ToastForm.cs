namespace GammaKey.UI;

public sealed class ToastForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private static ToastForm? _current;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1200 };
    private readonly Label _label = new() { AutoSize = true, Padding = new Padding(18, 10, 18, 10) };

    private ToastForm(string text)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = System.Drawing.Color.FromArgb(28, 28, 32);
        _label.ForeColor = System.Drawing.Color.White;
        _label.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _label.Text = text;
        Controls.Add(_label);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _timer.Tick += (_, _) => Close();
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

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + 48);
        _timer.Start();
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
