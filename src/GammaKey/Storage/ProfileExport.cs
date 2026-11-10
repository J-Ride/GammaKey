using GammaKey.Models;

namespace GammaKey.Storage;

public static class ProfileExport
{
    // Same shape as profiles.json with one profile and default settings, so Import reads it as is.
    public static AppSettings Single(Profile profile) => new() { Profiles = { profile } };

    public static string SuggestedFileName(string profileName)
    {
        var kept = string.Concat(profileName.Split(Path.GetInvalidFileNameChars())).Trim();
        return (kept.Length == 0 ? "profile" : kept) + ".json";
    }
}
