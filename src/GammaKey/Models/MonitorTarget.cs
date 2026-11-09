namespace GammaKey.Models;

public sealed class MonitorTarget
{
    public const string Primary = "primary";
    public const string All = "all";
    public const string Monitor = "monitor";

    public string Mode { get; set; } = Primary;
    public string? MonitorName { get; set; }

    public static MonitorTarget ForPrimary() => new() { Mode = Primary };
    public static MonitorTarget ForAll() => new() { Mode = All };
    public static MonitorTarget ForMonitor(string name) => new() { Mode = Monitor, MonitorName = name };
}
