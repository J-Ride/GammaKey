using GammaKey.Models;

namespace GammaKey.Storage;

public static class ProfileImport
{
    public sealed record Result(IReadOnlyList<Profile> Added, int HotkeysCleared);

    // Import only ever adds. A friend's file must never replace a tuned profile, and two
    // profiles must never share a hotkey, so clashes get a suffix or a cleared key.
    public static Result Merge(IReadOnlyList<Profile> existing, IReadOnlyList<Profile> incoming, string resetHotkey)
    {
        var names = new HashSet<string>(existing.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        var hotkeys = new HashSet<string>(existing.Select(p => p.Hotkey).Where(h => h.Length > 0), StringComparer.OrdinalIgnoreCase);
        if (resetHotkey.Length > 0) hotkeys.Add(resetHotkey);

        var added = new List<Profile>(incoming.Count);
        int cleared = 0;
        foreach (var source in incoming)
        {
            var profile = source.Clone();
            profile.Hotkey = source.Hotkey;
            profile.Name = UniqueName(source.Name, names);
            names.Add(profile.Name);
            if (profile.Hotkey.Length > 0 && !hotkeys.Add(profile.Hotkey))
            {
                profile.Hotkey = "";
                cleared++;
            }
            added.Add(profile);
        }
        return new Result(added, cleared);
    }

    private static string UniqueName(string name, HashSet<string> taken)
    {
        if (!taken.Contains(name)) return name;
        var candidate = name + " (imported)";
        for (int n = 2; taken.Contains(candidate); n++) candidate = $"{name} (imported {n})";
        return candidate;
    }
}
