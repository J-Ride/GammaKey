using System.Globalization;

namespace GammaKey.UI;

public static class Format
{
    public static string Signed(decimal value, int places)
    {
        var text = value.ToString("F" + places, CultureInfo.InvariantCulture);
        return places == 0 && value > 0 ? "+" + text : text;
    }

    // "D1" is how Keys names the digit row; the chip should read "1".
    public static string[] HotkeyParts(string combo)
    {
        if (combo.Length == 0) return Array.Empty<string>();
        var parts = combo.Split('+');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length == 2 && parts[i][0] == 'D' && char.IsDigit(parts[i][1])) parts[i] = parts[i][1..];
        return parts;
    }
}
