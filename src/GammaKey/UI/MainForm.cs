using GammaKey.App;
using GammaKey.Hotkeys;
using GammaKey.Models;

namespace GammaKey.UI;

public sealed class MainForm : Form
{
    private readonly AppController _controller;
    private readonly ListBox _profileList = new() { Dock = DockStyle.Fill, IntegralHeight = false, Margin = Padding.Empty };
    // Slider (320) + gap + readout (70), so the inputs share the slider rows' right edge.
    private const int WideInputWidth = 320 + Theme.Space3 + 70;

    private readonly TextBox _nameBox = new() { Width = WideInputWidth };
    private readonly HotkeyCaptureBox _hotkeyBox = new() { Width = WideInputWidth };
    private readonly ComboBox _targetBox = new() { Width = WideInputWidth, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly HotkeyCaptureBox _resetHotkeyBox = new() { Width = 260 };
    private readonly CheckBox _startWithWindows = new() { Text = "Start with Windows", AutoSize = true };
    private readonly CheckBox _showToast = new() { Text = "Show profile name on hotkey", AutoSize = true };
    private readonly CheckBox _guard = new() { Text = "Re-apply if Windows resets the curve", AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Theme.Warning, MaximumSize = new Size(420, 0), Margin = new Padding(Theme.LabelColumnWidth, Theme.Space1, 0, 0) };
    private readonly Label _vibranceNote = new() { AutoSize = true, ForeColor = Theme.TextMuted, Font = Theme.Caption, Margin = new Padding(Theme.LabelColumnWidth, 0, 0, Theme.Space1) };

    private readonly GkSlider _gamma = new() { Minimum = 30, Maximum = 280, Origin = 100, Width = 320 };
    private readonly GkSlider _brightness = new() { Minimum = -100, Maximum = 100, Origin = 0, Width = 320 };
    private readonly GkSlider _contrast = new() { Minimum = -100, Maximum = 100, Origin = 0, Width = 320 };
    private readonly GkSlider _shadows = new() { Minimum = -100, Maximum = 100, Origin = 0, Width = 320 };
    private readonly GkSlider _highlights = new() { Minimum = -100, Maximum = 100, Origin = 0, Width = 320 };
    private readonly GkSlider _vibrance = new() { Minimum = 50, Maximum = 100, Origin = 50, Width = 320 };
    private readonly Dictionary<GkSlider, NumericUpDown> _numbers = new();

    private Profile? _editing;
    private bool _loading;

    public MainForm(AppController controller)
    {
        _controller = controller;
        Text = "GammaKey";
        Width = 900;
        Height = 640;
        // Scale the pixel layout with the display so text is not clipped at 125 and 150 percent.
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = TrayIcon.CreateIcon();
        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += OnFormClosing;
        _controller.SettingsChanged += () =>
        {
            if (IsDisposed) return;
            LoadSettingsIntoControls();
            RefreshProfileList(keepSelection: true);
        };

        BuildLayout();
        Theme.Apply(this);
        LoadMonitorsIntoTarget();
        LoadSettingsIntoControls();
        RefreshProfileList();
        if (_profileList.Items.Count > 0)
        {
            // The form is built at every launch, even when it stays in the tray, and launch
            // must not change the screen: select under _loading, then load without applying.
            _loading = true;
            _profileList.SelectedIndex = 0;
            _loading = false;
            LoadSelectedProfile(apply: false);
        }
        SetEditorEnabled(_editing is not null);
    }

    public void ShowAndFocus()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(Theme.Space6) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        // Left: profile list, its buttons, then app settings. Settings sit here because the
        // editor column fills the 640 px window height on its own.
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Margin = Padding.Empty };
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(_profileList);
        var leftButtons = new TableLayoutPanel { Dock = DockStyle.Fill, Height = Theme.ControlHeight, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, Theme.Space3, 0, 0) };
        leftButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        string[] names = ["Add", "Duplicate", "Delete"];
        EventHandler[] handlers = [(_, _) => AddProfile(), (_, _) => DuplicateProfile(), (_, _) => DeleteProfile()];
        for (int i = 0; i < names.Length; i++)
        {
            leftButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / names.Length));
            var button = Button(names[i], handlers[i]);
            button.AutoSize = false;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 0, i < names.Length - 1 ? Theme.Space2 : 0, 0);
            leftButtons.Controls.Add(button, i, 0);
        }
        left.Controls.Add(leftButtons);
        left.Controls.Add(new Label { Text = "Settings", Font = Theme.Title, AutoSize = true, Margin = new Padding(0, Theme.Space6, 0, Theme.Space2) });
        left.Controls.Add(new Label { Text = "Reset hotkey", ForeColor = Theme.TextMuted, AutoSize = true, Margin = new Padding(0, 0, 0, Theme.Space1) });
        _resetHotkeyBox.Dock = DockStyle.Fill;
        _resetHotkeyBox.Margin = new Padding(0, 0, 0, Theme.Space2);
        left.Controls.Add(_resetHotkeyBox);
        foreach (var check in new[] { _startWithWindows, _showToast, _guard })
        {
            check.Margin = new Padding(0, 2, 0, 2);
            left.Controls.Add(check);
        }
        root.Controls.Add(left, 0, 0);

        // Right: editor
        var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(Theme.Space6, 0, 0, 0) };
        right.Controls.Add(new Label { Text = "Profile", Font = Theme.Title, AutoSize = true, Margin = new Padding(0, 0, 0, Theme.Space2) });
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

        var actions = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(Theme.LabelColumnWidth, Theme.Space2, 0, 0) };
        actions.Controls.Add(Button("Reset", (_, _) => _controller.Reset()));
        right.Controls.Add(actions);
        right.Controls.Add(_status);
        root.Controls.Add(right, 1, 0);

        _nameBox.TextChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Name = _nameBox.Text; _controller.Save(); } };
        _hotkeyBox.ComboChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Hotkey = _hotkeyBox.Combo; SaveSettingsAndRegister(_hotkeyBox); } };
        _targetBox.SelectedIndexChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Target = TargetFromCombo(); _controller.Save(); } };
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
        var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = new Padding(0, 0, Theme.Space2, 0) };
        b.Click += onClick;
        return b;
    }

    private static Control Row(string label, Control input) => FormRow(new Label { Text = label, ForeColor = Theme.TextMuted }, input);

    // One grid for every editor row so labels share one column width and rows share one height.
    private static TableLayoutPanel FormRow(Label label, params Control[] inputs)
    {
        var panel = new TableLayoutPanel { AutoSize = true, ColumnCount = inputs.Length + 1, RowCount = 1, Margin = new Padding(0, 2, 0, 2) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.LabelColumnWidth));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
        label.AutoSize = true;
        label.Anchor = AnchorStyles.Left;
        label.Margin = Padding.Empty;
        panel.Controls.Add(label, 0, 0);
        for (int i = 0; i < inputs.Length; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            inputs[i].Anchor = AnchorStyles.Left;
            inputs[i].Margin = new Padding(0, 0, Theme.Space3, 0);
            panel.Controls.Add(inputs[i], i + 1, 0);
        }
        return panel;
    }

    private Control Slider(string label, GkSlider bar, bool isGamma = false)
    {
        var number = new NumericUpDown
        {
            Minimum = bar.Minimum,
            Maximum = bar.Maximum,
            Width = 70,
            Font = Theme.Value,
            TextAlign = HorizontalAlignment.Right,
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
            SaveIfEditing();
        };

        // Saving on every drag tick would write the file many times a second, so save when
        // the drag or key press ends. A capture loss (Alt+Tab mid-drag) also ends the drag.
        bar.MouseUp += (_, _) => SaveIfEditing();
        bar.KeyUp += (_, _) => SaveIfEditing();
        bar.MouseCaptureChanged += (_, _) => SaveIfEditing();

        return FormRow(new Label { Text = label, Font = Theme.Label, ForeColor = Theme.TextMuted }, bar, number);
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

    private void SetEditorEnabled(bool on)
    {
        _nameBox.Enabled = on;
        _hotkeyBox.Enabled = on;
        _targetBox.Enabled = on;
        foreach (var (bar, number) in _numbers)
        {
            bar.Enabled = on;
            number.Enabled = on;
        }
        if (on && !_controller.Vibrance.IsAvailable)
        {
            _vibrance.Enabled = false;
            _numbers[_vibrance].Enabled = false;
        }
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

    private void LoadSelectedProfile(bool apply = true)
    {
        if (_loading) return;
        var index = _profileList.SelectedIndex;
        if (index < 0 || index >= _controller.Settings.Profiles.Count) { _editing = null; SetEditorEnabled(false); return; }
        _editing = _controller.Settings.Profiles[index];
        SetEditorEnabled(true);

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
        _hotkeyBox.ForeColor = Theme.TextPrimary;
        if (!apply) return;
        // Clicking a profile is the same as pressing its hotkey: the screen changes at once.
        var result = _controller.ApplyProfile(_editing);
        _status.Text = result.Note ?? "";
    }

    private ColorValues CurrentValues() => new(_gamma.Value / 100.0, _brightness.Value, _contrast.Value, _shadows.Value, _highlights.Value, _vibrance.Value);

    private void PushValuesToProfileAndPreview()
    {
        if (_editing is null) return;
        _editing.Values = CurrentValues();
        var result = _controller.Preview(_editing.Values, _editing.Target);
        _status.Text = result.Note ?? "";
    }

    private void SaveIfEditing()
    {
        if (_editing is not null && !_loading) _controller.Save();
    }

    private void SaveSettingsAndRegister(HotkeyCaptureBox? changedBox = null)
    {
        _controller.Save();
        var failed = _controller.RegisterHotkeys();
        var messages = new List<string>();
        if (failed.Count > 0) messages.Add($"Hotkey already used by another app: {string.Join(", ", failed)}. Pick another.");
        if (changedBox is not null)
        {
            // Another profile can still be refused after this one is fixed, so the colour and
            // the no-modifier warning depend only on the edited profile.
            bool isRefused = _editing is not null && failed.Contains(_editing.Name);
            changedBox.ForeColor = isRefused ? Theme.Warning : Theme.TextPrimary;
            if (!isRefused && changedBox.Combo.Length > 0 && !HotkeyCombo.HasModifier(changedBox.Combo)) messages.Add(HotkeyCombo.NoModifierWarning);
        }
        _status.Text = string.Join(" ", messages);
    }

    private void AddProfile()
    {
        var profile = new Profile();
        _controller.Settings.Profiles.Add(profile);
        SaveSettingsAndRegister();
        _profileList.SelectedIndex = _controller.Settings.Profiles.Count - 1;
    }

    private void DuplicateProfile()
    {
        if (_editing is null) return;
        var copy = _editing.Clone();
        copy.Name = _editing.Name + " copy";
        _controller.Settings.Profiles.Add(copy);
        SaveSettingsAndRegister();
        _profileList.SelectedIndex = _controller.Settings.Profiles.Count - 1;
    }

    private void DeleteProfile()
    {
        if (_editing is null) return;
        var index = _controller.Settings.Profiles.IndexOf(_editing);
        _controller.Settings.Profiles.Remove(_editing);
        SaveSettingsAndRegister();
        if (_profileList.Items.Count > 0)
        {
            // The save refreshes the list and keeps the old index. When the new index is the
            // same, the list raises no SelectedIndexChanged, so the editor is rebound here.
            _loading = true;
            _profileList.SelectedIndex = Math.Min(index, _profileList.Items.Count - 1);
            _loading = false;
            LoadSelectedProfile();
        }
        else { _editing = null; SetEditorEnabled(false); }
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
