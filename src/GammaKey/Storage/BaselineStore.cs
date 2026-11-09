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
    // The native SetDeviceGammaRamp call reads exactly 768 entries (3 channels x 256).
    private const int RampLength = 256 * 3;

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
            var baseline = JsonSerializer.Deserialize<Baseline>(File.ReadAllText(_path), ProfileStore.JsonOptions);
            if (baseline is null) return null;

            baseline.Ramps ??= new();
            baseline.VibranceLevels ??= new();
            foreach (var key in baseline.Ramps.Where(r => r.Value is null || r.Value.Length != RampLength).Select(r => r.Key).ToList())
                baseline.Ramps.Remove(key);
            return baseline;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
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
