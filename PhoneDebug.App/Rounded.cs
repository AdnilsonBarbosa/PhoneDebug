using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PhoneDebug.App;

/// <summary>Paths and regions for the rounded controls.</summary>
internal static class Rounded
{
    public static GraphicsPath Path(Rectangle bounds, float radius)
    {
        var d = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f);
        var path = new GraphicsPath();
        if (d <= 0f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}