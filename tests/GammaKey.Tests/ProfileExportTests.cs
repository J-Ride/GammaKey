using System.Text.Json;
using GammaKey.Models;
using GammaKey.Storage;
using Xunit;

namespace GammaKey.Tests;

public class ProfileExportTests
{
    [Fact]
    public void SingleExportHoldsExactlyThatProfile()
    {
        var profile = new Profile { Name = "Cave", Hotkey = "Ctrl+F9" };

        var exported = ProfileExport.Single(profile);

        Assert.Equal(new[] { "Cave" }, exported.Profiles.Select(p => p.Name));
    }

    [Fact]
    public void SingleExportRoundTripsThroughTryParse()
    {
        var profile = new Profile { Name = "Cave", Hotkey = "Ctrl+F9", Values = new ColorValues(1.6, 20, -10, 35, 0, 65) { BlackFloor = 20 } };

        var json = JsonSerializer.Serialize(ProfileExport.Single(profile), ProfileStore.JsonOptions);
        var parsed = ProfileStore.TryParse(json);

        Assert.Equal(profile.Values, Assert.Single(parsed!.Profiles).Values);
    }

    [Fact]
    public void SingleExportUsesDefaultSettings()
    {
        var exported = ProfileExport.Single(new Profile());

        Assert.Equal("", exported.ResetHotkey);
    }

    [Fact]
    public void SuggestedFileNameIsTheProfileNameWithJson()
    {
        Assert.Equal("Cave.json", ProfileExport.SuggestedFileName("Cave"));
    }

    [Fact]
    public void SuggestedFileNameDropsCharactersWindowsRefuses()
    {
        Assert.Equal("Night Mode.json", ProfileExport.SuggestedFileName("Night: Mode?"));
    }

    [Fact]
    public void SuggestedFileNameFallsBackWhenNothingIsLeft()
    {
        Assert.Equal("profile.json", ProfileExport.SuggestedFileName("<>:\"/\\|?*"));
    }
}
