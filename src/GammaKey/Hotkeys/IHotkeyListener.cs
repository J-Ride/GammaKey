namespace GammaKey.Hotkeys;

public interface IHotkeyListener : IDisposable
{
    event Action<int>? Pressed;
    bool Register(int id, HotkeyCombo combo);
    void UnregisterAll();
}
