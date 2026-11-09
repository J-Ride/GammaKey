namespace GammaKey.Models;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public string ResetHotkey { get; set; } = "";
    public bool StartWithWindows { get; set; }
    public bool ShowWindowOnLaunch { get; set; }
    public bool HotkeyPassthrough { get; set; }
    public bool GuardEnabled { get; set; } = true;
    public bool ShowToast { get; set; } = true;
    public List<Profile> Profiles { get; set; } = new();
}
