using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PhoneDebug.App;

/// <summary>
/// A flat button with rounded corners, drawn by hand so it gets hover and
/// pressed states the stock WinForms button cannot match.
/// </summary>
internal sealed class RoundedButton : Button
{
    private bool _hover;
    private bool _down;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverColor { get; set; } = Color.Empty;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color PressedColor { get; set; } = Color.Empty;

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _down = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left)
        {
            _down = true;
            Invalidate();
        }
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _down = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Rounded.Path(rect, Math.Min(10f, Height / 2f));

        var fill = Enabled
            ? _down && PressedColor != Color.Empty ? PressedColor
            : _hover && HoverColor != Color.Empty ? HoverColor
            : BackColor
            : Color.FromArgb(229, 231, 235);

        using (var brush = new SolidBrush(fill))
            g.FillPath(brush, path);

        var text = Enabled ? ForeColor : Color.FromArgb(156, 163, 175);
        TextRenderer.DrawText(g, Text, Font,
            rect, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}