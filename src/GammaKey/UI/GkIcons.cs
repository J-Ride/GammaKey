using System.Drawing.Drawing2D;

namespace GammaKey.UI;

// GammaKey.Color is a namespace, so the alias keeps Color meaning the drawing type here.
using Color = System.Drawing.Color;

public enum GkIcon { Add, Duplicate, Delete, Import, Export }

// One family: 16 px box, 2 px stroke, round caps, drawn from the mock's SVG paths.
public static class GkIcons
{
    public const int Size = 16;

    public static void Draw(Graphics g, GkIcon icon, Rectangle bounds, Color color)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        // Paint-time DPI: the button bounds arrive scaled, the glyph must grow with them.
        float k = g.DpiX / 96f;
        float size = Size * k;
        float x = bounds.Left + (bounds.Width - size) / 2f;
        float y = bounds.Top + (bounds.Height - size) / 2f;
        using var pen = new Pen(color, 2f * k) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        // Glyphs are authored on a 24-unit grid like the SVGs; scale to the device size.
        float s = size / 24f;
        PointF P(float gx, float gy) => new(x + gx * s, y + gy * s);

        switch (icon)
        {
            case GkIcon.Add:
                g.DrawLine(pen, P(12, 5), P(12, 19));
                g.DrawLine(pen, P(5, 12), P(19, 12));
                break;
            case GkIcon.Delete:
                g.DrawLine(pen, P(6, 6), P(18, 18));
                g.DrawLine(pen, P(18, 6), P(6, 18));
                break;
            case GkIcon.Duplicate:
                using (var front = Theme.RoundedPath(Rectangle.Round(new RectangleF(x + 8 * s, y + 4 * s, 13 * s, 13 * s)), (int)(2.5f * s)))
                    g.DrawPath(pen, front);
                // Back sheet: open L behind the front square.
                g.DrawLines(pen, new[] { P(4, 8), P(4, 17), P(7, 20), P(16, 20) });
                g.DrawLine(pen, P(14.5f, 8), P(14.5f, 13));
                g.DrawLine(pen, P(12, 10.5f), P(17, 10.5f));
                break;
            case GkIcon.Import:
                DrawDoor(g, pen, P);
                g.DrawLine(pen, P(21, 12), P(9, 12));
                g.DrawLines(pen, new[] { P(13, 8), P(9, 12), P(13, 16) });
                break;
            case GkIcon.Export:
                DrawDoor(g, pen, P);
                g.DrawLine(pen, P(9, 12), P(21, 12));
                g.DrawLines(pen, new[] { P(17, 8), P(21, 12), P(17, 16) });
                break;
        }
        g.SmoothingMode = old;
    }

    // The rounded "door" frame shared by Import and Export: open on the right where the arrow passes.
    private static void DrawDoor(Graphics g, Pen pen, Func<float, float, PointF> P)
    {
        g.DrawLines(pen, new[] { P(13, 9), P(13, 6), P(11, 4), P(7, 4), P(5, 6), P(5, 18), P(7, 20), P(11, 20), P(13, 18), P(13, 15) });
    }
}
