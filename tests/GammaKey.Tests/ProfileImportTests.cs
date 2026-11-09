using GammaKey.Models;
using GammaKey.Storage;
using Xunit;

namespace GammaKey.Tests;

public class ProfileImportTests
{
    private static Profile P(string name, string hotkey = "") => new() { Name = name, Hotkey = hotkey };

    [Fact]
    public void EveryImportedProfileGetsAFreshId()
    {
        var incoming = new[] { P("A"), P("B") };

        var result = ProfileImport.Merge(Array.Empty<Profile>(), incoming, "");

        Assert.DoesNotContain(result.Added, p => p.Id == incoming[0].Id || p.Id == incoming[1].Id);
    }

    [Fact]
    public void NoClashesKeepsNamesAndHotkeys()
    {
        var result = ProfileImport.Merge(new[] { P("Normal", "F1") }, new[] { P("Cave", "F2") }, "");

        var p = Assert.Single(result.Added);
        Assert.Equal(("Cave", "F2", 0), (p.Name, p.Hotkey, result.HotkeysCleared));
    }

    [Fact]
    public void NameClashGetsImportedSuffix()
    {
        var result = ProfileImport.Merge(new[] { P("Normal") }, new[] { P("normal") }, "");

        Assert.Equal("normal (imported)", Assert.Single(result.Added).Name);
    }

    [Fact]
    public void SecondNameClashGetsNumberedSuffix()
    {
        var existing = new[] { P("Normal"), P("Normal (imported)") };

        var result = ProfileImport.Merge(existing, new[] { P("Normal") }, "");

        Assert.Equal("Normal (imported 2)", Assert.Single(result.Added).Name);
    }

    [Fact]
    public void HotkeyClashWithExistingProfileIsCleared()
    {
        var result = ProfileImport.Merge(new[] { P("Normal", "Ctrl+F1") }, new[] { P("Cave", "Ctrl+F1") }, "");

        Assert.Equal(("", 1), (Assert.Single(result.Added).Hotkey, result.HotkeysCleared));
    }

    [Fact]
    public void HotkeyClashWithResetHotkeyIsCleared()
    {
        var result = ProfileImport.Merge(Array.Empty<Profile>(), new[] { P("Cave", "F12") }, "F12");

        Assert.Equal("", Assert.Single(result.Added).Hotkey);
    }

    [Fact]
    public void DuplicateHotkeyInsideImportKeepsFirstAndClearsSecond()
    {
        var result = ProfileImport.Merge(Array.Empty<Profile>(), new[] { P("A", "F5"), P("B", "F5") }, "");

        Assert.Equal(new[] { "F5", "" }, result.Added.Select(p => p.Hotkey));
    }

    [Fact]
    public void IncomingProfilesAreNotMutated()
    {
        var incoming = new[] { P("Normal", "F1") };

        ProfileImport.Merge(new[] { P("Normal", "F1") }, incoming, "");

        Assert.Equal(("Normal", "F1"), (incoming[0].Name, incoming[0].Hotkey));
    }
}
