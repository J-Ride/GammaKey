using System.Diagnostics;
using GammaKey.App;
using GammaKey.Hotkeys;

namespace GammaKey.UI;

public sealed class SettingsForm : Form
{
    private const int ContentWidth = 408;
    private readonly AppController _controller;
    private readonly GkToggle _startWithWindows = new() { Text = "Start with Windows", Description = "Runs in the tray at sign-in" };
    private readonly GkToggle _showWindowOnLaunch = new() { Text = "Show window on launch", Description = "Off means tray only" };
    private readonly GkToggle _showToast = new() { Text = "Show profile name on hotkey", Description = "1.5 s toast, bottom right" };
    private readonly GkToggle _guard = new() { Text = "Re-apply if Windows resets the curve", Description = "Guards against fullscreen and driver resets" };
    private readonly GkToggle _passthrough = new() { Text = "Let hotkeys pass through to other apps", Description = "System-wide keyboard hook. Prefer Ctrl or Alt combos instead.", Flag = "ANTI-CHEAT RISK" };
    private readonly GkHotkeyBox _resetHotkeyBox = new();
    // Hidden while empty so the window does not keep a blank line under the buttons.
    private readonly Label _status = new() { AutoSize = true, Visible = false, ForeColor = Theme.Warning, Margin = new Padding(0, Theme.Space2, 0, 0) };
    private readonly Action _onSettingsChanged;
    private bool _loading;

    public SettingsForm(AppController controller)
    {
        _controller = controller;
        // Setting AutoScaleMode or ClientSize on a live form runs the DPI scale pass at once, on
        // an empty form. Suspend first so the pass runs once, over the whole tree, at ResumeLayout.
        SuspendLayout();
        Text = "GammaKey settings";
        WindowChrome.Apply(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        // The window wraps its content, so nothing is cut off at any DPI scale, including after
        // a move to a monitor at another scale, and a status line that appears later still fits.
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        BuildLayout();
        Theme.Apply(this);
        ResumeLayout(true);
        LoadSettingsIntoControls();

        // The tray menu can toggle Start with Windows while this dialog is open.
        _onSettingsChanged = () => { if (!IsDisposed) LoadSettingsIntoControls(); };
        _controller.SettingsChanged += _onSettingsChanged;
        FormClosed += (_, _) => _controller.SettingsChanged -= _onSettingsChanged;
    }

    private void BuildLayout()
    {
        // Widths are logical and fixed; the stack and the window grow to fit the content.
        var stack = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(Theme.Space4), Margin = Padding.Empty, BackColor = Theme.Background };
        Controls.Add(stack);

        var startup = new GkGroup("Startup") { Width = ContentWidth, Margin = new Padding(0, 0, 0, Theme.Space3) };
        AddToggles(startup, _startWithWindows, _showWindowOnLaunch);
        stack.Controls.Add(startup);

        var behaviour = new GkGroup("Behaviour") { Width = ContentWidth, Margin = new Padding(0, 0, 0, Theme.Space3) };
        AddToggles(behaviour, _showToast, _guard, _passthrough);
        stack.Controls.Add(behaviour);

        var reset = new GkGroup("Restore monitor hotkey") { Width = ContentWidth, Margin = new Padding(0, 0, 0, Theme.Space3) };
        _resetHotkeyBox.Margin = new Padding(0, 2, 0, 4);
        reset.Content.Controls.Add(_resetHotkeyBox);
        stack.Controls.Add(reset);

        var buttons = new FlowLayoutPanel { AutoSize = false, Width = ContentWidth, Height = Theme.ControlHeight, FlowDirection = FlowDirection.RightToLeft, Margin = Padding.Empty, BackColor = Theme.Background };
        var close = new GkButton("Close") { Margin = Padding.Empty, DialogResult = DialogResult.OK };
        var openFolder = new GkButton("Open profiles folder") { Margin = new Padding(0, 0, Theme.Space2, 0) };
        openFolder.Click += (_, _) => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_controller.BaseDirectory}\"") { UseShellExecute = true });
        buttons.Controls.Add(close);
        buttons.Controls.Add(openFolder);
        stack.Controls.Add(buttons);

        _status.MaximumSize = new Size(ContentWidth, 0);
        _status.TextChanged += (_, _) => _status.Visible = _status.Text.Length > 0;
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

    // Rows are not docked: GkGroup.FitToContent sets each row's width.
    private static void AddToggles(GkGroup group, params GkToggle[] toggles)
    {
        for (int i = 0; i < toggles.Length; i++)
        {
            toggles[i].Margin = Padding.Empty;
            group.Content.Controls.Add(toggles[i]);
            if (i < toggles.Length - 1)
                group.Content.Controls.Add(new System.Windows.Forms.Panel { Height = 1, BackColor = Theme.Line, Margin = Padding.Empty });
        }
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
        // Register before Save so the armed-hotkeys chip refreshed by SettingsChanged is current.
        var failed = _controller.RegisterHotkeys();
        _controller.Save();
        var messages = new List<string>();
        if (failed.Count > 0) messages.Add($"Hotkey already used by another app: {string.Join(", ", failed)}. Pick another.");
        // A profile hotkey can still be refused after the Reset key is fixed, so the colour and
        // the no-modifier warning depend only on the Reset hotkey.
        bool isRefused = failed.Contains("Reset");
        _resetHotkeyBox.IsRefused = isRefused;
        if (!isRefused && _resetHotkeyBox.Combo.Length > 0 && !HotkeyCombo.HasModifier(_resetHotkeyBox.Combo) && !_controller.Settings.HotkeyPassthrough) messages.Add(HotkeyCombo.NoModifierWarning);
        _status.Text = string.Join(" ", messages);
    }
}
