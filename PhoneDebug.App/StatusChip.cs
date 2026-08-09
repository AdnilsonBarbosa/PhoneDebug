using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PhoneDebug.App;

/// <summary>
/// A rounded tag with a coloured status dot. Used for the connection state
/// and for small metadata labels such as the version.
/// </summary>
internal sealed class StatusChip : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color DotColor { get; set; } = Theme.Muted;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowDot { get; set; } = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Fill { get; set; } = Color.FromArgb(240, 243, 247);

    public StatusChip()
    {
        AutoSize = true;
        Font = Theme.Small;
        Height = 22;
        MinimumSize = new Size(0, 22);
        SetStyle(ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint, true);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font);
        var width = text.Width + (ShowDot ? 22 : 14);
        return new Size(width, Math.Max(22, Height));
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Math.Max(1, Height - 1));
        using var path = Rounded.Path(rect, Math.Min(11f, Height / 2f));
        using (var brush = new SolidBrush(Fill))
            g.FillPath(brush, path);

        if (ShowDot)
        {
            var dot = new Rectangle(8, Height / 2 - 3, 6, 6);
            using var dotBrush = new SolidBrush(DotColor);
            g.FillEllipse(dotBrush, dot);
        }

        var textRect = new Rectangle(ShowDot ? 18 : 8, 0, Width - (ShowDot ? 22 : 12), Height);
        TextRenderer.DrawText(g, Text, Font, textRect, ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}