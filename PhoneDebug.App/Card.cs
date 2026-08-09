using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PhoneDebug.App;

/// <summary>
/// A rounded panel with an optional border, used as the surface that groups
/// related controls (the device block, the action row, the log area).
/// </summary>
internal sealed class Card : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Radius { get; set; } = 12f;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Color.Empty;

    public Card()
    {
        BackColor = Theme.Raised;
        SetStyle(ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.SupportsTransparentBackColor
            | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(1, 1, Width - 3, Height - 3);
        using var path = Rounded.Path(rect, Radius);
        using (var brush = new SolidBrush(BackColor))
            g.FillPath(brush, path);

        if (BorderColor != Color.Empty)
        {
            using var pen = new Pen(BorderColor, 1f);
            g.DrawPath(pen, path);
        }
    }
}