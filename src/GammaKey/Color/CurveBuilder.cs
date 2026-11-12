using GammaKey.Models;

namespace GammaKey.Color;

public static class CurveBuilder
{
    public const int Size = 256;

    // Shadows and Highlights are knees: every tone on one side of the pivot moves by a constant
    // factor. Highlights down and Shadows up pull tones toward the pivot; the opposite direction
    // pushes them away. The factor stays positive, so each knee keeps the table
    // non-decreasing on its own. At full cut the factor is a half, so with the pivot at zero
    // the whole curve scales to 50 percent white: exactly the lowest top entry Windows accepts.
    private const double KneeStrength = 0.5;

    // Each slider moves a channel by up to 30 percent; Temperature and the RGB slider for the
    // same channel can stack to 60 percent. The clamp is a guard on that sum. At 1.6 the top
    // third of the range already clips to white, so the sliders stop there.
    private const double GainStrength = 0.3;
    private const double GainMin = 1.0 - GainStrength * 2;
    private const double GainMax = 1.0 + GainStrength * 2;

    // Black floor stops at half scale so the table stays inside the Windows gamma clamp even
    // with brightness at maximum.
    private const double FloorStrength = 0.5;

    public static ushort[] Build(ColorValues input)
    {
        var v = input.Clamped();
        var ramp = new ushort[Size * 3];
        double contrastFactor = (v.Contrast + 100) / 100.0;
        double brightnessShift = v.Brightness / 200.0;
        double shadowPivot = v.ShadowPivot / 100.0;
        double shadowFactor = 1.0 - v.Shadows / 100.0 * KneeStrength;
        double highlightPivot = v.HighlightPivot / 100.0;
        double highlightFactor = 1.0 + v.Highlights / 100.0 * KneeStrength;

        double[] gains =
        {
            Gain(v.Red / 100.0 + v.Temperature / 100.0),
            Gain(v.Green / 100.0),
            Gain(v.Blue / 100.0 - v.Temperature / 100.0),
        };
        double floor = v.BlackFloor / 100.0 * FloorStrength;
        var previous = new ushort[3];

        for (int i = 0; i < Size; i++)
        {
            double x = i / 255.0;
            double y = Math.Pow(x, 1.0 / v.Gamma);
            y = (y - 0.5) * contrastFactor + 0.5;
            y += brightnessShift;

            if (y < shadowPivot) y = shadowPivot - (shadowPivot - y) * shadowFactor;
            if (y > highlightPivot) y = highlightPivot + (y - highlightPivot) * highlightFactor;

            for (int c = 0; c < 3; c++)
            {
                double yc = Math.Clamp(y * gains[c], 0.0, 1.0);
                yc = floor + yc * (1.0 - floor);
                var value = (ushort)Math.Round(yc * 65535.0);
                if (value < previous[c]) value = previous[c];
                previous[c] = value;
                ramp[c * Size + i] = value;
            }
        }

        return ramp;
    }

    private static double Gain(double amount) => Math.Clamp(1.0 + GainStrength * amount, GainMin, GainMax);
}
