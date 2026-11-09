using GammaKey.App;
using GammaKey.Models;

namespace GammaKey.UI;

public sealed class MainForm : Form
{
    private readonly AppController _controller;
    private readonly ListBox _profileList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox _nameBox = new() { Width = 260 };
    private readonly HotkeyCaptureBox _hotkeyBox = new() { Width = 260 };
    private readonly ComboBox _targetBox = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly HotkeyCaptureBox _resetHotkeyBox = new() { Width = 260 };
    private readonly CheckBox _startWithWindows = new() { Text = "Start with Windows", AutoSize = true };
    private readonly CheckBox _showToast = new() { Text = "Show profile name on hotkey", AutoSize = true };
    private readonly CheckBox _guard = new() { Text = "Re-apply if Windows resets the curve", AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, ForeColor = System.Drawing.Color.DarkOrange };
    private readonly Label _vibranceNote = new() { AutoSize = true, ForeColor = System.Drawing.Color.Gray };

    private readonly TrackBar _gamma = new() { Minimum = 30, Maximum = 280, TickFrequency = 10, Width = 320 };
    private readonly TrackBar _brightness = new() { Minimum = -100, Maximum = 100, TickFrequency = 10, Width = 320 };
    private readonly TrackBar _contrast = new() { Minimum = -100, Maximum = 100, TickFrequency = 10, Width = 320 };
    private readonly TrackBar _shadows = new() { Minimum = -100, Maximum = 100, TickFrequency = 10, Width = 320 };
    private readonly TrackBar _highlights = new() { Minimum = -100, Maximum = 100, TickFrequency = 10, Width = 320 };
    private readonly TrackBar _vibrance = new() { Minimum = 50, Maximum = 100, TickFrequency = 5, Width = 320 };
    private readonly Dictionary<TrackBar, NumericUpDown> _numbers = new();

    private Profile? _editing;
    private bool _loading;

    public MainForm(AppController controller)
    {
        _controller = controller;
        Text = "GammaKey";
        Width = 900;
        Height = 640;
        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += OnFormClosing;

        BuildLayout();
        LoadMonitorsIntoTarget();
        LoadSettingsIntoControls();
        RefreshProfileList();
        if (_profileList.Items.Count > 0) _profileList.SelectedIndex = 0;
    }

    public void ShowAndFocus()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        // Left: profile list and buttons
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.Controls.Add(_profileList, 0, 0);
        var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        leftButtons.Controls.Add(Button("Add", (_, _) => AddProfile()));
        leftButtons.Controls.Add(Button("Duplicate", (_, _) => DuplicateProfile()));
        leftButtons.Controls.Add(Button("Delete", (_, _) => DeleteProfile()));
        left.Controls.Add(leftButtons, 0, 1);
        root.Controls.Add(left, 0, 0);

        // Right: editor
        var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(16, 0, 0, 0) };
        right.Controls.Add(Row("Name", _nameBox));
        right.Controls.Add(Row("Hotkey", _hotkeyBox));
        right.Controls.Add(Row("Monitors", _targetBox));
        right.Controls.Add(Slider("Gamma", _gamma, isGamma: true));
        right.Controls.Add(Slider("Brightness", _brightness));
        right.Controls.Add(Slider("Contrast", _contrast));
        right.Controls.Add(Slider("Shadows", _shadows));
        right.Controls.Add(Slider("Highlights", _highlights));
        right.Controls.Add(Slider("Vibrance", _vibrance));
        right.Controls.Add(_vibranceNote);

        var actions = new FlowLayoutPanel { AutoSize = true };
        actions.Controls.Add(Button("Apply", (_, _) => ApplyCurrent()));
        actions.Controls.Add(Button("Save", (_, _) => SaveCurrent()));
        actions.Controls.Add(Button("Reset", (_, _) => _controller.Reset()));
        right.Controls.Add(actions);
        right.Controls.Add(_status);

        right.Controls.Add(new Label { Text = "Settings", Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 16, 0, 4) });
        right.Controls.Add(Row("Reset hotkey", _resetHotkeyBox));
        right.Controls.Add(_startWithWindows);
        right.Controls.Add(_showToast);
        right.Controls.Add(_guard);
        root.Controls.Add(right, 1, 0);

        _nameBox.TextChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Name = _nameBox.Text; RefreshProfileList(keepSelection: true); } };
        _hotkeyBox.ComboChanged += (_, _) => { if (_editing is not null && !_loading) _editing.Hotkey = _hotkeyBox.Combo; };
        _targetBox.SelectedIndexChanged += (_, _) => { if (_editing is not null && !_loading) _editing.Target = TargetFromCombo(); };
        _resetHotkeyBox.ComboChanged += (_, _) => { if (!_loading) { _controller.Settings.ResetHotkey = _resetHotkeyBox.Combo; SaveSettingsAndRegister(); } };
        _startWithWindows.CheckedChanged += (_, _) => { if (!_loading) _controller.SetStartWithWindows(_startWithWindows.Checked); };
        _showToast.CheckedChanged += (_, _) => { if (!_loading) { _controller.Settings.ShowToast = _showToast.Checked; _controller.Save(); } };
        _guard.CheckedChanged += (_, _) => { if (!_loading) { _controller.Settings.GuardEnabled = _guard.Checked; _controller.Save(); } };
        _profileList.SelectedIndexChanged += (_, _) => LoadSelectedProfile();

        if (!_controller.Vibrance.IsAvailable)
        {
            _vibrance.Enabled = false;
            _vibranceNote.Text = _controller.Vibrance.UnavailableReason ?? "Vibrance needs an NVIDIA graphics card";
        }
    }

    private static Button Button(string text, EventHandler onClick)
    {
        var b = new Button { Text = text, AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
        b.Click += onClick;
        return b;
    }

    private static Control Row(string label, Control input)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
        panel.Controls.Add(new Label { Text = label, Width = 100, TextAlign = ContentAlignment.MiddleLeft, Height = input.Height });
        panel.Controls.Add(input);
        return panel;
    }

    private Control Slider(string label, TrackBar bar, bool isGamma = false)
    {
        var number = new NumericUpDown
        {
            Minimum = bar.Minimum,
            Maximum = bar.Maximum,
            Width = 70,
            DecimalPlaces = isGamma ? 2 : 0,
            Increment = isGamma ? 0.01m : 1m,
        };
        if (isGamma)
        {
            number.Minimum = 0.3m;
            number.Maximum = 2.8m;
        }
        _numbers[bar] = number;

        bar.ValueChanged += (_, _) =>
        {
            if (_loading) return;
            _loading = true;
            number.Value = isGamma ? bar.Value / 100m : bar.Value;
            _loading = false;
            PushValuesToProfileAndPreview();
        };
        number.ValueChanged += (_, _) =>
        {
            if (_loading) return;
            _loading = true;
            bar.Value = isGamma ? (int)Math.Round(number.Value * 100) : (int)number.Value;
            _loading = false;
            PushValuesToProfileAndPreview();
        };

        var panel = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
        panel.Controls.Add(new Label { Text = label, Width = 100, TextAlign = ContentAlignment.MiddleLeft, Height = bar.Height });
        panel.Controls.Add(bar);
        panel.Controls.Add(number);
        return panel;
    }

    private void LoadMonitorsIntoTarget()
    {
        _targetBox.Items.Clear();
        _targetBox.Items.Add("Primary monitor");
        _targetBox.Items.Add("All monitors");
        foreach (var monitor in _controller.Display.GetMonitors()) _targetBox.Items.Add(monitor.Label);
    }

    private MonitorTarget TargetFromCombo()
    {
        return _targetBox.SelectedIndex switch
        {
            0 => MonitorTarget.ForPrimary(),
            1 => MonitorTarget.ForAll(),
            _ => MonitorTarget.ForMonitor(StripHdrSuffix(_targetBox.SelectedItem?.ToString() ?? "")),
        };
    }

    private static string StripHdrSuffix(string label) => label.EndsWith(" (HDR)", StringComparison.Ordinal) ? label[..^6] : label;

    private void SelectTargetInCombo(MonitorTarget target)
    {
        if (target.Mode == MonitorTarget.All) { _targetBox.SelectedIndex = 1; return; }
        if (target.Mode == MonitorTarget.Monitor)
        {
            for (int i = 2; i < _targetBox.Items.Count; i++)
            {
                if (StripHdrSuffix(_targetBox.Items[i]!.ToString()!).Equals(target.MonitorName, StringComparison.OrdinalIgnoreCase))
                {
                    _targetBox.SelectedIndex = i;
                    return;
                }
            }
        }
        _targetBox.SelectedIndex = 0;
    }

    private void LoadSettingsIntoControls()
    {
        _loading = true;
        _resetHotkeyBox.Combo = _controller.Settings.ResetHotkey;
        _startWithWindows.Checked = _controller.Settings.StartWithWindows;
        _showToast.Checked = _controller.Settings.ShowToast;
        _guard.Checked = _controller.Settings.GuardEnabled;
        _loading = false;
    }

    private void RefreshProfileList(bool keepSelection = false)
    {
        var selected = _profileList.SelectedIndex;
        _loading = true;
        _profileList.Items.Clear();
        foreach (var p in _controller.Settings.Profiles)
            _profileList.Items.Add(p.Hotkey.Length > 0 ? $"{p.Name}   [{p.Hotkey}]" : p.Name);
        if (keepSelection && selected >= 0 && selected < _profileList.Items.Count) _profileList.SelectedIndex = selected;
        _loading = false;
    }

    private void LoadSelectedProfile()
    {
        if (_loading) return;
        var index = _profileList.SelectedIndex;
        if (index < 0 || index >= _controller.Settings.Profiles.Count) { _editing = null; return; }
        _editing = _controller.Settings.Profiles[index];

        _loading = true;
        _nameBox.Text = _editing.Name;
        _hotkeyBox.Combo = _editing.Hotkey;
        SelectTargetInCombo(_editing.Target);
        var v = _editing.Values;
        _gamma.Value = (int)Math.Round(v.Gamma * 100);
        _brightness.Value = v.Brightness;
        _contrast.Value = v.Contrast;
        _shadows.Value = v.Shadows;
        _highlights.Value = v.Highlights;
        _vibrance.Value = Math.Clamp(v.Vibrance, 50, 100);
        foreach (var (bar, number) in _numbers)
            number.Value = ReferenceEquals(bar, _gamma) ? bar.Value / 100m : bar.Value;
        _loading = false;
        _status.Text = "";
    }

    private ColorValues CurrentValues() => new(_gamma.Value / 100.0, _brightness.Value, _contrast.Value, _shadows.Value, _highlights.Value, _vibrance.Value);

    private void PushValuesToProfileAndPreview()
    {
        if (_editing is null) return;
        _editing.Values = CurrentValues();
        var result = _controller.Preview(_editing.Values, _editing.Target);
        _status.Text = result.Note ?? "";
    }

    private void ApplyCurrent()
    {
        if (_editing is null) return;
        var result = _controller.ApplyProfile(_editing);
        _status.Text = result.Note ?? "Applied";
    }

    private void SaveCurrent()
    {
        SaveSettingsAndRegister();
        RefreshProfileList(keepSelection: true);
        _status.Text = _status.Text.Length == 0 ? "Saved" : _status.Text;
    }

    private void SaveSettingsAndRegister()
    {
        _controller.Save();
        var failed = _controller.RegisterHotkeys();
        _status.Text = failed.Count == 0 ? "" : $"Hotkey already used by another app: {string.Join(", ", failed)}. Pick another.";
    }

    private void AddProfile()
    {
        var profile = new Profile();
        _controller.Settings.Profiles.Add(profile);
        RefreshProfileList();
        _profileList.SelectedIndex = _controller.Settings.Profiles.Count - 1;
    }

    private void DuplicateProfile()
    {
        if (_editing is null) return;
        var copy = _editing.Clone();
        copy.Name = _editing.Name + " copy";
        _controller.Settings.Profiles.Add(copy);
        RefreshProfileList();
        _profileList.SelectedIndex = _controller.Settings.Profiles.Count - 1;
    }

    private void DeleteProfile()
    {
        if (_editing is null) return;
        var index = _controller.Settings.Profiles.IndexOf(_editing);
        _controller.Settings.Profiles.Remove(_editing);
        SaveSettingsAndRegister();
        RefreshProfileList();
        if (_profileList.Items.Count > 0) _profileList.SelectedIndex = Math.Min(index, _profileList.Items.Count - 1);
        else _editing = null;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // Closing the window hides to the tray; Quit in the tray menu ends the app.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
