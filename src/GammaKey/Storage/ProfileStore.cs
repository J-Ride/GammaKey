using System.Text.Json;
using System.Text.Json.Serialization;
using GammaKey.Models;

namespace GammaKey.Storage;

public sealed class ProfileStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Path { get; }

    public ProfileStore(string path)
    {
        Path = path;
    }

    public AppSettings Load()
    {
        if (!File.Exists(Path)) return Defaults();

        var settings = TryParse(File.ReadAllText(Path));
        if (settings is null)
        {
            // A half-written or hand-edited file must never lose the user's profiles silently,
            // so keep a copy next to it before falling back.
            File.Copy(Path, Path + ".corrupt", overwrite: true);
            return Defaults();
        }
        return settings;
    }

    // Shared by Load and by profile import, so an imported file gets the same clamping and
    // null fixes as the app's own file.
    public static AppSettings? TryParse(string json)
    {
        AppSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        if (settings is null) return null;

        settings.Profiles ??= new List<Profile>();
        settings.Profiles.RemoveAll(p => p is null);

        foreach (var profile in settings.Profiles)
        {
            profile.Hotkey ??= "";
            profile.Name ??= "";
            profile.Values = profile.Values.Clamped();
            profile.Target ??= MonitorTarget.ForPrimary();
            if (string.IsNullOrWhiteSpace(profile.Id)) profile.Id = Guid.NewGuid().ToString("N");
        }
        settings.ResetHotkey ??= "";
        return settings;
    }

    public void Save(AppSettings settings)
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temp, Path, overwrite: true);
    }

    public static AppSettings Defaults() => new()
    {
        Profiles =
        {
            new Profile { Name = "Dark Room", Values = new ColorValues(1.5, 15, 0, 40, 0, 60) },
            new Profile { Name = "Bright Outdoors", Values = new ColorValues(0.85, 0, 10, 0, -20, 55) },
            new Profile { Name = "Normal", Values = ColorValues.Normal },
        },
    };
}
