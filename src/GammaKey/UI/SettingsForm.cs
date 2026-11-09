using System.Diagnostics;
using GammaKey.App;
using GammaKey.Hotkeys;

namespace GammaKey.UI;

public sealed class SettingsForm : Form
{
    private readonly AppController _controller;
    private readonly CheckBox _startWithWindows = new() { Text = "Start with Windows", AutoSize = true };
    private readonly CheckBox _showWindowOnLaunch = new() { Text = "Show window on launch", AutoSize = true };
    private readonly CheckBox _showToast = new() { Text = "Show profile name on hotkey", AutoSize = true };
    private readonly CheckBox _guard = new() { Text = "Re-apply if Windows resets the curve", AutoSize = true };
    private readonly CheckBox _passthrough = new() { Text = "Let hotkeys pass through to other apps", AutoSize = true };
    private readonly HotkeyCaptureBox _resetHotkeyBox = new() { Width = 320 };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Theme.Warning, MaximumSize = new Size(340, 0), Margin = new Padding(0, Theme.Space2, 0, 0) };
    private readonly Action _onSettingsChanged;
    private bool _loading;

    public SettingsForm(AppController controller)
    {
        _controller = controller;
        Text = "GammaKey settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(380, 330 + Theme.ControlHeight + Theme.Space2);

        BuildLayout();
        Theme.Apply(this);
        LoadSettingsIntoControls();

        // The tray menu can toggle Start with Windows while this dialog is open.
        _onSettingsChanged = () => { if (!IsDisposed) LoadSettingsIntoControls(); };
        _controller.SettingsChanged += _onSettingsChanged;
        FormClosed += (_, _) => _controller.SettingsChanged -= _onSettingsChanged;
    }

    private void BuildLayout()
    {
        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(Theme.Space6) };
        Controls.Add(stack);

        foreach (var check in new[] { _startWithWindows, _showWindowOnLaunch, _showToast, _guard, _passthrough })
        {
            check.Margin = new Padding(0, 2, 0, 2);
            stack.Controls.Add(check);
        }

        stack.Controls.Add(new Label { Text = "Restore monitor hotkey", ForeColor = Theme.TextMuted, AutoSize = true, Margin = new Padding(0, Theme.Space3, 0, Theme.Space1) });
        _resetHotkeyBox.Margin = Padding.Empty;
        stack.Controls.Add(_resetHotkeyBox);

        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, Theme.Space3, 0, 0) };
        var openFolder = new Button { Text = "Open profiles folder", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = new Padding(0, 0, Theme.Space2, 0) };
        openFolder.Click += (_, _) => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_controller.BaseDirectory}\"") { UseShellExecute = true });
        buttons.Controls.Add(openFolder);
        var close = new Button { Text = "Close", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = Padding.Empty, DialogResult = DialogResult.OK };
        buttons.Controls.Add(close);
        stack.Controls.Add(buttons);
        stack.Controls.Add(_status);
        CancelButton = close;

        _startWithWindows.CheckedChanged += (_, _) => { if (!_loading) _controller.SetStartWithWindows(_startWithWindows.Checked); };
        _showWindowOnLaunch.CheckedChanged += (_, _) => { if (!_loading) { _controller.Settings.ShowWindowOnLaunch = _showWindowOnLaunch.Checked; _controller.Save(); } };
        _showToast.CheckedChanged += (_, _) => { if (!_loading) { _controller.Settings.ShowToast = _showToast.Checked; _controller.Save(); } };
        _guard.CheckedChanged += (_, _) => { if (!_loading) { _controller.Settings.GuardEnabled = _guard.Checked; _controller.Save(); } };
        _resetHotkeyBox.ComboChanged += (_, _) => { if (!_loading) { _controller.Settings.ResetHotkey = _resetHotkeyBox.Combo; SaveAndRegister(); } };
        _passthrough.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            if (_passthrough.Checked)
            {
                var answer = MessageBox.Show(this,
                    "Pass-through watches every key press with a system-wide keyboard hook. Some anti-cheat systems flag that.\n\n" +
                    "Recommended: leave this off and give each hotkey a Ctrl or Alt modifier.\n\n" +
                    "Turn it on anyway?",
                    "GammaKey", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (answer != DialogResult.OK)
                {
                    _loading = true;
                    _passthrough.Checked = false;
                    _loading = false;
                    return;
                }
            }
            if (!_controller.SetHotkeyPassthrough(_passthrough.Checked))
            {
                _loading = true;
                _passthrough.Checked = false;
                _loading = false;
                _status.Text = "Could not start pass-through. Hotkeys stay in normal mode.";
            }
            else
            {
                _status.Text = "";
            }
        };
    }

    private void LoadSettingsIntoControls()
    {
        _loading = true;
        _startWithWindows.Checked = _controller.Settings.StartWithWindows;
        _showWindowOnLaunch.Checked = _controller.Settings.ShowWindowOnLaunch;
        _showToast.Checked = _controller.Settings.ShowToast;
        _guard.Checked = _controller.Settings.GuardEnabled;
        _passthrough.Checked = _controller.Settings.HotkeyPassthrough;
        _resetHotkeyBox.Combo = _controller.Settings.ResetHotkey;
        _loading = false;
    }

    private void SaveAndRegister()
    {
        _controller.Save();
        var failed = _controller.RegisterHotkeys();
        var messages = new List<string>();
        if (failed.Count > 0) messages.Add($"Hotkey already used by another app: {string.Join(", ", failed)}. Pick another.");
        // A profile hotkey can still be refused after the Reset key is fixed, so the colour and
        // the no-modifier warning depend only on the Reset hotkey.
        bool isRefused = failed.Contains("Reset");
        _resetHotkeyBox.ForeColor = isRefused ? Theme.Warning : Theme.TextPrimary;
        if (!isRefused && _resetHotkeyBox.Combo.Length > 0 && !HotkeyCombo.HasModifier(_resetHotkeyBox.Combo) && !_controller.Settings.HotkeyPassthrough) messages.Add(HotkeyCombo.NoModifierWarning);
        _status.Text = string.Join(" ", messages);
    }
}
