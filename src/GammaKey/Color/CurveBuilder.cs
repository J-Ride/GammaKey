using GammaKey.Models;

namespace GammaKey.Color;

public static class CurveBuilder
{
    public const int Size = 256;

    // Shadows and highlights are limited to a quarter of full scale so their slope never
    // cancels the base curve; combined with the running max below this keeps the table
    // non-decreasing, which Windows requires.
    private const double RegionStrength = 0.25;

    public static ushort[] Build(ColorValues input)
    {
        var v = input.Clamped();
        var ramp = new ushort[Size * 3];
        double contrastFactor = (v.Contrast + 100) / 100.0;
        double brightnessShift = v.Brightness / 200.0;
        double shadows = v.Shadows / 100.0 * RegionStrength;
        double highlights = v.Highlights / 100.0 * RegionStrength;
        ushort previous = 0;

        for (int i = 0; i < Size; i++)
        {
            double x = i / 255.0;
            double y = Math.Pow(x, 1.0 / v.Gamma);
            y = (y - 0.5) * contrastFactor + 0.5;
            y += brightnessShift;

            double shadowWeight = Math.Clamp(1.0 - 2.0 * x, 0.0, 1.0);
            y += shadows * shadowWeight * shadowWeight;

            double highlightWeight = Math.Clamp(2.0 * x - 1.0, 0.0, 1.0);
            y += highlights * highlightWeight * highlightWeight;

            var value = (ushort)Math.Round(Math.Clamp(y, 0.0, 1.0) * 65535.0);
            if (value < previous) value = previous;
            previous = value;

            ramp[i] = value;
            ramp[Size + i] = value;
            ramp[2 * Size + i] = value;
        }

        return ramp;
    }
}
