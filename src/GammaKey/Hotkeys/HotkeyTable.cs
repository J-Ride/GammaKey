namespace GammaKey.Hotkeys;

/// Maps (modifiers, key) to a hotkey id for the keyboard-hook listener.
public sealed class HotkeyTable
{
    private readonly Dictionary<int, (uint Modifiers, Keys Key)> _byId = new();

    public void Register(int id, HotkeyCombo combo) => _byId[id] = (combo.Modifiers, combo.Key);

    public void UnregisterAll() => _byId.Clear();

    public int? Match(uint modifiers, Keys key)
    {
        foreach (var (id, combo) in _byId)
            if (combo.Modifiers == modifiers && combo.Key == key) return id;
        return null;
    }
}
