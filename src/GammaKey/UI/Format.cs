using System.Globalization;
using GammaKey.Models;

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

    public static string ProfileSummary(Profile p)
    {
        var parts = new List<string>(3);
        if (p.Gamma != 1.0) parts.Add("gamma " + Signed((decimal)p.Gamma, 2));
        if (p.Brightness != 0) parts.Add("brightness " + Signed(p.Brightness, 0));
        if (p.Contrast != 0) parts.Add("contrast " + Signed(p.Contrast, 0));
        if (p.Shadows != 0) parts.Add("shadows " + Signed(p.Shadows, 0));
        if (p.Highlights != 0) parts.Add("highlights " + Signed(p.Highlights, 0));
        if (p.Vibrance != ColorValues.VibranceMin) parts.Add("vibrance " + p.Vibrance);
        return parts.Count == 0 ? "neutral curve" : string.Join(" · ", parts);
    }
}
