using System.Text.Json;

namespace GammaKey.Storage;

public sealed class Baseline
{
    public DateTime CapturedAt { get; set; } = DateTime.Now;
    public Dictionary<string, ushort[]> Ramps { get; set; } = new();
    public Dictionary<string, int> VibranceLevels { get; set; } = new();
}

public sealed class BaselineStore
{
    private readonly string _path;

    public BaselineStore(string path)
    {
        _path = path;
    }

    public bool Exists => File.Exists(_path);

    public Baseline? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            return JsonSerializer.Deserialize<Baseline>(File.ReadAllText(_path), ProfileStore.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(Baseline baseline)
    {
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(baseline, ProfileStore.JsonOptions));
        File.Move(temp, _path, overwrite: true);
    }

    public void Delete()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
