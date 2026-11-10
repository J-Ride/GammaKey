using System.Text.Json.Serialization;

namespace GammaKey.Models;

public sealed class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New profile";
    public string Hotkey { get; set; } = "";
    public MonitorTarget Target { get; set; } = MonitorTarget.ForPrimary();
    public double Gamma { get; set; } = 1.0;
    public int Brightness { get; set; }
    public int Contrast { get; set; }
    public int Shadows { get; set; }
    public int Highlights { get; set; }
    public int Vibrance { get; set; } = ColorValues.VibranceMin;
    public int Temperature { get; set; }
    public int Red { get; set; }
    public int Green { get; set; }
    public int Blue { get; set; }
    public int BlackFloor { get; set; }

    [JsonIgnore]
    public ColorValues Values
    {
        get => new(Gamma, Brightness, Contrast, Shadows, Highlights, Vibrance)
        {
            Temperature = Temperature,
            Red = Red,
            Green = Green,
            Blue = Blue,
            BlackFloor = BlackFloor,
        };
        set
        {
            Gamma = value.Gamma;
            Brightness = value.Brightness;
            Contrast = value.Contrast;
            Shadows = value.Shadows;
            Highlights = value.Highlights;
            Vibrance = value.Vibrance;
            Temperature = value.Temperature;
            Red = value.Red;
            Green = value.Green;
            Blue = value.Blue;
            BlackFloor = value.BlackFloor;
        }
    }

    public Profile Clone() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = Name,
        Hotkey = "",
        Target = new MonitorTarget { Mode = Target.Mode, MonitorName = Target.MonitorName },
        Values = Values,
    };

    // The editor works on a copy so Revert is a reload. Unlike Clone, the copy keeps the Id (the
    // active tag and the list row match on it) and the Hotkey (an edit must not clear it).
    public Profile CopyForEditing() => new()
    {
        Id = Id,
        Name = Name,
        Hotkey = Hotkey,
        Target = new MonitorTarget { Mode = Target.Mode, MonitorName = Target.MonitorName },
        Values = Values,
    };

    public void CopyContentFrom(Profile source)
    {
        Name = source.Name;
        Hotkey = source.Hotkey;
        Target = new MonitorTarget { Mode = source.Target.Mode, MonitorName = source.Target.MonitorName };
        Values = source.Values;
    }

    // The editor's dirty check. Id is left out so a working copy compares equal to its source.
    public bool HasSameContentAs(Profile other) =>
        Name == other.Name
        && Hotkey == other.Hotkey
        && Target.Mode == other.Target.Mode
        && Target.MonitorName == other.Target.MonitorName
        && Values == other.Values;
}
