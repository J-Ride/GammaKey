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
    public void LiteralNullFileKeepsBackup()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, "null");
        var store = new ProfileStore(path);

        store.Load();

        Assert.True(File.Exists(path + ".corrupt"));
    }

    [Fact]
    public void LiteralNullFileFallsBackToDefaults()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, "null");
        var store = new ProfileStore(path);

        var settings = store.Load();

        Assert.Equal(new[] { "Dark Room", "Bright Outdoors", "Normal" }, settings.Profiles.Select(p => p.Name));
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

    [Fact]
    public void ShowWindowOnLaunchRoundTrips()
    {
        var store = new ProfileStore(PathFor("profiles.json"));
        store.Save(new AppSettings { ShowWindowOnLaunch = true });

        Assert.True(store.Load().ShowWindowOnLaunch);
    }

    [Fact]
    public void FileWithoutShowWindowOnLaunchLoadsAsFalse()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, """{ "version": 1, "profiles": [] }""");

        Assert.False(new ProfileStore(path).Load().ShowWindowOnLaunch);
    }

    [Fact]
    public void HotkeyPassthroughRoundTrips()
    {
        var store = new ProfileStore(PathFor("profiles.json"));
        store.Save(new AppSettings { HotkeyPassthrough = true });

        Assert.True(store.Load().HotkeyPassthrough);
    }

    [Fact]
    public void FileWithoutHotkeyPassthroughLoadsAsFalse()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, """{ "version": 1, "profiles": [] }""");

        Assert.False(new ProfileStore(path).Load().HotkeyPassthrough);
    }

    [Fact]
    public void TryParseReturnsNullForInvalidJson()
    {
        Assert.Null(ProfileStore.TryParse("{ not json"));
    }

    [Fact]
    public void TryParseClampsAndFillsDefaults()
    {
        var settings = ProfileStore.TryParse("""{ "profiles": [ { "name": "Wild", "gamma": 9.0, "hotkey": null } ] }""");

        var p = Assert.Single(settings!.Profiles);
        Assert.Equal(2.8, p.Values.Gamma);
        Assert.Equal("", p.Hotkey);
        Assert.False(string.IsNullOrEmpty(p.Id));
    }

    [Fact]
    public void MissingBalanceFieldsLoadAsNormal()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, """
            { "version": 1, "profiles": [ { "name": "Old", "gamma": 1.2, "brightness": 5, "contrast": 0, "shadows": 0, "highlights": 0, "vibrance": 50 } ] }
            """);
        var store = new ProfileStore(path);

        var p = Assert.Single(store.Load().Profiles);

        Assert.Equal(new ColorValues(1.2, 5, 0, 0, 0, 50), p.Values);
    }

    [Fact]
    public void OutOfRangeBalanceFieldsAreClampedOnLoad()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, """
            { "version": 1, "profiles": [ { "name": "Wild", "temperature": 500, "red": -500, "green": 500, "blue": -500, "blackFloor": 500 } ] }
            """);
        var store = new ProfileStore(path);

        var p = Assert.Single(store.Load().Profiles);

        Assert.Equal(ColorValues.Normal with { Temperature = 100, Red = -100, Green = 100, Blue = -100, BlackFloor = 100 }, p.Values);
    }

    [Fact]
    public void BalanceFieldsRoundTripThroughSave()
    {
        var path = PathFor("profiles.json");
        var store = new ProfileStore(path);
        var settings = ProfileStore.Defaults();
        settings.Profiles[0].Values = ColorValues.Normal with { Temperature = 30, Red = -10, Green = 5, Blue = 20, BlackFloor = 40 };
        store.Save(settings);

        var p = store.Load().Profiles[0];

        Assert.Equal(ColorValues.Normal with { Temperature = 30, Red = -10, Green = 5, Blue = 20, BlackFloor = 40 }, p.Values);
    }

    [Fact]
    public void FileWithoutPivotsLoadsThemAtTheMiddle()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, """
            { "version": 1, "profiles": [ { "name": "Old", "gamma": 1.2, "brightness": 5, "contrast": 0, "shadows": 40, "highlights": -20, "vibrance": 50 } ] }
            """);
        var store = new ProfileStore(path);

        var p = Assert.Single(store.Load().Profiles);

        Assert.Equal(50, p.ShadowPivot);
        Assert.Equal(50, p.HighlightPivot);
    }

    [Fact]
    public void OutOfRangePivotsAreClampedOnLoad()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, """
            { "version": 1, "profiles": [ { "name": "Wild", "shadowPivot": 150, "highlightPivot": -5 } ] }
            """);
        var store = new ProfileStore(path);

        var p = Assert.Single(store.Load().Profiles);

        Assert.Equal(100, p.ShadowPivot);
        Assert.Equal(0, p.HighlightPivot);
    }

    [Fact]
    public void PivotsRoundTripThroughSave()
    {
        var path = PathFor("profiles.json");
        var store = new ProfileStore(path);
        var settings = store.Load();
        settings.Profiles[0].ShadowPivot = 30;
        settings.Profiles[0].HighlightPivot = 70;
        store.Save(settings);

        var reloaded = new ProfileStore(path).Load();

        Assert.Equal(30, reloaded.Profiles[0].ShadowPivot);
        Assert.Equal(70, reloaded.Profiles[0].HighlightPivot);
    }
}
