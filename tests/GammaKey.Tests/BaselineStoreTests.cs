using GammaKey.Storage;
using Xunit;

namespace GammaKey.Tests;

public class BaselineStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gammakey-baseline").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void LoadReturnsNullWhenMissing()
    {
        var store = new BaselineStore(Path.Combine(_dir, "baseline.json"));

        Assert.Null(store.Load());
    }

    [Fact]
    public void RoundTripsRampsAndVibrance()
    {
        var store = new BaselineStore(Path.Combine(_dir, "baseline.json"));
        var ramp = Enumerable.Range(0, 768).Select(i => (ushort)(i % 256 * 257)).ToArray();
        var baseline = new Baseline();
        baseline.Ramps[@"\\.\DISPLAY1"] = ramp;
        baseline.VibranceLevels[@"\\.\DISPLAY1"] = 12;

        store.Save(baseline);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(ramp, loaded!.Ramps[@"\\.\DISPLAY1"]);
        Assert.Equal(12, loaded.VibranceLevels[@"\\.\DISPLAY1"]);
    }

    [Fact]
    public void NullMembersLoadAsEmptyDictionaries()
    {
        var path = Path.Combine(_dir, "baseline.json");
        File.WriteAllText(path, """{ "capturedAt": "2026-01-01T00:00:00", "ramps": null, "vibranceLevels": null }""");
        var store = new BaselineStore(path);

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.Ramps);
        Assert.Empty(loaded.Ramps);
        Assert.NotNull(loaded.VibranceLevels);
        Assert.Empty(loaded.VibranceLevels);
    }

    [Fact]
    public void RampsWithWrongLengthAreDropped()
    {
        var store = new BaselineStore(Path.Combine(_dir, "baseline.json"));
        var baseline = new Baseline();
        baseline.Ramps["ok"] = new ushort[768];
        baseline.Ramps["bad"] = new ushort[10];
        store.Save(baseline);

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Contains("ok", loaded!.Ramps.Keys);
        Assert.DoesNotContain("bad", loaded.Ramps.Keys);
    }

    [Fact]
    public void CorruptFileLoadsAsNull()
    {
        var path = Path.Combine(_dir, "baseline.json");
        File.WriteAllText(path, "{ not json");
        var store = new BaselineStore(path);

        Assert.Null(store.Load());
    }

    [Fact]
    public void DeleteRemovesFile()
    {
        var path = Path.Combine(_dir, "baseline.json");
        var store = new BaselineStore(path);
        store.Save(new Baseline());

        store.Delete();

        Assert.False(File.Exists(path));
        Assert.False(store.Exists);
    }
}
