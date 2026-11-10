using System.Text.Json;

using GammaKey.App;
using GammaKey.Display;
using GammaKey.Hotkeys;
using GammaKey.Models;
using GammaKey.Storage;

namespace GammaKey.UI;

public sealed class MainForm : Form
{
    private readonly AppController _controller;
    private readonly ProfileList _profileList = new() { Dock = DockStyle.Fill };
    private readonly Label _profileCount = new() { AutoSize = true, Font = Theme.MonoSmall, ForeColor = Theme.TextDim, Margin = Padding.Empty };
    private const string ProfileFileFilter = "GammaKey profiles (*.json)|*.json";

    private readonly TextBox _nameBox = new() { Font = Theme.Label };
    private readonly GkHotkeyBox _hotkeyBox = new();
    private readonly GkDropdown _targetBox = new();
    private readonly Label _heading = new() { AutoSize = true, Font = Theme.Title, Margin = new Padding(0, 0, Theme.Space2, 0) };
    private readonly Label _activeTag = new() { AutoSize = true, Font = Theme.MonoSmall, ForeColor = Theme.Accent, Text = "ACTIVE", Padding = new Padding(5, 2, 5, 2), BorderStyle = BorderStyle.FixedSingle, Visible = false, Margin = new Padding(0, 4, 0, 0) };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Theme.Warning, Font = Theme.Caption, MaximumSize = new Size(600, 0), Margin = new Padding(0, Theme.Space2, 0, 0) };
    private readonly Label _vibranceNote = new() { AutoSize = true, ForeColor = Theme.TextDim, Font = Theme.Caption, Margin = new Padding(Theme.LabelColumnWidth + Theme.Space3, 0, 0, Theme.Space1), Text = "Vibrance is set in the driver and survives alt-tab. Needs an NVIDIA GPU." };

    private readonly GkChip _monitorChip = new() { Dot = DotState.Ok };
    private readonly GkChip _modeChip = new();
    private readonly GkChip _hdrChip = new() { Label = "HDR" };
    private readonly GkChip _hotkeysChip = new() { Label = "Hotkeys", Dot = DotState.Ok };
    private readonly Label _appliedLine = new() { AutoSize = true, Font = Theme.MonoSmall, ForeColor = Theme.Ok, Text = "● not applied yet", Margin = new Padding(0, 0, Theme.Space4, 0) };
    private readonly Label _hookLine = new() { AutoSize = true, Font = Theme.MonoSmall, ForeColor = Theme.TextDim };
    private readonly Label _savedLine = new() { AutoSize = true, Font = Theme.MonoSmall, ForeColor = Theme.TextDim, Text = "profiles.json · saved" };

    private readonly GkSlider _gamma = new() { Minimum = 30, Maximum = 280, Origin = 100, ScaleFormatter = v => Format.Signed(v / 100m, 2) };
    private readonly GkSlider _brightness = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _contrast = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _shadows = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _highlights = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _vibrance = new() { Minimum = 50, Maximum = 100, Origin = 50, ScaleFormatter = v => v.ToString() };
    private readonly GkSlider _temperature = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _red = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _green = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _blue = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _blackFloor = new() { Minimum = 0, Maximum = 100, Origin = 0, ScaleFormatter = v => v.ToString() };
    private readonly Dictionary<GkSlider, GkNumber> _numbers = new();
    private readonly List<GkIconButton> _profileButtons = new();

    // _saved is the profile inside Settings.Profiles; _editing is its working copy. Edits and
    // previews touch _editing only, so Revert is a fresh copy and nothing is written until Apply.
    private Profile? _saved;
    private Profile? _editing;
    private readonly Button _applyButton = new() { Text = "Apply", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = Padding.Empty, Enabled = false };
    private readonly Button _revertButton = new() { Text = "Revert", AutoSize = true, MinimumSize = new Size(72, Theme.ControlHeight), Margin = new Padding(0, 0, Theme.Space2, 0), Enabled = false };
    private string? _activeProfileId;
    private bool _loading;

    public MainForm(AppController controller)
    {
        _controller = controller;
        // Setting AutoScaleMode on a live form runs the DPI scale pass at once, on an empty form.
        // Suspend first so the pass runs once, over the whole tree, at ResumeLayout below.
        SuspendLayout();
        Text = "GammaKey";
        WindowChrome.Apply(this);
        Width = 960;
        Height = 680;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = TrayIcon.CreateIcon();
        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += OnFormClosing;
        _controller.SettingsChanged += () =>
        {
            if (IsDisposed) return;
            RefreshProfileList(keepSelection: true);
            RefreshStatus();
        };
        _controller.ProfileApplied += (p, _) =>
        {
            if (IsDisposed) return;
            _activeProfileId = p.Id;
            _appliedLine.Text = $"● applied {DateTime.Now:HH:mm:ss}";
            _activeTag.Visible = _editing is not null && _editing.Id == _activeProfileId;
        };
        _controller.ResetApplied += _ =>
        {
            if (IsDisposed) return;
            _activeProfileId = null;
            _appliedLine.Text = $"● restored {DateTime.Now:HH:mm:ss}";
            _activeTag.Visible = false;
        };

        BuildLayout();
        Theme.Apply(this);
        Theme.StylePrimary(_applyButton);
        // Every control now exists; a control added after this line would not be scaled.
        ResumeLayout(true);
        // Set after the scale pass, in device pixels, so the pass can neither skip nor double it.
        MinimumSize = new Size(Theme.Scale(860, DeviceDpi), Theme.Scale(600, DeviceDpi));
        LoadMonitorsIntoTarget();
        RefreshProfileList();
        RefreshStatus();
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
        RefreshDirty();
    }

    public void ShowAndFocus()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        MinimumSize = new Size(Theme.Scale(860, e.DeviceDpiNew), Theme.Scale(600, e.DeviceDpiNew));
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = Theme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(root);

        // Status strip
        var strip = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, BackColor = Theme.Panel, Padding = new Padding(Theme.Space4, Theme.Space2, Theme.Space4, Theme.Space2), Margin = Padding.Empty };
        strip.Paint += (_, e) => { using var p = new Pen(Theme.Line); e.Graphics.DrawLine(p, 0, strip.Height - 1, strip.Width, strip.Height - 1); };
        strip.Controls.AddRange(new Control[] { _monitorChip, _modeChip, _hdrChip, _hotkeysChip });
        root.Controls.Add(strip, 0, 0);

        // Body: rail + editor
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty, BackColor = Theme.Background };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 272));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(body, 0, 1);

        var rail = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = Theme.Panel };
        rail.Paint += (_, e) => { using var p = new Pen(Theme.Line); e.Graphics.DrawLine(p, rail.Width - 1, 0, rail.Width - 1, rail.Height); };
        rail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rail.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var railHead = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, BackColor = Theme.Panel, Padding = new Padding(Theme.Space4, Theme.Space3, Theme.Space4, Theme.Space1), Margin = Padding.Empty };
        railHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        railHead.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        railHead.Controls.Add(new Label { Text = "PROFILES", Font = Theme.Eyebrow, ForeColor = Theme.TextMuted, AutoSize = true, Margin = Padding.Empty }, 0, 0);
        railHead.Controls.Add(_profileCount, 1, 0);
        rail.Controls.Add(railHead, 0, 0);
        rail.Controls.Add(_profileList, 0, 1);

        var railActions = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Bottom, BackColor = Theme.Panel, Padding = new Padding(Theme.Space3, Theme.Space2, Theme.Space3, Theme.Space3), Margin = Padding.Empty };
        railActions.Paint += (_, e) => { using var p = new Pen(Theme.Line); e.Graphics.DrawLine(p, 0, 0, railActions.Width, 0); };
        var iconRow = new TableLayoutPanel { ColumnCount = 5, Width = 272 - 2 * Theme.Space3 - 1, Height = Theme.ControlHeight, Margin = new Padding(0, 0, 0, Theme.Space2), BackColor = Theme.Panel };
        for (int i = 0; i < 5; i++) iconRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        iconRow.Controls.Add(IconButton(GkIcon.Add, "Add profile", (_, _) => AddProfile(), isPrimary: true), 0, 0);
        iconRow.Controls.Add(IconButton(GkIcon.Duplicate, "Duplicate profile", (_, _) => DuplicateProfile()), 1, 0);
        iconRow.Controls.Add(IconButton(GkIcon.Delete, "Delete profile", (_, _) => DeleteProfile(), isDanger: true), 2, 0);
        iconRow.Controls.Add(IconButton(GkIcon.Import, "Import profiles", (_, _) => ImportProfiles()), 3, 0);
        iconRow.Controls.Add(IconButton(GkIcon.Export, "Export profiles", (_, _) => ExportProfiles()), 4, 0);
        railActions.Controls.Add(iconRow);
        railActions.Controls.Add(WideButton("Settings", (_, _) => OpenSettings()));
        var restore = WideButton("Restore monitor", (_, _) => _controller.Reset());
        restore.Margin = new Padding(0, Theme.Space2, 0, 0);
        railActions.Controls.Add(restore);
        rail.Controls.Add(railActions, 0, 2);
        body.Controls.Add(rail, 0, 0);

        // Editor
        var editor = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(Theme.Space4, Theme.Space3, Theme.Space4, Theme.Space4), BackColor = Theme.Background };
        var head = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 0, 0, Theme.Space2), BackColor = Theme.Background };
        head.Controls.Add(_heading);
        head.Controls.Add(_activeTag);
        editor.Controls.Add(head);

        var profileGroup = new GkGroup("Profile");
        profileGroup.Content.Controls.Add(FormRow(new Label { Text = "Name" }, _nameBox));
        var two = new TableLayoutPanel { ColumnCount = 2, Height = Theme.ControlHeight + 10, Margin = Padding.Empty, BackColor = Theme.Panel };
        two.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        two.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var hotkeyRow = FormRow(new Label { Text = "Hotkey" }, _hotkeyBox);
        var targetRow = FormRow(new Label { Text = "Monitors" }, _targetBox);
        hotkeyRow.Dock = targetRow.Dock = DockStyle.Fill;
        two.Controls.Add(hotkeyRow, 0, 0);
        two.Controls.Add(targetRow, 1, 0);
        profileGroup.Content.Controls.Add(two);
        // Right aligned: RightToLeft flow puts the first control at the right edge, so Apply
        // sits at the far right with Revert to its left. GkGroup widens the row to the group.
        var editActions = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = false, Height = Theme.ControlHeight + 4, Margin = new Padding(0, Theme.Space1, 0, 0), BackColor = Theme.Panel };
        editActions.Controls.Add(_applyButton);
        editActions.Controls.Add(_revertButton);
        _applyButton.Click += (_, _) => ApplyEdits();
        _revertButton.Click += (_, _) => RevertEdits();
        profileGroup.Content.Controls.Add(editActions);
        editor.Controls.Add(profileGroup);

        var curveGroup = new GkGroup("Curve", "GDI gamma ramp");
        curveGroup.Content.Controls.Add(Slider("Gamma", _gamma, isGamma: true));
        curveGroup.Content.Controls.Add(Slider("Brightness", _brightness));
        curveGroup.Content.Controls.Add(Slider("Contrast", _contrast));
        curveGroup.Content.Controls.Add(Slider("Shadows", _shadows));
        curveGroup.Content.Controls.Add(Slider("Highlights", _highlights));
        editor.Controls.Add(curveGroup);

        var balanceGroup = new GkGroup("Balance", "GDI gamma ramp");
        balanceGroup.Content.Controls.Add(Slider("Warm/cool", _temperature));
        balanceGroup.Content.Controls.Add(Slider("Red", _red));
        balanceGroup.Content.Controls.Add(Slider("Green", _green));
        balanceGroup.Content.Controls.Add(Slider("Blue", _blue));
        balanceGroup.Content.Controls.Add(Slider("Black floor", _blackFloor));
        editor.Controls.Add(balanceGroup);

        var colorGroup = new GkGroup("Color", "NVIDIA only");
        colorGroup.Content.Controls.Add(Slider("Vibrance", _vibrance));
        colorGroup.Content.Controls.Add(_vibranceNote);
        editor.Controls.Add(colorGroup);

        editor.Controls.Add(_status);
        body.Controls.Add(editor, 1, 0);

        // Groups stretch to the editor width; FlowLayoutPanel does not do that itself.
        editor.Layout += (_, _) =>
        {
            int w = editor.ClientSize.Width - editor.Padding.Horizontal;
            foreach (Control c in editor.Controls) if (c is GkGroup) c.Width = w;
        };

        // Status bar
        var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Theme.Panel, Padding = new Padding(Theme.Space3, 0, Theme.Space3, 0), Margin = Padding.Empty };
        bar.Paint += (_, e) => { using var p = new Pen(Theme.Line); e.Graphics.DrawLine(p, 0, 0, bar.Width, 0); };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach (var l in new[] { _appliedLine, _hookLine, _savedLine }) l.Anchor = AnchorStyles.Left;
        bar.Controls.Add(_appliedLine, 0, 0);
        bar.Controls.Add(_hookLine, 1, 0);
        bar.Controls.Add(_savedLine, 2, 0);
        root.Controls.Add(bar, 0, 2);

        _nameBox.TextChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Name = _nameBox.Text; RefreshDirty(); } };
        _hotkeyBox.ComboChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Hotkey = _hotkeyBox.Combo; _hotkeyBox.IsRefused = false; RefreshDirty(); } };
        // A new target previews the working copy there; it is saved and made active only by Apply.
        _targetBox.SelectedIndexChanged += (_, _) => { if (_editing is not null && !_loading) { _editing.Target = TargetFromCombo(); _status.Text = _controller.Preview(_editing.Values, _editing.Target).Note ?? ""; RefreshDirty(); } };
        _profileList.SelectedIndexChanged += (_, _) => LoadSelectedProfile();

        if (!_controller.Vibrance.IsAvailable)
        {
            _vibrance.Enabled = false;
            _vibranceNote.Text = _controller.Vibrance.UnavailableReason ?? "Vibrance needs an NVIDIA graphics card";
        }
    }

    private GkIconButton IconButton(GkIcon icon, string name, EventHandler onClick, bool isPrimary = false, bool isDanger = false)
    {
        var b = new GkIconButton(icon, name) { IsPrimary = isPrimary, IsDanger = isDanger, Anchor = AnchorStyles.None, Margin = Padding.Empty };
        b.Click += onClick;
        _profileButtons.Add(b);
        return b;
    }

    private static Button WideButton(string text, EventHandler onClick)
    {
        var b = new Button { Text = text, Width = 272 - 2 * Theme.Space3 - 1, Height = Theme.ControlHeight, Margin = Padding.Empty, Font = Theme.Label };
        b.Click += onClick;
        return b;
    }

    // One grid per row so labels share a column and the input stretches to the row's right edge.
    private static TableLayoutPanel FormRow(Label label, Control input)
    {
        var panel = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 2, 0, 2), Height = Theme.ControlHeight + 6, BackColor = Theme.Panel };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.LabelColumnWidth));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        label.AutoSize = true;
        label.ForeColor = Theme.TextMuted;
        label.Anchor = AnchorStyles.Left;
        label.Margin = Padding.Empty;
        panel.Controls.Add(label, 0, 0);
        input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        input.Margin = new Padding(0, 0, Theme.Space3, 0);
        panel.Controls.Add(input, 1, 0);
        return panel;
    }

    private TableLayoutPanel Slider(string label, GkSlider bar, bool isGamma = false)
    {
        var number = new GkNumber
        {
            Minimum = isGamma ? 0.3m : bar.Minimum,
            Maximum = isGamma ? 2.8m : bar.Maximum,
            Increment = isGamma ? 0.01m : 1m,
            DecimalPlaces = isGamma ? 2 : 0,
        };
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

        var panel = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 2, 0, 2), Height = bar.Height + 4, BackColor = Theme.Panel };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.LabelColumnWidth));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72 + Theme.Space3));
        var text = new Label { Text = label, AutoSize = true, ForeColor = Theme.TextMuted, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 4, 0, 0) };
        panel.Controls.Add(text, 0, 0);
        bar.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        bar.Margin = new Padding(0, 0, Theme.Space3, 0);
        panel.Controls.Add(bar, 1, 0);
        number.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        number.Margin = new Padding(Theme.Space3, 0, 0, 0);
        panel.Controls.Add(number, 2, 0);
        return panel;
    }

    private void RefreshStatus()
    {
        var monitors = _controller.Display.GetMonitors();
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
        if (primary is null)
        {
            _monitorChip.Value = "no monitor";
            _monitorChip.Dot = DotState.Dim;
            _modeChip.Visible = false;
        }
        else
        {
            _monitorChip.Value = primary.FriendlyName;
            _monitorChip.Label = "primary";
            var mode = DisplayModeNative.Get(primary.DeviceName);
            _modeChip.Visible = mode is not null;
            if (mode is not null) _modeChip.Value = $"{mode.Width}×{mode.Height} · {mode.RefreshHz} Hz";
            _hdrChip.Value = primary.IsHdrOn ? "on" : "off";
            _hdrChip.Dot = primary.IsHdrOn ? DotState.Ok : DotState.Dim;
        }
        _hotkeysChip.Value = $"{_controller.ArmedHotkeys} armed";
        _hotkeysChip.Dot = _controller.ArmedHotkeys > 0 ? DotState.Ok : DotState.Dim;
        _hookLine.Text = _controller.Settings.HotkeyPassthrough ? "hook: pass-through" : "hook: normal";
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
        // Duplicate and Delete need a selection; Add, Import and Export never do.
        _profileButtons[1].Enabled = on;
        _profileButtons[2].Enabled = on;
        if (!on) _activeTag.Visible = false;
    }

    private void RefreshProfileList(bool keepSelection = false)
    {
        var selected = _profileList.SelectedIndex;
        _loading = true;
        _profileList.Bind(_controller.Settings.Profiles, keepSelection ? selected : -1);
        _loading = false;
        _profileCount.Text = _controller.Settings.Profiles.Count.ToString();
    }

    private void LoadSelectedProfile(bool apply = true)
    {
        if (_loading) return;
        var index = _profileList.SelectedIndex;
        if (index < 0 || index >= _controller.Settings.Profiles.Count) { ClearEditor(); return; }
        _saved = _controller.Settings.Profiles[index];
        _editing = _saved.CopyForEditing();
        SetEditorEnabled(true);
        _activeTag.Visible = _editing.Id == _activeProfileId;
        ShowEditing();
        _hotkeyBox.IsRefused = false;
        RefreshDirty();
        if (!apply) return;
        // Clicking a profile is the same as pressing its hotkey: the screen changes at once.
        var result = _controller.ApplyProfile(_saved);
        _status.Text = result.Note ?? "";
    }

    // Pushes the working copy into every editor control without firing their change handlers.
    private void ShowEditing()
    {
        if (_editing is null) return;
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
        _temperature.Value = v.Temperature;
        _red.Value = v.Red;
        _green.Value = v.Green;
        _blue.Value = v.Blue;
        _blackFloor.Value = v.BlackFloor;
        foreach (var (bar, number) in _numbers)
            number.Value = ReferenceEquals(bar, _gamma) ? bar.Value / 100m : bar.Value;
        _loading = false;
    }

    private void ClearEditor()
    {
        _saved = null;
        _editing = null;
        SetEditorEnabled(false);
        RefreshDirty();
    }

    private bool IsDirty => _saved is not null && _editing is not null && !_editing.HasSameContentAs(_saved);

    // The heading, Apply and Revert all follow the dirty state, so they change in one place.
    private void RefreshDirty()
    {
        bool dirty = IsDirty;
        _applyButton.Enabled = dirty;
        _revertButton.Enabled = dirty;
        _heading.Text = _editing is null ? "No profile selected" : (dirty ? _editing.Name + " *" : _editing.Name);
    }

    private void ApplyEdits()
    {
        if (_saved is null || _editing is null) return;
        _saved.CopyContentFrom(_editing);
        SaveSettingsAndRegister(_hotkeyBox);
        var result = _controller.ApplyProfile(_saved);
        if (result.Note is not null) _status.Text = string.Join(" ", new[] { _status.Text, result.Note }.Where(s => s.Length > 0));
        RefreshDirty();
    }

    private void RevertEdits()
    {
        if (_saved is null) return;
        _editing = _saved.CopyForEditing();
        ShowEditing();
        _hotkeyBox.IsRefused = false;
        // The screen followed the working copy while it was edited; put the saved look back.
        _status.Text = _controller.Preview(_saved.Values, _saved.Target).Note ?? "";
        RefreshDirty();
    }

    private ColorValues CurrentValues() => new(_gamma.Value / 100.0, _brightness.Value, _contrast.Value, _shadows.Value, _highlights.Value, _vibrance.Value)
    {
        Temperature = _temperature.Value,
        Red = _red.Value,
        Green = _green.Value,
        Blue = _blue.Value,
        BlackFloor = _blackFloor.Value,
    };

    private void PushValuesToProfileAndPreview()
    {
        if (_editing is null) return;
        _editing.Values = CurrentValues();
        var result = _controller.Preview(_editing.Values, _editing.Target);
        _status.Text = result.Note ?? "";
        RefreshDirty();
    }

    private void SaveSettingsAndRegister(GkHotkeyBox? changedBox = null)
    {
        // Register before Save so the armed-hotkeys chip refreshed by SettingsChanged is current.
        var failed = _controller.RegisterHotkeys();
        _controller.Save();
        var messages = new List<string>();
        if (failed.Count > 0) messages.Add($"Hotkey already used by another app: {string.Join(", ", failed)}. Pick another.");
        if (changedBox is not null)
        {
            // Another profile can still be refused after this one is fixed, so the colour and
            // the no-modifier warning depend only on the edited profile.
            bool isRefused = _editing is not null && failed.Contains(_editing.Name);
            changedBox.IsRefused = isRefused;
            if (!isRefused && changedBox.Combo.Length > 0 && !HotkeyCombo.HasModifier(changedBox.Combo) && !_controller.Settings.HotkeyPassthrough) messages.Add(HotkeyCombo.NoModifierWarning);
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
        if (_saved is null) return;
        var copy = _saved.Clone();
        copy.Name = _saved.Name + " copy";
        _controller.Settings.Profiles.Add(copy);
        SaveSettingsAndRegister();
        _profileList.SelectedIndex = _controller.Settings.Profiles.Count - 1;
    }

    private void DeleteProfile()
    {
        if (_saved is null) return;
        var index = _controller.Settings.Profiles.IndexOf(_saved);
        _controller.Settings.Profiles.Remove(_saved);
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
        else ClearEditor();
    }

    private void ExportProfiles()
    {
        using var dialog = new SaveFileDialog { Filter = ProfileFileFilter, FileName = "GammaKey-profiles.json", DefaultExt = "json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            // Same shape as profiles.json, so an export and a copied profiles.json both import.
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_controller.Settings, ProfileStore.JsonOptions));
            _status.Text = $"Exported {_controller.Settings.Profiles.Count} profiles.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status.Text = "Could not write that file.";
        }
    }

    private void ImportProfiles()
    {
        using var dialog = new OpenFileDialog { Filter = ProfileFileFilter };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        AppSettings? incoming = null;
        try
        {
            incoming = ProfileStore.TryParse(File.ReadAllText(dialog.FileName));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // An unreadable file gets the same "Could not read" message as bad JSON, below.
        }
        if (incoming is null || incoming.Profiles.Count == 0)
        {
            _status.Text = "Could not read that file. Is it a GammaKey profiles file?";
            return;
        }

        var result = ProfileImport.Merge(_controller.Settings.Profiles, incoming.Profiles, _controller.Settings.ResetHotkey);
        int firstNew = _controller.Settings.Profiles.Count;
        _controller.Settings.Profiles.AddRange(result.Added);
        SaveSettingsAndRegister();
        _profileList.SelectedIndex = firstNew;
        _status.Text = result.HotkeysCleared switch
        {
            0 => $"Imported {result.Added.Count} profiles.",
            1 => $"Imported {result.Added.Count} profiles. 1 hotkey cleared because it was in use.",
            var k => $"Imported {result.Added.Count} profiles. {k} hotkeys cleared because they were in use.",
        };
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

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_controller);
        dialog.ShowDialog(this);
    }
}
