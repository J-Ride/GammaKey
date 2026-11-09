using GammaKey.Hotkeys;

namespace GammaKey.UI;

public sealed class HotkeyCaptureBox : TextBox
{
    private string _combo = "";

    public event EventHandler? ComboChanged;

    public HotkeyCaptureBox()
    {
        ReadOnly = true;
        Cursor = Cursors.Hand;
        ShortcutsEnabled = false;
        UpdateText();
    }

    public string Combo
    {
        get => _combo;
        set
        {
            _combo = value;
            UpdateText();
        }
    }

    // Every edit auto-saves, so Escape must not clear a hotkey; it goes to the form so a
    // dialog's CancelButton still closes it.
    protected override bool IsInputKey(Keys keyData) =>
        keyData is not (Keys.Tab or (Keys.Tab | Keys.Shift)) && (keyData & Keys.KeyCode) != Keys.Escape;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;

        if (e.KeyCode is Keys.Back or Keys.Delete)
        {
            SetCombo("");
            return;
        }

        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin or Keys.Escape) return;

        uint mods = 0;
        if (e.Control) mods |= HotkeyCombo.ModControl;
        if (e.Alt) mods |= HotkeyCombo.ModAlt;
        if (e.Shift) mods |= HotkeyCombo.ModShift;
        SetCombo(new HotkeyCombo(mods, e.KeyCode).ToString());
    }

    private void SetCombo(string combo)
    {
        _combo = combo;
        UpdateText();
        ComboChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateText()
    {
        Text = _combo.Length == 0 ? "Click, then press keys (Backspace clears)" : _combo;
    }
}
