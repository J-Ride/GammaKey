using GammaKey.Models;

namespace GammaKey.Color;

public static class CurveBuilder
{
    public const int Size = 256;

    // Shadows and highlights are limited to a quarter of full scale so their slope never
    // cancels the base curve; combined with the running max below this keeps the table
    // non-decreasing, which Windows requires.
    private const double RegionStrength = 0.25;

    // A channel gain of more than 30 percent clips mid tones to white; the clamp keeps the
    // combined Temperature plus RGB slider inside that limit.
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
        double shadows = v.Shadows / 100.0 * RegionStrength;
        double highlights = v.Highlights / 100.0 * RegionStrength;

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

            double shadowWeight = Math.Clamp(1.0 - 2.0 * x, 0.0, 1.0);
            y += shadows * shadowWeight * shadowWeight;

            double highlightWeight = Math.Clamp(2.0 * x - 1.0, 0.0, 1.0);
            y += highlights * highlightWeight * highlightWeight;

            for (int c = 0; c < 3; c++)
            {
                double yc = y * gains[c];
                yc = floor + yc * (1.0 - floor);
                var value = (ushort)Math.Round(Math.Clamp(yc, 0.0, 1.0) * 65535.0);
                if (value < previous[c]) value = previous[c];
                previous[c] = value;
                ramp[c * Size + i] = value;
            }
        }

        return ramp;
    }

    private static double Gain(double amount) => Math.Clamp(1.0 + GainStrength * amount, GainMin, GainMax);
}
