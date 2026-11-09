namespace GammaKey.Models;

public sealed record ColorValues(
    double Gamma,
    int Brightness,
    int Contrast,
    int Shadows,
    int Highlights,
    int Vibrance)
{
    public const double GammaMin = 0.3;
    public const double GammaMax = 2.8;
    public const int VibranceMin = 50;
    public const int VibranceMax = 100;

    public static readonly ColorValues Normal = new(1.0, 0, 0, 0, 0, VibranceMin);

    public ColorValues Clamped() => new(
        Math.Clamp(Gamma, GammaMin, GammaMax),
        Math.Clamp(Brightness, -100, 100),
        Math.Clamp(Contrast, -100, 100),
        Math.Clamp(Shadows, -100, 100),
        Math.Clamp(Highlights, -100, 100),
        Math.Clamp(Vibrance, VibranceMin, VibranceMax));
}
