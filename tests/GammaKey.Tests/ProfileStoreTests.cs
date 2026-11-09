using GammaKey.Models;
using GammaKey.Storage;
using Xunit;

namespace GammaKey.Tests;

public class ProfileStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gammakey-tests").FullName;
    private string PathFor(string name) => System.IO.Path.Combine(_dir, name);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void MissingFileGivesThreeDefaultProfiles()
    {
        var store = new ProfileStore(PathFor("profiles.json"));

        var settings = store.Load();

        Assert.Equal(new[] { "Dark Room", "Bright Outdoors", "Normal" }, settings.Profiles.Select(p => p.Name));
    }

    [Fact]
    public void DefaultsHaveNoHotkeysAssigned()
    {
        var settings = ProfileStore.Defaults();

        Assert.Equal("", settings.ResetHotkey);
        Assert.All(settings.Profiles, p => Assert.Equal("", p.Hotkey));
    }

    [Fact]
    public void SaveThenLoadRoundTrips()
    {
        var store = new ProfileStore(PathFor("profiles.json"));
        var settings = new AppSettings { ResetHotkey = "F12", GuardEnabled = false };
        settings.Profiles.Add(new Profile
        {
            Name = "Cave",
            Hotkey = "Ctrl+F9",
            Target = MonitorTarget.ForMonitor("LG ULTRAGEAR"),
            Values = new ColorValues(1.6, 20, -10, 35, 0, 65),
        });

        store.Save(settings);
        var loaded = store.Load();

        var p = Assert.Single(loaded.Profiles);
        Assert.Equal("Cave", p.Name);
        Assert.Equal("Ctrl+F9", p.Hotkey);
        Assert.Equal("monitor", p.Target.Mode);
        Assert.Equal("LG ULTRAGEAR", p.Target.MonitorName);
        Assert.Equal(new ColorValues(1.6, 20, -10, 35, 0, 65), p.Values);
        Assert.Equal("F12", loaded.ResetHotkey);
        Assert.False(loaded.GuardEnabled);
    }

    [Fact]
    public void OutOfRangeValuesAreClampedOnLoad()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, """
            { "version": 1, "profiles": [ { "name": "Wild", "gamma": 9.0, "brightness": 500, "contrast": -500, "shadows": 101, "highlights": -101, "vibrance": 3 } ] }
            """);
        var store = new ProfileStore(path);

        var p = Assert.Single(store.Load().Profiles);

        Assert.Equal(new ColorValues(2.8, 100, -100, 100, -100, 50), p.Values);
    }

    [Fact]
    public void CorruptFileFallsBackToDefaultsAndKeepsBackup()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, "{ not json");
        var store = new ProfileStore(path);

        var settings = store.Load();

        Assert.Equal(3, settings.Profiles.Count);
        Assert.True(File.Exists(path + ".corrupt"));
    }

    [Fact]
    public void NullProfilesArrayLoadsAsEmptyList()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, """{ "version": 1, "profiles": null }""");
        var store = new ProfileStore(path);

        var settings = store.Load();

        Assert.NotNull(settings.Profiles);
        Assert.Empty(settings.Profiles);
    }

    [Fact]
    public void NullProfileEntriesAreDropped()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, """{ "version": 1, "profiles": [ null, { "name": "Kept" } ] }""");
        var store = new ProfileStore(path);

        var p = Assert.Single(store.Load().Profiles);

        Assert.Equal("Kept", p.Name);
    }

    [Fact]
    public void NullHotkeyLoadsAsEmptyString()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, """{ "version": 1, "profiles": [ { "name": "A", "hotkey": null } ] }""");
        var store = new ProfileStore(path);

        var p = Assert.Single(store.Load().Profiles);

        Assert.Equal("", p.Hotkey);
    }

    [Fact]
    public void SaveLeavesNoTempFileBehind()
    {
        var store = new ProfileStore(PathFor("profiles.json"));

        store.Save(ProfileStore.Defaults());

        Assert.Single(Directory.GetFiles(_dir));
    }
}
