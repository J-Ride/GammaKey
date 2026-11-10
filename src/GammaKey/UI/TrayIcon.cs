using GammaKey.App;
using GammaKey.Models;

namespace GammaKey.UI;

public sealed class TrayIcon : IDisposable
{
    private readonly AppController _controller;
    private readonly MainForm _mainForm;
    private readonly NotifyIcon _icon = new();
    private readonly ContextMenuStrip _menu = new();
    private readonly Action _onSettingsChanged;
    private readonly Action<Profile, string?> _onProfileApplied;
    private readonly Action<string> _onResetApplied;

    public TrayIcon(AppController controller, MainForm mainForm)
    {
        _controller = controller;
        _mainForm = mainForm;
        _icon.Icon = CreateIcon();
        _icon.Text = "GammaKey";
        _icon.Visible = true;
        _icon.ContextMenuStrip = _menu;
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) _mainForm.ShowAndFocus(); };
        _menu.Opening += (_, _) => RebuildMenu();
        _onSettingsChanged = () =>
        {
            if (_menu.IsHandleCreated) _menu.BeginInvoke((Action)RebuildMenu);
            else RebuildMenu();
        };
        _onProfileApplied = (p, note) =>
        {
            if (!_controller.Settings.ShowToast) return;
            var detail = string.Join(" · ", new[] { string.Join(" ", Format.HotkeyParts(p.Hotkey)).ToUpperInvariant(), TargetText(p.Target), note ?? "" }.Where(s => s.Length > 0));
            ToastForm.Show(p.Name, detail);
        };
        _onResetApplied = text =>
        {
            if (_controller.Settings.ShowToast) ToastForm.Show(text, string.Join(" ", Format.HotkeyParts(_controller.Settings.ResetHotkey)).ToUpperInvariant());
        };
        _controller.SettingsChanged += _onSettingsChanged;
        _controller.ProfileApplied += _onProfileApplied;
        _controller.ResetApplied += _onResetApplied;
        RebuildMenu();
    }

    private static string TargetText(MonitorTarget target) => target.Mode switch
    {
        MonitorTarget.All => "all monitors",
        MonitorTarget.Monitor => target.MonitorName ?? "monitor",
        _ => "primary monitor",
    };

    internal static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var back = new SolidBrush(Theme.Background);
        g.FillRectangle(back, 0, 0, 32, 32);
        using var pen = new Pen(Theme.Accent, 3f);
        // Crosshair: ring with a gap, plus the four hairs. Reads at 16 px in the tray.
        g.DrawLine(pen, 16, 2, 16, 30);
        g.DrawLine(pen, 2, 16, 30, 16);
        using var hole = new SolidBrush(Theme.Background);
        g.FillEllipse(hole, 7, 7, 18, 18);
        g.DrawEllipse(pen, 8, 8, 16, 16);
        return Icon.FromHandle(bmp.GetHicon());
    }

    private void RebuildMenu()
    {
        _menu.Items.Clear();
        foreach (var profile in _controller.Settings.Profiles)
        {
            var label = profile.Hotkey.Length > 0 ? $"{profile.Name}    {profile.Hotkey}" : profile.Name;
            var captured = profile;
            _menu.Items.Add(label, null, (_, _) => _controller.ApplyProfile(captured));
        }
        _menu.Items.Add(new ToolStripSeparator());
        var resetLabel = _controller.Settings.ResetHotkey.Length > 0 ? $"Reset    {_controller.Settings.ResetHotkey}" : "Reset";
        _menu.Items.Add(resetLabel, null, (_, _) => _controller.Reset());
        _menu.Items.Add("Open GammaKey", null, (_, _) => _mainForm.ShowAndFocus());
        var startup = new ToolStripMenuItem("Start with Windows") { Checked = _controller.Settings.StartWithWindows, CheckOnClick = true };
        startup.CheckedChanged += (_, _) => _controller.SetStartWithWindows(startup.Checked);
        _menu.Items.Add(startup);
        _menu.Items.Add(new ToolStripSeparator());
        // Application.Exit closes the form with CloseReason.ApplicationExitCall, which skips
        // OnFormClosing's guard, so the guard runs here first.
        _menu.Items.Add("Quit", null, (_, _) => { _mainForm.ResolveUnsavedEdits(); Application.Exit(); });
    }

    public void Dispose()
    {
        _controller.SettingsChanged -= _onSettingsChanged;
        _controller.ProfileApplied -= _onProfileApplied;
        _controller.ResetApplied -= _onResetApplied;
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
