namespace GammaKey.Display;

public sealed record MonitorInfo(string DeviceName, string FriendlyName, bool IsPrimary, bool IsHdrOn)
{
    public string Label => IsHdrOn ? $"{FriendlyName} (HDR)" : FriendlyName;
}
