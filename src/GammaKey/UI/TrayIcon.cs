using GammaKey.App;

namespace GammaKey.UI;

public sealed class TrayIcon : IDisposable
{
    private readonly AppController _controller;
    private readonly MainForm _mainForm;
    private readonly NotifyIcon _icon = new();
    private readonly ContextMenuStrip _menu = new();

    public TrayIcon(AppController controller, MainForm mainForm)
    {
        _controller = controller;
        _mainForm = mainForm;
        _icon.Icon = SystemIcons.Application;
        _icon.Text = "GammaKey";
        _icon.Visible = true;
        _icon.ContextMenuStrip = _menu;
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) _mainForm.ShowAndFocus(); };
        _menu.Opening += (_, _) => RebuildMenu();
        _controller.SettingsChanged += RebuildMenu;
        _controller.ProfileApplied += (p, note) =>
        {
            if (_controller.Settings.ShowToast) ToastForm.Show(note is null ? p.Name : $"{p.Name}  ·  {note}");
        };
        _controller.ResetApplied += text =>
        {
            if (_controller.Settings.ShowToast) ToastForm.Show(text);
        };
        RebuildMenu();
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
        _menu.Items.Add("Quit", null, (_, _) => Application.Exit());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
