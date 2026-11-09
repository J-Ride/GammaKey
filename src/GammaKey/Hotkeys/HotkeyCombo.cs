namespace GammaKey.Hotkeys;

public readonly record struct HotkeyCombo(uint Modifiers, Keys Key)
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;

    private static readonly Keys[] ModifierKeys =
    {
        Keys.ControlKey, Keys.LControlKey, Keys.RControlKey, Keys.Control,
        Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey, Keys.Shift,
        Keys.Menu, Keys.LMenu, Keys.RMenu, Keys.Alt,
        Keys.LWin, Keys.RWin, Keys.None,
    };

    public static bool TryParse(string text, out HotkeyCombo combo)
    {
        combo = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        uint modifiers = 0;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl":
                case "control": modifiers |= ModControl; break;
                case "alt": modifiers |= ModAlt; break;
                case "shift": modifiers |= ModShift; break;
                case "win":
                case "windows": modifiers |= ModWin; break;
                default: return false;
            }
        }

        var last = parts[^1];
        if (!Enum.TryParse<Keys>(last, ignoreCase: true, out var key)) return false;
        if ((key & Keys.Modifiers) != 0 || key == Keys.KeyCode || !Enum.IsDefined(key)) return false;
        if (ModifierKeys.Contains(key)) return false;

        combo = new HotkeyCombo(modifiers, key);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>(5);
        if ((Modifiers & ModControl) != 0) parts.Add("Ctrl");
        if ((Modifiers & ModAlt) != 0) parts.Add("Alt");
        if ((Modifiers & ModShift) != 0) parts.Add("Shift");
        if ((Modifiers & ModWin) != 0) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join('+', parts);
    }
}
