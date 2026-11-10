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

    // Colour balance. Init properties, not positional, so old callers and old profile files
    // keep working; a missing field is 0, which is normal.
    public int Temperature { get; init; }
    public int Red { get; init; }
    public int Green { get; init; }
    public int Blue { get; init; }
    public int BlackFloor { get; init; }

    public static readonly ColorValues Normal = new(1.0, 0, 0, 0, 0, VibranceMin);

    public ColorValues Clamped() => this with
    {
        Gamma = Math.Clamp(Gamma, GammaMin, GammaMax),
        Brightness = Math.Clamp(Brightness, -100, 100),
        Contrast = Math.Clamp(Contrast, -100, 100),
        Shadows = Math.Clamp(Shadows, -100, 100),
        Highlights = Math.Clamp(Highlights, -100, 100),
        Vibrance = Math.Clamp(Vibrance, VibranceMin, VibranceMax),
        Temperature = Math.Clamp(Temperature, -100, 100),
        Red = Math.Clamp(Red, -100, 100),
        Green = Math.Clamp(Green, -100, 100),
        Blue = Math.Clamp(Blue, -100, 100),
        BlackFloor = Math.Clamp(BlackFloor, 0, 100),
    };
}
